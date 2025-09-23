Imports System.Data.SQLite

Public Class TradeAnalytics
    Private ReadOnly connectionString As String

    Public Sub New(databasePath As String)
        connectionString = $"Data Source={databasePath};Version=3;"
    End Sub

    Public Function GetTradeStatistics(Optional days As Integer = 30) As Dictionary(Of String, Object)
        Try
            Using connection As New SQLiteConnection(connectionString)
                connection.Open()

                Dim query As String = $"
                SELECT 
                    COUNT(*) as TotalTrades,
                    SUM(CASE WHEN IsProfit = 1 THEN 1 ELSE 0 END) as WinningTrades,
                    SUM(CASE WHEN IsProfit = 0 THEN 1 ELSE 0 END) as LosingTrades,
                    AVG(OrderSizeUSD) as AvgPositionSize,
                    SUM(ProfitLossUSD) as TotalPnLUSD,
                    AVG(ProfitLossUSD) as AvgPnLUSD,
                    MAX(ProfitLossUSD) as LargestWin,
                    MIN(ProfitLossUSD) as LargestLoss
                FROM Trades 
                WHERE Timestamp >= datetime('now', '-{days} days')"

                Using command As New SQLiteCommand(query, connection)
                    Using reader = command.ExecuteReader()
                        If reader.Read() Then
                            Return CreateStatisticsDictionary(reader)
                        End If
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Throw New Exception($"Error getting trade statistics: {ex.Message}")
        End Try

        Return New Dictionary(Of String, Object)
    End Function

    Private Function CreateStatisticsDictionary(reader As SQLiteDataReader) As Dictionary(Of String, Object)
        Dim totalTrades = If(IsDBNull(reader("TotalTrades")), 0, Convert.ToInt32(reader("TotalTrades")))
        Dim winningTrades = If(IsDBNull(reader("WinningTrades")), 0, Convert.ToInt32(reader("WinningTrades")))

        Return New Dictionary(Of String, Object) From {
            {"TotalTrades", totalTrades},
            {"WinningTrades", winningTrades},
            {"LosingTrades", If(IsDBNull(reader("LosingTrades")), 0, Convert.ToInt32(reader("LosingTrades")))},
            {"WinRate", If(totalTrades > 0, CDbl(winningTrades) / CDbl(totalTrades) * 100, 0)},
            {"AvgPositionSize", If(IsDBNull(reader("AvgPositionSize")), 0, Convert.ToDecimal(reader("AvgPositionSize")))},
            {"TotalPnLUSD", If(IsDBNull(reader("TotalPnLUSD")), 0, Convert.ToDecimal(reader("TotalPnLUSD")))},
            {"AvgPnLUSD", If(IsDBNull(reader("AvgPnLUSD")), 0, Convert.ToDecimal(reader("AvgPnLUSD")))},
            {"LargestWin", If(IsDBNull(reader("LargestWin")), 0, Convert.ToDecimal(reader("LargestWin")))},
            {"LargestLoss", If(IsDBNull(reader("LargestLoss")), 0, Convert.ToDecimal(reader("LargestLoss")))}
        }
    End Function
End Class
