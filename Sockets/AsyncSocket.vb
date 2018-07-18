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
Imports System.Json
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading


Namespace Sockets

    ''' <summary>
    ''' This is the base class which all socket-handling objects inherit from
    ''' </summary>
    Public Class AsyncSocket
        Implements IDisposable

#Region " Fields"

        ' Socket related members
        Protected _connectionStatus As AsyncSocketStatus = AsyncSocketStatus.NotConnected
        Protected _connectionSocket As Socket
        Protected _connectionRemoteEndPoint As IPEndPoint
        Protected _connectionLocalEndPoint As IPEndPoint
        Protected _dataBuffer As String = String.Empty

        Protected _context As String

        ' Async events members
        Protected _asyncConnArgs As SocketAsyncEventArgs
        Protected _asyncReadArgs As SocketAsyncEventArgs
        Protected _asyncSendArgs As New Concurrent.ConcurrentStack(Of SocketAsyncEventArgs)
        Protected _asyncActiveSends As Integer = 0

        Protected Shared _lockObj As New Object

        ' Activity timestamps
        Protected _lastInboundTimeStamp As DateTime = DateTime.MinValue             ' Records timestamp of last successfully sent message
        Protected _lastOutboundTimeStamp As DateTime = DateTime.MinValue            ' Records timestamp of last successfully received message
        Protected _connectedTimeStamp As DateTime = DateTime.MinValue               ' Records effective timestamp of connection

#End Region

#Region " Constructor"

        ''' <summary>
        ''' Standard constructor
        ''' </summary>
        Public Sub New(Optional context As String = "Socket")
            _context = context
        End Sub

        ''' <summary>
        ''' Creates a new instance with an already existing
        ''' </summary>
        ''' <param name="fromSocket">An already existing <see cref="Socket"/> object</param>
        Public Sub New(ByRef fromSocket As Socket, Optional context As String = "Socket")
            _context = context
            _connectionSocket = fromSocket
            If _connectionSocket.Connected() Then Call OnConnected()
        End Sub

#End Region

#Region " Events"

        Public Event Connected(ByRef sender As Object)
        Public Event ConnectionFailed(ByRef sender As Object)
        Public Event Disconnected(ByRef sender As Object)
        Public Event MessageReceived(ByRef sender As Object, ByVal Message As String)
        Public Event SendFailed(ByRef sender As Object)
        Public Event ReceiveFailed(ByRef sender As Object)

#End Region

#Region " Properties"

        ''' <summary>
        ''' Gets wether or not this socket is connected
        ''' </summary>
        ''' <returns>True Or False</returns>
        Public ReadOnly Property IsConnected
            Get
                If _connectionSocket Is Nothing Then Return False
                Return (_connectionStatus.HasFlags({AsyncSocketStatus.Connected}) AndAlso Not IsPendingState)
            End Get
        End Property

        ''' <summary>
        ''' Gets wether or not this socket is in pending connection/disconnection operations
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IsPendingState As Boolean
            Get
                Return _connectionStatus.HasAnyFlag({AsyncSocketStatus.Disconnecting, AsyncSocketStatus.Connecting})
            End Get
        End Property

        ''' <summary>
        ''' Gets the active local endpoint
        ''' </summary>
        ''' <returns>An <see cref="IPEndPoint"/> object or Nothing</returns>
        Public ReadOnly Property LocalEndPoint() As IPEndPoint
            Get
                Return _connectionLocalEndPoint
            End Get
        End Property

        ''' <summary>
        ''' Gets the active remote endpoint
        ''' </summary>
        ''' <returns>An <see cref="IPEndPoint"/> object or Nothing</returns>
        Public ReadOnly Property RemoteEndPoint() As IPEndPoint
            Get
                Return _connectionRemoteEndPoint
            End Get
        End Property

        ''' <summary>
        ''' Gets timestamp of last succesfully received message
        ''' </summary>
        ''' <returns>A <see cref="Date"/></returns>
        Public ReadOnly Property LastInboundTimeStamp As DateTime
            Get
                Return _lastInboundTimeStamp
            End Get
        End Property

        ''' <summary>
        ''' Gets timestamp of last successfully received message
        ''' </summary>
        ''' <returns>A <see cref="Date"/></returns>
        Public ReadOnly Property LastOutboundTimeStamp As DateTime
            Get
                Return _lastOutboundTimeStamp
            End Get
        End Property

        ''' <summary>
        ''' Gets the duration this socket has been Idle 
        ''' </summary>
        ''' <returns>A <see cref="TimeSpan"/></returns>
        Public ReadOnly Property IdleDuration As TimeSpan
            Get
                If Not IsConnected Then
                    Return New TimeSpan(0)
                Else
                    If _lastInboundTimeStamp >= _lastOutboundTimeStamp Then
                        Return DateTime.Now.Subtract(_lastInboundTimeStamp)
                    Else
                        Return DateTime.Now.Subtract(_lastOutboundTimeStamp)
                    End If
                End If
            End Get
        End Property

        ''' <summary>
        ''' Gets timestamp of connection
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ConnectedTimestamp As DateTime
            Get
                Return _connectedTimeStamp
            End Get
        End Property

        ''' <summary>
        ''' Gets the duration of this connection
        ''' </summary>
        ''' <returns>A <see cref="TimeSpan"/> object</returns>
        Public ReadOnly Property ConnectionDuration As TimeSpan
            Get
                If _connectedTimeStamp = DateTime.MinValue Then
                    Return New TimeSpan(0, 0, 0)
                End If
                Return DateTime.Now.Subtract(_connectedTimeStamp)
            End Get
        End Property

        ''' <summary>
        ''' Gets Timestamp of last successful message (in or out)
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property LastActivityTimestamp As DateTime
            Get
                If _lastInboundTimeStamp > _lastOutboundTimeStamp Then
                    Return _lastInboundTimeStamp
                Else
                    Return _lastOutboundTimeStamp
                End If
            End Get
        End Property

#End Region

#Region " Handlers"

        ''' <summary>
        ''' Occurs immediately after a succesful connection
        ''' </summary>
        Private Sub OnConnected()

            _connectedTimeStamp = DateTime.Now

            With _connectionStatus
                .SetFlags({AsyncSocketStatus.Connected})
                .UnsetFlags({AsyncSocketStatus.Connecting, AsyncSocketStatus.NotConnected})
            End With

            Try

                With _connectionSocket
                    .LingerState = New LingerOption(False, 0)
                    .SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, True)
                    .SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, True)
                    _connectionRemoteEndPoint = ConvertToIPEndPoint(.RemoteEndPoint)
                    _connectionLocalEndPoint = ConvertToIPEndPoint(.LocalEndPoint)
                End With

            Catch sex As SocketException

                ' Something bad happened to remote endpoint
                Disconnect()
                Return

            Catch ex As Exception

            End Try

            SetKeepAlive(_connectionSocket, 300000, 30000)

            ' Raise event
            If ConnectedEvent IsNot Nothing Then RaiseEvent Connected(Me)

        End Sub

        ''' <summary>
        ''' Occurs immediately after a failed connection attempt
        ''' </summary>
        Private Sub OnConnectionFailed()

            _connectionStatus.UnsetFlags({AsyncSocketStatus.Connecting})
            _connectionSocket = Nothing

            ' Raise event
            If ConnectionFailedEvent IsNot Nothing Then RaiseEvent ConnectionFailed(Me)

        End Sub

        ''' <summary>
        ''' Occurs immediately after a disconnection
        ''' </summary>
        Private Sub OnDisconnected()


            ' Signal Status of connection
            SyncLock _lockObj
                _connectionStatus.UnsetFlags({AsyncSocketStatus.Disconnecting, AsyncSocketStatus.Connected})
                _connectionStatus.SetFlags({AsyncSocketStatus.NotConnected})
            End SyncLock

            _connectedTimeStamp = DateTime.MinValue
            _lastInboundTimeStamp = DateTime.MinValue
            _lastOutboundTimeStamp = DateTime.MinValue
            _connectionSocket = Nothing

            Logger.Log(7, String.Format("Disconnected from {0}", _connectionRemoteEndPoint), _context)

            ' Empty data buffer and AsyncSocketEventargs
            _dataBuffer = String.Empty
            FlushSendArgs()

            ' Raise event
            If DisconnectedEvent IsNot Nothing Then RaiseEvent Disconnected(Me)

        End Sub


#End Region


#Region " Socket Operations"

        ''' <summary>
        ''' Starts receiving asynchronously from socket
        ''' </summary>
        Public Sub BeginReceive()

            If Not IsConnected Then Return

            If _asyncReadArgs Is Nothing Then
                _asyncReadArgs = New SocketAsyncEventArgs With {.RemoteEndPoint = RemoteEndPoint}
                AddHandler _asyncReadArgs.Completed, AddressOf OnAsyncIOCompleted
                _asyncReadArgs.SetBuffer(New Byte(DEFAULT_BUFFER_SIZE) {}, 0, DEFAULT_BUFFER_SIZE)
            End If

            Try

                If Not (_connectionSocket.ReceiveAsync(_asyncReadArgs)) Then
                    OnAsyncIOCompleted(_connectionSocket, _asyncReadArgs)
                End If

            Catch ex As Exception

                ' Another receiving operation is already pending
                ' This should not happen. Disconnect client
                Disconnect()

            End Try

        End Sub

        ''' <summary>
        ''' Starts a connection to remote endpoint
        ''' </summary>
        Public Sub Connect(ByRef toEndPoint As IPEndPoint)

            If IsConnected OrElse IsPendingState Then Return

            _connectionStatus.SetFlags({AsyncSocketStatus.Connecting})
            _connectionSocket = New Socket(toEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)

            If _asyncConnArgs Is Nothing Then
                _asyncConnArgs = New SocketAsyncEventArgs
                AddHandler _asyncConnArgs.Completed, AddressOf OnAsyncIOCompleted
            End If
            _asyncConnArgs.RemoteEndPoint = toEndPoint

            If Not _connectionSocket.ConnectAsync(_asyncConnArgs) Then
                OnAsyncIOCompleted(_connectionSocket, _asyncConnArgs)
            End If

        End Sub

        ''' <summary>
        ''' Performs clean disconnect of socket
        ''' </summary>
        Public Sub Disconnect()

            If Not IsConnected OrElse IsPendingState Then Return

            _connectionStatus.SetFlags({AsyncSocketStatus.Disconnecting})

            Try

                SyncLock _lockObj
                    _connectionStatus.SetFlags({AsyncSocketStatus.Disconnecting})
                    _connectionSocket.Shutdown(SocketShutdown.Both)
                    _connectionSocket.Disconnect(False)
                    _connectionSocket.Close(500)
                    _connectionSocket = Nothing
                End SyncLock

            Catch ex As Exception

                Logger.Log(0, String.Format("Error while closing connection with {0} : {1}", _connectionRemoteEndPoint, ex.GetBaseException.Message), _context)

            End Try

            OnDisconnected()


        End Sub

        ''' <summary>
        ''' Reads messages stored in buffer and processes them one by one
        ''' </summary>
        Private Sub QueueData()

            ' Split accumulated data in messages separated by newLine char
            Static offset As Integer = 0
            Static message As String = String.Empty

            offset = 0
            message = String.Empty

            Do While (offset >= 0 AndAlso _dataBuffer.Length > 0)

                offset = _dataBuffer.IndexOf(ChrW(10), 0)
                If offset >= 0 Then
                    message = _dataBuffer.Substring(0, offset)
                    _dataBuffer = _dataBuffer.Remove(0, offset + 1)
                    If MessageReceivedEvent IsNot Nothing Then RaiseEvent MessageReceived(Me, message)
                End If

            Loop

        End Sub

        ''' <summary>
        ''' Gets data from the Socket
        ''' </summary>
        Private Sub ReceiveSocketData(e As SocketAsyncEventArgs)

            If (Not IsConnected OrElse (e Is Nothing)) Then
                Return
            End If

            'If BytesTransferred is 0, then the remote endpoint closed the connection
            If (e.SocketError = SocketError.Success) Then

                If e.BytesTransferred > 0 Then

                    ' Append transferred data to dataBuffer
                    _dataBuffer += Encoding.ASCII.GetString(e.Buffer, e.Offset, e.BytesTransferred)

                    ' If the read socket is empty, we can do something with the data that we accumulated
                    ' from all of the previous read requests on this socket
                    If (_connectionSocket.Available = 0) Then
                        Call QueueData()
                    End If

                    ' Start another receive request and immediately check to see if the receive is already complete
                    ' Otherwise OnClientIOCompleted will get called when the receive is complete
                    ' We are basically calling this same method recursively until there is no more data
                    ' on the read socket
                    Try
                        If _connectionSocket IsNot Nothing AndAlso Not _connectionSocket.ReceiveAsync(e) Then
                            OnAsyncIOCompleted(_connectionSocket, e)
                        End If
                    Catch ex As Exception
                        Disconnect()
                    End Try

                Else

                    Logger.Log(7, String.Format("{0} remotely closed connection", RemoteEndPoint.ToString), _context)

                    ' Gracefully closes resources
                    Disconnect()

                End If


            Else

                If e.SocketError <> SocketError.OperationAborted Then
                    Logger.Log(0, String.Format("{0} socket error : {1}", RemoteEndPoint.ToString, [Enum].GetName(GetType(SocketError), e.SocketError).ToString()), _context)
                End If

                ' Gracefully closes resources
                Disconnect()

            End If

        End Sub

        ''' <summary>
        ''' Sends data to socket
        ''' </summary>
        ''' <param name="message">The mesasge to be sent</param>
        ''' <remarks>Lf separator is automatically added</remarks>
        Public Sub Send(message As String)

            If Not IsConnected Then Return

            Dim sendArg As SocketAsyncEventArgs = GetSendArg()
            Dim sendBuffer As Byte() = Encoding.ASCII.GetBytes(message + ChrW(10))
            sendArg.SetBuffer(sendBuffer, 0, sendBuffer.Length)

            ' If operation is ran synchronously immediately reuse asyncEvent
            Try
                If Not _connectionSocket.SendAsync(sendArg) Then
                    OnAsyncIOCompleted(_connectionSocket, sendArg)
                End If
            Catch ex As Exception
                Logger.Log(0, String.Format("Failed to send : {0}", ex.GetBaseException.Message), _context)
            End Try

        End Sub

        ''' <summary>
        ''' Sends data to socket
        ''' </summary>
        ''' <param name="jsonMessage">A <see cref="Json.JsonObject"/></param>
        ''' <remarks>Lf separator is automatically added</remarks>
        Public Sub Send(jsonMessage As JsonObject)
            Send(message:=jsonMessage.ToString)
        End Sub

#End Region

#Region " Helpers"

        ''' <summary>
        ''' Gets a SocketAsyncEventArg if available or creates one
        ''' </summary>
        ''' <returns>An <see cref="SocketAsyncEventArgs"/> object</returns>
        Private Function GetSendArg() As SocketAsyncEventArgs

            Dim retVar As SocketAsyncEventArgs = Nothing

            If _asyncSendArgs.TryPop(retVar) Then
                retVar.RemoteEndPoint = RemoteEndPoint()
            Else
                retVar = New SocketAsyncEventArgs With {.RemoteEndPoint = RemoteEndPoint()}
                AddHandler retVar.Completed, AddressOf OnAsyncIOCompleted
            End If

            Interlocked.Increment(_asyncActiveSends)
            Return retVar

        End Function

        ''' <summary>
        ''' Stores a SocketAsyncEventArg for future reuse
        ''' </summary>
        ''' <param name="e">An <see cref="SocketAsyncEventArgs"/> object</param>
        Private Sub RecycleSendArg(e As SocketAsyncEventArgs)

            Interlocked.Decrement(_asyncActiveSends)
            If IsConnected Then _asyncSendArgs.Push(e)

        End Sub

        ''' <summary>
        ''' Releases all created SocketAsyncEventArgs
        ''' </summary>
        Private Sub FlushSendArgs()

            Dim item As SocketAsyncEventArgs = Nothing

            While _asyncSendArgs.TryPop(item) > 0
                RemoveHandler item.Completed, AddressOf OnAsyncIOCompleted
                item.Dispose()
            End While

            item = Nothing

        End Sub

#End Region


#Region " Callbacks for async operations"


        ''' <summary>
        ''' Handles async IO operations
        ''' </summary>
        Private Sub OnAsyncIOCompleted(sender As Object, e As SocketAsyncEventArgs)

            If e Is Nothing Then

                Call Disconnect()

            Else

                Select Case e.LastOperation

                    Case SocketAsyncOperation.Connect

                        If e.SocketError = SocketError.Success Then

                            Logger.Log(7, String.Format("Connected to {0}", e.RemoteEndPoint.ToString), _context)
                            Call OnConnected()

                        Else

                            Logger.Log(7, String.Format("Connection to {0} failed [ {1} ]", e.RemoteEndPoint.ToString, [Enum].GetName(GetType(SocketError), e.SocketError).ToString()), _context)
                            Call OnConnectionFailed()

                        End If

                    Case SocketAsyncOperation.Disconnect

                        Logger.Log(7, String.Format("Disconnected from {0}", e.RemoteEndPoint.ToString), _context)
                        Call OnDisconnected()

                    Case SocketAsyncOperation.Receive

                        If e.SocketError = SocketError.Success Then

                            If e.BytesTransferred > 0 Then

                                _lastInboundTimeStamp = DateTime.Now
                                If Not disposedValue Then Call ReceiveSocketData(e)

                            Else

                                If Not _connectionStatus.HasFlags({AsyncSocketStatus.Disconnecting}) Then
                                    Logger.Log(7, String.Format("{0} remotely closed connection", e.RemoteEndPoint.ToString), _context)

                                    ' Gracefully closes resources
                                    Call Disconnect()
                                End If

                            End If
                        Else

                            If (e.SocketError <> SocketError.NotConnected AndAlso
                                e.SocketError <> SocketError.OperationAborted) Then

                                Logger.Log(0, String.Format("{0} socket error : {1}", e.RemoteEndPoint.ToString, [Enum].GetName(GetType(SocketError), e.SocketError).ToString()), _context)

                                ' Gracefully closes resources
                                Disconnect()

                            End If

                        End If

                    Case SocketAsyncOperation.Send

                        If e.SocketError <> SocketError.Success OrElse e.BytesTransferred = 0 Then
                            Logger.Log(0, String.Format("Failed to send to {0} : {1}", e.RemoteEndPoint.ToString, [Enum].GetName(GetType(SocketError), e.SocketError).ToString()), _context)
                            Call RecycleSendArg(e)
                            Call Disconnect()
                            Return
                        End If

                        _lastOutboundTimeStamp = DateTime.Now
                        Call RecycleSendArg(e)

                End Select

            End If

        End Sub

#End Region

#Region " Helpers"

        Private Function ConvertToIPEndPoint(ByRef ep As EndPoint) As IPEndPoint

            Dim ipAddress As IPAddress = IPAddress.Parse(ep.ToString.Split(":")(0))
            Dim port As Integer = Convert.ToInt32(ep.ToString.Split(":")(1))
            Return New IPEndPoint(ipAddress, port)

        End Function

        ''' <summary>
        ''' Sets keep-alive intervals for socket
        ''' </summary>
        ''' <param name="tcpSocket">The socket to manage</param>
        ''' <param name="keepAliveTime">First keep-alive in ms</param>
        ''' <param name="keepAliveInterval">Retry keep-alive in ms</param>
        ''' <returns></returns>
        Private Function SetKeepAlive(ByRef tcpSocket As Socket, ByVal keepAliveTime As UInteger, ByVal keepAliveInterval As UInteger) As Boolean

            ' Pack three params into 12-element byte array; not sure about endian issues on non-Intel
            Dim SIO_KEEPALIVE_VALS(11) As Byte
            Dim keepAliveEnable As UInteger = 1
            If (keepAliveTime = 0 Or keepAliveInterval = 0) Then keepAliveEnable = 0
            ' Bytes 00-03 are 'enable' where '1' is true, '0' is false
            ' Bytes 04-07 are 'time' in milliseconds
            ' Bytes 08-12 are 'interval' in milliseconds
            Array.Copy(BitConverter.GetBytes(keepAliveEnable), 0, SIO_KEEPALIVE_VALS, 0, 4)
            Array.Copy(BitConverter.GetBytes(keepAliveTime), 0, SIO_KEEPALIVE_VALS, 4, 4)
            Array.Copy(BitConverter.GetBytes(keepAliveInterval), 0, SIO_KEEPALIVE_VALS, 8, 4)

            Try
                Dim result() As Byte = BitConverter.GetBytes(CUInt(0)) ' Result needs 4-element byte array?
                tcpSocket.IOControl(IOControlCode.KeepAliveValues, SIO_KEEPALIVE_VALS, result)
            Catch e As Exception
                Return False
            End Try
            Return True

        End Function


#End Region

#Region "IDisposable Support"

        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then

                If disposing Then

                    If IsConnected Then Disconnect()

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

    End Class

End Namespace
