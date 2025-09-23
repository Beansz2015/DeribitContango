Imports System.Collections.Concurrent

Public Class DeribitRateLimiter

    Private ReadOnly maxRequests As Integer
    Private ReadOnly windowSeconds As Integer
    Private ReadOnly requestTimes As ConcurrentQueue(Of DateTime)
    Private ReadOnly lockObject As New Object()

    Public Sub New(Optional maxRequestsPerWindow As Integer = 50, Optional windowSizeSeconds As Integer = 10)
        maxRequests = maxRequestsPerWindow
        windowSeconds = windowSizeSeconds
        requestTimes = New ConcurrentQueue(Of DateTime)()
    End Sub

    Public Function CanMakeRequest() As Boolean
        SyncLock lockObject
            CleanupOldRequests()
            Return requestTimes.Count < maxRequests
        End SyncLock
    End Function

    Public Sub RecordRequest()
        SyncLock lockObject
            requestTimes.Enqueue(DateTime.Now)
            CleanupOldRequests()
        End SyncLock
    End Sub

    Private Sub CleanupOldRequests()
        Dim cutoffTime As DateTime = DateTime.Now.AddSeconds(-windowSeconds)

        While requestTimes.Count > 0
            Dim oldestTime As DateTime
            If requestTimes.TryPeek(oldestTime) AndAlso oldestTime < cutoffTime Then
                requestTimes.TryDequeue(oldestTime)
            Else
                Exit While
            End If
        End While
    End Sub

    Public ReadOnly Property RequestsInWindow As Integer
        Get
            SyncLock lockObject
                CleanupOldRequests()
                Return requestTimes.Count
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property AvailableRequests As Integer
        Get
            Return maxRequests - RequestsInWindow
        End Get
    End Property

    Public Function GetWaitTime() As TimeSpan
        SyncLock lockObject
            If CanMakeRequest() Then
                Return TimeSpan.Zero
            End If

            Dim oldestRequest As DateTime
            If requestTimes.TryPeek(oldestRequest) Then
                Dim waitUntil As DateTime = oldestRequest.AddSeconds(windowSeconds)
                Return If(waitUntil > DateTime.Now, waitUntil.Subtract(DateTime.Now), TimeSpan.Zero)
            End If

            Return TimeSpan.Zero
        End SyncLock
    End Function

End Class
