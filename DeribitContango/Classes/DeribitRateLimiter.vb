Imports System.Threading

Namespace DeribitContango
    ' Simple token-bucket limiter aligned with Deribit best practices and burst control
    Public Class DeribitRateLimiter
        Private ReadOnly _maxPerSecond As Integer
        Private _tokens As Integer
        Private _lastRefill As DateTime = DateTime.UtcNow
        Private ReadOnly _lock As New Object()

        Public Sub New(Optional maxPerSecond As Integer = 20)
            _maxPerSecond = Math.Max(1, maxPerSecond)
            _tokens = _maxPerSecond
        End Sub

        Public Async Function WaitTurnAsync() As Task
            While True
                SyncLock _lock
                    Dim now = DateTime.UtcNow
                    Dim elapsed = (now - _lastRefill).TotalSeconds
                    If elapsed >= 1 Then
                        Dim refill = CInt(Math.Floor(elapsed)) * _maxPerSecond
                        _tokens = Math.Min(_maxPerSecond, _tokens + refill)
                        _lastRefill = now
                    End If
                    If _tokens > 0 Then
                        _tokens -= 1
                        Return
                    End If
                End SyncLock
                Await Task.Delay(50)
            End While
        End Function
    End Class
End Namespace
