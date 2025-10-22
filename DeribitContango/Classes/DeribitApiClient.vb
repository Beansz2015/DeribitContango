Imports System.Net.WebSockets
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Namespace DeribitContango
    Public Class DeribitApiClient
        Implements IDisposable

        Private ReadOnly _ws As ClientWebSocket = New ClientWebSocket()
        Private _cts As CancellationTokenSource
        Private _recvTask As Task
        Private _reqId As Integer = 0
        Private ReadOnly _pending As New Concurrent.ConcurrentDictionary(Of Integer, TaskCompletionSource(Of JToken))()
        Private ReadOnly _rate As DeribitRateLimiter
        Private _accessToken As String = ""
        Private _connected As Boolean = False
        Private _sessionName As String = "contango"

        ' Public events
        Public Event PublicMessage(topic As String, payload As JObject)
        Public Event OrderUpdate(currency As String, payload As JObject)
        Public Event TradeUpdate(currency As String, payload As JObject)
        Public Event ConnectionStateChanged(connected As Boolean)

        Public Sub New(rateLimiter As DeribitRateLimiter)
            _rate = rateLimiter
        End Sub

        Public ReadOnly Property IsConnected As Boolean
            Get
                Return _connected
            End Get
        End Property

        Public Async Function ConnectAsync(uri As Uri) As Task
            _cts = New CancellationTokenSource()
            Await _ws.ConnectAsync(uri, _cts.Token)
            _connected = True
            RaiseEvent ConnectionStateChanged(True)
            _recvTask = Task.Run(AddressOf ReceiveLoop)
        End Function

        Public Async Function AuthorizeAsync(clientId As String, clientSecret As String) As Task
            Dim p As New JObject From {
              {"grant_type", "client_credentials"},
              {"client_id", clientId},
              {"client_secret", clientSecret}
            }
            Dim res = Await SendAsync("public/auth", p)
            _accessToken = res.Value(Of JObject)("result")?.Value(Of String)("access_token")
            If String.IsNullOrEmpty(_accessToken) Then
                Throw New ApplicationException("Authorization failed: missing access_token")
            End If
        End Function

        Public Async Function SubscribePublicAsync(topics As IEnumerable(Of String)) As Task
            Dim p As New JObject From {
              {"channels", New JArray(topics)}
            }
            Await SendAsync("public/subscribe", p)
        End Function

        Public Async Function SubscribePrivateAsync(topics As IEnumerable(Of String)) As Task
            Dim p As New JObject From {
              {"channels", New JArray(topics)}
            }
            Await SendAsync("private/subscribe", p)
        End Function

        Public Async Function PublicGetInstrumentsAsync(currency As String, kind As String, expired As Boolean) As Task(Of JArray)
            Dim p As New JObject From {
              {"currency", currency},
              {"kind", kind},
              {"expired", expired}
            }
            Dim res = Await SendAsync("public/get_instruments", p)
            Return res("result").Value(Of JArray)()
        End Function

        Public Async Function PublicGetIndexPriceAsync(indexName As String) As Task(Of Decimal)
            Dim p As New JObject From {{"index_name", indexName}}
            Dim res = Await SendAsync("public/get_index_price", p)
            Return res("result").Value(Of Decimal)("index_price")
        End Function

        Public Async Function PlaceOrderAsync(instrument As String,
                                      side As String,
                                      Optional amount As Decimal? = Nothing,
                                      Optional price As Decimal? = Nothing,
                                      Optional orderType As String = "limit",
                                      Optional tif As String = "good_til_cancelled",
                                      Optional postOnly As Boolean? = Nothing,
                                      Optional reduceOnly As Boolean? = Nothing,
                                      Optional label As String = Nothing) As Task(Of JObject)
            ' amount is REQUIRED:
            ' - Futures/perpetuals (inverse): USD notional (must be a multiple of 10)
            ' - Spot: base currency amount (e.g., BTC)
            If Not amount.HasValue OrElse amount.Value <= 0D Then
                Throw New ArgumentException("amount is required and must be > 0 for all instruments")
            End If

            Dim req As New JObject From {
    {"instrument_name", instrument},
    {"type", orderType},
    {"amount", amount.Value}
  }

            If Not String.IsNullOrEmpty(side) Then req("side") = side
            If Not String.IsNullOrEmpty(tif) Then req("time_in_force") = tif
            If postOnly.HasValue Then req("post_only") = postOnly.Value
            If reduceOnly.HasValue Then req("reduce_only") = reduceOnly.Value
            If Not String.IsNullOrEmpty(label) Then req("label") = label

            ' Only include price for limit orders; never for market_limit
            If orderType.Equals("limit", StringComparison.OrdinalIgnoreCase) AndAlso price.HasValue Then
                req("price") = price.Value
            End If

            Dim method As String = If(side.Equals("buy", StringComparison.OrdinalIgnoreCase), "private/buy", "private/sell")
            Dim res = Await SendAsync(method, req)
            Return res("result")?.Value(Of JObject)()
        End Function


        Public Async Function EditOrderAsync(orderId As String,
                                             Optional amount As Decimal? = Nothing,
                                             Optional contracts As Integer? = Nothing,
                                             Optional price As Decimal? = Nothing,
                                             Optional postOnly As Boolean? = Nothing) As Task(Of JObject)
            Dim p As New JObject From {{"order_id", orderId}}
            If price.HasValue Then p("price") = price.Value
            If amount.HasValue Then p("amount") = amount.Value
            If contracts.HasValue Then p("contracts") = contracts.Value
            If postOnly.HasValue Then p("post_only") = postOnly.Value

            Dim res = Await SendAsync("private/edit", p)
            Return res("result").Value(Of JObject)()
        End Function

        Public Async Function CancelOrderAsync(orderId As String) As Task(Of Boolean)
            Dim p As New JObject From {{"order_id", orderId}}
            Dim res = Await SendAsync("private/cancel", p)
            Dim r As JToken = res("result")
            Return (r IsNot Nothing) AndAlso (r.Type <> JTokenType.Null)
        End Function

        Public Async Function GetOrderStateAsync(orderId As String) As Task(Of JObject)
            Dim p As New JObject From {{"order_id", orderId}}
            Dim res = Await SendAsync("private/get_order_state", p)
            Return res("result").Value(Of JObject)()
        End Function

        Public Async Function GetPositionsAsync(currency As String, kind As String) As Task(Of JArray)
            Dim p As New JObject From {{"currency", currency}, {"kind", kind}}
            Dim res = Await SendAsync("private/get_positions", p)
            Return res("result").Value(Of JArray)()
        End Function

        Public Async Function SendAsync(method As String, paramsObj As JObject) As Task(Of JToken)
            Await _rate.WaitTurnAsync()
            Dim id As Integer = Threading.Interlocked.Increment(_reqId)
            Dim tcs As New TaskCompletionSource(Of JToken)(TaskCreationOptions.RunContinuationsAsynchronously)
            _pending(id) = tcs

            Dim req As New JObject From {
              {"jsonrpc", "2.0"},
              {"id", id},
              {"method", method},
              {"params", paramsObj}
            }
            ' Attach access token automatically for private endpoints
            If method.StartsWith("private/") Then
                paramsObj("access_token") = _accessToken
            End If

            Dim payload As String = req.ToString(Formatting.None)
            Dim bytes = Encoding.UTF8.GetBytes(payload)
            Await _ws.SendAsync(bytes, WebSocketMessageType.Text, True, _cts.Token)

            Return Await tcs.Task
        End Function

        Private Async Function ReceiveLoop() As Task
            Dim buffer = New Byte(65535) {}
            Dim sb As New StringBuilder()

            While _ws.State = WebSocketState.Open AndAlso Not _cts.IsCancellationRequested
                sb.Clear()
                Dim result As WebSocketReceiveResult = Nothing
                Do
                    result = Await _ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), _cts.Token)
                    If result.MessageType = WebSocketMessageType.Close Then
                        Exit While
                    End If
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count))
                Loop While Not result.EndOfMessage

                Dim msg As JObject = Nothing
                Try
                    msg = JObject.Parse(sb.ToString())
                Catch
                    Continue While
                End Try

                Dim idTok = msg("id")
                Dim methodTok = msg("method")

                If idTok IsNot Nothing AndAlso idTok.Type = JTokenType.Integer Then
                    Dim id = idTok.Value(Of Integer)()
                    Dim tcs As TaskCompletionSource(Of JToken) = Nothing
                    If _pending.TryRemove(id, tcs) Then
                        If msg("error") IsNot Nothing Then
                            tcs.SetException(New ApplicationException(msg("error").ToString()))
                        Else
                            tcs.SetResult(msg)
                        End If
                    End If

                ElseIf methodTok IsNot Nothing Then
                    Dim m = methodTok.Value(Of String)()
                    Dim paramsObj = msg("params")?.Value(Of JObject)()
                    If m = "subscription" AndAlso paramsObj IsNot Nothing Then
                        Dim ch = paramsObj.Value(Of String)("channel")
                        Dim dataTok = paramsObj("data")
                        If String.IsNullOrEmpty(ch) OrElse dataTok Is Nothing Then
                            Continue While
                        End If

                        ' Per-object raisers with normalization for user.trades
                        Dim raiseOrder As Action(Of String, JObject) =
          Sub(cur As String, obj As JObject)
              RaiseEvent OrderUpdate(cur, obj)
          End Sub

                        Dim raiseTrade As Action(Of String, JObject) =
          Sub(cur As String, obj As JObject)
              If obj Is Nothing Then Return
              ' Normalize to { instrument_name, trades:[obj] } if not already wrapped
              If obj("trades") Is Nothing Then
                  Dim instr = obj.Value(Of String)("instrument_name")
                  Dim wrapper As New JObject From {
                {"instrument_name", instr},
                {"trades", New JArray(obj)}
              }
                  RaiseEvent TradeUpdate(cur, wrapper)
              Else
                  RaiseEvent TradeUpdate(cur, obj)
              End If
          End Sub

                        If ch.StartsWith("user.orders.", StringComparison.OrdinalIgnoreCase) Then
                            Dim currency = ch.Split("."c).Last()
                            If dataTok.Type = JTokenType.Array Then
                                For Each item In CType(dataTok, JArray)
                                    If item.Type = JTokenType.Object Then
                                        raiseOrder(currency, CType(item, JObject))
                                    End If
                                Next
                            ElseIf dataTok.Type = JTokenType.Object Then
                                raiseOrder(currency, CType(dataTok, JObject))
                            End If

                        ElseIf ch.StartsWith("user.trades.", StringComparison.OrdinalIgnoreCase) Then
                            Dim currency = ch.Split("."c).Last()
                            If dataTok.Type = JTokenType.Array Then
                                For Each item In CType(dataTok, JArray)
                                    If item.Type = JTokenType.Object Then
                                        raiseTrade(currency, CType(item, JObject))
                                    End If
                                Next
                            ElseIf dataTok.Type = JTokenType.Object Then
                                raiseTrade(currency, CType(dataTok, JObject))
                            End If

                        Else
                            ' Public channels
                            If dataTok.Type = JTokenType.Object Then
                                RaiseEvent PublicMessage(ch, CType(dataTok, JObject))
                            End If
                        End If
                    End If
                End If
            End While

            _connected = False
            RaiseEvent ConnectionStateChanged(False)
        End Function

        Public Async Function GetUserTradesByOrderAsync(orderId As String) As Task(Of JArray)
            Dim p As New JObject From {
    {"order_id", orderId},
    {"sorting", "asc"},
    {"count", 200}
  }
            Dim res = Await SendAsync("private/get_user_trades_by_order", p)
            Dim arr = res("result")?.Value(Of JArray)()
            If arr Is Nothing Then Return New JArray()
            Return arr
        End Function

        ' Close entire position by instrument with a market or limit close
        Public Async Function ClosePositionAsync(instrument As String,
                                         closeType As String,   ' "market" or "limit"
                                         Optional price As Decimal? = Nothing) As Task(Of JObject)
            Dim p As New JObject From {
    {"instrument_name", instrument},
    {"type", closeType}
  }
            If closeType.Equals("limit", StringComparison.OrdinalIgnoreCase) AndAlso price.HasValue Then
                p("price") = price.Value
            End If
            Dim res = Await SendAsync("private/close_position", p)
            Return res("result")?.Value(Of JObject)()
        End Function

        ' Reads account summary for a currency (e.g., BTC) to obtain tradable spot balance.
        Public Async Function GetAccountSummaryAsync(currency As String) As Task(Of JObject)
            Dim p As New JObject From {
        {"currency", currency},
        {"extended", True}
    }
            Dim res = Await SendAsync("private/get_account_summary", p)
            Return res("result").Value(Of JObject)()
        End Function


        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                _cts?.Cancel()
            Catch
            End Try
            Try
                _ws?.Dispose()
            Catch
            End Try
        End Sub
    End Class
End Namespace
