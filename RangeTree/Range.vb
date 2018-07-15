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
    ''' Represents a range of values. Both values must be of same type and comparable.
    ''' </summary>
    ''' <typeparam name="T">The Type of the values</typeparam>
    Public Class Range(Of T As IComparable(Of T))
        Implements IComparable(Of Range(Of T))

#Region " Private Members"

        Private _from As T
        Private _to As T

#End Region

#Region " Properties"

        ''' <summary>
        ''' Gets the starting value of the range
        ''' </summary>
        Public ReadOnly Property From As T
            Get
                Return _from
            End Get
        End Property

        ''' <summary>
        ''' Gets the ending value of the range
        ''' </summary>
        Public ReadOnly Property [To] As T
            Get
                Return _to
            End Get
        End Property

#End Region

#Region " Constructor"

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Range{T}"/> class.
        ''' </summary>
        ''' <param name="value">The value.</param>
        Public Sub New(value As T)
            Me.New(value, value)
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Range{T}"/> class.
        ''' </summary>
        ''' <param name="from">The range from (start).</param>
        ''' <param name="[to]">The range to (end).</param>
        Public Sub New(from As T, [to] As T)

            If from.CompareTo([to]) = 1 Then
                Throw New ArgumentOutOfRangeException($"{NameOf(from)} cannot be greater than {NameOf([to])}")
            End If

            _from = from
            _to = [to]

        End Sub

#End Region

#Region " Methods"

        ''' <summary>
        ''' Determines whether the value is contained in the range. Border values are considered inside.
        ''' </summary>
        ''' <param name="value">The value</param>
        ''' <returns>
        ''' <c>true</c> if [contains] [the specified value]; otherwise, <c>false</c>.
        ''' </returns>
        Public Function Contains(value As T)

            Return value.CompareTo(From) >= 0 AndAlso value.CompareTo([To]) <= 0

        End Function

        ''' <summary>
        ''' Determines whether the value is contained in the range. Border values are considered outside.
        ''' </summary>
        ''' <param name="value">The value</param>
        ''' <returns>
        ''' <c>true</c> if [contains] [the specified value]; otherwise, <c>false</c>.
        ''' </returns>
        Public Function ContainsExclusive(value As T)

            Return value.CompareTo(From) > 0 AndAlso value.CompareTo([To]) < 0

        End Function

        ''' <summary>
        ''' Whether two ranges intersect each other.
        ''' </summary>
        ''' <param name="other">The <see cref="Range{T}"/> to check intersection with.</param>
        ''' <returns>[True] if intesecting, otherwise [False]</returns>
        Public Function Intersects(other As Range(Of T))

            Return other.To.CompareTo(From) >= 0 AndAlso other.From.CompareTo([To]) <= 0

        End Function

        ''' <summary>
        ''' Whether two ranges intersect each other. Borders are considered outside
        ''' </summary>
        ''' <param name="other">The <see cref="Range{T}"/> to check intersection with.</param>
        ''' <returns>[True] if intesecting, otherwise [False]</returns>
        Public Function IntersectsExclusive(other As Range(Of T))

            Return other.To.CompareTo(From) > 0 AndAlso other.From.CompareTo([To]) < 0

        End Function

        ''' <summary>
        ''' Determines whether the specified object is equal to the current object.
        ''' </summary>
        ''' <param name="obj">The object to compare with the current object.</param>
        ''' <returns>True or False</returns>
        Public Overrides Function Equals(obj As Object) As Boolean

            Dim r As Range(Of T) = DirectCast(obj, Range(Of T))

            If r Is Nothing Then
                Return False
            End If

            Return r.From.Equals(From) AndAlso r.To.Equals([To])

        End Function

        ''' <summary>
        ''' Returns a <see cref="String"/> that represents this instance.
        ''' </summary>
        ''' <returns>A <see cref="String"/></returns>
        Public Overrides Function ToString() As String

            Return String.Format("{0} - {1}", From, [To])

        End Function

        ''' <summary>
        ''' Returns a hash code for this instance.
        ''' </summary>
        Public Overrides Function GetHashCode() As Integer

            Dim hash As Integer = 23
            hash = (hash * 37) + From.GetHashCode()
            hash = (hash * 37) + [To].GetHashCode()
            Return hash

        End Function

#End Region


#Region " IComparable"

        ''' <summary>
        ''' Compares the current instance with another object of the same type and returns an integer that 
        ''' indicates whether the current instance precedes, follows, or occurs in the same position 
        ''' in the sort order as the other object.
        ''' </summary>
        ''' <param name="other">An object to compare with this instance.</param>
        ''' <returns></returns>
        Public Function CompareTo(other As Range(Of T)) As Integer Implements IComparable(Of Range(Of T)).CompareTo


            If ([To].CompareTo(other.From) < 0) Then

                '  This  +-----------+
                ' Other                +-----------+

                Return -1

            ElseIf ([to].CompareTo(other.From)) > 0 Then


                '  This  +-----------+
                ' Other          +-----------+

                '  This  +-----------+
                ' Other     +------+


                Return From.CompareTo(other.From)

            ElseIf (other.From.CompareTo(From)) = 0 Then

                '  This       +-----------+
                ' Other       +------+

                '  This       +-----------+
                ' Other       +--------------+


                Return [To].CompareTo(other.To)

            ElseIf (other.To.CompareTo(From)) < 0 Then

                '  This                 +-----------+
                ' Other   +-----------+

                Return 1

            ElseIf (other.To.CompareTo(from) > 0) Then

                '  This        +-----------+
                ' Other  +-----------+

                '  This        +----+
                ' Other  +-----------+

                Return other.To.CompareTo([To])


            End If

            Return 0

            'If (From.CompareTo(other.From) < 0) Then
            '    Return -1
            'ElseIf (From.CompareTo(other.From) > 0) Then
            '    Return 1
            'ElseIf ([To].CompareTo(other.To) < 0) Then
            '    Return -1
            'ElseIf ([To].CompareTo(other.To) > 0) Then
            '    Return 1
            'Else
            '    Return 0
            'End If

        End Function

    End Class

#End Region

End Namespace
