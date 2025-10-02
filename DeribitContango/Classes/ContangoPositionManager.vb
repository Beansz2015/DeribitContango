Imports Newtonsoft.Json.Linq
Imports System.Collections.Concurrent

Namespace DeribitContango
    Public Class ContangoPositionManager
        Private ReadOnly _api As DeribitApiClient
        Private ReadOnly _db As ContangoDatabase
        Private ReadOnly _monitor As ContangoBasisMonitor

        ' Instruments
        Public Property SpotInstrument As String = "BTC_USDC"
        Public Property FuturesInstrument As String = ""  ' e.g., "BTC-04OCT25"
        Public Property FuturesIsInverse As Boolean = True
        Public Property ExpiryUtc As DateTime

        ' Inputs
        Public Property EntryThreshold As Decimal = 0.0025D
        Public Property TargetUsd As Decimal = 0D
        Public Property TargetBtc As Decimal = 0D
        Public Property UseUsdInput As Boolean = True
        Public Property MaxSlippageBps As Decimal = 5D

        ' Instrument specs (defaults are conservative)
        Private _spotTick As Decimal = 0.01D
        Private _spotMinAmount As Decimal = 0.00001D
        Private _spotAmountStep As Decimal = 0.00001D
        Private _futTick As Decimal = 0.5D

        ' Order tracking / state
        Private ReadOnly _openOrderIds As New HashSet(Of String)()
        Private ReadOnly _lock As New Object()

        ' Pending futures hedge after spot-first flow
        Private _pendingFutContracts As Integer = 0
        Private _hedgeWatchTsUtc As DateTime = Date.MinValue
        Private ReadOnly _hedgeTimeoutSeconds As Integer = 10

        Public Sub New(api As DeribitApiClient, db As ContangoDatabase, monitor As ContangoBasisMonitor)
            _api = api
            _db = db
            _monitor = monitor
            AddHandler _api.OrderUpdate, AddressOf OnOrderUpdate
            AddHandler _api.TradeUpdate, AddressOf OnTradeUpdate
        End Sub

        ' Discover nearest unexpired inverse BTC weekly, or strictly next when rolling
        Public Async Function DiscoverNearestWeeklyAsync(Optional strictNextBeyond As DateTime? = Nothing) As Task
            Dim instruments = Await _api.PublicGetInstrumentsAsync("BTC", "future", False)
            Dim bestName As String = Nothing
            Dim bestExp As DateTime = Date.MaxValue

            For Each it In instruments
                Dim o = it.Value(Of JObject)()
                Dim name = o.Value(Of String)("instrument_name")
                Dim kind = o.Value(Of String)("kind")
                Dim sett = o.Value(Of String)("settlement_currency")
                Dim expMs = o.Value(Of Long)("expiration_timestamp")
                Dim expUtc = DateTimeOffset.FromUnixTimeMilliseconds(expMs).UtcDateTime
                Dim isPerp = name.Contains("PERPETUAL")
                If kind = "future" AndAlso Not isPerp AndAlso sett = "BTC" Then
                    If expUtc > DateTime.UtcNow Then
                        If strictNextBeyond.HasValue Then
                            If expUtc > strictNextBeyond.Value AndAlso expUtc < bestExp Then
                                bestExp = expUtc
                                bestName = name
                            End If
                        Else
                            If expUtc < bestExp Then
                                bestExp = expUtc
                                bestName = name
                            End If
                        End If
                    End If
                End If
            Next

            If bestName Is Nothing Then
                Throw New ApplicationException("No suitable weekly inverse BTC future found")
            End If
            FuturesInstrument = bestName
            ExpiryUtc = bestExp
            _monitor.WeeklyInstrument = bestName
            _monitor.WeeklyExpiryUtc = bestExp

            ' Refresh futures tick after selection
            Await RefreshInstrumentSpecsAsync()
        End Function

        ' Load and cache tick/size constraints for spot and futures
        Public Async Function RefreshInstrumentSpecsAsync() As Task
            ' Spot
            Dim spotSet = Await _api.PublicGetInstrumentsAsync("BTC", "spot", False)
            For Each it In spotSet
                Dim o = it.Value(Of JObject)()
                If String.Equals(o.Value(Of String)("instrument_name"), SpotInstrument, StringComparison.OrdinalIgnoreCase) Then
                    _spotTick = o.Value(Of Decimal?)("tick_size").GetValueOrDefault(_spotTick)
                    _spotMinAmount = o.Value(Of Decimal?)("min_trade_amount").GetValueOrDefault(_spotMinAmount)
                    Dim stepV = o.Value(Of Decimal?)("min_trade_amount_increment").GetValueOrDefault(0D)
                    If stepV > 0D Then _spotAmountStep = stepV
                End If
            Next

            ' Futures
            If Not String.IsNullOrEmpty(FuturesInstrument) Then
                Dim futSet = Await _api.PublicGetInstrumentsAsync("BTC", "future", False)
                For Each it In futSet
                    Dim o = it.Value(Of JObject)()
                    If String.Equals(o.Value(Of String)("instrument_name"), FuturesInstrument, StringComparison.OrdinalIgnoreCase) Then
                        _futTick = o.Value(Of Decimal?)("tick_size").GetValueOrDefault(_futTick)
                    End If
                Next
            End If
        End Function

        ' Inverse contract sizing helpers
        Public Function ComputeContractsFromUsd(usdNotional As Decimal) As Integer
            Dim contracts As Integer = CInt(Math.Round(usdNotional / 10D, MidpointRounding.AwayFromZero))
            If contracts < 1 Then contracts = 1
            Return contracts
        End Function

        Public Function ComputeBtcFromContracts(contracts As Integer, fillPriceUsd As Decimal) As Decimal
            If fillPriceUsd <= 0 Then Return 0D
            Return (contracts * 10D) / fillPriceUsd
        End Function

        ' Input normalization (ensures 10 USD/contract feasibility)
        Public Function NormalizeInputs(indexPrice As Decimal) As (targetUsd As Decimal, targetBtc As Decimal)
            If UseUsdInput Then
                If TargetUsd < 10D Then Throw New ApplicationException("USD amount too small for 10 USD/contract granularity")
                Return (TargetUsd, If(indexPrice > 0D, TargetUsd / indexPrice, 0D))
            Else
                Dim usd = TargetBtc * indexPrice
                If usd < 10D Then Throw New ApplicationException("BTC amount too small for 10 USD/contract granularity")
                Return (usd, TargetBtc)
            End If
        End Function

        ' Rounding utilities
        Private Function RoundToTick(px As Decimal, tick As Decimal) As Decimal
            If tick <= 0D Then Return px
            Return Math.Round(px / tick, MidpointRounding.AwayFromZero) * tick
        End Function

        Private Function RoundDownToStep(amount As Decimal, stepVal As Decimal) As Decimal
            If stepVal <= 0D Then Return amount
            Dim steps = Math.Floor(amount / stepVal)
            Return steps * stepVal
        End Function

        ' Entry: SPOT FIRST (BTC_USDC IOC), then FUTURES short post_only; IOC fallback after timeout
        Public Async Function EnterBasisAsync(indexPrice As Decimal,
                                      futBestBid As Decimal,
                                      spotBestAsk As Decimal) As Task
            Dim inputs = NormalizeInputs(indexPrice)
            Dim usdN As Decimal = inputs.targetUsd
            Dim btcN As Decimal = inputs.targetBtc

            Dim basis = _monitor.BasisMid
            If basis < EntryThreshold Then
                Throw New ApplicationException($"Basis {basis:P2} below threshold {EntryThreshold:P2}")
            End If

            ' Ensure instrument specs are present
            Await RefreshInstrumentSpecsAsync()

            ' 1) Buy spot BTC_USDC IOC marketable limit
            Dim mid = _monitor.SpotMid
            If mid <= 0D Then
                mid = If(spotBestAsk > 0D, spotBestAsk, _monitor.IndexPriceUsd)
            End If
            If mid <= 0D Then Throw New ApplicationException("Spot price unavailable")
            Dim limitUp = RoundToTick(mid * (1D + MaxSlippageBps / 10000D), _spotTick)

            Dim amt = btcN
            amt = RoundDownToStep(amt, _spotAmountStep)
            If amt < _spotMinAmount Then
                Throw New ApplicationException($"Amount below min_trade_amount {_spotMinAmount}")
            End If

            Dim spotOrder = Await _api.PlaceOrderAsync(
    instrument:=SpotInstrument,
    side:="buy",
    amount:=amt,
    contracts:=Nothing,
    price:=limitUp,
    orderType:="limit",
    tif:="immediate_or_cancel",
    postOnly:=Nothing,
    reduceOnly:=Nothing,
    label:="ContangoSpotIOC"
  )
            TrackOrder(spotOrder)

            ' Immediate fallback to place futures if private trade event is delayed/missed
            Await HandleSpotImmediateFillAsync(spotOrder)

            ' Initialize hedge timer for IOC fallback as an extra guard
            SyncLock _lock
                If _hedgeWatchTsUtc = Date.MinValue Then _hedgeWatchTsUtc = DateTime.UtcNow
            End SyncLock
        End Function


        Private Async Function HandleSpotImmediateFillAsync(spotOrderResult As JObject) As Task
            Try
                Dim ord = spotOrderResult?("order")?.Value(Of JObject)()
                Dim oid As String = ord?.Value(Of String)("order_id")
                Dim filledAmt As Decimal = ord?.Value(Of Decimal?)("filled_amount").GetValueOrDefault(0D)
                Dim avgPx As Decimal = ord?.Value(Of Decimal?)("average_price").GetValueOrDefault(0D)

                If (filledAmt <= 0D OrElse avgPx <= 0D) AndAlso Not String.IsNullOrEmpty(oid) Then
                    Dim st = Await _api.GetOrderStateAsync(oid)
                    If st IsNot Nothing Then
                        filledAmt = st.Value(Of Decimal?)("filled_amount").GetValueOrDefault(filledAmt)
                        avgPx = st.Value(Of Decimal?)("average_price").GetValueOrDefault(avgPx)
                    End If
                End If

                If filledAmt <= 0D Then Return

                ' Compute notional from realized spot fill
                Dim notional As Decimal = filledAmt * If(avgPx > 0D, avgPx, _monitor.SpotMid)
                If notional <= 0D Then Return

                ' Ensure futures specs and current bid
                Await RefreshInstrumentSpecsAsync()
                Dim bid = If(_monitor.WeeklyFutureBestBid > 0D, _monitor.WeeklyFutureBestBid, _monitor.WeeklyFutureMark)
                If bid <= 0D Then Return
                Dim px = RoundToTick(bid, _futTick)
                Dim contracts As Integer = ComputeContractsFromUsd(notional)

                ' Place post_only futures short now (no need to wait for private trades)
                Dim futLabel = $"ContangoFutShort_{DateTime.UtcNow:HHmmss}_FB"
                Dim futOrder = Await _api.PlaceOrderAsync(
      instrument:=FuturesInstrument,
      side:="sell",
      contracts:=contracts,
      price:=px,
      orderType:="limit",
      tif:="good_til_cancelled",
      postOnly:=True,
      reduceOnly:=False,
      label:=futLabel
    )
                TrackOrder(futOrder)

                ' Immediate persistence/log for operator visibility
                Try
                    Dim o = futOrder?("order")?.Value(Of JObject)()
                    Dim futOid = o?.Value(Of String)("order_id")
                    Dim futPx = o?.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                    _db.SaveTrade($"ORDER-{futOid}", DateTime.UtcNow, "sell", FuturesInstrument, Nothing, Nothing, If(futPx > 0D, futPx, 0D), "BTC", 0D)
                Catch
                End Try

                SyncLock _lock
                    _pendingFutContracts += contracts
                    If _hedgeWatchTsUtc = Date.MinValue Then _hedgeWatchTsUtc = DateTime.UtcNow
                End SyncLock

                StartHedgeFallbackTimer()
            Catch
                ' Swallow to avoid breaking the calling flow; private events may still trigger the hedge
            End Try
        End Function



        ' Roll: close current via reduce_only, then select strictly next weekly and later user can re-enter
        Public Async Function RollToNextWeeklyAsync(indexPrice As Decimal) As Task
            If String.IsNullOrEmpty(FuturesInstrument) Then Throw New ApplicationException("No active weekly instrument")

            ' Close current short (reduce_only IOC)
            Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
            Dim curContracts As Integer = 0
            For Each p In posArr
                Dim o = p.Value(Of JObject)()
                If o.Value(Of String)("instrument_name") = FuturesInstrument Then
                    curContracts = Math.Abs(o.Value(Of Integer)("size"))
                End If
            Next
            If curContracts > 0 Then
                Dim closeBuy = Await _api.PlaceOrderAsync(
                  instrument:=FuturesInstrument,
                  side:="buy",
                  contracts:=curContracts,
                  orderType:="market",
                  tif:="immediate_or_cancel",
                  reduceOnly:=True,
                  label:="ContangoRollClose"
                )
                TrackOrder(closeBuy)
            End If

            ' Strictly select next beyond current expiry
            Dim curExp = ExpiryUtc
            Await DiscoverNearestWeeklyAsync(curExp)
        End Function

        Public Async Function CloseAllAsync() As Task
            Try
                Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
                For Each p In posArr
                    Dim o = p.Value(Of JObject)()
                    Dim name = o.Value(Of String)("instrument_name")
                    Dim sz = o.Value(Of Integer)("size")
                    If sz < 0 Then
                        Dim contracts = Math.Abs(sz)
                        Dim closeBuy = Await _api.PlaceOrderAsync(
                          instrument:=name,
                          side:="buy",
                          contracts:=contracts,
                          orderType:="market",
                          tif:="immediate_or_cancel",
                          reduceOnly:=True,
                          label:="ContangoClose"
                        )
                        TrackOrder(closeBuy)
                    End If
                Next
            Catch
            End Try
        End Function

        Private Sub TrackOrder(orderObj As JObject)
            Dim oid = orderObj.Value(Of JObject)("order")?.Value(Of String)("order_id")
            If Not String.IsNullOrEmpty(oid) Then
                SyncLock _lock
                    _openOrderIds.Add(oid)
                End SyncLock
            End If
        End Sub

        ' Lifecycle: order updates
        Private Sub OnOrderUpdate(currency As String, payload As JObject)
            Dim orderId = payload.Value(Of String)("order_id")
            Dim state = payload.Value(Of String)("state")
            If String.IsNullOrEmpty(orderId) Then Return
            SyncLock _lock
                If _openOrderIds.Contains(orderId) AndAlso (state = "filled" OrElse state = "cancelled" OrElse state = "rejected") Then
                    _openOrderIds.Remove(orderId)
                End If
            End SyncLock
        End Sub

        ' Lifecycle: trade updates -> SPOT triggers FUTURES; FUTURES fills reduce outstanding; persist trades
        Private Async Sub OnTradeUpdate(currency As String, payload As JObject)
            Dim instrument = payload.Value(Of String)("instrument_name")
            If String.IsNullOrEmpty(instrument) Then Return
            Dim trades = payload("trades")
            If trades Is Nothing OrElse trades.Type <> JTokenType.Array OrElse trades.Count = 0 Then Return

            If instrument = SpotInstrument Then
                ' Spot fills -> place futures post_only short sized from realized spot notional
                Dim totalAmt As Decimal = 0D
                Dim w As Decimal = 0D
                Dim vwap As Decimal = 0D

                For Each t In trades
                    Dim sideStr = t.Value(Of String)("side")
                    If String.IsNullOrEmpty(sideStr) Then sideStr = t.Value(Of String)("direction")
                    If String.IsNullOrEmpty(sideStr) OrElse Not sideStr.Equals("buy", StringComparison.OrdinalIgnoreCase) Then Continue For

                    Dim amt = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                    Dim px = t.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                    Dim fee = t.Value(Of Decimal?)("fee").GetValueOrDefault(0D)
                    Dim feeCcy = t.Value(Of String)("fee_currency")

                    totalAmt += amt
                    If px > 0D AndAlso amt > 0D Then
                        vwap = (vwap * w + px * amt) / Math.Max(1D, w + amt)
                        w += amt
                    End If

                    _db.SaveTrade(
        t.Value(Of String)("trade_id"),
        DateTime.UtcNow,
        "buy",
        instrument,
        amt,
        Nothing,
        px,
        feeCcy,
        fee
      )
                Next

                If totalAmt > 0D Then
                    Await RefreshInstrumentSpecsAsync()
                    Dim notional = totalAmt * If(vwap > 0D, vwap, _monitor.SpotMid)
                    If notional > 0D Then
                        Dim bid = If(_monitor.WeeklyFutureBestBid > 0D, _monitor.WeeklyFutureBestBid, _monitor.WeeklyFutureMark)
                        If bid > 0D Then
                            Dim pxF = RoundToTick(bid, _futTick)
                            Dim contracts As Integer = ComputeContractsFromUsd(notional)

                            Dim futLabel = $"ContangoFutShort_{DateTime.UtcNow:HHmmss}"
                            Dim futOrder = Await _api.PlaceOrderAsync(
            instrument:=FuturesInstrument,
            side:="sell",
            amount:=Nothing,
            contracts:=contracts,
            price:=pxF,
            orderType:="limit",
            tif:="good_til_cancelled",
            postOnly:=True,
            reduceOnly:=False,
            label:=futLabel
          )
                            TrackOrder(futOrder)

                            ' Immediate persistence/log for operator visibility
                            Try
                                Dim o = futOrder?("order")?.Value(Of JObject)()
                                Dim futOid = o?.Value(Of String)("order_id")
                                Dim futPx = o?.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                                _db.SaveTrade($"ORDER-{futOid}", DateTime.UtcNow, "sell", FuturesInstrument, Nothing, Nothing, If(futPx > 0D, futPx, 0D), "BTC", 0D)
                            Catch
                            End Try

                            SyncLock _lock
                                _pendingFutContracts += contracts
                                If _hedgeWatchTsUtc = Date.MinValue Then _hedgeWatchTsUtc = DateTime.UtcNow
                            End SyncLock

                            StartHedgeFallbackTimer()
                        End If
                    End If
                End If

            ElseIf instrument = FuturesInstrument Then
                ' Futures fills -> decrement outstanding hedge contracts; persist trades
                Dim filledContracts As Integer = 0

                For Each t In trades
                    Dim sideStr = t.Value(Of String)("side")
                    If String.IsNullOrEmpty(sideStr) Then sideStr = t.Value(Of String)("direction")

                    Dim px = t.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                    Dim fee = t.Value(Of Decimal?)("fee").GetValueOrDefault(0D)
                    Dim feeCcy = t.Value(Of String)("fee_currency")

                    Dim c = t.Value(Of Integer?)("contracts").GetValueOrDefault(0)
                    If c = 0 Then
                        Dim amtUsd = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                        If amtUsd > 0D Then
                            c = CInt(Math.Round(amtUsd / 10D, MidpointRounding.AwayFromZero))
                        End If
                    End If

                    _db.SaveTrade(
        t.Value(Of String)("trade_id"),
        DateTime.UtcNow,
        If(String.IsNullOrEmpty(sideStr), "", sideStr),
        instrument,
        Nothing,
        c,
        px,
        feeCcy,
        fee
      )

                    If Not String.IsNullOrEmpty(sideStr) AndAlso sideStr.Equals("sell", StringComparison.OrdinalIgnoreCase) Then
                        filledContracts += c
                    End If
                Next

                If filledContracts > 0 Then
                    SyncLock _lock
                        _pendingFutContracts = Math.Max(0, _pendingFutContracts - filledContracts)
                    End SyncLock
                End If
            End If
        End Sub



        Private Sub StartHedgeFallbackTimer()
            Try
                Task.Run(AddressOf HedgeFallbackTimerAsync)
            Catch
                ' Swallow to avoid surfacing thread-pool exceptions to caller
            End Try
        End Sub

        Private Async Function HedgeFallbackTimerAsync() As Task
            Try
                Await Task.Delay(_hedgeTimeoutSeconds * 1000).ConfigureAwait(False)
                Await HedgeFallbackAsync().ConfigureAwait(False)
            Catch ex As Exception
                ' TODO: integrate with your logging/DB if desired
                ' This prevents unobserved task exceptions from crashing the process.
            End Try
        End Function


        Private Async Function HedgeFallbackAsync() As Task
            ' If futures outstanding remain past timeout or basis decays, sweep remaining IOC
            Dim needSweep As Integer = 0
            Dim basisNow As Decimal = _monitor.BasisMid
            SyncLock _lock
                If _pendingFutContracts > 0 AndAlso _hedgeWatchTsUtc <> Date.MinValue Then
                    Dim elapsed = (DateTime.UtcNow - _hedgeWatchTsUtc).TotalSeconds
                    If elapsed >= _hedgeTimeoutSeconds OrElse basisNow < Math.Max(0D, EntryThreshold - 0.0005D) Then
                        needSweep = _pendingFutContracts
                        _pendingFutContracts = 0
                        _hedgeWatchTsUtc = Date.MinValue
                    End If
                End If
            End SyncLock

            If needSweep > 0 Then
                Dim sweep = Await _api.PlaceOrderAsync(
                  instrument:=FuturesInstrument,
                  side:="sell",
                  contracts:=needSweep,
                  orderType:="market",
                  tif:="immediate_or_cancel",
                  reduceOnly:=False,
                  label:="ContangoFutIOC_Fallback"
                )
                TrackOrder(sweep)
            End If
        End Function
    End Class
End Namespace
