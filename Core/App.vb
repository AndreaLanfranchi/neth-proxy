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

    Public NotInheritable Class App

        Public Property StartTime As DateTime = DateTime.Now

#Region " Singletons"

        Private Shared ReadOnly _instance As App = New App

        Private _settings As Settings
        Private _telemetry As Telemetry
        Private _poolmgr As Pools.PoolManager
        Private _clntmgr As Clients.ClientsManager
        Private _apimgr As Api.ApiServer

        Private _eventsStack As AsyncEventArgsConcurrentStack

        Shared Sub New()
        End Sub

        Private Sub New()
        End Sub

        Public Sub Init()

            _settings = New Settings
            _telemetry = New Telemetry
            _poolmgr = New Pools.PoolManager
            _clntmgr = New Clients.ClientsManager

        End Sub

        Public Shared ReadOnly Property Instance As App
            Get
                Return _instance
            End Get
        End Property

        Public ReadOnly Property Settings As Settings
            Get
                Return _settings
            End Get
        End Property

        Public ReadOnly Property Telemetry As Telemetry
            Get
                Return _telemetry
            End Get
        End Property

        Public ReadOnly Property PoolMgr As Pools.PoolManager
            Get
                Return _poolmgr
            End Get
        End Property

        Public ReadOnly Property ClntMgr As Clients.ClientsManager
            Get
                Return _clntmgr
            End Get
        End Property

        Public ReadOnly Property ApiMgr As Api.ApiServer
            Get
                Return _apimgr
            End Get
        End Property

#End Region

#Region " Methods"

        Public Sub Start()

            _telemetry.StartWorking()
            _poolmgr.Start()
            _clntmgr.Start()

            ' Start Api server if needed
            If _settings.ApiListenerEndPoint IsNot Nothing Then
                _apimgr = New Api.ApiServer
                _apimgr.Start()
            End If

        End Sub

        Public Sub [Stop]()

            If _apimgr IsNot Nothing Then _apimgr.Stop()

            _telemetry.StopWorking()
            _clntmgr.Stop()
            _poolmgr.Stop()

        End Sub

#End Region

    End Class



End Namespace
