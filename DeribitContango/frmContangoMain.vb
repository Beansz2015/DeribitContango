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
    Private Const ClientId As String = "YZCnDmWo"
    Private Const ClientSecret As String = "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA"

    ' Core components
    Private rateLimiter As DeribitRateLimiter
    Private contangoDatabase As ContangoDatabase
    Private basisMonitor As ContangoBasisMonitor
    Private positionManager As ContangoPositionManager
    Private performanceMonitor As New PerformanceMonitor

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

#Region "Connection Management & Resilience"

    Private reconnectionTimer As System.Timers.Timer
    Private connectionHealthTimer As System.Timers.Timer
    Private isReconnecting As Boolean = False
    Private reconnectAttempts As Integer = 0
    Private maxReconnectAttempts As Integer = 50
    Private baseReconnectDelay As Integer = 2000 ' Start with 2 seconds
    Private maxReconnectDelay As Integer = 300000 ' Max 5 minutes
    Private lastHeartbeatTime As DateTime = DateTime.Now
    Private connectionLostTime As DateTime
    Private isShuttingDown As Boolean = False

#End Region


#Region "Form Events"

    Private Async Sub frmContangoMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            performanceMonitor.StartTime = DateTime.Now

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
                ' CORRECT:
                Dim calculatedExpiry As DateTime = CalculateContractExpiry(currentWeeklyContract)
                positionManager.OpenCashCarryPosition(currentBTCSpotPrice, currentWeeklyFuturesPrice, positionSize, currentWeeklyContract, calculatedExpiry)

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

            ' Calculate hours to expiry using stored expiry date
            Dim hoursToExpiry As Double = 0

            If positionManager.ExpiryDate > DateTime.MinValue Then
                Dim expiryUtc As DateTime = positionManager.ExpiryDate

                ' Ensure UTC - this is the critical fix
                If expiryUtc.Kind = DateTimeKind.Local Then
                    expiryUtc = expiryUtc.ToUniversalTime()
                ElseIf expiryUtc.Kind = DateTimeKind.Unspecified Then
                    expiryUtc = DateTime.SpecifyKind(expiryUtc, DateTimeKind.Utc)
                End If

                Dim timeToExpiry = expiryUtc.Subtract(DateTime.UtcNow)
                hoursToExpiry = Math.Max(0, timeToExpiry.TotalHours)
            End If

            lblDaysToExpiry.Text = $"Hours to Expiry: {hoursToExpiry:F1}"
        Else
            lblPositionStatus.Text = "No active position"
            lblUnrealizedPnL.Text = "Unrealized P&L: $0.00"
            lblDaysToExpiry.Text = "Hours to Expiry: --"
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
            'lblRateLimit.Text = $"Rate: {rateLimiter.RequestsInWindow}/{50}"
            lblRateLimit.Text = $"Rate: {rateLimiter.RequestsInWindow}/50 ({performanceMonitor.MessagesPerSecond:F1}/sec)"

            ' Update last update time
            lblLastUpdate.Text = $"Last Update: {lastBasisUpdate:HH:mm:ss}"

            lblUptime.Text = $"Uptime: {performanceMonitor.UptimePercent:F1}%"
            ' lblLatency.Text = $"Latency: {performanceMonitor.AverageLatency.TotalMilliseconds:F0}ms"

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

    Private Sub HandleSubscriptionMessage(json As JObject)
        Try
            Dim channel As String = json("params")?("channel")?.ToString()

            If Not String.IsNullOrEmpty(channel) Then
                'AppendLog($"Subscription confirmed: {channel}", Color.Cyan)

                ' Track active subscriptions
                If Not availableWeeklyContracts.Contains(channel) Then
                    availableWeeklyContracts.Add(channel)
                End If
            End If

        Catch ex As Exception
            AppendLog($"Subscription message error: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub HandleAccountSummaryResponse(json As JObject)
        Try
            Dim result As JObject = json("result")

            If result IsNot Nothing Then
                ' Extract BTC balance information with higher precision
                Dim totalBalance As Decimal = 0
                Dim availableBalance As Decimal = 0
                Dim equity As Decimal = 0

                ' Try multiple field names that Deribit might use
                If result("total_balance") IsNot Nothing Then
                    Decimal.TryParse(result("total_balance").ToString(), totalBalance)
                End If

                If result("available_balance") IsNot Nothing Then
                    Decimal.TryParse(result("available_balance").ToString(), availableBalance)
                End If

                If result("equity") IsNot Nothing Then
                    Decimal.TryParse(result("equity").ToString(), equity)
                End If

                If result("balance") IsNot Nothing Then
                    Decimal.TryParse(result("balance").ToString(), totalBalance)
                End If

                ' Use the highest precision value available
                Dim displayBalance As Decimal = Math.Max(Math.Max(totalBalance, availableBalance), equity)

                If displayBalance > 0 Then
                    AppendLog($"BTC Balance: {displayBalance:F8} BTC (${displayBalance * currentBTCSpotPrice:F2})", Color.Green)

                    ' Update UI if you want to show balance
                    Me.Invoke(Sub() lblBTCBalance.Text = $"Balance: {displayBalance:F8} BTC")
                Else
                    AppendLog("BTC Balance: Account connected but balance may be in different format", Color.Yellow)
                End If
            End If

        Catch ex As Exception
            AppendLog($"Account summary error: {ex.Message}", Color.Red)
        End Try
    End Sub


    Private Sub HandlePortfolioUpdate(data As JObject)
        Try
            ' Handle real-time portfolio updates with higher precision
            Dim totalEquity As Decimal = 0
            Dim availableFunds As Decimal = 0
            Dim totalPnL As Decimal = 0

            If data("total_pl") IsNot Nothing Then
                Decimal.TryParse(data("total_pl").ToString(), totalPnL)
            End If

            If data("equity") IsNot Nothing Then
                Decimal.TryParse(data("equity").ToString(), totalEquity)
            End If

            If data("available_funds") IsNot Nothing Then
                Decimal.TryParse(data("available_funds").ToString(), availableFunds)
            End If

            ' Enhanced logging with 8 decimal places
            If totalEquity > 0 OrElse totalPnL <> 0 Then
                AppendLog($"Portfolio: Equity: {totalEquity:F8} BTC, P&L: {totalPnL:F8} BTC", Color.Blue)
            Else
                AppendLog($"Portfolio: No balance detected (may be in different currency)", Color.Yellow)
            End If

            ' Update position manager if active
            If positionManager.IsPositionActive Then
                Me.Invoke(Sub() UpdatePositionDisplay())
            End If

        Catch ex As Exception
            AppendLog($"Portfolio update error: {ex.Message}", Color.Red)
        End Try
    End Sub


    Private Async Function ExecuteRollingTrade() As Task
        Try
            AppendLog("Executing rolling trade...", Color.Blue)

            ' Get next weekly contract
            Dim nextWeeklyContract As String = GetNextWeeklyContract()

            If String.IsNullOrEmpty(nextWeeklyContract) Then
                AppendLog("Cannot determine next weekly contract", Color.Red)
                Return
            End If

            ' Check if rolling is profitable
            Dim nextWeeklyPrice As Decimal = Await GetContractPrice(nextWeeklyContract)

            If nextWeeklyPrice <= 0 Then
                AppendLog("Cannot get next weekly price", Color.Red)
                Return
            End If

            Dim nextBasisSpread As Decimal = basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, nextWeeklyPrice)

            If nextBasisSpread < CDec(nudMinBasisThreshold.Value) Then
                AppendLog($"Next weekly basis {nextBasisSpread:P3} below threshold", Color.Yellow)
                Return
            End If

            ' Close current futures position
            Dim closeSuccess As Boolean = Await CloseFuturesPosition(currentWeeklyContract)

            If closeSuccess Then
                ' Open new futures position
                Dim openSuccess As Boolean = Await ExecuteFuturesShort(positionManager.PositionSize, nextWeeklyContract)

                If openSuccess Then
                    ' Update position manager
                    positionManager.ContractName = nextWeeklyContract
                    positionManager.EntryFuturesPrice = nextWeeklyPrice
                    positionManager.ExpiryDate = CalculateContractExpiry(nextWeeklyContract)

                    currentWeeklyContract = nextWeeklyContract

                    AppendLog($"Position rolled to {nextWeeklyContract} at {nextBasisSpread:P3} basis", Color.Green)
                Else
                    AppendLog("Failed to open new futures position", Color.Red)
                End If
            Else
                AppendLog("Failed to close current futures position", Color.Red)
            End If

        Catch ex As Exception
            AppendLog($"Rolling trade error: {ex.Message}", Color.Red)
        End Try
    End Function

    Private Async Function CloseContangoPosition() As Task(Of Boolean)
        Try
            AppendLog("Closing contango position...", Color.Blue)

            ' Close futures position
            Dim futuresCloseSuccess As Boolean = Await CloseFuturesPosition(currentWeeklyContract)

            ' Close spot position (sell BTC)
            Dim spotCloseSuccess As Boolean = Await ExecuteSpotSale(positionManager.PositionSize)

            If futuresCloseSuccess AndAlso spotCloseSuccess Then
                AppendLog("All positions closed successfully", Color.Green)
                Return True
            Else
                AppendLog("Warning: Some positions may not have closed properly", Color.Orange)
                Return False
            End If

        Catch ex As Exception
            AppendLog($"Close position error: {ex.Message}", Color.Red)
            Return False
        End Try
    End Function

    Private Function GetNextWeeklyContract() As String
        Try
            Dim currentFriday As DateTime = GetNextFriday(DateTime.Now)
            Dim nextFriday As DateTime = currentFriday.AddDays(7)

            Return $"BTC-{nextFriday:ddMMMyy}".ToUpper()

        Catch ex As Exception
            Return ""
        End Try
    End Function

    Private Function GetNextFriday(fromDate As DateTime) As DateTime
        Dim daysUntilFriday As Integer = ((DayOfWeek.Friday - fromDate.DayOfWeek + 7) Mod 7)
        If daysUntilFriday = 0 AndAlso fromDate.Hour >= 8 Then
            daysUntilFriday = 7
        End If
        Return fromDate.AddDays(daysUntilFriday)
    End Function

    Private Function CalculateContractExpiry(contractName As String) As DateTime
        Try
            ' BTC weekly options expire Fridays at 08:00 UTC
            ' Parse contract format: BTC-DDMMMYY (e.g., "BTC-26SEP25")

            If String.IsNullOrEmpty(contractName) OrElse Not contractName.StartsWith("BTC-") OrElse contractName.Length < 11 Then
                AppendLog($"Invalid contract format: {contractName}", Color.Yellow)
                Return DateTime.UtcNow.AddDays(7) ' Fallback
            End If

            ' Extract date components
            Dim datePart As String = contractName.Substring(4) ' Remove "BTC-"

            If datePart.Length < 7 Then
                AppendLog($"Invalid date part: {datePart}", Color.Yellow)
                Return DateTime.UtcNow.AddDays(7) ' Fallback
            End If

            Dim dayStr = datePart.Substring(0, 2)    ' "26"
            Dim monthStr = datePart.Substring(2, 3)  ' "SEP"  
            Dim yearStr = datePart.Substring(5, 2)   ' "25"

            ' Parse components
            Dim day As Integer
            Dim year As Integer

            If Not Integer.TryParse(dayStr, day) OrElse day < 1 OrElse day > 31 Then
                AppendLog($"Invalid day: {dayStr}", Color.Yellow)
                Return DateTime.UtcNow.AddDays(7)
            End If

            If Not Integer.TryParse(yearStr, year) Then
                AppendLog($"Invalid year: {yearStr}", Color.Yellow)
                Return DateTime.UtcNow.AddDays(7)
            End If

            year += 2000 ' Convert 25 to 2025

            Dim month As Integer = GetMonthNumber(monthStr)
            If month = 0 Then
                AppendLog($"Invalid month: {monthStr}", Color.Yellow)
                Return DateTime.UtcNow.AddDays(7)
            End If

            ' Create UTC expiry (Friday 08:00 UTC) - THIS IS KEY
            Dim calculatedExpiry As New DateTime(year, month, day, 8, 0, 0, DateTimeKind.Utc)

            ' Validate it's in the future
            If calculatedExpiry <= DateTime.UtcNow Then
                AppendLog($"Contract {contractName} expired on {calculatedExpiry:yyyy-MM-dd HH:mm} UTC", Color.Orange)
            End If

            ' Success - log the calculated expiry
            AppendLog($"Calculated expiry for {contractName}: {calculatedExpiry:yyyy-MM-dd HH:mm} UTC", Color.Gray)

            Return calculatedExpiry

        Catch ex As Exception
            AppendLog($"Error parsing contract {contractName}: {ex.Message}", Color.Red)
            Return DateTime.UtcNow.AddDays(7) ' Safe fallback
        End Try
    End Function


    Private Function GetMonthNumber(monthAbbr As String) As Integer
        Select Case monthAbbr.ToUpper()
            Case "JAN" : Return 1
            Case "FEB" : Return 2
            Case "MAR" : Return 3
            Case "APR" : Return 4
            Case "MAY" : Return 5
            Case "JUN" : Return 6
            Case "JUL" : Return 7
            Case "AUG" : Return 8
            Case "SEP" : Return 9
            Case "OCT" : Return 10
            Case "NOV" : Return 11
            Case "DEC" : Return 12
            Case Else : Return 0
        End Select
    End Function

    Private Sub DisplayCorrectTime()
        ' Add this to your UI timer or position display update
        Dim nowMalaysia As DateTime = DateTime.Now ' Your local time
        Dim nowUtc As DateTime = DateTime.UtcNow   ' UTC time

        AppendLog($"Local Time: {nowMalaysia:yyyy-MM-dd HH:mm:ss} (+8)", Color.Gray)
        AppendLog($"UTC Time: {nowUtc:yyyy-MM-dd HH:mm:ss}", Color.Gray)

        ' Calculate actual hours to BTC-26SEP25 expiry (Sep 26, 08:00 UTC)
        Dim expiryUtc As New DateTime(2025, 9, 26, 8, 0, 0, DateTimeKind.Utc)
        Dim hoursRemaining As Double = expiryUtc.Subtract(nowUtc).TotalHours

        AppendLog($"Actual hours to BTC-26SEP25 expiry: {hoursRemaining:F1}", Color.Yellow)
    End Sub


    Private Async Function GetContractPrice(contractName As String) As Task(Of Decimal)
        Try
            ' Request current price for specific contract
            Dim priceRequest As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "public/get_ticker"},
            {"params", New JObject From {
                {"instrument_name", contractName}
            }}
        }

            Await SendWebSocketMessage(priceRequest.ToString())

            ' For now, return current futures price as placeholder
            ' In production, you'd wait for the response
            Await Task.Delay(1000)
            Return currentWeeklyFuturesPrice

        Catch ex As Exception
            AppendLog($"Error getting contract price: {ex.Message}", Color.Red)
            Return 0
        End Try
    End Function

    Private Async Function CloseFuturesPosition(contractName As String) As Task(Of Boolean)
        Try
            ' Implementation for closing futures position
            AppendLog($"Closing futures position on {contractName}", Color.Blue)

            ' TODO: Implement actual Deribit close order
            Await Task.Delay(1000) ' Placeholder

            Return True

        Catch ex As Exception
            AppendLog($"Error closing futures: {ex.Message}", Color.Red)
            Return False
        End Try
    End Function

    Private Async Function ExecuteSpotSale(amount As Decimal) As Task(Of Boolean)
        Try
            ' Implementation for selling BTC spot
            AppendLog($"Selling {amount} BTC at spot", Color.Blue)

            ' TODO: Implement actual Deribit sell order
            Await Task.Delay(1000) ' Placeholder

            Return True

        Catch ex As Exception
            AppendLog($"Error selling spot: {ex.Message}", Color.Red)
            Return False
        End Try
    End Function

    Private Sub ValidateMarketData()
        Try
            ' Validate BTC spot price is reasonable (between $10k-$200k)
            If currentBTCSpotPrice < 10000 Or currentBTCSpotPrice > 200000 Then
                AppendLog($"Warning: Unusual BTC spot price: ${currentBTCSpotPrice}", Color.Yellow)
                Return
            End If

            ' Validate weekly futures price
            If currentWeeklyFuturesPrice < 10000 Or currentWeeklyFuturesPrice > 200000 Then
                AppendLog($"Warning: Unusual futures price: ${currentWeeklyFuturesPrice}", Color.Yellow)
                Return
            End If

            ' Validate basis spread is reasonable (-5% to +10%)
            Dim basisSpread As Decimal = basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice)

            If Math.Abs(basisSpread) > 0.1 Then ' Greater than 10%
                AppendLog($"Warning: Extreme basis spread: {basisSpread:P3}", Color.Red)
            End If

            ' Log successful validation
            If basisSpread > 0.001 Then ' Positive basis > 0.1%
                AppendLog($"Valid contango detected: {basisSpread:P3} ({basisMonitor.GetAnnualizedBasisReturn(basisSpread):P1} annualized)", Color.Cyan)
            End If

        Catch ex As Exception
            AppendLog($"Market data validation error: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Function ValidatePositionSize(requestedSize As Decimal) As Boolean
        Try
            ' Maximum position size (e.g., 10% of paper balance)
            Dim maxSize As Decimal = If(paperTradingMode, paperBalance * 0.1, 0.5) ' 0.5 BTC max for live

            If requestedSize > maxSize Then
                AppendLog($"Position size {requestedSize} exceeds maximum {maxSize}", Color.Red)
                Return False
            End If

            ' Minimum position size
            If requestedSize < 0.001 Then
                AppendLog($"Position size {requestedSize} below minimum 0.001 BTC", Color.Red)
                Return False
            End If

            ' Check basis spread threshold
            Dim currentBasis = basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice)
            If currentBasis < CDec(nudMinBasisThreshold.Value) Then
                AppendLog($"Basis spread {currentBasis:P3} below threshold {nudMinBasisThreshold.Value:P3}", Color.Red)
                Return False
            End If

            Return True

        Catch ex As Exception
            AppendLog($"Position validation error: {ex.Message}", Color.Red)
            Return False
        End Try
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

            ' Handle account summary responses (BTC balance)
            If json("id")?.ToString() = "account_summary" AndAlso json("result") IsNot Nothing Then
                HandleAccountSummaryResponse(json)
            End If

            ' Handle get_instruments response
            If json("id")?.ToString() = "get_instruments" AndAlso json("result") IsNot Nothing Then
                HandleInstrumentsResponse(json)
            End If

            ' Debug: Log the message type for troubleshooting
            If json("method") IsNot Nothing Then
                Dim method = json("method").ToString()

                ' Only log subscription confirmations once, not repeatedly
                If method = "subscription" AndAlso json("params")?("channel") IsNot Nothing Then
                    Dim channel = json("params")("channel").ToString()
                    If Not channel.Contains("deribit_price_index") Then
                        HandleSubscriptionMessage(json)
                    End If

                    ' Handle actual data updates (not just confirmations)
                    If json("params")?("data") IsNot Nothing Then
                        HandleMarketDataUpdate(json)
                    End If
                End If

                ' Handle heartbeat requests
                If method = "heartbeat" Then
                    HandleHeartbeatRequest(json)
                End If
            End If

            ' Handle authentication responses (only if it has result with access_token)
            If json("result") IsNot Nothing Then
                If json("result").Type = JTokenType.Object Then
                    Dim resultObj = CType(json("result"), JObject)
                    If resultObj("access_token") IsNot Nothing Then
                        HandleAuthenticationResponse(json)
                    End If
                End If
            End If

            ' Handle specific ID-based responses
            If json("id") IsNot Nothing AndAlso json("id").ToString() = "999" Then
                HandleAccountSummaryResponse(json)
            End If

            lastMessageTime = DateTime.Now

        Catch ex As Exception
            AppendLog($"Error processing message: {ex.Message}", Color.Red)
            ' Optionally log the raw message for debugging:
            ' AppendLog($"Raw message: {message.Substring(0, Math.Min(200, message.Length))}", Color.Gray)
        End Try
    End Sub

    Private Sub HandleInstrumentsResponse(json As JObject)
        Try
            Dim result = json("result")

            If result IsNot Nothing AndAlso result.Type = JTokenType.Array Then
                Dim contracts As JArray = CType(result, JArray)

                ' Find the nearest expiry contract
                Dim nearestContract As String = ""
                Dim nearestExpiry As DateTime = DateTime.MaxValue

                For Each contract In contracts
                    Dim contractName = contract("instrument_name")?.ToString()
                    Dim expiryStr = contract("expiration_timestamp")?.ToString()

                    If contractName IsNot Nothing AndAlso contractName.StartsWith("BTC-") Then
                        If Long.TryParse(expiryStr, Nothing) Then
                            Dim expiry = DateTimeOffset.FromUnixTimeMilliseconds(CLng(expiryStr)).DateTime

                            If expiry > DateTime.Now AndAlso expiry < nearestExpiry Then
                                nearestExpiry = expiry
                                nearestContract = contractName
                            End If
                        End If
                    End If
                Next

                If Not String.IsNullOrEmpty(nearestContract) Then
                    currentWeeklyContract = nearestContract
                    AppendLog($"Found next contract: {nearestContract} expires {nearestExpiry:yyyy-MM-dd HH:mm}", Color.Green)

                    ' CORRECTED: Subscribe to this contract
                    SubscribeToContract(nearestContract).ContinueWith(
                    Sub(task)
                        If task.IsFaulted Then
                            Me.Invoke(Sub() AppendLog($"Contract subscription failed: {task.Exception?.GetBaseException()?.Message}", Color.Red))
                        End If
                    End Sub)
                Else
                    AppendLog("No suitable BTC futures contracts found", Color.Red)
                End If
            End If

        Catch ex As Exception
            AppendLog($"Error processing instruments: {ex.Message}", Color.Red)
        End Try
    End Sub


    Private Async Function SubscribeToContract(contractName As String) As Task
        Try
            Dim contractSubscription As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "public/subscribe"},
            {"params", New JObject From {
                {"channels", New JArray({$"ticker.{contractName}.raw"})}
            }}
        }

            Await SendWebSocketMessage(contractSubscription.ToString())
            AppendLog($"Subscribed to contract: {contractName}", Color.Green)

        Catch ex As Exception
            AppendLog($"Error subscribing to contract: {ex.Message}", Color.Red)
        End Try
    End Function

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

            ' Subscribe to BTC-specific portfolio (enhanced)
            Dim btcPortfolioSubscription As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Guid.NewGuid().ToString()},
            {"method", "private/subscribe"},
            {"params", New JObject From {
                {"channels", New JArray({"user.portfolio.btc", "user.changes.btc"})}
            }}
        }

            Await SendWebSocketMessage(btcPortfolioSubscription.ToString())

            ' Also request current account summary explicitly
            Dim accountSummaryRequest As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", "account_summary"},
            {"method", "private/get_account_summary"},
            {"params", New JObject From {
                {"currency", "BTC"}
            }}
        }

            Await SendWebSocketMessage(accountSummaryRequest.ToString())

            AppendLog("BTC portfolio subscriptions requested", Color.Blue)


        Catch ex As Exception
            AppendLog($"Subscription error: {ex.Message}", Color.Red)
        End Try
    End Function

    Private Async Function SubscribeToWeeklyFutures() As Task
        Try
            ' First, get available contracts dynamically
            Await GetAvailableContracts()

        Catch ex As Exception
            AppendLog($"Weekly subscription error: {ex.Message}", Color.Red)
        End Try
    End Function


    Private Function GetCurrentWeeklyContract() As String
        Try
            ' Use UTC for Deribit calculations (Deribit uses UTC)
            Dim nowUtc As DateTime = DateTime.UtcNow
            Dim daysUntilFriday As Integer = ((DayOfWeek.Friday - nowUtc.DayOfWeek + 7) Mod 7)

            ' If it's already Friday after 08:00 UTC, get next Friday
            If daysUntilFriday = 0 AndAlso nowUtc.Hour >= 8 Then
                daysUntilFriday = 7
            End If

            Dim nextFriday As DateTime = nowUtc.AddDays(daysUntilFriday)

            ' Format as BTC-DDMMMYY
            Dim contractName As String = $"BTC-{nextFriday:ddMMMyy}".ToUpper()

            Return contractName

        Catch ex As Exception
            AppendLog($"Error calculating weekly contract: {ex.Message}", Color.Red)
            Return "BTC-27SEP25" ' Next week's contract
        End Try
    End Function


    Private recentBasisHistory As New Queue(Of BasisDataPoint)
    Private Const MAX_HISTORY_SIZE As Integer = 1000 ' Keep last 1000 data points

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
                        'AppendLog($"BTC Spot updated: ${currentBTCSpotPrice:N2}", Color.Cyan)
                    End If
                End If

                ' Handle weekly futures price updates (look for BTC- contracts)
                If channel.Contains("ticker.BTC-") AndAlso channel.Contains(".raw") Then
                    If Decimal.TryParse(data("last_price")?.ToString(), currentWeeklyFuturesPrice) Then
                        Me.Invoke(Sub()
                                      lblWeeklyFuturesPrice.Text = $"${currentWeeklyFuturesPrice:N2}"
                                      lblWeeklyFuturesPrice.ForeColor = Color.Purple
                                  End Sub)

                        'AppendLog($"Weekly Futures updated: ${currentWeeklyFuturesPrice:N2}", Color.Purple)

                        ' Update basis spread
                        UpdateBasisSpread()

                        ' Save to database
                        'contangoDatabase.SaveBasisData(currentBTCSpotPrice, currentWeeklyFuturesPrice,
                        ' basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice),
                        'currentWeeklyContract)

                        ' Store in memory-only history (no database)
                        If currentBTCSpotPrice > 0 AndAlso currentWeeklyFuturesPrice > 0 Then
                            Dim basisPoint As New BasisDataPoint With {
                                .Timestamp = DateTime.Now,
                                .SpotPrice = currentBTCSpotPrice,
                                .FuturesPrice = currentWeeklyFuturesPrice,
                                .BasisSpread = basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice)
                            }

                            recentBasisHistory.Enqueue(basisPoint)

                            ' Keep only recent history (limit memory usage)
                            While recentBasisHistory.Count > MAX_HISTORY_SIZE
                                recentBasisHistory.Dequeue()
                            End While
                        End If
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

    Private Function GetCurrentMonthlyContract() As String
        Try
            Dim nextMonth As DateTime = DateTime.Now.AddDays(30)
            Dim lastFriday As DateTime = GetLastFridayOfMonth(nextMonth)

            Return $"BTC-{lastFriday:ddMMMyy}".ToUpper()

        Catch ex As Exception
            Return "BTC-27DEC24" ' Fallback to quarterly
        End Try
    End Function

    Private Function GetLastFridayOfMonth(targetMonth As DateTime) As DateTime
        Dim lastDay = New DateTime(targetMonth.Year, targetMonth.Month, DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month))

        While lastDay.DayOfWeek <> DayOfWeek.Friday
            lastDay = lastDay.AddDays(-1)
        End While

        Return lastDay
    End Function

    Private Async Function GetAvailableContracts() As Task
        Try
            Dim instrumentsRequest As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", "get_instruments"},
            {"method", "public/get_instruments"},
            {"params", New JObject From {
                {"currency", "BTC"},
                {"kind", "future"},
                {"expired", False}
            }}
        }

            Await SendWebSocketMessage(instrumentsRequest.ToString())
            AppendLog("Requesting available BTC futures contracts", Color.Blue)

        Catch ex As Exception
            AppendLog($"Error requesting contracts: {ex.Message}", Color.Red)
        End Try
    End Function


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

            If futuresSuccess Then
                ' Calculate and save complete trade entry information
                Dim tradeEntryBasisSpread As Decimal = basisMonitor.CalculateBasisSpread(currentBTCSpotPrice, currentWeeklyFuturesPrice)
                Dim annualizedReturn As Decimal = basisMonitor.GetAnnualizedBasisReturn(tradeEntryBasisSpread)

                ' Save basis data for the trade
                contangoDatabase.SaveBasisData(currentBTCSpotPrice, currentWeeklyFuturesPrice,
                                 tradeEntryBasisSpread, currentWeeklyContract)

                ' Enhanced logging with trade details
                AppendLog($"TRADE EXECUTED: Entry basis {tradeEntryBasisSpread:P3} (est. {annualizedReturn:P1} annual)", Color.Green)
                AppendLog($"Trade details saved: Spot ${currentBTCSpotPrice:N2}, Futures ${currentWeeklyFuturesPrice:N2}", Color.Blue)

                ' Update the position manager with actual entry values
                Dim calculatedExpiry As DateTime = CalculateContractExpiry(currentWeeklyContract)
                positionManager.OpenCashCarryPosition(currentBTCSpotPrice, currentWeeklyFuturesPrice,
                                    positionSize, currentWeeklyContract,
                                    calculatedExpiry)
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

    ' Add this method to monitor connection quality
    Private Sub UpdateConnectionHealth()
        Try
            If webSocketClient?.State = WebSocketState.Open Then
                Dim timeSinceLastMessage = DateTime.Now.Subtract(lastMessageTime)

                If timeSinceLastMessage.TotalSeconds < 5 Then
                    lblConnectionStatus.Text = "Status: Excellent"
                    lblConnectionStatus.ForeColor = Color.Green
                ElseIf timeSinceLastMessage.TotalSeconds < 30 Then
                    lblConnectionStatus.Text = "Status: Good"
                    lblConnectionStatus.ForeColor = Color.Orange
                Else
                    lblConnectionStatus.Text = "Status: Poor Connection"
                    lblConnectionStatus.ForeColor = Color.Red
                End If
            Else
                lblConnectionStatus.Text = "Status: Disconnected"
                lblConnectionStatus.ForeColor = Color.Red
            End If

        Catch ex As Exception
            AppendLog($"Connection health error: {ex.Message}", Color.Red)
        End Try
    End Sub


#End Region

#Region "Paper Trading Mode"
    Private paperTradingMode As Boolean = True ' Start in paper mode
    Private paperBalance As Decimal = 0.001D ' 1 BTC paper balance
    Private paperPositions As New List(Of PaperPosition)

    Public Class PaperPosition
        Public Property EntryPrice As Decimal
        Public Property Size As Decimal
        Public Property EntryTime As DateTime
        Public Property ContractName As String
        Public Property PositionType As String ' "SPOT" or "FUTURES"
    End Class

    Private Async Function ExecutePaperTrade(positionSize As Decimal) As Task(Of Boolean)
        Try
            If paperTradingMode Then
                ' Simulate spot purchase
                paperPositions.Add(New PaperPosition With {
                    .EntryPrice = currentBTCSpotPrice,
                    .Size = positionSize,
                    .EntryTime = DateTime.Now,
                    .ContractName = "BTC-SPOT",
                    .PositionType = "SPOT"
                })

                ' Simulate futures short
                paperPositions.Add(New PaperPosition With {
                    .EntryPrice = currentWeeklyFuturesPrice,
                    .Size = -positionSize,
                    .EntryTime = DateTime.Now,
                    .ContractName = currentWeeklyContract,
                    .PositionType = "FUTURES"
                })

                paperBalance -= positionSize * currentBTCSpotPrice * 0.1 ' 10% margin used

                AppendLog($"PAPER TRADE: Cash-carry executed - {positionSize} BTC", Color.Blue)
                AppendLog($"PAPER TRADE: Spot @ ${currentBTCSpotPrice}, Futures @ ${currentWeeklyFuturesPrice}", Color.Blue)

                Return True
            Else
                ' Execute real trade - CORRECTED METHOD NAME:
                Return Await ExecuteCashCarryTrade(positionSize)
            End If

        Catch ex As Exception
            AppendLog($"Paper trade error: {ex.Message}", Color.Red)
            Return False
        End Try
    End Function

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        DisplayCorrectTime()

    End Sub


#End Region

End Class