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

Namespace Core

    Public Class SlidingQueue(Of T)

        Private _maxCapacity As Integer
        Private _queue As Concurrent.ConcurrentQueue(Of T)

        Public Sub New(capacity As Integer)
            _maxCapacity = capacity
            _queue = New Concurrent.ConcurrentQueue(Of T)
        End Sub

        Public Sub Enqueue(item As T)
            _queue.Enqueue(item)
            While _queue.Count > _maxCapacity
                Dim dummy As T
                _queue.TryDequeue(dummy)
            End While
        End Sub

        Public Sub Clear()
            _queue.Clear()
        End Sub

        Public Function Contains(ByVal item As T) As Boolean
            Return _queue.Contains(item)
        End Function

        Public Function AsEnumerable() As IEnumerable(Of T)
            Return _queue.AsEnumerable
        End Function

    End Class

End Namespace

