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

    ''' <summary>
    ''' Holds the whole set of settings
    ''' </summary>
    Public Class Settings

#Region " Fields"

        ' Pools
        Public Property PoolsNoProbe As Boolean
        Public Property PoolsWorkTimeout As Integer                         ' Default 120 seconds. If no work within this time then close connection and try another
        Public Property PoolsResponseTimeout As Integer                     ' Default 2000 ms response time to submissions of shares
        Public Property PoolsReportHashRate As Boolean                      ' Whether or not to report hash rate to pools
        Public Property PoolsReportWorkerNames As Boolean                   ' Whether or not to report single workers names to pools
        Public Property PoolsStratumLogin As String                         ' Default stratum login
        Public Property PoolsStratumPassword As String                      ' Default stratum password
        Public Property PoolsStratumWorker As String                        ' Default stratum worker name
        Public Property PoolsMaxConnectionErrors As Integer                 ' Max number of connection errors allowed before switching pool

        ' Segments spacing
        Public Property WorkersSpacing As Integer = 24                      ' This is the exponent of 2 which spaces workers segments

        ' Server listener
        Public Property ListenerEndPoint As Net.IPEndPoint                  ' The endpoint this proxy server is listening on
        Public Property ApiListenerEndPoint As Net.IPEndPoint               ' The endpoint this api server is listening on

        ' Statistics
        Public Property StatsEnabled As Boolean = True                      ' Display statistics
        Public Property StatsInterval As Integer                            ' Default 60 seconds for output stats

        ' Misc
        Public Property NoConsole As Boolean                                ' Whether or not to respond to interactive console
        Public Property LogVerbosity As Integer                             ' Log Verbosity level
        Public Property NoFee As Boolean = False                            ' Whether the user decides to pay no fees. If true then no checking for overlapping ranges nor segment compact


#End Region

#Region " Contructor"

        Public Sub New()

            StatsInterval = 60

            ListenerEndPoint = New Net.IPEndPoint(Net.IPAddress.Any, 4444)  ' Default

            ' Defaults for pools
            PoolsNoProbe = False
            PoolsWorkTimeout = 120                  ' Seconds
            PoolsResponseTimeout = 2000             ' Milliseconds
            PoolsReportHashRate = False
            PoolsReportWorkerNames = False
            PoolsMaxConnectionErrors = 5

            PoolsStratumLogin = String.Empty
            PoolsStratumPassword = "x"
            PoolsStratumWorker = String.Empty

            ' Misc
            NoConsole = False
            LogVerbosity = 4

        End Sub

#End Region

    End Class

End Namespace