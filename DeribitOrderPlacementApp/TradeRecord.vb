Imports System.Data.SQLite
Imports System.IO
Public Class TradeRecord
    Public Property TradeId As Integer
    Public Property Timestamp As DateTime ' Exit timestamp
    Public Property OrderType As String ' Limit, Trailing, Market
    Public Property Direction As String ' Long, Short
    Public Property EntryPrice As Decimal
    Public Property ExitPrice As Decimal
    Public Property OrderSizeUSD As Decimal
    Public Property ProfitLossUSD As Decimal
    Public Property IsProfit As Boolean

    ' Add slippage tracking
    Public Property RequoteCount As Integer = 0
    Public Property AttemptType As String = "Entry" ' "Entry", "Retry", "Abandoned"
    Public Property SignalPrice As Decimal = 0 ' Original signal price
    Public Property SlippageATR As Decimal = 0 ' Slippage in ATR units
    Public Property MaxSlippageExceeded As Boolean = False

    Public Sub New()
        Timestamp = DateTime.UtcNow
    End Sub

    Public Sub New(orderType As String, direction As String, entryPrice As Decimal,
                   exitPrice As Decimal, orderSizeUSD As Decimal, profitLossUSD As Decimal, isProfit As Boolean)
        Me.New() ' Call default constructor
        Me.OrderType = orderType
        Me.Direction = direction
        Me.EntryPrice = entryPrice
        Me.ExitPrice = exitPrice
        Me.OrderSizeUSD = orderSizeUSD
        Me.ProfitLossUSD = profitLossUSD
        Me.IsProfit = isProfit
    End Sub
End Class

