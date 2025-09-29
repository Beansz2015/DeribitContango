Imports System.Data.SQLite

Namespace DeribitContango
    Public Class ContangoDatabase
        Private ReadOnly _conn As SQLiteConnection

        Public Sub New(dbPath As String)
            Dim cs = $"Data Source={dbPath};Version=3;"
            _conn = New SQLiteConnection(cs)
            _conn.Open()
            EnsureSchema()
        End Sub

        Private Sub EnsureSchema()
            Using cmd = _conn.CreateCommand()
                cmd.CommandText =
                  "CREATE TABLE IF NOT EXISTS basis_snapshots(" &
                  "ts_utc TEXT NOT NULL," &
                  "index_price REAL," &
                  "spot_bid REAL," &
                  "spot_ask REAL," &
                  "fut_bid REAL," &
                  "fut_ask REAL," &
                  "fut_mark REAL," &
                  "weekly_instr TEXT," &
                  "weekly_expiry_utc TEXT);" &
                  "CREATE TABLE IF NOT EXISTS trades(" &
                  "trade_id TEXT PRIMARY KEY," &
                  "ts_utc TEXT NOT NULL," &
                  "side TEXT NOT NULL," &
                  "instrument TEXT NOT NULL," &
                  "amount REAL," &
                  "contracts INTEGER," &
                  "price REAL," &
                  "fee_currency TEXT," &
                  "fee REAL);" &
                  "CREATE TABLE IF NOT EXISTS positions(" &
                  "id INTEGER PRIMARY KEY," &
                  "active INTEGER NOT NULL," &
                  "entry_ts_utc TEXT," &
                  "spot_instr TEXT," &
                  "fut_instr TEXT," &
                  "target_btc REAL," &
                  "target_usd REAL," &
                  "fut_contracts INTEGER," &
                  "spot_btc REAL);" &
                  "CREATE TABLE IF NOT EXISTS closes(" &
                  "id INTEGER PRIMARY KEY AUTOINCREMENT," &
                  "ts_utc TEXT," &
                  "reason TEXT," &
                  "realized_pnl_usdc REAL);" &
                  "CREATE TABLE IF NOT EXISTS settings(" &
                  "k TEXT PRIMARY KEY," &
                  "v TEXT);" &
                  "CREATE INDEX IF NOT EXISTS idx_snap_ts ON basis_snapshots(ts_utc);" &
                  "CREATE INDEX IF NOT EXISTS idx_trades_ts ON trades(ts_utc);"
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Public Sub SaveSnapshot(tsUtc As DateTime, indexPrice As Decimal, spotBid As Decimal, spotAsk As Decimal, futBid As Decimal, futAsk As Decimal, futMark As Decimal, instr As String, expUtc As DateTime)
            Using cmd = _conn.CreateCommand()
                cmd.CommandText = "INSERT INTO basis_snapshots VALUES(@ts,@idx,@sb,@sa,@fb,@fa,@fm,@ins,@exp)"
                cmd.Parameters.AddWithValue("@ts", tsUtc.ToString("o"))
                cmd.Parameters.AddWithValue("@idx", indexPrice)
                cmd.Parameters.AddWithValue("@sb", spotBid)
                cmd.Parameters.AddWithValue("@sa", spotAsk)
                cmd.Parameters.AddWithValue("@fb", futBid)
                cmd.Parameters.AddWithValue("@fa", futAsk)
                cmd.Parameters.AddWithValue("@fm", futMark)
                cmd.Parameters.AddWithValue("@ins", instr)
                cmd.Parameters.AddWithValue("@exp", expUtc.ToString("o"))
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Public Sub SaveTrade(tradeId As String, tsUtc As DateTime, side As String, instrument As String, amount As Decimal?, contracts As Integer?, price As Decimal, feeCcy As String, fee As Decimal)
            Using cmd = _conn.CreateCommand()
                cmd.CommandText = "INSERT OR REPLACE INTO trades(trade_id,ts_utc,side,instrument,amount,contracts,price,fee_currency,fee) VALUES(@id,@ts,@sd,@ins,@amt,@ctr,@px,@fc,@f)"
                cmd.Parameters.AddWithValue("@id", tradeId)
                cmd.Parameters.AddWithValue("@ts", tsUtc.ToString("o"))
                cmd.Parameters.AddWithValue("@sd", side)
                cmd.Parameters.AddWithValue("@ins", instrument)
                cmd.Parameters.AddWithValue("@amt", If(amount.HasValue, amount.Value, CType(DBNull.Value, Object)))
                cmd.Parameters.AddWithValue("@ctr", If(contracts.HasValue, contracts.Value, CType(DBNull.Value, Object)))
                cmd.Parameters.AddWithValue("@px", price)
                cmd.Parameters.AddWithValue("@fc", feeCcy)
                cmd.Parameters.AddWithValue("@f", fee)
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Public Sub UpsertPosition(active As Boolean, entryTsUtc As DateTime?, spotInstr As String, futInstr As String, targetBtc As Decimal, targetUsd As Decimal, futContracts As Integer, spotBtc As Decimal)
            Using cmd = _conn.CreateCommand()
                cmd.CommandText = "INSERT OR REPLACE INTO positions(id,active,entry_ts_utc,spot_instr,fut_instr,target_btc,target_usd,fut_contracts,spot_btc) " &
                                  "VALUES(1,@a,@e,@si,@fi,@tb,@tu,@fc,@sb)"
                cmd.Parameters.AddWithValue("@a", If(active, 1, 0))
                cmd.Parameters.AddWithValue("@e", If(entryTsUtc.HasValue, entryTsUtc.Value.ToString("o"), CType(DBNull.Value, Object)))
                cmd.Parameters.AddWithValue("@si", spotInstr)
                cmd.Parameters.AddWithValue("@fi", futInstr)
                cmd.Parameters.AddWithValue("@tb", targetBtc)
                cmd.Parameters.AddWithValue("@tu", targetUsd)
                cmd.Parameters.AddWithValue("@fc", futContracts)
                cmd.Parameters.AddWithValue("@sb", spotBtc)
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Public Sub InsertClose(tsUtc As DateTime, reason As String, realizedPnlUsdc As Decimal)
            Using cmd = _conn.CreateCommand()
                cmd.CommandText = "INSERT INTO closes(ts_utc,reason,realized_pnl_usdc) VALUES(@ts,@r,@p)"
                cmd.Parameters.AddWithValue("@ts", tsUtc.ToString("o"))
                cmd.Parameters.AddWithValue("@r", reason)
                cmd.Parameters.AddWithValue("@p", realizedPnlUsdc)
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Public Sub SetSetting(key As String, value As String)
            Using cmd = _conn.CreateCommand()
                cmd.CommandText = "INSERT OR REPLACE INTO settings(k,v) VALUES(@k,@v)"
                cmd.Parameters.AddWithValue("@k", key)
                cmd.Parameters.AddWithValue("@v", value)
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Public Function GetSetting(key As String, Optional defaultValue As String = Nothing) As String
            Using cmd = _conn.CreateCommand()
                cmd.CommandText = "SELECT v FROM settings WHERE k=@k"
                cmd.Parameters.AddWithValue("@k", key)
                Dim o = cmd.ExecuteScalar()
                If o Is Nothing OrElse o Is DBNull.Value Then Return defaultValue
                Return CStr(o)
            End Using
        End Function
    End Class
End Namespace
