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
Imports System.Net

Namespace Pools

    ''' <summary>
    ''' Abstraction for an host endpoint
    ''' </summary>
    Public Class Pool

#Region " Fields"

        Private _host As String
        Private _ports() As Integer

        Private _ipAddressList As New List(Of PoolIpAddress)
        Private _ipEndPoints As New Queue(Of IPEndPoint)

        Private _lastProbedOn As DateTime = DateTime.MinValue
        Private _isValid As Boolean = False

        Public IsPrimary As Boolean = False                         '<-- Primary pool is the first being added

        ' Stratum login for THIS pool (if any)
        Private _stratumLogin As String                             '<-- Specific Authentication credentials for this Pool
        Private _stratumPassw As String = "x"                       '<-- Specific Authentication credentials for this Pool
        Private _stratumWorker As String = String.Empty             '<-- Specific Authentication credentials for this Pool

        Public DevFeeAddress As String = String.Empty               '<-- DevFee address for this Pool
        Public JobsReceived As Long = 0
        Public KnownStaleSolutions As Long = 0
        Public SolutionsSubmitted As Long = 0
        Public SolutionsAccepted As Long = 0
        Public SolutionsRejected As Long = 0
        Public SoftErrors As Integer = 0
        Public HardErrors As Integer = 0
        Public IsFeeAuthorized As Boolean = False


#End Region

#Region " Constructor"

        Public Sub New()
        End Sub

        Public Sub New(hostName As String, hostPort As Integer)

            _host = hostName
            AddPort(hostPort)

            ' If pool host is already an IP address then no need to probe
            ' and / or resolve
            Dim tmpIpAddress As Net.IPAddress = Nothing
            If Net.IPAddress.TryParse(_host, tmpIpAddress) Then
                _ipAddressList.Add(New PoolIpAddress With {.IpAddress = tmpIpAddress})
                _isValid = True
            Else
                ' Validate host name
                If Not Core.Helpers.IsValidHostName(_host) Then
                    Logger.Log(0, String.Format("Pool Host {0} is neither a valid IP address nor a valid hostname", _host), "Err")
                Else
                    _isValid = True
                End If
            End If

        End Sub

        Public Sub New(hostName As String, portNumbers() As Integer)

            _host = hostName
            For i As Integer = 0 To portNumbers.Length - 1
                If portNumbers(i) > 0 AndAlso portNumbers(i) < 65535 Then
                    AddPort(portNumbers(i))
                End If
            Next

            ' If pool host is already an IP address then no need to probe
            ' and / or resolve
            Dim tmpIpAddress As Net.IPAddress = Nothing
            If Net.IPAddress.TryParse(_host, tmpIpAddress) Then
                _ipAddressList.Add(New PoolIpAddress With {.IpAddress = tmpIpAddress})
                _isValid = True
            Else
                ' Validate host name
                If Not Core.Helpers.IsValidHostName(_host) Then
                    Logger.Log(0, String.Format("Pool Host {0} is neither a valid IP address nor a valid hostname", _host), "Err")
                Else
                    _isValid = True
                End If
            End If


        End Sub

#End Region

#Region " Properties"

        Public ReadOnly Property Host As String
            Get
                Return _host
            End Get
        End Property

        Public ReadOnly Property Ports As Integer()
            Get
                Return _ports
            End Get
        End Property

        Public ReadOnly Property LastProbedOn As DateTime
            Get
                Return _lastProbedOn
            End Get
        End Property

        Public ReadOnly Property IsValid As Boolean
            Get
                Return _isValid
            End Get
        End Property

        Public ReadOnly Property IsProbed As Boolean
            Get
                Return Not _lastProbedOn = DateTime.MinValue
            End Get
        End Property

        Public ReadOnly Property IpEndPoints As Queue(Of IPEndPoint)
            Get
                Return _ipEndPoints
            End Get
        End Property

        Public Property StratumLogin As String
            Get
                If _stratumLogin <> String.Empty Then
                    Return _stratumLogin
                ElseIf App.Instance.Settings.PoolsStratumLogin <> String.Empty Then
                    Return App.Instance.Settings.PoolsStratumLogin
                Else
                    Return String.Empty
                End If
            End Get
            Set(value As String)
                _stratumLogin = value
            End Set
        End Property

        Public Property StratumPassw As String
            Get
                If _stratumLogin <> String.Empty Then
                    Return _stratumPassw
                ElseIf App.Instance.Settings.PoolsStratumLogin <> String.Empty Then
                    Return App.Instance.Settings.PoolsStratumPassword
                Else
                    Return String.Empty
                End If
            End Get
            Set(value As String)
                _stratumPassw = value
            End Set
        End Property

        Public Property StratumWorker As String
            Get
                If _stratumWorker <> String.Empty Then
                    Return _stratumWorker
                ElseIf App.Instance.Settings.PoolsStratumWorker <> String.Empty Then
                    Return App.Instance.Settings.PoolsStratumWorker
                Else
                    Return String.Empty
                End If
            End Get
            Set(value As String)
                _stratumWorker = value
            End Set
        End Property

#End Region

#Region " Methods"

        Public Sub AddPort(portNumber As Integer)

            If _ports Is Nothing Then
                _ports = {portNumber}
            Else
                If Not _ports.Contains(portNumber) Then
                    Array.Resize(Of Integer)(_ports, _ports.Length + 1)
                    _ports(_ports.Length - 1) = portNumber
                End If
            End If

        End Sub

        Public Sub AddPort(portNumbers As Integer())
            For i As Integer = 0 To portNumbers.Length - 1
                AddPort(portNumbers(i))
            Next
        End Sub

        Public Sub InitIpEndPointsQueue()

            _ipEndPoints = New Queue(Of IPEndPoint)

            If _ipAddressList.Count > 0 Then

                _ipAddressList.Sort()
                For addrIdx As Integer = 0 To _ipAddressList.Count - 1
                    For portIdx As Integer = 0 To _ports.Length - 1
                        _ipEndPoints.Enqueue(New IPEndPoint(_ipAddressList(addrIdx).IpAddress, _ports(portIdx)))
                    Next
                Next

            End If

        End Sub

        Public Function GetAddressQueue() As Queue(Of IPAddress)

            If _ipAddressList.Count = 0 Then Return New Queue(Of IPAddress)
            _ipAddressList.Sort()
            Dim retQueue As New Queue(Of IPAddress)
            For i As Integer = 0 To _ipAddressList.Count - 1
                retQueue.Enqueue(_ipAddressList(i).IpAddress)
            Next
            Return retQueue

        End Function

        Public Sub Resolve()

            ' If Host is already an IP address no need to resolve
            If _ipAddressList.Count > 0 AndAlso (_ipAddressList(0).IpAddress.ToString = _host) Then Return

            Dim hostInfo As IPHostEntry = Nothing
            Try
                hostInfo = Dns.GetHostEntry(_host)
                For Each tmpIpAddress As IPAddress In hostInfo.AddressList
                    _ipAddressList.Add(New PoolIpAddress With {.IpAddress = tmpIpAddress})
                Next
                _isValid = True
            Catch ex As Exception
                Logger.Log(0, String.Format("Pool {0} does not resolve to any valid Ip", _host), "Err")
                _isValid = False
            End Try

        End Sub

        Public Async Function ResolveAsync() As Task

            ' If Host is already an IP address no need to resolve
            If _ipAddressList.Count > 0 AndAlso (_ipAddressList(0).IpAddress.ToString = _host) Then Return

            Dim hostInfo As IPHostEntry = Nothing
            Try
                hostInfo = Await Dns.GetHostEntryAsync(_host)
                For Each tmpIpAddress As IPAddress In hostInfo.AddressList
                    _ipAddressList.Add(New PoolIpAddress With {.IpAddress = tmpIpAddress})
                Next
                _isValid = True
            Catch ex As Exception
                Logger.Log(0, String.Format("Pool {0} does not resolve to any valid Ip", _host), "Err")
                _isValid = False
            End Try

        End Function

        Public Async Function ResolveAsync(refresh As Boolean) As Task

            _ipAddressList.Clear()
            _isValid = False
            Await ResolveAsync()

        End Function

        Public Async Function ProbeAsync() As Task

            ' No need to probe for less than 2 ipaddresses
            If _ipAddressList.Count < 2 Then
                _ipAddressList.Sort()
                Return
            End If

            Logger.Log(2, String.Format("Probing {1}'s {0} ip addresses", _ipAddressList.Count, _host), "Probe")


            Dim pingQry As NetworkInformation.Ping = New NetworkInformation.Ping()
            Dim pingRep As NetworkInformation.PingReply
            Dim pingRtt As Integer()

            For Each _poolIpAddress As PoolIpAddress In _ipAddressList

                ' 10 tests
                pingRtt = Enumerable.Repeat(Of Integer)(1000, 10).ToArray
                For testNum As Integer = 0 To pingRtt.Length - 1

                    Try
                        pingRep = Await pingQry.SendPingAsync(address:=_poolIpAddress.IpAddress, timeout:=pingRtt(testNum))
                        'Console.WriteLine([Enum].GetName(GetType(NetworkInformation.IPStatus), pingRep.Status))
                        If pingRep.Status = NetworkInformation.IPStatus.TimedOut Then Exit For
                        pingRtt(testNum) = pingRep.RoundtripTime
                    Catch ex As Exception
                        ' Lack of response or timeout
                    End Try

                Next

                ' Store avg response time
                _poolIpAddress.RoundTripTime = CInt(Math.Round(pingRtt.Average(), 0))
                _poolIpAddress.Icmp = True
                Logger.Log(2, String.Format("Ip {0,16} : avg time {1} ms.", _poolIpAddress.IpAddress.ToString, _poolIpAddress.RoundTripTime), "Probe")

            Next

            _ipAddressList.Sort()
            _lastProbedOn = DateTime.Now

        End Function


#End Region

#Region " Private Classes"

        Private Class PoolIpAddress

            Implements IComparable(Of PoolIpAddress)

            Public IpAddress As Net.IPAddress
            Public Icmp As Boolean
            Public RoundTripTime As Integer

            Public Function CompareTo(other As PoolIpAddress) As Integer Implements IComparable(Of PoolIpAddress).CompareTo

                If other Is Nothing Then Return 1
                If Icmp AndAlso Not other.Icmp Then Return -1
                If Not Icmp AndAlso other.Icmp Then Return 1
                If Not Icmp AndAlso Not other.Icmp Then Return 0

                Return RoundTripTime.CompareTo(other.RoundTripTime)

            End Function

        End Class

#End Region

    End Class


End Namespace