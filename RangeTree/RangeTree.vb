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

    Public Class RangeTree(Of TKey As IComparable(Of TKey), T As IRangeProvider(Of TKey))
        Implements IRangeTree(Of TKey, T)

#Region " Private Members"

        Private root As RangeTreeNode(Of TKey, T)
        Private _items As List(Of T)
        Private _isInSync As Boolean
        Private _autoRebuild As Boolean
        Private _rangeComparer As IComparer(Of T)

#End Region

#Region " Constructor"

        ''' <summary>
        ''' Initializes a new instance of <see cref="RangeTree(Of TKey, T)"/>
        ''' </summary>
        ''' <param name="rangeComparer">The Range Comparer</param>
        Public Sub New(rangeComparer As IComparer(Of T))

            _rangeComparer = rangeComparer
            root = New RangeTreeNode(Of TKey, T)(rangeComparer)
            _items = New List(Of T)
            _isInSync = True
            _autoRebuild = True

        End Sub

        ''' <summary>
        ''' Initializes a new instance of <see cref="RangeTree(Of TKey, T)"/>
        ''' </summary>
        ''' <param name="items">The initial list of items</param>
        ''' <param name="rangeComparer">The Range Comparer</param>
        Public Sub New(items As IEnumerable(Of T), rangeComparer As IComparer(Of T))

            _rangeComparer = rangeComparer
            root = New RangeTreeNode(Of TKey, T)(items, rangeComparer)
            _items = items.ToList()
            _isInSync = True
            _autoRebuild = True

        End Sub


#End Region

#Region " Properties"

        ''' <summary>
        ''' Gets a value indicating whether the tree is currently in sync or not. 
        ''' If it is "out of sync" you can either rebuild it manually (call Rebuild) 
        ''' or let it rebuild automatically when you query it next.
        ''' </summary>
        ''' <returns>True / False</returns>
        Public ReadOnly Property IsInSync As Boolean
            Get
                Return _isInSync
            End Get
        End Property

        ''' <summary>
        ''' Gets all of the tree items.
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Items As IEnumerable(Of T) Implements IRangeTree(Of TKey, T).Items
            Get
                Return _items
            End Get
        End Property

        ''' <summary>
        ''' Gets the total number of items
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Count As Integer Implements IRangeTree(Of TKey, T).Count
            Get
                Return _items.Count
            End Get
        End Property

        ''' <summary>
        ''' Gets the median of this Tree
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Center As TKey
            Get
                Return root.Center
            End Get
        End Property


#End Region

#Region " Methods"

        ''' <summary>
        ''' Rebuilds the tree if it is out of sync.
        ''' </summary>
        Public Sub Rebuild() Implements IRangeTree(Of TKey, T).Rebuild

            If _isInSync Then Return
            root = New RangeTreeNode(Of TKey, T)(_items, _rangeComparer)
            _isInSync = True

        End Sub

        ''' <summary>
        ''' Adds the specified item. Tree will go out of sync.
        ''' </summary>
        ''' <param name="item">The item to add</param>
        Public Sub Add(item As T) Implements IRangeTree(Of TKey, T).Add

            SyncLock root
                _isInSync = False
                _items.Add(item)
            End SyncLock

        End Sub

        ''' <summary>
        ''' Adds the specified list of items. Tree will go out of sync.
        ''' </summary>
        ''' <param name="items"></param>
        Public Sub Add(items As IEnumerable(Of T)) Implements IRangeTree(Of TKey, T).Add

            SyncLock root
                _isInSync = False
                _items.AddRange(items)
            End SyncLock


        End Sub

        ''' <summary>
        ''' Removes the specified item. Tree will go out of sync
        ''' </summary>
        ''' <param name="item"></param>
        Public Sub Remove(item As T) Implements IRangeTree(Of TKey, T).Remove

            SyncLock root
                _isInSync = False
                _items.Remove(item)
            End SyncLock

        End Sub

        ''' <summary>
        ''' Removes the specified list of items. Tree will go out of sync
        ''' </summary>
        ''' <param name="items"></param>
        Public Sub Remove(items As IEnumerable(Of T)) Implements IRangeTree(Of TKey, T).Remove

            SyncLock root
                _isInSync = False
                For Each i As T In items
                    _items.Remove(i)
                Next
            End SyncLock

        End Sub

        ''' <summary>
        ''' Clears the tree
        ''' </summary>
        Public Sub Clear() Implements IRangeTree(Of TKey, T).Clear

            root = New RangeTreeNode(Of TKey, T)(_rangeComparer)
            SyncLock root
                _items.Clear()
                _isInSync = True
            End SyncLock

        End Sub

        ''' <summary>
        ''' Performs a "stab" query with a single value. All items with overlapping ranges are returned.
        ''' </summary>
        ''' <param name="value">The value to search for</param>
        ''' <returns>All matching results as <see cref="List(Of T)"/></returns>
        Public Function Query(value As TKey) As List(Of T) Implements IRangeTree(Of TKey, T).Query

            If (_isInSync = False AndAlso _autoRebuild = True) Then
                Rebuild()
            End If

            Return root.Query(value)

        End Function

        ''' <summary>
        ''' Performs a "stab" query with a single range value. All items with overlapping ranges are returned.
        ''' </summary>
        ''' <param name="value">The value to search for</param>
        ''' <returns>All matching results as <see cref="List(Of T)"/></returns>
        Public Function Query(value As Range(Of TKey)) As List(Of T) Implements IRangeTree(Of TKey, T).Query

            If (_isInSync = False AndAlso _autoRebuild = True) Then
                Rebuild()
            End If

            Return root.Query(value)

        End Function

#End Region

    End Class

End Namespace
