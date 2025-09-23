Imports System.Globalization
Imports System.IO
'Imports System.Net.Http
'Imports System.Net.Http.Headers
'Imports System.Net.WebRequestMethods
Imports System.Net.WebSockets
'Imports System.Reflection
'Imports System.Runtime
Imports System.Text
Imports System.Threading
'Imports System.Windows.Forms.VisualStyles
'Imports System.Xml
'Imports Microsoft.VisualBasic.ApplicationServices
Imports Newtonsoft.Json.Linq


Public Class frmMainPageV2

    Private _indicators As FrmIndicators
    Private _autotradesettings As AutoTradeSettings

    Private webSocketClient As ClientWebSocket
    Private cancellationTokenSource As CancellationTokenSource
    Private lastMessageTime As DateTime
    Private keepAliveTimer As System.Timers.Timer

    'For refresh authentication token
    Private refreshToken As String = Nothing
    Private refreshTokenExpiryTime As DateTime = DateTime.MinValue

    ' Replace with your client ID and client secret
    Private Const ClientId As String = "YZCnDmWo"
    Private Const ClientSecret As String = "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA"

    'Public Variables
    Public BestBidPrice, BestAskPrice, TPTrailprice As Decimal
    Public StopLossTriggerOriginal As Decimal = 0 ' Original stop loss price
    'For auto trading logging
    Public AutoPlacedPrice, AutoTakeProfit, AutoStopLoss As Decimal

    Private TradeMode As Boolean = True ' Tracks if Buy or Sell mode
    Private isTrailingStopLossPlaced As Boolean = False 'Tracks if trailing stop loss already placed once
    Private SLTriggered As Boolean = False ' Tracks if stop loss has been triggered

    Private latestOrderId As String = Nothing ' Track the most recent order ID

    'For rate limiter
    Private rateLimiter As DeribitRateLimiter
    Private accountLimits As RateLimitInfo

    'Database class calls
    Private tradeDatabase As TradeDatabase
    Private tradeAnalytics As TradeAnalytics

    'To prevent duplicate API calls
    Private isRequestingLiveData As Boolean = False
    Private lastLiveDataRequest As DateTime = DateTime.MinValue

    'For circuit breaker in auto-trading in frmindicators
    Public USDPublicSession As Decimal

    Public ReadOnly Property RateLimiterInstance As DeribitRateLimiter
        Get
            Return rateLimiter
        End Get
    End Property


    Public Class RateLimitInfo
        Public Property MaxCredits As Integer
        Public Property RefillRate As Integer
        Public Property BurstLimit As Integer
        Public Property CurrentEstimatedCredits As Integer
    End Class

    Public Class DeribitRateLimiter
        Private ReadOnly _maxCredits As Integer
        Private ReadOnly _refillRate As Integer = 15 ' Credits per millisecond 'Conservative = 10 | Reasonable = 20
        Private ReadOnly _costPerRequest As Integer = 83 ' Conservative estimate = 200 | Reasonable = 50
        Private _currentCredits As Integer
        Private _lastRefillTime As DateTime
        Private ReadOnly _lockObject As New Object()

        Public Sub New(maxCredits As Integer, costPerRequest As Integer)
            _maxCredits = maxCredits
            _costPerRequest = costPerRequest
            _currentCredits = maxCredits
            _lastRefillTime = DateTime.UtcNow
        End Sub

        Public Function CanMakeRequest() As Boolean
            SyncLock _lockObject
                RefillCredits()
                Return _currentCredits >= _costPerRequest
            End SyncLock
        End Function

        Public Function ConsumeCredits() As Boolean
            SyncLock _lockObject
                RefillCredits()
                If _currentCredits >= _costPerRequest Then
                    _currentCredits -= _costPerRequest
                    Return True
                End If
                Return False
            End SyncLock
        End Function

        Private Sub RefillCredits()
            Dim now As DateTime = DateTime.UtcNow
            Dim elapsedMs As Double = (now - _lastRefillTime).TotalMilliseconds

            If elapsedMs > 0 Then
                Dim creditsToAdd As Integer = CInt(elapsedMs * _refillRate)
                _currentCredits = Math.Min(_maxCredits, _currentCredits + creditsToAdd)
                _lastRefillTime = now
            End If
        End Sub

        Public Function GetWaitTimeMs() As Integer
            SyncLock _lockObject
                RefillCredits()
                If _currentCredits >= _costPerRequest Then
                    Return 0
                End If

                Dim creditsNeeded As Integer = _costPerRequest - _currentCredits
                Return CInt(Math.Ceiling(creditsNeeded / _refillRate))
            End SyncLock
        End Function

        Public Function GetDetailedStatus() As String
            SyncLock _lockObject
                RefillCredits()
                Return $"Credits: {_currentCredits}/{_maxCredits} | " &
                       $"Last Refill: {_lastRefillTime:HH:mm:ss.fff} | " &
                       $"Can Request: {CanMakeRequest()}"
            End SyncLock
        End Function

        Public Sub ForceRefillDebug()
            ' Manual credit refill for testing
            SyncLock _lockObject
                _currentCredits = _maxCredits
                _lastRefillTime = DateTime.UtcNow
            End SyncLock
        End Sub

    End Class

    'For AUTOMATED ORDER PLACEMENT
    '----------------------------------------------------------------------------------------------
    Public Async Function ExecuteAutomatedOrder(orderType As String) As Task
        Await ExecuteOrderAsync(orderType)
    End Function

    Public Function GetTradeMode() As Boolean
        Return TradeMode
    End Function

    Public ReadOnly Property WebSocketConnection As ClientWebSocket
        Get
            Return webSocketClient
        End Get
    End Property

    Public ReadOnly Property RateLimitManager As DeribitRateLimiter
        Get
            Return rateLimiter
        End Get
    End Property

    Public ReadOnly Property IsWebSocketConnected As Boolean
        Get
            Return webSocketClient IsNot Nothing AndAlso webSocketClient.State = WebSocketState.Open
        End Get
    End Property

    ' Optional: Add rate limit status check
    Public ReadOnly Property CanMakeAPIRequest As Boolean
        Get
            Return rateLimiter IsNot Nothing AndAlso rateLimiter.CanMakeRequest()
        End Get
    End Property
    '----------------------------------------------------------------------------------------------

    Private Sub frmMainPageV2_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            _indicators = New FrmIndicators(Me)     ' pass “self” as host
            _indicators.Show()                      ' non-modal; use .ShowDialog() if you prefer modal

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Startup Error: {ex.Message}{vbCrLf}{ex.StackTrace}", Color.Red)
            'Application.Exit()
        End Try
    End Sub

    Private Sub frmMainPageV2_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown
        Try
            ' Initialize trade database
            tradeDatabase = New TradeDatabase()
            tradeAnalytics = New TradeAnalytics(tradeDatabase.DatabasePath)

            ' Subscribe to database events
            AddHandler tradeDatabase.DatabaseError, AddressOf OnDatabaseError
            AddHandler tradeDatabase.TradeRecorded, AddressOf OnTradeRecorded
            AddHandler tradeDatabase.TradeDeleted, AddressOf OnTradeDeleted

            AppendColoredText(txtLogs, "Trade database initialized successfully", Color.LimeGreen)

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Failed to initialize trade database: {ex.Message}", Color.Red)
        End Try
    End Sub

    ' Event handlers
    Private Sub OnDatabaseError(message As String)
        AppendColoredText(txtLogs, $"Database Error: {message}", Color.Red)
    End Sub

    Private Sub OnTradeRecorded(tradeId As Integer, trade As TradeRecord)
        AppendColoredText(txtLogs, $"Trade #{tradeId} recorded in database", Color.Cyan)
    End Sub
    Private Sub OnTradeDeleted(tradeId As Integer)
        AppendColoredText(txtLogs, $"Trade #{tradeId} deleted from database", Color.Yellow)
    End Sub

    Private Async Function WebSocketCalls() As Task

        Dim reconnectNeeded As Boolean = False

        ' Initialize WebSocket
        webSocketClient = New ClientWebSocket()
        webSocketClient.Options.KeepAliveInterval = TimeSpan.FromSeconds(30)
        cancellationTokenSource = New CancellationTokenSource()

        Try
            ' Connect to Deribit WebSocket
            'Await webSocketClient.ConnectAsync(New Uri("wss://www.deribit.com/ws/api/v2"), cancellationTokenSource.Token)

            ' Connect with timeout protection
            Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(30))
                Await webSocketClient.ConnectAsync(New Uri("wss://www.deribit.com/ws/api/v2"), cts.Token)
            End Using


            ' Start fetching real-time server time
            'Await Task.Run(AddressOf FetchServerTimeContinuously)

            ' Initialize rate limiter before any API calls
            If rateLimiter Is Nothing Then
                rateLimiter = New DeribitRateLimiter(2000, 50) ' Conservative emergency limiter
            End If

            ' Authorize the connection
            Await AuthorizeWebSocketConnection()

            Await EnableDeribitHeartbeatEnhanced()

            ' Subscribe to the BTC-PERPETUAL index price user portfolio
            Await SubscribeToIndexPrice()

            ' Subscribe to the user portfolio
            Await SubscribeToUserPortfolio()

            ' Subscribe to the quote.BTC-PERPETUAL channel
            Await SubscribeToQuoteBTCPerpetual()

            ' Subscribe to the user.changes.BTC-PERPETUAL.raw channel (Changes to orders, trades & positions)
            Await SubscribeToUserOrders()

            ' Start monitoring authentication
            Await Task.Run(AddressOf MonitorAuthentication)

            ' Subscribe to the BTC-PERPETUAL index price
            'Dim subscriptionPayload As String = "{""jsonrpc"":""2.0"",""id"":1,""method"":""public/subscribe"",""params"":{""channels"":[""perpetual.BTC-PERPETUAL.agg2"", ""user.portfolio.btc""]}}"

            Dim WebSocketCallsBackgroundTask = Task.Run(Async Function()
                                                            Try
                                                                Await Task.Delay(3000) ' Give connection time to stabilize
                                                                Await InitializeRateLimitsAfterAuth()
                                                                AppendColoredText(txtLogs, "Rate limits updated after authentication", Color.Cyan)
                                                            Catch ex As Exception
                                                                AppendColoredText(txtLogs, $"Background rate limit update failed: {ex.Message}", Color.Yellow)
                                                                ' Keep using emergency rate limiter
                                                            End Try
                                                            Return Nothing
                                                        End Function)

            'Monitor connection health in a separate non-async task
            Dim connectionHealthTask = Task.Run(AddressOf MonitorConnectionHealth)

            ' Start receiving messages
            Await ReceiveWebSocketMessagesAsync()

        Catch ex As Exception
            Throw New Exception("Authentication failed: " & ex.Message)
            'txtLogs.AppendText("Authentication failed." + ex.Message + Environment.NewLine)
            AppendColoredText(txtLogs, "Authentication failed." + ex.Message, Color.Red)
            reconnectNeeded = True

        End Try
        If reconnectNeeded Then
            Await ReconnectWebSocket()
        End If
    End Function

    Private Async Function AuthorizeWebSocketConnection() As Task

        ' Create the authorization message using JObject
        Dim authPayload = New JObject(
            New JProperty("jsonrpc", "2.0"),
            New JProperty("id", 2),
            New JProperty("method", "public/auth"),
            New JProperty("params", New JObject(
                New JProperty("grant_type", "client_credentials"),
                New JProperty("client_id", ClientId),
                New JProperty("client_secret", ClientSecret)
            ))
        )
        'Await SendWebSocketMessageAsync(authPayload)
        Await SendWebSocketMessageAsync(authPayload.ToString())

        ' Read the response
        Dim buffer = New Byte(1024 * 4) {}
        Dim result = Await webSocketClient.ReceiveAsync(New ArraySegment(Of Byte)(buffer), cancellationTokenSource.Token)
        Dim response = Encoding.UTF8.GetString(buffer, 0, result.Count)

        Dim json = JObject.Parse(response)
        Dim errorField = json.SelectToken("error")

        If errorField IsNot Nothing Then
            Throw New Exception("Authorization failed: " & errorField.ToString())
            'txtLogs.AppendText("Authorization failed: " & errorField.ToString() + Environment.NewLine)
            AppendColoredText(txtLogs, "Authorization failed: " & errorField.ToString(), Color.Red)
        Else
            'txtLogs.AppendText("WebSocket authorized successfully" + Environment.NewLine)
            AppendColoredText(txtLogs, "WebSocket authorized successfully", Color.DodgerBlue)
            Await EnableDeribitHeartbeatEnhanced()

            Dim refreshTokenToken = json.SelectToken("result.refresh_token")
            If refreshTokenToken IsNot Nothing Then
                refreshToken = refreshTokenToken.ToString()
            Else
                Throw New Exception("Refresh token not found in response")
                'txtLogs.AppendText("Refresh token not found in response" + Environment.NewLine)
                AppendColoredText(txtLogs, "Refresh token not found in response", Color.Yellow)
            End If

            Dim expiresInToken = json.SelectToken("result.expires_in")
            Dim expiresIn As Double = 0
            If expiresInToken IsNot Nothing Then
                expiresIn = expiresInToken.ToObject(Of Double)()
                'txtLogs.AppendText("Token Expires In: " & expiresIn.ToString + Environment.NewLine)       ' TEST
            End If

            refreshTokenExpiryTime = DateTime.UtcNow.AddSeconds(expiresIn - 240) ' Refresh 4 minutes before expiry

            'Give successful status update
            lblStatus.ForeColor = Color.LimeGreen
            btnConnect.Text = "ONLINE"
            btnConnect.BackColor = Color.Lime

        End If
    End Function

    Private Async Function RefreshWebSocketAuthentication() As Task
        If DateTime.UtcNow >= refreshTokenExpiryTime Then

            'Dim refreshPayload As String = $"{{""jsonrpc"":""2.0"",""id"":3,""method"":""public/auth"",""params"":{{""grant_type"":""refresh_token"",""refresh_token"":""{refreshToken}""}}}}"
            ' Create the refresh message using JObject
            Dim refreshPayload = New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 3),
                New JProperty("method", "public/auth"),
                New JProperty("params", New JObject(
                    New JProperty("grant_type", "refresh_token"),
                    New JProperty("refresh_token", refreshToken)
                ))
            )
            'Await SendWebSocketMessageAsync(refreshPayload)
            Await SendWebSocketMessageAsync(refreshPayload.ToString())

            Dim buffer = New Byte(1024 * 4) {}
            Dim result = Await webSocketClient.ReceiveAsync(New ArraySegment(Of Byte)(buffer), cancellationTokenSource.Token)
            Dim response = Encoding.UTF8.GetString(buffer, 0, result.Count)

            Dim json = JObject.Parse(response)
            If json.SelectToken("error") IsNot Nothing Then
                Throw New Exception("Token refresh failed: " & json.SelectToken("error").ToString())
                'txtLogs.AppendText("Token refresh failed: " & json.SelectToken("error").ToString() + Environment.NewLine)
                AppendColoredText(txtLogs, "Token refresh failed: " & json.SelectToken("error").ToString(), Color.Yellow)
            End If

            Dim refreshTokenToken = json.SelectToken("result.refresh_token")
            If refreshTokenToken IsNot Nothing Then
                refreshToken = refreshTokenToken.ToString()
            Else
                Throw New Exception("Refresh token not found in response")
                'txtLogs.AppendText("Refresh token not found in response" + Environment.NewLine)
                AppendColoredText(txtLogs, "Refresh token not found in response", Color.Yellow)
            End If

            Dim expiresInToken = json.SelectToken("result.expires_in")
            Dim expiresIn As Double = 0
            If expiresInToken IsNot Nothing Then
                expiresIn = expiresInToken.ToObject(Of Double)()
            End If

            'txtLogs.AppendText("WebSocket re-authenticated successfully" + Environment.NewLine)
            AppendColoredText(txtLogs, "WebSocket re-authenticated successfully", Color.DodgerBlue)
        End If
    End Function

    Private Async Function SendWebSocketMessageAsync(message As String) As Task
        Try
            Dim bytes = Encoding.UTF8.GetBytes(message)

            ' Attempt to send the message
            Await webSocketClient.SendAsync(New ArraySegment(Of Byte)(bytes), WebSocketMessageType.Text, True, cancellationTokenSource.Token)

        Catch ex As WebSocketException
            ' Log WebSocket-specific errors
            AppendColoredText(txtLogs, "WebSocket Error: " & ex.Message, Color.Red)
            Dim disconnectTask = HandleWebSocketDisconnect()

        Catch ex As OperationCanceledException
            ' Log if operation was canceled (e.g., during shutdown)
            AppendColoredText(txtLogs, "Operation Canceled: " & ex.Message, Color.Orange)
        Catch ex As Exception
            ' General exception handler for any other errors
            AppendColoredText(txtLogs, "Error sending message: " & ex.Message, Color.Red)
            ' Fire and forget reconnection attempt
            Dim disconnectTask = HandleWebSocketDisconnect()
        End Try
    End Function

    ' These fields are for reconnect logic
    Private isReconnecting As Integer = 0  ' 0 = no, 1 = yes (Interlocked)
    Private reconnectAttempts As Integer = 0
    Private maxReconnectAttempts As Integer = 10
    Private isClosing As Boolean = False

    Private Async Function HandleWebSocketDisconnect() As Task

        If Not isClosing Then
            ' Single-flight guard - only one reconnect at a time
            If Interlocked.Exchange(isReconnecting, 1) = 1 Then Return

            Try
                AppendColoredText(txtLogs, "Connection lost - initiating recovery sequence", Color.Orange)

                ' Update UI immediately on UI thread
                Me.BeginInvoke(Sub()
                                   btnConnect.Text = "Connect!"
                                   btnConnect.BackColor = Color.Red
                                   lblStatus.Text = "Disconnected"
                               End Sub)

                ' Wait before attempting reconnection
                Await Task.Delay(2000 + (reconnectAttempts * 1000)) ' Progressive backoff

                For attempt = 1 To maxReconnectAttempts
                    Dim success As Boolean = False
                    Dim errorMessage As String = ""

                    Try
                        AppendColoredText(txtLogs, $"Reconnection attempt {attempt}/{maxReconnectAttempts}", Color.Yellow)

                        ' Call connection method directly - NOT through UI button
                        Await ConnectToWebSocketDirectly()

                        ' If we reach here, connection succeeded
                        success = True

                    Catch ex As Exception
                        errorMessage = ex.Message
                    End Try

                    ' Handle results outside the Try/Catch
                    If success Then
                        reconnectAttempts = 0
                        AppendColoredText(txtLogs, "Successfully reconnected", Color.LimeGreen)
                        Return
                    Else
                        reconnectAttempts += 1
                        AppendColoredText(txtLogs, $"Reconnect attempt {attempt} failed: {errorMessage}", Color.Red)

                        ' Only delay if we have more attempts left
                        If attempt < maxReconnectAttempts Then
                            Dim delayMs = 2000 * Math.Min(attempt, 5) ' Cap at 10 second delays
                            Await Task.Delay(delayMs)
                        End If
                    End If
                Next

                ' All reconnection attempts failed
                AppendColoredText(txtLogs, "All reconnection attempts failed - manual intervention required", Color.Red)

            Finally
                Interlocked.Exchange(isReconnecting, 0)
            End Try
        Else
            Return
        End If

    End Function


    Private Async Function ConnectToWebSocketDirectly() As Task
        ' Clean shutdown of existing connection
        Try
            If cancellationTokenSource IsNot Nothing Then
                cancellationTokenSource.Cancel()
            End If
            If webSocketClient IsNot Nothing Then
                If webSocketClient.State = WebSocketState.Open Then
                    Await webSocketClient.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "reconnecting", CancellationToken.None)
                End If
                webSocketClient.Dispose()
            End If
            cancellationTokenSource?.Dispose()
        Catch
            ' Ignore cleanup errors
        End Try

        ' Create fresh instances
        webSocketClient = New ClientWebSocket()
        webSocketClient.Options.KeepAliveInterval = TimeSpan.FromSeconds(30)
        cancellationTokenSource = New CancellationTokenSource()

        ' Connect with timeout
        Using connectTimeout As New CancellationTokenSource(TimeSpan.FromSeconds(30))
            Using combined = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationTokenSource.Token, connectTimeout.Token)

                Await webSocketClient.ConnectAsync(
                New Uri("wss://www.deribit.com/ws/api/v2"),
                combined.Token)
            End Using
        End Using

        ' Authenticate and subscribe
        Await AuthorizeWebSocketConnection()
        Await EnableDeribitHeartbeatEnhanced()
        Await SubscribeToIndexPrice()
        Await SubscribeToUserPortfolio()
        Await SubscribeToQuoteBTCPerpetual()
        Await SubscribeToUserOrders()

        ' Update UI on success
        Me.BeginInvoke(Sub()
                           btnConnect.Text = "ONLINE"
                           btnConnect.BackColor = Color.Lime
                           lblStatus.Text = "Connected"
                       End Sub)

        ' Start background tasks - use proper variable names
        Dim authTask = Task.Run(AddressOf MonitorAuthentication) ' Fire and forget
        Dim receiveTask = Task.Run(Function() ReceiveWebSocketMessagesAsync()) ' Fire and forget
    End Function



    Private Async Function ReceiveWebSocketMessagesAsync() As Task
        Dim buffer(65535) As Byte ' Larger buffer
        Dim reconnectNeeded As Boolean = False

        ' Initialize lastMessageTime to the current time
        lastMessageTime = DateTime.Now

        While webSocketClient.State = WebSocketState.Open
            Try
                Dim result = Await webSocketClient.ReceiveAsync(
                New ArraySegment(Of Byte)(buffer),
                cancellationTokenSource.Token)

                If result.MessageType = WebSocketMessageType.Close Then
                    AppendColoredText(txtLogs, "Server closed connection gracefully", Color.Yellow)
                    Exit While
                End If

                Dim response = Encoding.UTF8.GetString(buffer, 0, result.Count)

                ' Update last message time on any valid message
                lastMessageTime = DateTime.Now

                ' Call the function to handle heartbeat requests from server
                HandleHeartbeat(response)
                ' Call the function to handle quote updates
                HandleQuoteUpdates(response)
                ' Call the function to handle index price updates
                HandleIndexUpdates(response)
                ' Call the function to handle user portfolio updates
                HandleBalanceUpdates(response)
                ' Call the function to handle user order/position updates
                HandleOrderPositionUpdates(response)
                ' NEW: Handle account summary responses
                HandleAccountSummaryResponse(response)
                ' NEW: Handle rate limit errors
                HandleRateLimitError(response)
                ' NEW: Handle margin estimates for liquidation price calculations
                HandleMarginEstimationResponse(response)
            Catch ex As WebSocketException
                AppendColoredText(txtLogs, $"WebSocket exception: {ex.Message}", Color.Red)
                reconnectNeeded = True
                Exit While

            Catch ex As OperationCanceledException
                ' Normal during shutdown
                AppendColoredText(txtLogs, "Receive operation cancelled", Color.Gray)
                Exit While

            Catch ex As Exception
                AppendColoredText(txtLogs, $"Receive error: {ex.Message}", Color.Red)
                reconnectNeeded = True
                Exit While
            End Try

            ' Timeout check
            If DateTime.Now.Subtract(lastMessageTime).TotalSeconds > 75 Then
                AppendColoredText(txtLogs, "Message timeout - connection may be dead", Color.Yellow)
                reconnectNeeded = True
                Exit While
            End If
        End While

        ' Only trigger reconnect if we detected a problem

        If reconnectNeeded Then
            If Not isClosing Then
                ' Fire and forget - don't await to avoid blocking
                Dim reconnectTask = Task.Run(Function() HandleWebSocketDisconnect())
            Else
                Return
            End If
        End If

    End Function

    Private accountSummaryTaskCompletionSource As TaskCompletionSource(Of RateLimitInfo)
    Private Async Sub MonitorAuthentication()
        While webSocketClient.State = WebSocketState.Open
            Try
                Await RefreshWebSocketAuthentication()
                Await Task.Delay(60000) ' Check every minute
            Catch ex As Exception
                'txtLogs.AppendText("Error refreshing token: " & ex.Message + Environment.NewLine)
                AppendColoredText(txtLogs, "Error refreshing token: " & ex.Message, Color.Yellow)
            End Try
        End While
    End Sub

    Private Async Function ReconnectWebSocket() As Task
        'lblStatus.Text = "Reconnecting..."
        'txtLogs.AppendText("Reconnecting..." + Environment.NewLine)
        AppendColoredText(txtLogs, "Reconnecting...", Color.DodgerBlue)

        ' Ensure stop-loss protection during reconnection
        If SLTriggered AndAlso PositionSLOrderId IsNot Nothing AndAlso BestAskPrice > 0 Then
            Try
                Await ForceStopLossUpdate(If(TradeMode, BestAskPrice, BestBidPrice))
            Catch ex As Exception
                AppendColoredText(txtLogs, $"Failed to force SL update during reconnection: {ex.Message}", Color.Red)
            End Try
        End If

        If webSocketClient IsNot Nothing Then
            webSocketClient.Dispose()
        End If

        Await WebSocketCalls()
    End Function


    'Private Async Function EnableHeartbeat(intervalSeconds As Integer) As Task
    'Dim heartbeatPayload As String = $"{{""jsonrpc"":""2.0"",""id"":3,""method"":""public/set_heartbeat"",""params"":{{""interval"":{intervalSeconds}}}}}"
    '        Await SendWebSocketMessageAsync(heartbeatPayload)

    Private Async Function EnableDeribitHeartbeatEnhanced() As Task
        Try
            ' Use Deribit's official heartbeat API with proper JSON structure
            Dim heartbeatPayload As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", 1001},
            {"method", "public/set_heartbeat"},
            {"params", New JObject From {
                {"interval", 30}
            }}
        }

            Await SendWebSocketMessageAsync(heartbeatPayload.ToString())
            'AppendColoredText(txtLogs, "Deribit official heartbeat enabled (30s interval)", Color.Cyan)

        Catch ex As Exception
            AppendColoredText(txtLogs, "Heartbeat setup failed: " & ex.Message, Color.Red)
        End Try
    End Function

    Private Async Function MonitorConnectionHealth() As Task
        While webSocketClient?.State = WebSocketState.Open
            Try
                ' Send periodic ping using Deribit's official heartbeat API
                Dim pingPayload As New JObject From {
                {"jsonrpc", "2.0"},
                {"id", 9999},
                {"method", "public/ping"},
                {"params", New JObject()}
            }

                Await SendWebSocketMessageAsync(pingPayload.ToString())

                ' Wait 60 seconds before next health check
                Await Task.Delay(60000)

            Catch ex As Exception
                AppendColoredText(txtLogs, $"Connection health check failed: {ex.Message}", Color.Orange)
                Exit While
            End Try
        End While
    End Function


    'Rate limiter functions below
    Private Sub HandleAccountSummaryResponse(response As String)
        Try
            Dim json = JObject.Parse(response)

            ' Check if this is an account summary response (ID 999 from our request)
            Dim messageId = json.SelectToken("id")?.ToObject(Of Integer)()
            If messageId = 999 Then

                Dim errorField = json.SelectToken("error")
                If errorField IsNot Nothing Then
                    AppendColoredText(txtLogs, $"Account summary error: {errorField.ToString()}", Color.Yellow)

                    ' Complete the task with conservative defaults on error
                    If accountSummaryTaskCompletionSource IsNot Nothing Then
                        accountSummaryTaskCompletionSource.SetResult(New RateLimitInfo With {
                        .MaxCredits = 1000,
                        .RefillRate = 10,
                        .BurstLimit = 10,
                        .CurrentEstimatedCredits = 1000
                    })
                    End If
                    Return
                End If

                ' Extract rate limit information from the result
                Dim result = json.SelectToken("result")
                If result IsNot Nothing Then
                    Dim rateLimitInfo = ExtractRateLimitsFromAccountSummary(result)
                    AppendColoredText(txtLogs, $"Account summary received - Max Credits: {rateLimitInfo.MaxCredits}", Color.LimeGreen)

                    ' Complete the waiting task
                    If accountSummaryTaskCompletionSource IsNot Nothing Then
                        accountSummaryTaskCompletionSource.SetResult(rateLimitInfo)
                    End If
                End If
            End If

        Catch ex As Exception
            ' Ignore parsing errors for non-account-summary responses
        End Try
    End Sub

    Private Function ExtractRateLimitsFromAccountSummary(accountData As JToken) As RateLimitInfo
        Try
            ' Look for limits in the account summary response
            Dim limits = accountData.SelectToken("limits")
            If limits IsNot Nothing Then
                Dim matchingEngineLimit = limits.SelectToken("matching_engine")
                If matchingEngineLimit IsNot Nothing Then
                    Dim burst = matchingEngineLimit.SelectToken("burst")?.ToObject(Of Integer)()
                    Dim rate = matchingEngineLimit.SelectToken("rate")?.ToObject(Of Integer)()

                    If burst.HasValue AndAlso rate.HasValue Then
                        ' Calculate total credits using Deribit's formula
                        Dim totalCredits As Integer = CInt(Math.Round(burst.Value * 10000 / rate.Value))

                        Return New RateLimitInfo With {
                        .MaxCredits = totalCredits,
                        .RefillRate = 10, ' Standard 10 credits per millisecond
                        .BurstLimit = burst.Value,
                        .CurrentEstimatedCredits = totalCredits
                    }
                    End If
                End If
            End If

            ' Fallback to conservative estimates if limits not found
            Return New RateLimitInfo With {
            .MaxCredits = 2000,
            .RefillRate = 10,
            .BurstLimit = 20,
            .CurrentEstimatedCredits = 2000
        }

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error extracting rate limits: {ex.Message}", Color.Yellow)
            ' Return very conservative defaults
            Return New RateLimitInfo With {
            .MaxCredits = 2000,
            .RefillRate = 20,
            .BurstLimit = 20,
            .CurrentEstimatedCredits = 2000
        }
        End Try
    End Function

    Private Sub HandleRateLimitError(response As String)
        Try
            Dim json = JObject.Parse(response)
            Dim errorField = json.SelectToken("error")

            If errorField IsNot Nothing Then
                Dim errorCode = errorField.SelectToken("code")?.ToObject(Of Integer)()
                If errorCode = 10028 Then ' too_many_requests
                    AppendColoredText(txtLogs, "Rate limit exceeded - reducing API frequency", Color.Red)
                    ' Add null check for rateLimiter
                    If rateLimiter IsNot Nothing AndAlso accountLimits IsNot Nothing Then
                        Dim newMaxCredits = CInt(accountLimits.MaxCredits * 0.7)
                        rateLimiter = New DeribitRateLimiter(newMaxCredits, 200)
                        accountLimits.MaxCredits = newMaxCredits ' Update the stored limits too
                        AppendColoredText(txtLogs, $"Rate limiter adjusted to {newMaxCredits} max credits", Color.Yellow)
                    End If
                End If
            End If
        Catch ex As Exception
            ' Ignore parsing errors for non-JSON responses
        End Try
    End Sub

    Private Async Function InitializeRateLimitsAfterAuth() As Task
        Try
            AppendColoredText(txtLogs, "Updating rate limits from account summary...", Color.DodgerBlue)

            ' Try to get actual account limits (this should work now that we're authenticated)
            Dim actualLimits = Await GetAccountSummaryLimitsWithTimeout(3000) ' 3 second timeout

            If actualLimits IsNot Nothing Then
                ' Update with real limits
                rateLimiter = New DeribitRateLimiter(actualLimits.MaxCredits, 50)
                AppendColoredText(txtLogs, $"Rate limits updated: {actualLimits.MaxCredits} max credits, {actualLimits.MaxCredits / 50} req/sec", Color.LimeGreen)
            Else
                ' Keep using emergency rate limiter
                AppendColoredText(txtLogs, "Using emergency rate limiter: 2000 credits, 40 req/sec", Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Rate limit update error: {ex.Message}", Color.Yellow)
            ' Keep existing emergency rate limiter
        End Try
    End Function

    Private Async Function GetAccountSummaryLimitsWithTimeout(timeoutMs As Integer) As Task(Of RateLimitInfo)
        Try
            Using cts As New CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs))
                accountSummaryTaskCompletionSource = New TaskCompletionSource(Of RateLimitInfo)

                ' Send account summary request
                Dim payload As New JObject From {
                {"jsonrpc", "2.0"},
                {"id", 999},
                {"method", "private/get_account_summary"},
                {"params", New JObject From {{"currency", "BTC"}, {"extended", True}}}
            }

                Await SendWebSocketMessageAsync(payload.ToString())

                ' Wait for response with timeout
                Return Await accountSummaryTaskCompletionSource.Task.WaitAsync(cts.Token)
            End Using

        Catch ex As TaskCanceledException
            AppendColoredText(txtLogs, "Account summary request timed out - using fallback", Color.Yellow)
            Return Nothing
        Catch ex As Exception
            AppendColoredText(txtLogs, $"Account summary error: {ex.Message}", Color.Yellow)
            Return Nothing
        End Try
    End Function


    'Price-related subscriptions below
    '---------------------------------------------------------------------------------------------------------
    Private Async Function SubscribeToIndexPrice() As Task
        ' Subscribe to the BTC-PERPETUAL index price and user portfolio
        Dim subscriptionPayload = New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "public/subscribe"),
                New JProperty("params", New JObject(
                    New JProperty("channels", New JArray("deribit_price_index.btc_usd"))
                ))
            )

        'New JProperty("channels", New JArray("perpetual.BTC-PERPETUAL.raw"))

        Await SendWebSocketMessageAsync(subscriptionPayload.ToString())
    End Function

    Private Async Function SubscribeToUserPortfolio() As Task
        ' Subscribe to the BTC-PERPETUAL index price and user portfolio
        Dim subscriptionPayload = New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 4),
                New JProperty("method", "private/subscribe"),
                New JProperty("params", New JObject(
                    New JProperty("channels", New JArray("user.portfolio.btc"))
                ))
            )

        Await SendWebSocketMessageAsync(subscriptionPayload.ToString())
    End Function

    Private Async Function SubscribeToQuoteBTCPerpetual() As Task
        ' Create the subscription payload
        Dim subscriptionPayload = New JObject(
        New JProperty("jsonrpc", "2.0"),
        New JProperty("id", 6), ' Ensure a unique ID for this subscription
        New JProperty("method", "public/subscribe"),                      'Test if private/subscribe will work
        New JProperty("params", New JObject(
            New JProperty("channels", New JArray("quote.BTC-PERPETUAL"))
        ))
    )
        Await SendWebSocketMessageAsync(subscriptionPayload.ToString())
    End Function

    Private Async Function SubscribeToUserOrders() As Task
        ' Subscribe to the BTC-PERPETUAL index price and user portfolio
        Dim subscriptionPayload = New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 20),
                New JProperty("method", "private/subscribe"),
                New JProperty("params", New JObject(
                    New JProperty("channels", New JArray("user.changes.BTC-PERPETUAL.raw"))
                ))
            )

        Await SendWebSocketMessageAsync(subscriptionPayload.ToString())
    End Function

    'All Subscription Update Handling below
    '---------------------------------------------------------------------------------------------

    'Handle Index price updates from Websocket

    Private Async Sub HandleHeartbeat(response As String)
        Try
            ' Parse the WebSocket response
            Dim json = JObject.Parse(response)

            ' Check if the response is heartbeat request from server
            Dim messageType = json.SelectToken("method")?.ToString()

            ' Handle ping pong messages
            'Dim messageType2 As String = json.SelectToken("result")?.ToString()

            ' If messageType2 = "pong" Then
            ' txtLogs.AppendText("Pong" + Environment.NewLine)
            ' End If

            If messageType = "heartbeat" Then

                'Await SendWebSocketMessageAsync("{""jsonrpc"":""2.0"",""id"":4,""method"":""public/ping"",""params"":{}}")

                Me.Invoke(Sub()
                              '              txtLogs.AppendText("REQ. received." + Environment.NewLine)
                              radHeartBeat.BackColor = Color.Crimson
                          End Sub)

                Await SendWebSocketMessageAsync("{""jsonrpc"":""2.0"",""id"":4,""method"":""public/test"",""params"":{}}")

                Await Task.Delay(500)

                Me.Invoke(Sub()
                              'txtLogs.AppendText("ACK. sent." + Environment.NewLine)
                              radHeartBeat.BackColor = Color.Black
                          End Sub)

                ' Checks if got error message
                Dim errorField = json.SelectToken("error")
                If errorField IsNot Nothing Then
                    'txtLogs.AppendText("Error: " & errorField.ToString() + Environment.NewLine)
                    AppendColoredText(txtLogs, "Error: " & errorField.ToString(), Color.Yellow)
                End If

            End If
        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in HandleHeartbeat: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub HandleIndexUpdates(response As String)
        Try
            ' Parse the WebSocket response
            Dim json = JObject.Parse(response)

            ' Check if the response is for the deribit_price_index.btc_usd channel
            Dim channel = json.SelectToken("params.channel")?.ToString()
            If channel = "deribit_price_index.btc_usd" Then
                ' Extract the index price
                Dim indexPrice As String = json.SelectToken("params.data.price")
                Dim comms As Decimal = Nothing

                comms = 0.0005 * indexPrice
                comms = Math.Abs(Math.Round(comms, 0, MidpointRounding.AwayFromZero))

                ' Update public variables and textboxes on the UI thread
                If indexPrice IsNot Nothing And IsNumeric(indexPrice) Then
                    lastMessageTime = DateTime.Now
                    Me.Invoke(Sub()
                                  lblIndexPrice.Text = indexPrice
                                  txtComms.Text = comms
                              End Sub)
                End If

                ' Checks if got error message
                Dim errorField = json.SelectToken("error")
                If errorField IsNot Nothing Then
                    'txtLogs.AppendText("Error: " & errorField.ToString() + Environment.NewLine)
                    AppendColoredText(txtLogs, "Error: " & errorField.ToString(), Color.Yellow)
                End If

            End If
        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in HandleIndexUpdates: {ex.Message}", Color.Red)
        End Try
    End Sub

    'Handle user portfolio updates from Websocket
    Private Sub HandleBalanceUpdates(response As String)
        Try
            ' Parse the WebSocket response
            Dim json = JObject.Parse(response)

            ' Check if the response is for the deribit_price_index.btc_usd channel
            Dim channel = json.SelectToken("params.channel")?.ToString()
            If channel = "user.portfolio.btc" Then
                ' Extract the best bid and ask prices
                Dim btcEquity As Decimal = json.SelectToken("params.data.equity")
                Dim btcBalance As Decimal = json.SelectToken("params.data.balance")
                Dim btcSession As Decimal = btcEquity - btcBalance

                Dim USDEquity, Equiv, USDSession As Decimal

                ' Update public variables and textboxes on the UI thread
                '---------------------------------------------------------------

                If (btcEquity <> Nothing) And IsNumeric(lblIndexPrice.Text) Then
                    Me.Invoke(Sub()
                                  lblBTCEquity.Text = btcEquity.ToString("F8")
                                  USDEquity = Decimal.Parse(lblIndexPrice.Text.Trim) * btcEquity
                                  lblUSDEquity.Text = USDEquity.ToString("C", CultureInfo.CreateSpecificCulture("en-US"))
                              End Sub)
                End If

                If (btcBalance <> Nothing) And IsNumeric(lblIndexPrice.Text) Then
                    Me.Invoke(Sub()
                                  ' Update the label with the BTC balance
                                  lblBalance.Text = btcBalance.ToString("F8")
                                  Equiv = Decimal.Parse(lblIndexPrice.Text.Trim) * Decimal.Parse(lblBalance.Text.Trim)
                                  lblEquiv.Text = Equiv.ToString("C", CultureInfo.CreateSpecificCulture("en-US"))
                              End Sub)
                End If

                If (btcSession <> Nothing) And IsNumeric(lblIndexPrice.Text) Then
                    Me.Invoke(Sub()
                                  lblBTCSession.Text = btcSession.ToString("F8")
                                  USDSession = Decimal.Parse(lblIndexPrice.Text.Trim) * Decimal.Parse(lblBTCSession.Text.Trim)
                                  USDPublicSession = USDSession 'For circuitbreaker in auto trading in frmindicators
                                  lblUSDSession.Text = USDSession.ToString("C", CultureInfo.CreateSpecificCulture("en-US"))
                              End Sub)

                End If

                ' Checks if got error message
                Dim errorField = json.SelectToken("error")
                If errorField IsNot Nothing Then
                    'txtLogs.AppendText("Error: " & errorField.ToString() + Environment.NewLine)
                    AppendColoredText(txtLogs, "Error: " & errorField.ToString(), Color.Yellow)
                End If

                '---------------------------------------------------------------

                If btcSession < 0 Then
                    lblBTCEquity.ForeColor = Color.Firebrick
                    lblBTCSession.ForeColor = Color.Firebrick
                    lblUSDEquity.ForeColor = Color.Firebrick
                    lblUSDSession.ForeColor = Color.Firebrick
                Else
                    lblBTCEquity.ForeColor = Color.ForestGreen
                    lblBTCSession.ForeColor = Color.ForestGreen
                    lblUSDEquity.ForeColor = Color.ForestGreen
                    lblUSDSession.ForeColor = Color.ForestGreen
                End If
            End If
        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in HandleBalanceUpdates: {ex.Message}", Color.Red)
        End Try
    End Sub

    'Modify below to control how often stop loss is repositioned
    Private lastStopLossUpdate As DateTime = DateTime.MinValue
    Private Const MinStopLossUpdateInterval As Integer = 333 ' 0.3 second minimum between updates
    Private Const MinPriceMovementThreshold As Decimal = 5D ' Minimum $5 movement to trigger update
    Private newPricePublic As Decimal = 0 'For storing the price during emergency reduce market order for logging

    'Handle best bid/asks updates from Websocket
    Private Async Sub HandleQuoteUpdates(response As String)
        Try
            ' Parse the WebSocket response
            Dim json = JObject.Parse(response)

            ' Check if the response is for the quote.BTC-PERPETUAL channel
            Dim channel = json.SelectToken("params.channel")?.ToString()
            If channel = "quote.BTC-PERPETUAL" Then
                ' Extract the best bid and ask prices
                Dim bestBid = json.SelectToken("params.data.best_bid_price")?.ToObject(Of Decimal)()
                Dim bestAsk = json.SelectToken("params.data.best_ask_price")?.ToObject(Of Decimal)()

                ' Update public variables and textboxes on the UI thread
                If bestBid IsNot Nothing Then
                    BestBidPrice = bestBid
                    Me.Invoke(Sub()
                                  txtTopBid.Text = BestBidPrice.ToString("F2")
                              End Sub)
                End If

                If bestAsk IsNot Nothing Then
                    BestAskPrice = bestAsk
                    Me.Invoke(Sub()
                                  txtTopAsk.Text = BestAskPrice.ToString("F2")
                              End Sub)
                End If

                'For keeping current order at top of orderbook. +/- 3 leeway to reduce too many edit orders sent
                If (CurrentOpenOrderId IsNot Nothing) And (CurrentTPOrderId IsNot Nothing) And (CurrentSLOrderId IsNot Nothing) Then
                    If TradeMode = True Then
                        If bestBid > ((Decimal.Parse(txtPlacedPrice.Text)) + 3) Then
                            ' Add null check for rateLimiter
                            If rateLimiter IsNot Nothing AndAlso rateLimiter.CanMakeRequest() Then
                                'Stop if repositioned past ATR slippage threshold
                                If chkMaxSlippageATR.Checked And IsATRSlippageExcessive(bestBid, "LONG") Then
                                    Await CancelOrderAsync()
                                    'Return
                                Else
                                    Await UpdateLimitOrderWithOTOCOAsync(bestBid)

                                    Dim currentPlacedPrice As Decimal = 0D
                                    If Decimal.TryParse(txtPlacedPrice.Text, currentPlacedPrice) AndAlso currentPlacedPrice > 0 Then
                                        AppendColoredText(txtLogs, $"Order repositioned: ${currentPlacedPrice:F2} → ${bestBid:F2}", Color.Yellow)
                                    End If

                                    txtPlacedPrice.Text = bestBid
                                End If
                            Else
                                ' Handle both null limiter and rate limiting scenarios
                                If rateLimiter Is Nothing Then
                                    '-- First warn the log
                                    AppendColoredText(txtLogs, "Rate limiter not initialized – creating skipping order update", Color.Orange)

                                    '-- Fire-and-forget: get real limits without blocking the quote thread
                                    Dim _ignore = Task.Run(Async Function()
                                                               Await InitializeRateLimits()
                                                           End Function)

                                    '-- Install a conservative limiter so the very next tick can proceed
                                    rateLimiter = New DeribitRateLimiter(1000, 50)

                                Else
                                    ' Limiter exists but credits are currently insufficient
                                    AppendColoredText(txtLogs, "Skipping order update due to rate limits", Color.Orange)
                                End If
                            End If
                        End If
                    Else
                        If bestAsk < ((Decimal.Parse(txtPlacedPrice.Text)) - 3) Then
                            If rateLimiter IsNot Nothing AndAlso rateLimiter.CanMakeRequest() Then
                                If chkMaxSlippageATR.Checked And IsATRSlippageExcessive(bestAsk, "SHORT") Then
                                    Await CancelOrderAsync()
                                    'Return
                                Else
                                    Await UpdateLimitOrderWithOTOCOAsync(bestAsk)

                                    Dim currentPlacedPrice As Decimal = 0D
                                    If Decimal.TryParse(txtPlacedPrice.Text, currentPlacedPrice) AndAlso currentPlacedPrice > 0 Then
                                        AppendColoredText(txtLogs, $"Order repositioned: ${currentPlacedPrice:F2} → ${bestAsk:F2}", Color.Yellow)
                                    End If

                                    txtPlacedPrice.Text = bestAsk
                                End If
                            Else
                                If rateLimiter Is Nothing Then
                                    AppendColoredText(txtLogs, "Rate limiter not initialized - skipping order update", Color.Orange)
                                    Dim _ignore = Task.Run(Async Function()
                                                               Await InitializeRateLimits()
                                                           End Function)

                                    '-- Install a conservative limiter so the very next tick can proceed
                                    rateLimiter = New DeribitRateLimiter(1000, 50)
                                Else
                                    AppendColoredText(txtLogs, "Skipping order update due to rate limits", Color.Orange)
                                End If
                            End If
                        End If
                    End If
                End If

                'For keeping triggered stop loss order at top of orderbook. +/- 5 leeway to reduce too many edit orders sent
                If SLTriggered AndAlso PositionSLOrderId IsNot Nothing Then
                    Dim currentTime As DateTime = DateTime.UtcNow

                    ' Rate limiting: Only update if minimum time has passed
                    If (currentTime - lastStopLossUpdate).TotalMilliseconds >= MinStopLossUpdateInterval Then

                        ' Parse current stop loss price once to avoid multiple parsing
                        Dim currentStopPrice As Decimal = 0D
                        If Decimal.TryParse(txtPlacedStopLossPrice.Text, currentStopPrice) AndAlso currentStopPrice > 0 Then

                            ' Emergency condition: Check if price moved beyond emergency threshold
                            Dim emergencyThreshold As Decimal = Decimal.Parse(txtMarketStopLoss.Text)
                            Dim priceMovement As Decimal = 0D

                            If TradeMode Then
                                priceMovement = StopLossTriggerOriginal - bestAsk
                            Else
                                priceMovement = bestBid - StopLossTriggerOriginal
                            End If

                            ' Call ForceStopLossUpdate if emergency conditions are met


                            If priceMovement >= emergencyThreshold Then
                                If chkMarketStopLoss.Checked Then
                                    Await ForceStopLossUpdate(If(TradeMode, bestAsk, bestBid))
                                    Return ' Exit early after emergency update
                                End If
                            End If

                            'Normal conditions operation
                            Dim shouldUpdate As Boolean = False
                            Dim newStopPrice As Decimal = 0D

                            If TradeMode Then
                                ' Long position: Update when ask price moves significantly below current stop
                                If bestAsk < (currentStopPrice - MinPriceMovementThreshold) Then
                                    newStopPrice = bestAsk
                                    shouldUpdate = True
                                End If
                            Else
                                ' Short position: Update when bid price moves significantly above current stop
                                If bestBid > (currentStopPrice + MinPriceMovementThreshold) Then
                                    newStopPrice = bestBid
                                    shouldUpdate = True
                                End If
                            End If

                            ' Execute update if conditions are met
                            If shouldUpdate Then
                                Try
                                    ' Check if we should use force update instead of normal rate-limited update
                                    If priceMovement >= (emergencyThreshold * 0.5) Then ' 50% of emergency threshold
                                        Await ForceStopLossUpdate(newStopPrice)
                                    Else
                                        Await UpdateStopLossForTriggeredStopLossOrder(newStopPrice)
                                    End If

                                    txtPlacedStopLossPrice.Text = newStopPrice.ToString("F2")
                                    lastStopLossUpdate = currentTime

                                    AppendColoredText(txtLogs, $"SL repositioned: ${currentStopPrice:F2} → ${newStopPrice:F2}", Color.Orange)

                                Catch ex As Exception
                                    AppendColoredText(txtLogs, $"Critical SL update failed: {ex.Message}", Color.Red)

                                    ' Emergency fallback: Reset timer to allow immediate retry
                                    lastStopLossUpdate = DateTime.MinValue
                                End Try
                            End If
                            'Else
                            '    AppendColoredText(txtLogs, "Invalid stop loss price for repositioning", Color.Yellow)
                        End If
                    Else
                        ' Log rate limiting (optional - can be removed to reduce noise)
                        Dim remainingMs = MinStopLossUpdateInterval - (currentTime - lastStopLossUpdate).TotalMilliseconds
                        If remainingMs > 1000 Then ' Only log if significant time remaining
                            AppendColoredText(txtLogs, $"SL update rate limited: {remainingMs / 1000:F1}s remaining", Color.Gray)
                        End If
                    End If
                End If



                'For keeping current order at top of orderbook for trailing stop loss orders. +/- 3 leeway to reduce too many edit orders sent
                If (CurrentOpenOrderId IsNot Nothing) And (CurrentSLOrderId IsNot Nothing) And (isTrailingStopLossPlaced = True) Then
                    If TradeMode = True Then
                        If bestBid > ((Decimal.Parse(txtPlacedPrice.Text)) + 3) Then
                            ' Add null check for rateLimiter
                            If rateLimiter IsNot Nothing AndAlso rateLimiter.CanMakeRequest() Then
                                If chkMaxSlippageATR.Checked And IsATRSlippageExcessive(bestAsk, "LONG") Then
                                    Await CancelOrderAsync()
                                    'Return
                                Else
                                    Await UpdateStopLossForTrailingOrder(bestBid)
                                    txtPlacedPrice.Text = bestBid
                                End If
                            Else
                                ' Handle both null limiter and rate limiting scenarios
                                If rateLimiter Is Nothing Then
                                    AppendColoredText(txtLogs, "Rate limiter not initialized - skipping trailing update", Color.Orange)
                                    Dim _ignore = Task.Run(Async Function()
                                                               Await InitializeRateLimits()
                                                           End Function)

                                    '-- Install a conservative limiter so the very next tick can proceed
                                    rateLimiter = New DeribitRateLimiter(1000, 50)
                                Else
                                    AppendColoredText(txtLogs, "Skipping trailing order update due to rate limits", Color.Orange)
                                End If
                            End If
                        End If
                    Else
                        If bestAsk < ((Decimal.Parse(txtPlacedPrice.Text)) - 3) Then
                            If rateLimiter IsNot Nothing AndAlso rateLimiter.CanMakeRequest() Then
                                If chkMaxSlippageATR.Checked And IsATRSlippageExcessive(bestAsk, "SHORT") Then
                                    Await CancelOrderAsync()
                                    'Return
                                Else
                                    Await UpdateStopLossForTrailingOrder(bestAsk)
                                    txtPlacedPrice.Text = bestAsk
                                End If
                            Else
                                If rateLimiter Is Nothing Then
                                    AppendColoredText(txtLogs, "Rate limiter not initialized - skipping trailing update", Color.Orange)
                                    Dim _ignore = Task.Run(Async Function()
                                                               Await InitializeRateLimits()
                                                           End Function)

                                    '-- Install a conservative limiter so the very next tick can proceed
                                    rateLimiter = New DeribitRateLimiter(1000, 50)
                                Else
                                    AppendColoredText(txtLogs, "Skipping trailing order update due to rate limits", Color.Orange)
                                End If
                            End If
                        End If
                    End If
                End If



                'Check if a trailing order is in position and current price has hit take profit price. 
                'If yes, cancel stop loss and place trailing stop loss order
                If (isTrailingPosition = True) And (isTrailingStopLossPlaced = True) Then

                    If TradeMode = True Then

                        'To set price at which trailing stop loss order is triggered for placement. This directly uses txtTPOffset and txtComms numbers in realtime.
                        'If manual take profit textbox is not empty, use that
                        If Decimal.Parse(txtManualTP.Text) > 0 Then
                            TPTrailprice = Decimal.Parse(txtManualTP.Text)
                        Else
                            TPTrailprice = Decimal.Parse(txtPlacedPrice.Text) + (Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text))
                        End If
                        If TPTrailprice <= bestAsk Then
                            isTrailingStopLossPlaced = False
                            Await TrailingStopLossOrderAsync()
                        End If
                    Else
                        If Decimal.Parse(txtManualTP.Text) > 0 Then
                            TPTrailprice = Decimal.Parse(txtManualTP.Text)
                        Else
                            TPTrailprice = Decimal.Parse(txtPlacedPrice.Text) - (Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text))
                        End If
                        If TPTrailprice >= bestBid Then
                            isTrailingStopLossPlaced = False
                            Await TrailingStopLossOrderAsync()
                        End If
                    End If
                End If

                If Decimal.Parse(txtPlacedPrice.Text) > 0 Then
                    Dim PnL As Decimal
                    If TradeMode = True Then
                        PnL = (BestAskPrice - Decimal.Parse(txtPlacedPrice.Text)) * (Decimal.Parse(txtAmount.Text) / Decimal.Parse(txtPlacedPrice.Text))
                        If BestAskPrice < Decimal.Parse(txtPlacedPrice.Text) Then
                            Me.Invoke(Sub()
                                          lblPnL.ForeColor = Color.Red
                                      End Sub)
                        Else
                            Me.Invoke(Sub()
                                          lblPnL.ForeColor = Color.Chartreuse
                                      End Sub)
                        End If
                    Else
                        PnL = (Decimal.Parse(txtPlacedPrice.Text) - BestAskPrice) * (Decimal.Parse(txtAmount.Text) / Decimal.Parse(txtPlacedPrice.Text))
                        If BestAskPrice > Decimal.Parse(txtPlacedPrice.Text) Then
                            Me.Invoke(Sub()
                                          lblPnL.ForeColor = Color.Red
                                      End Sub)
                        Else
                            Me.Invoke(Sub()
                                          lblPnL.ForeColor = Color.Chartreuse
                                      End Sub)
                        End If
                    End If
                    Me.Invoke(Sub()
                                  lblPnL.Text = PnL.ToString("F2")
                              End Sub)
                Else
                    Me.Invoke(Sub()
                                  lblPnL.ForeColor = Color.Chartreuse
                                  lblPnL.Text = "0"
                              End Sub)
                End If
            End If
        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in HandleQuoteUpdates: {ex.Message}", Color.Red)
        End Try
    End Sub

    'For margin calculations when in position
    Private Async Function GetLivePositionData(instrumentName As String) As Task
        If isRequestingLiveData Then Exit Function         ' secondary guard
        isRequestingLiveData = True
        Try
            ' Check rate limit before making API call
            If rateLimiter IsNot Nothing AndAlso Not rateLimiter.CanMakeRequest() Then
                Dim waitTime = rateLimiter.GetWaitTimeMs()
                AppendColoredText(txtLogs, $"Rate limit reached for position data, waiting {waitTime}ms", Color.Yellow)
                Await Task.Delay(waitTime)
            End If

            ' Consume credits for the API call
            If rateLimiter IsNot Nothing Then
                rateLimiter.ConsumeCredits()
            End If

            ' Create the get_position request (using different ID from estimation)
            Dim positionPayload = New JObject(
            New JProperty("jsonrpc", "2.0"),
            New JProperty("id", 777), ' Different ID for live position data
            New JProperty("method", "private/get_position"),
            New JProperty("params", New JObject(
                New JProperty("instrument_name", instrumentName)
            ))
        )

            Await SendWebSocketMessageAsync(positionPayload.ToString())

            'AppendColoredText(txtLogs, $"Live position data requested for {instrumentName}", Color.Cyan)

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error requesting live position data: {ex.Message}", Color.Red)

        Finally
            isRequestingLiveData = False                ' unlock no matter what
        End Try
    End Function

    Private Function GetEquityBTC() As Decimal
        Dim eq As Decimal
        If Decimal.TryParse(lblBTCEquity.Text,
                        Globalization.NumberStyles.Any,
                        Globalization.CultureInfo.InvariantCulture, eq) Then
            Return eq
        End If
        Return 0D
    End Function


    ' Variable to track the specific order ID of interest
    Private CurrentOpenOrderId, CurrentTPOrderId, CurrentSLOrderId As String
    Private PositionTPOrderId, PositionSLOrderId As String
    Private isTrailingStop As Boolean = False
    Private isTrailingPosition As Boolean = False
    Private PositionEmpty As Boolean = False
    Private PositionLog As Boolean = False 'Flag to track if position log has been written
    Private OrderLog As Boolean = False 'Flag to track if order log has been written

    Private Async Sub HandleOrderPositionUpdates(response As String)
        Try

            ' Parse the WebSocket response
            Dim json = JObject.Parse(response)

            ' Check if the message is from the "user.changes.BTC-PERPETUAL.raw" channel
            Dim channel = json.SelectToken("params.channel")?.ToString()
            If channel = "user.changes.BTC-PERPETUAL.raw" Then
                Dim orderData = json.SelectToken("params.data")

                ' Check if the update relates to orders
                If orderData IsNot Nothing Then
                    Dim orders = orderData.SelectToken("orders")?.ToObject(Of List(Of JObject))()
                    If orders IsNot Nothing AndAlso orders.Count > 0 Then
                        Dim OpenOrderNo As Boolean = False
                        Dim unTrigOrder As Boolean = False
                        Dim OpenPositions As Boolean = False
                        Dim ExecPrice, PorLAmt As Decimal
                        Dim PorL As Boolean = True
                        Dim label4DB As String = Nothing

                        For Each order In orders
                            ' Extract relevant fields
                            Dim orderState = order.SelectToken("order_state")?.ToString()
                            Dim orderId = order.SelectToken("order_id")?.ToString()
                            Dim label = order.SelectToken("label")?.ToString()

                            'Process desc.:
                            'Step 1. Placed order : EntryLimitOrder = Open / TakeLimitProfit + StopLossOrder = Untriggered
                            'Step 2. In position : EntryLimitOrder = Filled / TakeLimitProfit + StopLossOrder = Triggered AND new TakeLimitProfit + StopLossOrder = Open

                            ' Process only "open" orders
                            If (orderState = "open") Then

                                Dim price = order.SelectToken("price")?.ToObject(Of Decimal?)()
                                Dim triggerPrice = order.SelectToken("trigger_price")?.ToObject(Of Decimal?)()

                                ' Update textboxes based on the label
                                Me.Invoke(Sub()

                                              Select Case label
                                                  Case "EntryLimitOrder"
                                                      txtPlacedPrice.Text = If(price?.ToString("F2"), "0")
                                                      OpenOrderNo = True
                                                      OpenPositions = False
                                                      ' Save the current order_id for tracking
                                                      CurrentOpenOrderId = orderId
                                                      lblOrderStatus.Text = "Order Placed"
                                                      lblOrderStatus.ForeColor = Color.Chartreuse

                                                      'When order is executed, TakeLimitProfit/StopLossOrder becomes 2 orders each
                                                      '- 1 with triggered state (The order before execution) and 1 with open state (Triggered by execution)
                                                  Case "TakeLimitProfit"
                                                      PositionTPOrderId = orderId

                                                      lblOrderStatus.Text = "In Position"
                                                      lblOrderStatus.ForeColor = Color.Yellow
                                                      OpenPositions = True
                                                      OpenOrderNo = False
                                                  Case "StopLossOrder"
                                                      PositionSLOrderId = orderId
                                                      SLTriggered = True

                                                      'This groups CRITICAL SL, Triggered SL messages together
                                                      If orderId = lastSLId Then
                                                          'AppendColoredText(txtLogs, pendingLocalMsg, Color.Red)
                                                          pendingLocalMsg = "" : lastSLId = ""
                                                      End If

                                                      If UpdateFlag = False Then
                                                          AppendColoredText(txtLogs, $"Triggered SL placed @ ${price}", Color.Red)
                                                      End If

                                                      txtPlacedStopLossPrice.Text = If(price?.ToString("F2"), "0")

                                                      lblOrderStatus.Text = "Stop Loss Triggered"
                                                      lblOrderStatus.ForeColor = Color.Red
                                                      OpenPositions = True
                                                      OpenOrderNo = False
                                                  Case "EntryTrailingOrder"
                                                      txtPlacedPrice.Text = If(price?.ToString("F2"), "0")
                                                      CurrentOpenOrderId = orderId
                                                      If Decimal.Parse(txtManualTP.Text) > 0 Then
                                                          txtPlacedTakeProfitPrice.Text = txtManualTP.Text
                                                      Else
                                                          If TradeMode = True Then
                                                              txtPlacedTakeProfitPrice.Text = Decimal.Parse(If(price?.ToString("F2"), "0")) + ((Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text)))
                                                          Else
                                                              txtPlacedTakeProfitPrice.Text = Decimal.Parse(If(price?.ToString("F2"), "0")) - ((Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text)))
                                                          End If

                                                      End If

                                                      OpenOrderNo = True
                                                      OpenPositions = False
                                                      isTrailingStop = True     'For checking if is trailing order when executing In Position code
                                                      isTrailingPosition = False  'For sanity confirm that it is not in position
                                                      ' Save the current order_id for tracking
                                                      CurrentOpenOrderId = orderId
                                                      lblOrderStatus.Text = "Order Placed"
                                                      lblOrderStatus.ForeColor = Color.Chartreuse
                                                  Case "TrailingStopLoss"
                                                      lblOrderStatus.Text = "In Position"
                                                      lblOrderStatus.ForeColor = Color.Yellow
                                                      OpenPositions = True
                                                      OpenOrderNo = False
                                                      isTrailingStop = True     'For checking if is trailing order when executing In Position code
                                                      isTrailingPosition = True
                                                      isTrailingStopLossPlaced = False  'For sanity check that trailing stop loss has been placed
                                                      ' Save the current order_id for tracking
                                                      CurrentOpenOrderId = orderId
                                              End Select
                                          End Sub)

                                'If UpdateFlag = True Then
                                ' If label = "StopLossOrder" Then
                                'AppendColoredText(txtLogs, $"Updated to: ${price}", Color.Crimson)
                                'Else
                                '   AppendColoredText(txtLogs, $"Updated to: ${price}", Color.Yellow)
                                'End If
                                'UpdateFlag = False
                                'End If


                            ElseIf (orderState = "untriggered") Then
                                'Dim price = order.SelectToken("price")?.ToObject(Of Decimal?)()
                                'Dim triggerPrice = order.SelectToken("trigger_price")?.ToObject(Of Decimal?)()

                                ' Update textboxes based on the label
                                Me.Invoke(Sub()
                                              Select Case label
                                                  Case "TakeLimitProfit"
                                                      Dim price = order.SelectToken("price")?.ToObject(Of Decimal?)()
                                                      Dim triggerPrice = order.SelectToken("trigger_price")?.ToObject(Of Decimal?)()

                                                      txtPlacedTakeProfitPrice.Text = If(price?.ToString("F2"), "0")
                                                      unTrigOrder = True
                                                      CurrentTPOrderId = orderId
                                                  Case "StopLossOrder"
                                                      Dim price = order.SelectToken("price")?.ToObject(Of Decimal?)()
                                                      Dim triggerPrice = order.SelectToken("trigger_price")?.ToObject(Of Decimal?)()

                                                      txtPlacedTrigStopPrice.Text = If(triggerPrice?.ToString("F2"), "0")
                                                      txtPlacedStopLossPrice.Text = If(price?.ToString("F2"), "0")
                                                      unTrigOrder = True
                                                      CurrentSLOrderId = orderId
                                                      PositionSLOrderId = orderId
                                                  Case "TrailingStopLoss"
                                                      'Trigger-price is received from channel only when order is placed/triggered/filled. 
                                                      'It doesn't update when market price moves and it dynamically adjusts.

                                                      Dim triggerPrice = order.SelectToken("trigger_price")?.ToObject(Of Decimal?)()
                                                      AppendColoredText(txtLogs, $"Trigger Price: ${triggerPrice}", Color.Yellow)
                                                      txtPlacedTakeProfitPrice.Text = If(triggerPrice?.ToString("F2"), "0")
                                                      unTrigOrder = True
                                                      CurrentTPOrderId = orderId
                                              End Select
                                          End Sub)

                            ElseIf (orderState = "filled") Then
                                Select Case label
                                    Case "EntryLimitOrder"
                                        lblOrderStatus.Text = "In Position"
                                        lblOrderStatus.ForeColor = Color.Yellow
                                        OpenPositions = True
                                        OpenOrderNo = False
                                        UpdateFlag = False
                                    Case "TakeLimitProfit"
                                        OpenPositions = True
                                        ExecPrice = order.SelectToken("price")?.ToObject(Of Decimal?)()
                                        PorLAmt = (ExecPrice - Decimal.Parse(txtPlacedPrice.Text)) * (Decimal.Parse(txtAmount.Text) / ExecPrice)
                                        PorLAmt = Math.Abs(Math.Round(PorLAmt, 2, MidpointRounding.AwayFromZero))
                                        PorL = True
                                        label4DB = label

                                    Case "StopLossOrder"
                                        OpenPositions = True
                                        ExecPrice = order.SelectToken("price")?.ToObject(Of Decimal?)()
                                        PorLAmt = (Decimal.Parse(txtPlacedPrice.Text) - ExecPrice) * (Decimal.Parse(txtAmount.Text) / ExecPrice)
                                        PorLAmt = Math.Abs(Math.Round(PorLAmt, 2, MidpointRounding.AwayFromZero))
                                        PorL = False
                                        SLTriggered = False
                                        'Need to calculate PorL when trailing stop loss is triggered
                                        label4DB = label

                                    Case "EntryTrailingOrder"
                                        lblOrderStatus.Text = "In Position"
                                        lblOrderStatus.ForeColor = Color.Yellow
                                        OpenPositions = True
                                        OpenOrderNo = False
                                        isTrailingStop = True 'For checking if is trailing order when executing In Position code
                                        isTrailingPosition = False   'For sanity confirm that it is not in position
                                        UpdateFlag = False
                                    Case "TrailingStopLoss"
                                        OpenPositions = True
                                        ExecPrice = order.SelectToken("average_price")?.ToObject(Of Decimal?)()
                                        PorLAmt = (ExecPrice - Decimal.Parse(txtPlacedPrice.Text)) * (Decimal.Parse(txtAmount.Text) / ExecPrice)
                                        PorLAmt = Math.Abs(Math.Round(PorLAmt, 2, MidpointRounding.AwayFromZero))
                                        PorL = True
                                        label4DB = label

                                    Case "ReduceLimitOrder"
                                        OpenPositions = True
                                        ExecPrice = order.SelectToken("price")?.ToObject(Of Decimal?)()
                                        If TradeMode = True Then
                                            If ExecPrice > Decimal.Parse(txtPlacedPrice.Text) Then
                                                PorLAmt = (ExecPrice - Decimal.Parse(txtPlacedPrice.Text)) * (Decimal.Parse(txtAmount.Text) / ExecPrice)
                                                PorLAmt = Math.Abs(Math.Round(PorLAmt, 2, MidpointRounding.AwayFromZero))
                                                PorL = True
                                                label4DB = label

                                            Else
                                                PorLAmt = (Decimal.Parse(txtPlacedPrice.Text) - ExecPrice) * (Decimal.Parse(txtAmount.Text) / ExecPrice)
                                                PorLAmt = Math.Abs(Math.Round(PorLAmt, 2, MidpointRounding.AwayFromZero))
                                                PorL = False
                                                label4DB = label

                                            End If
                                        Else
                                            If ExecPrice > Decimal.Parse(txtPlacedPrice.Text) Then
                                                PorLAmt = (Decimal.Parse(txtPlacedPrice.Text) - ExecPrice) * (Decimal.Parse(txtAmount.Text) / ExecPrice)
                                                PorLAmt = Math.Abs(Math.Round(PorLAmt, 2, MidpointRounding.AwayFromZero))
                                                PorL = False
                                                label4DB = label

                                            Else
                                                PorLAmt = (ExecPrice - Decimal.Parse(txtPlacedPrice.Text)) * (Decimal.Parse(txtAmount.Text) / ExecPrice)
                                                PorLAmt = Math.Abs(Math.Round(PorLAmt, 2, MidpointRounding.AwayFromZero))
                                                PorL = True
                                                label4DB = label

                                            End If
                                        End If

                                    Case "ReduceMarketOrder"
                                        OpenPositions = True 'Actually no positions but flagged true to use code in openpositions segment for cleanup
                                        If _indicators.IsAutoTradingEnabled Then
                                            LogTradeDecision("Exit Position - Market Order Loss", 0, 0) 'For autotrade log for when trade exit position
                                        End If
                                        ResetOrderAttempt() ' Reset ATR slippage tracking
                                        AppendColoredText(txtLogs, $"Position executed at {newPricePublic}.", Color.Crimson)
                                        AppendColoredText(txtLogs, $"Loss: Check order history.", Color.Crimson)

                                End Select
                            ElseIf orderState = "cancelled" Then
                                Select Case label
                                    Case "TakeLimitProfit"
                                        OpenPositions = True
                                    Case "StopLossOrder"
                                        OpenPositions = True
                                    Case "ReduceLimitOrder"
                                        OpenPositions = True
                                End Select
                            End If

                        Next


                        If _indicators.IsAutoTradingEnabled And OrderLog = False Then
                            '    If Not (txtPlacedPrice.Text = "0") And (txtPlacedTrigStopPrice.Text = "0") And (txtPlacedTakeProfitPrice.Text = "0") Then
                            If (OpenPositions = False) And (OpenOrderNo = True) Then
                                LogTradeDecision("Order Placed", 0, 0) 'For autotrade log for when order is placed
                                OrderLog = True
                            End If
                        End If


                        If OpenPositions = True Then

                            txtManualSL.Text = "0"
                            txtManualTP.Text = "0"


                            'If it is a trailing order, set flag that it is in position
                            If isTrailingStop = True Then
                                isTrailingPosition = True
                                'AppendColoredText(txtLogs, $"Trailing Position: ${isTrailingPosition}", Color.Yellow)
                            End If

                            ' === keep IDs alive while the ENTRY order is still open ===
                            'Dim entryStillPending As Boolean =
                            'orders.Any(Function(o) o.SelectToken("label")?.ToString() = "EntryLimitOrder" _
                            'AndAlso o.SelectToken("order_state")?.ToString() = "open")

                            'Stop autoplacement of orders at top of orderbook if position is found

                            'Dim tpPresent = orders.Any(Function(o) o.SelectToken("label")?.ToString() = "TakeLimitProfit")
                            'Dim slPresent = orders.Any(Function(o) o.SelectToken("label")?.ToString() = "StopLossOrder")

                            'If tpPresent AndAlso slPresent AndAlso Not entryStillPending Then
                            ' entry was filled/cancelled *and* both child legs are already on the book
                            CurrentOpenOrderId = Nothing
                            CurrentTPOrderId = Nothing
                            CurrentSLOrderId = Nothing
                            'End If

                            ' No orders found, check for positions
                            Dim positions = orderData.SelectToken("positions")?.ToObject(Of List(Of JObject))()
                            If positions IsNot Nothing AndAlso positions.Count > 0 Then

                                For Each position In positions

                                    Dim size = position.SelectToken("size")?.ToObject(Of Decimal)()

                                    If size <> 0 Then
                                        ' Prevent duplicate live data requests
                                        If Not isRequestingLiveData AndAlso (DateTime.Now - lastLiveDataRequest).TotalSeconds > 2 Then
                                            isRequestingLiveData = True
                                            lastLiveDataRequest = DateTime.Now

                                            Await GetLivePositionData("BTC-PERPETUAL")

                                            ' Reset flag after a delay
                                            Await Task.Delay(3000)
                                            isRequestingLiveData = False
                                        End If

                                        If _indicators.IsAutoTradingEnabled And (PositionLog = False) Then
                                            LogTradeDecision("In Position", 0, 0) 'For autotrade log for when trade is in position
                                            PositionLog = True ' Set flag to prevent duplicate logging
                                        End If

                                    Else
                                        ' Position closed - reset flags
                                        isRequestingLiveData = False
                                        lastLiveDataRequest = DateTime.MinValue

                                    End If

                                    If size = 0 Then ' Position has been closed

                                        'Reset all flags
                                        OpenPositions = False
                                        isTrailingStop = False
                                        isTrailingPosition = False
                                        isTrailingStopLossPlaced = False
                                        SLTriggered = False
                                        StopLossTriggerOriginal = 0

                                        PositionEmpty = True
                                        PositionLog = False ' Reset position log flag so it can log next new position
                                        OrderLog = False ' Reset order log flag so it can log next new order

                                        Await CancelOrderAsync()

                                        'Clearing margin displays
                                        Me.Invoke(Sub()
                                                      lblEstimatedLiquidation.Text = "L.Liq: N/A"
                                                      lblInitialMargin.Text = "L.IM: N/A"
                                                      lblMaintenanceMargin.Text = "L.MM: N/A"
                                                      lblEstimatedLeverage.Text = "L.Lev: N/A"

                                                      ' Reset colors
                                                      lblEstimatedLiquidation.ForeColor = Color.Gray
                                                      lblEstimatedLeverage.ForeColor = Color.Gray
                                                  End Sub)


                                        If (PorL = True) And (PorLAmt > 0) Then
                                            AppendColoredText(txtLogs, $"Position executed at {ExecPrice}.", Color.LimeGreen)
                                            AppendColoredText(txtLogs, $"Profit made: ${PorLAmt}.", Color.LimeGreen)

                                            If _indicators.IsAutoTradingEnabled Then
                                                LogTradeDecision("Exit Position - Profit", PorLAmt, ExecPrice) 'For autotrade log for when trade exit position
                                            End If

                                        ElseIf (PorL = False) And (PorLAmt > 0) Then
                                            AppendColoredText(txtLogs, $"Position executed at {ExecPrice}.", Color.Crimson)
                                            AppendColoredText(txtLogs, $"Loss of: ${PorLAmt}.", Color.Crimson)

                                            If _indicators.IsAutoTradingEnabled Then
                                                LogTradeDecision("Exit Position - Loss", PorLAmt, ExecPrice) 'For autotrade log for when trade exit position
                                            End If

                                        End If

                                        'To record to DB
                                        ' In HandleOrderPositionUpdates
                                        If PorLAmt > 0 Then
                                            Dim tradeId = RecordCompletedTrade(
                                                Decimal.Parse(txtPlacedPrice.Text),
                                                ExecPrice,
                                                Decimal.Parse(txtAmount.Text),
                                                PorLAmt,
                                                PorL,
                                                TradeMode,
                                                label4DB
                                            )
                                        End If

                                        ' Update last trade time immediately to prevent multiple rapid executions
                                        _indicators.lastAutoTradeTime = DateTime.Now

                                    Else
                                        OpenPositions = True
                                        'lblOrderStatus.Text = "In Position"
                                        'lblOrderStatus.ForeColor = Color.Yellow
                                        'Dim price = position.SelectToken("average_price")?.ToObject(Of Decimal?)()
                                        'txtPlacedPrice.Text = If(price?.ToString("F2"), "0")
                                    End If


                                Next
                            End If



                        End If

                    End If


                End If
            End If
            '            End If
        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in HandleOrderPositionUpdates: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub HandleOrderUpdates(message As String)
        ' Log every received WebSocket message for debugging
        'AppendColoredText(txtLogs, "WebSocket Update Received: " & message, Color.LimeGreen)

        ' Check if the message contains any trigger price updates
        If message.Contains("trigger_price") Then
            AppendColoredText(txtLogs, "Detected Trigger Price Update: " & message, Color.LimeGreen)
        End If
    End Sub

    ' Add these variables to your order placement logic
    Private originalSignalPrice As Decimal = 0
    Private orderCreationTime As DateTime = DateTime.MinValue
    Private currentRequoteCount As Integer = 0

    Private Function CalculateATRSlippageLimit() As Decimal
        ' Get current ATR from your indicators form
        Dim currentATR As Decimal
        If Not Decimal.TryParse(_indicators.lblATR.Text, currentATR) Then
            Return 70 ' Fallback to $70 if ATR unavailable
        End If

        ' Get ATR multiplier from settings
        Dim atrMultiplier As Decimal
        If Not Decimal.TryParse(txtMaxSlippageATR.Text, atrMultiplier) Then
            atrMultiplier = 0.6D ' Default 0.6x ATR
        End If

        Return currentATR * atrMultiplier
    End Function

    Private Function IsATRSlippageExcessive(currentPrice As Decimal, direction As String) As Boolean
        If originalSignalPrice = 0 Then
            originalSignalPrice = currentPrice ' Set initial price
            Return False
        End If

        Dim slippageLimit As Decimal = CalculateATRSlippageLimit()
        Dim actualSlippage As Decimal = Math.Abs(currentPrice - originalSignalPrice)

        ' Calculate slippage in ATR units for logging
        Dim currentATR As Decimal = Decimal.Parse(_indicators.lblATR.Text)
        Dim slippageInATR As Decimal = If(currentATR > 0, actualSlippage / currentATR, 0)

        If actualSlippage > slippageLimit Then
            AppendColoredText(txtLogs, $"{direction} slippage ${actualSlippage:F2} ({slippageInATR:F2}x ATR) exceeds limit ${slippageLimit:F2}", Color.Red)

            ' Reset for next attempt
            ResetOrderAttempt() 'Temp fix - suppose to call LogFailedEntry below which is giving an error

            ' Log failed attempt
            'LogFailedEntry("ATR slippage exceeded", slippageInATR)
            Return True
        End If

        ' Log acceptable slippage
        'AppendColoredText(txtLogs, $"{direction} slippage ${actualSlippage:F2} ({slippageInATR:F2}x ATR) within limit", Color.Gray)
        Return False
    End Function

    Private Sub LogFailedEntry(reason As String, Optional slippageATR As Decimal = 0)
        ' Create failed trade record
        Dim failedTrade = New TradeRecord With {
        .AttemptType = "Failed",
        .OrderType = reason,
        .Timestamp = DateTime.UtcNow,
        .RequoteCount = currentRequoteCount,
        .SignalPrice = originalSignalPrice,
        .SlippageATR = slippageATR,
        .MaxSlippageExceeded = True
    }

        ' You could optionally save failed attempts to database for analysis

        ' Engage cooldown
        Dim cooloffMins = Integer.Parse(_autotradesettings.txtCooloff.Text)
        _indicators.lastAutoTradeTime = DateTime.Now.AddMinutes(cooloffMins)

        'AppendLog($"Entry failed: {reason}. Cooloff: {cooloffMins}min", Color.Orange)

        ' Reset for next attempt
        ResetOrderAttempt()
    End Sub

    Private Sub ResetOrderAttempt()
        currentRequoteCount = 0
        orderCreationTime = DateTime.MinValue
        originalSignalPrice = 0
    End Sub


    'All order execution code below
    '-----------------------------------------------------------------------
    Private Async Function ExecuteOrderAsync(TypeOfOrder As String) As Task
        Try
            Dim takeprofitprice As Decimal
            Dim stoplossTriggerPrice As Decimal
            Dim triggeroffset As Decimal
            Dim stoplossPrice As Decimal
            Dim BestPrice As Decimal
            Dim ordermethod As String = String.Empty ' Default value to avoid warnings
            Dim direction As String = String.Empty ' Default value for direction
            Dim ordertype As String = String.Empty
            Dim MarketOrderType As Boolean = False

            ' Ensure WebSocket is connected
            If webSocketClient Is Nothing OrElse webSocketClient.State <> WebSocketState.Open Then
                AppendColoredText(txtLogs, "WebSocket is not connected.", Color.Red)
                Return
            End If

            ' Validate the input amount
            Dim amountText As String = txtAmount.Text
            Dim amount As Decimal

            If Not Decimal.TryParse(amountText, amount) OrElse amount <= 0 Then
                AppendColoredText(txtLogs, "Please enter a valid positive amount.", Color.Yellow)
                Return
            End If

            Select Case TypeOfOrder
                Case "BuyLimit"

                    ' Ensure BestBidPrice is valid
                    If BestBidPrice <= 0 Then
                        AppendColoredText(txtLogs, "Best bid price is not valid.", Color.Yellow)
                        Return
                    End If

                    BestPrice = BestBidPrice

                    'If manual take profit textbox is not empty, use that
                    If Decimal.Parse(txtManualTP.Text) > 0 Then
                        takeprofitprice = Decimal.Parse(txtManualTP.Text)
                    Else
                        takeprofitprice = BestPrice + Decimal.Parse(txtTakeProfit.Text)
                    End If

                    takeprofitprice = If(Decimal.Parse(txtManualTP.Text) > 0, Decimal.Parse(txtManualTP.Text), BestPrice + Decimal.Parse(txtTakeProfit.Text))

                    'If manual stop loss textbox is not empty, use that
                    If Decimal.Parse(txtManualSL.Text) > 0 Then
                        stoplossPrice = Decimal.Parse(txtManualSL.Text)
                        stoplossTriggerPrice = Decimal.Parse(txtManualSL.Text) + Decimal.Parse(txtStopLoss.Text)
                    Else
                        stoplossTriggerPrice = BestPrice - Decimal.Parse(txtTrigger.Text)
                        stoplossPrice = BestPrice - (Decimal.Parse(txtStopLoss.Text) + Decimal.Parse(txtTrigger.Text))
                        'stoplossPrice = BestPrice - Decimal.Parse(txtTrigger.Text) + Decimal.Parse(txtStopLoss.Text)
                    End If

                    'For initiating ATR Slippage function
                    direction = "LONG"
                    If IsATRSlippageExcessive(BestPrice, direction) Then
                        Return
                    End If

                    triggeroffset = Decimal.Parse(txtTriggerOffset.Text)

                    ordermethod = "private/buy"
                    direction = "sell"
                    ordertype = "limit"


                Case "SellLimit"

                    ' Ensure BestAskPrice is valid
                    If BestAskPrice <= 0 Then
                        AppendColoredText(txtLogs, "Best ask price Is Not valid.", Color.Yellow)
                        Return
                    End If

                    BestPrice = BestAskPrice

                    'If manual take profit textbox is not empty, use that
                    If Decimal.Parse(txtManualTP.Text) > 0 Then
                        takeprofitprice = Decimal.Parse(txtManualTP.Text)
                    Else
                        takeprofitprice = BestPrice - Decimal.Parse(txtTakeProfit.Text)
                    End If

                    'If manual stop loss textbox is not empty, use that
                    If Decimal.Parse(txtManualSL.Text) > 0 Then
                        stoplossPrice = Decimal.Parse(txtManualSL.Text)
                        stoplossTriggerPrice = Decimal.Parse(txtManualSL.Text) - Decimal.Parse(txtStopLoss.Text)
                    Else
                        stoplossTriggerPrice = BestPrice + Decimal.Parse(txtTrigger.Text)
                        stoplossPrice = BestPrice + (Decimal.Parse(txtStopLoss.Text) + Decimal.Parse(txtTrigger.Text))
                        'stoplossPrice = BestPrice + Decimal.Parse(txtTrigger.Text) - Decimal.Parse(txtStopLoss.Text)
                    End If

                    'For initiating ATR Slippage function
                    direction = "SHORT"
                    If IsATRSlippageExcessive(BestPrice, direction) Then
                        Return
                    End If

                    triggeroffset = Decimal.Parse(txtTriggerOffset.Text)

                    ordermethod = "private/sell"
                    direction = "buy"
                    ordertype = "limit"


                Case "BuyNoSpread"

                    ' Ensure BestBidPrice is valid
                    If BestAskPrice <= 0 Then
                        AppendColoredText(txtLogs, "Best bid price Is Not valid.", Color.Yellow)
                        Return
                    End If

                    BestPrice = BestAskPrice - 0.5

                    'If manual take profit textbox is not empty, use that
                    If Decimal.Parse(txtManualTP.Text) > 0 Then
                        takeprofitprice = Decimal.Parse(txtManualTP.Text)
                    Else
                        takeprofitprice = BestPrice + Decimal.Parse(txtTakeProfit.Text)
                    End If

                    'If manual stop loss textbox is not empty, use that
                    If Decimal.Parse(txtManualSL.Text) > 0 Then
                        stoplossPrice = Decimal.Parse(txtManualSL.Text)
                        stoplossTriggerPrice = Decimal.Parse(txtManualSL.Text) + Decimal.Parse(txtStopLoss.Text)
                    Else
                        stoplossTriggerPrice = BestPrice - Decimal.Parse(txtTrigger.Text)
                        stoplossPrice = BestPrice - (Decimal.Parse(txtStopLoss.Text) + Decimal.Parse(txtTrigger.Text))
                    End If

                    'For initiating ATR Slippage function
                    direction = "LONG"
                    If IsATRSlippageExcessive(BestPrice, direction) Then
                        Return
                    End If

                    triggeroffset = Decimal.Parse(txtTriggerOffset.Text)

                    ordermethod = "private/buy"
                    direction = "sell"
                    ordertype = "limit"


                Case "SellNoSpread"

                    ' Ensure BestAskPrice is valid
                    If BestBidPrice <= 0 Then
                        AppendColoredText(txtLogs, "Best ask price Is Not valid.", Color.Yellow)
                        Return
                    End If

                    BestPrice = BestBidPrice + 0.5

                    'If manual take profit textbox is not empty, use that
                    If Decimal.Parse(txtManualTP.Text) > 0 Then
                        takeprofitprice = Decimal.Parse(txtManualTP.Text)
                    Else
                        takeprofitprice = BestPrice - Decimal.Parse(txtTakeProfit.Text)
                    End If

                    'If manual stop loss textbox is not empty, use that
                    If Decimal.Parse(txtManualSL.Text) > 0 Then
                        stoplossPrice = Decimal.Parse(txtManualSL.Text)
                        stoplossTriggerPrice = Decimal.Parse(txtManualSL.Text) - Decimal.Parse(txtStopLoss.Text)
                    Else
                        stoplossTriggerPrice = BestPrice + Decimal.Parse(txtTrigger.Text)
                        stoplossPrice = BestPrice + (Decimal.Parse(txtStopLoss.Text) + Decimal.Parse(txtTrigger.Text))
                    End If

                    'For initiating ATR Slippage function
                    direction = "SHORT"
                    If IsATRSlippageExcessive(BestPrice, direction) Then
                        Return
                    End If

                    triggeroffset = Decimal.Parse(txtTriggerOffset.Text)

                    ordermethod = "private/sell"
                    direction = "buy"
                    ordertype = "limit"


                Case "BuyMarket"

                    MarketOrderType = True

                    ' Ensure BestBidPrice is valid
                    If BestAskPrice <= 0 Then
                        AppendColoredText(txtLogs, "Best bid price Is Not valid.", Color.Yellow)
                        Return
                    End If

                    BestPrice = BestAskPrice - 0.5

                    'If manual take profit textbox is not empty, use that
                    If Decimal.Parse(txtManualTP.Text) > 0 Then
                        takeprofitprice = Decimal.Parse(txtManualTP.Text)
                    Else
                        takeprofitprice = BestPrice + Decimal.Parse(txtTakeProfit.Text)
                    End If

                    'If manual stop loss textbox is not empty, use that
                    If Decimal.Parse(txtManualSL.Text) > 0 Then
                        stoplossPrice = Decimal.Parse(txtManualSL.Text)
                        stoplossTriggerPrice = Decimal.Parse(txtManualSL.Text) + Decimal.Parse(txtStopLoss.Text)
                    Else
                        stoplossTriggerPrice = BestPrice - Decimal.Parse(txtTrigger.Text)
                        stoplossPrice = BestPrice - (Decimal.Parse(txtStopLoss.Text) + Decimal.Parse(txtTrigger.Text))
                    End If

                    triggeroffset = Decimal.Parse(txtTriggerOffset.Text)

                    ordermethod = "private/buy"
                    direction = "sell"
                    ordertype = "market"

                Case "SellMarket"

                    MarketOrderType = True

                    ' Ensure BestAskPrice is valid
                    If BestBidPrice <= 0 Then
                        AppendColoredText(txtLogs, "Best ask price Is Not valid.", Color.Yellow)
                        Return
                    End If

                    BestPrice = BestBidPrice + 0.5

                    'If manual take profit textbox is not empty, use that
                    If Decimal.Parse(txtManualTP.Text) > 0 Then
                        takeprofitprice = Decimal.Parse(txtManualTP.Text)
                    Else
                        takeprofitprice = BestPrice - Decimal.Parse(txtTakeProfit.Text)
                    End If

                    'If manual stop loss textbox is not empty, use that
                    If Decimal.Parse(txtManualSL.Text) > 0 Then
                        stoplossPrice = Decimal.Parse(txtManualSL.Text)
                        stoplossTriggerPrice = Decimal.Parse(txtManualSL.Text) - Decimal.Parse(txtStopLoss.Text)
                    Else
                        stoplossTriggerPrice = BestPrice + Decimal.Parse(txtTrigger.Text)
                        stoplossPrice = BestPrice + (Decimal.Parse(txtStopLoss.Text) + Decimal.Parse(txtTrigger.Text))
                    End If

                    triggeroffset = Decimal.Parse(txtTriggerOffset.Text)

                    ordermethod = "private/sell"
                    direction = "buy"
                    ordertype = "market"

                Case Else
                    ' Handle unexpected or unsupported order types
                    AppendColoredText(txtLogs, "Unsupported order type specified.", Color.IndianRed)
                    Return
            End Select

            StopLossTriggerOriginal = stoplossTriggerPrice ' Save the original SL trigger price

            ' Construct the JSON payload for the reduce-only order
            Dim params As New JObject(
     New JProperty("instrument_name", "BTC-PERPETUAL"),
        New JProperty("amount", amount),
        New JProperty("type", ordertype),
        New JProperty("label", If(MarketOrderType, "EntryMarketOrder", "EntryLimitOrder")),
        New JProperty("time_in_force", "good_til_cancelled"),
        New JProperty("linked_order_type", "one_triggers_one_cancels_other"),
        New JProperty("trigger_fill_condition", "first_hit"),
        New JProperty("reject_post_only", False),
        New JProperty("otoco_config", New JArray(
            New JObject(
                New JProperty("amount", amount),
                New JProperty("direction", direction),
                New JProperty("type", "limit"),
                New JProperty("label", "TakeLimitProfit"),
                New JProperty("price", takeprofitprice),
                New JProperty("time_in_force", "good_til_cancelled"),
                New JProperty("post_only", True)
            ),
            New JObject(
            New JProperty("amount", amount),
            New JProperty("direction", direction),
            New JProperty("type", "stop_limit"),
            New JProperty("trigger_price", stoplossTriggerPrice), ' Base trigger price                    
            New JProperty("price", stoplossPrice), ' Stop loss limit price
            New JProperty("label", "StopLossOrder"),
            New JProperty("time_in_force", "good_til_cancelled"),
            New JProperty("trigger_offset", triggeroffset), ' Offset for dynamic adjustment
            New JProperty("post_only", True),    'False guaranteed to work, but likely to become market order
            New JProperty("reduce_only", True),
            New JProperty("reject_post_only", False),
            New JProperty("trigger", "last_price")
            )
        ))
    )

            'Lines below taken out from stop loss order OTOCO code
            'New JProperty("trigger_offset", triggeroffset), ' Offset for dynamic adjustment
            'New JProperty("post_only", True)    'False guaranteed to work, but likely to become market order
            'New JProperty("reject_post_only", False),

            'New JProperty("reduce_only", True),                 
            'NOTE: It seems putting reduce_only calls in take profit will cause cancellation of both take profit and stop loss orders when stop loss trigger is hit, leaving a position open with no stop loss.


            ' Add price property only for limit orders
            If Not MarketOrderType Then
                params.Add("price", BestPrice)
                params.Add("post_only", True) ' Post-only is valid only for limit orders
            End If

            ' Prepare the payload for the linked order
            Dim OrderPayload As New JObject(
    New JProperty("jsonrpc", "2.0"),
    New JProperty("id", 2),
    New JProperty("method", ordermethod),
    New JProperty("params", params)
)

            'AppendColoredText(txtLogs, OrderPayload.ToString(), Color.LightGray)


            ' Send the order and capture the server's response
            Await SendWebSocketMessageAsync(OrderPayload.ToString())

            txtPlacedTakeProfitPrice.Text = takeprofitprice.ToString("F2")
            txtPlacedTrigStopPrice.Text = stoplossTriggerPrice.ToString("F2")
            txtPlacedStopLossPrice.Text = stoplossPrice.ToString("F2")

            txtPlacedPrice.Text = BestPrice.ToString("F2")

            If TypeOfOrder = "BuyLimit" Then
                ' Optional: Handle post-order logic (e.g., display confirmation)
                AppendColoredText(txtLogs, $"Buy limit order placed For {amount} at {BestPrice}.", Color.MediumSeaGreen)
            ElseIf TypeOfOrder = "SellLimit" Then
                ' Optional: Handle post-order logic (e.g., display confirmation)
                AppendColoredText(txtLogs, $"Sell limit order placed For {amount} at {BestPrice}.", Color.Crimson)
            ElseIf TypeOfOrder = "BuyNoSpread" Then
                AppendColoredText(txtLogs, $"Buy limit no spread order placed For {amount} at {BestPrice}.", Color.MediumSeaGreen)
            ElseIf TypeOfOrder = "SellNoSpread" Then
                AppendColoredText(txtLogs, $"Sell limit no spread order placed For {amount} at {BestPrice}.", Color.Crimson)
            ElseIf TypeOfOrder = "BuyMarket" Then
                AppendColoredText(txtLogs, $"Market buy order placed For {amount} starting at {BestPrice}.", Color.MediumSeaGreen)
            ElseIf TypeOfOrder = "SellMarket" Then
                AppendColoredText(txtLogs, $"Market sell order placed For {amount} starting at {BestPrice}.", Color.Crimson)
            End If

        Catch ex As Exception
            ' Handle any errors
            AppendColoredText(txtLogs, "Error placing order: " & ex.Message, Color.Red)
        End Try
    End Function


    Private Async Function CancelOrderAsync() As Task

        Dim cancelPayload As New JObject(
        New JProperty("jsonrpc", "2.0"),
        New JProperty("id", 30),
        New JProperty("method", "private/cancel_all_by_instrument"),
        New JProperty("params", New JObject(
        New JProperty("instrument_name", "BTC-PERPETUAL"),
        New JProperty("type", "all")
        ))
    )

        Await SendWebSocketMessageAsync(cancelPayload.ToString())

        Me.Invoke(Sub()
                      txtPlacedPrice.Text = "0"
                      txtPlacedTakeProfitPrice.Text = "0"
                      txtPlacedTrigStopPrice.Text = "0"
                      txtPlacedStopLossPrice.Text = "0"
                  End Sub)
        lblOrderStatus.Text = "Awaiting Orders"
        lblOrderStatus.ForeColor = Color.DeepSkyBlue

        'Reset all flags
        isTrailingStop = False
        isTrailingPosition = False
        isTrailingStopLossPlaced = False
        SLTriggered = False
        StopLossTriggerOriginal = 0

        'PositionEmpty = True
        PositionLog = False ' Reset position log flag so it can log next new position
        OrderLog = False ' Reset order log flag so it can log next new order

        ResetOrderAttempt() ' Reset ATR slippage tracking

        'Clearing margin displays
        Me.Invoke(Sub()
                      lblEstimatedLiquidation.Text = "L.Liq: N/A"
                      lblInitialMargin.Text = "L.IM: N/A"
                      lblMaintenanceMargin.Text = "L.MM: N/A"
                      lblEstimatedLeverage.Text = "L.Lev: N/A"

                      ' Reset colors
                      lblEstimatedLiquidation.ForeColor = Color.Gray
                      lblEstimatedLeverage.ForeColor = Color.Gray
                  End Sub)

        If PositionEmpty = False Then
            AppendColoredText(txtLogs, $"Cancelled all open orders", Color.Yellow)
        Else
            PositionEmpty = False
        End If

    End Function


    Private Async Function SendReduceOrderAsync(price As Decimal?, amount As Decimal, direction As String, isMarketOrder As Boolean) As Task
        Try
            'Remember to do a cancel all orders here before sending reduce order
            'Await CancelOrderAsync()

            ' Determine the order type (limit or market)
            Dim orderType As String = If(isMarketOrder, "market", "limit")

            If isMarketOrder Then
                StopLossTriggerOriginal = 0
            End If

            ' Construct the JSON payload for the reduce-only order
            Dim params As New JObject(
            New JProperty("instrument_name", "BTC-PERPETUAL"),
            New JProperty("amount", amount),
            New JProperty("type", orderType),
            New JProperty("reduce_only", True),
            New JProperty("time_in_force", "good_til_cancelled"),
            New JProperty("label", If(isMarketOrder, "ReduceMarketOrder", "ReduceLimitOrder"))
        )

            ' Add price property only for limit orders
            If Not isMarketOrder Then
                If price Is Nothing OrElse price <= 0 Then
                    Throw New ArgumentException("Price must be specified for limit orders.")
                End If
                params.Add("price", price)
                params.Add("post_only", True) ' Post-only is valid only for limit orders
            End If

            Dim payload As New JObject(
            New JProperty("jsonrpc", "2.0"),
            New JProperty("id", 1),
            New JProperty("method", If(direction = "buy", "private/buy", "private/sell")),
            New JProperty("params", params)
        )
            'Reset trailing order flags
            isTrailingPosition = False
            isTrailingStop = False

            ' Send the payload via WebSocket
            Await SendWebSocketMessageAsync(payload.ToString())

            Dim orderDescription As String = $"{orderType.ToUpper()} {direction} {amount} {(If(isMarketOrder, "", $"@ {price}"))}"
            AppendColoredText(txtLogs, $"Reduce-only {orderDescription} order sent.", Color.Green)

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in SendReduceOrderAsync: {ex.Message}", Color.Red)
        End Try
    End Function
    Private UpdateFlag As Boolean = False
    Private Async Function UpdateLimitOrderWithOTOCOAsync(newPrice As Decimal) As Task
        Try
            ' Ensure rate limiter exists
            If rateLimiter Is Nothing Then
                AppendColoredText(txtLogs, "Rate limiter not initialized - creating emergency limiter", Color.Yellow)
                rateLimiter = New DeribitRateLimiter(1000, 50) ' Emergency conservative limiter
            End If

            ' Check rate limit before making API calls
            If Not rateLimiter.CanMakeRequest() Then
                Dim waitTime = rateLimiter.GetWaitTimeMs()
                AppendColoredText(txtLogs, $"Rate limit reached, waiting {waitTime}ms", Color.Yellow)
                Await Task.Delay(waitTime)
            End If

            ' Only proceed if we can consume credits for all 3 API calls needed
            If Not (rateLimiter.ConsumeCredits() AndAlso rateLimiter.CanMakeRequest() AndAlso rateLimiter.CanMakeRequest()) Then
                AppendColoredText(txtLogs, "Insufficient credits for order update - skipping", Color.Orange)
                Return
            End If

            Dim newTPprice, newTrigSLprice, newSLprice As Decimal
            Dim amount As Decimal = Decimal.Parse(txtAmount.Text)

            ' Your existing price calculation logic remains the same
            If TradeMode = True Then
                If Decimal.Parse(txtManualTP.Text) > 0 Then
                    newTPprice = Decimal.Parse(txtManualTP.Text)
                Else
                    newTPprice = newPrice + Decimal.Parse(txtTakeProfit.Text)
                End If

                If Decimal.Parse(txtManualSL.Text) > 0 Then
                    newSLprice = Decimal.Parse(txtManualSL.Text)
                    newTrigSLprice = newSLprice + Decimal.Parse(txtStopLoss.Text)
                Else
                    newTrigSLprice = newPrice - Decimal.Parse(txtTrigger.Text)
                    newSLprice = newTrigSLprice - Decimal.Parse(txtStopLoss.Text)
                End If
            Else
                If Decimal.Parse(txtManualTP.Text) > 0 Then
                    newTPprice = Decimal.Parse(txtManualTP.Text)
                Else
                    newTPprice = newPrice - Decimal.Parse(txtTakeProfit.Text)
                End If

                If Decimal.Parse(txtManualSL.Text) > 0 Then
                    newSLprice = Decimal.Parse(txtManualSL.Text)
                    newTrigSLprice = newSLprice - Decimal.Parse(txtStopLoss.Text)
                Else
                    newTrigSLprice = newPrice + Decimal.Parse(txtTrigger.Text)
                    newSLprice = newTrigSLprice + Decimal.Parse(txtStopLoss.Text)
                End If
            End If

            StopLossTriggerOriginal = newTrigSLprice ' Save the original SL trigger price

            ' Send all three updates with rate limiting
            Await SendRateLimitedUpdate("main", CurrentOpenOrderId, newPrice, amount)
            rateLimiter.ConsumeCredits() ' Consume for second call
            Await SendRateLimitedUpdate("takeprofit", CurrentTPOrderId, newTPprice, amount)
            rateLimiter.ConsumeCredits() ' Consume for third call
            Await SendRateLimitedUpdate("stoploss", CurrentSLOrderId, newSLprice, amount, newTrigSLprice)

            UpdateFlag = True

        Catch ex As Exception
            AppendColoredText(txtLogs, "Error in rate-limited UpdateLimitOrderWithOTOCOAsync: " & ex.Message, Color.Red)
        End Try
    End Function

    Private Async Function SendRateLimitedUpdate(orderType As String, orderId As String, price As Decimal, amount As Decimal, Optional triggerPrice As Decimal? = Nothing) As Task
        Try
            Dim updatePayload As JObject

            If triggerPrice.HasValue Then
                ' Stop loss order with trigger price
                updatePayload = New JObject From {
                {"jsonrpc", "2.0"},
                {"id", 223346},
                {"method", "private/edit"},
                {"params", New JObject From {
                    {"order_id", orderId},
                    {"price", price},
                    {"trigger_price", triggerPrice.Value},
                    {"amount", amount}
                }}
            }
            Else
                ' Regular limit order
                updatePayload = New JObject From {
                {"jsonrpc", "2.0"},
                {"id", 223344},
                {"method", "private/edit"},
                {"params", New JObject From {
                    {"order_id", orderId},
                    {"price", price},
                    {"amount", amount}
                }}
            }
            End If

            Await SendWebSocketMessageAsync(updatePayload.ToString())

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in SendRateLimitedUpdate ({orderType}): {ex.Message}", Color.Red)
        End Try
    End Function


    Private lastSLId As String = ""
    Private pendingLocalMsg As String = ""

    Private Async Function UpdateStopLossForTriggeredStopLossOrder(newPrice As Decimal) As Task
        Try
            ' Your existing emergency market order logic first
            If (TradeMode = True) And (StopLossTriggerOriginal - newPrice >= Decimal.Parse(txtMarketStopLoss.Text)) Then
                Await CancelOrderAsync()
                newPricePublic = newPrice 'For storing reduce market order price for logging
                btnReduceMarket.PerformClick()
                AppendColoredText(txtLogs, "Emergency Sell Market Order Executed.", Color.Red)
                Return ' Exit early after emergency execution
            ElseIf (TradeMode = False) And (newPrice - StopLossTriggerOriginal >= Decimal.Parse(txtMarketStopLoss.Text)) Then
                Await CancelOrderAsync()
                newPricePublic = newPrice 'For storing reduce market order price for logging
                btnReduceMarket.PerformClick()
                AppendColoredText(txtLogs, "Emergency Buy Market Order Executed.", Color.Red)
                Return ' Exit early after emergency execution
            End If

            ' Enhanced rate limiter handling for critical operations
            If rateLimiter Is Nothing Then
                AppendColoredText(txtLogs, "CRITICAL: Rate limiter not initialized for SL update", Color.Red)
                rateLimiter = New DeribitRateLimiter(1000, 50) ' Emergency conservative limiter
            End If

            ' Force execution with timeout for critical stop loss updates
            Dim maxWaitTime As Integer = 3000 ' Maximum 3 seconds wait for SL updates
            Dim waitStartTime As DateTime = DateTime.UtcNow

            While Not rateLimiter.CanMakeRequest()
                If (DateTime.UtcNow - waitStartTime).TotalMilliseconds > maxWaitTime Then
                    AppendColoredText(txtLogs, "CRITICAL SL: Forcing execution despite rate limits", Color.Orange)
                    Exit While
                End If
                Await Task.Delay(100)
            End While

            ' Consume credits and proceed with update
            rateLimiter.ConsumeCredits()

            ' Validate amount input
            Dim amount As Decimal = 0D
            If Not Decimal.TryParse(txtAmount.Text, amount) OrElse amount <= 0 Then
                AppendColoredText(txtLogs, "Invalid amount for SL update", Color.Red)
                Return
            End If

            ' Validate order ID
            If String.IsNullOrEmpty(PositionSLOrderId) Then
                AppendColoredText(txtLogs, "PositionSLOrderId is null - cannot update SL", Color.Red)
                Return
            End If

            ' Validate WebSocket connection
            If webSocketClient Is Nothing OrElse webSocketClient.State <> WebSocketState.Open Then
                AppendColoredText(txtLogs, "WebSocket disconnected - cannot send critical SL update", Color.Red)
                Return
            End If

            ' Construct and send the update payload
            Dim updateOrderPayload As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", 223350},
            {"method", "private/edit"},
            {"params", New JObject From {
                {"order_id", PositionSLOrderId},
                {"price", newPrice},
                {"amount", amount}
            }}
        }

            Await SendWebSocketMessageAsync(updateOrderPayload.ToString())
            UpdateFlag = True

            ' Store pending message for confirmation
            pendingLocalMsg = $"CRITICAL SL repositioned to: ${newPrice:F2}"
            lastSLId = PositionSLOrderId

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in UpdateStopLossForTriggeredStopLossOrder: {ex.Message}", Color.Red)

            ' Handle rate limit errors specifically
            If ex.Message.Contains("too_many_requests") OrElse ex.Message.Contains("10028") Then
                AppendColoredText(txtLogs, "Rate limit hit during critical SL update - will retry", Color.Yellow)
                ' Reset last update time to allow immediate retry
                lastStopLossUpdate = DateTime.MinValue
            End If
        End Try
    End Function

    Private Async Function UpdateStopLossForTrailingOrder(newPrice As Decimal) As Task
        Try

            ' Ensure rate limiter exists
            If rateLimiter Is Nothing Then
                AppendColoredText(txtLogs, "Rate limiter not initialized - creating emergency limiter for trailing order", Color.Yellow)
                rateLimiter = New DeribitRateLimiter(1000, 50) ' Emergency conservative limiter
            End If

            ' Check rate limit before making multiple API calls (this function makes 2 calls)
            If Not rateLimiter.CanMakeRequest() Then
                Dim waitTime = rateLimiter.GetWaitTimeMs()
                AppendColoredText(txtLogs, $"Rate limit reached for trailing update, waiting {waitTime}ms", Color.Yellow)
                Await Task.Delay(waitTime)
            End If

            ' Check if we have enough credits for both API calls (main order + stop loss)
            If Not (rateLimiter.ConsumeCredits() AndAlso rateLimiter.CanMakeRequest()) Then
                AppendColoredText(txtLogs, "Insufficient credits for trailing order update - skipping", Color.Orange)
                Return
            End If

            Dim newTrigSLprice, newSLprice As Decimal
            Dim amount As Decimal = Decimal.Parse(txtAmount.Text)

            ' Calculate the new stop loss prices based on direction
            If TradeMode = True Then
                ' Buy direction
                If Decimal.Parse(txtManualSL.Text) > 0 Then
                    newSLprice = Decimal.Parse(txtManualSL.Text)
                    newTrigSLprice = newSLprice + Decimal.Parse(txtStopLoss.Text)
                Else
                    newTrigSLprice = newPrice - Decimal.Parse(txtTrigger.Text)
                    newSLprice = newTrigSLprice - Decimal.Parse(txtStopLoss.Text)
                End If
            Else
                ' Sell direction
                If Decimal.Parse(txtManualSL.Text) > 0 Then
                    newSLprice = Decimal.Parse(txtManualSL.Text)
                    newTrigSLprice = newSLprice - Decimal.Parse(txtStopLoss.Text)
                Else
                    newTrigSLprice = newPrice + Decimal.Parse(txtTrigger.Text)
                    newSLprice = newTrigSLprice + Decimal.Parse(txtStopLoss.Text)
                End If
            End If

            StopLossTriggerOriginal = newTrigSLprice ' Save the original SL trigger price

            ' Update main trailing order
            Dim updateOrderPayload As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", 223347},
            {"method", "private/edit"},
            {"params", New JObject From {
                {"order_id", CurrentOpenOrderId},
                {"price", newPrice},
                {"amount", amount}
            }}
        }

            Await SendWebSocketMessageAsync(updateOrderPayload.ToString())

            ' Consume credits for second API call
            rateLimiter.ConsumeCredits()

            ' Update stop loss order
            Dim updateStopLossPayload As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", 223348},
            {"method", "private/edit"},
            {"params", New JObject From {
                {"order_id", CurrentSLOrderId},
                {"price", newSLprice},
                {"trigger_price", newTrigSLprice},
                {"amount", amount}
            }}
        }

            Await SendWebSocketMessageAsync(updateStopLossPayload.ToString())

            UpdateFlag = True
            AppendColoredText(txtLogs, $"Rate-limited trailing order updated to: ${newPrice}", Color.Cyan)

        Catch ex As Exception
            AppendColoredText(txtLogs, "Error in UpdateStopLossForTrailingOrder: " & ex.Message, Color.Red)
        End Try
    End Function

    Private Async Function StopLossForTrailingOrderAsync(TypeOfOrder As String) As Task
        Try

            Dim stoplossTriggerPrice As Decimal
            Dim triggeroffset As Decimal
            Dim stoplossPrice As Decimal
            Dim BestPrice As Decimal
            Dim ordermethod As String = String.Empty ' Default value to avoid warnings
            Dim direction As String = String.Empty ' Default value for direction
            Dim ordertype As String = String.Empty

            ' Ensure WebSocket is connected
            If webSocketClient Is Nothing OrElse webSocketClient.State <> WebSocketState.Open Then
                'txtLogs.AppendText("WebSocket is not connected." + Environment.NewLine)
                AppendColoredText(txtLogs, "WebSocket is not connected.", Color.Red)
                Return
            End If

            ' Validate the input amount
            Dim amountText As String = txtAmount.Text
            Dim amount As Decimal

            If Not Decimal.TryParse(amountText, amount) OrElse amount <= 0 Then
                'txtLogs.AppendText("Please enter a valid positive amount." + Environment.NewLine)
                AppendColoredText(txtLogs, "Please enter a valid positive amount.", Color.Yellow)
                Return
            End If

            Select Case TypeOfOrder
                Case "BuyTrail"

                    ' Ensure BestBidPrice is valid
                    If BestBidPrice <= 0 Then
                        'txtLogs.AppendText("Best bid price is not valid." + Environment.NewLine)
                        AppendColoredText(txtLogs, "Best bid price is not valid.", Color.Yellow)
                        Return
                    End If

                    BestPrice = BestBidPrice

                    'To set price at which trailing stop loss order is triggered for placement
                    'If manual take profit textbox is not empty, use that
                    If Decimal.Parse(txtManualTP.Text) > 0 Then
                        TPTrailprice = Decimal.Parse(txtManualTP.Text)
                        If TPTrailprice < (BestPrice + Decimal.Parse(txtComms.Text)) Then
                            AppendColoredText(txtLogs, "Manual TP is less than comms paid.", Color.Yellow)
                        End If
                    Else
                        TPTrailprice = BestPrice + (Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text))
                    End If

                    'If manual stop loss textbox is not empty, use that
                    If Decimal.Parse(txtManualSL.Text) > 0 Then
                        stoplossPrice = Decimal.Parse(txtManualSL.Text)
                        stoplossTriggerPrice = Decimal.Parse(txtManualSL.Text) + Decimal.Parse(txtStopLoss.Text)
                    Else
                        stoplossTriggerPrice = BestPrice - Decimal.Parse(txtTrigger.Text)
                        stoplossPrice = BestPrice - (Decimal.Parse(txtStopLoss.Text) + Decimal.Parse(txtTrigger.Text))
                    End If

                    'For initiating ATR Slippage function
                    direction = "LONG"
                    If IsATRSlippageExcessive(BestPrice, direction) Then
                        Return
                    End If

                    triggeroffset = Decimal.Parse(txtTriggerOffset.Text)

                    ordermethod = "private/buy"
                    direction = "sell"

                Case "SellTrail"

                    ' Ensure BestAskPrice is valid
                    If BestAskPrice <= 0 Then
                        'txtLogs.AppendText("Best ask price Is Not valid." + Environment.NewLine)
                        AppendColoredText(txtLogs, "Best ask price Is Not valid.", Color.Yellow)
                        Return
                    End If

                    BestPrice = BestAskPrice

                    'If manual take profit textbox is not empty, use that
                    If Decimal.Parse(txtManualTP.Text) > 0 Then
                        TPTrailprice = Decimal.Parse(txtManualTP.Text)
                        If TPTrailprice > (BestPrice - Decimal.Parse(txtComms.Text)) Then
                            AppendColoredText(txtLogs, "Manual TP is less than comms paid.", Color.Yellow)
                        End If
                    Else
                        TPTrailprice = BestPrice - (Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text))
                    End If

                    'If manual stop loss textbox is not empty, use that
                    If Decimal.Parse(txtManualSL.Text) > 0 Then
                        stoplossPrice = Decimal.Parse(txtManualSL.Text)
                        stoplossTriggerPrice = Decimal.Parse(txtManualSL.Text) - Decimal.Parse(txtStopLoss.Text)
                    Else
                        stoplossTriggerPrice = BestPrice + Decimal.Parse(txtTrigger.Text)
                        stoplossPrice = BestPrice + (Decimal.Parse(txtStopLoss.Text) + Decimal.Parse(txtTrigger.Text))
                    End If

                    'For initiating ATR Slippage function
                    direction = "SHORT"
                    If IsATRSlippageExcessive(BestPrice, direction) Then
                        Return
                    End If

                    triggeroffset = Decimal.Parse(txtTriggerOffset.Text)

                    ordermethod = "private/sell"
                    direction = "buy"

                Case Else

                    ' Handle unexpected or unsupported order types
                    AppendColoredText(txtLogs, "Unsupported order type specified.", Color.IndianRed)
                    Return

            End Select

            StopLossTriggerOriginal = stoplossTriggerPrice ' Save the original SL trigger price

            ' Construct the JSON payload for the reduce-only order
            Dim params As New JObject(
             New JProperty("instrument_name", "BTC-PERPETUAL"),
                New JProperty("amount", amount),
                New JProperty("type", "limit"),
                New JProperty("label", "EntryTrailingOrder"),
                New JProperty("time_in_force", "good_til_cancelled"),
                New JProperty("linked_order_type", "one_triggers_other"),
                New JProperty("trigger_fill_condition", "first_hit"),
                New JProperty("reject_post_only", False),
                New JProperty("otoco_config", New JArray(
                    New JObject(
                    New JProperty("amount", amount),
                    New JProperty("direction", direction),
                    New JProperty("type", "stop_limit"),
                    New JProperty("trigger_price", stoplossTriggerPrice), ' Base trigger price
                    New JProperty("trigger_offset", triggeroffset), ' Offset for dynamic adjustment
                    New JProperty("price", stoplossPrice), ' Stop loss limit price
                    New JProperty("label", "StopLossOrder"),
                    New JProperty("reduce_only", True),
                    New JProperty("time_in_force", "good_til_cancelled"),
                    New JProperty("post_only", True),
                    New JProperty("trigger", "last_price")
                    )
                ))
            )

            params.Add("price", BestPrice)
            params.Add("post_only", True) ' Post-only is valid only for limit orders

            ' Prepare the payload for the linked order
            Dim OrderPayload As New JObject(
            New JProperty("jsonrpc", "2.0"),
            New JProperty("id", 2),
            New JProperty("method", ordermethod),
            New JProperty("params", params)
        )

            ' Send the order and capture the server's response
            Await SendWebSocketMessageAsync(OrderPayload.ToString())

            If Decimal.Parse(txtManualTP.Text) > 0 Then
                txtPlacedTakeProfitPrice.Text = txtManualTP.Text
            Else
                If TradeMode = True Then
                    txtPlacedTakeProfitPrice.Text = BestPrice + ((Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text)))
                Else
                    txtPlacedTakeProfitPrice.Text = BestPrice - ((Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text)))
                End If

            End If

            txtPlacedTrigStopPrice.Text = stoplossTriggerPrice.ToString("F2")
            txtPlacedStopLossPrice.Text = stoplossPrice.ToString("F2")

            txtPlacedPrice.Text = BestPrice.ToString("F2")

            isTrailingStopLossPlaced = True

            If TypeOfOrder = "BuyTrail" Then
                ' Optional: Handle post-order logic (e.g., display confirmation)
                AppendColoredText(txtLogs, $"Buy Trailing order placed For {amount} at {BestPrice}.", Color.MediumSeaGreen)
            ElseIf TypeOfOrder = "SellTrail" Then
                ' Optional: Handle post-order logic (e.g., display confirmation)
                AppendColoredText(txtLogs, $"Sell Trailing order placed For {amount} at {BestPrice}.", Color.Red)
            End If

        Catch ex As Exception
            ' Handle any errors
            AppendColoredText(txtLogs, "Error in StopLossForTrailingOrderAsync: " & ex.Message, Color.Red)
        End Try
    End Function

    'This function is called when market price has reached the Take Profit price set in Manual TP textbox or auto-calc by txtplacedprice + txtcomms + txttpoffset 
    'after user clicks the trailing stop loss button. Call comes from HandleQuoteUpdates function.
    Private Async Function TrailingStopLossOrderAsync() As Task
        Try
            ' Ensure WebSocket is connected
            If webSocketClient Is Nothing OrElse webSocketClient.State <> WebSocketState.Open Then
                'txtLogs.AppendText("WebSocket is not connected." + Environment.NewLine)
                AppendColoredText(txtLogs, "WebSocket is not connected.", Color.Red)
                Return
            End If

            ' Validate the input amount
            Dim amountText As String = txtAmount.Text
            Dim amount As Decimal

            If Not Decimal.TryParse(amountText, amount) OrElse amount <= 0 Then
                'txtLogs.AppendText("Please enter a valid positive amount." + Environment.NewLine)
                AppendColoredText(txtLogs, "Please enter a valid positive amount.", Color.Yellow)
                Return
            End If

            Dim method As String
            Dim startoffset As Decimal = Decimal.Parse(txtTPOffset.Text)
            Dim triggerpricing As Decimal

            If TradeMode = True Then
                method = "private/sell"
                triggerpricing = TPTrailprice - startoffset
            Else
                method = "private/buy"
                triggerpricing = TPTrailprice + startoffset
            End If

            'To cancel the current stop loss order
            Dim cancelPayload As New JObject(
        New JProperty("jsonrpc", "2.0"),
        New JProperty("id", 30),
        New JProperty("method", "private/cancel_all_by_instrument"),
        New JProperty("params", New JObject(
        New JProperty("instrument_name", "BTC-PERPETUAL"),
        New JProperty("type", "all")
        ))
    )
            Await SendWebSocketMessageAsync(cancelPayload.ToString())
            AppendColoredText(txtLogs, "Cancelled current stop loss order.", Color.Green)


            ' Construct the JSON payload for the trailing stop loss order
            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 30),
                New JProperty("method", method),
                New JProperty("params", New JObject(
                    New JProperty("instrument_name", "BTC-PERPETUAL"),
                            New JProperty("amount", amount),
                            New JProperty("type", "trailing_stop"),
                            New JProperty("label", "TrailingStopLoss"),
                            New JProperty("trail_offset", startoffset), 'Offset for the trailing stop to maintain from market price
                            New JProperty("trigger_offset", startoffset), 'Starting offset for the trailing stop
                            New JProperty("reduce_only", True),
                            New JProperty("trigger", "last_price"),
                            New JProperty("time_in_force", "good_til_cancelled")
                ))
            )

            ' Send the payload via WebSocket
            Await SendWebSocketMessageAsync(payload.ToString())

            Dim orderDescription As String = $"Trailing Stop Loss order set at: {startoffset} offset"
            AppendColoredText(txtLogs, $"Reduce-only {orderDescription}.", Color.Green)

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in TrailingStopLossOrderAsync: {ex.Message}", Color.Red)
        End Try
    End Function

    Private Async Function ForceStopLossUpdate(newPrice As Decimal, Optional reason As String = "Emergency Update") As Task
        ' Bypass all rate limiting for emergency situations
        lastStopLossUpdate = DateTime.MinValue
        'AppendColoredText(txtLogs, $"EMERGENCY SL UPDATE: {reason}", Color.Red)
        Await UpdateStopLossForTriggeredStopLossOrder(newPrice)
    End Function


    'Non-order execution functions below
    '------------------------------------------------
    ' Define a function to add colored text

    Private Async Function GetAccountSummaryLimits() As Task(Of RateLimitInfo)
        Try
            ' Create a task completion source to wait for the response
            accountSummaryTaskCompletionSource = New TaskCompletionSource(Of RateLimitInfo)()

            ' Create the account summary request
            Dim accountSummaryPayload = New JObject(
            New JProperty("jsonrpc", "2.0"),
            New JProperty("id", 999), ' Unique ID to identify this request
            New JProperty("method", "private/get_account_summary"),
            New JProperty("params", New JObject(
                New JProperty("currency", "BTC"),
                New JProperty("extended", True)
            ))
        )

            ' Send the request via WebSocket
            Await SendWebSocketMessageAsync(accountSummaryPayload.ToString())

            ' Wait for the response (with timeout)
            Dim timeoutTask = Task.Delay(5000) ' 5 second timeout
            Dim completedTask = Await Task.WhenAny(accountSummaryTaskCompletionSource.Task, timeoutTask)

            ' Use 'Is' operator instead of '=' for task comparison
            If completedTask Is timeoutTask Then
                ' Timeout occurred
                AppendColoredText(txtLogs, "Account summary request timed out - using conservative defaults", Color.Yellow)
                Return New RateLimitInfo With {
                .MaxCredits = 2000,
                .RefillRate = 20,
                .BurstLimit = 20,
                .CurrentEstimatedCredits = 2000
            }
            Else
                ' Response received
                Return Await accountSummaryTaskCompletionSource.Task
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error getting account limits: {ex.Message}", Color.Yellow)
            ' Return very conservative defaults on error
            Return New RateLimitInfo With {
            .MaxCredits = 500,
            .RefillRate = 10,
            .BurstLimit = 5,
            .CurrentEstimatedCredits = 500
        }
        End Try
    End Function

    ' --- place in frmMainPageV2 (replace existing helper) -------------------
    Private Sub AppendColoredText(rtb As RichTextBox, text As String, color As Color)
        Const RL_MSG As String = "Rate limiter not initialized - skipping order update"
        Static skipNext As Boolean = False          ' <-- single persistent flag

        'If several other types of repeating messages, use below code to suppress them and replace skipnext as Boolean
        '        Static lastMsg As String = ""
        '       If text.Equals(lastMsg, StringComparison.Ordinal) Then Exit Sub
        '      lastMsg = text

        ' 1. Decide whether this call should be written
        If text.Equals(RL_MSG, StringComparison.Ordinal) Then
            If skipNext Then Exit Sub               ' already shown → suppress
            skipNext = True                         ' first time → show & arm flag
        Else
            skipNext = False                        ' any other message resets flag
        End If

        ' 2. Normal logging
        Me.Invoke(Sub()
                      rtb.SelectionStart = rtb.TextLength
                      rtb.SelectionLength = 0
                      rtb.SelectionColor = color
                      rtb.AppendText(text & Environment.NewLine)
                      rtb.SelectionColor = rtb.ForeColor
                  End Sub)
    End Sub

    Private Sub ButtonDisabler()
        btnLimit.Enabled = False
        btnNoSpread.Enabled = False
        btnTrail.Enabled = False
        btnMarket.Enabled = False

        btnReduceMarket.Enabled = True
        btnReduceLimit.Enabled = True
        btnCancelAllOpen.Enabled = True

    End Sub

    Private Sub ButtonEnabler()
        btnLimit.Enabled = True
        btnNoSpread.Enabled = True
        btnTrail.Enabled = True
        btnMarket.Enabled = True

        btnReduceMarket.Enabled = False
        btnReduceLimit.Enabled = False
        btnCancelAllOpen.Enabled = False

    End Sub

    Public Function RecordCompletedTrade(entryPrice As Decimal, exitPrice As Decimal,
                                   orderSizeUSD As Decimal, profitLossUSD As Decimal,
                                   isProfit As Boolean, tradeMode As Boolean,
                                   orderType As String) As Integer
        Try
            If tradeDatabase Is Nothing Then
                AppendColoredText(txtLogs, "Trade database not initialized", Color.Red)
                Return 0
            End If

            ' Create completed trade record
            Dim completedTrade As New TradeRecord(
            orderType,
            If(tradeMode, "Long", "Short"),
            entryPrice,
            exitPrice,
            orderSizeUSD,
            profitLossUSD,
            isProfit
        )

            ' Record the completed trade (synchronous)
            Dim tradeId As Integer = tradeDatabase.RecordCompletedTrade(completedTrade)

            Return tradeId

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error recording completed trade: {ex.Message}", Color.Red)
            Return 0
        End Try
    End Function



    Private Function CalculateDeribitInverseLiquidationPrice(
        positionSizeUSD As Decimal, leverage As Decimal,
        entryPrice As Decimal, isShort As Boolean) _
        As Dictionary(Of String, Decimal)

        ' ---------------- basics ----------------
        Dim equityBTC As Decimal = GetEquityBTC()
        Dim posBTC As Decimal = positionSizeUSD / entryPrice        ' signed
        Dim absPosBTC As Decimal = Math.Abs(posBTC)

        ' -------- Standard-Margin tier-0 IM/MM (BTC PERP) ----------
        Const BASE_IM As Decimal = 0.02D   ' 2 %
        Const BASE_MM As Decimal = 0.01D   ' 1 %

        Dim initialMarginBTC = absPosBTC * BASE_IM
        Dim maintenanceMarginBTC = absPosBTC * BASE_MM

        ' ------------- liquidation math -----------------------------
        ' Δ  = (Equity – MM) / |posBTC|
        Dim delta As Decimal = 0D
        If absPosBTC > 0D Then _
        delta = (equityBTC - maintenanceMarginBTC) / absPosBTC

        Dim liqPrice As Decimal
        If isShort Then                     ' short → 1 – Δ
            liqPrice = If(delta >= 1D, 0D, entryPrice / (1D - delta))
        Else                                ' long  → 1 + Δ
            liqPrice = entryPrice / (1D + delta)
        End If

        Return New Dictionary(Of String, Decimal) From {
        {"InitialMarginBTC", initialMarginBTC},
        {"MaintenanceMarginBTC", maintenanceMarginBTC},
        {"EstimatedLiquidationPrice", liqPrice},
        {"EffectiveLeverage", leverage}
    }
    End Function




    'Might need to delete if not used later for liquidation estimation with positions
    Private Sub HandleMarginEstimationResponse(response As String)
        Try
            Dim json = JObject.Parse(response)

            ' Check if this is a margin estimation response (ID 890) OR live position data (ID 777)
            Dim messageId = json.SelectToken("id")?.ToObject(Of Integer)()

            If messageId = 890 OrElse messageId = 777 Then

                Dim errorField = json.SelectToken("error")
                If errorField IsNot Nothing Then
                    Dim errorType = If(messageId = 777, "Live position", "Margin estimation")
                    AppendColoredText(txtLogs, $"{errorType} error: {errorField.ToString()}", Color.Yellow)
                    Return
                End If

                Dim result = json.SelectToken("result")
                If result IsNot Nothing Then

                    If messageId = 777 Then
                        ' Handle live position data (single position object)
                        ProcessPositionData(result)
                    Else
                        ' Handle margin estimation data (your existing logic)
                        ProcessEstimationData(result)
                    End If
                End If
            End If

        Catch ex As Exception
            ' Ignore parsing errors for non-relevant responses
        End Try
    End Sub

    Private Sub ProcessPositionData(positionData As JToken)
        Try
            ' Extract live position information from Deribit
            Dim initialMargin = positionData.SelectToken("initial_margin")?.ToObject(Of Decimal?)()
            Dim maintenanceMargin = positionData.SelectToken("maintenance_margin")?.ToObject(Of Decimal?)()
            Dim estimatedLiquidation = positionData.SelectToken("estimated_liquidation_price")?.ToObject(Of Decimal?)()
            Dim positionSize = positionData.SelectToken("size")?.ToObject(Of Decimal?)()
            Dim markPrice = positionData.SelectToken("mark_price")?.ToObject(Of Decimal?)()
            Dim averagePrice = positionData.SelectToken("average_price")?.ToObject(Of Decimal?)()

            ' CORRECTED: For BTC-PERPETUAL, positionSize is in USD, not BTC
            Dim effectiveLeverage As Decimal = 0

            If positionSize.HasValue AndAlso markPrice.HasValue Then
                ' Position value in USD is simply the absolute position size (already in USD)
                Dim positionValueUSD As Decimal = Math.Abs(positionSize.Value)

                ' Account balance in USD
                Dim accountBalanceBTC As Decimal = GetEquityBTC()
                Dim accountBalanceUSD As Decimal = accountBalanceBTC * markPrice.Value

                ' Calculate leverage as position value / account balance
                If accountBalanceUSD > 0 Then
                    effectiveLeverage = positionValueUSD / accountBalanceUSD
                End If

                ' Debug logging with corrected values
                'AppendColoredText(txtLogs, $"DEBUG CORRECTED: positionValueUSD={Math.Abs(positionSize.Value):F2}, accountBalanceUSD={accountBalanceUSD:F2}", Color.Gray)



            End If

            Me.Invoke(Sub()
                          ' Update UI with LIVE Deribit data
                          If estimatedLiquidation.HasValue AndAlso estimatedLiquidation.Value > 0 Then
                              lblEstimatedLiquidation.Text = $"L.Liq: ${estimatedLiquidation.Value:F2}"
                              lblEstimatedLiquidation.ForeColor = Color.Red
                          Else
                              lblEstimatedLiquidation.Text = "L.Liq: N/A"
                              lblEstimatedLiquidation.ForeColor = Color.Gray
                          End If

                          If initialMargin.HasValue Then
                              lblInitialMargin.Text = $"L.IM: {initialMargin.Value:F8} BTC"
                          End If

                          If maintenanceMargin.HasValue Then
                              lblMaintenanceMargin.Text = $"L.MM: {maintenanceMargin.Value:F8} BTC"
                          End If

                          ' Display proper leverage based on account balance
                          lblEstimatedLeverage.Text = $"L.Lev: {effectiveLeverage:F2}x"

                          ' Color code leverage risk
                          If effectiveLeverage > 10 Then
                              lblEstimatedLeverage.ForeColor = Color.Red
                          ElseIf effectiveLeverage > 5 Then
                              lblEstimatedLeverage.ForeColor = Color.Orange
                          Else
                              lblEstimatedLeverage.ForeColor = Color.LimeGreen
                          End If
                      End Sub)

            ' Improved logging with corrected calculation
            Dim liquidationText As String = If(estimatedLiquidation.HasValue AndAlso estimatedLiquidation.Value > 0,
                                          "$" & estimatedLiquidation.Value.ToString("F2"), "N/A")

            AppendColoredText(txtLogs, $"LIVE position data - Liq: {liquidationText}, Leverage: {effectiveLeverage:F2}x", Color.Red)

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error processing live position data: {ex.Message}", Color.Red)
        End Try
    End Sub


    'For text file trade logging
    Private Sub LogTradeDecision(ordertype As String, PLAmt As Decimal, ExitP As Decimal)

        Dim placedPrice As Decimal = Convert.ToDecimal(txtPlacedPrice.Text)
        Dim TakeProfit As Decimal = Convert.ToDecimal(txtPlacedTakeProfitPrice.Text)
        Dim triggerPrice As Decimal = Convert.ToDecimal(txtPlacedTrigStopPrice.Text)
        Dim logentry As String = String.Empty

        'Dim TPPrice, SLPrice As Decimal

        'If TradeMode Then
        ' TPPrice = placedPrice + TakeProfit
        ' SLPrice = placedPrice - triggerPrice
        'Else
        ' TPPrice = placedPrice - TakeProfit
        'SLPrice = placedPrice + triggerPrice
        'End If

        If ordertype.Contains("Exit Position") Then

            If ordertype.Contains("Exit Position - Profit") Then
                logentry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " &
                       $"Type: {ordertype} | " &
                        $"Exit Price: {ExitP} | " &
                       $"Profit: {PLAmt} | "
            ElseIf ordertype.Contains("Exit Position - Loss") Then
                logentry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " &
                    $"Type: {ordertype} | " &
                     $"Exit Price: {ExitP} | " &
                    $"Loss: {PLAmt} | "
            ElseIf ordertype.Contains("Exit Position - Market Order Loss") Then
                logentry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " &
                    $"Type: {ordertype} | " &
                     $"Exit Price: {newPricePublic} | Loss: Check Order History "
            End If

        Else
            logentry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " &
                       $"Type: {ordertype} | " &
                        $"Placed Price: {placedPrice} | " &
                       $"Take Profit: {TakeProfit} | " &
                       $"Stop Loss Trigger: {triggerPrice} "
        End If

        ' Write to file for later analysis
        Try
            System.IO.File.AppendAllText("AutoTradeLog.txt", logentry & Environment.NewLine)
        Catch
            AppendColoredText(txtLogs, "Text file IO error", Color.Red) ' Handle file write errors
        End Try

    End Sub


    Private Sub ProcessEstimationData(estimationData As JToken)
        ' Your existing estimation logic remains the same...
        ' (Keep your current estimation processing code here)
    End Sub


    'All button logic below
    '----------------------------------------------------------------------------------

    'Private Async Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
    ' Try
    ' If btnConnect.Text = "Connect!" Then
    '' Initialize WebSocket connection first
    '             Await WebSocketCalls()

    ' Automatically initialize rate limits after successful connection
    ' If btnConnect.Text = "ONLINE" Then
    '                Await InitializeRateLimits()
    ''AppendColoredText(txtLogs, "Rate limiting initialized automatically after connection.", Color.LimeGreen)
    'End If

    '          AppendColoredText(txtLogs, "Connected." + Environment.NewLine, Color.DodgerBlue)
    'ElseIf btnConnect.Text = "ONLINE" Then
    '           ' Manual rate limit refresh when already connected
    '          Await InitializeRateLimits()
    '         AppendColoredText(txtLogs, "Rate limits manually refreshed.", Color.LimeGreen)
    'End If

    'Catch ex As Exception
    '       AppendColoredText(txtLogs, "Connect failed." + Environment.NewLine, Color.Red)
    'End Try
    'End Sub

    Private Async Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
        Try
            If btnConnect.Text = "Connect!" Then
                btnConnect.Enabled = False
                btnConnect.Text = "Connecting..."
                btnConnect.BackColor = Color.Orange

                Try
                    Await ConnectToWebSocketDirectly()
                    AppendColoredText(txtLogs, "Connected successfully", Color.LimeGreen)
                Catch ex As Exception
                    AppendColoredText(txtLogs, $"Connection failed: {ex.Message}", Color.Red)
                    btnConnect.Text = "Connect!"
                    btnConnect.BackColor = Color.Red
                Finally
                    btnConnect.Enabled = True
                End Try

            ElseIf btnConnect.Text = "ONLINE" Then
                ' Manual rate limit refresh when already connected
                Dim refreshratelimits = Task.Run(Async Function()
                                                     Try
                                                         Await InitializeRateLimitsAfterAuth()
                                                         AppendColoredText(txtLogs, "Rate limits manually refreshed", Color.LimeGreen)
                                                     Catch ex As Exception
                                                         AppendColoredText(txtLogs, $"Rate limit refresh failed: {ex.Message}", Color.Yellow)
                                                     End Try
                                                 End Function)
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Button click error: {ex.Message}", Color.Red)
        End Try
    End Sub



    Public Async Function InitializeRateLimits() As Task
        Try
            AppendColoredText(txtLogs, "Initializing rate limits from account summary...", Color.DodgerBlue)

            ' Get actual account limits
            accountLimits = Await GetAccountSummaryLimits()

            ' Initialize rate limiter with actual limits
            rateLimiter = New DeribitRateLimiter(accountLimits.MaxCredits, 50) 'Conservative = 200 | Reasonable = 50

            AppendColoredText(txtLogs, $"Rate limits: {accountLimits.MaxCredits} max credits, sustainable rate: {accountLimits.MaxCredits / 50} req/sec", Color.LimeGreen) 'Conservative = 200 | Reasonable = 50

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Rate limit initialization error: {ex.Message}", Color.Yellow)
            ' Initialize with very conservative defaults
            'rateLimiter = New DeribitRateLimiter(1000, 200)

            ' Initialize with more reasonable defaults
            rateLimiter = New DeribitRateLimiter(2000, 50) ' Reduced cost per request
        End Try
    End Function



    Private Sub btnChangeForm_Click(sender As Object, e As EventArgs) Handles btnChangeForm.Click
        frmMainPage.Show()
        Me.Hide()

    End Sub

    Private Async Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Try
            ' Signal shutdown intent
            isClosing = True

            ' Cancel any ongoing operations
            cancellationTokenSource?.Cancel()

            ' Close WebSocket gracefully
            If webSocketClient?.State = WebSocketState.Open Then
                Try
                    Await webSocketClient.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "User closing",
                    CancellationToken.None)
                Catch
                    ' Ignore close errors
                End Try
            End If

            ' Clean up resources
            webSocketClient?.Dispose()
            cancellationTokenSource?.Dispose()

            ' Close the application cleanly
            Me.Close()

        Catch ex As Exception
            ' Log but don't crash
            AppendColoredText(txtLogs, $"Shutdown error: {ex.Message}", Color.Red)
            Me.Close()
        End Try
    End Sub



    Private Sub btnClearLog_Click(sender As Object, e As EventArgs) Handles btnClearLog.Click
        txtLogs.Clear()

    End Sub

    Private Sub btnSell_Click(sender As Object, e As EventArgs) Handles btnSell.Click

        'Sets mode to Sell mode
        TradeMode = False

        'Set btnSell color to on
        btnSell.FlatStyle = FlatStyle.Flat
        btnSell.FlatAppearance.BorderSize = 2 ' Optional: Highlight border
        btnSell.BackColor = Color.Red ' Change to "depressed" color
        btnSell.ForeColor = Color.Black

        'Reset btnBuy color
        btnBuy.FlatStyle = FlatStyle.Popup
        btnBuy.FlatAppearance.BorderSize = 0
        btnBuy.BackColor = Color.DarkGreen ' Reset to default color
        btnBuy.ForeColor = Color.White


        btnLimit.BackColor = Color.DarkRed
        btnNoSpread.BackColor = Color.Firebrick
        btnTrail.BackColor = Color.IndianRed
        btnMarket.BackColor = Color.LightCoral

        btnLimit.Text = "Limit SELL"
        btnNoSpread.Text = "No Sprd. SELL"
        btnTrail.Text = "Trail SELL"
        btnMarket.Text = "Mkt. SELL"
        btnReduceLimit.Text = "Reduce BUY"
        btnReduceMarket.Text = "Mkt. Rdc. Buy"

        TradeButtons.Text = "Short"
        PlacedOrders.Text = "Placed Short"

        txtPlacedStopLossPrice.Location = New Point(173, 95)
        txtPlacedTrigStopPrice.Location = New Point(173, 146)
        txtPlacedPrice.Location = New Point(173, 197)
        txtPlacedTakeProfitPrice.Location = New Point(173, 248)

        lblPlacedStopLossPrice.Location = New Point(22, 97)
        lblPlacedTrigStopPrice.Location = New Point(24, 150)
        lblPlacedPrice.Location = New Point(21, 201)
        lblPlacedTakeProfitPrice.Location = New Point(12, 254)

        btnEditTPPrice.Location = New Point(379, 247)
        btnEditSLPrice.Location = New Point(379, 146)
        btnTPOffset.Location = New Point(379, 93)

    End Sub

    Private Sub btnBuy_Click(sender As Object, e As EventArgs) Handles btnBuy.Click

        'Sets mode to Buy mode
        TradeMode = True

        'Set btnBuy color to on
        btnBuy.FlatStyle = FlatStyle.Flat
        btnBuy.FlatAppearance.BorderSize = 2 ' Optional: Highlight border
        btnBuy.BackColor = Color.Lime ' Change to "depressed" color
        btnBuy.ForeColor = Color.Black

        'Reset btnSell color
        btnSell.FlatStyle = FlatStyle.Popup
        btnSell.FlatAppearance.BorderSize = 0
        btnSell.BackColor = Color.DarkRed ' Reset to default color
        btnSell.ForeColor = Color.White


        btnLimit.BackColor = Color.DarkGreen
        btnNoSpread.BackColor = Color.Green
        btnTrail.BackColor = Color.ForestGreen
        btnMarket.BackColor = Color.SeaGreen

        btnLimit.Text = "Limit BUY"
        btnNoSpread.Text = "No Sprd. BUY"
        btnTrail.Text = "Trail BUY"
        btnMarket.Text = "Mkt. BUY"
        btnReduceLimit.Text = "Reduce SELL"
        btnReduceMarket.Text = "Mkt. Rdc. Sell"

        TradeButtons.Text = "Long"
        PlacedOrders.Text = "Placed Long"

        txtPlacedTakeProfitPrice.Location = New Point(173, 95)
        txtPlacedPrice.Location = New Point(173, 146)
        txtPlacedTrigStopPrice.Location = New Point(173, 197)
        txtPlacedStopLossPrice.Location = New Point(173, 248)

        lblPlacedTakeProfitPrice.Location = New Point(12, 97)
        lblPlacedPrice.Location = New Point(21, 150)
        lblPlacedTrigStopPrice.Location = New Point(24, 201)
        lblPlacedStopLossPrice.Location = New Point(22, 254)

        btnEditTPPrice.Location = New Point(379, 93)
        btnEditSLPrice.Location = New Point(379, 194)
        btnTPOffset.Location = New Point(379, 247)

    End Sub

    Private Async Sub btnLimit_Click(sender As Object, e As EventArgs) Handles btnLimit.Click
        Try
            ' Get margin estimation before placing order
            btnEstimateMargins_Click(Nothing, Nothing) ' Call the estimation function

            ' Wait a moment for UI update
            Await Task.Delay(500)

            ' Then execute the order
            If TradeMode = True Then
                Await ExecuteOrderAsync("BuyLimit")
            Else
                Await ExecuteOrderAsync("SellLimit")
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in btnLimit_Click: {ex.Message}", Color.Red)
        End Try
    End Sub



    Private Async Sub btnNoSpread_Click(sender As Object, e As EventArgs) Handles btnNoSpread.Click
        Try
            ' Get margin estimation before placing order
            btnEstimateMargins_Click(Nothing, Nothing) ' Call the estimation function

            ' Wait a moment for UI update
            Await Task.Delay(500)

            ' Then execute the order
            If TradeMode = True Then
                Await ExecuteOrderAsync("BuyNoSpread")
            Else
                Await ExecuteOrderAsync("SellNoSpread")
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in btnNoSpread_Click: {ex.Message}", Color.Red)
        End Try

    End Sub

    Private Async Sub btnLCancelAllOpen_Click(sender As Object, e As EventArgs) Handles btnCancelAllOpen.Click
        Await CancelOrderAsync()
    End Sub

    Private Sub selectallclick(sender As Object, e As EventArgs) Handles txtAmount.Click, txtTakeProfit.Click, txtTrigger.Click, txtStopLoss.Click, txtTriggerOffset.Click, txtTPOffset.Click, txtComms.Click, txtManualTP.Click, txtManualSL.Click, txtPlacedTakeProfitPrice.Click, txtPlacedTrigStopPrice.Click, txtPlacedStopLossPrice.Click
        'Cast the sender to a TextBox
        Dim txtBox = CType(sender, TextBox)

        'Select all text in the TextBox
        txtBox.SelectionStart = 0
        txtBox.SelectionLength = txtBox.Text.Length
    End Sub

    Private Async Sub btnMarket_Click(sender As Object, e As EventArgs) Handles btnMarket.Click
        Try
            ' Get margin estimation before placing order
            btnEstimateMargins_Click(Nothing, Nothing) ' Call the estimation function

            ' Wait a moment for UI update
            Await Task.Delay(500)
            ' Then execute the order
            If TradeMode = True Then
                Await ExecuteOrderAsync("BuyMarket")
            Else
                Await ExecuteOrderAsync("SellMarket")
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in btnMarket_Click: {ex.Message}", Color.Red)
        End Try

    End Sub

    Private Async Sub btnReduceLimit_Click(sender As Object, e As EventArgs) Handles btnReduceLimit.Click
        Try
            Dim direction As String

            ' Determine the direction based on the current position
            If TradeMode = False Then
                direction = "buy" ' To reduce a short, we buy
            ElseIf TradeMode = True Then
                direction = "sell" ' To reduce a long, we sell
            Else
                AppendColoredText(txtLogs, "No position to reduce.", Color.Red)
                Return
            End If

            ' Validate the amount
            Dim amount As Decimal
            If Not Decimal.TryParse(txtAmount.Text, amount) OrElse amount <= 0 Then
                AppendColoredText(txtLogs, "Invalid amount.", Color.Red)
                Return
            End If

            ' Validate the price
            Dim price As Decimal
            If Not Decimal.TryParse(If(TradeMode = False, txtTopBid.Text, txtTopAsk.Text), price) OrElse price <= 0 Then
                AppendColoredText(txtLogs, "Invalid price.", Color.Red)
                Return
            End If

            ' Call the function to send the reduce-only limit order
            Await SendReduceOrderAsync(price, amount, direction, isMarketOrder:=False)

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in btnReduceLimit_Click: {ex.Message}", Color.Red)
        End Try
    End Sub



    Private Async Sub btnReduceMarket_Click(sender As Object, e As EventArgs) Handles btnReduceMarket.Click
        Try
            Dim direction As String

            ' Determine the direction based on the current position
            If TradeMode = True Then
                direction = "sell" ' To reduce a short, we buy
            ElseIf TradeMode = False Then
                direction = "buy" ' To reduce a long, we sell
            Else
                AppendColoredText(txtLogs, "No position to reduce.", Color.Red)
                Return
            End If

            ' Validate the amount
            Dim amount As Decimal
            If Not Decimal.TryParse(txtAmount.Text, amount) OrElse amount <= 0 Then
                AppendColoredText(txtLogs, "Invalid amount.", Color.Red)
                Return
            End If

            ' Call the function to send the reduce-only market order
            Await SendReduceOrderAsync(Nothing, amount, direction, isMarketOrder:=True)

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in btnReduceMarket_Click: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Async Sub btnEditTPPrice_Click(sender As Object, e As EventArgs) Handles btnEditTPPrice.Click
        Try
            If (isTrailingStop = True) And (isTrailingPosition = True) And (isTrailingStopLossPlaced = True) Then
                If Decimal.Parse(txtManualTP.Text) > 0 Then
                    txtPlacedTakeProfitPrice.Text = txtManualTP.Text
                    If TradeMode = True Then
                        If Decimal.Parse(txtPlacedTakeProfitPrice.Text) < (Decimal.Parse(txtPlacedPrice.Text) + Decimal.Parse(txtComms.Text)) Then
                            AppendColoredText(txtLogs, "Manual TP is less than comms paid.", Color.Yellow)
                        End If
                    Else
                        If Decimal.Parse(txtPlacedTakeProfitPrice.Text) > (Decimal.Parse(txtPlacedPrice.Text) - Decimal.Parse(txtComms.Text)) Then
                            AppendColoredText(txtLogs, "Manual TP is less than comms paid.", Color.Yellow)
                        End If
                    End If

                Else
                    If TradeMode = True Then
                        txtPlacedTakeProfitPrice.Text = Decimal.Parse(txtPlacedPrice.Text) + (Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text))
                    Else
                        txtPlacedTakeProfitPrice.Text = Decimal.Parse(txtPlacedPrice.Text) - (Decimal.Parse(txtTPOffset.Text) + Decimal.Parse(txtComms.Text))
                    End If
                End If
                AppendColoredText(txtLogs, $"Updated Trailing SL target to: ${txtPlacedTakeProfitPrice.Text}", Color.Yellow)

            Else
                If Decimal.Parse(txtPlacedTakeProfitPrice.Text) > 0 Then
                    Dim newTPprice = Decimal.Parse(txtPlacedTakeProfitPrice.Text)
                    Dim amount = Decimal.Parse(txtAmount.Text)
                    Dim TPOrderID As String = Nothing

                    ' Ensure WebSocket is connected
                    If webSocketClient Is Nothing OrElse webSocketClient.State <> WebSocketState.Open Then
                        'txtLogs.AppendText("WebSocket is not connected." + Environment.NewLine)
                        AppendColoredText(txtLogs, "WebSocket is not connected.", Color.Red)
                        Return
                    End If

                    If CurrentTPOrderId IsNot Nothing Then
                        TPOrderID = CurrentTPOrderId
                    ElseIf PositionTPOrderId IsNot Nothing Then
                        TPOrderID = PositionTPOrderId
                    Else
                        AppendColoredText(txtLogs, "T.P. Order ID not found for edit.", Color.Yellow)
                    End If

                    ' Construct the payload for updating the take profit order
                    Dim updateTakeProfitPayload As New JObject From {
                {"jsonrpc", "2.0"},
                {"id", 223345},
                {"method", "private/edit"},
                {"params", New JObject From {
                    {"order_id", TPOrderID}, ' Replace with the take profit order ID
                    {"price", newTPprice},
                    {"amount", amount}
                }}
            }

                    ' Send the payload to update the take profit order
                    Await SendWebSocketMessageAsync(updateTakeProfitPayload.ToString)

                    AppendColoredText(txtLogs, $"Updated T.P. to: ${newTPprice}", Color.Yellow)

                    'Else
                    'AppendColoredText(txtLogs, "T.P. textbox is 0 or no Trailing S.L. order.", Color.Yellow)
                End If
            End If
        Catch ex As Exception
            txtLogs.AppendText("Error in btnEditTPPrice: " & ex.Message & Environment.NewLine)
        End Try
    End Sub

    Private Async Sub btnEditSLPrice_Click(sender As Object, e As EventArgs) Handles btnEditSLPrice.Click
        Try
            If Decimal.Parse(txtPlacedTrigStopPrice.Text) > 0 Then
                Dim newTSprice As Decimal = Decimal.Parse(txtPlacedTrigStopPrice.Text)
                Dim newSLprice As Decimal
                Dim amount As Decimal = Decimal.Parse(txtAmount.Text)
                Dim SLOrderID As String = Nothing

                ' Ensure WebSocket is connected
                If webSocketClient Is Nothing OrElse webSocketClient.State <> WebSocketState.Open Then
                    'txtLogs.AppendText("WebSocket is not connected." + Environment.NewLine)
                    AppendColoredText(txtLogs, "WebSocket is not connected.", Color.Red)
                    Return
                End If

                If CurrentSLOrderId IsNot Nothing Then
                    SLOrderID = CurrentSLOrderId
                ElseIf PositionTPOrderId IsNot Nothing Then
                    SLOrderID = PositionSLOrderId
                Else
                    AppendColoredText(txtLogs, "S.L. Order ID not found for edit.", Color.Yellow)
                End If

                If TradeMode = True Then
                    newSLprice = newTSprice - Decimal.Parse(txtStopLoss.Text)
                Else
                    newSLprice = newTSprice + Decimal.Parse(txtStopLoss.Text)
                End If

                ' Construct the payload for updating the take profit order
                Dim updateTakeProfitPayload As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", 223346},
            {"method", "private/edit"},
            {"params", New JObject From {
                {"order_id", SLOrderID}, ' Replace with the take profit order ID
                {"price", newSLprice},
                {"trigger_price", newTSprice},
                {"amount", amount}
            }}
        }

                ' Send the payload to update the take profit order
                Await SendWebSocketMessageAsync(updateTakeProfitPayload.ToString())

                AppendColoredText(txtLogs, $"Updated T.S. to: ${newTSprice}", Color.Yellow)
                AppendColoredText(txtLogs, $"Updated S.L. to: ${newSLprice}", Color.Yellow)
            Else
                AppendColoredText(txtLogs, "S.L. textbox is 0", Color.Yellow)
            End If

        Catch ex As Exception
            txtLogs.AppendText("Error in btnEditSLPrice: " & ex.Message & Environment.NewLine)
        End Try
    End Sub

    Private Async Sub btnTrail_Click(sender As Object, e As EventArgs) Handles btnTrail.Click
        Try
            ' Get margin estimation before placing order
            btnEstimateMargins_Click(Nothing, Nothing) ' Call the estimation function

            ' Wait a moment for UI update
            Await Task.Delay(500)

            ' Then execute the order
            If TradeMode = True Then
                Await StopLossForTrailingOrderAsync("BuyTrail")
            Else
                Await StopLossForTrailingOrderAsync("SellTrail")
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error in btnTrail_Click: {ex.Message}", Color.Red)
        End Try

    End Sub

    Private Sub btnViewTrades_Click(sender As Object, e As EventArgs) Handles btnViewTrades.Click
        Try
            If tradeDatabase Is Nothing Then
                AppendColoredText(txtLogs, "Trade database not initialized", Color.Red)
                Return
            End If

            Dim trades = tradeDatabase.GetAllTrades

            If trades.Count > 0 Then
                Dim viewForm As New Form
                viewForm.Text = "Trade History - Right-click to Delete"
                viewForm.Size = New Size(1000, 700)
                viewForm.StartPosition = FormStartPosition.CenterScreen

                Dim dataGrid As New DataGridView
                dataGrid.Dock = DockStyle.Fill
                dataGrid.AutoGenerateColumns = False
                dataGrid.ReadOnly = True
                dataGrid.AllowUserToAddRows = False
                dataGrid.AllowUserToDeleteRows = False
                dataGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
                dataGrid.MultiSelect = True ' Allow multiple row selection

                ' Your existing column definitions here...
                dataGrid.Columns.Add(New DataGridViewTextBoxColumn With {
                .HeaderText = "ID",
                .DataPropertyName = "TradeId",
                .Width = 50,
                .DefaultCellStyle = New DataGridViewCellStyle With {.Alignment = DataGridViewContentAlignment.MiddleCenter}
            })

                dataGrid.Columns.Add(New DataGridViewTextBoxColumn With {
                .HeaderText = "Date/Time",
                .DataPropertyName = "Timestamp",
                .Width = 140,
                .DefaultCellStyle = New DataGridViewCellStyle With {.Format = "MM/dd/yyyy HH:mm:ss"}
            })

                dataGrid.Columns.Add(New DataGridViewTextBoxColumn With {
                .HeaderText = "Type",
                .DataPropertyName = "OrderType",
                .Width = 70,
                .DefaultCellStyle = New DataGridViewCellStyle With {.Alignment = DataGridViewContentAlignment.MiddleCenter}
            })

                dataGrid.Columns.Add(New DataGridViewTextBoxColumn With {
                .HeaderText = "Direction",
                .DataPropertyName = "Direction",
                .Width = 70,
                .DefaultCellStyle = New DataGridViewCellStyle With {.Alignment = DataGridViewContentAlignment.MiddleCenter}
            })

                dataGrid.Columns.Add(New DataGridViewTextBoxColumn With {
                .HeaderText = "Entry Price",
                .DataPropertyName = "EntryPrice",
                .Width = 100,
                .DefaultCellStyle = New DataGridViewCellStyle With {
                    .Format = "F2",
                    .Alignment = DataGridViewContentAlignment.MiddleRight
                }
            })

                dataGrid.Columns.Add(New DataGridViewTextBoxColumn With {
                .HeaderText = "Exit Price",
                .DataPropertyName = "ExitPrice",
                .Width = 100,
                .DefaultCellStyle = New DataGridViewCellStyle With {
                    .Format = "F2",
                    .Alignment = DataGridViewContentAlignment.MiddleRight
                }
            })

                dataGrid.Columns.Add(New DataGridViewTextBoxColumn With {
                .HeaderText = "Size (USD)",
                .DataPropertyName = "OrderSizeUSD",
                .Width = 100,
                .DefaultCellStyle = New DataGridViewCellStyle With {
                    .Format = "F2",
                    .Alignment = DataGridViewContentAlignment.MiddleRight
                }
            })

                dataGrid.Columns.Add(New DataGridViewTextBoxColumn With {
                .HeaderText = "P/L (USD)",
                .DataPropertyName = "ProfitLossUSD",
                .Width = 100,
                .DefaultCellStyle = New DataGridViewCellStyle With {
                    .Format = "F2",
                    .Alignment = DataGridViewContentAlignment.MiddleRight
                }
            })

                ' Add result column
                Dim resultColumn As New DataGridViewTextBoxColumn With {
                .HeaderText = "Result",
                .Name = "ResultColumn",
                .Width = 70,
                .DefaultCellStyle = New DataGridViewCellStyle With {.Alignment = DataGridViewContentAlignment.MiddleCenter}
            }
                dataGrid.Columns.Add(resultColumn)

                ' Bind data and color code rows
                dataGrid.DataSource = trades

                For Each row As DataGridViewRow In dataGrid.Rows
                    If row.DataBoundItem IsNot Nothing Then
                        Dim trade = CType(row.DataBoundItem, TradeRecord)

                        If trade.IsProfit Then
                            row.Cells("ResultColumn").Value = "WIN"
                            row.DefaultCellStyle.BackColor = Color.LightGreen
                            row.DefaultCellStyle.ForeColor = Color.DarkGreen
                        Else
                            row.Cells("ResultColumn").Value = "LOSS"
                            row.DefaultCellStyle.BackColor = Color.LightCoral
                            row.DefaultCellStyle.ForeColor = Color.DarkRed
                        End If
                    End If
                Next

                ' Add context menu for deletion
                Dim contextMenu As New ContextMenuStrip

                Dim deleteSelectedItem As New ToolStripMenuItem("Delete Selected Trade(s)")
                AddHandler deleteSelectedItem.Click, Sub()
                                                         DeleteSelectedTrades(dataGrid, trades)
                                                     End Sub

                Dim deleteAllItem As New ToolStripMenuItem("Delete All Trades")
                AddHandler deleteAllItem.Click, Sub()
                                                    DeleteAllTrades(dataGrid, trades)
                                                End Sub

                contextMenu.Items.Add(deleteSelectedItem)
                contextMenu.Items.Add(New ToolStripSeparator)
                contextMenu.Items.Add(deleteAllItem)

                dataGrid.ContextMenuStrip = contextMenu

                ' Add summary panel (your existing code)
                Dim summaryPanel As New Panel
                summaryPanel.Height = 80
                summaryPanel.Dock = DockStyle.Bottom
                summaryPanel.BackColor = Color.LightGray

                ' Calculate summary statistics
                Dim totalTrades = trades.Count
                Dim winningTrades = 0
                Dim totalPnL As Decimal = 0

                For Each trade In trades
                    If trade.IsProfit Then
                        winningTrades += 1
                    End If
                    totalPnL += trade.ProfitLossUSD
                Next

                Dim losingTrades = totalTrades - winningTrades
                Dim winRate = If(totalTrades > 0, winningTrades / totalTrades * 100, 0)

                Dim summaryLabel As New Label
                summaryLabel.Text = $"Total Trades: {totalTrades} | " &
                               $"Wins: {winningTrades} | " &
                               $"Losses: {losingTrades} | " &
                               $"Win Rate: {winRate:F1}% | " &
                               $"Total P/L: ${totalPnL:F2}"
                summaryLabel.Font = New Font("Calibri", 12, FontStyle.Bold)
                summaryLabel.ForeColor = If(totalPnL >= 0, Color.DarkGreen, Color.DarkRed)
                summaryLabel.AutoSize = True
                summaryLabel.Location = New Point(10, 30)

                summaryPanel.Controls.Add(summaryLabel)

                viewForm.Controls.Add(dataGrid)
                viewForm.Controls.Add(summaryPanel)
                viewForm.Show()

                AppendColoredText(txtLogs, $"Displaying {totalTrades} trades - Right-click to delete", Color.LimeGreen)

            Else
                AppendColoredText(txtLogs, "No trades found in database", Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error viewing trades: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub DeleteSelectedTrades(dataGrid As DataGridView, trades As List(Of TradeRecord))
        Try
            If dataGrid.SelectedRows.Count = 0 Then
                MessageBox.Show("Please select one or more trades to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim selectedTradeIds As New List(Of Integer)
            For Each row As DataGridViewRow In dataGrid.SelectedRows
                If row.DataBoundItem IsNot Nothing Then
                    Dim trade = CType(row.DataBoundItem, TradeRecord)
                    selectedTradeIds.Add(trade.TradeId)
                End If
            Next

            Dim result = MessageBox.Show($"Are you sure you want to delete {selectedTradeIds.Count} selected trade(s)?",
                                   "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

            If result = DialogResult.Yes Then
                Dim deletedCount = tradeDatabase.DeleteMultipleTrades(selectedTradeIds)

                If deletedCount > 0 Then
                    AppendColoredText(txtLogs, $"Successfully deleted {deletedCount} trade(s)", Color.LimeGreen)

                    ' Refresh the data grid
                    RefreshTradeGrid(dataGrid)
                Else
                    AppendColoredText(txtLogs, "No trades were deleted", Color.Yellow)
                End If
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error deleting selected trades: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub DeleteAllTrades(dataGrid As DataGridView, trades As List(Of TradeRecord))
        Try
            Dim result = MessageBox.Show($"Are you sure you want to delete ALL {trades.Count} trades? This action cannot be undone!",
                                   "Confirm Delete All", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)

            If result = DialogResult.Yes Then
                Dim allTradeIds = trades.Select(Function(t) t.TradeId).ToList()
                Dim deletedCount = tradeDatabase.DeleteMultipleTrades(allTradeIds)

                If deletedCount > 0 Then
                    AppendColoredText(txtLogs, $"Successfully deleted all {deletedCount} trades", Color.LimeGreen)

                    ' Refresh the data grid
                    RefreshTradeGrid(dataGrid)
                Else
                    AppendColoredText(txtLogs, "No trades were deleted", Color.Yellow)
                End If
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error deleting all trades: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub RefreshTradeGrid(dataGrid As DataGridView)
        Try
            ' Get updated trade list
            Dim updatedTrades = tradeDatabase.GetAllTrades()

            ' Update the data source
            dataGrid.DataSource = updatedTrades

            ' Reapply color coding
            For Each row As DataGridViewRow In dataGrid.Rows
                If row.DataBoundItem IsNot Nothing Then
                    Dim trade = CType(row.DataBoundItem, TradeRecord)

                    If trade.IsProfit Then
                        row.Cells("ResultColumn").Value = "WIN"
                        row.DefaultCellStyle.BackColor = Color.LightGreen
                        row.DefaultCellStyle.ForeColor = Color.DarkGreen
                    Else
                        row.Cells("ResultColumn").Value = "LOSS"
                        row.DefaultCellStyle.BackColor = Color.LightCoral
                        row.DefaultCellStyle.ForeColor = Color.DarkRed
                    End If
                End If
            Next

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error refreshing trade grid: {ex.Message}", Color.Red)
        End Try
    End Sub


    Private Sub ExportTradesToCSV(trades As List(Of TradeRecord))
        Try
            Dim saveDialog As New SaveFileDialog()
            saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            saveDialog.FileName = $"TradingHistory_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            saveDialog.Title = "Export Trade History"

            If saveDialog.ShowDialog() = DialogResult.OK Then
                Using writer As New StreamWriter(saveDialog.FileName)
                    ' Write headers
                    writer.WriteLine("TradeId,DateTime,OrderType,Direction,EntryPrice,ExitPrice,OrderSizeUSD,ProfitLossUSD,Result")

                    ' Write data
                    For Each trade In trades
                        Dim result = If(trade.IsProfit, "WIN", "LOSS")
                        writer.WriteLine($"{trade.TradeId},{trade.Timestamp:yyyy-MM-dd HH:mm:ss},{trade.OrderType},{trade.Direction},{trade.EntryPrice:F2},{trade.ExitPrice:F2},{trade.OrderSizeUSD:F2},{trade.ProfitLossUSD:F2},{result}")
                    Next
                End Using

                AppendColoredText(txtLogs, $"Trade data exported to: {saveDialog.FileName}", Color.LimeGreen)

                ' Optionally open the file
                If MessageBox.Show("Export complete. Open file now?", "Export Success", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                    Process.Start(saveDialog.FileName)
                End If
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error exporting trades: {ex.Message}", Color.Red)
            MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnEstimateMargins_Click(sender As Object,
                                           e As EventArgs) _
                                           Handles btnEstimateMargins.Click
        Try
            '---------------------------  input validation  --------------------
            If String.IsNullOrEmpty(txtAmount.Text) OrElse
           Not IsNumeric(txtAmount.Text) Then
                AppendColoredText(txtLogs, "Please enter a valid amount", Color.Yellow)
                Return
            End If

            Dim positionSizeUSD As Decimal = Decimal.Parse(txtAmount.Text)
            Dim currentPrice As Decimal = If(TradeMode, BestBidPrice, BestAskPrice)
            If currentPrice <= 0D Then
                AppendColoredText(txtLogs, "Invalid market price for estimation", Color.Yellow)
                Return
            End If

            ' **Deribit equity = total BTC in account (lblBTCEquity)**
            Dim accountBalanceBTC As Decimal = GetEquityBTC()
            Dim accountBalanceUSD As Decimal = accountBalanceBTC * currentPrice

            '-----------------------  effective leverage  ----------------------
            Dim effectiveLeverage As Decimal =
            If(accountBalanceUSD = 0D, 0D, positionSizeUSD / accountBalanceUSD)

            '----------------  call the corrected margin routine  --------------
            Dim isShort As Boolean = Not TradeMode          ' True = short
            Dim margins = CalculateDeribitInverseLiquidationPrice(
                           positionSizeUSD,
                           effectiveLeverage,
                           currentPrice,
                           isShort)

            '--------------------  update GUI labels  --------------------------
            Me.Invoke(Sub()
                          ' Liquidation price
                          If margins("EstimatedLiquidationPrice") = 0D Then
                              lblEstimatedLiquidation.Text = "Est.Liq: N/A"
                              lblEstimatedLiquidation.ForeColor = Color.Gray
                          Else
                              lblEstimatedLiquidation.Text =
                    $"Est.Liq: ${margins("EstimatedLiquidationPrice"):F2}"
                              lblEstimatedLiquidation.ForeColor = Color.Orange
                          End If

                          ' Margins
                          lblInitialMargin.Text = $"IM: {margins("InitialMarginBTC"):F8}"
                          lblMaintenanceMargin.Text = $"MM: {margins("MaintenanceMarginBTC"):F8}"

                          ' Leverage & colour-coding
                          lblEstimatedLeverage.Text = $"Lev: {margins("EffectiveLeverage"):F1}x"
                          Select Case margins("EffectiveLeverage")
                              Case > 10 : lblEstimatedLeverage.ForeColor = Color.Red
                              Case > 5 : lblEstimatedLeverage.ForeColor = Color.Orange
                              Case Else : lblEstimatedLeverage.ForeColor = Color.LimeGreen
                          End Select
                      End Sub)

            Dim liqTxt = If(margins("EstimatedLiquidationPrice") = 0D,
                        "N/A",
                        "$" & margins("EstimatedLiquidationPrice").ToString("F2"))
            AppendColoredText(txtLogs,
                          $"Liq: {liqTxt}, Lev: {margins("EffectiveLeverage"):F1}x",
                          Color.LimeGreen)

        Catch ex As Exception
            AppendColoredText(txtLogs,
                          $"Error in Deribit inverse margin estimation: {ex.Message}",
                          Color.Red)
        End Try
    End Sub

    Private Async Sub btnRefreshLiveData_Click(sender As Object, e As EventArgs) Handles btnRefreshLiveData.Click
        Try
            ' Only refresh if we have a position
            If Decimal.Parse(txtPlacedPrice.Text) > 0 Then
                Await GetLivePositionData("BTC-PERPETUAL")
            Else
                AppendColoredText(txtLogs, "No active position to refresh", Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(txtLogs, $"Error refreshing live data: {ex.Message}", Color.Red)
        End Try
    End Sub

    Private Sub btnMark_Click(sender As Object, e As EventArgs) Handles btnMark.Click
        StopLossTriggerOriginal = Decimal.Parse(txtPlacedTrigStopPrice.Text)
    End Sub

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp As CreateParams = MyBase.CreateParams
            ' Disable the maximize and close buttons
            cp.ClassStyle = cp.ClassStyle Or &H200  ' CS_NOCLOSE: Disables the close button
            Return cp
        End Get
    End Property

End Class

Public Class CustomLabel
    Inherits Label

    Public Sub New()
        ' Set default properties
        Me.Font = New Font("Calibri", 14, FontStyle.Regular) ' Change to your preferred font
        Me.ForeColor = Color.WhiteSmoke              ' Change to your preferred color
        Me.AutoSize = True                            ' Optional: Ensure the label resizes automatically
    End Sub
End Class
Public Class CustomTextBox
    Inherits TextBox

    Public Sub New()
        ' Set default properties
        Me.Font = New Font("Calibri", 16, FontStyle.Bold)
        Me.ForeColor = SystemColors.WindowText
        Me.BackColor = Color.WhiteSmoke
        Me.TextAlign = HorizontalAlignment.Center
    End Sub

    Protected Overrides Sub OnCreateControl()
        MyBase.OnCreateControl()
        Me.Size = New Size(200, 47) ' Enforce size
        If String.IsNullOrEmpty(Me.Text) Then
            Me.Text = "0" ' Set default text if none exists
        End If
    End Sub
End Class

