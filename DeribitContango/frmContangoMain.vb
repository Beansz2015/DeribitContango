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
        ' Implementation will be added in Phase 6
        Await Task.Delay(100)
    End Function

    Private Async Function HandleWebSocketMessages() As Task
        ' Implementation will be added in Phase 6
        Await Task.Delay(100)
    End Function

#End Region

End Class
