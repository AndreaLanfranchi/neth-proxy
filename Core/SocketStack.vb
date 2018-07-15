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

Imports System.Collections.Concurrent
Imports System.Net.Sockets

Namespace Core

    ''' <summary>
    ''' Standard Stack implementation for reusable sockets
    ''' </summary>
    Public Class AsyncEventArgsConcurrentStack

        Private _stack As New ConcurrentStack(Of SocketAsyncEventArgs)

#Region " Properties"

        Public ReadOnly Property Count As Integer
            Get
                Return _stack.Count
            End Get
        End Property

        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return _stack.IsEmpty
            End Get
        End Property

#End Region

#Region " Methods"

        ''' <summary>
        ''' Returns the enumerator of the stack
        ''' </summary>
        ''' <returns></returns>
        Public Function GetEnumerator() As IEnumerator(Of SocketAsyncEventArgs)

            Return _stack.GetEnumerator()

        End Function


        ''' <summary>
        ''' Pops an item off the top of the stack
        ''' </summary>
        ''' <returns></returns>
        Public Function Pop() As SocketAsyncEventArgs

            Dim item As SocketAsyncEventArgs = Nothing
            If Not _stack.TryPop(item) Then
                item = New SocketAsyncEventArgs
            End If
            Return item

        End Function

        ''' <summary>
        ''' Pushes an item on the top of the stack
        ''' </summary>
        ''' <param name="item"></param>
        Public Sub Push(item As SocketAsyncEventArgs)

            If item Is Nothing Then
                Throw New ArgumentNullException("Cannot add null item to the stack")
            End If
            _stack.Push(item)

        End Sub

#End Region


    End Class

End Namespace