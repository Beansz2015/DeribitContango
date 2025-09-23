
Public Class ContangoPositionManager

    Public Property CurrentSpotPosition As Decimal
    Public Property CurrentFuturesPosition As Decimal
    Public Property EntrySpotPrice As Decimal
    Public Property EntryFuturesPrice As Decimal
    Public Property EntryBasisSpread As Decimal
    Public Property PositionSize As Decimal
    Public Property EntryTimestamp As DateTime
    Public Property ExpiryDate As DateTime
    Public Property ContractName As String

    Private tradeHistory As New List(Of ContangoTrade)

    Public ReadOnly Property IsHedged As Boolean
        Get
            ' Position is hedged if spot and futures positions are roughly equal and opposite
            Return Math.Abs(CurrentSpotPosition + CurrentFuturesPosition) < (PositionSize * 0.01D) ' 1% tolerance
        End Get
    End Property

    Public ReadOnly Property IsPositionActive As Boolean
        Get
            Return PositionSize > 0 AndAlso (CurrentSpotPosition <> 0 OrElse CurrentFuturesPosition <> 0)
        End Get
    End Property

    Public ReadOnly Property DaysToExpiry As Integer
        Get
            If ExpiryDate = DateTime.MinValue Then Return 0
            Return Math.Max(0, CInt((ExpiryDate - DateTime.Now).TotalDays))
        End Get
    End Property

    Public Function CalculateUnrealizedPnL(currentSpotPrice As Decimal, currentFuturesPrice As Decimal) As Decimal
        If Not IsPositionActive Then Return 0

        ' Calculate P&L for cash-carry position
        ' Long spot: (currentSpotPrice - entrySpotPrice) * positionSize
        ' Short futures: (entryFuturesPrice - currentFuturesPrice) * positionSize

        Dim spotPnL As Decimal = (currentSpotPrice - EntrySpotPrice) * CurrentSpotPosition
        Dim futuresPnL As Decimal = (EntryFuturesPrice - currentFuturesPrice) * Math.Abs(CurrentFuturesPosition)

        Return spotPnL + futuresPnL
    End Function

    Public Function CalculateExpectedProfit() As Decimal
        ' Expected profit is the captured basis spread
        Return EntryBasisSpread * PositionSize * EntrySpotPrice
    End Function

    Public Function CalculateCurrentBasisSpread(currentSpotPrice As Decimal, currentFuturesPrice As Decimal) As Decimal
        If currentSpotPrice <= 0 Then Return 0
        Return (currentFuturesPrice - currentSpotPrice) / currentSpotPrice
    End Function

    Public Sub OpenCashCarryPosition(spotPrice As Decimal, futuresPrice As Decimal, positionSizeParam As Decimal, contractNameParam As String, expiryDateParam As DateTime)
        EntrySpotPrice = spotPrice
        EntryFuturesPrice = futuresPrice
        PositionSize = positionSizeParam
        ContractName = contractNameParam
        ExpiryDate = expiryDateParam
        EntryTimestamp = DateTime.Now

        ' Set positions
        CurrentSpotPosition = positionSizeParam ' Long spot
        CurrentFuturesPosition = -positionSizeParam ' Short futures

        ' Calculate entry basis
        EntryBasisSpread = (futuresPrice - spotPrice) / spotPrice
    End Sub

    Public Function CloseCashCarryPosition(currentSpotPrice As Decimal, currentFuturesPrice As Decimal) As ContangoTrade
        If Not IsPositionActive Then Return Nothing

        ' Calculate final P&L
        Dim finalPnL As Decimal = CalculateUnrealizedPnL(currentSpotPrice, currentFuturesPrice)

        ' Create trade record
        Dim completedTrade As New ContangoTrade With {
            .EntryDate = EntryTimestamp,
            .ExitDate = DateTime.Now,
            .EntrySpotPrice = EntrySpotPrice,
            .EntryFuturesPrice = EntryFuturesPrice,
            .ExitSpotPrice = currentSpotPrice,
            .ExitFuturesPrice = currentFuturesPrice,
            .PositionSize = PositionSize,
            .EntryBasisSpread = EntryBasisSpread,
            .ExitBasisSpread = CalculateCurrentBasisSpread(currentSpotPrice, currentFuturesPrice),
            .RealizedPnL = finalPnL,
            .ContractName = ContractName,
            .DaysHeld = CInt((DateTime.Now - EntryTimestamp).TotalDays)
        }

        ' Add to history
        tradeHistory.Add(completedTrade)

        ' Reset positions
        ResetPosition()

        Return completedTrade
    End Function

    Private Sub ResetPosition()
        CurrentSpotPosition = 0
        CurrentFuturesPosition = 0
        EntrySpotPrice = 0
        EntryFuturesPrice = 0
        EntryBasisSpread = 0
        PositionSize = 0
        EntryTimestamp = DateTime.MinValue
        ExpiryDate = DateTime.MinValue
        ContractName = ""
    End Sub

    Public Function GetPositionSummary() As String
        If Not IsPositionActive Then Return "No active position"

        Return $"Position: {PositionSize} BTC | Entry Basis: {EntryBasisSpread:P3} | Days to Expiry: {DaysToExpiry} | Contract: {ContractName}"
    End Function

    Public Function GetTradeHistory() As List(Of ContangoTrade)
        Return tradeHistory.ToList()
    End Function

    Public Function GetTotalProfit() As Decimal
        Return tradeHistory.Sum(Function(t) t.RealizedPnL)
    End Function

    Public Function GetWinRate() As Decimal
        If tradeHistory.Count = 0 Then Return 0

        Dim winningTrades As Integer = 0
        For Each trade In tradeHistory
            If trade.RealizedPnL > 0 Then
                winningTrades += 1
            End If
        Next

        Return CDec(winningTrades) / tradeHistory.Count
    End Function

End Class

Public Class ContangoTrade
    Public Property EntryDate As DateTime
    Public Property ExitDate As DateTime
    Public Property EntrySpotPrice As Decimal
    Public Property EntryFuturesPrice As Decimal
    Public Property ExitSpotPrice As Decimal
    Public Property ExitFuturesPrice As Decimal
    Public Property PositionSize As Decimal
    Public Property EntryBasisSpread As Decimal
    Public Property ExitBasisSpread As Decimal
    Public Property RealizedPnL As Decimal
    Public Property ContractName As String
    Public Property DaysHeld As Integer

    Public ReadOnly Property ReturnOnCapital As Decimal
        Get
            If EntrySpotPrice * PositionSize = 0 Then Return 0
            Return RealizedPnL / (EntrySpotPrice * PositionSize)
        End Get
    End Property

    Public ReadOnly Property AnnualizedReturn As Decimal
        Get
            If DaysHeld = 0 Then Return 0
            Return ReturnOnCapital * (365D / DaysHeld)
        End Get
    End Property

End Class
