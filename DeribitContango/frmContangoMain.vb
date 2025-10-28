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

    ' ===== Expiry automation =====
    Private _expiryAutoEnabled As Boolean = True
    Private _expiryLeadSeconds As Integer = 60          ' arm 60s before expiry
    Private _settlementLagSeconds As Integer = 45       ' wait 45s after 08:00 UTC
    Private _expiryPollIntervalMs As Integer = 3000     ' poll settlement every 3s

    Private _expiryArmedUtc As DateTime = Date.MinValue ' expiry time currently armed
    Private _expiryWorkerCts As Threading.CancellationTokenSource = Nothing
    Private _armedInstrument As String = Nothing        ' instrument being monitored for settlement

    ' Debounce flag for signal-driven cancel
    Private _signalCancelInFlight As Boolean = False

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

            ArmNextExpiry()

            AppendLog("Initialized UI and core components.")
        Catch ex As Exception
            AppendLog("Load error: " & ex.Message)
        End Try
    End Sub


    ' Detects existing BTC dated futures + spot BTC on startup and syncs runtime.
    Private Async Function SyncPositionOnStartupAsync() As Task
        Try
            ' 1) Find a non-perpetual BTC futures position with non-zero size
            Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
            Dim chosenName As String = Nothing
            Dim signedContracts As Integer = 0

            For Each p In posArr
                Dim o = p.Value(Of JObject)()
                Dim kind = o.Value(Of String)("kind")
                Dim name = o.Value(Of String)("instrument_name")
                If String.IsNullOrEmpty(kind) OrElse String.IsNullOrEmpty(name) Then Continue For
                Dim isPerp = (name.IndexOf("PERPETUAL", StringComparison.OrdinalIgnoreCase) >= 0)
                If kind = "future" AndAlso Not isPerp Then
                    Dim k As Integer = o.Value(Of Integer?)("size").GetValueOrDefault(0)
                    If k = 0 Then
                        Dim usd = o.Value(Of Decimal?)("size_usd").GetValueOrDefault(0D)
                        If usd <> 0D Then k = CInt(Math.Truncate(usd / 10D))
                    End If
                    If k <> 0 Then
                        chosenName = name
                        signedContracts = k
                        Exit For
                    End If
                End If
            Next

            ' 2) Read BTC spot: use balance (actual BTC), ignore available_funds under portfolio margin
            Dim acct = Await _api.GetAccountSummaryAsync("BTC")
            Dim spotBtc As Decimal = acct.Value(Of Decimal?)("balance").GetValueOrDefault(0D)
            Dim awf As Decimal = acct.Value(Of Decimal?)("available_withdrawal_funds").GetValueOrDefault(spotBtc)
            ' Optional: PM hint for logs (do not use for logic)
            Dim af As Decimal = acct.Value(Of Decimal?)("available_funds").GetValueOrDefault(0D)
            If spotBtc = 0D AndAlso af > 0D Then
                AppendLog($"Startup note: portfolio margin detected (available_funds>0, BTC balance=0); treating spot BTC as 0 for basis detection.")
            End If
            ' Treat tradable spot based on actual BTC holdings (or AWF proxy), not cross-collateral AF
            Dim tradableSpot As Boolean = (Math.Max(spotBtc, awf) >= 0.0001D)

            ' 3) Apply rules and wire runtime
            If Not String.IsNullOrEmpty(chosenName) Then
                If Not tradableSpot Then
                    AppendLog($"Startup error: futures position detected ({chosenName}, size={signedContracts}) but spot BTC < 0.0001; manual intervention required.")
                    _pm.SetActiveFromExternal(False)
                    btnEnter.Enabled = True
                    Return
                End If

                _pm.FuturesInstrument = chosenName
                _mon.WeeklyInstrument = chosenName

                Await _pm.RefreshInstrumentSpecsAsync()
                _mon.WeeklyExpiryUtc = _pm.ExpiryUtc

                _pm.SetActiveFromExternal(True)

                ' Initialize the spot hedge amount for close monitor
                _pm.InitializeSpotHedgeAmount(Math.Max(spotBtc, awf))
                _pm.InitializeFuturesPosition(signedContracts)


                Await _api.SubscribePublicAsync({
    $"ticker.{_pm.FuturesInstrument}.100ms",
    $"book.{_pm.FuturesInstrument}.100ms"
})

                UpdateExpiryLabels()
                ArmNextExpiry()

                AppendLog($"Redetected active basis: futures={_pm.FuturesInstrument} (contracts={signedContracts}), spot={Math.Max(spotBtc, awf):0.00000000} BTC; expiry automation armed.")


                ' Rebuild entry basis from trade history on restart if we have an active basis trade
                Try
                    Dim hasActiveFutures = Await DetectHasActiveFuturesAsync()
                    Dim spotBtcBal = Await GetSpotBtcBalanceAsync()

                    ' Treat spot >= min trade amount as “spot leg present”
                    Dim minSpot As Decimal = 0.0001D

                    If hasActiveFutures AndAlso spotBtcBal >= minSpot Then
                        Await _pm.CalculateEntryBasisFromTradesAsync()
                    End If
                Catch
                    ' Swallow: not critical to block UI
                End Try


                btnEnter.Enabled = False
                _entryWatchRunning = False
                _entryWatchAttempted = False
            Else
                If tradableSpot Then
                    AppendLog($"Startup note: no futures position, but spot={Math.Max(spotBtc, awf):0.00000000} BTC remains; not an active basis trade.")
                Else
                    AppendLog("Startup: no futures position and spot < 0.0001 BTC; idle and ready.")
                End If
                _pm.SetActiveFromExternal(False)
                btnEnter.Enabled = True
            End If

        Catch ex As Exception
            AppendLog("Startup position redetect error: " & ex.Message)
        End Try
    End Function


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
        'numThreshold.Enabled = Not active
        numSlippageBps.Enabled = Not active
        numRequoteTicks.Enabled = Not active
        numRequoteMs.Enabled = Not active

        ' Optional visual cue
        btnEnter.BackColor = If(active, Color.LightGray, Color.LightSkyBlue)
        AppendLog(If(active, "Entry disabled: position cycle active.", "Entry enabled: no active position."))
    End Sub

    Private Async Function DetectHasActiveFuturesAsync() As Task(Of Boolean)
        Try
            ' Read Deribit positions and check selected weekly instrument
            Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
            If posArr Is Nothing OrElse posArr.Count = 0 Then Return False

            For Each p In posArr
                Dim o = p.Value(Of JObject)()
                Dim instr = o.Value(Of String)("instrument_name")
                If String.Equals(instr, _pm.FuturesInstrument, StringComparison.OrdinalIgnoreCase) Then
                    Dim sz As Integer = o.Value(Of Integer)("size")
                    If sz <> 0 Then
                        Return True
                    End If
                End If
            Next
            Return False
        Catch
            Return False
        End Try
    End Function

    Private Async Function GetSpotBtcBalanceAsync() As Task(Of Decimal)
        Try
            ' Uses private/get_account_summary extended = true
            Dim acct = Await _api.GetAccountSummaryAsync("BTC")
            If acct Is Nothing Then Return 0D

            ' Prefer available balance if exposed, else fallback to balance
            Dim avail = acct.Value(Of Decimal?)("available_funds").GetValueOrDefault(0D)
            Dim balance = acct.Value(Of Decimal?)("balance").GetValueOrDefault(0D)

            ' If available funds looks zero (due to margin context), use balance for spot
            Dim spot = If(avail > 0D, avail, balance)

            ' If your UI/monitor already tracks spot base balance, you can replace this with that
            Return Math.Max(spot, 0D)
        Catch
            Return 0D
        End Try
    End Function


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

            ' NEW: perform startup position re-detection and sync
            Await SyncPositionOnStartupAsync()

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
            ArmNextExpiry()
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

            ' 1) Close current and select next weekly
            Await _pm.RollToNextWeeklyAsync(_mon.IndexPriceUsd)
            AppendLog("Requested roll: closed current via reduce_only and selected next weekly.")

            ' 2) Wire new weekly feeds and specs
            If Not String.IsNullOrEmpty(_pm.FuturesInstrument) Then
                Await _api.SubscribePublicAsync({
        $"ticker.{_pm.FuturesInstrument}.100ms",
        $"book.{_pm.FuturesInstrument}.100ms"
      })
                Await _pm.RefreshInstrumentSpecsAsync()
                UpdateExpiryLabels()
                ArmNextExpiry()
            End If

            ' 3) Auto-entry after roll (gated by checkbox)
            If chkAutoEnterAfterRoll.Checked Then
                If String.IsNullOrEmpty(_pm.FuturesInstrument) Then
                    Throw New ApplicationException("Weekly future not selected after roll.")
                End If

                _pm.UseUsdInput = radUSD.Checked
                _pm.MaxSlippageBps = numSlippageBps.Value
                _pm.EntryThreshold = CDec(numThreshold.Value) / 100D    ' percent → fraction

                ' Validate and normalize amount into PM targets
                Dim amt As Decimal
                If Not Decimal.TryParse(txtAmount.Text.Trim(), amt) OrElse amt <= 0D Then
                    Throw New ApplicationException("Invalid amount.")
                End If

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

                ' Optional: clear pre-roll samples so median reflects the new weekly only
                _basisBuf.Clear()

                ' Compute current rolling median and decide immediate vs. watch
                Dim medPct As Decimal = RollingMedianBasisPct()
                Dim thresholdPct As Decimal = CDec(numThreshold.Value)

                If medPct >= thresholdPct Then
                    AppendLog($"Auto-entry after roll: median basis={medPct:0.00}% >= {thresholdPct:0.00}%. Executing now...")
                    ' Execute immediately on UI thread
                    If InvokeRequired Then
                        BeginInvoke(Async Sub() Await ExecuteEnterNowAsync())
                    Else
                        Await ExecuteEnterNowAsync()
                    End If
                Else
                    ' Arm the watcher to trigger on sustained basis
                    StartEntryWatch()
                    AppendLog($"Auto-entry armed after roll: watching median basis >= {thresholdPct:0.00}% (current={medPct:0.00}%).")
                End If
            Else
                AppendLog("Auto-entry after roll is disabled (checkbox off).")
            End If


        Catch ex As Exception
            AppendLog("Roll error: " & ex.Message)
        End Try
    End Sub

    Private Async Sub btnCloseAll_Click(sender As Object, e As EventArgs) Handles btnCloseAll.Click
        Try
            Await _pm.CloseAllAsync()
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

            ' Keep the median label live and compute rolling median
            Dim samplePct As Decimal = _mon.BasisMid * 100D
            _basisBuf.Enqueue(samplePct)
            Dim medPct As Decimal = RollingMedianBasisPct()
            lblMedianBasis.Text = $"{medPct:0.00}%"

            ' ========== Signal-driven watchdog: cancel only when median < threshold ==========
            If _pm IsNot Nothing AndAlso _pm.HasLiveEntryOrder Then
                Dim thresholdPct As Decimal = CDec(numThreshold.Value)
                If medPct < thresholdPct AndAlso Not _signalCancelInFlight Then
                    _signalCancelInFlight = True
                    AppendLog($"Watchdog: median basis={medPct:0.00}% < threshold={thresholdPct:0.00}%. Cancelling entry and resuming basis watch...")

                    ' Cancel on a worker, then resume watch after PM reports inactive
                    Call Task.Run(Async Function()
                                      Try
                                          Await _pm.CancelEntryDueToSignalAsync("median basis below threshold")
                                      Catch
                                          ' swallow
                                      End Try
                                      BeginInvoke(Sub()
                                                      _signalCancelInFlight = False
                                                      If Not _pm.IsActive Then
                                                          StartEntryWatch()
                                                      End If
                                                  End Sub)
                                  End Function)

                End If
            End If
            ' ================================================================================

            If _expiryAutoEnabled Then
                If _expiryArmedUtc = Date.MinValue Then ArmNextExpiry()
                Dim nowUtc = DateTime.UtcNow
                If nowUtc >= _expiryArmedUtc.AddSeconds(_settlementLagSeconds) Then
                    If _expiryWorkerCts Is Nothing OrElse _expiryWorkerCts.IsCancellationRequested Then
                        _expiryWorkerCts = New Threading.CancellationTokenSource()
                        AppendLog($"Expiry window reached for {_expiryArmedUtc:yyyy-MM-dd HH:mm:ss} UTC; starting settlement poll...")
                        Call Task.Run(Function() ExpirySettlementWorkerAsync(_expiryWorkerCts.Token))
                    End If
                End If
            End If

            UpdatePositionDisplay()

            ' Force UI refresh when the position just became inactive
            If _pm IsNot Nothing AndAlso (Not _pm.IsActive) AndAlso _pm.HasEntryBasisData Then
                ' Ensure entry-basis area clears as soon as position ends
                UpdatePositionDisplay()
            End If

        Catch
        End Try
    End Sub

    Private Sub UpdatePositionDisplay()
        Try
            ' Show position details if PM is active OR if there's an actual position
            If _pm.IsActive OrElse _pm.CurrentFuturesUsdNotional <> 0D OrElse _pm.CurrentSpotHedgeAmount > 0D Then
                ' Show spot BTC amount
                lblSpotBTCValue.Text = _pm.CurrentSpotHedgeAmount.ToString("0.00000000")

                ' Show spot USD value
                Dim spotUsdValue As Decimal = _pm.CurrentSpotHedgeAmount * _mon.IndexPriceUsd
                lblSpotUSDValue.Text = spotUsdValue.ToString("0.00")

                ' Show futures USD value
                lblFuturesUSDValue.Text = _pm.CurrentFuturesUsdNotional.ToString("0.00")

                ' Show instrument name
                lblInstrumentValue.Text = _pm.FuturesInstrument

                ' Entry basis and P&L tracking
                If _pm.HasEntryBasisData Then
                    lblEntryBasis.Text = $"{_pm.EntryBasisPercent:P2}"

                    Dim pnlUsd = _pm.EstimatedPnlUsd
                    Dim pnlPct = _pm.EstimatedPnlPercent
                    Dim pnlColor As Color = If(pnlPct >= 0, Color.Green, Color.Red)

                    lblEstimatedPnl.Text = $"${pnlUsd:+0.00;-0.00} ({pnlPct:+0.00%;-0.00%})"
                    lblEstimatedPnl.ForeColor = pnlColor
                Else
                    lblEntryBasis.Text = "-"
                    lblEstimatedPnl.Text = "-"
                    lblEstimatedPnl.ForeColor = Color.Black
                End If
            Else
                ' Clear display when no active position
                lblSpotBTCValue.Text = "-"
                lblSpotUSDValue.Text = "-"
                lblFuturesUSDValue.Text = "-"
                lblInstrumentValue.Text = "-"
                lblEntryBasis.Text = "-"
                lblEstimatedPnl.Text = "-"

            End If
        Catch
            ' Swallow display errors
        End Try
    End Sub



    Private Async Function ExpirySettlementWorkerAsync(ct As Threading.CancellationToken) As Task
        Try
            ' 1) Poll for settlement of the armed instrument (if any)
            Dim settled As Boolean = False
            Dim lastChecked As DateTime = DateTime.MinValue

            Do While Not ct.IsCancellationRequested AndAlso Not settled
                Try
                    Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
                    Dim foundCur As Boolean = False
                    Dim sizeUsd As Integer = 0

                    For Each p In posArr
                        Dim o = p.Value(Of JObject)()
                        Dim name = o.Value(Of String)("instrument_name")
                        If Not String.IsNullOrEmpty(_armedInstrument) AndAlso String.Equals(name, _armedInstrument, StringComparison.OrdinalIgnoreCase) Then
                            foundCur = True
                            sizeUsd = Math.Abs(o.Value(Of Integer)("size")) ' inverse futures size ~ USD notional
                            Exit For
                        End If
                    Next

                    ' Settled if the armed instrument is gone or its size is zero
                    settled = (Not foundCur) OrElse (sizeUsd = 0)

                Catch
                    ' network/API hiccup; keep polling
                End Try

                If Not settled Then
                    Try
                        Await Task.Delay(_expiryPollIntervalMs, ct)
                    Catch
                        Exit Do
                    End Try
                End If
            Loop

            If ct.IsCancellationRequested Then Return

            ' 2) Discover the new nearest weekly (post-expiry this is next week)
            Try
                Await _pm.DiscoverNearestWeeklyAsync() ' no cutoff -> nearest non-expired is next weekly now
                AppendLog($"Auto-discovered weekly after expiry: {_pm.FuturesInstrument}")

                ' Clear position data and deactivate PM since old position settled
                _pm.ClearPositionData()
                _pm.SetActiveFromExternal(False)

                ' Subscribe to market streams for the new weekly and refresh specs/labels
                Await _api.SubscribePublicAsync({
        $"ticker.{_pm.FuturesInstrument}.100ms",
        $"book.{_pm.FuturesInstrument}.100ms"
    })
                Await _pm.RefreshInstrumentSpecsAsync()
                If InvokeRequired Then
                    BeginInvoke(Sub() UpdateExpiryLabels())
                Else
                    UpdateExpiryLabels()
                End If
            Catch ex As Exception
                AppendLog("Auto-discover error post-expiry: " & ex.Message)
                ' Even on discover failure, re-arm to avoid getting stuck
            End Try



            ' 3) Start the basis watch (same flow as clicking Enter)
            If InvokeRequired Then
                BeginInvoke(Sub()
                                If Not _pm.IsActive Then
                                    AppendLog("Post-expiry auto-entry: triggering basis watch for new weekly.")
                                    btnEnter.PerformClick()
                                Else
                                    AppendLog("Post-expiry auto-entry blocked: PM still shows active (settlement detection may be delayed).")
                                End If
                            End Sub)
            Else
                If Not _pm.IsActive Then
                    AppendLog("Post-expiry auto-entry: triggering basis watch for new weekly.")
                    btnEnter.PerformClick()
                Else
                    AppendLog("Post-expiry auto-entry blocked: PM still shows active (settlement detection may be delayed).")
                End If
            End If


        Catch
            ' swallow worker exception; will re-arm below
        Finally
            ' 4) Re-arm for the next weekly expiry
            _expiryWorkerCts?.Cancel()
            _expiryWorkerCts = Nothing
            _expiryArmedUtc = Date.MinValue
            _armedInstrument = _pm.FuturesInstrument
            ArmNextExpiry()
        End Try
    End Function


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
    ' Arms the entry watcher; runs until an order is actually placed or PM becomes active.
    Private Sub StartEntryWatch()
        If _entryWatchRunning Then Return
        _entryWatchCts = New Threading.CancellationTokenSource()
        _entryWatchRunning = True
        _entryWatchAttempted = False

        Dim thresholdPct As Decimal = CDec(numThreshold.Value)
        AppendLog($"Entry watch started: threshold={thresholdPct:0.##}% (comparing to lblBasis).")

        ' VB.NET: do not use "_ ="; either Call or assign to a named variable.
        Call Task.Run(Function() EntryWatchLoopAsync(_entryWatchCts.Token))
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

    ' Watches rolling-median basis and attempts entry when median >= threshold.
    ' IMPORTANT: Do not stop the watcher unless a futures order is live or PM becomes active.
    Private Async Function EntryWatchLoopAsync(ct As Threading.CancellationToken) As Task
        Try
            Dim thresholdPct As Decimal = CDec(numThreshold.Value)
            Dim sampleMs As Integer = _basisSampleMs

            While Not ct.IsCancellationRequested
                If _pm IsNot Nothing AndAlso (_pm.IsActive OrElse _pm.HasLiveEntryOrder) Then
                    Exit While
                End If

                Dim medPct As Decimal = RollingMedianBasisPct()

                If medPct >= thresholdPct Then
                    AppendLog($"Entry condition met: median basis={medPct:0.00}% >= {thresholdPct:0.00}%. Attempting entry...")
                    _entryWatchAttempted = True

                    Await ExecuteEnterNowAsync()

                    If _pm IsNot Nothing AndAlso (_pm.IsActive OrElse _pm.HasLiveEntryOrder) Then
                        Exit While
                    Else
                        Await Task.Delay(Math.Max(sampleMs, 250), ct)
                        Continue While
                    End If
                End If

                Await Task.Delay(sampleMs, ct)
            End While

        Catch ex As TaskCanceledException
            ' normal
        Catch ex As Exception
            AppendLog("Entry watch error: " & ex.Message)
        Finally
            _entryWatchRunning = False
            _entryWatchCts = Nothing
            If _pm IsNot Nothing AndAlso (_pm.IsActive OrElse _pm.HasLiveEntryOrder) Then
                AppendLog("Entry watch stopped: order live or cycle active.")
            Else
                AppendLog("Entry watch stopped: idle.")
            End If
        End Try
    End Function






    ' Extracted from previous btnEnter_Click: actually sends the futures order
    ' Attempts to place the futures leg; if pre-order validation fails, auto-continue the watcher.
    Private Async Function ExecuteEnterNowAsync() As Task
        Try
            If _pm Is Nothing Then
                AppendLog("Enter blocked: PM not initialized.")
                Return
            End If

            If _pm.IsActive OrElse _pm.HasLiveEntryOrder Then
                AppendLog("Entry blocked: active position in progress.")
                Return
            End If

            ' Configure PM from UI
            _pm.EntryThreshold = CDec(numThreshold.Value) / 100D
            _pm.MaxSlippageBps = numSlippageBps.Value
            _pm.UseUsdInput = radUSD.Checked

            ' Thread-safe UI update
            If InvokeRequired Then
                BeginInvoke(Sub() btnEnter.Enabled = False)
            Else
                btnEnter.Enabled = False
            End If

            ' Attempt to enter; PM will validate instantaneous BasisMid vs EntryThreshold
            Await _pm.EnterBasisAsync(_mon.IndexPriceUsd, _mon.WeeklyFutureBestBid, _mon.SpotBestAsk)

            ' If we reach here without exception, PM either placed an order or marked active
            If _pm.IsActive OrElse _pm.HasLiveEntryOrder Then
                AppendLog("Entry disabled: position cycle active.")
            End If

        Catch ex As Exception
            ' Pre-order failures (e.g., BasisMid dipped below threshold) land here
            AppendLog($"Enter error: {ex.Message}")

            ' Auto-continue the watcher when no order was placed and PM is still inactive
            If (_pm IsNot Nothing) AndAlso (Not _pm.IsActive) AndAlso (Not _pm.HasLiveEntryOrder) Then
                ' If the watcher was stopped by prior logic, re-arm it
                If Not _entryWatchRunning Then
                    Dim thresholdPct As Decimal = CDec(numThreshold.Value)
                    AppendLog($"Entry watch auto-resume: threshold={thresholdPct:0.##}%.")
                    StartEntryWatch()
                End If
            End If

        Finally
            ' Thread-safe UI update - re-enable enter button only if no active cycle/order
            If InvokeRequired Then
                BeginInvoke(Sub()
                                If _pm Is Nothing OrElse (Not _pm.IsActive AndAlso Not _pm.HasLiveEntryOrder) Then
                                    btnEnter.Enabled = True
                                End If
                            End Sub)
            Else
                If _pm Is Nothing OrElse (Not _pm.IsActive AndAlso Not _pm.HasLiveEntryOrder) Then
                    btnEnter.Enabled = True
                End If
            End If
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


    Private Sub ArmNextExpiry()
        ' If a weekly is selected, arm its ExpiryUtc; otherwise arm the upcoming Friday 08:00 UTC
        If Not String.IsNullOrEmpty(_pm.FuturesInstrument) AndAlso _pm.ExpiryUtc > Date.MinValue Then
            _expiryArmedUtc = _pm.ExpiryUtc
            _armedInstrument = _pm.FuturesInstrument
        Else
            _expiryArmedUtc = DeribitContango.ContangoBasisMonitor.NextWeeklyExpiryUtc(DateTime.UtcNow)
            _armedInstrument = Nothing
        End If
        AppendLog($"Expiry automation armed for: {_expiryArmedUtc:yyyy-MM-dd HH:mm:ss} UTC")
    End Sub


End Class
