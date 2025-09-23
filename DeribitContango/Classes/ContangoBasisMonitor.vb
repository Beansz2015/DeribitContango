
Public Class ContangoBasisMonitor

    Public Property CurrentSpotPrice As Decimal
    Public Property CurrentFuturesPrice As Decimal
    Public Property LastUpdateTime As DateTime

    Private basisHistory As New List(Of BasisDataPoint)
    Private ReadOnly maxHistoryPoints As Integer = 1000

    Public Function CalculateBasisSpread(spotPrice As Decimal, futuresPrice As Decimal) As Decimal
        If spotPrice <= 0 Then Return 0

        CurrentSpotPrice = spotPrice
        CurrentFuturesPrice = futuresPrice
        LastUpdateTime = DateTime.Now

        Dim basis As Decimal = (futuresPrice - spotPrice) / spotPrice

        ' Store in history
        AddToHistory(basis)

        Return basis
    End Function

    Public Function GetAnnualizedBasisReturn(weeklyBasis As Decimal) As Decimal
        ' Convert weekly basis to annualized return
        ' Using compound return: (1 + weekly)^52 - 1
        Return (CDec(Math.Pow(CDbl(1 + weeklyBasis), 52)) - 1)
    End Function

    Public Function IsRollingProfitable(nextWeeklyFuturesPrice As Decimal, minimumBasisThreshold As Decimal) As Boolean
        If CurrentSpotPrice <= 0 Then Return False

        Dim nextBasis As Decimal = CalculateBasisSpread(CurrentSpotPrice, nextWeeklyFuturesPrice)
        Return nextBasis >= minimumBasisThreshold
    End Function

    Public Function GetAverageBasisLast24Hours() As Decimal
        Dim cutoffTime As DateTime = DateTime.Now.AddHours(-24)
        Dim recentBasis As New List(Of BasisDataPoint)

        ' Manual filtering instead of LINQ Where()
        For Each item In basisHistory
            If item.Timestamp >= cutoffTime Then
                recentBasis.Add(item)
            End If
        Next

        If recentBasis.Count = 0 Then Return 0

        ' Manual average calculation
        Dim total As Decimal = 0
        For Each item In recentBasis
            total += item.BasisSpread
        Next

        Return total / recentBasis.Count
    End Function

    Public Function GetBasisVolatility() As Decimal
        If basisHistory.Count < 2 Then Return 0

        ' Get last 100 entries manually
        Dim startIndex As Integer = Math.Max(0, basisHistory.Count - 100)
        Dim recent As New List(Of Decimal)

        For i As Integer = startIndex To basisHistory.Count - 1
            recent.Add(basisHistory(i).BasisSpread)
        Next

        If recent.Count < 2 Then Return 0

        ' Calculate mean manually
        Dim total As Decimal = 0
        For Each value In recent
            total += value
        Next
        Dim mean As Decimal = total / recent.Count

        ' Calculate variance manually
        Dim varianceSum As Decimal = 0
        For Each value In recent
            varianceSum += (value - mean) * (value - mean)
        Next
        Dim variance As Decimal = varianceSum / (recent.Count - 1)

        Return CDec(Math.Sqrt(CDbl(variance)))
    End Function

    Private Sub AddToHistory(basisSpread As Decimal)
        basisHistory.Add(New BasisDataPoint With {
            .Timestamp = DateTime.Now,
            .BasisSpread = basisSpread,
            .SpotPrice = CurrentSpotPrice,
            .FuturesPrice = CurrentFuturesPrice
        })

        ' Limit history size
        If basisHistory.Count > maxHistoryPoints Then
            basisHistory.RemoveRange(0, basisHistory.Count - maxHistoryPoints)
        End If
    End Sub

    Public Function GetBasisTrend(lookbackMinutes As Integer) As String
        Dim cutoffTime As DateTime = DateTime.Now.AddMinutes(-lookbackMinutes)
        Dim recentData As New List(Of BasisDataPoint)

        ' Manual filtering and sorting
        For Each item In basisHistory
            If item.Timestamp >= cutoffTime Then
                recentData.Add(item)
            End If
        Next

        ' Simple sort by timestamp
        recentData.Sort(Function(x, y) x.Timestamp.CompareTo(y.Timestamp))

        If recentData.Count < 2 Then Return "INSUFFICIENT_DATA"

        Dim firstBasis As Decimal = recentData(0).BasisSpread
        Dim lastBasis As Decimal = recentData(recentData.Count - 1).BasisSpread
        Dim change As Decimal = lastBasis - firstBasis

        If Math.Abs(change) < 0.0001 Then Return "STABLE"
        Return If(change > 0, "EXPANDING", "COMPRESSING")
    End Function

End Class

Public Class BasisDataPoint
    Public Property Timestamp As DateTime
    Public Property BasisSpread As Decimal
    Public Property SpotPrice As Decimal
    Public Property FuturesPrice As Decimal
End Class
