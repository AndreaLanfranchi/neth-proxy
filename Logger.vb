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

Public Class Logger

    Private Shared msgFormat1 As String = " {0:MMM dd HH:mm:ss.fff} | {1,-6} | {2}"
    Private Shared msgFormat2 As String = " {0,147}"
    Private Shared statHeader0 As String = "----------- + --------------------------- + -------------- + ------ + --------- + -------------- + ------ + ------ +"
    Private Shared statHeader1 As String = "      Total | Active                      |     Total Jobs | Miners |   Hashing |      Submitted |   Kn % |   Kn % |"
    Private Shared statHeader3 As String = "   run time | Pool                        |       Received |  Count | Power / s |      Solutions |  Stale | Reject |"
    Private Shared statHeader4 As String = "----------- + --------------------------- + -------------- + ------ + --------- + -------------- + ------ + ------ +"
    Private Shared statLineFmt As String = "{0,11} | {1,-27} | {2,14:N0} | {3,6:N0} | {4,9:N2} | {5,14:N0} | {6,6:N2} | {7,6:N2} |"

    Public Shared Function GetStatFmt() As String
        Return statLineFmt
    End Function

    Public Shared Sub Log(severity As Integer, message As String, Optional category As String = "Info")

        Static statsRows As Integer = 0

        ' Severity goes from 
        ' 0 - Error
        ' 1 - Info
        ' 2 - Stats
        ' 3 - Connections / Disconnections
        ' 4 - Logins / Subscriptions
        ' 5 - Workers hashrates
        ' 6 - Jobs notification & Workers Submissions
        ' 7 - Low level socket Connections / Disconnections
        ' ...
        ' 9 - Debug Json

        If severity > App.Instance.Settings.LogVerbosity Then Return

        ' For stat output
        If (severity = 2) Then
            If statsRows >= 30 Then
                Console.Out.WriteLine(msgFormat2, statHeader0)
                Console.Out.WriteLine(msgFormat2, statHeader1)
                Console.Out.WriteLine(msgFormat2, statHeader3)
                Console.Out.WriteLine(msgFormat2, statHeader4)
                statsRows = 0
            End If
            Console.Out.WriteLine(msgFormat1, DateTime.Now, "Stats", message)
            statsRows += 1
        Else
            statsRows = 30
            Console.Out.WriteLine(msgFormat1, DateTime.Now, category, message)
        End If

    End Sub

End Class
