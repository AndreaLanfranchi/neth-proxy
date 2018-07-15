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

Namespace Clients

    Public Class ClientInfo

        Public Property TimeStamp As DateTime
        Public Property Version As String
        Public Property RunTime As Long
        Public Property HashRate As UInt64 = UInt64.MinValue
        Public Property HashRates As New List(Of UInt64)
        Public Property Fans As New List(Of Double)
        Public Property Temps As New List(Of Double)
        Public Property Powers As New List(Of Double)
        Public Property Solutions As New ClientSolutionsInfo

    End Class

    Public Class ClientSolutionsInfo

        Public Property Count As Long
        Public Property Invalid As Long
        Public Property Rejected As Long

    End Class

    Public Class ClientScrambleInfo

        Public Property TimeStamp As DateTime
        Public Property NonceScrambler As UInt64 = UInt64.MinValue
        Public Property Segments As New List(Of ClientScrambleInfoSegment)
        Public Property GpuWidth As UInteger

        Public ReadOnly Property ScrambleStart As UInt64
            Get
                If Segments.Count = 0 Then Return UInt64.MinValue
                Return Segments.First.SegmentStart
            End Get
        End Property

        Public ReadOnly Property ScrambleStop As UInt64
            Get
                If Segments.Count = 0 Then Return UInt64.MinValue
                Return Segments.Last.SegmentStop
            End Get
        End Property

    End Class

    Public Class ClientScrambleInfoSegment

        Public Property GpuIndex As UInteger
        Public Property SegmentStart As UInt64 = UInt64.MinValue
        Public Property SegmentStop As UInt64 = UInt64.MinValue

    End Class

End Namespace
