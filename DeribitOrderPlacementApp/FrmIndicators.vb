
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Net.WebSockets
Imports System.Text
Imports System.Threading
Imports System.Timers
'Imports System.Windows.Forms.VisualStyles.VisualStyleElement
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Skender
Imports Skender.Stock
Imports Skender.Stock.Indicators
'Imports Windows.Win32.Storage
'Imports Windows.Win32.System

Public Class FrmIndicators
    Private ReadOnly _host As Form          ' reference to frmMainPageV2
    Private client As ClientWebSocket
    Private Shared ohlcList As New List(Of Quote)()
    Private Const DeribitUrl As String = "wss://www.deribit.com/ws/api/v2"
    Private lastTimestamp As Long
    'Private pollTimer As New Timers.Timer(60000) ' 60 000 ms = 1 minute
    Private pollTimer As New Timers.Timer(5000) ' 5-second intervals
    Private heartbeatTimer As New System.Windows.Forms.Timer() With {.Interval = 500, .Enabled = False}
    Private score As Integer = 0
    Private startupFired As Boolean = False
    Private formLoadTimestamp As DateTime = DateTime.MinValue
    Private formLoadOHLCIndex As Integer = -1

    Private _autoTradeSettings As AutoTradeSettings  ' Reference to settings form

    Public Sub New(host As Form)
        InitializeComponent()               ' designer code
        _host = host
    End Sub

    Public ReadOnly Property IsAutoTradingEnabled As Boolean
        Get
            Return enableAutoTrading
        End Get
    End Property


    ' ── Form Load ───────────────────────────────────────────────────────────────
    Private Sub FrmIndicators_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        _autoTradeSettings = New AutoTradeSettings(Me)

        AddHandler heartbeatTimer.Tick, AddressOf heartbeatTimer_Tick
        pollTimer.AutoReset = True
        AddHandler pollTimer.Elapsed, AddressOf OnPollElapsed

        Task.Run(AddressOf ConnectAndStream)

        formLoadTimestamp = DateTime.UtcNow
        formLoadOHLCIndex = ohlcList.Count   ' next bar that arrives

        'Form positioning
        StickToHost()                           ' first positioning
        AddHandler _host.LocationChanged, AddressOf HostMovedOrResized
        AddHandler _host.SizeChanged, AddressOf HostMovedOrResized

        AUTO_TRADE_COOLDOWN_MS = 60000 * Integer.Parse(_autoTradeSettings.txtCooloff.Text) ' x minutes between trades
    End Sub

    Private Sub HostMovedOrResized(sender As Object, e As EventArgs)
        StickToHost()
    End Sub

    Private Sub StickToHost()
        If _host Is Nothing OrElse _host.IsDisposed Then Return

        ' place Indicators just outside the host’s right border, aligned to top
        Me.StartPosition = FormStartPosition.Manual
        Me.Location = New Point(_host.Right, _host.Top)
        'Me.Location = New Point(_host.Right - Me.Width, _host.Top)

    End Sub


    ' ── WebSocket Connection & Subscription ────────────────────────────────────
    Private Async Sub ConnectAndStream()
        Try
            client = New ClientWebSocket()
            Await client.ConnectAsync(New Uri(DeribitUrl), CancellationToken.None)

            ' 1) Load last hour of 1-min bars
            Dim histReq = New With {
            .jsonrpc = "2.0", .id = 1,
            .method = "public/get_tradingview_chart_data",
            .params = New With {
                .instrument_name = "BTC-PERPETUAL",
                .resolution = "1",
                .start_timestamp = CLng(DateTimeOffset.UtcNow.AddHours(-72).ToUnixTimeMilliseconds()),
                .end_timestamp = CLng(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            }
        }
            Await SendJson(histReq)

            'For startup only
            If startupFired = False Then
                Dim quotes = SyncLockCopy(ohlcList)
                FireStartupSignals(quotes)
            End If

            ' 2) Subscribe to live 1-min OHLC candles
            Dim subReq = New With {
                .jsonrpc = "2.0", .id = 2,
                .method = "public/subscribe",
                .params = New With {
                    .channels = New String() {"chart.trades.BTC-PERPETUAL.1"}
                }
            }
            Await SendJson(subReq)

            ' 3) Read loop
            ' Enhanced message reading loop
            'Dim buffer(8192) As Byte 'Original buffer size

            ' Enhanced version with debugging
            Dim buffer(65535) As Byte
            Dim sb As New StringBuilder()

            While client.State = WebSocketState.Open
                Try
                    Dim res = Await client.ReceiveAsync(New ArraySegment(Of Byte)(buffer), CancellationToken.None)

                    If res.MessageType = WebSocketMessageType.Close Then Exit While

                    ' Accumulate fragments
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count))

                    ' Process only complete messages
                    If res.EndOfMessage AndAlso res.MessageType = WebSocketMessageType.Text Then
                        Dim jsonText As String = sb.ToString()
                        sb.Clear()

                        ProcessMessage(jsonText)
                    End If

                Catch ex As WebSocketException
                    AppendLog($"WebSocket error: {ex.Message}", Color.Red)
                    Exit While
                End Try
            End While



        Catch ex As Exception
            ' Handle connection errors
            AppendLog($"WebSocket error: {ex.Message}", Color.Red)
            ' Implement reconnection logic here
        End Try

    End Sub

    Private Async Function SendJson(msg As Object) As Task
        Dim json = JsonConvert.SerializeObject(msg)
        Dim bytes = Encoding.UTF8.GetBytes(json)
        Await client.SendAsync(New ArraySegment(Of Byte)(bytes), WebSocketMessageType.Text, True, CancellationToken.None)
    End Function

    ' ── Poll Handler (fallback) ────────────────────────────────────────────────
    Private Sub OnPollElapsed(sender As Object, e As ElapsedEventArgs)

        Try
            If client Is Nothing OrElse client.State <> WebSocketState.Open Then
                ' Attempt reconnection
                Task.Run(AddressOf ConnectAndStream)
                Return
            End If

            Dim nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            Dim req = New With {
            .jsonrpc = "2.0", .id = 3,
            .method = "public/get_tradingview_chart_data",
            .params = New With {
                .instrument_name = "BTC-PERPETUAL",
                .resolution = "1",
                .start_timestamp = lastTimestamp + 1,
                .end_timestamp = nowMs
            }
        }
            Task.Run(Async Function()
                         Try
                             Await SendJson(req)
                         Catch ex As Exception
                             AppendLog($"Polling error: {ex.Message}", Color.Red)
                         End Try
                         Return Nothing ' Explicit return for Function
                     End Function)
        Catch ex As Exception
            AppendLog($"Timer error: {ex.Message}", Color.Red)
        End Try
    End Sub

    ' ── Message Processor ─────────────────────────────────────────────────────
    Private Sub ProcessMessage(raw As String)
        Try
            ' Add debug logging
            'AppendLog($"Received: {raw}", Color.LightGray)

            Dim msg = JObject.Parse(raw)

            ' Ignore subscribe‐confirmations (result=array)
            If msg("result") IsNot Nothing AndAlso TypeOf msg("result") Is JArray Then Return

            Dim id = msg("id")?.ToObject(Of Integer)()

            ' Handle history (id=1) or poll (id=3)
            If (id = 1 OrElse id = 3) AndAlso msg("result") IsNot Nothing Then
                Dim res = msg("result")
                Dim times = res("ticks").ToObject(Of List(Of Long))()
                Dim opens = res("open").ToObject(Of List(Of Decimal))()
                Dim highs = res("high").ToObject(Of List(Of Decimal))()
                Dim lows = res("low").ToObject(Of List(Of Decimal))()
                Dim closes = res("close").ToObject(Of List(Of Decimal))()
                Dim vols = res("volume").ToObject(Of List(Of Decimal))()

                SyncLock ohlcList
                    ' Append only new bars
                    For i = 0 To times.Count - 1
                        Dim t = times(i)
                        If t > lastTimestamp Then
                            lastTimestamp = t
                            ohlcList.Add(New Quote With {
                                .Date = DateTimeOffset.FromUnixTimeMilliseconds(t).LocalDateTime,
                                .Open = opens(i),
                                .High = highs(i),
                                .Low = lows(i),
                                .Close = closes(i),
                                .Volume = vols(i)
                            })
                            If ohlcList.Count > 4320 Then ohlcList.RemoveAt(0)
                        End If
                    Next

                    ' Start fallback polling after history loaded
                    If Not pollTimer.Enabled Then pollTimer.Start()
                End SyncLock

                Task.Run(Sub()
                             Me.Invoke(Sub() UpdateSignals())
                         End Sub)

                Return
            End If

            ' Handle live candle subscription
            If msg("method")?.ToString() = "subscription" Then
                Dim channelName = msg("params")("channel")?.ToString()

                ' Enhanced thread-safe heartbeat indication with error handling
                Try
                    Me.Invoke(Sub()
                                  redHeartBeat.BackColor = Color.Crimson
                                  heartbeatTimer.Stop()
                                  heartbeatTimer.Start()
                              End Sub)
                Catch ex As Exception
                    ' Fallback logging if UI update fails
                    System.Diagnostics.Debug.WriteLine($"Heartbeat UI update failed: {ex.Message}")
                End Try
                'End heartbeat indication

                If channelName?.StartsWith("chart.trades.BTC-PERPETUAL") Then
                    Dim data = msg("params")("data").ToObject(Of DeribitCandle)()
                    Dim barMs = data.ticks
                    Dim q = New Quote With {
                .Date = DateTimeOffset.FromUnixTimeMilliseconds(barMs).LocalDateTime,
                .Open = data.open,
                .High = data.high,
                .Low = data.low,
                .Close = data.close,
                .Volume = data.volume
            }

                    SyncLock ohlcList
                        If barMs > lastTimestamp Then
                            lastTimestamp = barMs
                            ohlcList.Add(q)
                            If ohlcList.Count > 4320 Then ohlcList.RemoveAt(0)
                        End If
                    End SyncLock

                    Task.Run(Sub()
                                 Me.Invoke(Sub() UpdateSignals())
                             End Sub)
                End If
            End If
        Catch ex As JsonException
            AppendLog($"JSON parsing error: {ex.Message}", Color.Red)
        Catch ex As Exception
            AppendLog($"Message processing error: {ex.Message}", Color.Red)
        End Try
    End Sub


    Private Function SyncLockCopy(src As List(Of Quote)) As List(Of Quote)
        SyncLock src
            Return New List(Of Quote)(src)
        End SyncLock
    End Function

    ' ── Indicator Calculations & UI Update ────────────────────────────────────

    'One time startup signals
    Private Sub FireStartupSignals(quotes As IList(Of Quote))
        If quotes.Count < 14 Then Return

        ' Evaluate each indicator’s current label text
        UpdateDmi(quotes)
        UpdateMacd(quotes)
        UpdateRsi(quotes)
        UpdateStochastic(quotes) 'startupFired = True is placed inside here to stop re-execution

        startupFired = True
    End Sub

    'AUTOMATED TRADING SYSTEM CODE BELOW
    '------------------------------------------

    Private Sub ProcessAutomatedSignal(currentScore As Integer, bias As Decimal)
        Try
            ' Log entry point
            ' AppendLog($"ProcessAutomatedSignal: Score={currentScore}, Bias={bias:F1}%", Color.Cyan)

            Dim mainForm As frmMainPageV2 = CType(_host, frmMainPageV2)

            If (enableAutoTrading = True) Then
                If frmMainPageV2.USDPublicSession < Decimal.TryParse(_autoTradeSettings.txtCircuitBreaker.Text, CircuitBreak) Then
                    'LogTradeDecision("ANY", currentScore, "Circuit breaker active", False)

                    enableAutoTrading = False
                    btnAutoTrade.Text = "AUTO: STOPPED"
                    btnAutoTrade.BackColor = Color.DarkRed
                    AppendLog($"Circuit breaker triggered! Loss limit reached: ${CircuitBreak:F2}", Color.Red)
                    LogTradeDecision("ANY", currentScore, "Circuit breaker triggered.", False)

                    Return
                End If
            End If

            ' ATR Filter Check with logging
            Dim currentATR As Decimal = 0
            If Not Decimal.TryParse(lblATR.Text, currentATR) Then
                AppendLog($"Auto-trade blocked: Invalid ATR value '{lblATR.Text}'", Color.Red)
                Return
            End If

            Dim atrLimit As Decimal = 0
            If Not String.IsNullOrEmpty(_autoTradeSettings.txtATRLimit.Text) Then
                If Not Decimal.TryParse(_autoTradeSettings.txtATRLimit.Text, atrLimit) Then
                    AppendLog($"Auto-trade blocked: Invalid ATR limit '{_autoTradeSettings.txtATRLimit.Text}'", Color.Red)
                    Return
                End If
            End If

            'AppendLog($"ATR Check: Current={currentATR:F2}, Limit={atrLimit:F2}", Color.Gray)

            If atrLimit > 0 AndAlso currentATR < atrLimit Then
                ' AppendLog($"Auto-trade blocked: ATR {currentATR:F2} < limit {atrLimit:F2}", Color.Yellow)
                Return
            End If

            ' Signal Strength Validation with logging
            Dim longThreshold As Integer = 0
            Dim shortThreshold As Integer = 0

            If Not Integer.TryParse(_autoTradeSettings.txtLScore.Text, longThreshold) Then
                AppendLog($"Auto-trade blocked: Invalid long threshold '{_autoTradeSettings.txtLScore.Text}'", Color.Red)
                Return
            End If

            If Not Integer.TryParse(_autoTradeSettings.txtSScore.Text, shortThreshold) Then
                AppendLog($"Auto-trade blocked: Invalid short threshold '{_autoTradeSettings.txtSScore.Text}'", Color.Red)
                Return
            End If

            'AppendLog($"Threshold Check: Score={currentScore}, Long≥{longThreshold}, Short≤{shortThreshold}", Color.Gray)

            If Decimal.Parse(mainForm.txtPlacedPrice.Text) = 0 Then
                If currentScore >= longThreshold Then
                    ' Check trend alignment before executing LONG trade
                    If IsTrendAligned("LONG") Then
                        AppendLog($"LONG signal triggered: {currentScore} >= {longThreshold} (Trend Aligned)", Color.Green)
                        ExecuteAutomatedTrade("LONG", currentScore, currentATR)
                        LogTradeDecision("LONG", currentScore, "Signal threshold met - Trend aligned", True)
                    Else
                        AppendLog($"LONG signal blocked: Trend not aligned (Score: {currentScore})", Color.Orange)
                        LogTradeDecision("LONG", currentScore, "Signal blocked - No Long trend confirmation", False)
                    End If
                ElseIf currentScore <= shortThreshold Then
                    ' Check trend alignment before executing SHORT trade
                    If IsTrendAligned("SHORT") Then
                        AppendLog($"SHORT signal triggered: {currentScore} <= {shortThreshold} (Trend Aligned)", Color.Green)
                        ExecuteAutomatedTrade("SHORT", currentScore, currentATR)
                        LogTradeDecision("SHORT", currentScore, "Signal threshold met - Trend aligned", True)
                    Else
                        AppendLog($"SHORT signal blocked: Trend not aligned (Score: {currentScore})", Color.Orange)
                        LogTradeDecision("SHORT", currentScore, "Signal blocked - No Short trend confirmation", False)
                    End If
                    'Else
                    '    AppendLog($"No signal: Score {currentScore} between thresholds ({shortThreshold} to {longThreshold})", Color.Gray)
                End If
            End If


        Catch ex As Exception
            AppendLog($"Auto-trading error in ProcessAutomatedSignal: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Function IsTrendAligned(direction As String) As Boolean
        Try
            Dim quotes = SyncLockCopy(ohlcList)
            If quotes.Count < 200 Then Return True ' Not enough data for EMA200

            Dim ema50 = quotes.GetEma(50).LastOrDefault()?.Ema
            Dim ema200 = quotes.GetEma(200).LastOrDefault()?.Ema

            If Not ema50.HasValue OrElse Not ema200.HasValue Then Return True

            Dim currentPrice = quotes.Last().Close

            'For minimum trend strength calculation
            '-----------------------------------------
            Dim minSeparation As Decimal = 0.5 ' Default
            If Not String.IsNullOrEmpty(_autoTradeSettings.txtTrendStrength.Text) Then
                Decimal.TryParse(_autoTradeSettings.txtTrendStrength.Text, minSeparation)
            End If

            Dim trendStrength = Math.Abs(ema50.Value - ema200.Value) / ema200.Value * 100

            '-----------------------------------------

            If direction = "LONG" Then
                ' Only go long if price > EMA50 > EMA200 (strong bullish trend) and trend strength is sufficient
                Return currentPrice > ema50.Value AndAlso ema50.Value > ema200.Value AndAlso trendStrength > minSeparation
            Else
                ' Only go short if price < EMA50 < EMA200 (strong bearish trend) and trend strength is sufficient
                Return currentPrice < ema50.Value AndAlso ema50.Value < ema200.Value AndAlso trendStrength > minSeparation
            End If
        Catch ex As Exception
            AppendLog($"Trend alignment check error: {ex.Message}", Color.Orange)
            Return True ' Default to allowing trade on error
        End Try
    End Function


    Private enableAutoTrading As Boolean = False
    Public lastAutoTradeTime As DateTime = DateTime.MinValue
    Private AUTO_TRADE_COOLDOWN_MS As Integer

    Private Function CanPlaceAutomatedOrder() As Boolean

        ' Time-based restriction check (FIRST PRIORITY)
        If IsWithinRestrictedTimeRange() Then
            ' Only log once per minute to avoid spam
            ' Static lastRestrictedLog As DateTime = DateTime.MinValue
            ' If (DateTime.Now - lastRestrictedLog).TotalMinutes >= 1 Then
            ' Dim utc8Time As DateTime = DateTime.UtcNow.AddHours(8)
            ' AppendLog($"Auto-trade blocked: Restricted time period (Current: {utc8Time:HH:mm})", Color.Orange)
            ' lastRestrictedLog = DateTime.Now
            ' End If
            Return False
        End If

        ' Cooldown Check
        If (DateTime.Now - lastAutoTradeTime).TotalMilliseconds < AUTO_TRADE_COOLDOWN_MS Then
            Return False
        End If

        ' WebSocket Health Check - use consistent reference
        Dim mainForm As frmMainPageV2 = CType(_host, frmMainPageV2)

        ' Add diagnostic logging
        'AppendLog($"Auto-trade check: EnableAutoTrading={enableAutoTrading}", Color.Gray)
        'AppendLog($"Auto-trade check: WebSocket={mainForm.IsWebSocketConnected}", Color.Gray)
        'AppendLog($"Auto-trade check: RateLimiter initialized={mainForm.RateLimiterInstance IsNot Nothing}", Color.Gray)

        'If mainForm.RateLimiterInstance IsNot Nothing Then
        ' AppendLog($"Auto-trade check: Can make request={mainForm.RateLimiterInstance.CanMakeRequest()}", Color.Gray)
        ' End If

        If Not mainForm.IsWebSocketConnected Then
            Return False
        End If

        ' FIXED: Use the CanMakeAPIRequest property instead of direct rate limiter access
        If Not mainForm.CanMakeAPIRequest Then
            AppendLog("Auto-trade blocked: Rate limit active", Color.Orange)

            ' Force rate limiter initialization if it's not ready
            If mainForm.RateLimiterInstance Is Nothing Then
                AppendLog("Rate limiter not initialized - triggering initialization", Color.Yellow)
                ' Trigger initialization in main form
                Task.Run(Async Function()
                             Try
                                 Await mainForm.InitializeRateLimits()
                             Catch ex As Exception
                                 AppendLog($"Failed to initialize rate limiter: {ex.Message}", Color.Red)
                             End Try
                             Return Nothing
                         End Function)
            End If

            Return False
        End If

        Return enableAutoTrading
    End Function

    Private Sub UpdateLastAutoTradeTime()
        lastAutoTradeTime = DateTime.Now
    End Sub

    Private Sub ExecuteAutomatedTrade(direction As String, score As Integer, atr As Decimal)
        Try
            Dim mainForm As frmMainPageV2 = CType(_host, frmMainPageV2)

            ' Check if we can make the request
            If Not mainForm.CanMakeAPIRequest Then
                AppendLog("Auto-trade execution blocked: Rate limit active", Color.Orange)
                Return
            End If

            ' Position Status Check
            If Decimal.Parse(mainForm.txtPlacedPrice.Text) > 0 Then
                '     AppendLog("Auto-trade blocked: Position already open", Color.Yellow)
                Return
            End If

            ' Update last trade time immediately to prevent multiple rapid executions
            UpdateLastAutoTradeTime()

            If direction = "LONG" Then
                btnATR.PerformClick()
                mainForm.btnBuy.PerformClick()
                mainForm.btnLimit.PerformClick()
            ElseIf direction = "SHORT" Then
                btnATR.PerformClick()
                mainForm.btnSell.PerformClick()
                mainForm.btnLimit.PerformClick()
            End If

            AppendLog($"AUTO-TRADE: {direction} executed (Score: {score}, ATR: {atr:F2})", Color.Cyan)

        Catch ex As Exception
            AppendLog($"Auto-trade execution failed: {ex.Message}", Color.Red)
        End Try
    End Sub

    'For checking if the current time is within the restricted time range for auto trading
    Private Function IsWithinRestrictedTimeRange() As Boolean
        Try
            ' Get current time in UTC+8 (Singapore/Malaysia/Hong Kong time)
            Dim utc8Time As DateTime = DateTime.UtcNow.AddHours(8)
            Dim currentTime As TimeSpan = utc8Time.TimeOfDay

            ' Check if textboxes are empty - if so, no time restrictions
            If String.IsNullOrWhiteSpace(_autoTradeSettings.txtStartTime.Text) OrElse String.IsNullOrWhiteSpace(_autoTradeSettings.txtEndTime.Text) Then
                Return False
            End If

            ' Parse start and end times from textboxes
            Dim startTime As TimeSpan
            Dim endTime As TimeSpan

            If Not TimeSpan.TryParse(_autoTradeSettings.txtStartTime.Text.Trim(), startTime) Then
                AppendLog($"Invalid start time format: '{_autoTradeSettings.txtStartTime.Text}'. Use HH:mm format (e.g., 21:30)", Color.Yellow)
                Return False ' Invalid format means no restriction
            End If

            If Not TimeSpan.TryParse(_autoTradeSettings.txtEndTime.Text.Trim(), endTime) Then
                AppendLog($"Invalid end time format: '{_autoTradeSettings.txtEndTime.Text}'. Use HH:mm format (e.g., 22:00)", Color.Yellow)
                Return False ' Invalid format means no restriction
            End If

            ' Handle time ranges that span midnight
            If startTime <= endTime Then
                ' Normal range (e.g., 09:30 - 22:00)
                Return currentTime >= startTime AndAlso currentTime <= endTime
            Else
                ' Range spans midnight (e.g., 22:00 - 02:00 next day)
                Return currentTime >= startTime OrElse currentTime <= endTime
            End If

        Catch ex As Exception
            AppendLog($"Error checking restricted time range: {ex.Message}", Color.Red)
            Return False ' If error, don't restrict trading
        End Try
    End Function

    Private Function GetCurrentUTC8TimeString() As String
        Dim utc8Time As DateTime = DateTime.UtcNow.AddHours(8)
        Return utc8Time.ToString("HH:mm:ss")
    End Function

    'For text file trade logging
    Private Sub LogTradeDecision(signal As String, score As Integer, reason As String, executed As Boolean)

        'Dim topBidPrice As Decimal = Decimal.Parse(frmMainPageV2.txtTopBid.Text)
        'Dim takeProfitprice As Decimal = Decimal.Parse(frmMainPageV2.txtTakeProfit.Text)
        'Dim triggerPrice As Decimal = Decimal.Parse(frmMainPageV2.txtTrigger.Text)

        'Dim TPPrice, STPrice As Decimal

        'If signal = "LONG" Then
        ' TPPrice = topBidPrice + takeProfitprice
        'STPrice = topBidPrice - triggerPrice
        'Else
        'TPPrice = topBidPrice - takeProfitprice
        'STPrice = topBidPrice + triggerPrice
        'End If

        Dim logEntry As String = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " &
                           $"Signal: {signal} | Score: {score} | " &
                           $"Reason: {reason} | Executed: {executed} "
        ' $"Init. Price: {topBidPrice} | Take Profit: {TPPrice} | " &
        '  $"Stop Loss: {STPrice} "

        ' Write to file for later analysis
        Try
            File.AppendAllText("AutoTradeLog.txt", logEntry & Environment.NewLine)
        Catch
            AppendLog("Text file IO error", Color.Red) ' Handle file write errors
        End Try

        'AppendLog(logEntry, If(executed, Color.Cyan, Color.Gray))
    End Sub

    'Private USDPL As Decimal = 0
    Private CircuitBreak As Decimal = 0

    '--------------------------------------------

    'Start of live signal calls & updates
    Private Sub UpdateSignals()
        Dim quotes = SyncLockCopy(ohlcList)
        If quotes.Count < 14 Then Return

        UpdateDmi(quotes)
        UpdateMacd(quotes)
        UpdateRsi(quotes)
        UpdateStochastic(quotes)
        EvaluateEmaVwapSignals(quotes)
        UpdateATR(quotes)

        Dim signedBias As Decimal = (score / 21) * 100

        ' Log current score for monitoring
        'AppendLog($"Current Signal Score: {score}/21 ({signedBias:F1}%)", Color.LightBlue)

        ' AUTO-TRADING INTEGRATION POINT
        If enableAutoTrading AndAlso CanPlaceAutomatedOrder() Then
            Try
                ProcessAutomatedSignal(score, signedBias)
            Catch ex As Exception
                ' Handle exceptions on background thread
                Me.Invoke(Sub()
                              AppendLog($"Auto-trading error: {ex.Message}", Color.Red)
                          End Sub)
            End Try
        End If

        If signedBias > 0 Then

            'lblOverall.ForeColor = Color.DodgerBlue
            lblScore.ForeColor = Color.DodgerBlue
            lblScore.Text = $"BUY {Math.Abs(signedBias):F1}% - ({score}/21)"
        Else
            'lblOverall.ForeColor = Color.Crimson
            lblScore.ForeColor = Color.Crimson
            lblScore.Text = $"SELL {Math.Abs(signedBias):F1}% - ({score}/21)"
        End If

        score = 0

    End Sub

    ' ── DMI Section ───────────────────────────────────────────────────────
    Private Sub UpdateDmi(quotes As IList(Of Quote))
        ' ═══════════════════════════════════════════════════════════════════
        ' DMI - Hybrid with Signal Confirmation Window
        ' ═══════════════════════════════════════════════════════════════════
        Static prevPDI As Decimal = -1, prevMDI As Decimal = -1, prevADX As Decimal = -1
        Static dmiInitialized As Boolean = False
        Static lastDMISignal As String = "-"
        Static lastDMISignalTime As DateTime = DateTime.MinValue
        Static dmiPeriodsAfterCrossover As Integer = 0

        Dim dmi = quotes.GetAdx(9).LastOrDefault()
        If dmi IsNot Nothing AndAlso dmi.Pdi.HasValue AndAlso dmi.Mdi.HasValue AndAlso dmi.Adx.HasValue Then
            Dim pdi = CDec(dmi.Pdi.Value)
            Dim mdi = CDec(dmi.Mdi.Value)
            Dim adx = CDec(dmi.Adx.Value)

            ' Initialize on first run
            If Not dmiInitialized Then
                prevPDI = pdi : prevMDI = mdi : prevADX = adx
                dmiInitialized = True
                lblDMI.Text = "INITIALIZING." : lblDMI.ForeColor = Color.Gray
                lastDMISignal = "INITIALIZING."
                Return
            End If

            Dim newDMISignal As String = lastDMISignal
            Dim newDMIColor As Color = lblDMI.ForeColor

            'Check for startup flag
            If startupFired = False Then
                If pdi > mdi AndAlso adx > 22 Then
                    ' NEW Bullish crossover with trend strength - reset confirmation window
                    dmiPeriodsAfterCrossover = 0
                    If adx > 40 Then
                        newDMISignal = "STRONG BUY - S" : newDMIColor = Color.Lime
                    ElseIf adx > 30 Then
                        newDMISignal = "BUY - S" : newDMIColor = Color.LightGreen
                    Else
                        newDMISignal = "WEAK BUY - S" : newDMIColor = Color.YellowGreen
                    End If
                    lastDMISignalTime = DateTime.Now

                ElseIf pdi < mdi AndAlso adx > 22 Then
                    ' NEW Bearish crossover with trend strength - reset confirmation window
                    dmiPeriodsAfterCrossover = 0
                    If adx > 40 Then
                        newDMISignal = "STRONG SELL - S" : newDMIColor = Color.Red
                    ElseIf adx > 30 Then
                        newDMISignal = "SELL - S" : newDMIColor = Color.Orange
                    Else
                        newDMISignal = "WEAK SELL - S" : newDMIColor = Color.Yellow
                    End If
                    lastDMISignalTime = DateTime.Now

                ElseIf lastDMISignal.Contains("BUY") AndAlso dmiPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for BUY signals
                    If adx > 40 AndAlso Not lastDMISignal.StartsWith("STRONG") Then
                        newDMISignal = "STRONG BUY - S" : newDMIColor = Color.Lime
                        AppendLog("DMI ↑ STRONG BUY : ADX inc.", Color.Lime)
                    ElseIf adx > 30 AndAlso lastDMISignal = "WEAK BUY" Then
                        newDMISignal = "BUY - S" : newDMIColor = Color.LightGreen
                        AppendLog("DMI ↑ BUY : ADX inc.", Color.Lime)
                    End If

                ElseIf lastDMISignal.Contains("SELL") AndAlso dmiPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for SELL signals
                    If adx > 40 AndAlso Not lastDMISignal.StartsWith("STRONG") Then
                        newDMISignal = "STRONG SELL - S" : newDMIColor = Color.Red
                        AppendLog("DMI ↑ STRONG SELL : ADX inc.", Color.Lime)
                    ElseIf adx > 30 AndAlso lastDMISignal = "WEAK SELL" Then
                        newDMISignal = "SELL - S" : newDMIColor = Color.Orange
                        AppendLog("DMI ↑ SELL : ADX inc.", Color.Lime)
                    End If
                End If

            Else
                ' Check for new crossover signals
                If pdi > mdi AndAlso prevPDI <= prevMDI AndAlso adx > 22 Then
                    ' NEW Bullish crossover with trend strength - reset confirmation window
                    dmiPeriodsAfterCrossover = 0
                    If adx > 40 Then
                        newDMISignal = "STRONG BUY" : newDMIColor = Color.Lime
                    ElseIf adx > 30 Then
                        newDMISignal = "BUY" : newDMIColor = Color.LightGreen
                    Else
                        newDMISignal = "WEAK BUY" : newDMIColor = Color.YellowGreen
                    End If
                    lastDMISignalTime = DateTime.Now

                ElseIf pdi < mdi AndAlso prevPDI >= prevMDI AndAlso adx > 22 Then
                    ' NEW Bearish crossover with trend strength - reset confirmation window
                    dmiPeriodsAfterCrossover = 0
                    If adx > 40 Then
                        newDMISignal = "STRONG SELL" : newDMIColor = Color.Red
                    ElseIf adx > 30 Then
                        newDMISignal = "SELL" : newDMIColor = Color.Orange
                    Else
                        newDMISignal = "WEAK SELL" : newDMIColor = Color.Yellow
                    End If
                    lastDMISignalTime = DateTime.Now

                ElseIf lastDMISignal.Contains("BUY") AndAlso dmiPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for BUY signals
                    If adx > 40 AndAlso Not lastDMISignal.StartsWith("STRONG") Then
                        newDMISignal = "STRONG BUY" : newDMIColor = Color.Lime
                        AppendLog("DMI ↑ STRONG BUY : ADX inc.", Color.Lime)
                    ElseIf adx > 30 AndAlso lastDMISignal = "WEAK BUY" Then
                        newDMISignal = "BUY" : newDMIColor = Color.LightGreen
                        AppendLog("DMI ↑ BUY : ADX inc.", Color.Lime)
                    End If

                ElseIf lastDMISignal.Contains("SELL") AndAlso dmiPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for SELL signals
                    If adx > 40 AndAlso Not lastDMISignal.StartsWith("STRONG") Then
                        newDMISignal = "STRONG SELL" : newDMIColor = Color.Red
                        AppendLog("DMI ↑ STRONG SELL : ADX inc.", Color.Lime)
                    ElseIf adx > 30 AndAlso lastDMISignal = "WEAK SELL" Then
                        newDMISignal = "SELL" : newDMIColor = Color.Orange
                        AppendLog("DMI ↑ SELL : ADX inc.", Color.Lime)
                    End If
                End If
            End If

            ' Update display only if signal changed
            If newDMISignal <> lastDMISignal Then
                lblDMI.Text = newDMISignal
                lblDMI.ForeColor = newDMIColor
                lastDMISignal = newDMISignal
                AppendLog($"DMI: {newDMISignal} (PDI:{pdi:F1}, MDI:{mdi:F1}, ADX:{adx:F1})", Color.Yellow)
            End If

            'To calculate leaning to bias indicator strength:
            Select Case newDMISignal
                Case "WEAK BUY" : score += 1
                Case "BUY" : score += 2
                Case "STRONG BUY" : score += 3
                Case "WEAK SELL" : score -= 1
                Case "SELL" : score -= 2
                Case "STRONG SELL" : score -= 3
            End Select

            ' Increment confirmation window counter
            dmiPeriodsAfterCrossover += 1
            prevPDI = pdi : prevMDI = mdi : prevADX = adx
        End If
    End Sub

    ' ── MACD Section ──────────────────────────────────────────────────────
    Private Sub UpdateMacd(quotes As IList(Of Quote))
        '═══════════════════════════════════════════════════════════════════
        ' MACD - Hybrid with Signal Confirmation Window
        '═══════════════════════════════════════════════════════════════════
        Static prevHistogram As Decimal = 0, prevMacdLine As Decimal = 0, prevSignalLine As Decimal = 0
        Static macdInitialized As Boolean = False
        Static lastMACDSignal As String = "-"
        Static lastMACDSignalTime As DateTime = DateTime.MinValue
        Static macdPeriodsAfterCrossover As Integer = 0

        Dim macd = quotes.GetMacd(6, 13, 5).LastOrDefault()
        If macd IsNot Nothing AndAlso macd.Histogram.HasValue AndAlso macd.Macd.HasValue AndAlso macd.Signal.HasValue Then
            Dim histogram = CDec(macd.Histogram.Value)
            Dim macdLine = CDec(macd.Macd.Value)
            Dim signalLine = CDec(macd.Signal.Value)

            ' Initialize on first run
            If Not macdInitialized Then
                prevHistogram = histogram : prevMacdLine = macdLine : prevSignalLine = signalLine
                macdInitialized = True
                lblMACD.Text = "INITIALIZING." : lblMACD.ForeColor = Color.Gray
                lastMACDSignal = "INITIALIZING."
                Return
            End If

            Dim newMACDSignal As String = lastMACDSignal
            Dim newMACDColor As Color = lblMACD.ForeColor

            'Check for startup flag
            If startupFired = False Then
                If macdLine > signalLine Then
                    ' NEW Bullish crossover - reset confirmation window
                    macdPeriodsAfterCrossover = 0
                    If histogram > 10 Then
                        newMACDSignal = "STRONG BUY - S" : newMACDColor = Color.Lime
                    ElseIf histogram > 3 Then
                        newMACDSignal = "BUY - S" : newMACDColor = Color.LightGreen
                    ElseIf histogram > 0.5 Then
                        newMACDSignal = "WEAK BUY - S" : newMACDColor = Color.YellowGreen
                    End If
                    lastMACDSignalTime = DateTime.Now

                ElseIf macdLine < signalLine Then
                    ' NEW Bearish crossover - reset confirmation window
                    macdPeriodsAfterCrossover = 0
                    If histogram < -10 Then
                        newMACDSignal = "STRONG SELL - S" : newMACDColor = Color.Red
                    ElseIf histogram < -3 Then
                        newMACDSignal = "SELL - S" : newMACDColor = Color.Orange
                    ElseIf histogram < -0.5 Then
                        newMACDSignal = "WEAK SELL - S" : newMACDColor = Color.Yellow
                    End If
                    lastMACDSignalTime = DateTime.Now

                ElseIf lastMACDSignal.Contains("BUY") AndAlso macdPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for BUY signals
                    If histogram > 10 AndAlso Not lastMACDSignal.StartsWith("STRONG") Then
                        newMACDSignal = "STRONG BUY - S" : newMACDColor = Color.Lime
                        AppendLog("MACD ↑ STRONG BUY : Hist. inc.", Color.Lime)
                    ElseIf histogram > 3 AndAlso lastMACDSignal = "WEAK BUY" Then
                        newMACDSignal = "BUY - S" : newMACDColor = Color.LightGreen
                        AppendLog("MACD ↑ BUY : Hist. inc.", Color.Lime)
                    End If

                ElseIf lastMACDSignal.Contains("SELL") AndAlso macdPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for SELL signals
                    If histogram < -10 AndAlso Not lastMACDSignal.StartsWith("STRONG") Then
                        newMACDSignal = "STRONG SELL - S" : newMACDColor = Color.Red
                        AppendLog("MACD ↑ STRONG SELL : Hist. dec.", Color.Lime)
                    ElseIf histogram < -3 AndAlso lastMACDSignal = "WEAK SELL" Then
                        newMACDSignal = "SELL - S" : newMACDColor = Color.Orange
                        AppendLog("MACD ↑ SELL : Hist. dec.", Color.Lime)
                    End If
                End If

            Else
                ' Check for new crossover signals
                If macdLine > signalLine AndAlso prevMacdLine <= prevSignalLine Then
                    ' NEW Bullish crossover - reset confirmation window
                    macdPeriodsAfterCrossover = 0
                    If histogram > 10 Then
                        newMACDSignal = "STRONG BUY" : newMACDColor = Color.Lime
                    ElseIf histogram > 3 Then
                        newMACDSignal = "BUY" : newMACDColor = Color.LightGreen
                    ElseIf histogram > 0.5 Then
                        newMACDSignal = "WEAK BUY" : newMACDColor = Color.YellowGreen
                    End If
                    lastMACDSignalTime = DateTime.Now

                ElseIf macdLine < signalLine AndAlso prevMacdLine >= prevSignalLine Then
                    ' NEW Bearish crossover - reset confirmation window
                    macdPeriodsAfterCrossover = 0
                    If histogram < -10 Then
                        newMACDSignal = "STRONG SELL" : newMACDColor = Color.Red
                    ElseIf histogram < -3 Then
                        newMACDSignal = "SELL" : newMACDColor = Color.Orange
                    ElseIf histogram < -0.5 Then
                        newMACDSignal = "WEAK SELL" : newMACDColor = Color.Yellow
                    End If
                    lastMACDSignalTime = DateTime.Now

                ElseIf lastMACDSignal.Contains("BUY") AndAlso macdPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for BUY signals
                    If histogram > 10 AndAlso Not lastMACDSignal.StartsWith("STRONG") Then
                        newMACDSignal = "STRONG BUY" : newMACDColor = Color.Lime
                        AppendLog("MACD ↑ STRONG BUY : Hist. inc.", Color.Lime)
                    ElseIf histogram > 3 AndAlso lastMACDSignal = "WEAK BUY" Then
                        newMACDSignal = "BUY" : newMACDColor = Color.LightGreen
                        AppendLog("MACD ↑ BUY : Hist. inc.", Color.Lime)
                    End If

                ElseIf lastMACDSignal.Contains("SELL") AndAlso macdPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for SELL signals
                    If histogram < -10 AndAlso Not lastMACDSignal.StartsWith("STRONG") Then
                        newMACDSignal = "STRONG SELL" : newMACDColor = Color.Red
                        AppendLog("MACD ↑ STRONG SELL : Hist. dec.", Color.Lime)
                    ElseIf histogram < -3 AndAlso lastMACDSignal = "WEAK SELL" Then
                        newMACDSignal = "SELL" : newMACDColor = Color.Orange
                        AppendLog("MACD ↑ SELL : Hist. dec.", Color.Lime)
                    End If
                End If
            End If
            ' ── EMA Confirmation by Signal Text Matching ──────────────────────
            Dim currentEmaSignal As String = lblEMA.Text

            If newMACDSignal.Contains("BUY") AndAlso currentEmaSignal.Contains("BUY") Then
                If Not newMACDSignal.Contains(" - EMA OK") Then
                    newMACDSignal &= " - EMA OK"
                    'newMACDColor = Color.Cyan
                End If
            ElseIf newMACDSignal.Contains("SELL") AndAlso currentEmaSignal.Contains("SELL") Then
                If Not newMACDSignal.Contains(" - EMA OK") Then
                    newMACDSignal &= " - EMA OK"
                    'newMACDColor = Color.Cyan
                End If
            End If

            ' Only update if signal changed
            If newMACDSignal <> lastMACDSignal Then
                lblMACD.Text = newMACDSignal
                lblMACD.ForeColor = newMACDColor
                lastMACDSignal = newMACDSignal
                AppendLog($"MACD: {newMACDSignal} (MACD:{macdLine:F3}, Signal:{signalLine:F3}, Hist:{histogram:F3})", Color.Yellow)
            End If

            'To calculate leaning to bias indicator strength:
            If newMACDSignal.Contains("WEAK BUY") Then
                score += 1
            ElseIf newMACDSignal.Contains("BUY") Then
                score += 2
            ElseIf newMACDSignal.Contains("STRONG BUY") Then
                score += 3
            ElseIf newMACDSignal.Contains("WEAK SELL") Then
                score -= 1
            ElseIf newMACDSignal.Contains("SELL") Then
                score -= 2
            ElseIf newMACDSignal.Contains("STRONG SELL") Then
                score -= 3
            End If

            If newMACDSignal.Contains("BUY") AndAlso newMACDSignal.Contains("EMA OK") Then
                score += 1
            ElseIf newMACDSignal.Contains("SELL") AndAlso newMACDSignal.Contains("EMA OK") Then
                score -= 1
            End If

            ' Increment confirmation window counter
            macdPeriodsAfterCrossover += 1
            prevHistogram = histogram : prevMacdLine = macdLine : prevSignalLine = signalLine
        End If
    End Sub

    Private currentRsiValue As Decimal = 0 ' Global variable to hold RSI value  
    ' ── RSI Section ───────────────────────────────────────────────────────
    Private Sub UpdateRsi(quotes As IList(Of Quote))
        '═══════════════════════════════════════════════════════════════════
        ' RSI - Hybrid with Signal Confirmation Window
        '═══════════════════════════════════════════════════════════════════
        Static prevRSI As Decimal = -1, prevPrice As Decimal = -1
        Static rsiInitialized As Boolean = False
        Static lastRSISignal As String = "-"
        Static lastRSISignalTime As DateTime = DateTime.MinValue
        Static rsiPeriodsAfterCrossover As Integer = 0

        Dim rsi = quotes.GetRsi(9).LastOrDefault()
        Dim currentPrice = quotes.Last().Close

        If rsi IsNot Nothing AndAlso rsi.Rsi.HasValue Then
            Dim rsiValue = CDec(rsi.Rsi.Value)
            currentRsiValue = rsiValue

            ' Initialize on first run
            If Not rsiInitialized Then
                prevRSI = rsiValue : prevPrice = currentPrice
                rsiInitialized = True
                lblRSI.Text = "INITIALIZING." : lblRSI.ForeColor = Color.Gray
                lastRSISignal = "INITIALIZING."
                Return
            End If

            Dim newRSISignal As String = lastRSISignal
            Dim newRSIColor As Color = lblRSI.ForeColor

            'check for startup flag
            If startupFired = False Then
                If rsiValue < 30 Then
                    ' NEW Oversold entry - reset confirmation window
                    rsiPeriodsAfterCrossover = 0
                    If rsiValue < 15 Then
                        newRSISignal = "STRONG BUY - S" : newRSIColor = Color.Lime
                    ElseIf rsiValue < 25 Then
                        newRSISignal = "BUY - S" : newRSIColor = Color.LightGreen
                    Else
                        newRSISignal = "WEAK BUY - S" : newRSIColor = Color.YellowGreen
                    End If
                    lastRSISignalTime = DateTime.Now

                ElseIf rsiValue > 70 Then
                    ' NEW Overbought entry - reset confirmation window
                    rsiPeriodsAfterCrossover = 0
                    If rsiValue > 85 Then
                        newRSISignal = "STRONG SELL - S" : newRSIColor = Color.Red
                    ElseIf rsiValue > 75 Then
                        newRSISignal = "SELL - S" : newRSIColor = Color.Orange
                    Else
                        newRSISignal = "WEAK SELL - S" : newRSIColor = Color.Yellow
                    End If
                    lastRSISignalTime = DateTime.Now

                ElseIf rsiValue > 50 Then
                    ' NEW Momentum confirmation - reset confirmation window
                    rsiPeriodsAfterCrossover = 0
                    newRSISignal = "MOMENTUM BUY - S" : newRSIColor = Color.CornflowerBlue
                    lastRSISignalTime = DateTime.Now

                ElseIf rsiValue < 50 Then
                    ' NEW Momentum confirmation - reset confirmation window  
                    rsiPeriodsAfterCrossover = 0
                    newRSISignal = "MOMENTUM SELL - S" : newRSIColor = Color.Coral
                    lastRSISignalTime = DateTime.Now

                ElseIf lastRSISignal.Contains("BUY") AndAlso Not lastRSISignal.Contains("MOMENTUM") AndAlso rsiPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for BUY signals
                    If rsiValue < 15 AndAlso Not lastRSISignal.StartsWith("STRONG") Then
                        newRSISignal = "STRONG BUY - S" : newRSIColor = Color.Lime
                        AppendLog("RSI ↑ STRONG BUY : Deeper oversold", Color.Lime)
                    ElseIf rsiValue < 25 AndAlso lastRSISignal = "WEAK BUY" Then
                        newRSISignal = "BUY" : newRSIColor = Color.LightGreen
                        AppendLog("RSI ↑ BUY : Deeper oversold", Color.Lime)
                    End If

                ElseIf lastRSISignal.Contains("SELL") AndAlso Not lastRSISignal.Contains("MOMENTUM") AndAlso rsiPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for SELL signals
                    If rsiValue > 85 AndAlso Not lastRSISignal.StartsWith("STRONG") Then
                        newRSISignal = "STRONG SELL - S" : newRSIColor = Color.Red
                        AppendLog("RSI ↑ STRONG SELL : Deeper overbought", Color.Lime)
                    ElseIf rsiValue > 75 AndAlso lastRSISignal = "WEAK SELL" Then
                        newRSISignal = "SELL" : newRSIColor = Color.Orange
                        AppendLog("RSI ↑ SELL : Deeper overbought", Color.Lime)
                    End If
                End If

            Else
                ' Check for new zone breach signals
                If rsiValue < 30 AndAlso prevRSI >= 30 Then
                    ' NEW Oversold entry - reset confirmation window
                    rsiPeriodsAfterCrossover = 0
                    If rsiValue < 15 Then
                        newRSISignal = "STRONG BUY" : newRSIColor = Color.Lime
                    ElseIf rsiValue < 25 Then
                        newRSISignal = "BUY" : newRSIColor = Color.LightGreen
                    Else
                        newRSISignal = "WEAK BUY" : newRSIColor = Color.YellowGreen
                    End If
                    lastRSISignalTime = DateTime.Now

                ElseIf rsiValue > 70 AndAlso prevRSI <= 70 Then
                    ' NEW Overbought entry - reset confirmation window
                    rsiPeriodsAfterCrossover = 0
                    If rsiValue > 85 Then
                        newRSISignal = "STRONG SELL" : newRSIColor = Color.Red
                    ElseIf rsiValue > 75 Then
                        newRSISignal = "SELL" : newRSIColor = Color.Orange
                    Else
                        newRSISignal = "WEAK SELL" : newRSIColor = Color.Yellow
                    End If
                    lastRSISignalTime = DateTime.Now

                ElseIf rsiValue > 50 AndAlso prevRSI <= 50 AndAlso currentPrice > prevPrice Then
                    ' NEW Momentum confirmation - reset confirmation window
                    rsiPeriodsAfterCrossover = 0
                    newRSISignal = "MOMENTUM BUY" : newRSIColor = Color.CornflowerBlue
                    lastRSISignalTime = DateTime.Now

                ElseIf rsiValue < 50 AndAlso prevRSI >= 50 AndAlso currentPrice < prevPrice Then
                    ' NEW Momentum confirmation - reset confirmation window  
                    rsiPeriodsAfterCrossover = 0
                    newRSISignal = "MOMENTUM SELL" : newRSIColor = Color.Coral
                    lastRSISignalTime = DateTime.Now

                ElseIf lastRSISignal.Contains("BUY") AndAlso Not lastRSISignal.Contains("MOMENTUM") AndAlso rsiPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for BUY signals
                    If rsiValue < 15 AndAlso Not lastRSISignal.StartsWith("STRONG") Then
                        newRSISignal = "STRONG BUY" : newRSIColor = Color.Lime
                        AppendLog("RSI ↑ STRONG BUY : Deeper oversold", Color.Lime)
                    ElseIf rsiValue < 25 AndAlso lastRSISignal = "WEAK BUY" Then
                        newRSISignal = "BUY" : newRSIColor = Color.LightGreen
                        AppendLog("RSI ↑ BUY : Deeper oversold", Color.Lime)
                    End If

                ElseIf lastRSISignal.Contains("SELL") AndAlso Not lastRSISignal.Contains("MOMENTUM") AndAlso rsiPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for SELL signals
                    If rsiValue > 85 AndAlso Not lastRSISignal.StartsWith("STRONG") Then
                        newRSISignal = "STRONG SELL" : newRSIColor = Color.Red
                        AppendLog("RSI ↑ STRONG SELL : Deeper overbought", Color.Lime)
                    ElseIf rsiValue > 75 AndAlso lastRSISignal = "WEAK SELL" Then
                        newRSISignal = "SELL" : newRSIColor = Color.Orange
                        AppendLog("RSI ↑ SELL : Deeper overbought", Color.Lime)
                    End If
                End If
            End If

            ' ── Stoch Confirmation by Signal Text Matching ──────────────────────
            Dim currentStochSignal As String = lblStoch.Text

            If newRSISignal.Contains("BUY") AndAlso currentStochSignal.Contains("BUY") AndAlso
               rsiValue < 65 AndAlso stochD < 40 Then
                If Not newRSISignal.Contains(" - STOCH OK") Then
                    newRSISignal &= " - STOCH OK"
                    'rsiColor = Color.Cyan
                End If

            ElseIf newRSISignal.Contains("SELL") AndAlso currentStochSignal.Contains("SELL") AndAlso
                rsiValue > 35 AndAlso stochD > 60 Then
                If Not newRSISignal.Contains(" - STOCH OK") Then
                    newRSISignal &= " - STOCH OK"
                    'rsiColor = Color.Cyan
                End If
            End If

            ' Only update if signal changed
            If newRSISignal <> lastRSISignal Then
                lblRSI.Text = newRSISignal
                lblRSI.ForeColor = newRSIColor
                lastRSISignal = newRSISignal
                AppendLog($"RSI: {newRSISignal} (RSI:{rsiValue:F1}, Price:{currentPrice:F2})", Color.Yellow)
            End If

            'To calculate leaning to bias indicator strength:
            If newRSISignal.Contains("WEAK BUY") Then
                score += 1
            ElseIf newRSISignal.Contains("BUY") Then
                score += 2
            ElseIf newRSISignal.Contains("MOMENTUM BUY") Then
                score += 2
            ElseIf newRSISignal.Contains("STRONG BUY") Then
                score += 3
            ElseIf newRSISignal.Contains("WEAK SELL") Then
                score -= 1
            ElseIf newRSISignal.Contains("SELL") Then
                score -= 2
            ElseIf newRSISignal.Contains("MOMENTUM SELL") Then
                score -= 2
            ElseIf newRSISignal.Contains("STRONG SELL") Then
                score -= 3
            End If

            If newRSISignal.Contains("BUY") AndAlso newRSISignal.Contains("STOCH OK") Then
                score += 1
            ElseIf newRSISignal.Contains("SELL") AndAlso newRSISignal.Contains("STOCH OK") Then
                score -= 1
            End If

            ' Increment confirmation window counter
            rsiPeriodsAfterCrossover += 1
            prevRSI = rsiValue : prevPrice = currentPrice
        End If
    End Sub

    Private stochD As Decimal = 0 ' Global variable to hold Stochastic D value

    ' ── Stochastic Section ────────────────────────────────────────────────
    Private Sub UpdateStochastic(quotes As IList(Of Quote))
        '═══════════════════════════════════════════════════════════════════
        ' STOCHASTIC - Hybrid with Signal Confirmation Window
        '═══════════════════════════════════════════════════════════════════
        Static prevK As Decimal = -1, prevD As Decimal = -1
        Static initialized As Boolean = False
        Static lastSignal As String = "-"
        Static lastSignalTime As DateTime = DateTime.MinValue
        Static stochPeriodsAfterCrossover As Integer = 0

        If quotes.Count() < 15 Then
            lblStoch.Text = "INSUFFICIENT DATA"
            lblStoch.ForeColor = Color.Gray
            Return
        End If

        Dim st = quotes.GetStoch(8, 3, 3).LastOrDefault()
        If st IsNot Nothing AndAlso st.Oscillator.HasValue AndAlso st.Signal.HasValue Then
            Dim k = CDec(st.Oscillator.Value)
            Dim d = CDec(st.Signal.Value)
            stochD = d ' Store D value globally for use in RSI confirmation

            ' Initialize on first run
            If Not initialized Then
                prevK = k : prevD = d : initialized = True
                lblStoch.Text = "INITIALIZING." : lblStoch.ForeColor = Color.Gray
                lastSignal = "INITIALIZING."
                Return
            End If

            Dim newSignal As String = lastSignal
            Dim newColor As Color = lblStoch.ForeColor

            'check for startup flag
            If startupFired = False Then
                If k > d Then
                    ' NEW Bullish crossover - reset confirmation window
                    stochPeriodsAfterCrossover = 0
                    If d < 25 Then
                        newSignal = "STRONG BUY - S" : newColor = Color.Lime
                    ElseIf d < 40 Then
                        newSignal = "BUY - S" : newColor = Color.LightGreen
                    Else
                        newSignal = "WEAK BUY - S" : newColor = Color.YellowGreen
                    End If
                    lastSignalTime = DateTime.Now

                ElseIf k < d Then
                    ' NEW Bearish crossover - reset confirmation window
                    stochPeriodsAfterCrossover = 0
                    If d > 75 Then
                        newSignal = "STRONG SELL - S" : newColor = Color.Red
                    ElseIf d > 60 Then
                        newSignal = "SELL - S" : newColor = Color.Orange
                    Else
                        newSignal = "WEAK SELL - S" : newColor = Color.Yellow
                    End If
                    lastSignalTime = DateTime.Now

                ElseIf lastSignal.Contains("BUY") AndAlso stochPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for BUY signals
                    If d < 25 AndAlso Not lastSignal.StartsWith("STRONG") Then
                        newSignal = "STRONG BUY - S" : newColor = Color.Lime
                        AppendLog($"STOCH. ↑ STRONG BUY", Color.Lime)
                    ElseIf d < 40 AndAlso lastSignal = "WEAK BUY" Then
                        newSignal = "BUY - S" : newColor = Color.LightGreen
                        AppendLog("STOCH. ↑ BUY", Color.Lime)
                    End If

                ElseIf lastSignal.Contains("SELL") AndAlso stochPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for SELL signals
                    If d > 75 AndAlso Not lastSignal.StartsWith("STRONG") Then
                        newSignal = "STRONG SELL - S" : newColor = Color.Red
                        AppendLog("STOCH. ↑ STRONG SELL", Color.Lime)
                    ElseIf d > 60 AndAlso lastSignal = "WEAK SELL" Then
                        newSignal = "SELL - S" : newColor = Color.Orange
                        AppendLog("STOCH. ↑ SELL", Color.Lime)
                    End If
                End If
                startupFired = True
            Else
                ' Check for new crossover signals
                If k > d AndAlso prevK <= prevD Then
                    ' NEW Bullish crossover - reset confirmation window
                    stochPeriodsAfterCrossover = 0
                    If d < 25 Then
                        newSignal = "STRONG BUY" : newColor = Color.Lime
                    ElseIf d < 40 Then
                        newSignal = "BUY" : newColor = Color.LightGreen
                    Else
                        newSignal = "WEAK BUY" : newColor = Color.YellowGreen
                    End If
                    lastSignalTime = DateTime.Now

                ElseIf k < d AndAlso prevK >= prevD Then
                    ' NEW Bearish crossover - reset confirmation window
                    stochPeriodsAfterCrossover = 0
                    If d > 75 Then
                        newSignal = "STRONG SELL" : newColor = Color.Red
                    ElseIf d > 60 Then
                        newSignal = "SELL" : newColor = Color.Orange
                    Else
                        newSignal = "WEAK SELL" : newColor = Color.Yellow
                    End If
                    lastSignalTime = DateTime.Now

                ElseIf lastSignal.Contains("BUY") AndAlso stochPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for BUY signals
                    If d < 25 AndAlso Not lastSignal.StartsWith("STRONG") Then
                        newSignal = "STRONG BUY" : newColor = Color.Lime
                        AppendLog($"STOCH. ↑ STRONG BUY", Color.Lime)
                    ElseIf d < 40 AndAlso lastSignal = "WEAK BUY" Then
                        newSignal = "BUY" : newColor = Color.LightGreen
                        AppendLog("STOCH. ↑ BUY", Color.Lime)
                    End If

                ElseIf lastSignal.Contains("SELL") AndAlso stochPeriodsAfterCrossover <= 3 Then
                    ' Allow strength UPGRADES within confirmation window for SELL signals
                    If d > 75 AndAlso Not lastSignal.StartsWith("STRONG") Then
                        newSignal = "STRONG SELL" : newColor = Color.Red
                        AppendLog("STOCH. ↑ STRONG SELL", Color.Lime)
                    ElseIf d > 60 AndAlso lastSignal = "WEAK SELL" Then
                        newSignal = "SELL" : newColor = Color.Orange
                        AppendLog("STOCH. ↑ SELL", Color.Lime)
                    End If
                End If
            End If

            Dim currentRsiSignal As String = lblRSI.Text

            If newSignal.Contains("BUY") AndAlso currentRsiSignal.Contains("BUY") AndAlso
    currentRsiValue < 65 AndAlso d < 40 Then
                If Not newSignal.Contains(" - RSI OK") Then
                    newSignal &= " - RSI OK"
                    'stochColor = Color.Cyan
                End If
            ElseIf newSignal.Contains("SELL") AndAlso currentRsiSignal.Contains("SELL") AndAlso
            currentRsiValue > 35 AndAlso d > 60 Then
                If Not newSignal.Contains(" - RSI OK") Then
                    newSignal &= " - RSI OK"
                    'stochColor = Color.Cyan
                End If
            End If
            ' Only update if signal actually changed
            If newSignal <> lastSignal Then
                lblStoch.Text = newSignal
                lblStoch.ForeColor = newColor
                lastSignal = newSignal
                AppendLog($"Stoch: {newSignal} (K:{k:F1}, D:{d:F1})", Color.Yellow)
            End If

            'To calculate leaning to bias indicator strength:
            If newSignal.Contains("WEAK BUY") Then
                score += 1
            ElseIf newSignal.Contains("BUY") Then
                score += 2
            ElseIf newSignal.Contains("STRONG BUY") Then
                score += 3
            ElseIf newSignal.Contains("WEAK SELL") Then
                score -= 1
            ElseIf newSignal.Contains("SELL") Then
                score -= 2
            ElseIf newSignal.Contains("STRONG SELL") Then
                score -= 3
            End If

            If newSignal.Contains("BUY") AndAlso newSignal.Contains("RSI OK") Then
                score += 1
            ElseIf newSignal.Contains("SELL") AndAlso newSignal.Contains("RSI OK") Then
                score -= 1
            End If

            ' Increment confirmation window counter
            stochPeriodsAfterCrossover += 1
            prevK = k : prevD = d
        End If
    End Sub

    '========================================================================
    '  Evaluate 9-21-50 EMA + Daily VWAP with 3-bar strength upgrades & EMA50 filter
    '========================================================================
    Private Sub EvaluateEmaVwapSignals(quotes As IList(Of Quote))
        If quotes Is Nothing OrElse quotes.Count < 60 Then Exit Sub

        Static lastSignal As String = "-"
        Static lastColor As Color = Color.Gray
        Static signalAge As Integer = 0

        ' Get indicator values
        Dim e9 = quotes.GetEma(9).LastOrDefault()?.Ema
        Dim e21 = quotes.GetEma(21).LastOrDefault()?.Ema
        Dim e50 = quotes.GetEma(50).LastOrDefault()?.Ema
        Dim vwap = quotes.GetVwap().LastOrDefault()?.Vwap
        If e9 Is Nothing OrElse e21 Is Nothing OrElse e50 Is Nothing OrElse vwap Is Nothing Then Exit Sub

        Dim ema9 = e9.Value
        Dim ema21v = e21.Value
        Dim ema50 = e50.Value
        Dim vvw = vwap.Value
        Dim price = quotes.Last().Close

        ' Only proceed if price and EMA9/21 are on the same side of EMA50
        Dim bullishTrend = (price > ema50 AndAlso ema9 > ema21v)
        Dim bearishTrend = (price < ema50 AndAlso ema9 < ema21v)
        If Not (bullishTrend OrElse bearishTrend) Then
            ' Show neutral if outside trend
            If Me.InvokeRequired Then
                Me.Invoke(Sub()
                              lblEMA.Text = "EMA: Neutral Trend"
                              lblEMA.ForeColor = Color.Gray
                              lblVWAP.Text = "VWAP: —"
                              lblVWAP.ForeColor = Color.Gray
                          End Sub)
            Else
                lblEMA.Text = "EMA: Neutral Trend"
                lblEMA.ForeColor = Color.Gray
                lblVWAP.Text = "VWAP: —"
                lblVWAP.ForeColor = Color.Gray
            End If
            Return
        End If

        ' Pre-calc distance bps
        Dim bps As Decimal = Math.Abs((price - ema9) / price) * 10000D

        ' 1) Base EMA9/21 signal
        Dim sig As String = "-"
        Dim col As Color = Color.Gray
        If bullishTrend Then
            Select Case bps
                Case < 10D : sig = "WEAK BUY"
                    col = Color.YellowGreen
                Case < 25D : sig = "BUY"
                    col = Color.LightGreen
                Case Else : sig = "STRONG BUY"
                    col = Color.Lime
            End Select
        ElseIf bearishTrend Then
            Select Case bps
                Case < 10D : sig = "WEAK SELL"
                    col = Color.Yellow
                Case < 25D : sig = "SELL"
                    col = Color.Orange
                Case Else : sig = "STRONG SELL"
                    col = Color.Red
            End Select
        End If

        ' 2) VWAP bias
        Dim bias As String = "-"
        Dim biasCol As Color = Color.Gray
        Dim pctVwap As Decimal = (price - vvw) / vvw * 100D
        If pctVwap > 0D Then
            bias = "BUY BIAS"
            biasCol = Color.LightGreen
        ElseIf pctVwap < 0D Then
            bias = "SELL BIAS"
            biasCol = Color.Orange
        End If

        ' 3) Confirmation
        Dim confirmed = (sig.Contains("BUY") AndAlso bias.Contains("BUY")) _
                 Or (sig.Contains("SELL") AndAlso bias.Contains("SELL"))
        If confirmed AndAlso sig <> "-" Then
            sig &= " – VWAP OK"
            bias &= " – EMA OK"
            col = Color.Cyan
            biasCol = Color.Cyan
        End If

        ' 4) 3-bar confirmation upgrades
        If sig <> lastSignal Then
            lastSignal = sig
            lastColor = col
            signalAge = 0
        ElseIf signalAge < 3 Then
            ' allow deeper-strength upgrades
            If sig.Contains("BUY") Then
                If sig.StartsWith("WEAK") AndAlso bps >= 10D Then
                    sig = If(confirmed, "BUY – VWAP OK", "BUY")
                    col = Color.LightGreen
                ElseIf bps >= 25D Then
                    sig = If(confirmed, "STRONG BUY – VWAP OK", "STRONG BUY")
                    col = Color.Lime
                End If
            ElseIf sig.Contains("SELL") Then
                If sig.StartsWith("WEAK") AndAlso bps >= 10D Then
                    sig = If(confirmed, "SELL – VWAP OK", "SELL")
                    col = Color.Orange
                ElseIf bps >= 25D Then
                    sig = If(confirmed, "STRONG SELL – VWAP OK", "STRONG SELL")
                    col = Color.Red
                End If
            End If
            lastSignal = sig
            lastColor = col
            signalAge += 1
        End If

        'To calculate EMA - leaning to bias indicator strength:
        If sig.Contains("WEAK BUY") Then
            score += 1
        ElseIf sig.Contains("BUY") Then
            score += 2
        ElseIf sig.Contains("STRONG BUY") Then
            score += 3
        ElseIf sig.Contains("WEAK SELL") Then
            score -= 1
        ElseIf sig.Contains("SELL") Then
            score -= 2
        ElseIf sig.Contains("STRONG SELL") Then
            score -= 3
        End If

        If sig.Contains("BUY") AndAlso sig.Contains("VWAP OK") Then
            score += 1
        ElseIf sig.Contains("SELL") AndAlso sig.Contains("VWAP OK") Then
            score -= 1
        End If

        'To calculate VWAP - leaning to bias indicator strength:
        If bias.Contains("BUY BIAS") Then
            score += 1
        ElseIf bias.Contains("SELL BIAS") Then
            score -= 1
        End If

        If bias.Contains("BUY") AndAlso bias.Contains("EMA OK") Then
            score += 1
        ElseIf bias.Contains("SELL") AndAlso bias.Contains("EMA OK") Then
            score -= 1
        End If

        ' 5) UI update
        If Me.InvokeRequired Then
            Me.Invoke(Sub()
                          lblEMA.Text = sig
                          lblEMA.ForeColor = col
                          lblVWAP.Text = bias
                          lblVWAP.ForeColor = biasCol
                      End Sub)
        Else
            lblEMA.Text = sig
            lblEMA.ForeColor = col
            lblVWAP.Text = bias
            lblVWAP.ForeColor = biasCol
        End If

    End Sub

    Private Sub UpdateATR(quotes As IList(Of Quote))
        ' ═══════════════════════════════════════════════════════════════════
        'ATR
        ' ═══════════════════════════════════════════════════════════════════
        ' Compute 7‐period ATR using Skender

        Dim textATR As Integer = Integer.Parse(_autoTradeSettings.txtATR.Text.Trim())
        Dim atrSeries = quotes.GetAtr(textATR)
        Dim atrValue = atrSeries.LastOrDefault()?.Atr

        ' Only proceed if ATR exists
        If atrValue.HasValue Then
            Dim atrText = $"{atrValue.Value:F2}"
            ' Thread‐safe UI update
            If Me.InvokeRequired Then
                Me.Invoke(Sub()
                              lblATR.Text = atrText
                              lblATR.ForeColor = Color.White
                          End Sub)
            Else
                lblATR.Text = atrText
                lblATR.ForeColor = Color.White
            End If
        End If


    End Sub

    '--------------------------------------------------------------------------
    ' Helper to update the two labels on the UI thread
    '--------------------------------------------------------------------------
    Private Sub UpdateEmaVwapLabels(emaText As String, emaClr As Color, vwapText As String, vwapClr As Color)
        lblEMA.Text = emaText
        lblEMA.ForeColor = emaClr
        lblVWAP.Text = vwapText
        lblVWAP.ForeColor = vwapClr
    End Sub



    Private Sub AppendLog(text As String, Optional color As Color = Nothing)
        Try
            ' Check if we're on the UI thread
            If Me.InvokeRequired Then
                ' We're on a background thread - marshal to UI thread
                Me.BeginInvoke(Sub() AppendLog(text, color))
                Return
            End If

            Dim currentTime As DateTime = DateTime.Now
            Dim formattedTime As String = currentTime.ToString("HH:mm:ss")

            ' We're on the UI thread - safe to update control
            txtIndLogs.SelectionStart = txtIndLogs.TextLength
            txtIndLogs.SelectionLength = 0

            ' Apply color if specified
            If color <> Nothing AndAlso color <> Color.Empty Then
                txtIndLogs.SelectionColor = color
            Else
                txtIndLogs.SelectionColor = txtIndLogs.ForeColor
            End If

            txtIndLogs.AppendText(formattedTime & " - " & text & Environment.NewLine)
            txtIndLogs.SelectionColor = txtIndLogs.ForeColor ' Reset color

            ' Auto-scroll to bottom
            txtIndLogs.ScrollToCaret()

        Catch ex As Exception
            ' Fallback to debug output if UI logging fails
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}")
        End Try
    End Sub



    ' ── Cleanup ────────────────────────────────────────────────────────────────
    Private Sub FrmIndicators_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        pollTimer.Stop()
        If client IsNot Nothing AndAlso client.State = WebSocketState.Open Then
            client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait()
        End If
    End Sub

    ' ── Data Models ───────────────────────────────────────────────────────────
    Private Class DeribitCandle
        Public Property ticks As Long
        Public Property open As Decimal
        Public Property high As Decimal
        Public Property low As Decimal
        Public Property close As Decimal
        Public Property volume As Decimal
    End Class

    Private Sub heartbeatTimer_Tick(sender As Object, e As EventArgs)
        redHeartBeat.BackColor = Color.Black
        heartbeatTimer.Stop()
    End Sub


    'BACKTESTING CODE
    '--------------------------------------------------------------------------------------------
    '═══════════════════════════════════════════════════════════════════════
    ' 1. Button handler
    '═══════════════════════════════════════════════════════════════════════
    Private Sub btnBacktest_Click(sender As Object, e As EventArgs) Handles btnBacktest.Click
        Task.Run(AddressOf BacktestSignals)   ' keep UI responsive
    End Sub

    '═══════════════════════════════════════════════════════════════════════
    ' 2. Back-test core
    '═══════════════════════════════════════════════════════════════════════
    Private Sub BacktestSignals()
        '──── parameters ────

        Dim LScore As String = _autoTradeSettings.txtLScore.Text.Trim()
        Dim longThreshold As Integer = 0
        Integer.TryParse(LScore, longThreshold)

        Dim SScore As String = _autoTradeSettings.txtSScore.Text.Trim()
        Dim shortThreshold As Integer = 0
        Integer.TryParse(SScore, shortThreshold)

        Dim ATRString As String = _autoTradeSettings.txtATR.Text.Trim()
        Dim atrPeriod As Integer = 0
        Integer.TryParse(ATRString, atrPeriod)

        Dim ATRLimitString As String = _autoTradeSettings.txtATRLimit.Text.Trim
        Dim atrLimit As Decimal = 0
        If Not String.IsNullOrEmpty(ATRLimitString) Then
            Decimal.TryParse(ATRLimitString, atrLimit)
        End If

        Dim tpATRString As String = _autoTradeSettings.txtTP.Text.Trim()
        Dim tpATR As Decimal = 0
        Decimal.TryParse(tpATRString, tpATR)

        Dim slATRString As String = _autoTradeSettings.txtSL.Text.Trim()
        Dim slATR As Decimal = 0
        Decimal.TryParse(slATRString, slATR)

        Dim lookbackBarsString As String = txtTestTime.Text.Trim()
        Dim lookbackBars As Integer = 0
        Integer.TryParse(lookbackBarsString, lookbackBars)
        ' last 17 h - 1024 is Deribit limit per window request

        '──── pull history ────
        Dim history = SyncLockCopy(ohlcList)
        If history.Count < atrPeriod + 2 Then
            AppendLog("Back-test aborted: not enough bars.", Color.Red)
            Return
        End If
        'history = history.Skip(Math.Max(0, history.Count - lookbackBars)).ToList()
        Dim formLoadHistory = history.Skip(formLoadOHLCIndex).ToList()

        If lookbackBars > 0 AndAlso lookbackBars < formLoadHistory.Count Then
            formLoadHistory =
            formLoadHistory.Skip(formLoadHistory.Count - lookbackBars).ToList()
        End If
        If formLoadHistory.Count < atrPeriod + 2 Then
            AppendLog("Insufficient data after form-load filter.", Color.Red)
            Return
        End If

        'Dim atrSeries = history.GetAtr(atrPeriod).ToList()
        Dim atrSeries = formLoadHistory.GetAtr(atrPeriod).ToList()

        '──── stats ────
        Dim trades%, longWins%, longLoss%, shortWins%, shortLoss%
        Dim netPL As Decimal = 0D
        Dim filteredBars As Integer = 0  ' Track filtered candles

        For i As Integer = atrPeriod To formLoadHistory.Count - 2

            Dim currentATR As Decimal = atrSeries(i).Atr.GetValueOrDefault(0)

            If atrLimit > 0 AndAlso currentATR < atrLimit Then
                filteredBars += 1
                'AppendLog($"Bar {i}: ATR {currentATR:F2} below limit {atrLimit:F2} - SKIPPED", Color.Gray)
                Continue For  ' Skip this candle
            End If

            Dim window = formLoadHistory.Take(i + 1).ToList()

            ' fire your live indicator subs → labels + global score
            score = 0
            Me.Invoke(Sub()
                          UpdateDmi(window)
                          UpdateMacd(window)
                          UpdateRsi(window)
                          UpdateStochastic(window)
                          EvaluateEmaVwapSignals(window)
                      End Sub)
            Dim barScore As Integer = score     ' capture
            score = 0                           ' clear

            Dim entrySide As String = Nothing
            If barScore >= longThreshold Then
                entrySide = "LONG"
            ElseIf barScore <= shortThreshold Then
                entrySide = "SHORT"
            End If
            If entrySide Is Nothing Then Continue For

            trades += 1
            Dim entryPrice = window.Last().Close
            Dim atr = atrSeries(i).Atr.GetValueOrDefault()

            Dim tp As Decimal, sl As Decimal
            If entrySide = "LONG" Then
                tp = entryPrice + tpATR * atr
                sl = entryPrice - slATR * atr
            Else                               ' SHORT
                tp = entryPrice - tpATR * atr  ' profit when price falls
                sl = entryPrice + slATR * atr
            End If

            '──── forward scan for exit ────
            Dim pl As Decimal = 0D : Dim hitTP As Boolean = False
            For j As Integer = i + 1 To formLoadHistory.Count - 1
                Dim hi = formLoadHistory(j).High, lo = formLoadHistory(j).Low
                If entrySide = "LONG" Then
                    If hi >= tp Then pl = tp - entryPrice : hitTP = True : Exit For
                    If lo <= sl Then pl = sl - entryPrice : Exit For
                Else ' SHORT
                    If lo <= tp Then pl = entryPrice - tp : hitTP = True : Exit For
                    If hi >= sl Then pl = entryPrice - sl : Exit For
                End If
            Next

            ' if neither TP nor SL hit, close at last bar
            If pl = 0D AndAlso i < formLoadHistory.Count - 2 Then
                pl = If(entrySide = "LONG",
                    formLoadHistory.Last().Close - entryPrice,
                    entryPrice - formLoadHistory.Last().Close)
            End If

            If entrySide = "LONG" Then
                If pl >= 0D Then longWins += 1 Else longLoss += 1
            Else
                If pl >= 0D Then shortWins += 1 Else shortLoss += 1
            End If
            netPL += pl
        Next

        '──── results ────
        Dim longTrades = longWins + longLoss
        Dim shortTrades = shortWins + shortLoss
        Dim winRateLong = If(longTrades > 0, longWins * 100D / longTrades, 0D)
        Dim winRateShort = If(shortTrades > 0, shortWins * 100D / shortTrades, 0D)

        AppendLog($"──── Back-test {lookbackBars / 60:F2}h  (1-min) ────", Color.Cyan)
        AppendLog($"ATR Filter: {If(atrLimit > 0, $"≥{atrLimit:F2}", "DISABLED")}", Color.Yellow)
        AppendLog($"Filtered Bars: {filteredBars} ({(filteredBars * 100D) / (formLoadHistory.Count - atrPeriod):F1}%)", Color.Gray)
        AppendLog($"Long Trades: {longTrades} | Wins: {longWins} | Losses: {longLoss} | Win%: {winRateLong:F1}", Color.Cyan)
        AppendLog($"Short Trades: {shortTrades} | Wins: {shortWins} | Losses: {shortLoss} | Win%: {winRateShort:F1}", Color.Cyan)
        AppendLog($"Total Trades: {trades}", Color.Cyan)
        AppendLog($"Net P&L: {netPL:F2}", Color.Cyan)
    End Sub

    Private Sub btnATR_Click(sender As Object, e As EventArgs) Handles btnATR.Click
        Dim ATRInt, TPInt, SLInt As Decimal
        Dim TPResult, SLResult As Integer
        Dim ATRString = lblATR.Text
        Dim TPString = _autoTradeSettings.txtTP.Text
        Dim SLString = _autoTradeSettings.txtSL.Text

        Decimal.TryParse(ATRString, ATRInt)

        Decimal.TryParse(TPString, TPInt)
        TPResult = CInt(ATRInt * TPInt)

        Decimal.TryParse(SLString, SLInt)
        SLResult = CInt(ATRInt * SLInt)

        frmMainPageV2.txtTakeProfit.Text = TPResult.ToString
        frmMainPageV2.txtTrigger.Text = SLResult.ToString

        AppendLog($"ATR Pasted: TP:{TPResult} SL:{SLResult}", Color.Cyan)


    End Sub

    ' Add to frmIndicators
    Private Sub btnAutoTrade_Click(sender As Object, e As EventArgs) Handles btnAutoTrade.Click
        enableAutoTrading = Not enableAutoTrading

        If enableAutoTrading Then
            btnAutoTrade.Text = "AUTO: ON"
            btnAutoTrade.BackColor = Color.LimeGreen
            AppendLog("Automated trading ENABLED", Color.Green)
        Else
            btnAutoTrade.Text = "AUTO: OFF"
            btnAutoTrade.BackColor = Color.Red
            AppendLog("Automated trading DISABLED", Color.Red)
        End If
    End Sub

    Private Sub btnAutoTradeSettings_Click(sender As Object, e As EventArgs) Handles btnAutoTradeSettings.Click
        If _autoTradeSettings.Visible Then
            _autoTradeSettings.Hide()
        Else
            '_autoTradeSettings.Location = New Point(Me.Right + 6, Me.Top)
            _autoTradeSettings.Show()
            _autoTradeSettings.BringToFront()
            ' StickToHost() will be called automatically via the Load event
        End If
    End Sub

    ' Clean up when frmIndicators closes
    Private Sub FrmIndicators_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        If _autoTradeSettings IsNot Nothing AndAlso Not _autoTradeSettings.IsDisposed Then
            _autoTradeSettings.Close()
        End If
    End Sub
End Class