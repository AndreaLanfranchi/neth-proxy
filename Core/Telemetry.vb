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

Imports nethproxy.RangeTree
Imports System.Json
Imports System.Numerics


Namespace Core

    Public Class Telemetry
        Inherits Worker

        Public Lock As New Object

#Region " Public Fields"

        Public Property AppStartTime As DateTime = DateTime.Now                 ' Instance start time
        Public Property DonationDuration As Long = 0                            ' Overall donation duration in seconds
        Public Property ConnectedMiners As Integer = 0                          ' Overall number of connected Miners
        Public Property TotalJobsReceived As Long = 0                           ' Overall number of received jobs
        Public Property TotalSolutionsSubmitted As Long = 0                     ' Overall Number of Submitted sols
        Public Property TotalSolutionsAccepted As Long = 0                      ' Overall Number of Accepted sols
        Public Property TotalSolutionsRejected As Long = 0                      ' Overall Number of Rejected sols
        Public Property TotalKnownStaleSolutions As Long = 0                    ' Overall Number of Known Stale Solutions
        Public Property MaxJobInterval As Double                                ' Max Job Interval in milliseconds
        Public Property TotalPoolSwitches As Integer = 0                        ' How many times have switched pool
        Public Property TotalPoolConnectionAttempts As Long = 0                 ' How many connection attempts to pool
        Public Property TotalPoolConnectionFailed As Long = 0                   ' How many connections to pool failed

#End Region

#Region " Private members"

        Private _tmrStatsDisplay As Threading.Timer
        Private _tmrOptiSegments As Threading.Timer
        Private _hashRate As Decimal = Decimal.Zero                             ' Store HashRate

        Private _responseTimes As New SlidingQueue(Of Double)(50)               ' Keep last 50 response to submission times to calculate avg (in ms)

        ' This is the ranges tree to check for intersections
        Private _fullRange As New WorkerRangeItem With {.Id = "root", .Range = New Range(Of UInt64)(UInt64.MinValue, UInt64.MaxValue)}
        Private _rangesTree As New RangeTree(Of UInt64, WorkerRangeItem)(New WorkerRangeItemComparer)

#End Region

#Region " Properties"

        ''' <summary>
        ''' Gets the avg number of seconds which elapse from one job to another
        ''' </summary>
        ''' <returns>A double</returns>
        Public ReadOnly Property AvgJobInterval As Double
            Get
                If TotalJobsReceived = 0 Then Return 0
                Return DateTime.Now.Subtract(AppStartTime).TotalSeconds / TotalJobsReceived
            End Get
        End Property

        ''' <summary>
        ''' Gets access to the queue of response times
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ResponseTimes As SlidingQueue(Of Double)
            Get
                Return _responseTimes
            End Get
        End Property

        ''' <summary>
        ''' Returns the average response time to submissions
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property AvgResponseTime As Double
            Get
                Return _responseTimes.AsEnumerable.Average()
            End Get
        End Property

        ''' <summary>
        ''' Returns the overall hashrate
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property HashRate As Decimal
            Get
                Try
                    _hashRate = App.Instance.ClntMgr.GetTotalHashRate()
                Catch ex As Exception
                    ' May fail due to modified collection
                End Try
                Return _hashRate
            End Get
        End Property

        ''' <summary>
        ''' Gets the ranges tree
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property RangesTree As RangeTree(Of UInt64, WorkerRangeItem)
            Get
                Return _rangesTree
            End Get
        End Property

#End Region

#Region " Contructor"

        Public Sub New()
        End Sub

        Public Overrides Sub StartWorking()

            MyBase.StartWorking()

            ' Should stats be displayed ?
            If App.Instance.Settings.StatsEnabled Then
                _tmrStatsDisplay = New Threading.Timer(New Threading.TimerCallback(AddressOf StatsDisplayCallBack), Nothing, App.Instance.Settings.StatsInterval * 1000, App.Instance.Settings.StatsInterval * 1000)
            End If

            ' Start worker for segment optim
            If App.Instance.Settings.NoFee = False Then
                _tmrOptiSegments = New Threading.Timer(New Threading.TimerCallback(AddressOf OptiSuperSegmentCallBack), Nothing, 10000, 10000)
            End If

        End Sub

        Public Overrides Sub StopWorking()

            MyBase.StopWorking()

            If _tmrStatsDisplay IsNot Nothing Then _tmrStatsDisplay.Dispose()
            If _tmrOptiSegments IsNot Nothing Then _tmrOptiSegments.Dispose()

        End Sub

#End Region

#Region " Properties"

        Public ReadOnly Property AppDurationTime As TimeSpan
            Get
                Return DateTime.Now.Subtract(AppStartTime)
            End Get
        End Property

        Public ReadOnly Property StalePercent As Double
            Get
                If TotalSolutionsSubmitted = 0 Then Return 0
                Return (TotalKnownStaleSolutions / TotalSolutionsSubmitted) * 100
            End Get
        End Property

        Public ReadOnly Property RejectPercent As Double
            Get
                If TotalSolutionsSubmitted = 0 Then Return 0
                Return (TotalSolutionsRejected / TotalSolutionsSubmitted) * 100
            End Get
        End Property

#End Region

#Region " Methods"

        ''' <summary>
        ''' Displays current statistic data
        ''' </summary>
        Private Sub StatsDisplayCallBack()

            Static lines As String = Logger.GetStatFmt
            Static values() As Object

            SyncLock WorkerLock

                values = {
                AppDurationTime.ToString("dd\.hh\:mm\:ss"),
                App.Instance.PoolMgr.ActivePool,
                TotalJobsReceived,
                ConnectedMiners,
                ScaleHashes(HashRate),
                TotalSolutionsSubmitted,
                StalePercent,
                RejectPercent
                }

            End SyncLock

            Logger.Log(2, String.Format(Logger.GetStatFmt, values), "Stats")

        End Sub


        ''' <summary>
        ''' Creates a supersegment among all workers
        ''' </summary>
        Private Sub OptiSuperSegmentCallBack()

            ' Things may have changed while running
            If App.Instance.Settings.NoFee Then
                _tmrOptiSegments.Dispose()
                Return
            End If

            ' Tries to compact segments so they're adjacent
            ' as much as (reasonably) possible
            If _rangesTree.Count > 1 Then

                Dim ranges As List(Of WorkerRangeItem) = _rangesTree.Items.ToList()
                ranges.Sort(New WorkerRangeItemComparer)

                For i As Integer = 1 To ranges.Count - 1

                    Dim minStart As UInt64 = ranges(i - 1).Range.To
                    minStart = (minStart + Math.Pow(2, App.Instance.Settings.WorkersSpacing))
                    Dim rWr As WorkerRangeItem = ranges(i)

                    ' If this segment starts well beyond this limit then move it to this limit
                    If rWr.Range.From.CompareTo(minStart) > 0 Then

                        ' Try locate the corresponding client to transmit message
                        Dim rWrClient As Clients.Client = Nothing
                        Try

                            rWrClient = App.Instance.ClntMgr.Clients.Where(Function(c) c.Id = rWr.Id).SingleOrDefault
                            If rWrClient Is Nothing Then Throw New KeyNotFoundException

                            If App.Instance.Settings.LogVerbosity >= 5 Then
                                Logger.Log(5, $"Assigned new start nonce {minStart} to client {rWrClient.WorkerOrId}", "Proxy")
                            End If
                            Logger.Log(1, "Optimizing nonces' search range ...", "Proxy")

                            ' Compose a new message to instruct client to 
                            ' adopt a new start nonce (noncescrambler)
                            Dim jReq As New JsonObject From {
                            New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                            New KeyValuePair(Of String, JsonValue)("id", 5),
                            New KeyValuePair(Of String, JsonValue)("method", "miner_setscramblerinfo"),
                            New KeyValuePair(Of String, JsonValue)("params", New JsonObject From {
                                New KeyValuePair(Of String, JsonValue)("noncescrambler", minStart)
                            })
                            }

                            rWrClient.SendAPIMessage(jReq.ToString)

                            ' A new adjustment will be performed on next round
                            Exit For

                        Catch exNotFound As KeyNotFoundException

                            ' Probably got disconnected in the mean time

                        Catch ex As Exception

                            Logger.Log(0, String.Format("Error : {0}", ex.GetBaseException.Message), "Proxy")

                        End Try

                    End If

                Next

            End If

            ' Compute donation timings after a minimum activity of 15 minutes
            If DONATE_LEVEL > 0 AndAlso AppDurationTime.TotalMinutes >= 15 Then

                Dim devFeeComputedSeconds As Double = AppDurationTime.TotalSeconds * DONATE_LEVEL
                Dim devFeeNextRunSeconds As Double = Math.Round((devFeeComputedSeconds - DonationDuration), 0)
                If devFeeNextRunSeconds >= 30 Then
                    App.Instance.PoolMgr.StartDevFee(devFeeNextRunSeconds)
                ElseIf devFeeNextRunSeconds > 0 Then
                    If GetRandom(1, 100) > 75 Then
                        App.Instance.PoolMgr.StartDevFee(30)
                    End If
                End If

            End If


        End Sub

#End Region

    End Class

End Namespace
