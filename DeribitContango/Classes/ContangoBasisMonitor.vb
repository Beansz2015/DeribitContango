Imports Newtonsoft.Json.Linq

Namespace DeribitContango
    Public Class ContangoBasisMonitor
        Public Property IndexPriceUsd As Decimal
        Public Property SpotBestBid As Decimal
        Public Property SpotBestAsk As Decimal
        Public Property WeeklyFutureBestBid As Decimal
        Public Property WeeklyFutureBestAsk As Decimal
        Public Property WeeklyFutureMark As Decimal
        Public Property WeeklyInstrument As String
        Public Property WeeklyExpiryUtc As DateTime

        ' Basis: (F - S)/S
        Public ReadOnly Property BasisMid As Decimal
            Get
                Dim s = SpotMid
                Dim f = FutureMid
                If s <= 0 OrElse f <= 0 Then Return 0D
                Return (f - s) / s
            End Get
        End Property

        Public ReadOnly Property SpotMid As Decimal
            Get
                If SpotBestBid <= 0 OrElse SpotBestAsk <= 0 Then Return 0D
                Return (SpotBestBid + SpotBestAsk) / 2D
            End Get
        End Property

        Public ReadOnly Property FutureMid As Decimal
            Get
                If WeeklyFutureBestBid > 0 AndAlso WeeklyFutureBestAsk > 0 Then
                    Return (WeeklyFutureBestBid + WeeklyFutureBestAsk) / 2D
                End If
                If WeeklyFutureMark > 0 Then Return WeeklyFutureMark
                Return 0D
            End Get
        End Property

        Public Function AnnualizedFromWeekly() As Decimal
            Dim b = BasisMid
            If b = 0D Then Return 0D
            ' Approximate weekly compounding to annual: (1 + b)^(52) - 1
            Return CDec(Math.Pow(1.0 + CDbl(b), 52.0) - 1.0)
        End Function

        Public Shared Function NextWeeklyExpiryUtc(nowUtc As DateTime) As DateTime
            ' Deribit weeklies expire Friday 08:00 UTC
            Dim d = nowUtc.Date
            ' Move to next Friday
            While d.DayOfWeek <> DayOfWeek.Friday
                d = d.AddDays(1)
            End While
            Dim expiry = d.AddHours(8)
            If expiry <= nowUtc Then
                expiry = expiry.AddDays(7)
            End If
            Return expiry
        End Function

        Public Shared Function ConvertUtcToMYT(dt As DateTime) As DateTime
            ' Malaysia is UTC+8; use Windows ID for Singapore Standard Time for Kuala Lumpur
            Try
                Dim tz = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
                Return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt, DateTimeKind.Utc), tz)
            Catch
                Return dt.AddHours(8)
            End Try
        End Function
    End Class
End Namespace
