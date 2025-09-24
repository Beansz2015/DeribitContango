Imports System.Net.WebSockets
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json.Linq

Public Class frmContangoMain

#Region "Core Infrastructure"

    ' WebSocket connection components
    Private webSocketClient As ClientWebSocket
    Private cancellationTokenSource As CancellationTokenSource
    Private lastMessageTime As DateTime
    Private keepAliveTimer As System.Timers.Timer

    ' Authentication
    Private refreshToken As String = Nothing
    Private refreshTokenExpiryTime As DateTime = DateTime.MinValue
    Private Const ClientId As String = "YOUR_CLIENT_ID"
    Private Const ClientSecret As String = "YOUR_CLIENT_SECRET"

    ' Core components
    Private rateLimiter As DeribitRateLimiter
    Private contangoDatabase As ContangoDatabase
    Private basisMonitor As ContangoBasisMonitor
    Private positionManager As ContangoPositionManager

    ' Logging
    Private logLock As New Object()

#End Region

#Region "Market Data & Trading State"

    Private currentBTCSpotPrice As Decimal
    Private currentWeeklyFuturesPrice As Decimal
    Private availableWeeklyContracts As List(Of String)
    Private currentWeeklyContract As String = ""

    Private isContangoPositionActive As Boolean = False
    Private lastBasisUpdate As DateTime = DateTime.MinValue

#End Region

#Region "Form Events"

    Private Async Sub frmContangoMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            ' Initialize components
            InitializeComponents()

            ' Setup UI
            InitializeUI()

            AppendLog("Initializing DeribitContango...", Color.Blue)

            ' Connect to Deribit asynchronously (proper await)
            Await InitializeWebSocketConnection()

            AppendLog("DeribitContango initialized successfully", Color.Green)

        Catch ex As Exception
            AppendLog($"Initialization error: {ex.Message}", Color.Red)
        End Try
    End Sub



    Private Sub frmContangoMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        Try
            ' Clean shutdown
            cancellationTokenSource?.Cancel()
            If webSocketClient?.State = WebSocketState.Open Then
                webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Application closing", CancellationToken.None).Wait(1000)
            End If
            keepAliveTimer?.Stop()

            AppendLog("Application shutdown complete", Color.Blue)

        Catch ex As Exception
            AppendLog($"Shutdown error: {ex.Message}", Color.Red)
        End Try
    End Sub

#End Region

#Region "Control Event Handlers"

    Private Async Sub btnExecuteCashCarry_Click(sender As Object, e As EventArgs) Handles btnExecuteCashCarry.Click
        Try
            btnExecuteCashCarry.Enabled = False
            AppendLog("Executing cash-carry trade...", Color.Blue)

            ' Get parameters
            Dim positionSize As Decimal = nudPositionSize.Value
            Dim minBasisThreshold As Decimal = nudMinBasisThreshold.Value

            ' Validate current basis
            Dim currentBasis As Decimal = basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice)

            If currentBasis < minBasisThreshold Then
                AppendLog($"Current basis {currentBasis:P3} below threshold {minBasisThreshold:P3}", Color.Yellow)
                btnExecuteCashCarry.Enabled = True
                Return
            End If

            ' Execute trade (implementation in Phase 6)
            Dim success As Boolean = Await ExecuteCashCarryTrade(positionSize)

            If success Then
                isContangoPositionActive = True
                positionManager.OpenCashCarryPosition(currentBTCSpotPrice, currentWeeklyFuturesPrice, positionSize, currentWeeklyContract, DateTime.Now.AddDays(7))

                ' Update UI
                btnRollPosition.Enabled = True
                btnClosePosition.Enabled = True
                UpdatePositionDisplay()

                AppendLog($"Cash-carry position opened: {positionSize} BTC at {currentBasis:P3} basis", Color.Green)
            Else
                AppendLog("Cash-carry execution failed", Color.Red)
                btnExecuteCashCarry.Enabled = True
            End If

        Catch ex As Exception
            AppendLog($"Cash-carry error: {ex.Message}", Color.Red)
            btnExecuteCashCarry.Enabled = True
        End Try
    End Sub

    Private Async Sub btnRollPosition_Click(sender As Object, e As EventArgs) Handles btnRollPosition.Click
        Try
            btnRollPosition.Enabled = False
            AppendLog("Rolling position to next weekly contract...", Color.Blue)

            ' Check if rolling is profitable
            Dim nextBasis As Decimal = 0.003D ' Placeholder - will be calculated from next weekly price

            If nextBasis >= nudMinBasisThreshold.Value Then
                ' Execute roll (implementation in Phase 6)
                Await ExecuteRollingTrade()
                AppendLog($"Position rolled successfully. New basis: {nextBasis:P3}", Color.Green)
            Else
                AppendLog($"Rolling not profitable. Next basis: {nextBasis:P3}", Color.Yellow)
            End If

            btnRollPosition.Enabled = True

        Catch ex As Exception
            AppendLog($"Rolling error: {ex.Message}", Color.Red)
            btnRollPosition.Enabled = True
        End Try
    End Sub

    Private Async Sub btnClosePosition_Click(sender As Object, e As EventArgs) Handles btnClosePosition.Click
        Try
            btnClosePosition.Enabled = False
            AppendLog("Closing contango position...", Color.Blue)

            ' Close position (implementation in Phase 6)
            Dim success As Boolean = Await CloseContangoPosition()

            If success Then
                Dim completedTrade = positionManager.CloseCashCarryPosition(currentBTCSpotPrice, currentWeeklyFuturesPrice)
                If completedTrade IsNot Nothing Then
                    ' Save to database
                    contangoDatabase.SaveContangoTrade(completedTrade)
                    AppendLog($"Position closed. P&L: ${completedTrade.RealizedPnL:F2}", Color.Green)
                End If

                isContangoPositionActive = False
                UpdatePositionDisplay()

                ' Re-enable entry button
                btnExecuteCashCarry.Enabled = True
                btnRollPosition.Enabled = False
            Else
                AppendLog("Position close failed", Color.Red)
                btnClosePosition.Enabled = True
            End If

        Catch ex As Exception
            AppendLog($"Close position error: {ex.Message}", Color.Red)
            btnClosePosition.Enabled = True
        End Try
    End Sub

    Private Sub btnClearLogs_Click(sender As Object, e As EventArgs) Handles btnClearLogs.Click
        txtLogs.Clear()
        AppendLog("Logs cleared", Color.Gray)
    End Sub

#End Region

#Region "Core Infrastructure Methods"

    Private Sub InitializeComponents()
        ' Initialize rate limiter
        rateLimiter = New DeribitRateLimiter(50, 10)

        ' Initialize database
        contangoDatabase = New ContangoDatabase()

        ' Initialize contango components
        basisMonitor = New ContangoBasisMonitor()
        positionManager = New ContangoPositionManager()

        ' Initialize collections
        availableWeeklyContracts = New List(Of String)

        ' Setup timers
        keepAliveTimer = New System.Timers.Timer(30000)
        AddHandler keepAliveTimer.Elapsed, AddressOf KeepAliveTimer_Elapsed

        ' Setup UI update timer
        Dim uiTimer As New System.Windows.Forms.Timer With {.Interval = 1000}
        AddHandler uiTimer.Tick, AddressOf UpdateUI_Tick
        uiTimer.Start()
    End Sub

    Private Sub InitializeUI()
        ' Update labels
        lblConnectionStatus.Text = "Status: Connecting..."
        lblConnectionStatus.ForeColor = Color.Orange
        lblSpotPrice.Text = "$0.00"
        lblWeeklyFuturesPrice.Text = "$0.00"
        lblBasisSpread.Text = "0.000%"
        lblAnnualizedReturn.Text = "0.0%"
        lblRateLimit.Text = "Rate: 0/50"
        lblLastUpdate.Text = "Last Update: --:--:--"

        ' Disable trading buttons initially
        btnExecuteCashCarry.Enabled = False
        btnRollPosition.Enabled = False
        btnClosePosition.Enabled = False

        UpdatePositionDisplay()
    End Sub

    Private Sub UpdatePositionDisplay()
        If positionManager.IsPositionActive Then
            lblPositionStatus.Text = positionManager.GetPositionSummary()
            lblUnrealizedPnL.Text = $"Unrealized P&L: ${positionManager.CalculateUnrealizedPnL(currentBTCSpotPrice, currentWeeklyFuturesPrice):F2}"
            lblDaysToExpiry.Text = $"Days to Expiry: {positionManager.DaysToExpiry}"
        Else
            lblPositionStatus.Text = "No active position"
            lblUnrealizedPnL.Text = "Unrealized P&L: $0.00"
            lblDaysToExpiry.Text = "Days to Expiry: --"
        End If
    End Sub

    Private Sub UpdateUI_Tick(sender As Object, e As EventArgs)
        Try
            ' Update connection status
            If webSocketClient?.State = WebSocketState.Open Then
                lblConnectionStatus.Text = "Status: Connected"
                lblConnectionStatus.ForeColor = Color.Green
            Else
                lblConnectionStatus.Text = "Status: Disconnected"
                lblConnectionStatus.ForeColor = Color.Red
            End If

            ' Update rate limit
            lblRateLimit.Text = $"Rate: {rateLimiter.RequestsInWindow}/{50}"

            ' Update last update time
            lblLastUpdate.Text = $"Last Update: {lastBasisUpdate:HH:mm:ss}"

            ' Update position display
            UpdatePositionDisplay()

        Catch ex As Exception
            ' Ignore UI update errors
        End Try
    End Sub

    Private Async Function InitializeWebSocketConnection() As Task
        Try
            webSocketClient = New ClientWebSocket()
            cancellationTokenSource = New CancellationTokenSource()

            Dim uri As New Uri("wss://www.deribit.com/ws/api/v2")
            Await webSocketClient.ConnectAsync(uri, cancellationTokenSource.Token)

            AppendLog("WebSocket connected successfully", Color.Green)

            ' Start authentication
            Await AuthorizeWebSocketConnection()

            ' Start message handling
            Dim messageTask = HandleWebSocketMessages()

            ' Start keep-alive
            keepAliveTimer.Start()

        Catch ex As Exception
            AppendLog($"WebSocket connection failed: {ex.Message}", Color.Red)
        End Try
    End Function

    Private Async Function AuthorizeWebSocketConnection() As Task
        Try
            If Not rateLimiter.CanMakeRequest() Then
                AppendLog("Rate limit exceeded - waiting", Color.Yellow)
                Return
            End If

            Dim authRequest As New JObject From {
                {"jsonrpc", "2.0"},
                {"id", Guid.NewGuid().ToString()},
                {"method", "public/auth"},
                {"params", New JObject From {
                    {"grant_type", "client_credentials"},
                    {"client_id", ClientId},
                    {"client_secret", ClientSecret}
                }}
            }

            Await SendWebSocketMessage(authRequest.ToString())
            rateLimiter.RecordRequest()

        Catch ex As Exception
            AppendLog($"Authentication failed: {ex.Message}", Color.Red)
        End Try
    End Function

    ' Additional WebSocket and trading methods will be added in Phase 6
    ' Placeholder methods for now:

    Private Async Function ExecuteCashCarryTrade(positionSize As Decimal) As Task(Of Boolean)
        ' Implementation in Phase 6
        Await Task.Delay(1000) ' Simulate execution time
        Return True
    End Function

    Private Async Function ExecuteRollingTrade() As Task
        ' Implementation in Phase 6
        Await Task.Delay(1000)
    End Function

    Private Async Function CloseContangoPosition() As Task(Of Boolean)
        ' Implementation in Phase 6
        Await Task.Delay(1000)
        Return True
    End Function

#End Region

#Region "Logging Methods"

    Private Sub AppendLog(message As String, color As Color)
        If Me.InvokeRequired Then
            Me.Invoke(Sub() AppendLog(message, color))
            Return
        End If

        SyncLock logLock
            Try
                Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss")
                Dim logEntry As String = $"[{timestamp}] {message}{Environment.NewLine}"

                txtLogs.SelectionStart = txtLogs.TextLength
                txtLogs.SelectionLength = 0
                txtLogs.SelectionColor = color
                txtLogs.AppendText(logEntry)
                txtLogs.SelectionColor = txtLogs.ForeColor
                txtLogs.ScrollToCaret()

                ' Limit log size
                If txtLogs.Lines.Length > 1000 Then
                    Dim lines As String() = txtLogs.Lines
                    txtLogs.Lines = lines.Skip(200).ToArray()
                End If

                ' Save to database
                contangoDatabase?.LogPerformance(message, "INFO")

            Catch
                ' Ignore logging errors
            End Try
        End SyncLock
    End Sub

    Private Sub KeepAliveTimer_Elapsed(sender As Object, e As System.Timers.ElapsedEventArgs)
        If DateTime.Now.Subtract(lastMessageTime).TotalSeconds > 60 Then
            ' Use ContinueWith for fire-and-forget async
            SendKeepAlive().ContinueWith(
            Sub(task)
                If task.IsFaulted Then
                    Me.Invoke(Sub() AppendLog($"Keep-alive error: {task.Exception?.GetBaseException()?.Message}", Color.Orange))
                End If
            End Sub)
        End If
    End Sub


    Private Async Function SendKeepAlive() As Task
        ' Implementation will be added in Phase 6
        Await Task.Delay(100)
    End Function

    Private Async Function SendWebSocketMessage(message As String) As Task
        Try
            If webSocketClient?.State <> WebSocketState.Open Then
                AppendLog("WebSocket not connected - cannot send message", Color.Red)
                Return
            End If

            If Not rateLimiter.CanMakeRequest() Then
                Dim waitTime = rateLimiter.GetWaitTime()
                If waitTime.TotalSeconds > 0 Then
                    AppendLog($"Rate limit reached, waiting {waitTime.TotalSeconds:F1}s", Color.Yellow)
                    Await Task.Delay(waitTime)
                End If
            End If

            Dim buffer As Byte() = Encoding.UTF8.GetBytes(message)
            Await webSocketClient.SendAsync(New ArraySegment(Of Byte)(buffer), WebSocketMessageType.Text, True, cancellationTokenSource.Token)

            rateLimiter.RecordRequest()

        Catch ex As Exception
            AppendLog($"Error sending WebSocket message: {ex.Message}", Color.Red)
        End Try
    End Function


    Private Async Function HandleWebSocketMessages() As Task
        Dim buffer(8192) As Byte

        While webSocketClient.State = WebSocketState.Open AndAlso Not cancellationTokenSource.Token.IsCancellationRequested
            Try
                Dim result = Await webSocketClient.ReceiveAsync(New ArraySegment(Of Byte)(buffer), cancellationTokenSource.Token)

                If result.MessageType = WebSocketMessageType.Text Then
                    Dim message As String = Encoding.UTF8.GetString(buffer, 0, result.Count)
                    ProcessWebSocketMessage(message)
                    lastMessageTime = DateTime.Now
                ElseIf result.MessageType = WebSocketMessageType.Close Then
                    AppendLog("WebSocket connection closed by server", Color.Orange)
                    Exit While
                End If

            Catch ex As Exception When TypeOf ex Is OperationCanceledException
                Exit While
            Catch ex As Exception
                AppendLog($"WebSocket receive error: {ex.Message}", Color.Red)
                Exit While
            End Try
        End While
    End Function

    Private Sub ProcessWebSocketMessage(message As String)
        Try
            Dim json As JObject = JObject.Parse(message)

            ' Handle authentication responses
            If json("result")?("access_token") IsNot Nothing Then
                HandleAuthenticationResponse(json)
            End If

            ' Handle subscription confirmations
            If json("method")?.ToString() = "subscription" Then
                HandleSubscriptionMessage(json)
            End If

            ' Handle market data updates
            If json("params")?("channel") IsNot Nothing Then
                HandleMarketDataUpdate(json)
            End If

            ' Handle heartbeat/ping responses
            If json("method")?.ToString() = "heartbeat" Then
                HandleHeartbeatRequest(json)
            End If

            ' Handle account summary responses
            If json("id")?.ToString() = "999" Then
                HandleAccountSummaryResponse(json)
            End If

            lastMessageTime = DateTime.Now

        Catch ex As Exception
            AppendLog($"Error processing message: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub HandleAuthenticationResponse(json As JObject)
        Try
            If json("result")?("access_token") IsNot Nothing Then
                refreshToken = json("result")("refresh_token")?.ToString()

                If DateTime.TryParse(json("result")("expires_in")?.ToString(), refreshTokenExpiryTime) Then
                    refreshTokenExpiryTime = DateTime.Now.AddSeconds(CDbl(json("result")("expires_in")))
                End If

                AppendLog("Authentication successful", Color.Green)

                ' Subscribe to market data
                Task.Run(Async Function()
                             Try
                                 Await SubscribeToMarketData()
                             Catch ex As Exception
                                 Me.Invoke(Sub() AppendLog($"Subscription failed: {ex.Message}", Color.Red))
                             End Try
                             Return Nothing
                         End Function)
            Else
                AppendLog($"Authentication failed: {json("error")?.ToString()}", Color.Red)
            End If

        Catch ex As Exception
            AppendLog($"Auth response error: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Async Function SubscribeToMarketData() As Task
        Try
            ' Subscribe to BTC spot price (using index price as proxy)
            Dim spotSubscription As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "public/subscribe"},
            {"params", New JObject From {
                {"channels", New JArray({"deribit_price_index.btc_usd"})}
            }}
        }

            Await SendWebSocketMessage(spotSubscription.ToString())

            ' Subscribe to current weekly futures
            Await SubscribeToWeeklyFutures()

            ' Subscribe to user portfolio for balance updates
            Dim portfolioSubscription As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "private/subscribe"},
            {"params", New JObject From {
                {"channels", New JArray({"user.portfolio.btc"})}
            }}
        }

            Await SendWebSocketMessage(portfolioSubscription.ToString())

            AppendLog("Market data subscriptions active", Color.Green)

        Catch ex As Exception
            AppendLog($"Subscription error: {ex.Message}", Color.Red)
        End Try
    End Function

    Private Async Function SubscribeToWeeklyFutures() As Task
        Try
            ' Get list of available instruments
            Dim instrumentsRequest As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "public/get_instruments"},
            {"params", New JObject From {
                {"currency", "BTC"},
                {"kind", "future"},
                {"expired", False}
            }}
        }

            Await SendWebSocketMessage(instrumentsRequest.ToString())

            ' For now, subscribe to a known weekly pattern (will be dynamic later)
            Dim currentWeekly = GetCurrentWeeklyContract()
            If Not String.IsNullOrEmpty(currentWeekly) Then
                Dim weeklySubscription As New JObject From {
                {"jsonrpc", "2.0"},
                {"id", Guid.NewGuid().ToString()},
                {"method", "public/subscribe"},
                {"params", New JObject From {
                    {"channels", New JArray({$"ticker.{currentWeekly}.raw"})}
                }}
            }

                Await SendWebSocketMessage(weeklySubscription.ToString())
                currentWeeklyContract = currentWeekly
                AppendLog($"Subscribed to weekly contract: {currentWeekly}", Color.Cyan)
            End If

        Catch ex As Exception
            AppendLog($"Weekly subscription error: {ex.Message}", Color.Red)
        End Try
    End Function

    Private Function GetCurrentWeeklyContract() As String
        Try
            ' Calculate next Friday's date
            Dim today As DateTime = DateTime.Now
            Dim daysUntilFriday As Integer = ((DayOfWeek.Friday - today.DayOfWeek + 7) Mod 7)
            If daysUntilFriday = 0 AndAlso today.Hour >= 8 Then ' After 8 AM UTC on Friday
                daysUntilFriday = 7 ' Next Friday
            End If

            Dim nextFriday As DateTime = today.AddDays(daysUntilFriday)

            ' Format as BTC-DDMMMYY (e.g., BTC-04OCT25)
            Dim contractName As String = $"BTC-{nextFriday:ddMMMyy}".ToUpper()

            Return contractName

        Catch ex As Exception
            AppendLog($"Error calculating weekly contract: {ex.Message}", Color.Red)
            Return "BTC-27SEP25" ' Fallback
        End Try
    End Function

    Private Sub HandleMarketDataUpdate(json As JObject)
        Try
            Dim channel As String = json("params")?("channel")?.ToString()
            Dim data As JObject = json("params")?("data")

            If channel IsNot Nothing AndAlso data IsNot Nothing Then
                ' Handle BTC spot price (index price)
                If channel = "deribit_price_index.btc_usd" Then
                    If Decimal.TryParse(data("price")?.ToString(), currentBTCSpotPrice) Then
                        Me.Invoke(Sub()
                                      lblSpotPrice.Text = $"${currentBTCSpotPrice:N2}"
                                      lblSpotPrice.ForeColor = Color.Blue
                                  End Sub)
                    End If
                End If

                ' Handle weekly futures price
                If channel.Contains("ticker.BTC-") AndAlso channel.Contains(".raw") Then
                    If Decimal.TryParse(data("last_price")?.ToString(), currentWeeklyFuturesPrice) Then
                        Me.Invoke(Sub()
                                      lblWeeklyFuturesPrice.Text = $"${currentWeeklyFuturesPrice:N2}"
                                      lblWeeklyFuturesPrice.ForeColor = Color.Purple
                                  End Sub)

                        ' Update basis spread
                        UpdateBasisSpread()

                        ' Save basis data to database
                        contangoDatabase.SaveBasisData(currentBTCSpotPrice, currentWeeklyFuturesPrice,
                                                 basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice),
                                                 currentWeeklyContract)
                    End If
                End If

                ' Handle portfolio updates
                If channel = "user.portfolio.btc" Then
                    HandlePortfolioUpdate(data)
                End If
            End If

            lastBasisUpdate = DateTime.Now

        Catch ex As Exception
            AppendLog($"Market data error: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub UpdateBasisSpread()
        Try
            If currentBTCSpotPrice > 0 AndAlso currentWeeklyFuturesPrice > 0 Then
                Dim basisSpread As Decimal = basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice)
                Dim annualizedReturn As Decimal = basisMonitor.GetAnnualizedBasisReturn(basisSpread)

                Me.Invoke(Sub()
                              lblBasisSpread.Text = $"{basisSpread:P3}"
                              lblAnnualizedReturn.Text = $"{annualizedReturn:P1}"

                              ' Color coding based on profitability
                              If basisSpread >= CDec(nudMinBasisThreshold.Value) Then
                                  lblBasisSpread.ForeColor = Color.Green
                                  btnExecuteCashCarry.Enabled = Not isContangoPositionActive
                                  btnExecuteCashCarry.BackColor = Color.LightGreen
                              Else
                                  lblBasisSpread.ForeColor = Color.Red
                                  btnExecuteCashCarry.Enabled = False
                                  btnExecuteCashCarry.BackColor = Color.LightGray
                              End If

                              ' Update trend indicator
                              Dim trend = basisMonitor.GetBasisTrend(30) ' 30-minute trend
                              Select Case trend
                                  Case "EXPANDING"
                                      lblBasisSpread.BackColor = Color.LightGreen
                                  Case "COMPRESSING"
                                      lblBasisSpread.BackColor = Color.LightCoral
                                  Case Else
                                      lblBasisSpread.BackColor = Color.Transparent
                              End Select
                          End Sub)
            End If

        Catch ex As Exception
            AppendLog($"Basis calculation error: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Async Function ExecuteCashCarryTrade(positionSize As Decimal) As Task(Of Boolean)
        Try
            AppendLog($"Executing cash-carry trade: {positionSize} BTC", Color.Blue)

            ' Validate current market conditions
            If currentBTCSpotPrice <= 0 OrElse currentWeeklyFuturesPrice <= 0 Then
                AppendLog("Invalid market prices - cannot execute trade", Color.Red)
                Return False
            End If

            Dim currentBasis As Decimal = basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice)
            If currentBasis < CDec(nudMinBasisThreshold.Value) Then
                AppendLog($"Basis {currentBasis:P3} below threshold {nudMinBasisThreshold.Value:P3}", Color.Red)
                Return False
            End If

            ' Execute spot purchase (market order)
            Dim spotSuccess As Boolean = Await ExecuteSpotPurchase(positionSize)
            If Not spotSuccess Then
                AppendLog("Spot purchase failed", Color.Red)
                Return False
            End If

            ' Execute futures short (market order)
            Dim futuresSuccess As Boolean = Await ExecuteFuturesShort(positionSize, currentWeeklyContract)
            If Not futuresSuccess Then
                AppendLog("Futures short failed - attempting to close spot position", Color.Red)
                ' TODO: Implement emergency spot close
                Return False
            End If

            AppendLog($"Cash-carry trade executed successfully at {currentBasis:P3} basis", Color.Green)
            Return True

        Catch ex As Exception
            AppendLog($"Cash-carry execution error: {ex.Message}", Color.Red)
            Return False
        End Try
    End Function

    Private Async Function ExecuteSpotPurchase(amount As Decimal) As Task(Of Boolean)
        Try
            ' For Deribit, we'll use BTC-PERPETUAL as proxy for spot
            Dim orderPayload As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "private/buy"},
            {"params", New JObject From {
                {"instrument_name", "BTC-PERPETUAL"},
                {"amount", amount * currentBTCSpotPrice}, ' Convert to USD
                {"type", "market"},
                {"label", "ContangoSpotBuy"}
            }}
        }

            Await SendWebSocketMessage(orderPayload.ToString())
            AppendLog($"Spot buy order sent: {amount} BTC", Color.Green)

            ' TODO: Wait for order confirmation
            Await Task.Delay(2000) ' Temporary delay

            Return True

        Catch ex As Exception
            AppendLog($"Spot purchase error: {ex.Message}", Color.Red)
            Return False
        End Try
    End Function

    Private Async Function ExecuteFuturesShort(amount As Decimal, contract As String) As Task(Of Boolean)
        Try
            Dim orderPayload As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "private/sell"},
            {"params", New JObject From {
                {"instrument_name", contract},
                {"amount", amount * currentWeeklyFuturesPrice}, ' Convert to USD
                {"type", "market"},
                {"label", "ContangoFuturesShort"}
            }}
        }

            Await SendWebSocketMessage(orderPayload.ToString())
            AppendLog($"Futures short order sent: {amount} BTC on {contract}", Color.Green)

            ' TODO: Wait for order confirmation
            Await Task.Delay(2000) ' Temporary delay

            Return True

        Catch ex As Exception
            AppendLog($"Futures short error: {ex.Message}", Color.Red)
            Return False
        End Try
    End Function

    Private Async Function SendKeepAlive() As Task
        Try
            If webSocketClient?.State <> WebSocketState.Open Then Return

            Dim pingRequest As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "public/ping"}
        }

            Await SendWebSocketMessage(pingRequest.ToString())

        Catch ex As Exception
            AppendLog($"Keep-alive error: {ex.Message}", Color.Orange)
        End Try
    End Function

    Private Sub HandleHeartbeatRequest(json As JObject)
        Try
            If json("method")?.ToString() = "heartbeat" Then
                ' Send heartbeat response
                Dim response As New JObject From {
                {"jsonrpc", "2.0"},
                {"id", json("id")},
                {"method", "public/ping"}
            }

                Task.Run(Async Function()
                             Await SendWebSocketMessage(response.ToString())
                             Return Nothing
                         End Function)
            End If

        Catch ex As Exception
            AppendLog($"Heartbeat error: {ex.Message}", Color.Red)
        End Try
    End Sub

#End Region

End Class
