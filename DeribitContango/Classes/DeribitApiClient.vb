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
                                              side As String, ' "buy" or "sell"
                                              Optional amount As Decimal? = Nothing,
                                              Optional contracts As Integer? = Nothing,
                                              Optional price As Decimal? = Nothing,
                                              Optional orderType As String = "limit",
                                              Optional tif As String = "good_til_cancelled",
                                              Optional postOnly As Boolean? = Nothing,
                                              Optional reduceOnly As Boolean? = Nothing,
                                              Optional label As String = Nothing) As Task(Of JObject)

            Dim method As String = If(side = "buy", "private/buy", "private/sell")
            Dim p As New JObject From {
              {"instrument_name", instrument},
              {"type", orderType},
              {"time_in_force", tif}
            }
            If price.HasValue Then p("price") = price.Value
            If amount.HasValue Then p("amount") = amount.Value
            If contracts.HasValue Then p("contracts") = contracts.Value
            If postOnly.HasValue Then p("post_only") = postOnly.Value
            If reduceOnly.HasValue Then p("reduce_only") = reduceOnly.Value
            If Not String.IsNullOrEmpty(label) Then p("label") = label

            Dim res = Await SendAsync(method, p)
            Return res("result").Value(Of JObject)()
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
                        Dim data = paramsObj("data")?.Value(Of JObject)()
                        If Not String.IsNullOrEmpty(ch) AndAlso data IsNot Nothing Then
                            If ch.StartsWith("user.orders.") Then
                                Dim parts = ch.Split("."c)
                                Dim currency = parts.Last()
                                RaiseEvent OrderUpdate(currency, data)
                            ElseIf ch.StartsWith("user.trades.") Then
                                Dim parts = ch.Split("."c)
                                Dim currency = parts.Last()
                                RaiseEvent TradeUpdate(currency, data)
                            Else
                                RaiseEvent PublicMessage(ch, data)
                            End If
                        End If
                    End If
                End If
            End While

            _connected = False
            RaiseEvent ConnectionStateChanged(False)
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
