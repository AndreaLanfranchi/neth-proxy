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

Imports System.Runtime.CompilerServices

Namespace Core
    Module Extensions

#Region " Enums"

        ''' <summary>
        ''' Flags setter for Enum
        ''' </summary>
        <Extension()>
        Public Function SetFlags(ByRef value As [Enum], ParamArray flags() As [Enum]) As [Enum]

            If flags.Length Then
                For Each flag In flags
                    value = value Or CObj(flag)
                Next
            End If
            Return value

        End Function

        ''' <summary>
        ''' Flags unsetter for Enum
        ''' </summary>
        <Extension()>
        Public Function UnsetFlags(ByRef value As [Enum], ParamArray flags() As [Enum]) As [Enum]

            If flags.Length Then
                For Each flag In flags
                    value = value And Not CObj(flag)
                Next
            End If
            Return value

        End Function

        ''' <summary>
        ''' Flags checker for Enum
        ''' </summary>
        <Extension()>
        Public Function HasFlags(value As [Enum], ParamArray flags() As [Enum]) As Boolean

            If flags.Length Then
                For Each flag In flags
                    If Not value.HasBitFlag(flag) Then Return False
                Next
                Return True
            Else
                Return False
            End If

        End Function

        ''' <summary>
        ''' Flags checker for Enum
        ''' </summary>
        <Extension()>
        Public Function HasAnyFlag(value As [Enum], ParamArray flags() As [Enum]) As Boolean

            If flags.Length Then
                For Each flag In flags
                    If value.HasBitFlag(flag) Then Return True
                Next
                Return False
            Else
                Return False
            End If

        End Function


        <Extension()>
        Public Function HasBitFlag(value As [Enum], flag As [Enum]) As Boolean
            Return ((value And CObj(flag)) = flag)
        End Function


        ''' <summary>
        ''' Gets whether or not a give jsonvalue is empty
        ''' </summary>
        ''' <param name="value"></param>
        ''' <returns></returns>
        <Extension()>
        Public Function Empty(value As Json.JsonValue) As Boolean

            If value Is Nothing Then Return True
            Select Case value.JsonType
                Case Json.JsonType.String
                    Return String.IsNullOrEmpty(value)
                Case Json.JsonType.Array
                    Return value.Count = 0
                Case Json.JsonType.Object
                    Return value.ToString = """{}"""
                Case Else
                    Return False
            End Select

        End Function

        ''' <summary>
        ''' Restarts a timer
        ''' </summary>
        ''' <param name="value"></param>
        <Extension()>
        Public Sub Reset(ByRef value As Timers.Timer)

            value.Stop()
            value.Start()

        End Sub

#End Region

    End Module

End Namespace
