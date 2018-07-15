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
Imports System.Net
Imports System.Net.Sockets
Imports System.Text

Namespace Api

    Public Class ApiClient

        ' Pointers to singletons
        Private _settings As Core.Settings
        Private _clntmgr As Clients.ClientsManager

        ' The base socket handler
        Private WithEvents _socket As AsyncSocket = Nothing

        ' Logging Context
        Private _context As String = "Api"
        Private _lockObj As New Object

        Public ReadOnly Property Id As String                                           ' Unique identifier

#Region " Constructor"

        Private Sub New()
        End Sub


        Public Sub New(ByRef acceptedSocket As Socket)

            ' Retrieve singletons
            _settings = App.Instance.Settings
            _clntmgr = App.Instance.ClntMgr

            ' Start a new async socket
            _Id = acceptedSocket.RemoteEndPoint.ToString()
            _socket = New AsyncSocket(acceptedSocket, "Api")
            _socket.BeginReceive()

        End Sub


#End Region

#Region " Properties"

        ''' <summary>
        ''' Whether or not this socket is connected
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IsConnected
            Get
                If _socket Is Nothing Then Return False
                Return _socket.IsConnected
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
        ''' Returns how much time this connection has been idle
        ''' </summary>
        ''' <returns>A <see cref="TimeSpan"/> object</returns>
        Public ReadOnly Property IdleDuration As TimeSpan
            Get
                If _socket Is Nothing Then Return New TimeSpan(0, 0, 0)
                Return _socket.IdleDuration
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



#End Region

#Region " Events"

        Public Event Disconnected(ByRef sender As ApiClient)

#End Region

#Region " Methods"

        ''' <summary>
        ''' Issues immediate disconnection of the underlying socket
        ''' and signals client disconnection
        ''' </summary>
        Public Sub Disconnect()

            _socket.Disconnect()
            If DisconnectedEvent IsNot Nothing Then RaiseEvent Disconnected(Me)

        End Sub

        ''' <summary>
        ''' Handles the incoming message
        ''' </summary>
        ''' <param name="message">A Json object string</param>
        Private Sub ProcessMessage(message As String)

            ' Out message received
            If _settings.LogVerbosity >= 9 Then Logger.Log(9, "<< " & message, _context)

            Dim jReq As JsonObject = Nothing
            Dim jRes As JsonObject = New JsonObject
            Dim msgId As Integer = 0
            Dim msgMethod As String = String.Empty

            jRes("id") = Nothing
            jRes("jsonrpc") = "2.0"


            Try

                jReq = JsonValue.Parse(message)
                With jReq
                    If .ContainsKey("id") Then .TryGetValue("id", msgId)
                    If .ContainsKey("method") Then .TryGetValue("method", msgMethod)
                    If Not .ContainsKey("jsonrpc") Then Throw New Exception("Missing jsonrpc member")
                    If Not .Item("jsonrpc") = "2.0" Then Throw New Exception("Invalid jsonrpc value")
                End With

            Catch ex As Exception

                ' Invalid format of json
                Logger.Log(0, String.Format("Json parse failed from api client {1} : {0}", ex.GetBaseException.Message, Id), _context)
                jRes("error") = New JsonObject From {
                    New KeyValuePair(Of String, JsonValue)("code", -32700),
                    New KeyValuePair(Of String, JsonValue)("message", ex.GetBaseException.Message)
                    }
                _socket.Send(jRes.ToString)

                Return

            End Try

            ' Apply message id
            ' as we respond with the same
            jRes("id") = msgId


            ' Handle message
            Select Case True

                Case msgMethod = "ping"

                    ' Reply to proxy check of liveness
                    jRes("result") = "pong"
                    _socket.Send(jRes.ToString)

                Case msgMethod = "quit"

                    ' Close the underlying connection
                    jRes("result") = True
                    _socket.Send(jRes.ToString)

                    Call Disconnect()

                Case msgMethod = "workers.getlist"

                    ' Retrieve a list of connected miners
                    Dim jArr As Json.JsonArray = New Json.JsonArray
                    Dim cList As List(Of Clients.Client) = _clntmgr.Clients.ToList()
                    If cList.Count > 0 Then

                        ' Sort workers by name
                        cList.Sort(Function(x As Clients.Client, y As Clients.Client)
                                       Return x.WorkerOrId.CompareTo(y.WorkerOrId)
                                   End Function)
                        Dim i As Integer = 0

                        For Each c As Clients.Client In cList.OrderBy(Function(s) s.WorkerOrId)
                            i += 1
                            Dim jItem As New JsonObject From {
                            New KeyValuePair(Of String, JsonValue)("index", i),
                            New KeyValuePair(Of String, JsonValue)("connected", c.IsConnected),
                            New KeyValuePair(Of String, JsonValue)("runtime", c.ConnectionDuration.TotalSeconds()),
                            New KeyValuePair(Of String, JsonValue)("worker", c.WorkerOrId),
                            New KeyValuePair(Of String, JsonValue)("hashrate", c.HashRate),
                            New KeyValuePair(Of String, JsonValue)("submitted", c.SolutionsSubmitted),
                            New KeyValuePair(Of String, JsonValue)("stales", c.KnownStaleSolutions),
                            New KeyValuePair(Of String, JsonValue)("accepted", c.SolutionsAccepted),
                            New KeyValuePair(Of String, JsonValue)("rejected", c.SolutionsRejected),
                            New KeyValuePair(Of String, JsonValue)("lastsubmit", c.LastSubmittedTimestamp.ToString())}
                            jArr.Add(jItem)

                        Next

                        jRes("result")("count") = i


                    Else

                        jRes("result")("count") = 0

                    End If

                    jRes("result")("workers") = jArr
                    _socket.Send(jRes.ToString)

                Case Else

                    ' Any other not implemented (yet ?)
                    jRes("error") = New JsonObject From {
                    New KeyValuePair(Of String, JsonValue)("code", -32700),
                    New KeyValuePair(Of String, JsonValue)("message", "Method Not implement or Not available")
                    }
                    _socket.Send(jRes.ToString)
                    Logger.Log(0, String.Format("Client {0} sent invalid method {1}", Id, msgMethod), _context)

            End Select


        End Sub

        ''' <summary>
        ''' Sends the specified message through the underlying socket
        ''' </summary>
        ''' <param name="message"></param>
        Public Sub Send(ByVal message As String)

            _socket.Send(message)

        End Sub



#End Region

#Region " Async Socket Event Handlers"

        Private Sub OnSocketConnected(ByRef sender As AsyncSocket) Handles _socket.Connected
            Logger.Log(3, String.Format("New API connection from {0}", sender.RemoteEndPoint.ToString()), _context)
        End Sub

        Private Sub OnSocketDisconnected(ByRef sender As AsyncSocket) Handles _socket.Disconnected

            Disconnect()

        End Sub

        Private Sub OnSocketMessageReceived(ByRef sender As AsyncSocket, ByVal message As String) Handles _socket.MessageReceived

            ' Queue the message processing
            ProcessMessage(message)

        End Sub

#End Region

    End Class

End Namespace
