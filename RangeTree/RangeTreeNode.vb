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
    ''' A node of the range tree. Given a list of items, it builds its subtree. 
    ''' Also contains methods to query the subtree. Basically, all interval tree logic is here.
    ''' </summary>
    ''' <typeparam name="TKey">The type of <see cref="ICollection(Of T)"/></typeparam>
    ''' <typeparam name="T">The type of <see cref="IRangeProvider(Of T)"/></typeparam>
    Public Class RangeTreeNode(Of TKey As IComparable(Of TKey), T As IRangeProvider(Of TKey))

#Region " Private members"

        Private _center As TKey
        Private _lNode As RangeTreeNode(Of TKey, T)
        Private _rNode As RangeTreeNode(Of TKey, T)
        Private _items As List(Of T)

        Private _rangeComparer As IComparer(Of T)

#End Region

#Region " Properties"

        Public ReadOnly Property RangeComparer As IComparer(Of T)
            Get
                Return _rangeComparer
            End Get
        End Property

        ''' <summary>
        ''' Gets the median value of this tree
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Center As TKey
            Get
                Return _center
            End Get
        End Property

#End Region

#Region " Constructor"

        ''' <summary>
        ''' Initializes a new instance of the <see cref="RangeTreeNode(Of TKey, T)"/> class.
        ''' </summary>
        ''' <param name="rangeComparer">The Range comparer</param>
        Public Sub New(Optional rangeComparer As IComparer(Of T) = Nothing)

            If rangeComparer IsNot Nothing Then
                _rangeComparer = rangeComparer
            End If

            _center = Nothing
            _lNode = Nothing
            _rNode = Nothing
            _items = Nothing

        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="RangeTreeNode(Of TKey, T)"/> class.
        ''' </summary>
        ''' <param name="item">An existing list of items</param>
        ''' <param name="rangeComparer">The Range Comparer</param>
        Public Sub New(items As IEnumerable(Of T), Optional rangeComparer As IComparer(Of T) = Nothing)

            If rangeComparer IsNot Nothing Then
                _rangeComparer = rangeComparer
            End If

            Dim endPoints As New List(Of TKey)

            For Each i As T In items
                endPoints.Add(i.Range.From)
                endPoints.Add(i.Range.To)
            Next
            endPoints.Sort()

            ' Use the median as center value
            If (endPoints.Count > 0) Then
                _center = endPoints(endPoints.Count / 2)
            End If

            _items = New List(Of T)
            Dim l As New List(Of T)
            Dim r As New List(Of T)

            ' Iterate over all items
            ' if the range of an item is completely left of the center, add it to the left items
            ' if it is on the right of the center, add it to the right items
            ' otherwise (range overlaps the center), add the item to this node's items
            For Each i As T In items

                If i.Range.To.CompareTo(_center) < 0 Then

                    ' Range ends to the left
                    l.Add(i)

                ElseIf i.Range.From.CompareTo(_center) > 0 Then

                    ' Range starts to the right
                    r.Add(i)
                Else

                    ' Range intersects this median
                    _items.Add(i)

                End If

            Next

            ' Sort items to speed up later queries
            If _items.Count > 0 Then
                _items.Sort(_rangeComparer)
            Else
                _items = Nothing
            End If

            ' Create left and right nodes if any
            If l.Count > 0 Then _lNode = New RangeTreeNode(Of TKey, T)(l, _rangeComparer)
            If r.Count > 0 Then _rNode = New RangeTreeNode(Of TKey, T)(r, _rangeComparer)

        End Sub

        ''' <summary>
        ''' Performans a "stab" query with a single value. All items with overlapping ranges are returned.
        ''' </summary>
        ''' <param name="value">The value to search</param>
        ''' <returns>The resulting matches as <see cref="List(Of T)"/></returns>
        Public Function Query(value As TKey) As List(Of T)

            Dim results As New List(Of T)

            ' If the node has items check ranges
            If _items IsNot Nothing Then
                For Each i As T In _items
                    If i.Range.Contains(value) Then
                        results.Add(i)
                    End If
                Next
            End If

            ' Go to the left or go to the right of the tree, depending
            ' where the query value lies compared to the center
            If (value.CompareTo(_center) < 0 AndAlso _lNode IsNot Nothing) Then
                results.AddRange(_lNode.Query(value))
            End If
            If (value.CompareTo(_center) > 0 AndAlso _rNode IsNot Nothing) Then
                results.AddRange(_rNode.Query(value))
            End If

            Return results

        End Function

        ''' <summary>
        ''' Performs a range query. All items with overlapping ranges are returned.
        ''' </summary>
        ''' <param name="value">The Range to search</param>
        ''' <returns>The resulting matches as <see cref="List(Of T)"/></returns>
        Public Function Query(value As Range(Of TKey)) As List(Of T)

            Dim results As New List(Of T)

            ' If the node has items, check their ranges.
            If _items IsNot Nothing Then
                For Each i As T In _items
                    If (i.Range.Intersects(value)) Then
                        results.Add(i)
                    End If
                Next
            End If

            ' Go to the left or go to the right of the tree, depending
            ' where the query value lies compared to the center
            If (value.To.CompareTo(_center) < 0 AndAlso _lNode IsNot Nothing) Then
                results.AddRange(_lNode.Query(value))
            End If
            If (value.From.CompareTo(_center) > 0 AndAlso _rNode IsNot Nothing) Then
                results.AddRange(_rNode.Query(value))
            End If

            Return results

        End Function

#End Region

    End Class

End Namespace
