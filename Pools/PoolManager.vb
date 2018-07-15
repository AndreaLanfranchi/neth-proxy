' =======================================================================================
'
'   This file is part of neth-proxy.
'
'   neth-proxy is free software: you can redistribute it and/or modify
'   it under the terms Of the GNU General Public License As published by
'   the Free Software Foundation, either version 3 Of the License, Or
'   (at your option) any later version.
'
'   neth-proxy is distributed In the hope that it will be useful,
'   but WITHOUT ANY WARRANTY; without even the implied warranty Of
'   MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
'   GNU General Public License For more details.
'
'   You should have received a copy Of the GNU General Public License
'   along with neth-proxy.  If not, see < http://www.gnu.org/licenses/ >.
'
' =======================================================================================

Imports nethproxy.Core
Imports nethproxy.Sockets
Imports System.Json
Imports System.Threading

Namespace Pools

    Public Class PoolManager


#Region " Fields"

        ' References to singletons
        Private _settings As Settings = App.Instance.Settings
        Private _telemetry As Telemetry = App.Instance.Telemetry

        Private _tskNetworkAvailable As Task
        Private _ctsNetworkAvailable As CancellationTokenSource

        ' All pools
        Private _poolsQueue As Queue(Of Pool) = New Queue(Of Pool)

        ' Pool and Mining jobs related members
        Private _poolStatus As PoolStatus = PoolStatus.NotConnected
        Private _currentPool As Pool
        Private _stratumMode As StratumModeEnum = StratumModeEnum.Undefined

        Private _jobHeaders As New SlidingQueue(Of String)(5)                           ' Keeps track of last 5 jobs received
        Public Property CurrentJob As Core.Job
        Private _currentDiff As Double = 0

        ' Devfee
        Private _devFeeStartedOn As DateTime = DateTime.MinValue
        Private _devAddress As String = String.Empty


        ' Connection Socket
        Private WithEvents _socket As New AsyncSocket("Pool")

        ' Lock and context
        Private _context As String = "Pool"
        Private _lockObj As New Object
        Private _isRunning As Boolean = False

        ' Timers
        Private WithEvents _jobTimeoutTimer As Timers.Timer
        Private WithEvents _responseTimeoutTimer As Timers.Timer
        Private WithEvents _devFeeIntervalTimer As New Timers.Timer

        ' The queue of submissions
        Private _submissionsQueue As New Concurrent.ConcurrentQueue(Of SubmissionEntry)

        ''' <summary>
        ''' Represent a share submission entry
        ''' </summary>
        Private Class SubmissionEntry
            Public TimeStamp As DateTime
            Public OriginClient As Clients.Client
            Public OriginId As Integer
        End Class

#End Region

#Region " Properties"

        ''' <summary>
        ''' Gets the actively connected pool
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ActivePool As String
            Get
                ' May throw if not connected
                If Not IsConnected Then
                    Return "Not Connected"
                Else
                    Try
                        Return (_currentPool.Host + ":" + _socket.RemoteEndPoint.Port.ToString)
                    Catch ex As Exception
                        Return "Not connected"
                    End Try
                End If
            End Get
        End Property

        ''' <summary>
        ''' Gets the time elapsed on current connected pool
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ConnectionDuration As TimeSpan
            Get
                If _socket Is Nothing Then Return New TimeSpan(0, 0, 0)
                Return _socket.ConnectionDuration
            End Get
        End Property

        ''' <summary>
        ''' Gets wether or not the underlying socket is connected
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IsConnected As Boolean
            Get
                Return _poolStatus.HasBitFlag(PoolStatus.Connected)
            End Get
        End Property

        ''' <summary>
        ''' Gets wether or not we're Subscribed
        ''' </summary>
        ''' <returns>True / False</returns>
        Public ReadOnly Property IsSubscribed As Boolean
            Get
                Return _poolStatus.HasBitFlag(PoolStatus.Subscribed)
            End Get
        End Property

        ''' <summary>
        ''' Gets wether or not we're Authorized
        ''' </summary>
        ''' <returns>True / False</returns>
        Public ReadOnly Property IsAuthorized As Boolean
            Get
                Return _poolStatus.HasBitFlag(PoolStatus.Authorized)
            End Get
        End Property

        ''' <summary>
        ''' Gets a reference to currently working pool
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property CurrentPool As Pool
            Get
                Return _currentPool
            End Get
        End Property

        ''' <summary>
        ''' Gets the Queue of currently configured pools
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property PoolsQueue As Queue(Of Pool)
            Get
                Return _poolsQueue
            End Get
        End Property

        ''' <summary>
        ''' Gets Stratum Login For Current Pool
        ''' </summary>
        ''' <returns>A String</returns>
        Public ReadOnly Property StratumLogin As String
            Get

                If _currentPool Is Nothing Then
                    Return String.Empty
                End If

                If Not String.IsNullOrEmpty(_devAddress) Then
                    Return _devAddress
                Else
                    Return _currentPool.StratumLogin
                End If

            End Get
        End Property

        ''' <summary>
        ''' Gets current stratum mode
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property StratumMode As StratumModeEnum
            Get
                Return _stratumMode
            End Get
        End Property

        ''' <summary>
        ''' Whether or not pool is on dev fee
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IsFeeOn As Boolean
            Get
                Return Not String.IsNullOrEmpty(_devAddress)
            End Get
        End Property

#End Region

#Region " Constructor"

        Public Sub New()
        End Sub

#End Region

#Region " Start / Stop"

        ''' <summary>
        ''' Starts the pool
        ''' </summary>
        Public Sub Start()

            If _isRunning Then Return
            _isRunning = True
            Connect()

        End Sub

        ''' <summary>
        ''' Stops the pool
        ''' </summary>
        Public Sub [Stop]()

            _isRunning = False
            Disconnect()

            If _tskNetworkAvailable IsNot Nothing Then
                Try
                    _ctsNetworkAvailable.Cancel()
                Catch ex As Exception

                End Try
            End If

        End Sub

#End Region

#Region " Events"

        ''' <summary>
        ''' Fires whenever a new job is received from the pool
        ''' </summary>
        Public Event EventNewJobReceived(ByVal JsonJob As JsonObject)

#End Region

#Region " Timers Handlers"

        ''' <summary>
        ''' Checks the delay from last job is not greater than --work-timeout
        ''' </summary>
        Private Sub OnJobTimeoutTimerElapsed() Handles _jobTimeoutTimer.Elapsed

            If Not IsConnected Then Return
            Logger.Log(1, String.Format("No new job from pool in {0} seconds. Disconnecting ...", _settings.PoolsWorkTimeout), _context)
            ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf Disconnect))

        End Sub

        ''' <summary>
        ''' Checks the delay of each response to solution submission
        ''' </summary>
        Private Sub OnResponseTimeoutTimerElapsed() Handles _responseTimeoutTimer.Elapsed

            If _submissionsQueue.IsEmpty Then Return
            Logger.Log(1, String.Format("Response time from Pool above {0:N0} ms. Disconnect !", _settings.PoolsResponseTimeout), _context)
            ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf Disconnect))

        End Sub

#End Region

#Region " Methods"

        ''' <summary>
        ''' Starts the donation fee period
        ''' </summary>
        ''' <param name="seconds">Number of seconds of duration</param>
        Public Sub StartDevFee(seconds As Double)

            ' At least a 30 seconds run
            If _currentPool Is Nothing OrElse IsAuthorized = False OrElse seconds < 30 Then Return

            If _devFeeStartedOn <> DateTime.MinValue OrElse String.IsNullOrEmpty(_devAddress) = False Then
                Return
            Else
                _devFeeIntervalTimer.Interval = seconds * 1000
                _devFeeIntervalTimer.AutoReset = False
                _devFeeIntervalTimer.Enabled = False
            End If
            Dim p As String = $"{_currentPool.Host}:{_socket.RemoteEndPoint.Port}".ToLower()
            Dim d As String = GetDevAddress(p)
            If String.IsNullOrEmpty(d) Then
                Logger.Log(1, "DevFee not authorized. Switching to --no-fee mode.")
                App.Instance.Settings.NoFee = True
                _devFeeStartedOn = DateTime.MinValue
                _devAddress = String.Empty
                Return
            Else
                _devAddress = d
            End If


            Select Case StratumMode

                Case StratumModeEnum.Stratum

                    If _currentPool.IsFeeAuthorized = False Then

                        Dim jReq As New JsonObject From {
                                    New KeyValuePair(Of String, JsonValue)("id", 3),
                                    New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                    New KeyValuePair(Of String, JsonValue)("method", "mining.authorize"),
                                    New KeyValuePair(Of String, JsonValue)("params", New JsonArray({d, "x"}))}
                        SendMessage(jReq.ToString())

                    End If

                Case StratumModeEnum.Ethproxy

                    Dim jReq As New JsonObject From {
                                    New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                    New KeyValuePair(Of String, JsonValue)("id", 1),
                                    New KeyValuePair(Of String, JsonValue)("method", "eth_submitLogin"),
                                    New KeyValuePair(Of String, JsonValue)("params", New JsonArray From {d})
                                    }
                    SendMessage(jReq.ToString())

            End Select

        End Sub

        ''' <summary>
        ''' Stops the donation fee period
        ''' </summary>
        Public Sub StopDevFee() Handles _devFeeIntervalTimer.Elapsed

            If _devFeeStartedOn = DateTime.MinValue Then Return
            Dim ranForSeconds As Double = DateTime.Now.Subtract(_devFeeStartedOn).TotalSeconds
            _telemetry.DonationDuration = _telemetry.DonationDuration + CLng(ranForSeconds)
            _devFeeStartedOn = DateTime.MinValue
            _devAddress = String.Empty
            _devFeeIntervalTimer.Stop()
            _devFeeIntervalTimer.Enabled = False

            Logger.Log(1, $"DevFee stopped. Ran for {ranForSeconds.ToString} seconds.", _context)

            ' Restore normal operations
            Select Case StratumMode
                Case StratumModeEnum.Stratum

                    ' Actually nothing to do here
                    'Dim jReq As New JsonObject From {
                    '            New KeyValuePair(Of String, JsonValue)("id", 3),
                    '            New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                    '            New KeyValuePair(Of String, JsonValue)("method", "mining.authorize"),
                    '            New KeyValuePair(Of String, JsonValue)("params", New JsonArray({CurrentPool.StratumLogin, CurrentPool.StratumPassw}))}
                    'SendMessage(jReq.ToString())

                Case StratumModeEnum.Ethproxy

                    Dim jReq As New JsonObject From {
                                    New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                    New KeyValuePair(Of String, JsonValue)("id", 1),
                                    New KeyValuePair(Of String, JsonValue)("method", "eth_submitLogin"),
                                    New KeyValuePair(Of String, JsonValue)("params", New JsonArray From {CurrentPool.StratumLogin})
                                    }
                    SendMessage(jReq.ToString())

            End Select


        End Sub

        ''' <summary>
        ''' Adds a new pool definition
        ''' </summary>
        ''' <param name="poolHost">Host Name or Ip Address</param>
        ''' <param name="poolPort">Port to connect to 1-65535</param>
        Public Sub AddPool(poolHost As String, poolPort As Integer)

            Dim pEp As Pool = Nothing

            ' Look if same host with different port
            If _poolsQueue.Count > 0 Then pEp = _poolsQueue.AsEnumerable.Where(Function(p) p.Host.ToLower = poolHost.ToLower And p.StratumLogin = String.Empty And p.StratumPassw = "x" And p.StratumWorker = String.Empty).SingleOrDefault

            If pEp IsNot Nothing Then

                pEp.AddPort(poolPort)

            Else

                pEp = New Pool(poolHost, poolPort)
                pEp.Resolve()
                _poolsQueue.Enqueue(pEp)

            End If

        End Sub

        ''' <summary>
        ''' Adds a new pool definition
        ''' </summary>
        ''' <param name="poolEndPoint">A <see cref="Pools.Pool"/> object</param>
        Public Sub AddPool(poolEndPoint As Pool)

            Dim pEp As Pool = Nothing

            ' Look if same host with different port
            If _poolsQueue.Count > 0 Then
                pEp = _poolsQueue.AsEnumerable.Where(Function(p) p.Host.ToLower = poolEndPoint.Host.ToLower And p.StratumLogin = poolEndPoint.StratumLogin.ToLower And p.StratumPassw = poolEndPoint.StratumPassw And p.StratumWorker = poolEndPoint.StratumWorker).SingleOrDefault
            End If
            If pEp Is Nothing Then
                poolEndPoint.Resolve()
                _poolsQueue.Enqueue(poolEndPoint)
            Else
                pEp.AddPort(poolEndPoint.Ports)
            End If

        End Sub

        ''' <summary>
        ''' Invokes connection to socket
        ''' </summary>
        Public Sub Connect()

            If Not _isRunning Then Return

            ' Reset stratum mode
            _stratumMode = StratumModeEnum.Undefined

            _ctsNetworkAvailable = New CancellationTokenSource
            _tskNetworkAvailable = Task.Factory.StartNew(AddressOf Core.WaitNetworkAvailable, _ctsNetworkAvailable.Token)

            ' Wait for network connectivity
            If _tskNetworkAvailable IsNot Nothing Then
                _tskNetworkAvailable.Wait()
                If _tskNetworkAvailable.Status = TaskStatus.Canceled Then Return
            End If

            If _currentPool IsNot Nothing AndAlso _currentPool.IpEndPoints.Count = 0 Then

                ' We've already consumed all available IPs for this pool
                ' switch to next
                Interlocked.Increment(_telemetry.TotalPoolSwitches)
                _poolsQueue.Enqueue(_poolsQueue.Dequeue())
                _currentPool = _poolsQueue.Peek()

            End If


            If _currentPool Is Nothing Then
                _currentPool = _poolsQueue.Peek()
                Logger.Log(1, String.Format("Selected Pool : {0}", _currentPool.Host))
            End If

            If _currentPool.IpEndPoints.Count() = 0 Then

                _currentPool.Resolve()

                If Not _settings.PoolsNoProbe Then

                    ' Do not probe again
                    If Not _currentPool.IsProbed Then
                        Dim tskProbe As Task = _currentPool.ProbeAsync
                        tskProbe.Wait()
                    End If

                End If

                _currentPool.InitIpEndPointsQueue()

            End If

            ' Reset values
            CurrentJob = Nothing
            _currentDiff = 0

            ' Peek first endpoint in queue
            Interlocked.Increment(_telemetry.TotalPoolConnectionAttempts)
            _socket.Connect(_currentPool.IpEndPoints.Peek())

        End Sub

        ''' <summary>
        ''' Forces disconnection
        ''' </summary>
        Protected Sub Disconnect()

            CurrentJob = Nothing
            _currentDiff = 0
            _jobHeaders.Clear()
            _currentPool.IsFeeAuthorized = False

            If Not String.IsNullOrEmpty(_devAddress) Then
                StopDevFee()
            End If

            If _jobTimeoutTimer IsNot Nothing Then _jobTimeoutTimer.Stop()
            _socket.Disconnect()

        End Sub

        ''' <summary>
        ''' Submits solution to pool
        ''' </summary>
        ''' <param name="jsonSolution">The <see cref="Json.JsonObject"/> holding the solution sent by worker</param>
        Public Function SubmitSolution(jsonSolutionMessage As JsonObject, originClient As Clients.Client) As Integer

            ' Return codes 
            ' 0 - Wasted (no connection or not authorized)
            ' 1 - Submitted
            ' 2 - Submitted Stale

            Dim retVar As Integer = 0
            If Not _isRunning Then Return retVar

            ' Assuming a first in first out queue for solutions
            ' each stop watch should keep amount for accepted/rejected response
            SyncLock _lockObj

                If Not IsAuthorized Then Return retVar

                ' Check if Stale Solution and remove provided worker from client
                ' Also ensure id = 4 ... we need this
                jsonSolutionMessage("id") = 4
                Dim isStale As Boolean = False
                If CurrentJob IsNot Nothing Then
                    If CurrentJob.Header.IndexOf(jsonSolutionMessage("params")(3).ToString.Replace("""", String.Empty)) < 0 Then
                        isStale = True
                    End If
                End If
                If jsonSolutionMessage.ContainsKey("worker") Then jsonSolutionMessage.Remove("worker")

                ' We always receive submission messages in stratum format
                ' if we're in ethproxy mode change accordingly
                If _stratumMode = StratumModeEnum.Ethproxy Then

                    jsonSolutionMessage("method") = "eth_submitWork"
                    Dim newParams As JsonArray = New JsonArray({
                        jsonSolutionMessage("params")(2),
                        jsonSolutionMessage("params")(3),
                        jsonSolutionMessage("params")(4)
                    })
                    jsonSolutionMessage("params") = newParams

                Else

                    ' Replace with current stratum login
                    jsonSolutionMessage("params").Item(0) = StratumLogin

                End If

                ' Add worker name if needed
                If (Not IsFeeOn AndAlso _settings.PoolsReportWorkerNames AndAlso Not String.IsNullOrEmpty(originClient.WorkerName)) Then
                    jsonSolutionMessage.Add(New KeyValuePair(Of String, JsonValue)("worker", originClient.WorkerName))
                End If

                ' Enqueue a new datetime
                ' We assume solutions are accepted FIFO style
                _submissionsQueue.Enqueue(New SubmissionEntry With {.TimeStamp = DateTime.Now, .OriginClient = originClient, .OriginId = jsonSolutionMessage("id")})

                If isStale Then
                    Interlocked.Increment(_telemetry.TotalKnownStaleSolutions)
                    Interlocked.Increment(_currentPool.KnownStaleSolutions)
                End If

                ' Log submission & start response timer
                SendMessage(jsonSolutionMessage.ToString)
                If _responseTimeoutTimer Is Nothing Then
                    _responseTimeoutTimer = New Timers.Timer With {.Interval = _settings.PoolsResponseTimeout, .AutoReset = False, .Enabled = True}
                Else
                    _responseTimeoutTimer.Stop()
                    _responseTimeoutTimer.Interval = _settings.PoolsResponseTimeout
                    _responseTimeoutTimer.Start()
                End If
                Logger.Log(6, originClient.WorkerOrId + If(isStale, " stale", String.Empty) + " nonce " + jsonSolutionMessage("params")(2).ToString, "Worker")

                Interlocked.Increment(_telemetry.TotalSolutionsSubmitted)
                Interlocked.Increment(_currentPool.SolutionsSubmitted)

                retVar += 1
                If isStale Then retVar += 1

            End SyncLock

            Return retVar

        End Function

        ''' <summary>
        ''' Submits Hashrate to pool
        ''' </summary>
        ''' <param name="jsonHashrate"></param>
        Public Sub SubmitHashrate(jsonHashrate As JsonObject)

            If Not IsAuthorized Then Return
            jsonHashrate("id") = 9
            SendMessage(jsonHashrate)

        End Sub


#End Region

#Region " Private Methods"

        ''' <summary>
        ''' Process a message received by the pool
        ''' </summary>
        Private Sub ProcessMessage(message As String)

            If String.IsNullOrEmpty(message) Then Return

            ' Out message received
            Logger.Log(9, "<< " & message, _context)

            Dim jsonMsg As JsonObject = Nothing
            Dim msgId As Integer = 0
            Dim msgMethod As String = String.Empty

            Dim isNotification As Boolean = False           ' Whether or not this message is a reply to previous request or is a broadcast notification
            Dim isSuccess As Boolean = False                ' Whether or not this is a succesful or failed response (implies _isNotification = false)
            Dim errorReason As String = String.Empty        ' The error (if any) descriptive text (if any)

            Try

                jsonMsg = JsonValue.Parse(message)
                With jsonMsg

                    If .ContainsKey("id") AndAlso .Item("id") IsNot Nothing Then .TryGetValue("id", msgId)
                    If .ContainsKey("method") AndAlso .Item("method") IsNot Nothing Then .TryGetValue("method", msgMethod)

                    If .ContainsKey("error") Then
                        If .Item("error") Is Nothing Then
                            isSuccess = True
                        Else
                            errorReason = .Item("error").ToString
                        End If
                    Else
                        isSuccess = True
                    End If

                    If .ContainsKey("result") Then
                        If .Item("result").JsonType = JsonType.Array Then
                            If .Item("result").Count > 0 Then
                                isSuccess = True
                            End If
                        ElseIf .Item("result").JsonType = JsonType.Boolean Then
                            .TryGetValue("result", isSuccess)
                        End If
                    End If

                    ' Messages with a method or msgId = 0
                    If Not String.IsNullOrEmpty(msgMethod) OrElse msgId = 0 Then
                        isNotification = True
                    End If

                    If _stratumMode = StratumModeEnum.Ethproxy Then
                        If isNotification AndAlso .ContainsKey("result") AndAlso .Item("result").JsonType = JsonType.Array AndAlso .Item("result").Count > 0 Then
                            msgMethod = "mining.notify"
                        End If
                    End If


                End With

            Catch ex As Exception

                ' Invalid format of json
                Logger.Log(0, String.Format("Invalid Json object received from Pool : {0}", ex.GetBaseException.Message), "Pool")
                Return

            End Try


            ' Handle responses
            If Not isNotification Then

                Select Case msgId

                    ' Handle response to mining.subscribe
                    Case 1

                        If Not isSuccess Then

                            Select Case _stratumMode

                                Case StratumModeEnum.TentativeStratum

                                    ' We've already tried Stratum and EthProxy
                                    ' We can't test Ethereumstratum (NiceHash) as 
                                    ' this mode can't work (atm) with extranonces
                                    If Not String.IsNullOrEmpty(errorReason) Then
                                        Logger.Log(0, String.Format("Received error from pool : {0}", errorReason), _context)
                                    End If
                                    Logger.Log(1, "Subscription failed ! Disconnecting ...", _context)
                                    _currentPool.HardErrors += 1
                                    Call Disconnect()
                                    Return

                                Case StratumModeEnum.TentativeEthProxy

                                    ' Try to fall back to Stratum Mode
                                    Dim jReq As New JsonObject From {
                                    New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                    New KeyValuePair(Of String, JsonValue)("id", 1),
                                    New KeyValuePair(Of String, JsonValue)("method", "mining.subscribe"),
                                    New KeyValuePair(Of String, JsonValue)("params", New JsonArray From {})
                                    }

                                    _stratumMode = StratumModeEnum.TentativeStratum
                                    SendMessage(jReq.ToString)
                                    Return

                                Case StratumModeEnum.Ethproxy

                                    If Not String.IsNullOrEmpty(_devAddress) Then
                                        Logger.Log(1, "DevFee not authorized. Switching to --no-fee mode.")
                                        App.Instance.Settings.NoFee = True
                                        _devFeeStartedOn = DateTime.MinValue
                                        _devAddress = String.Empty
                                        Return
                                    End If

                            End Select


                        End If

                        Select Case _stratumMode

                            Case StratumModeEnum.TentativeStratum

                                Logger.Log(1, "Stratum mode detected : Subscribed !", _context)
                                _poolStatus.SetFlags({PoolStatus.Subscribed})
                                _stratumMode = StratumModeEnum.Stratum

                                ' Send authorize request
                                Dim jsonReq As New JsonObject From {
                                New KeyValuePair(Of String, JsonValue)("id", 3),
                                New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                New KeyValuePair(Of String, JsonValue)("method", "mining.authorize"),
                                New KeyValuePair(Of String, JsonValue)("params", New JsonArray({_currentPool.StratumLogin, _currentPool.StratumPassw}))
                            }
                                SendMessage(jsonReq.ToString())

                            Case StratumModeEnum.TentativeEthProxy

                                Logger.Log(1, "EthProxy mode detected : Logged in !", _context)
                                _poolStatus.SetFlags({PoolStatus.Subscribed, PoolStatus.Authorized})
                                _stratumMode = StratumModeEnum.Ethproxy

                                ' Send getWork request
                                Dim jsonReq As New JsonObject From {
                                New KeyValuePair(Of String, JsonValue)("id", 5),
                                New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                New KeyValuePair(Of String, JsonValue)("method", "eth_getWork"),
                                New KeyValuePair(Of String, JsonValue)("params", New JsonArray({}))
                            }
                                SendMessage(jsonReq.ToString())

                            Case StratumModeEnum.Ethproxy

                                If Not String.IsNullOrEmpty(_devAddress) Then
                                    Logger.Log(1, "DevFee authorized", _context)
                                    _devFeeStartedOn = DateTime.Now
                                    _devFeeIntervalTimer.Enabled = True
                                    _devFeeIntervalTimer.Start()
                                End If

                                ' Send getWork request
                                Dim jsonReq As New JsonObject From {
                                New KeyValuePair(Of String, JsonValue)("id", 5),
                                New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                New KeyValuePair(Of String, JsonValue)("method", "eth_getWork"),
                                New KeyValuePair(Of String, JsonValue)("params", New JsonArray({}))
                            }
                                SendMessage(jsonReq.ToString())

                        End Select


                    ' Handle response to mining.authorize
                    Case 3

                        If Not isSuccess Then

                            If Not String.IsNullOrEmpty(_devAddress) Then

                                Logger.Log(1, "DevFee not authorized. Switching to --no-fee mode.")
                                App.Instance.Settings.NoFee = True
                                _devFeeStartedOn = DateTime.MinValue
                                _devAddress = String.Empty
                                Return

                            End If


                            Logger.Log(1, "Authorization failed ! Disconnecting ...", _context)
                            _currentPool.HardErrors += 1
                            Call Disconnect()
                            Return

                        End If

                        If Not String.IsNullOrEmpty(_devAddress) Then

                            Logger.Log(1, "DevFee authorized", _context)
                            _devFeeStartedOn = DateTime.Now
                            _devFeeIntervalTimer.Enabled = True
                            _devFeeIntervalTimer.Start()
                            _currentPool.IsFeeAuthorized = True

                        Else

                            Logger.Log(1, String.Format("Authorized account {0}", _currentPool.StratumLogin), _context)
                            _poolStatus.SetFlags({PoolStatus.Authorized})

                        End If


                    ' Handle response to submitted solution
                    Case 4

                        SyncLock _lockObj

                            Dim sentEntry As New SubmissionEntry
                            Dim responseTime As New TimeSpan(0)
                            If Not _submissionsQueue.TryDequeue(sentEntry) Then

                                sentEntry.TimeStamp = DateTime.Now

                            Else

                                ' Save response time
                                responseTime = DateTime.Now.Subtract(sentEntry.TimeStamp)
                                _telemetry.ResponseTimes.Enqueue(responseTime.TotalMilliseconds)

                                ' Report back to client same result 
                                ' received from pool
                                sentEntry.OriginClient.Send(message)

                            End If

                            If isSuccess Then
                                Interlocked.Increment(_telemetry.TotalSolutionsAccepted)
                                Interlocked.Increment(_currentPool.SolutionsAccepted)
                                Logger.Log(6, String.Format("Solution accepted :-) [{0:N0}ms]", responseTime.TotalMilliseconds), _context)
                            Else
                                Interlocked.Increment(_telemetry.TotalSolutionsRejected)
                                Interlocked.Increment(_currentPool.SolutionsRejected)
                                If Not String.IsNullOrEmpty(errorReason) Then
                                    Logger.Log(0, String.Format("Received error from pool : {0}", errorReason), _context)
                                End If
                                Logger.Log(6, String.Format("Solution rejected :-O [{0:N0}ms]", responseTime.TotalMilliseconds), _context)
                            End If

                            ' If we have other submissions waiting for response restart
                            ' the clock
                            If Not _submissionsQueue.IsEmpty Then
                                _responseTimeoutTimer.Reset
                            End If

                        End SyncLock

                    Case 5

                        ' Response to first eth_getWork request for EthProxy stratum mode
                        isNotification = True
                        msgMethod = "mining.notify"

                    Case 9

                        ' Response to hashrate submission
                        ' Nothing to do here

                    Case 999

                        ' This unfortunate case should Not happen as none of the outgoing requests Is marked with id 999
                        ' However it has been tested that ethermine.org responds with this id when error replying to 
                        ' either mining.subscribe (1) Or mining.authorize requests (3)
                        ' To properly handle this situation we need to rely on Subscribed/Authorized states

                        If Not isSuccess Then

                            If Not IsSubscribed Then

                                Logger.Log(0, String.Format("Subscription to pool failed : {0}", errorReason), _context)
                                Disconnect()
                                Return

                            ElseIf IsSubscribed AndAlso IsAuthorized = False Then

                                If String.IsNullOrEmpty(_devAddress) Then

                                    Logger.Log(0, String.Format("Authorization to pool failed : {0}", errorReason), _context)
                                    Disconnect()
                                    Return

                                Else

                                    Logger.Log(1, "DevFee not authorized. Switching to --no-fee mode.")
                                    App.Instance.Settings.NoFee = True
                                    _devFeeStartedOn = DateTime.MinValue
                                    _devAddress = String.Empty
                                    Return

                                End If

                            End If

                        End If

                    Case Else

                        Logger.Log("0", "Received unprocessable response from Pool. Discarding ...", _context)
                        Return

                End Select


            End If

            ' Process notifications
            If isNotification Then

                ' Handle notifications
                Select Case msgMethod

                    ' New job notification

                    Case "mining.notify"

                        Dim notifyJob As Core.Job = Nothing
                        If jsonMsg.ContainsKey("result") AndAlso jsonMsg("result").JsonType = JsonType.Array AndAlso jsonMsg("result").Count > 0 Then
                            notifyJob = New Core.Job(jsonMsg("result"), StratumModeEnum.Ethproxy)
                        ElseIf jsonMsg.ContainsKey("params") AndAlso jsonMsg("params").JsonType = JsonType.Array AndAlso jsonMsg("params").Count > 0 Then
                            notifyJob = New Core.Job(jsonMsg("params"), StratumModeEnum.Stratum)
                        Else
                            Return
                        End If


                        ' Compute time since last job
                        Dim timeSinceLastJob As TimeSpan = New TimeSpan(0)

                        If CurrentJob IsNot Nothing Then

                            timeSinceLastJob = notifyJob.TimeStamp.Subtract(CurrentJob.TimeStamp)
                            If timeSinceLastJob.TotalMilliseconds > _telemetry.MaxJobInterval Then
                                _telemetry.MaxJobInterval = timeSinceLastJob.TotalMilliseconds
                            End If

                            ' Check we're not receiving a duplicate job
                            If _jobHeaders.Contains(notifyJob.Header) Then
                                Return
                            Else
                                _jobTimeoutTimer.Reset
                                _jobHeaders.Enqueue(notifyJob.Header)
                            End If

                            ' Compute difficulty
                            If notifyJob.Target <> If(CurrentJob Is Nothing, String.Empty, CurrentJob.Target) Then
                                _currentDiff = GetDiffToTarget(notifyJob.Target)
                                Logger.Log(1, String.Format("Pool difficulty set to {0}", ScaleHashes(_currentDiff)), _context)
                            End If

                            ' Set currentJob
                            CurrentJob = notifyJob
                            SyncLock _lockObj
                                _telemetry.TotalJobsReceived += 1
                                _currentPool.JobsReceived += 1
                            End SyncLock

                        Else

                            CurrentJob = notifyJob
                            ' Compute difficulty
                            _currentDiff = GetDiffToTarget(notifyJob.Target)
                            Logger.Log(1, String.Format("Pool difficulty set to {0}", ScaleHashes(_currentDiff)), _context)

                        End If

                        ' Log and notify workers
                        Logger.Log(6, String.Format("New job #{0} [{1:N2}s]", CurrentJob.Id, timeSinceLastJob.TotalSeconds), _context)
                        If EventNewJobReceivedEvent IsNot Nothing Then
                            RaiseEvent EventNewJobReceived(New JsonObject From {
                                New KeyValuePair(Of String, JsonValue)("id", Nothing),
                                New KeyValuePair(Of String, JsonValue)("method", "mining.notify"),
                                New KeyValuePair(Of String, JsonValue)("params", New JsonArray From {CurrentJob.Header, CurrentJob.Header, CurrentJob.Seed, CurrentJob.Target})
                                 })
                        End If

                    Case "client.get_version"

                        ' Request of version
                        Dim jRes As New JsonObject From {
                            New KeyValuePair(Of String, JsonValue)("id", msgId),
                            New KeyValuePair(Of String, JsonValue)("result", "neth-proxy " + GetType(Program).Assembly.GetName().Version.ToString()),
                            New KeyValuePair(Of String, JsonValue)("error", Nothing)
                            }

                        SendMessage(jRes.ToString)

                    Case Else

                        Logger.Log(0, String.Format("Received unknown method {0} from pool", msgMethod), _context)

                End Select

            End If


        End Sub

        ''' <summary>
        ''' Sends the given message to the underlying socket
        ''' </summary>
        ''' <param name="message">The message to be sent</param>
        Private Sub SendMessage(message As String)

            ' Out message being sent
            Logger.Log(9, ">> " & message, _context)

            _socket.Send(message)

        End Sub

        ''' <summary>
        ''' Sends a json object to the underlying socket
        ''' </summary>
        ''' <param name="jmessage">The <see cref="JsonObject"/> to be sent</param>
        Private Sub SendMessage(jmessage As JsonObject)

            SendMessage(jmessage.ToString)

        End Sub

        ''' <summary>
        ''' Forces disconnection from current pool and
        ''' reconnect to failover pool (if any)
        ''' </summary>
        Public Sub SwitchPool()

            If Not IsConnected Then Return

            ' Clears all remaining IPs for this
            ' pool and disconnects
            While _currentPool.IpEndPoints.Count > 0
                _currentPool.IpEndPoints.Dequeue()
            End While
            Disconnect()

        End Sub

        ''' <summary>
        ''' Forces disconnection and reconnection on next available ip
        ''' </summary>
        Public Sub Reconnect()

            If Not IsConnected Then Return
            Disconnect()

        End Sub

#End Region

#Region " Async Socket Event Handlers"

        Private Sub OnSocketConnected(ByRef sender As AsyncSocket) Handles _socket.Connected

            SyncLock _lockObj
                _poolStatus.SetFlags({PoolStatus.Connected})
                _poolStatus.UnsetFlags({PoolStatus.NotConnected})
            End SyncLock

            ' Initialize checks for worktimeout
            If _jobTimeoutTimer Is Nothing Then
                _jobTimeoutTimer = New Timers.Timer With {
                    .Interval = _settings.PoolsWorkTimeout * 1000,
                    .Enabled = True,
                    .AutoReset = True
                    }
            End If

            Logger.Log(3, String.Format("Connection to {0} successful", _currentPool.Host), _context)

            ' Start receiving
            sender.BeginReceive()

            ' Send subscription request
            If Not IsSubscribed Then

                _stratumMode = StratumModeEnum.TentativeEthProxy

                Dim jReq As New JsonObject From {
                                    New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                    New KeyValuePair(Of String, JsonValue)("id", 1),
                                    New KeyValuePair(Of String, JsonValue)("method", "eth_submitLogin"),
                                    New KeyValuePair(Of String, JsonValue)("params", New JsonArray From {StratumLogin})
                                    }
                SendMessage(jReq.ToString)

            End If


        End Sub

        Private Sub OnSocketConnectionFailed(ByRef sender As AsyncSocket) Handles _socket.ConnectionFailed

            ' Quit if stopped
            If Not _isRunning Then Return

            ' Dequeue failed ip
            Interlocked.Increment(_telemetry.TotalPoolConnectionFailed)
            _currentPool.IpEndPoints.Dequeue()

            ' Resubmit new connection
            ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf Connect))


        End Sub

        Private Sub OnSocketMessageReceived(ByRef sender As AsyncSocket, ByVal message As String) Handles _socket.MessageReceived

            ' Queue the message processing
            ProcessMessage(message)

        End Sub

        Private Sub OnSocketDisconnected(ByRef sender As Object) Handles _socket.Disconnected

            Logger.Log(3, $"Pool {_currentPool.Host} disconnected", _context)

            SyncLock _lockObj
                _poolStatus.SetFlags({PoolStatus.NotConnected})
                _poolStatus.UnsetFlags({PoolStatus.Connected, PoolStatus.Authorized, PoolStatus.Subscribed})
            End SyncLock

            ' Flush queue of submission times for nonces
            ' as we won't receive answers for pending submissions
            ' also inform clients 
            Dim item As SubmissionEntry = Nothing
            While _submissionsQueue.Count > 0
                If _submissionsQueue.TryDequeue(item) Then
                    Dim jresponse As New Json.JsonObject
                    jresponse("id") = item.OriginId
                    jresponse("jsonrpc") = "2.0"
                    jresponse("result") = False

                    Try
                        item.OriginClient.Send(jresponse.ToString())
                    Catch ex As Exception
                        ' May be already disposed
                    End Try

                End If
            End While

            If Not _isRunning Then Return

            ' Dequeue failed ip and start reconnect
            If _currentPool.IpEndPoints.Count > 0 Then _currentPool.IpEndPoints.Dequeue()
            ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf Connect))

        End Sub

#End Region


    End Class

End Namespace