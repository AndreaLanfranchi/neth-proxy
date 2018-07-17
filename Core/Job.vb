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
    ''' Abstraction of a job received from pool
    ''' </summary>
    Public Class Job

        Public Property TimeStamp As DateTime = DateTime.Now
        Public Property Header As String
        Public Property Seed As String
        Public Property Target As String

        Public ReadOnly Property Id
            Get
                Return Header.Substring(2, 8)
            End Get
        End Property

        Public Sub New()
        End Sub

        Public Sub New(arr As Json.JsonArray, mode As StratumModeEnum)

            If mode = StratumModeEnum.Ethproxy Then

                Header = ToHex64(arr(0))
                Seed = ToHex64(arr(1))
                Target = ToHex64(arr(2))

            ElseIf mode = StratumModeEnum.Stratum Then

                Header = ToHex64(arr(1))
                Seed = ToHex64(arr(2))
                Target = ToHex64(arr(3))

            End If

        End Sub

    End Class

End Namespace
