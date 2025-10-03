Imports Newtonsoft.Json.Linq
Imports System.Text

Public Class frmContangoMain

    Private _db As DeribitContango.ContangoDatabase
    Private _rate As DeribitContango.DeribitRateLimiter
    Private _api As DeribitContango.DeribitApiClient
    Private _mon As DeribitContango.ContangoBasisMonitor
    Private _pm As DeribitContango.ContangoPositionManager

    Private _uiTimer As System.Windows.Forms.Timer

    Private Sub frmContangoMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            _db = New DeribitContango.ContangoDatabase("contango.db3")
            _rate = New DeribitContango.DeribitRateLimiter(20)
            _api = New DeribitContango.DeribitApiClient(_rate)
            _mon = New DeribitContango.ContangoBasisMonitor()
            _pm = New DeribitContango.ContangoPositionManager(_api, _db, _mon)

            AddHandler _api.ConnectionStateChanged, AddressOf OnConnState
            AddHandler _api.PublicMessage, AddressOf OnPublicMessage
            AddHandler _api.OrderUpdate, AddressOf OnOrderUpdate
            AddHandler _api.TradeUpdate, AddressOf OnTradeUpdate
            AddHandler _pm.Info, Sub(m) AppendLog(m)   ' <-- integration for re-quote/watchdog messages

            ' Initialize UI controls from PM defaults
            numRequoteTicks.Value = _pm.RequoteMinTicks
            numRequoteMs.Value = _pm.RequoteIntervalMs

            _uiTimer = New System.Windows.Forms.Timer()
            _uiTimer.Interval = 1000
            AddHandler _uiTimer.Tick, AddressOf OnUiTick
            _uiTimer.Start()

            radUSD.Checked = True
            numThreshold.Value = CDec(_pm.EntryThreshold)
            numSlippageBps.Value = CDec(_pm.MaxSlippageBps)

            AppendLog("Initialized UI and core components.")
        Catch ex As Exception
            AppendLog("Load error: " & ex.Message)
        End Try
    End Sub

    Private Sub numRequoteTicks_ValueChanged(sender As Object, e As EventArgs) Handles numRequoteTicks.ValueChanged
        _pm.RequoteMinTicks = CInt(numRequoteTicks.Value)
    End Sub

    Private Sub numRequoteMs_ValueChanged(sender As Object, e As EventArgs) Handles numRequoteMs.ValueChanged
        _pm.RequoteIntervalMs = CInt(numRequoteMs.Value)
    End Sub


    Private Async Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
        Try
            btnConnect.Enabled = False
            Dim wsUri As New Uri("wss://www.deribit.com/ws/api/v2")

            Await _api.ConnectAsync(wsUri)
            AppendLog("WebSocket connected.")

            Await _api.AuthorizeAsync(txtClientId.Text.Trim(), txtClientSecret.Text.Trim())
            AppendLog("Authorized; subscribing channels...")

            Await _api.SubscribePrivateAsync({
        "user.orders.BTC",
        "user.trades.BTC",
        "user.orders.USDC",
        "user.trades.USDC"
      })

            Await _api.SubscribePublicAsync({
        "deribit_price_index.btc_usd",
        "ticker.BTC_USDC.100ms",
        "book.BTC_USDC.100ms"
      })

            Await _pm.RefreshInstrumentSpecsAsync()

            AppendLog("Subscriptions active. Click Discover Weekly to select nearest future.")
        Catch ex As Exception
            AppendLog("Connect error: " & ex.Message)
            btnConnect.Enabled = True
        End Try
    End Sub

    Private Async Sub btnDiscoverWeekly_Click(sender As Object, e As EventArgs) Handles btnDiscoverWeekly.Click
        Try
            Await _pm.DiscoverNearestWeeklyAsync()
            AppendLog("Nearest weekly: " & _pm.FuturesInstrument)

            Await _api.SubscribePublicAsync({
        $"ticker.{_pm.FuturesInstrument}.100ms",
        $"book.{_pm.FuturesInstrument}.100ms"
      })

            Await _pm.RefreshInstrumentSpecsAsync()
            UpdateExpiryLabels()
        Catch ex As Exception
            AppendLog("Discover error: " & ex.Message)
        End Try
    End Sub

    Private Async Sub btnEnter_Click(sender As Object, e As EventArgs) Handles btnEnter.Click
        Try
            _pm.UseUsdInput = radUSD.Checked
            _pm.EntryThreshold = numThreshold.Value
            _pm.MaxSlippageBps = numSlippageBps.Value

            Dim amt As Decimal
            If Not Decimal.TryParse(txtAmount.Text.Trim(), amt) OrElse amt <= 0D Then
                Throw New ApplicationException("Invalid amount.")
            End If
            If _pm.UseUsdInput Then
                _pm.TargetUsd = amt
                _pm.TargetBtc = 0D
            Else
                _pm.TargetBtc = amt
                _pm.TargetUsd = 0D
            End If

            If String.IsNullOrEmpty(_pm.FuturesInstrument) Then
                Throw New ApplicationException("Weekly future not selected. Click Discover Weekly first.")
            End If

            ' Futures first, spot on fills
            Await _pm.EnterBasisAsync(_mon.IndexPriceUsd, _mon.WeeklyFutureBestBid, _mon.SpotBestAsk)
            AppendLog("Submitted futures post_only; re-quote active until fills trigger spot IOC hedges.")
        Catch ex As Exception
            AppendLog("Enter error: " & ex.Message)
        End Try
    End Sub

    Private Async Sub btnRoll_Click(sender As Object, e As EventArgs) Handles btnRoll.Click
        Try
            Await _pm.RollToNextWeeklyAsync(_mon.IndexPriceUsd)
            AppendLog("Requested roll: closed current via reduce_only and selected next weekly.")

            If Not String.IsNullOrEmpty(_pm.FuturesInstrument) Then
                Await _api.SubscribePublicAsync({
          $"ticker.{_pm.FuturesInstrument}.100ms",
          $"book.{_pm.FuturesInstrument}.100ms"
        })
                Await _pm.RefreshInstrumentSpecsAsync()
                UpdateExpiryLabels()
            End If
        Catch ex As Exception
            AppendLog("Roll error: " & ex.Message)
        End Try
    End Sub

    Private Async Sub btnCloseAll_Click(sender As Object, e As EventArgs) Handles btnCloseAll.Click
        Try
            Await _pm.CloseAllAsync()
            AppendLog("CloseAll requested; awaiting reduce_only fills.")
        Catch ex As Exception
            AppendLog("CloseAll error: " & ex.Message)
        End Try
    End Sub

    ' Events

    Private Sub OnConnState(connected As Boolean)
        If InvokeRequired Then
            BeginInvoke(Sub() OnConnState(connected))
            Return
        End If
        lblConn.Text = If(connected, "Connected (WS)", "Disconnected (WS)")
        btnConnect.Enabled = Not connected
    End Sub

    Private Sub OnPublicMessage(topic As String, payload As JObject)
        Try
            If topic.StartsWith("deribit_price_index", StringComparison.OrdinalIgnoreCase) Then
                Dim px = payload.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                If px <= 0D Then px = payload.Value(Of Decimal?)("index_price").GetValueOrDefault(0D)
                If px > 0D Then _mon.IndexPriceUsd = px
            ElseIf topic.StartsWith("ticker.BTC_USDC", StringComparison.OrdinalIgnoreCase) Then
                Dim bb = payload.Value(Of Decimal?)("best_bid_price").GetValueOrDefault(0D)
                Dim ba = payload.Value(Of Decimal?)("best_ask_price").GetValueOrDefault(0D)
                If bb > 0D Then _mon.SpotBestBid = bb
                If ba > 0D Then _mon.SpotBestAsk = ba
            ElseIf topic.StartsWith("book.BTC_USDC", StringComparison.OrdinalIgnoreCase) Then
                UpdateFromBookPayload(payload, isSpot:=True)
            ElseIf topic.StartsWith("ticker.", StringComparison.OrdinalIgnoreCase) AndAlso Not topic.Contains("BTC_USDC") Then
                Dim bb = payload.Value(Of Decimal?)("best_bid_price").GetValueOrDefault(0D)
                Dim ba = payload.Value(Of Decimal?)("best_ask_price").GetValueOrDefault(0D)
                Dim mark = payload.Value(Of Decimal?)("mark_price").GetValueOrDefault(0D)
                If bb > 0D Then _mon.WeeklyFutureBestBid = bb
                If ba > 0D Then _mon.WeeklyFutureBestAsk = ba
                If mark > 0D Then _mon.WeeklyFutureMark = mark
            ElseIf topic.StartsWith("book.", StringComparison.OrdinalIgnoreCase) AndAlso Not topic.Contains("BTC_USDC") Then
                UpdateFromBookPayload(payload, isSpot:=False)
            End If
        Catch ex As Exception
            AppendLog("Public parse error: " & ex.Message)
        End Try
    End Sub

    Private Sub OnOrderUpdate(currency As String, payload As JObject)
        Dim st = payload.Value(Of String)("state")
        If String.IsNullOrEmpty(st) Then st = payload.Value(Of String)("order_state")
        Dim line = $"ORDER {currency} {payload.Value(Of String)("order_id")} {st} {payload.Value(Of String)("instrument_name")}"
        AppendLog(line)
    End Sub


    Private Sub OnTradeUpdate(currency As String, payload As JObject)
        Dim instr = payload.Value(Of String)("instrument_name")
        AppendLog($"TRADE {currency} {instr}")
    End Sub

    Private Sub OnUiTick(sender As Object, e As EventArgs)
        Try
            lblIndex.Text = _mon.IndexPriceUsd.ToString("0.00")
            lblSpotBid.Text = _mon.SpotBestBid.ToString("0.00")
            lblSpotAsk.Text = _mon.SpotBestAsk.ToString("0.00")
            lblFutBid.Text = _mon.WeeklyFutureBestBid.ToString("0.00")
            lblFutAsk.Text = _mon.WeeklyFutureBestAsk.ToString("0.00")
            lblFutMark.Text = If(_mon.WeeklyFutureMark > 0D, _mon.WeeklyFutureMark.ToString("0.00"), "-")

            Dim b = _mon.BasisMid
            lblBasis.Text = (b * 100D).ToString("0.00") & "%"

            Dim a = _mon.AnnualizedFromWeekly()
            lblAnnual.Text = (a * 100D).ToString("0.00") & "%"

            UpdateExpiryLabels()

            Static lastSnap As DateTime = DateTime.MinValue
            If (DateTime.UtcNow - lastSnap).TotalSeconds >= 2 Then
                lastSnap = DateTime.UtcNow
                _db.SaveSnapshot(
                  DateTime.UtcNow,
                  _mon.IndexPriceUsd,
                  _mon.SpotBestBid,
                  _mon.SpotBestAsk,
                  _mon.WeeklyFutureBestBid,
                  _mon.WeeklyFutureBestAsk,
                  _mon.WeeklyFutureMark,
                  _pm.FuturesInstrument,
                  _pm.ExpiryUtc
                )
            End If
        Catch
        End Try
    End Sub

    Private Sub UpdateExpiryLabels()
        Dim expUtc = _pm.ExpiryUtc
        If expUtc = Date.MinValue Then
            lblExpiryUTC.Text = "-"
            lblExpiryMYT.Text = "-"
        Else
            lblExpiryUTC.Text = expUtc.ToString("yyyy-MM-dd HH:mm:ss") & " (UTC)"
            Dim myt = DeribitContango.ContangoBasisMonitor.ConvertUtcToMYT(expUtc)
            lblExpiryMYT.Text = myt.ToString("yyyy-MM-dd HH:mm:ss") & " (MYT)"
        End If
        lblNowUTC.Text = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") & " (UTC)"
        Dim nowMyt = DeribitContango.ContangoBasisMonitor.ConvertUtcToMYT(DateTime.UtcNow)
        lblNowMYT.Text = nowMyt.ToString("yyyy-MM-dd HH:mm:ss") & " (MYT)"
    End Sub

    Private Sub radUSD_CheckedChanged(sender As Object, e As EventArgs) Handles radUSD.CheckedChanged
        lblAmountUnits.Text = If(radUSD.Checked, "Amount USD", "Amount BTC")
    End Sub

    Private Sub radBTC_CheckedChanged(sender As Object, e As EventArgs) Handles radBTC.CheckedChanged
        lblAmountUnits.Text = If(radUSD.Checked, "Amount USD", "Amount BTC")
    End Sub

    Private Sub AppendLog(msg As String)
        If InvokeRequired Then
            BeginInvoke(Sub() AppendLog(msg))
            Return
        End If
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}" & Environment.NewLine)
    End Sub


    ' Returns Nullable(Of Decimal) to allow graceful fallback
    Private Function ExtractTopPriceFromBookEntry(entry As JToken) As Decimal?
        If entry Is Nothing Then Return Nothing

        ' Case 1: Array formats
        If entry.Type = JTokenType.Array Then
            Dim arr = DirectCast(entry, JArray)
            If arr.Count >= 3 AndAlso arr(0).Type = JTokenType.String Then
                ' ["new"|"change"|"delete", price, amount]
                If String.Equals(arr(0).Value(Of String)(), "delete", StringComparison.OrdinalIgnoreCase) Then
                    Return Nothing
                End If
                If arr(1).Type = JTokenType.Float OrElse arr(1).Type = JTokenType.Integer Then
                    Return arr(1).Value(Of Decimal)()
                End If
            ElseIf arr.Count >= 2 Then
                ' [price, amount]
                If arr(0).Type = JTokenType.Float OrElse arr(0).Type = JTokenType.Integer Then
                    Return arr(0).Value(Of Decimal)()
                End If
            End If
            Return Nothing
        End If

        ' Case 2: Object formats
        If entry.Type = JTokenType.Object Then
            Dim o = DirectCast(entry, JObject)
            ' action + price
            Dim action = o.Value(Of String)("action")
            If Not String.IsNullOrEmpty(action) AndAlso String.Equals(action, "delete", StringComparison.OrdinalIgnoreCase) Then
                Return Nothing
            End If
            Dim pxNullable = o.Value(Of Decimal?)("price")
            If pxNullable.HasValue Then Return pxNullable.Value
            ' Some book formats might embed price under different keys; extend here if needed
            Return Nothing
        End If

        Return Nothing
    End Function

    Private Sub UpdateFromBookPayload(payload As JObject, isSpot As Boolean)
        Try
            Dim bids = payload("bids")
            Dim asks = payload("asks")

            Dim topBid As Decimal? = Nothing
            Dim topAsk As Decimal? = Nothing

            If bids IsNot Nothing AndAlso bids.Type = JTokenType.Array AndAlso bids.Count > 0 Then
                topBid = ExtractTopPriceFromBookEntry(bids(0))
            End If
            If asks IsNot Nothing AndAlso asks.Type = JTokenType.Array AndAlso asks.Count > 0 Then
                topAsk = ExtractTopPriceFromBookEntry(asks(0))
            End If

            If isSpot Then
                If topBid.HasValue Then _mon.SpotBestBid = topBid.Value
                If topAsk.HasValue Then _mon.SpotBestAsk = topAsk.Value
            Else
                If topBid.HasValue Then _mon.WeeklyFutureBestBid = topBid.Value
                If topAsk.HasValue Then _mon.WeeklyFutureBestAsk = topAsk.Value
            End If
        Catch ex As Exception
            ' Swallow and rely on ticker.* as primary source
        End Try
    End Sub



End Class
