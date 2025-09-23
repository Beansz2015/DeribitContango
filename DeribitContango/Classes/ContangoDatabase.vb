Imports System.Data.SQLite
Imports System.IO

Public Class ContangoDatabase

    Private ReadOnly connectionString As String
    Private ReadOnly dbPath As String

    Public Sub New(Optional databasePath As String = "")
        If String.IsNullOrEmpty(databasePath) Then
            dbPath = Path.Combine(Application.StartupPath, "contango.db")
        Else
            dbPath = databasePath
        End If

        connectionString = $"Data Source={dbPath};Version=3;"
        InitializeDatabase()
    End Sub

    Private Sub InitializeDatabase()
        Try
            Using connection As New SQLiteConnection(connectionString)
                connection.Open()

                ' Create trades table
                Dim createTradesTable As String = "
                CREATE TABLE IF NOT EXISTS ContangoTrades (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    EntryDate TEXT NOT NULL,
                    ExitDate TEXT,
                    EntrySpotPrice DECIMAL NOT NULL,
                    EntryFuturesPrice DECIMAL NOT NULL,
                    ExitSpotPrice DECIMAL,
                    ExitFuturesPrice DECIMAL,
                    PositionSize DECIMAL NOT NULL,
                    EntryBasisSpread DECIMAL NOT NULL,
                    ExitBasisSpread DECIMAL,
                    RealizedPnL DECIMAL,
                    ContractName TEXT NOT NULL,
                    DaysHeld INTEGER,
                    Status TEXT DEFAULT 'OPEN',
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )"

                Using command As New SQLiteCommand(createTradesTable, connection)
                    command.ExecuteNonQuery()
                End Using

                ' Create basis history table
                Dim createBasisTable As String = "
                CREATE TABLE IF NOT EXISTS BasisHistory (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    SpotPrice DECIMAL NOT NULL,
                    FuturesPrice DECIMAL NOT NULL,
                    BasisSpread DECIMAL NOT NULL,
                    ContractName TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )"

                Using command As New SQLiteCommand(createBasisTable, connection)
                    command.ExecuteNonQuery()
                End Using

                ' Create performance logs table
                Dim createLogsTable As String = "
                CREATE TABLE IF NOT EXISTS PerformanceLogs (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    LogLevel TEXT NOT NULL,
                    Category TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )"

                Using command As New SQLiteCommand(createLogsTable, connection)
                    command.ExecuteNonQuery()
                End Using

            End Using

        Catch ex As Exception
            Throw New Exception($"Database initialization failed: {ex.Message}")
        End Try
    End Sub

    Public Function SaveContangoTrade(trade As ContangoTrade) As Integer
        Try
            Using connection As New SQLiteConnection(connectionString)
                connection.Open()

                Dim insertQuery As String = "
                INSERT INTO ContangoTrades (
                    EntryDate, ExitDate, EntrySpotPrice, EntryFuturesPrice, 
                    ExitSpotPrice, ExitFuturesPrice, PositionSize, 
                    EntryBasisSpread, ExitBasisSpread, RealizedPnL, 
                    ContractName, DaysHeld, Status
                ) VALUES (
                    @EntryDate, @ExitDate, @EntrySpotPrice, @EntryFuturesPrice,
                    @ExitSpotPrice, @ExitFuturesPrice, @PositionSize,
                    @EntryBasisSpread, @ExitBasisSpread, @RealizedPnL,
                    @ContractName, @DaysHeld, @Status
                )"

                Using command As New SQLiteCommand(insertQuery, connection)
                    command.Parameters.AddWithValue("@EntryDate", trade.EntryDate.ToString("yyyy-MM-dd HH:mm:ss"))
                    command.Parameters.AddWithValue("@ExitDate", If(trade.ExitDate = DateTime.MinValue, DBNull.Value, trade.ExitDate.ToString("yyyy-MM-dd HH:mm:ss")))
                    command.Parameters.AddWithValue("@EntrySpotPrice", trade.EntrySpotPrice)
                    command.Parameters.AddWithValue("@EntryFuturesPrice", trade.EntryFuturesPrice)
                    command.Parameters.AddWithValue("@ExitSpotPrice", If(trade.ExitSpotPrice = 0, DBNull.Value, trade.ExitSpotPrice))
                    command.Parameters.AddWithValue("@ExitFuturesPrice", If(trade.ExitFuturesPrice = 0, DBNull.Value, trade.ExitFuturesPrice))
                    command.Parameters.AddWithValue("@PositionSize", trade.PositionSize)
                    command.Parameters.AddWithValue("@EntryBasisSpread", trade.EntryBasisSpread)
                    command.Parameters.AddWithValue("@ExitBasisSpread", If(trade.ExitBasisSpread = 0, DBNull.Value, trade.ExitBasisSpread))
                    command.Parameters.AddWithValue("@RealizedPnL", If(trade.RealizedPnL = 0, DBNull.Value, trade.RealizedPnL))
                    command.Parameters.AddWithValue("@ContractName", trade.ContractName)
                    command.Parameters.AddWithValue("@DaysHeld", trade.DaysHeld)
                    command.Parameters.AddWithValue("@Status", If(trade.ExitDate = DateTime.MinValue, "OPEN", "CLOSED"))

                    command.ExecuteNonQuery()

                    ' Get the inserted ID
                    command.CommandText = "SELECT last_insert_rowid()"
                    Return Convert.ToInt32(command.ExecuteScalar())
                End Using
            End Using

        Catch ex As Exception
            Throw New Exception($"Failed to save trade: {ex.Message}")
        End Try
    End Function

    Public Sub SaveBasisData(spotPrice As Decimal, futuresPrice As Decimal, basisSpread As Decimal, contractName As String)
        Try
            Using connection As New SQLiteConnection(connectionString)
                connection.Open()

                Dim insertQuery As String = "
                INSERT INTO BasisHistory (Timestamp, SpotPrice, FuturesPrice, BasisSpread, ContractName)
                VALUES (@Timestamp, @SpotPrice, @FuturesPrice, @BasisSpread, @ContractName)"

                Using command As New SQLiteCommand(insertQuery, connection)
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    command.Parameters.AddWithValue("@SpotPrice", spotPrice)
                    command.Parameters.AddWithValue("@FuturesPrice", futuresPrice)
                    command.Parameters.AddWithValue("@BasisSpread", basisSpread)
                    command.Parameters.AddWithValue("@ContractName", contractName)

                    command.ExecuteNonQuery()
                End Using
            End Using

        Catch ex As Exception
            ' Don't throw on logging errors
        End Try
    End Sub

    Public Sub LogPerformance(message As String, level As String, Optional category As String = "GENERAL")
        Try
            Using connection As New SQLiteConnection(connectionString)
                connection.Open()

                Dim insertQuery As String = "
                INSERT INTO PerformanceLogs (Timestamp, Message, LogLevel, Category)
                VALUES (@Timestamp, @Message, @LogLevel, @Category)"

                Using command As New SQLiteCommand(insertQuery, connection)
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    command.Parameters.AddWithValue("@Message", message)
                    command.Parameters.AddWithValue("@LogLevel", level)
                    command.Parameters.AddWithValue("@Category", category)

                    command.ExecuteNonQuery()
                End Using
            End Using

        Catch ex As Exception
            ' Don't throw on logging errors
        End Try
    End Sub

    Public Function GetTradeHistory(Optional limit As Integer = 100) As List(Of ContangoTrade)
        Dim trades As New List(Of ContangoTrade)

        Try
            Using connection As New SQLiteConnection(connectionString)
                connection.Open()

                Dim selectQuery As String = $"
                SELECT * FROM ContangoTrades 
                ORDER BY EntryDate DESC 
                LIMIT {limit}"

                Using command As New SQLiteCommand(selectQuery, connection)
                    Using reader As SQLiteDataReader = command.ExecuteReader()
                        While reader.Read()
                            trades.Add(New ContangoTrade With {
                                .EntryDate = DateTime.Parse(reader("EntryDate").ToString()),
                                .ExitDate = If(IsDBNull(reader("ExitDate")), DateTime.MinValue, DateTime.Parse(reader("ExitDate").ToString())),
                                .EntrySpotPrice = Convert.ToDecimal(reader("EntrySpotPrice")),
                                .EntryFuturesPrice = Convert.ToDecimal(reader("EntryFuturesPrice")),
                                .ExitSpotPrice = If(IsDBNull(reader("ExitSpotPrice")), 0, Convert.ToDecimal(reader("ExitSpotPrice"))),
                                .ExitFuturesPrice = If(IsDBNull(reader("ExitFuturesPrice")), 0, Convert.ToDecimal(reader("ExitFuturesPrice"))),
                                .PositionSize = Convert.ToDecimal(reader("PositionSize")),
                                .EntryBasisSpread = Convert.ToDecimal(reader("EntryBasisSpread")),
                                .ExitBasisSpread = If(IsDBNull(reader("ExitBasisSpread")), 0, Convert.ToDecimal(reader("ExitBasisSpread"))),
                                .RealizedPnL = If(IsDBNull(reader("RealizedPnL")), 0, Convert.ToDecimal(reader("RealizedPnL"))),
                                .ContractName = reader("ContractName").ToString(),
                                .DaysHeld = If(IsDBNull(reader("DaysHeld")), 0, Convert.ToInt32(reader("DaysHeld")))
                            })
                        End While
                    End Using
                End Using
            End Using

        Catch ex As Exception
            ' Return empty list on error
        End Try

        Return trades
    End Function

    Public Function GetTotalProfit() As Decimal
        Try
            Using connection As New SQLiteConnection(connectionString)
                connection.Open()

                Dim selectQuery As String = "SELECT SUM(RealizedPnL) FROM ContangoTrades WHERE Status = 'CLOSED'"

                Using command As New SQLiteCommand(selectQuery, connection)
                    Dim result = command.ExecuteScalar()
                    Return If(IsDBNull(result), 0, Convert.ToDecimal(result))
                End Using
            End Using

        Catch ex As Exception
            Return 0
        End Try
    End Function

End Class
