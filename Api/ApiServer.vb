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
Imports System.Net.Sockets

Namespace Api

    Public Class ApiServer

#Region " Fields"

        ' Ref to Singletons
        Private _telemetry As Telemetry = App.Instance.Telemetry
        Private _settings As Settings = App.Instance.Settings
        Private _poolmgr As Pools.PoolManager = App.Instance.PoolMgr

        ' Logging context
        Protected _context As String = "Api"
        Protected Shared _lockObj As New Object

        ' This is server socket
        Private _serverSocket As Socket
        Private _isRunning As Boolean = False

        ' Here is our list of connected clients
        Protected _clientsList As New List(Of ApiClient)

#End Region

#Region " Constructor"

        Public Sub New()
        End Sub

#End Region

#Region " Methods"

        ''' <summary>
        ''' Starts the server and begin listen for incoming connections
        ''' </summary>
        ''' <returns>True or False</returns>
        Public Function Start() As Boolean

            If _settings.ApiListenerEndPoint Is Nothing Then Return False

            Try

                _serverSocket = New Socket(_settings.ApiListenerEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)

                ' Now make it a listener socket at the IP address and port that we specified
                _serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1)
                _serverSocket.Bind(_settings.ApiListenerEndPoint)

                ' Now start listening on the listener socket and wait for asynchronous client connections
                _serverSocket.Listen(Core.DEFAULT_MAX_CONNECTIONS)
                Logger.Log(1, String.Format("Accepting client connections on {0}", _settings.ApiListenerEndPoint), _context)

                _isRunning = True

                ' Begin accepting connections
                StartAcceptClientAsync()

                Return True

            Catch ex As Exception

                Logger.Log(0, ex.GetBaseException.Message, _context)
                Return False

            End Try


        End Function

        ''' <summary>
        ''' This method is called once to stop the server if it is started.
        ''' </summary>
        Public Sub [Stop]()

            _isRunning = False

            ' Close all clients
            While _clientsList.Count > 0
                _clientsList(_clientsList.Count - 1).Disconnect()
            End While

            If _serverSocket IsNot Nothing Then

                Try

                    _serverSocket.Shutdown(SocketShutdown.Both)
                    _serverSocket.Disconnect(True)
                    _serverSocket.Close()
                    _serverSocket.Dispose()

                Catch ex As Exception

                End Try

            End If

        End Sub

        ''' <summary>
        ''' Processes the accept socket connection
        ''' </summary>
        ''' <param name="e">An <see cref="SocketAsyncEventArgs"/> object</param>
        Public Sub ProcessClientAccept(e As SocketAsyncEventArgs)

            ' First we get the accept socket from the passed in arguments
            Dim acceptSocket As Socket = e.AcceptSocket

            ' If the accept socket is connected to a client we will process it
            ' otherwise nothing happens
            If acceptSocket.Connected Then


                If _isRunning Then


                    Try

                        Logger.Log(1, String.Format("Connection request from {0}", acceptSocket.RemoteEndPoint.ToString), _context)

                        ' Initialize a new client which will begin to receive
                        ' immediately
                        SyncLock _lockObj

                            Dim newClient As New ApiClient(e.AcceptSocket)
                            AddHandler newClient.Disconnected, AddressOf OnClientDisconnected
                            _clientsList.Add(newClient)

                        End SyncLock

                    Catch ex As Exception

                        acceptSocket.Disconnect(False)
                        acceptSocket.Close()
                        Logger.Log(0, ex.GetBaseException.Message, _context)

                    End Try

                    ' Start the process again to wait for the next connection
                    StartAcceptClientAsync()

                Else

                    Logger.Log(1, String.Format("Connection request from {0} rejected: Stopping ...", acceptSocket.RemoteEndPoint.ToString), _context)
                    acceptSocket.Disconnect(False)
                    acceptSocket.Close()

                End If


            End If


        End Sub

#End Region

#Region " Async Worker"

        ''' <summary>
        ''' This method implements the asynchronous loop of events
        ''' that accepts incoming client connections
        ''' </summary>
        Public Sub StartAcceptClientAsync(Optional e As SocketAsyncEventArgs = Nothing)

            If Not _isRunning Then Return

            ' If there is not an accept socket, create it
            ' If there is, reuse it
            If (e Is Nothing) Then
                e = New SocketAsyncEventArgs()
                AddHandler e.Completed, AddressOf OnClientAcceptCompleted
            Else
                e.AcceptSocket = Nothing
            End If

            ' If there are no connections waiting to be processed then we can go ahead and process the accept.
            ' Otherwise, the Completed event we tacked onto the accept socket will do it when it completes
            If Not (_serverSocket.AcceptAsync(e)) Then
                ProcessClientAccept(e)
            End If

        End Sub


#End Region

#Region " Events Handlers"

        ''' <summary>
        ''' Handles acceptance of new client socket
        ''' </summary>
        Private Sub OnClientAcceptCompleted(sender As Object, e As SocketAsyncEventArgs)

            If (e Is Nothing OrElse (e.SocketError <> SocketError.Success)) Then Return
            ProcessClientAccept(e)

        End Sub

        ''' <summary>
        ''' Handles client disconnection
        ''' </summary>
        ''' <param name="sender">The disconnected client</param>
        Public Sub OnClientDisconnected(ByRef sender As ApiClient)

            RemoveHandler sender.Disconnected, AddressOf OnClientDisconnected
            _clientsList.Remove(sender)

            Logger.Log(6, String.Format("Total clients now {0}", _telemetry.ConnectedMiners), _context)

        End Sub


#End Region


    End Class

End Namespace