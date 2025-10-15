Imports System.Text
Imports Newtonsoft.Json.Linq

Public Class frmContangoMain

    Private _db As DeribitContango.ContangoDatabase
    Private _rate As DeribitContango.DeribitRateLimiter
    Private _api As DeribitContango.DeribitApiClient
    Private _mon As DeribitContango.ContangoBasisMonitor
    Private _pm As DeribitContango.ContangoPositionManager

    Private _uiTimer As System.Windows.Forms.Timer

    ' Entry watch toggle
    Private _entryWatchCts As Threading.CancellationTokenSource = Nothing
    Private _entryWatchRunning As Boolean = False
    Private _entryWatchAttempted As Boolean = False

    ' Rolling median basis buffer (percent units like lblBasis)
    Private ReadOnly _basisBuf As New Queue(Of Decimal)()
    Private _basisWindowMs As Integer = 3000   ' window length, e.g., last 3 seconds
    Private _basisSampleMs As Integer = 100    ' sample cadence from UI tick / watcher


    Private Sub frmContangoMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            _db = New DeribitContango.ContangoDatabase("contango.db3")
            _rate = New DeribitContango.DeribitRateLimiter(20)
            _api = New DeribitContango.DeribitApiClient(_rate)
            _mon = New DeribitContango.ContangoBasisMonitor()
            _pm = New DeribitContango.ContangoPositionManager(_api, _db, _mon)

            AddHandler _pm.ActiveChanged, AddressOf OnPmActiveChanged
            OnPmActiveChanged(_pm.IsActive)

            AddHandler _api.ConnectionStateChanged, AddressOf OnConnState
            AddHandler _api.PublicMessage, AddressOf OnPublicMessage
            AddHandler _api.OrderUpdate, AddressOf OnOrderUpdate
            AddHandler _api.TradeUpdate, AddressOf OnTradeUpdate
            AddHandler _pm.Info, Sub(m) AppendLog(m)

            ' UI ← PM defaults
            numRequoteTicks.Value = _pm.RequoteMinTicks
            numRequoteMs.Value = _pm.RequoteIntervalMs

            _uiTimer = New System.Windows.Forms.Timer()
            _uiTimer.Interval = 1000
            AddHandler _uiTimer.Tick, AddressOf OnUiTick
            _uiTimer.Start()

            radUSD.Checked = True

            ' IMPORTANT: show EntryThreshold as percent
            numThreshold.Value = CDec(_pm.EntryThreshold * 100D)

            numSlippageBps.Value = CDec(_pm.MaxSlippageBps)

            ' Rolling median UI defaults and clamps
            numBasisWindowMs.Minimum = 200
            numBasisWindowMs.Maximum = 20000
            numBasisWindowMs.Increment = 100
            numBasisWindowMs.Value = _basisWindowMs

            numBasisSampleMs.Minimum = 20
            numBasisSampleMs.Maximum = 500
            numBasisSampleMs.Increment = 10
            numBasisSampleMs.Value = _basisSampleMs


            AppendLog("Initialized UI and core components.")
        Catch ex As Exception
            AppendLog("Load error: " & ex.Message)
        End Try
    End Sub


    ' 2) Add this handler in frmContangoMain:
    Private Sub OnPmActiveChanged(active As Boolean)
        If InvokeRequired Then
            BeginInvoke(Sub() OnPmActiveChanged(active))
            Return
        End If

        ' Disable/enable actionable entry controls during an active position
        btnEnter.Enabled = Not active
        txtAmount.Enabled = Not active
        radUSD.Enabled = Not active
        radBTC.Enabled = Not active
        numThreshold.Enabled = Not active
        numSlippageBps.Enabled = Not active
        numRequoteTicks.Enabled = Not active
        numRequoteMs.Enabled = Not active

        ' Optional visual cue
        btnEnter.BackColor = If(active, Color.LightGray, Color.LightSkyBlue)
        AppendLog(If(active, "Entry disabled: position cycle active.", "Entry enabled: no active position."))
    End Sub



    Private Sub numRequoteMs_ValueChanged(sender As Object, e As EventArgs) Handles numRequoteMs.ValueChanged
        If _pm Is Nothing Then Exit Sub
        _pm.RequoteIntervalMs = CInt(numRequoteMs.Value)
    End Sub

    Private Sub numRequoteTicks_ValueChanged(sender As Object, e As EventArgs) Handles numRequoteTicks.ValueChanged
        If _pm Is Nothing Then Exit Sub
        _pm.RequoteMinTicks = CInt(numRequoteTicks.Value)
    End Sub


    Private Sub numBasisWindowMs_ValueChanged(sender As Object, e As EventArgs) Handles numBasisWindowMs.ValueChanged
        _basisWindowMs = Math.Max(100, Math.Min(20000, CInt(numBasisWindowMs.Value)))
        AppendLog($"Basis window set to {_basisWindowMs} ms (rolling median).")
    End Sub

    Private Sub numBasisSampleMs_ValueChanged(sender As Object, e As EventArgs) Handles numBasisSampleMs.ValueChanged
        _basisSampleMs = Math.Max(20, Math.Min(500, CInt(numBasisSampleMs.Value)))
        AppendLog($"Basis sample cadence set to {_basisSampleMs} ms.")
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

    ' Toggle basis watch on Enter button without async/await
    Private Sub btnEnter_Click(sender As Object, e As EventArgs) Handles btnEnter.Click
        Try
            ' If an entry is in progress, block
            If _pm.IsActive Then
                AppendLog("Entry blocked: active position in progress.")
                Return
            End If

            ' Toggle: if watch is running and no attempt yet, stop it
            If _entryWatchRunning AndAlso Not _entryWatchAttempted Then
                StopEntryWatch("Operator stopped watching.")
                Return
            End If

            ' Start watching basis >= threshold (percent)
            _pm.UseUsdInput = radUSD.Checked
            _pm.MaxSlippageBps = numSlippageBps.Value
            _pm.EntryThreshold = CDec(numThreshold.Value) / 100D    ' percent → fraction

            ' Validate amount now to fail fast, but do not place any orders yet
            Dim amt As Decimal
            If Not Decimal.TryParse(txtAmount.Text.Trim(), amt) OrElse amt <= 0D Then
                Throw New ApplicationException("Invalid amount.")
            End If

            ' Normalize preview and store to PM
            If _pm.UseUsdInput Then
                Dim futRef = If(_mon.WeeklyFutureBestBid > 0D, _mon.WeeklyFutureBestBid, _mon.WeeklyFutureMark)
                Dim minUsd = _pm.MinUsdForOneSpotStep(futRef)
                Dim roundedUsd = CDec(Math.Ceiling(Math.Max(amt, minUsd) / 10D) * 10D)
                If roundedUsd <> amt Then
                    AppendLog($"Amount USD adjusted from {amt:0} to {roundedUsd:0} to satisfy 10-USD granularity and one-spot-step minimum")
                    amt = roundedUsd
                    txtAmount.Text = amt.ToString("0")
                End If
                _pm.TargetUsd = amt
                _pm.TargetBtc = 0D
            Else
                Dim stepv = _pm.SpotAmountStep
                Dim minv = _pm.SpotMinAmount
                Dim steps = CDec(Math.Round(Math.Max(amt, minv) / stepv, MidpointRounding.AwayFromZero))
                Dim snapped = steps * stepv
                If snapped <> amt Then
                    AppendLog($"Amount BTC adjusted from {amt:0.########} to {snapped:0.########} to satisfy spot increment")
                    amt = snapped
                    txtAmount.Text = amt.ToString("0.########")
                End If
                _pm.TargetBtc = amt
                _pm.TargetUsd = 0D
            End If

            If String.IsNullOrEmpty(_pm.FuturesInstrument) Then
                Throw New ApplicationException("Weekly future not selected. Click Discover Weekly first.")
            End If

            StartEntryWatch()
        Catch ex As Exception
            AppendLog("Enter error: " & ex.Message)
        End Try
    End Sub



    Private Async Sub btnRoll_Click(sender As Object, e As EventArgs) Handles btnRoll.Click
        Try
            If _pm.IsActive Then
                AppendLog("Roll blocked: active position in progress.")
                Return
            End If
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
        Dim oid = payload.Value(Of String)("order_id")
        Dim instr = payload.Value(Of String)("instrument_name")
        Dim side = payload.Value(Of String)("direction")
        If String.IsNullOrEmpty(side) Then side = payload.Value(Of String)("side")
        Dim px As Decimal = payload.Value(Of Decimal?)("price").GetValueOrDefault(0D)

        Dim amtStr As String = ""
        Dim contracts = payload.Value(Of Integer?)("contracts").GetValueOrDefault(0)
        Dim amount = payload.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
        If contracts > 0 Then
            amtStr = $"contracts={contracts}"
        ElseIf amount > 0D Then
            amtStr = $"amount={amount:0.########}"
        End If

        AppendLog($"ORDER {currency} id={oid} state={st} {instr} {side} px={If(px > 0D, px.ToString("0.00"), "-")} {amtStr}")
    End Sub

    Private Sub OnTradeUpdate(currency As String, payload As JObject)
        Try
            Dim instr = payload.Value(Of String)("instrument_name")
            Dim trades = payload("trades")
            If trades Is Nothing OrElse trades.Type <> JTokenType.Array OrElse trades.Count = 0 Then Return

            For Each t In trades
                Dim sideStr = t.Value(Of String)("side")
                If String.IsNullOrEmpty(sideStr) Then sideStr = t.Value(Of String)("direction")

                Dim px As Decimal = t.Value(Of Decimal?)("price").GetValueOrDefault(0D)

                ' Prefer explicit contracts for futures; otherwise derive from amount (USD) / 10
                Dim contracts = t.Value(Of Integer?)("contracts").GetValueOrDefault(0)
                If contracts = 0 Then
                    Dim amtUsd = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                    If amtUsd > 0D Then
                        contracts = CInt(Math.Round(amtUsd / 10D, MidpointRounding.AwayFromZero))
                    End If
                End If

                ' For spot fills, show BTC amount; for futures, show contracts
                Dim amtBtc As Decimal = 0D
                If contracts = 0 Then
                    ' Likely a spot trade; Deribit uses "amount" as base quantity
                    amtBtc = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                End If

                Dim idStr = t.Value(Of String)("trade_id")
                Dim sideDisp = If(String.IsNullOrEmpty(sideStr), "-", sideStr)
                Dim pxDisp = If(px > 0D, px.ToString("0.00"), "-")

                If contracts > 0 Then
                    AppendLog($"TRADE {currency} {instr} {sideDisp} px={pxDisp} contracts={contracts} id={idStr}")
                Else
                    AppendLog($"TRADE {currency} {instr} {sideDisp} px={pxDisp} amount={amtBtc:0.########} id={idStr}")
                End If
            Next

        Catch ex As Exception
            AppendLog("Trade log error: " & ex.Message)
        End Try
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

            Dim futRef = If(_mon.WeeklyFutureBestBid > 0D, _mon.WeeklyFutureBestBid, _mon.WeeklyFutureMark)
            lblMinUSD.Text = $"Min USD for 1 step: {_pm.MinUsdForOneSpotStep(futRef):0}"

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

            ' Keep the median label live:
            Dim samplePct As Decimal = _mon.BasisMid * 100D
            _basisBuf.Enqueue(samplePct)
            Dim medPct As Decimal = RollingMedianBasisPct()

            lblMedianBasis.Text = $"{medPct:0.00}%"

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

    'For state watch toggle
    Private Sub StartEntryWatch()
        Try
            StopEntryWatch() ' idempotent
            _entryWatchAttempted = False
            _entryWatchCts = New Threading.CancellationTokenSource()
            _entryWatchRunning = True
            btnEnter.BackColor = Color.LightGreen
            AppendLog($"Entry watch started: threshold={numThreshold.Value:0.####}% (comparing to lblBasis).")
            Task.Run(Function() EntryWatchLoopAsync(_entryWatchCts.Token))
        Catch ex As Exception
            AppendLog("Entry watch start error: " & ex.Message)
            _entryWatchRunning = False
            btnEnter.BackColor = Color.LightSkyBlue
        End Try
    End Sub

    Private Sub StopEntryWatch(Optional reason As String = Nothing)
        Try
            _entryWatchCts?.Cancel()
        Catch
        End Try
        _entryWatchRunning = False
        btnEnter.BackColor = Color.LightSkyBlue
        If Not String.IsNullOrEmpty(reason) Then
            AppendLog("Entry watch stopped: " & reason)
        End If
    End Sub

    Private Async Function EntryWatchLoopAsync(ct As Threading.CancellationToken) As Task
        Dim thresholdPct As Decimal = CDec(numThreshold.Value)
        Dim cadence As Integer = Math.Max(50, _basisSampleMs)

        While Not ct.IsCancellationRequested
            Try
                If _pm.IsActive Then
                    StopEntryWatch("position became active.")
                    Exit While
                End If

                ' Sample basis as percent and enqueue
                Dim samplePct As Decimal = _mon.BasisMid * 100D
                _basisBuf.Enqueue(samplePct)

                ' Compute rolling median over window
                Dim medPct As Decimal = RollingMedianBasisPct()

                ' NEW: show median in the label
                If InvokeRequired Then
                    BeginInvoke(Sub() lblMedianBasis.Text = $"{medPct:0.00}%")
                Else
                    lblMedianBasis.Text = $"{medPct:0.00}%"
                End If

                If medPct >= thresholdPct Then
                    _entryWatchAttempted = True
                    AppendLog($"Entry condition met: median basis={medPct:0.00}% >= {thresholdPct:0.00}%. Attempting entry...")
                    If InvokeRequired Then
                        BeginInvoke(Async Sub() Await ExecuteEnterNowAsync())
                    Else
                        Await ExecuteEnterNowAsync()
                    End If
                    StopEntryWatch("attempted entry.")
                    Exit While
                End If
            Catch ex As TaskCanceledException
                Exit While
            Catch
                ' keep watching
            End Try

            Try
                Await Task.Delay(cadence, ct)
            Catch
                Exit While
            End Try
        End While
    End Function



    ' Extracted from previous btnEnter_Click: actually sends the futures order
    Private Async Function ExecuteEnterNowAsync() As Task
        Try
            If _pm.IsActive Then
                AppendLog("Entry blocked: active position in progress.")
                Return
            End If

            ' Ensure PM thresholds reflect percent input
            _pm.EntryThreshold = CDec(numThreshold.Value) / 100D
            _pm.MaxSlippageBps = numSlippageBps.Value
            _pm.UseUsdInput = radUSD.Checked

            btnEnter.Enabled = False  ' disable during send

            ' Futures first, spot on fills
            Await _pm.EnterBasisAsync(_mon.IndexPriceUsd, _mon.WeeklyFutureBestBid, _mon.SpotBestAsk)
            AppendLog("Submitted futures post_only; re-quote active until fills trigger spot IOC hedges.")
        Catch ex As Exception
            AppendLog("Enter error: " & ex.Message)
        Finally
            ' Re-enable only if PM did not activate (otherwise OnPmActiveChanged handles it)
            If Not _pm.IsActive Then btnEnter.Enabled = True
        End Try
    End Function

    ' Basis samples pushed by watcher carry only value; time window enforced by queue length from cadence × window
    ' Derive max buffer length from window/cadence
    Private Function RollingMedianBasisPct() As Decimal
        ' Enforce max buffer length derived from window/cadence
        Dim maxLen As Integer = Math.Max(1, CInt(Math.Round(_basisWindowMs / Math.Max(1, _basisSampleMs), MidpointRounding.AwayFromZero)))
        While _basisBuf.Count > maxLen
            _basisBuf.Dequeue()
        End While
        If _basisBuf.Count = 0 Then Return 0D
        Dim arr = _basisBuf.ToArray()
        Array.Sort(arr)
        Dim n = arr.Length
        Dim mid = n \ 2
        If (n And 1) = 1 Then
            Return arr(mid)
        Else
            Return (arr(mid - 1) + arr(mid)) / 2D
        End If
    End Function



End Class
