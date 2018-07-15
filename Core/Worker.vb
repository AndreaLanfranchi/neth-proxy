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

    Public Enum WorkerState
        Stopped
        Starting
        Started
        Stopping
    End Enum

    Public MustInherit Class Worker

        Private _state As WorkerState = WorkerState.Stopped

        Protected Shared WorkerLock As New Object
        Protected WorkerThread As Threading.Thread

        Public ReadOnly Property State As WorkerState
            Get
                Return _state
            End Get
        End Property

        Public Overridable Sub StartWorking()

            SyncLock (WorkerLock)
                _state = WorkerState.Started
            End SyncLock

        End Sub

        Public Overridable Sub StopWorking()

            SyncLock (WorkerLock)
                _state = WorkerState.Stopped
            End SyncLock

        End Sub

    End Class

End Namespace

