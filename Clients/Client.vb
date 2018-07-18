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
Imports nethproxy.RangeTree
Imports nethproxy.Sockets
Imports System.Json
Imports System.Net
Imports System.Net.Sockets
Imports System.Threading

Namespace Clients

    Public Class Client
        Implements IDisposable

        ' Main Socket
        Private _clientStatus As ClientStatus = ClientStatus.NotConnected
        Private WithEvents _socket As AsyncSocket = Nothing                     ' This is work socket to deploy jobs and receive solutions

        ' Api Socket
        Private WithEvents _apisocket As AsyncSocket = Nothing                  ' This is the socket to connect to client's API
        Private _apiMessagesQueue As New Concurrent.ConcurrentQueue(Of String)

        ' Timers for events
        Private WithEvents _scheduleTimer As Timers.Timer                       ' Triggers scheduled operations
        Private _scheduleRunning As Boolean = False

        ' Reference to singletons
        Private WithEvents _poolmgr As Pools.PoolManager = App.Instance.PoolMgr
        Private _clntmgr As ClientsManager = App.Instance.ClntMgr
        Private _telemetry As Telemetry = App.Instance.Telemetry
        Private _settings As Settings = App.Instance.Settings

        ' Logging Context
        Private _context As String = "Worker"
        Private _lockObj As New Object

        ' Client specific members
        Private _id As String
        Private _workerName As String = String.Empty

        ' Api specific
        Public Property ApiEndPoint As IPEndPoint
        Public Property ApiAvailable As Boolean = False
        Public Property ApiConnectionAttempts As Integer = 0

        Private _apiInfoPending As Boolean = False
        Private _apiScrambleInfoPending As Boolean = False

        ' Data Pulled From API Calls
        Public Property ApiInfo As ClientInfo                                   ' Info pulled from miner_getstathr
        Public Property ApiScrambleInfo As ClientScrambleInfo                   ' Info pulled from miner_getscramblerinfo
        Public Property ApiSegmentCheckedOn As DateTime = DateTime.MinValue     ' Date/time segment was checked/narrowed/enlarged

        ' Statistics
        Public HashRate As Decimal = Decimal.Zero
        Public MaxHashRate As Decimal = Decimal.Zero
        Public SolutionsSubmitted As Long = 0
        Public SolutionsAccepted As Long = 0
        Public SolutionsRejected As Long = 0
        Public KnownStaleSolutions As Long = 0
        Public LastSubmittedTimestamp As DateTime = DateTime.MinValue

#Region " Properties"

        ''' <summary>
        ''' Gets the unique id for this client
        ''' </summary>
        ''' <returns>A string</returns>
        Public ReadOnly Property Id As String
            Get
                Return _id
            End Get
        End Property

        ''' <summary>
        ''' Gets the time of connection of this worker
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ConnectedTimeStamp As DateTime
            Get
                If _socket Is Nothing Then Return DateTime.MinValue
                Return _socket.ConnectedTimestamp
            End Get
        End Property

        ''' <summary>
        ''' Gets the duration of this worker's connection
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ConnectionDuration As TimeSpan
            Get
                If _socket Is Nothing Then
                    Return New TimeSpan(0, 0, 0)
                End If
                Return _socket.ConnectionDuration
            End Get
        End Property

        ''' <summary>
        ''' Gets the amount of time this client has been idle
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IdleDuration As TimeSpan
            Get
                If _socket Is Nothing Then Return New TimeSpan(0, 0, 0)
                Return _socket.IdleDuration
            End Get
        End Property

        ''' <summary>
        ''' Gets whether or not the underlying socket is connected
        ''' </summary>
        ''' <returns>True / False</returns>
        Public ReadOnly Property IsConnected As Boolean
            Get
                If disposedValue Then Return False
                If _socket Is Nothing Then
                    Return False
                Else
                    Return _socket.IsConnected()
                End If
            End Get
        End Property

        ''' <summary>
        ''' Whether or not client is Sbuscribed
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IsSubscribed As Boolean
            Get
                Return _clientStatus.HasFlag(ClientStatus.Subscribed)
            End Get
        End Property

        ''' <summary>
        ''' Whether or not client is Authorized
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IsAuthorized As Boolean
            Get
                Return _clientStatus.HasFlag(ClientStatus.Authorized)
            End Get
        End Property

        ''' <summary>
        ''' Gets whether or not the underlying API socket is connected
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IsAPIConnected As Boolean
            Get
                If _apiEndPoint Is Nothing Then Return False
                If _apisocket Is Nothing Then Return False
                Return _apisocket.IsConnected()
            End Get
        End Property

        ''' <summary>
        ''' Returns the remote endpoint of the underlying socket
        ''' </summary>
        ''' <returns>True / False</returns>
        Public ReadOnly Property RemoteEndPoint As IPEndPoint
            Get
                If _socket Is Nothing Then Return Nothing
                Return _socket.RemoteEndPoint
            End Get
        End Property

        ''' <summary>
        ''' Returns the name of the worker as reported during
        ''' subscribe / authorize process
        ''' </summary>
        ''' <returns>A string</returns>
        Public ReadOnly Property WorkerName As String
            Get
                Return _workerName
            End Get
        End Property

        ''' <summary>
        ''' Returns Worker Name or Id if the first is empty
        ''' </summary>
        ''' <returns>A string</returns>
        Public ReadOnly Property WorkerOrId As String
            Get
                If String.IsNullOrEmpty(_workerName) Then
                    Return _id
                Else
                    Return _workerName
                End If
            End Get
        End Property


#End Region

#Region " Events"

        Public Event Disconnected(ByRef sender As Client)

#End Region

#Region " Constructor"

        ' Inhibit factory method
        Private Sub New()
        End Sub

        ''' <summary>
        ''' Default constructor
        ''' </summary>
        ''' <param name="acceptedSocket">The <see cref="Socket"/> object accepted by the listener</param>
        Public Sub New(ByRef acceptedSocket As Socket)

            ' Start a new async socket
            _socket = New AsyncSocket(acceptedSocket)
            _id = _socket.RemoteEndPoint.ToString()
            _clientStatus.SetFlags({ClientStatus.Connected})
            _clientStatus.UnsetFlags({ClientStatus.NotConnected, ClientStatus.Subscribed, ClientStatus.Authorized})
            _socket.BeginReceive()

            ' Start internal scheduler with a random interval among 10 and 30 seconds
            _scheduleTimer = New Timers.Timer With {.Interval = (GetRandom(10, 30) * 1000), .AutoReset = False, .Enabled = True}

        End Sub

#End Region

#Region " Methods"

        ''' <summary>
        ''' Issues immediate disconnection of the underlying socket
        ''' and signals client disconnection
        ''' </summary>
        Public Sub Disconnect()

            If _clientStatus.HasFlag(ClientStatus.NotConnected) Then Return

            _clientStatus.SetFlags({ClientStatus.NotConnected})
            _clientStatus.UnsetFlags({ClientStatus.Connected, ClientStatus.Authorized, ClientStatus.Subscribed})

            _scheduleTimer.Enabled = False
            StopApiConnection()

            If _socket IsNot Nothing AndAlso _socket.IsConnected Then
                _socket.Disconnect()
            End If

            ' Remove from Telemetry the segment of this Client
            Dim WorkerRange As WorkerRangeItem = Nothing
            SyncLock _telemetry.Lock

                Try

                    WorkerRange = _telemetry.RangesTree.Items.Where(Function(wr) wr.Id = Id).SingleOrDefault
                    If WorkerRange IsNot Nothing Then
                        _telemetry.RangesTree.Remove(WorkerRange)
                    End If

                Catch IOEx As InvalidOperationException

                    ' This should not happen : more than one range with same id
                    ' To be safe just clear _telemetry.RangesTree and let
                    ' it be repopulated with Clients scheduled events
                    _telemetry.RangesTree.Clear()

                Catch ex As Exception

                    ' This should not happen
                    ' Output to log
                    Logger.Log(0, String.Format("{0} unmanaged error: {1}", WorkerOrId, ex.GetBaseException.Message), _context)

                End Try

            End SyncLock

            If DisconnectedEvent IsNot Nothing Then RaiseEvent Disconnected(Me)

        End Sub

        ''' <summary>
        ''' Handles the incoming message
        ''' </summary>
        ''' <param name="message">A Json object string</param>
        Private Sub ProcessMessage(message As String)

            ' Out message received
            If _settings.LogVerbosity >= 9 Then Logger.Log(9, "<< " & message, _context)

            Dim jsonMsg As JsonObject = Nothing
            Dim msgId As Integer = 0
            Dim msgMethod As String = String.Empty
            Dim msgResult As Boolean = False
            Dim msgError As String = String.Empty

            Try

                jsonMsg = JsonValue.Parse(message)
                With jsonMsg
                    If .ContainsKey("id") Then .TryGetValue("id", msgId)
                    If .ContainsKey("method") Then .TryGetValue("method", msgMethod)
                    If .ContainsKey("error") Then .TryGetValue("error", msgError)
                    If .ContainsKey("result") Then .TryGetValue("result", msgResult)
                End With

            Catch ex As Exception

                ' Invalid format of json
                Logger.Log(0, String.Format("Json parse failed from worker {1} : {0}", ex.GetBaseException.Message, WorkerOrId), _context)
                Return

            End Try


            ' Handle message
            Select Case True

                Case msgMethod = "mining.subscribe"

                    ' Check is NOT mining.subscribe in NiceHash format
                    If jsonMsg.ContainsKey("params") Then
                        If jsonMsg("params").JsonType = JsonType.Array Then
                            If jsonMsg("params").ToString.IndexOf("EthereumStratum/") > 0 Then
                                _socket.Send(NewJsonRpcResErr(msgId, "Not implemented").ToString())
                                Return
                            End If
                        End If
                    End If

                    ' Accept mining subscription
                    _clientStatus.SetFlags({ClientStatus.Subscribed})
                    _socket.Send(NewJsonRpcResOk(msgId).ToString)
                    Logger.Log(4, String.Format("{0} subscribed", RemoteEndPoint.ToString), _context)

                Case msgMethod = "mining.authorize"

                    ' Authorization MUST come in one of these alternative ways
                    ' /<workername>         To present the workername only
                    ' /<workername>/<port>  To present the workername and the API port of ethminer

                    Dim auth As String = jsonMsg("params").Item(0)

                    If Not String.IsNullOrEmpty(auth.Trim()) Then

                        Dim strPos As Integer = auth.IndexOf("/")

                        If strPos < 0 Then
                            _socket.Send(NewJsonRpcResErr(msgId, "Invalid credentials : use /<workername>[/<portnumber>]").ToString())
                            ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf Disconnect))
                            Return
                        Else
                            auth = auth.Substring(strPos + 1)
                        End If

                        Dim authParts As String() = auth.Trim.Split("/", StringSplitOptions.RemoveEmptyEntries)
                        If authParts.Length > 0 Then
                            For i As Integer = 0 To authParts.Length - 1
                                Select Case i
                                    Case 0
                                        ' Workername
                                        If authParts(i) <> "." Then _workerName = authParts(i).Trim
                                    Case 1
                                        ' Client's API port
                                        Dim port As Integer = 0
                                        If Not Integer.TryParse(authParts(i).Trim, port) OrElse (port < 1 OrElse port > 65535) Then
                                            Logger.Log(0, $"Worker {WorkerOrId} does not provide a valid API port", _context)
                                        Else
                                            _apiEndPoint = New IPEndPoint(_socket.RemoteEndPoint.Address, port)
                                        End If
                                End Select
                            Next
                        End If

                    End If

                    ' Immediately send job if available
                    _clientStatus.SetFlags({ClientStatus.Authorized})
                    If _poolmgr.CurrentJob IsNot Nothing Then
                        With _poolmgr.CurrentJob
                            PushJob(New JsonObject From {
                                    New KeyValuePair(Of String, JsonValue)("id", Nothing),
                                    New KeyValuePair(Of String, JsonValue)("method", "mining.notify"),
                                    New KeyValuePair(Of String, JsonValue)("params", New JsonArray From { .Header, .Header, .Seed, .Target})
                                     })
                        End With
                    End If


                    Logger.Log(4, String.Format("{0} authorized. Worker name {1} {2}", RemoteEndPoint.ToString, If(String.IsNullOrEmpty(_workerName), "[no-name]", _workerName), If(_ApiEndPoint Is Nothing, "", "Control port " & _ApiEndPoint.Port.ToString)), _context)

                    ' Start talking to client's API interface
                    If ApiEndPoint IsNot Nothing Then

                        Dim jReq As New JsonObject From {
                            New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                            New KeyValuePair(Of String, JsonValue)("id", 1),
                            New KeyValuePair(Of String, JsonValue)("method", "miner_ping")
                            }
                        SendAPIMessage(jReq.ToString)

                    End If


                Case msgMethod = "mining.submit"

                    ' Prevent submission if not authorized
                    If Not IsAuthorized Then
                        _socket.Send(NewJsonRpcResErr(msgId, "Not Authorized").ToString)
                        Return
                    End If

                    ' Sanity checks over solution submitted
                    ' Should I ? Would you really want to submit something wrong ?
                    ' If user has set ethminer with --noeval there is no need to
                    ' recheck here.

                    ' Accept solution immediately to prevent client
                    ' to fall into --response-timeout
                    LastSubmittedTimestamp = DateTime.Now
                    Interlocked.Increment(SolutionsSubmitted)
                    _socket.Send(NewJsonRpcResOk(msgId))

                    ' Submit to pool 
                    _poolmgr.SubmitSolution(jsonMsg, Me)


                Case msgMethod = "eth_submitHashrate"

                    ' Prevent proxying submission if client not authorized
                    ' to keep list of workers clean
                    If Not IsAuthorized OrElse _poolmgr.IsFeeOn Then Return

                    ' Aknowledge to client - Is this really needed ?
                    ' Ethminer does not take any action on such a reply
                    '_socket.Send(NewJsonRpcResOk(msgId).ToString())


                    ' Try to detect HashRate
                    Try

                        HashRate = Convert.ToUInt64(jsonMsg("params").Item(0), fromBase:=16)
                        If HashRate > MaxHashRate Then MaxHashRate = HashRate

                    Catch ex As Exception

                        Logger.Log(0, WorkerOrId + " sent an invalid hashrate value : " + jsonMsg("params")(1).ToString, _context)
                        Return

                    End Try

                    ' Conditional log before calculating values
                    If _settings.LogVerbosity >= 5 Then
                        Logger.Log(5, String.Format("{0} Hashrate {1}", WorkerOrId, ScaleHashes(HashRate)), _context)
                    End If

                    ' Report this client hashrate only if --report-workers is set
                    If (_settings.PoolsReportWorkerNames AndAlso _settings.PoolsReportHashRate) Then

                        Dim jsonReq As JsonObject = NewJsonRpc(9)
                        jsonReq.AddRange({
                        New KeyValuePair(Of String, JsonValue)("method", "eth_submitHashrate"),
                        New KeyValuePair(Of String, JsonValue)("worker", _workerName),
                        New KeyValuePair(Of String, JsonValue)("params", New JsonArray From {jsonMsg("params").Item(0), jsonMsg("params")(1)})
                        })

                        ' Send to pool
                        _poolmgr.SubmitHashrate(jsonReq)

                    End If

                Case Else

                    ' Any other not implemented (yet ?)
                    _socket.Send(NewJsonRpcResErr(msgId, String.Format("Method {0} not implemented", msgMethod)).ToString())


            End Select

        End Sub

        ''' <summary>
        ''' Sends the specified message through the underlying socket
        ''' </summary>
        ''' <param name="message"></param>
        Public Sub Send(ByVal message As String)
            If disposedValue Then Return
            If _socket IsNot Nothing AndAlso _socket.IsConnected Then
                _socket.Send(message)
            End If
        End Sub

        ''' <summary>
        ''' Pushes Job from Pool to Client
        ''' </summary>
        ''' <param name="sender">The <see cref="Pools.PoolManager"/> object which raised the event</param>
        Public Sub PushJob(JsonJob As JsonObject) Handles _poolmgr.EventNewJobReceived

            If Not IsConnected OrElse Not IsAuthorized Then Return
            _socket.Send(JsonJob)

        End Sub

#End Region

#Region " Async Socket Event Handlers"

        Private Sub OnSocketConnected(ByRef sender As AsyncSocket) Handles _socket.Connected
        End Sub

        Private Sub OnSocketDisconnected(ByRef sender As AsyncSocket) Handles _socket.Disconnected

            Disconnect()

        End Sub

        Private Sub OnSocketMessageReceived(ByRef sender As AsyncSocket, ByVal message As String) Handles _socket.MessageReceived
            ProcessMessage(message)
        End Sub

#End Region

#Region " Api Methods"

        ''' <summary>
        ''' Will start API connection on selected port
        ''' </summary>
        Private Sub StartApiConnection()

            If _ApiEndPoint Is Nothing Then
                Return
            End If

            Try

                If _apisocket Is Nothing Then
                    _apisocket = New AsyncSocket("Api")
                    _apisocket.Connect(_apiEndPoint)
                Else
                    If _apisocket.IsConnected = False AndAlso _apisocket.IsPendingState = False Then
                        _apisocket.Connect(_ApiEndPoint)
                    End If
                End If


            Catch ex As Exception

                ' Object disposed or disposing

            End Try

        End Sub

        ''' <summary>
        ''' Will stop API connection if any
        ''' </summary>
        Private Sub StopApiConnection()

            If _apisocket IsNot Nothing Then

                _apiMessagesQueue.Clear()
                _apisocket.Disconnect()

            End If

        End Sub

        ''' <summary>
        ''' Processes API responses
        ''' </summary>
        Private Sub ProcessAPIResponse(ByVal message As String)

            Dim jsonMsg As New JsonObject
            Dim msgId As Integer = 0
            Dim msgResult As Boolean = False
            Dim msgError As String = String.Empty

            Try

                jsonMsg = JsonObject.Parse(message)
                With jsonMsg
                    If .ContainsKey("id") Then .TryGetValue("id", msgId)
                    If .ContainsKey("error") AndAlso .Item("error") IsNot Nothing Then
                        msgError = .Item("error").ToString
                    End If
                    If Not .ContainsKey("jsonrpc") Then Throw New Exception("Missing jsonrpc member")
                    If Not .Item("jsonrpc") = "2.0" Then Throw New Exception("Jsonrpc value mismatch")
                End With

            Catch ex As Exception

                ' Invalid format of json
                Logger.Log(0, String.Format("Api response parse from worker {1} : {0}", ex.GetBaseException.Message, WorkerOrId), _context)
                Return

            End Try

            ' If any error in the processing of method then
            ' abandon API session
            If Not String.IsNullOrEmpty(msgError) Then

                ' The request returned error
                ' So ethminer's version does not implement this method
                ' Clear ApiEndPoint and Disconnect Client
                Logger.Log(0, $"{WorkerOrId} does not support required API interface. Disconnecting ...", _context)
                ApiEndPoint = Nothing
                ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf Disconnect))
                Return

            End If


            Select Case msgId

                Case 1

                    ' Response to miner_ping
                    ' Well actually not very much to do but as we're live
                    ' prepare a request to pull ScrambleInfo
                    If Not _apiScrambleInfoPending Then

                        Dim jReq As New JsonObject From {
                        New KeyValuePair(Of String, JsonValue)("id", 2),
                        New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                        New KeyValuePair(Of String, JsonValue)("method", "miner_getscramblerinfo")
                        }
                        _apiScrambleInfoPending = True
                        SendAPIMessage(jReq.ToString)
                    End If

                Case 2

                    ' Response to miner_getscramblerinfo

                    _apiScrambleInfoPending = False
                    Dim jResult As JsonObject = jsonMsg("result")

                    Try

                        Dim newScrambleInfo As New ClientScrambleInfo With {
                        .TimeStamp = DateTime.Now,
                        .NonceScrambler = jResult("noncescrambler"),
                        .GpuWidth = jResult("segmentwidth")
                        }

                        For i As Integer = 0 To jResult("segments").Count - 1
                            Dim newSegment As New ClientScrambleInfoSegment With {
                            .GpuIndex = jResult("segments")(i)("gpu"),
                            .SegmentStart = jResult("segments")(i)("start"),
                            .SegmentStop = jResult("segments")(i)("stop")
                        }
                            newScrambleInfo.Segments.Add(newSegment)
                        Next i

                        ApiScrambleInfo = newScrambleInfo

                    Catch ex As Exception

                        Logger.Log(0, $"{WorkerOrId} Could not load miner_getscramblerinfo", _context)
                        Return

                    End Try

                    '' No checks on no fee
                    If App.Instance.Settings.NoFee = True Then Return

                    '' Remove any previous segment registration from this worker
                    Dim WorkerRange As WorkerRangeItem
                    SyncLock _telemetry.Lock

                        Try

                            WorkerRange = _telemetry.RangesTree.Items.Where(Function(wr) wr.Id = Id).SingleOrDefault
                            If WorkerRange IsNot Nothing Then
                                _telemetry.RangesTree.Remove(WorkerRange)
                            End If

                        Catch ex As Exception

                            ' This should not happen as all workers have a unique id
                            ' TODO disconnect all workers and wait for their reconnection

                        End Try

                        ' Check if this worker's range overlaps with some other's
                        WorkerRange = New WorkerRangeItem With {.Id = Id, .Name = WorkerOrId, .Range = New Range(Of UInt64)(ApiScrambleInfo.ScrambleStart, ApiScrambleInfo.ScrambleStop)}
                        Dim OverlappingRanges As List(Of WorkerRangeItem) = _telemetry.RangesTree.Query(WorkerRange.Range).Where(Function(r) r.Id <> WorkerRange.Id).ToList()
                        If OverlappingRanges.Count > 0 Then

                            For Each wr As WorkerRangeItem In OverlappingRanges
                                Logger.Log(1, $"{WorkerRange.Id} range overlaps with {wr.Id}", _context)
                            Next
                            Logger.Log(1, $"Shuffling {WorkerRange.Id} ...", _context)

                            ' Prepare a request to shuffle ScrambleInfo
                            Dim jReq As New JsonObject From {
                            New KeyValuePair(Of String, JsonValue)("id", 3),
                            New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                            New KeyValuePair(Of String, JsonValue)("method", "miner_shuffle")
                            }
                            SendAPIMessage(jReq.ToString)

                        Else

                            ' Save non overlapping range 
                            _telemetry.RangesTree.Add(WorkerRange)

                        End If

                    End SyncLock

                Case 3

                    ' Response to miner_shuffle
                    ' As we're here the method replied successfully
                    ' so issue a new request for miner_getscramblerinfo

                    If Not _apiScrambleInfoPending Then

                        Dim jReq As New JsonObject From {
                        New KeyValuePair(Of String, JsonValue)("id", 2),
                        New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                        New KeyValuePair(Of String, JsonValue)("method", "miner_getscramblerinfo")
                        }
                        _apiScrambleInfoPending = True
                        SendAPIMessage(jReq.ToString)
                    End If

                Case 4

                    ' Response to miner_getstathr
                    _apiInfoPending = False
                    Dim jResult As JsonObject = jsonMsg("result")
                    Dim newClientInfo As New ClientInfo With {
                        .TimeStamp = DateTime.Now,
                        .HashRate = jResult("ethhashrate"),
                        .RunTime = jResult("runtime"),
                        .Version = jResult("version")
                        }

                    Dim gpuCount As Integer = jResult("ethhashrates").Count
                    With newClientInfo
                        For i As Integer = 0 To gpuCount - 1

                            .HashRates.Add(jResult("ethhashrates")(i))
                            .Fans.Add(jResult("fanpercentages")(i))
                            .Temps.Add(jResult("temperatures")(i))
                            .Powers.Add(jResult("powerusages")(i))

                        Next
                        .Solutions.Count = jResult("ethshares")
                        .Solutions.Invalid = jResult("ethinvalid")
                        .Solutions.Rejected = jResult("ethrejected")
                    End With

                    ' Persist informations
                    ApiInfo = newClientInfo

                Case 5

                    ' Response to miner_setscramblerinfo
                    ' As we got here then result is success
                    ' if no other requests pending reissue info 
                    ' about scrambler
                    If Not _apiScrambleInfoPending Then

                        Dim jReq As New JsonObject From {
                        New KeyValuePair(Of String, JsonValue)("id", 2),
                        New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                        New KeyValuePair(Of String, JsonValue)("method", "miner_getscramblerinfo")
                        }
                        _apiScrambleInfoPending = True
                        SendAPIMessage(jReq.ToString)
                    End If


            End Select



        End Sub

        ''' <summary>
        ''' Sends a method request to client's API
        ''' </summary>
        ''' <param name="message">The message to be sent</param>
        Public Sub SendAPIMessage(message As String)

            ' Do not enter if not possible to communicate
            If Not IsConnected OrElse ApiEndPoint Is Nothing Then Return

            _apiMessagesQueue.Enqueue(message)
            If Not IsAPIConnected Then
                ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf StartApiConnection))
            Else
                ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf SendAPIMessageQueue))
            End If

        End Sub

        ''' <summary>
        ''' Asyncronously flushes the queue of messages to client's API
        ''' </summary>
        Private Sub SendAPIMessageQueue()

            If _apisocket Is Nothing OrElse
                    _apisocket.IsConnected = False Then
                Return
            End If

            While Not _apiMessagesQueue.IsEmpty
                Dim message As String = String.Empty
                If _apiMessagesQueue.TryDequeue(message) Then
                    _apisocket.Send(message)
                End If
            End While

        End Sub

        ''' <summary>
        ''' Checks the witdth of subsegments assigned to each
        ''' gpu to find wheter or not it can be compacted
        ''' </summary>
        Private Sub CheckWorkerSegment()

            ' Process only if connected for more than 5 minutes
            If ApiEndPoint Is Nothing OrElse
                    ConnectionDuration.TotalMinutes < 5 OrElse
                    App.Instance.Settings.NoFee Then
                Return
            End If

            If ApiInfo Is Nothing OrElse DateTime.Now.Subtract(ApiInfo.TimeStamp).TotalMinutes > 3 Then
                If Not _apiInfoPending Then
                    Dim jReq As New JsonObject From {
                                    New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                                    New KeyValuePair(Of String, JsonValue)("id", 4),
                                    New KeyValuePair(Of String, JsonValue)("method", "miner_getstathr")
                                }
                    _apiInfoPending = True
                    SendAPIMessage(jReq.ToString)
                End If
            End If

            If ApiScrambleInfo Is Nothing OrElse DateTime.Now.Subtract(ApiScrambleInfo.TimeStamp).TotalMinutes > 3 Then
                If Not _apiScrambleInfoPending Then
                    Dim jReq As New JsonObject From {
                        New KeyValuePair(Of String, JsonValue)("id", 2),
                        New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                        New KeyValuePair(Of String, JsonValue)("method", "miner_getscramblerinfo")
                        }
                    _apiScrambleInfoPending = True
                    SendAPIMessage(jReq.ToString)
                End If
            End If

            ' Wait for next loop in case we need fresher informations
            ' or not enough time to optimize segments
            If _apiInfoPending OrElse
                _apiScrambleInfoPending OrElse
                App.Instance.PoolMgr.ConnectionDuration.TotalMinutes < 10 Then
                Return
            End If

            Try

                ' Analyze worker's segment and hashrate
                Dim maxGPUHashratePerSecond As UInt64 = ApiInfo.HashRates.Max()
                Dim avgJobInterval As Double = (_telemetry.MaxJobInterval / 1000) * 1.2             ' Apply 20% margin in excess
                Dim maxGpuHashesPerJobInterval = (maxGPUHashratePerSecond * avgJobInterval)
                Dim newSegmentWidth As Integer = Math.Round(Math.Log(maxGpuHashesPerJobInterval, 2))

                ' As rounding may be done to lower integer apply further check
                While Math.Pow(2, newSegmentWidth) < maxGpuHashesPerJobInterval
                    newSegmentWidth += 1
                End While

                ' We've got a new ideal segment width per gpu. Compare to current
                ' and apply differences
                If newSegmentWidth <> ApiScrambleInfo.GpuWidth Then

                    Dim jReq As New JsonObject From {
                    New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                    New KeyValuePair(Of String, JsonValue)("id", 5),
                    New KeyValuePair(Of String, JsonValue)("method", "miner_setscramblerinfo"),
                    New KeyValuePair(Of String, JsonValue)("params", New JsonObject From {
                        New KeyValuePair(Of String, JsonValue)("segmentwidth", newSegmentWidth)
                    })
                    }
                    ApiSegmentCheckedOn = DateTime.Now
                    SendAPIMessage(jReq.ToString)

                End If

            Catch ex As Exception

            End Try

        End Sub

#End Region

#Region " Async Api Socket Event Handlers"

        Private Sub OnApiSocketConnected(ByRef sender As AsyncSocket) Handles _apisocket.Connected

            ApiConnectionAttempts = 0
            sender.BeginReceive()
            If _apiMessagesQueue.Count > 0 Then
                ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf SendAPIMessageQueue))
            End If

        End Sub

        Private Sub OnApiSocketConnectionFailed(ByRef sender As AsyncSocket) Handles _apisocket.ConnectionFailed

            ' Disconnect client after 5 failed connection attempts
            Interlocked.Increment(ApiConnectionAttempts)
            If ApiConnectionAttempts >= 4 Then
                Logger.Log(0, $"Client {WorkerOrId} does not have a respondig API interface. Disconnecting.")
                ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf Disconnect))
            End If

        End Sub

        Private Sub OnApiSocketDisconnected(ByRef sender As AsyncSocket) Handles _apisocket.Disconnected

            Logger.Log(7, String.Format("Client {0} API interface disconnected", WorkerOrId), _context)

        End Sub

        Private Sub OnApiSocketMessageReceived(ByRef sender As AsyncSocket, ByVal message As String) Handles _apisocket.MessageReceived

            ' Process the response
            ProcessAPIResponse(message)

        End Sub

#End Region

#Region " Scheduler"

        ''' <summary>
        ''' Performs scheduled tasks
        ''' </summary>
        Private Sub OnScheduleTimerElapsed() Handles _scheduleTimer.Elapsed

            If _scheduleRunning Then Return
            _scheduleRunning = True

            ' -----------------------------------------------------------
            ' Check client isn't idle for more than 1 minute
            ' -----------------------------------------------------------
            If IsConnected AndAlso IdleDuration.TotalMinutes > 1 Then

                ' Force disconnection
                Logger.Log(1, String.Format("{0} has been idle for more than 1 minute. Disconnecting.", WorkerOrId), _context)
                _scheduleRunning = False
                Disconnect()
                Return

            End If

            ' -----------------------------------------------------------
            ' Disconnect API if idle
            ' -----------------------------------------------------------
            If _apisocket IsNot Nothing AndAlso _apisocket.IdleDuration.TotalSeconds >= 15 Then
                StopApiConnection()
            End If

            ' -----------------------------------------------------------
            ' Disconnect API if idle
            ' -----------------------------------------------------------
            CheckWorkerSegment()

            ' -----------------------------------------------------------
            ' Eventually resubmit scheduler
            ' -----------------------------------------------------------
            _scheduleTimer.Reset((GetRandom(1000, 3000) * 10))
            _scheduleRunning = False

        End Sub

#Region " IDisposable Support"

        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then
                If disposing Then

                    _socket.Dispose()
                    If _apisocket IsNot Nothing Then _apisocket.Dispose()
                    If _scheduleTimer IsNot Nothing Then _scheduleTimer.Dispose()
                    _poolmgr = Nothing
                    _clntmgr = Nothing
                    _telemetry = Nothing
                    _settings = Nothing
                    ApiInfo = Nothing
                    ApiScrambleInfo = Nothing

                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                ' TODO: set large fields to null.
            End If
            disposedValue = True
        End Sub

        ' TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        'Protected Overrides Sub Finalize()
        '    ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        '    Dispose(False)
        '    MyBase.Finalize()
        'End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            ' TODO: uncomment the following line if Finalize() is overridden above.
            ' GC.SuppressFinalize(Me)
        End Sub
#End Region


#End Region

    End Class

End Namespace
