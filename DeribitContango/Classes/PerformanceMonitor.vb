Namespace DeribitContango
    Public Class PerformanceMonitor
        Public Property RealizedPnL_USDC As Decimal
        Public Property Fees_USDC As Decimal

        Public Sub AddTrade(feeCcy As String, fee As Decimal, pnlDeltaUsdc As Decimal)
            ' If fees on BTC, ignore conversion here; focus on USDC PnL aggregation for basis core
            If feeCcy IsNot Nothing AndAlso feeCcy.ToUpperInvariant() = "USDC" Then
                Fees_USDC += fee
            End If
            RealizedPnL_USDC += pnlDeltaUsdc
        End Sub
    End Class
End Namespace
