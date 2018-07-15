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
    ''' Range tree interface.
    ''' </summary>
    ''' <typeparam name="TKey">The type of the range</typeparam>
    ''' <typeparam name="T">The type of the data items</typeparam>
    Public Interface IRangeTree(Of TKey As IComparable(Of TKey), T As IRangeProvider(Of TKey))

        ReadOnly Property Items As IEnumerable(Of T)
        ReadOnly Property Count As Integer

        Function Query(value As TKey) As List(Of T)
        Function Query(value As Range(Of TKey)) As List(Of T)

        Sub Rebuild()
        Sub Add(item As T)
        Sub Add(items As IEnumerable(Of T))
        Sub Remove(item As T)
        Sub Remove(items As IEnumerable(Of T))
        Sub Clear()

    End Interface

End Namespace

