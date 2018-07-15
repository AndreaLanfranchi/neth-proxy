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

Namespace RangeTree

    ''' <summary>
    ''' Represents the working range of a single miner (or worker)
    ''' </summary>
    Public Class WorkerRangeItem
        Implements IRangeProvider(Of UInt64)

        Public Property TimeStamp As DateTime = DateTime.Now
        Public Property Id As String
        Public Property Name As String
        Public Property Range As Range(Of UInt64) Implements IRangeProvider(Of UInt64).Range

    End Class

    ''' <summary>
    ''' Represents the comparer among Workers Ranges
    ''' </summary>
    Public Class WorkerRangeItemComparer
        Implements IComparer(Of WorkerRangeItem)

        Public Function Compare(x As WorkerRangeItem, y As WorkerRangeItem) As Integer Implements IComparer(Of WorkerRangeItem).Compare
            Return x.Range.CompareTo(y.Range)
        End Function

    End Class

End Namespace
