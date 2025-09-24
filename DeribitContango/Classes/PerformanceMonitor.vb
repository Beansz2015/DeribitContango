Public Class PerformanceMonitor
    Public Property StartTime As DateTime
    Public Property MessageCount As Integer
    Public Property LastLatency As TimeSpan
    Public Property AverageLatency As TimeSpan
    Public Property ErrorCount As Integer
    Public Property ReconnectionCount As Integer

    Private latencies As New List(Of TimeSpan)

    Public Sub RecordLatency(latency As TimeSpan)
        latencies.Add(latency)
        LastLatency = latency

        If latencies.Count > 100 Then
            latencies.RemoveAt(0) ' Keep last 100 measurements
        End If

        ' Calculate average
        If latencies.Count > 0 Then
            Dim totalMs = latencies.Sum(Function(l) l.TotalMilliseconds)
            AverageLatency = TimeSpan.FromMilliseconds(totalMs / latencies.Count)
        End If
    End Sub

    Public ReadOnly Property MessagesPerSecond As Double
        Get
            If StartTime = DateTime.MinValue Then Return 0
            Dim elapsed = DateTime.Now.Subtract(StartTime).TotalSeconds
            If elapsed = 0 Then Return 0
            Return MessageCount / elapsed
        End Get
    End Property

    Public ReadOnly Property UptimePercent As Double
        Get
            If StartTime = DateTime.MinValue Then Return 0
            Dim totalTime = DateTime.Now.Subtract(StartTime).TotalMinutes
            Dim downTime = ReconnectionCount * 0.5 ' Assume 30s per reconnection
            Return Math.Max(0, (totalTime - downTime) / totalTime * 100)
        End Get
    End Property
End Class