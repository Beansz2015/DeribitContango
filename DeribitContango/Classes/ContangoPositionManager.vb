Imports System.Threading
Imports Newtonsoft.Json.Linq

Namespace DeribitContango
    Public Class ContangoPositionManager
        Private ReadOnly _api As DeribitApiClient
        Private ReadOnly _db As ContangoDatabase
        Private ReadOnly _monitor As ContangoBasisMonitor

        ' Public operator messages (wire to UI log)
        Public Event Info(message As String)

        ' Instruments
        Public Property SpotInstrument As String = "BTC_USDC"
        Public Property FuturesInstrument As String = ""   ' e.g., "BTC-10OCT25"
        Public Property FuturesIsInverse As Boolean = True
        Public Property ExpiryUtc As DateTime

        ' Inputs
        Public Property EntryThreshold As Decimal = 0.0025D
        Public Property TargetUsd As Decimal = 0D
        Public Property TargetBtc As Decimal = 0D
        Public Property UseUsdInput As Boolean = True
        Public Property MaxSlippageBps As Decimal = 5D

        ' Instrument specs
        Private _spotTick As Decimal = 0.01D
        Private _spotMinAmount As Decimal = 0.0001D
        Private _spotAmountStep As Decimal = 0.0001D
        Private _futTick As Decimal = 0.5D

        ' State
        Private ReadOnly _openOrderIds As New HashSet(Of String)()
        Private ReadOnly _lock As New Object()

        ' Futures-first tracking
        Private _pendingFutContracts As Integer = 0
        Private _hedgeWatchTsUtc As DateTime = Date.MinValue
        Private ReadOnly _hedgeTimeoutSeconds As Integer = 10
        Private _lastFutOrderId As String = Nothing

        ' Re-quote controls (publicly tunable from UI)
        Private _requoteCts As CancellationTokenSource = Nothing
        Private _requoteMinTicksBacking As Integer = 2
        Private _requoteIntervalMsBacking As Integer = 300
        Private _requoteMaxTicks As Integer = 3   ' keep upper bound to reduce churn

        ' Fill monitor (futures) to hedge even if private trades are delayed
        Private _fillMonCts As CancellationTokenSource = Nothing
        Private _lastObservedFilledContracts As Integer = 0

        ' Track contracts seen from per-order trades to compute deltas robustly
        Private _lastObservedOrderTradeContracts As Integer = 0

        'For setting the flag when there is an active hedge
        Private _isActive As Boolean = False
        Public Event ActiveChanged(isActive As Boolean)

        ' Live spot hedge size in BTC (no residual policy for entry; used for close)
        Private _openSpotHedgeBtc As Decimal = 0D

        ' Close-side re-quote
        Private _closeRequoteCts As CancellationTokenSource = Nothing
        Private _lastCloseOrderId As String = Nothing


        ' Is the fill monitor running now?
        Private ReadOnly Property IsFillMonitorActive As Boolean
            Get
                Return _fillMonCts IsNot Nothing AndAlso Not _fillMonCts.IsCancellationRequested
            End Get
        End Property


        Public ReadOnly Property IsActive As Boolean
            Get
                Return _isActive
            End Get
        End Property

        Private Sub SetActive(active As Boolean)
            If _isActive <> active Then
                _isActive = active
                RaiseEvent ActiveChanged(_isActive)
            End If
        End Sub


        Public Enum SpotHedgeRounding
            DownToStep = 0
            NearestStep = 1
        End Enum

        Public Property HedgeRounding As SpotHedgeRounding = SpotHedgeRounding.DownToStep

        ' Expose spot increment info for UI display if needed
        Public ReadOnly Property SpotMinAmount As Decimal
            Get
                Return _spotMinAmount
            End Get
        End Property

        Public ReadOnly Property SpotAmountStep As Decimal
            Get
                Return _spotAmountStep
            End Get
        End Property


        Public Sub New(api As DeribitApiClient, db As ContangoDatabase, monitor As ContangoBasisMonitor)
            _api = api
            _db = db
            _monitor = monitor
            AddHandler _api.OrderUpdate, AddressOf OnOrderUpdate
            AddHandler _api.TradeUpdate, AddressOf OnTradeUpdate
        End Sub

        ' ============ Instrument discovery/specs ============

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
                                bestExp = expUtc : bestName = name
                            End If
                        Else
                            If expUtc < bestExp Then
                                bestExp = expUtc : bestName = name
                            End If
                        End If
                    End If
                End If
            Next

            If bestName Is Nothing Then Throw New ApplicationException("No suitable weekly inverse BTC future found")
            FuturesInstrument = bestName
            ExpiryUtc = bestExp
            _monitor.WeeklyInstrument = bestName
            _monitor.WeeklyExpiryUtc = bestExp
            Await RefreshInstrumentSpecsAsync()
        End Function

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

        ' ============ Helpers ============

        Public Function ComputeContractsFromUsd(usdNotional As Decimal) As Integer
            Dim contracts As Integer = CInt(Math.Round(usdNotional / 10D, MidpointRounding.AwayFromZero))
            If contracts < 1 Then contracts = 1
            Return contracts
        End Function

        Public Function ComputeBtcFromContracts(contracts As Integer, fillPriceUsd As Decimal) As Decimal
            If fillPriceUsd <= 0 Then Return 0D
            Return (contracts * 10D) / fillPriceUsd
        End Function

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

        Private Function RoundToTick(px As Decimal, tick As Decimal) As Decimal
            If tick <= 0D Then Return px
            Return Math.Round(px / tick, MidpointRounding.AwayFromZero) * tick
        End Function

        Private Function RoundDownToStep(amount As Decimal, stepVal As Decimal) As Decimal
            If stepVal <= 0D Then Return amount
            Dim steps = Math.Floor(amount / stepVal)
            Return steps * stepVal
        End Function

        Private Sub TrackOrder(orderObj As JObject)
            Dim oid = orderObj.Value(Of JObject)("order")?.Value(Of String)("order_id")
            If Not String.IsNullOrEmpty(oid) Then
                SyncLock _lock
                    _openOrderIds.Add(oid)
                End SyncLock
            End If
        End Sub

        'To help restrict input sizes to meaningful minimums
        Private Function MinContractsForOneSpotStep(futRefPx As Decimal) As Integer
            If futRefPx <= 0D Then Return 1
            Return CInt(Math.Ceiling((_spotMinAmount * futRefPx) / 10D))
        End Function

        Public Function MinUsdForOneSpotStep(Optional futRefPx As Decimal = 0D) As Decimal
            Dim ref = futRefPx
            If ref <= 0D Then ref = If(_monitor.WeeklyFutureBestBid > 0D, _monitor.WeeklyFutureBestBid, _monitor.WeeklyFutureMark)
            If ref <= 0D Then Return 10D
            Return CDec(Math.Ceiling((_spotMinAmount * ref) / 10D) * 10D)
        End Function

        Private Function RoundUsdToValidContracts(usd As Decimal, futRefPx As Decimal) As (usdRounded As Decimal, contracts As Integer)
            ' Force 10-USD multiples and minimum contracts to achieve one spot step
            Dim k = ComputeContractsFromUsd(usd)
            Dim minK = MinContractsForOneSpotStep(futRefPx)
            If k < minK Then k = minK
            Dim usdOut = k * 10D
            Return (usdOut, k)
        End Function


        ' ============ Entry: futures first, spot on fills ============

        Public Async Function EnterBasisAsync(indexPrice As Decimal,
                                      futBestBid As Decimal,
                                      spotBestAsk As Decimal) As Task
            ' Block concurrent entries
            SyncLock _lock
                If _pendingFutContracts > 0 OrElse Not String.IsNullOrEmpty(_lastFutOrderId) Then
                    Throw New ApplicationException("An active hedge is in progress; wait until it completes or cancel it before entering again.")
                End If
            End SyncLock

            Dim inputs = NormalizeInputs(indexPrice)
            Dim usdN As Decimal = inputs.targetUsd

            Dim basis = _monitor.BasisMid
            If basis < EntryThreshold Then
                Throw New ApplicationException($"Basis {basis:P2} below threshold {EntryThreshold:P2}")
            End If

            Await RefreshInstrumentSpecsAsync()

            Dim bid = If(_monitor.WeeklyFutureBestBid > 0D, _monitor.WeeklyFutureBestBid, _monitor.WeeklyFutureMark)
            If bid <= 0D Then Throw New ApplicationException("Futures price unavailable")
            Dim pxF = RoundToTick(bid, _futTick)

            ' Snap USD to valid contracts so the later spot hedge can be a single valid increment
            Dim sized = RoundUsdToValidContracts(usdN, pxF)
            usdN = sized.usdRounded
            Dim contracts As Integer = sized.contracts
            Dim amountUsd As Integer = contracts * 10  ' Deribit inverse futures: 10 USD per contract

            Dim futOrder = Await _api.PlaceOrderAsync(
    instrument:=FuturesInstrument,
    side:="sell",
    amount:=amountUsd,              ' USD notional required for futures
    price:=pxF,
    orderType:="limit",
    tif:="good_til_cancelled",
    postOnly:=True,
    reduceOnly:=False,
    label:=$"ContangoFutShort_{DateTime.UtcNow:HHmmss}"
  )
            TrackOrder(futOrder)

            ' Use exchange-acknowledged price for logging to match the platform
            Dim ackPx As Decimal = pxF
            Try
                Dim o = futOrder?("order")?.Value(Of JObject)()
                _lastFutOrderId = o?.Value(Of String)("order_id")
                Dim oPx = o?.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                If oPx > 0D Then ackPx = oPx
            Catch
                _lastFutOrderId = _lastFutOrderId
            End Try

            SyncLock _lock
                _pendingFutContracts = contracts
                _hedgeWatchTsUtc = DateTime.UtcNow
            End SyncLock

            SetActive(True)

            StartHedgeFallbackTimer()
            StartRequoteLoop()
            StartFillMonitorLoop()

            RaiseEvent Info($"Futures post_only placed at {ackPx:0.00}, contracts={contracts}, order_id={_lastFutOrderId}")
            RaiseEvent Info("Submitted futures post_only; re-quote active until fills trigger spot IOC hedges.")
        End Function


        ' Futures private order updates (open/filled/cancelled) for cleanup
        Private Sub OnOrderUpdate(currency As String, payload As JObject)
            Dim orderId = payload.Value(Of String)("order_id")
            If String.IsNullOrEmpty(orderId) Then Return
            Dim state = payload.Value(Of String)("state")
            If String.IsNullOrEmpty(state) Then state = payload.Value(Of String)("order_state")
            If String.IsNullOrEmpty(state) Then Return

            ' Entry-side futures order lifecycle
            If orderId = _lastFutOrderId AndAlso (state = "filled" OrElse state = "cancelled" OrElse state = "rejected") Then
                StopRequoteLoop()
                StopFillMonitorLoop()
                SyncLock _lock
                    _pendingFutContracts = 0
                    _hedgeWatchTsUtc = Date.MinValue
                End SyncLock
                _lastFutOrderId = Nothing
                SetActive(False)
                RaiseEvent Info($"Futures order {orderId} {state}; position lifecycle closed.")
            End If

            ' Close-side futures order lifecycle
            If orderId = _lastCloseOrderId AndAlso (state = "filled" OrElse state = "cancelled" OrElse state = "rejected") Then
                StopCloseRequoteLoop()
                _lastCloseOrderId = Nothing
                RaiseEvent Info($"Close order {orderId} {state}; close re-quote loop stopped.")
            End If

            SyncLock _lock
                If _openOrderIds.Contains(orderId) AndAlso (state = "filled" OrElse state = "cancelled" OrElse state = "rejected") Then
                    _openOrderIds.Remove(orderId)
                End If
            End SyncLock
        End Sub


        ' Futures fills -> buy spot IOC; Spot fills -> persist
        ' Normalize and persist trades; maintain live spot hedge balance for Close Monitor
        Private Sub OnTradeUpdate(currency As String, payload As JObject)
            Try
                Dim instr = payload.Value(Of String)("instrument_name")
                Dim trades = payload("trades")
                If trades Is Nothing OrElse trades.Type <> JTokenType.Array OrElse trades.Count = 0 Then Return

                For Each t In trades
                    Dim sideStr = t.Value(Of String)("side")
                    If String.IsNullOrEmpty(sideStr) Then sideStr = t.Value(Of String)("direction")
                    Dim px As Decimal = t.Value(Of Decimal?)("price").GetValueOrDefault(0D)

                    ' Compute futures contracts if present, else derive from USD amount
                    Dim contracts = t.Value(Of Integer?)("contracts").GetValueOrDefault(0)
                    If contracts = 0 Then
                        Dim amtUsd = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                        If amtUsd > 0D Then contracts = CInt(Math.Round(amtUsd / 10D, MidpointRounding.AwayFromZero))
                    End If

                    Dim amtBtc As Decimal = 0D
                    Dim isSpot As Boolean = String.Equals(instr, SpotInstrument, StringComparison.OrdinalIgnoreCase)

                    If isSpot Then
                        ' For spot, Deribit "amount" is base currency
                        amtBtc = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                    End If

                    ' Persist trade
                    _db.SaveTrade(
        t.Value(Of String)("trade_id"),
        DateTime.UtcNow,
        If(String.IsNullOrEmpty(sideStr), "", sideStr),
        instr,
        If(isSpot, CType(amtBtc, Decimal?), Nothing),
        If(Not isSpot AndAlso contracts > 0, CType(contracts, Integer?), Nothing),
        px,
        t.Value(Of String)("fee_currency"),
        t.Value(Of Decimal?)("fee").GetValueOrDefault(0D)
      )

                    ' Maintain live spot hedge so CloseMonitor can unwind after futures is flat
                    If isSpot AndAlso amtBtc > 0D AndAlso Not String.IsNullOrEmpty(sideStr) Then
                        If sideStr.Equals("buy", StringComparison.OrdinalIgnoreCase) Then
                            _openSpotHedgeBtc += amtBtc
                        ElseIf sideStr.Equals("sell", StringComparison.OrdinalIgnoreCase) Then
                            _openSpotHedgeBtc = Math.Max(0D, _openSpotHedgeBtc - amtBtc)
                        End If
                    End If
                Next

            Catch
                ' ignore and continue
            End Try
        End Sub


        ' ============ Re-quote loop ============

        Private Sub StartRequoteLoop()
            Try
                StopRequoteLoop()
                _requoteCts = New CancellationTokenSource()
                Task.Run(Function() RequoteLoopAsync(_requoteCts.Token))
            Catch
            End Try
        End Sub

        Private Sub StopRequoteLoop()
            Try
                _requoteCts?.Cancel()
            Catch
            End Try
        End Sub

        Private Async Function RequoteLoopAsync(ct As Threading.CancellationToken) As Task
            While Not ct.IsCancellationRequested
                Dim doDelay As Boolean = False
                Try
                    If String.IsNullOrEmpty(_lastFutOrderId) Then Exit While

                    ' Pull current order state
                    Dim cur = Await _api.GetOrderStateAsync(_lastFutOrderId)
                    If cur Is Nothing Then
                        doDelay = True
                    Else
                        Dim ordState As String = cur.Value(Of String)("order_state")
                        If String.IsNullOrEmpty(ordState) Then ordState = cur.Value(Of String)("state")
                        If ordState = "filled" OrElse ordState = "cancelled" OrElse ordState = "rejected" Then
                            Exit While
                        End If

                        ' Compute current target bid
                        Dim bestBid = If(_monitor.WeeklyFutureBestBid > 0D, _monitor.WeeklyFutureBestBid, _monitor.WeeklyFutureMark)
                        If bestBid <= 0D Then
                            doDelay = True
                        Else
                            Dim targetPx = RoundToTick(bestBid, _futTick)
                            Dim curPx As Decimal = cur?.Value(Of Decimal?)("price").GetValueOrDefault(0D)

                            ' Tick delta vs. current resting
                            Dim tickSteps As Integer = 0
                            If _futTick > 0D AndAlso curPx > 0D Then
                                tickSteps = CInt(Math.Round((targetPx - curPx) / _futTick, MidpointRounding.AwayFromZero))
                            End If

                            ' Only edit/repost if improvement within guard band
                            'If Math.Abs(tickSteps) >= _requoteMinTicks AndAlso Math.Abs(tickSteps) <= _requoteMaxTicks Then
                            If Math.Abs(tickSteps) >= RequoteMinTicks AndAlso Math.Abs(tickSteps) <= 3 Then
                                Dim edited As Boolean = False

                                ' Try in-place edit first to preserve queue (post_only + limit)
                                Try
                                    Dim editRes = Await _api.EditOrderAsync(_lastFutOrderId, targetPx, Nothing, True)
                                    edited = True
                                    Dim eordPx As Decimal = targetPx
                                    Try
                                        Dim eord = editRes?("order")?.Value(Of JObject)()
                                        Dim pxAck = eord?.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                                        If pxAck > 0D Then eordPx = pxAck
                                    Catch
                                    End Try
                                    RaiseEvent Info($"Re-quote edit: price -> {eordPx:0.00}")
                                Catch
                                    edited = False
                                End Try

                                ' If edit failed (crossing risk or edit not allowed), cancel+repost
                                If Not edited Then
                                    Try
                                        Await _api.CancelOrderAsync(_lastFutOrderId)

                                        ' Use USD notional for futures amount
                                        Dim amtUsd As Integer
                                        SyncLock _lock
                                            amtUsd = Math.Max(1, _pendingFutContracts) * 10
                                        End SyncLock

                                        Dim repost = Await _api.PlaceOrderAsync(
                  instrument:=FuturesInstrument,
                  side:="sell",
                  amount:=amtUsd,           ' USD notional
                  price:=targetPx,
                  orderType:="limit",
                  tif:="good_til_cancelled",
                  postOnly:=True,
                  reduceOnly:=False,
                  label:=$"ContangoFutRequote_{DateTime.UtcNow:HHmmss}"
                )
                                        Dim ro = repost?("order")?.Value(Of JObject)()
                                        Dim newId = ro?.Value(Of String)("order_id")
                                        If Not String.IsNullOrEmpty(newId) Then _lastFutOrderId = newId

                                        Dim roPx As Decimal = targetPx
                                        Try
                                            Dim pxAck = ro?.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                                            If pxAck > 0D Then roPx = pxAck
                                        Catch
                                        End Try

                                        RaiseEvent Info($"Re-quote repost: price -> {roPx:0.00}, id={_lastFutOrderId}")
                                    Catch
                                        ' ignore; pace and retry
                                    End Try
                                End If

                            Else
                                doDelay = True
                            End If
                        End If
                    End If

                Catch ex As TaskCanceledException
                    Exit While
                Catch
                    doDelay = True
                End Try

                If doDelay Then
                    Try
                        Await Task.Delay(RequoteIntervalMs, ct)
                    Catch
                        Exit While
                    End Try
                End If
            End While
        End Function



        Public Property RequoteMinTicks As Integer
            Get
                Return _requoteMinTicksBacking
            End Get
            Set(value As Integer)
                ' clamp to [1..10]; default 2
                If value < 1 Then value = 1
                If value > 10 Then value = 10
                _requoteMinTicksBacking = value
            End Set
        End Property

        Public Property RequoteIntervalMs As Integer
            Get
                Return _requoteIntervalMsBacking
            End Get
            Set(value As Integer)
                ' clamp to [100..5000] ms; default 300
                If value < 100 Then value = 100
                If value > 5000 Then value = 5000
                _requoteIntervalMsBacking = value
            End Set
        End Property

        Private Sub StartFillMonitorLoop()
            Try
                StopFillMonitorLoop()
                _fillMonCts = New CancellationTokenSource()
                _lastObservedFilledContracts = 0
                _lastObservedOrderTradeContracts = 0
                Task.Run(Function() FillMonitorLoopAsync(_fillMonCts.Token))
            Catch
            End Try
        End Sub



        Private Sub StopFillMonitorLoop()
            Try
                _fillMonCts?.Cancel()
            Catch
            End Try
        End Sub

        Private Async Function FillMonitorLoopAsync(ct As Threading.CancellationToken) As Task
            While Not ct.IsCancellationRequested
                Dim doDelay As Boolean = False
                Try
                    If String.IsNullOrEmpty(_lastFutOrderId) Then Exit While

                    ' 1) Fetch order state
                    Dim st = Await _api.GetOrderStateAsync(_lastFutOrderId)
                    If st Is Nothing Then
                        doDelay = True
                    Else
                        Dim ordState As String = st.Value(Of String)("order_state")
                        If String.IsNullOrEmpty(ordState) Then ordState = st.Value(Of String)("state")

                        ' 2) Try trades-by-order first for robust deltas and VWAP
                        Dim tradesArr As JArray = Nothing
                        Try
                            tradesArr = Await _api.GetUserTradesByOrderAsync(_lastFutOrderId)
                        Catch
                            tradesArr = Nothing
                        End Try

                        Dim totalContractsFromTrades As Integer = 0
                        Dim sliceContracts As Integer = 0
                        Dim sliceVwap As Decimal = 0D

                        If tradesArr IsNot Nothing AndAlso tradesArr.Count > 0 Then
                            ' Sum contracts and compute VWAP for newly observed slice
                            Dim totalC As Integer = 0
                            Dim sumPxAmt As Decimal = 0D
                            Dim sumAmt As Integer = 0

                            For Each t In tradesArr
                                Dim c = t.Value(Of Integer?)("contracts").GetValueOrDefault(0)
                                If c = 0 Then
                                    Dim amtUsd = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                                    If amtUsd > 0D Then c = CInt(Math.Round(amtUsd / 10D, MidpointRounding.AwayFromZero))
                                End If
                                totalC += c
                            Next

                            totalContractsFromTrades = totalC
                            sliceContracts = Math.Max(0, totalContractsFromTrades - _lastObservedOrderTradeContracts)

                            If sliceContracts > 0 Then
                                ' Compute slice VWAP only over the newly added trades
                                Dim toSkip = _lastObservedOrderTradeContracts
                                Dim taken As Integer = 0
                                For Each t In tradesArr
                                    Dim c = t.Value(Of Integer?)("contracts").GetValueOrDefault(0)
                                    If c = 0 Then
                                        Dim amtUsd = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                                        If amtUsd > 0D Then c = CInt(Math.Round(amtUsd / 10D, MidpointRounding.AwayFromZero))
                                    End If
                                    Dim px = t.Value(Of Decimal?)("price").GetValueOrDefault(0D)

                                    If toSkip > 0 Then
                                        toSkip -= c
                                        If toSkip < 0 Then
                                            Dim partialContracts As Integer = Math.Min(c, -toSkip)
                                            If px > 0D AndAlso partialContracts > 0 Then
                                                sumPxAmt += px * partialContracts
                                                sumAmt += partialContracts
                                                taken += partialContracts
                                            End If
                                        End If
                                    Else
                                        If px > 0D AndAlso c > 0 Then
                                            sumPxAmt += px * c
                                            sumAmt += c
                                            taken += c
                                        End If
                                    End If

                                    If taken >= sliceContracts Then Exit For
                                Next
                                If sumAmt > 0 Then sliceVwap = sumPxAmt / sumAmt

                            End If
                        End If

                        ' 3) If trades slice exists, hedge from slice
                        If sliceContracts > 0 AndAlso sliceVwap > 0D Then
                            RaiseEvent Info($"poll: trades delta={sliceContracts} vwap={sliceVwap:0.00}")
                            SyncLock _lock
                                _pendingFutContracts = Math.Max(0, _pendingFutContracts - sliceContracts)
                            End SyncLock
                            Await PlaceSpotIOCForContractsAsync(sliceContracts, sliceVwap)
                            _lastObservedOrderTradeContracts += sliceContracts

                            ' Update filled-contracts tracker as well (best effort)
                            Dim filledNowFromTrades = _lastObservedOrderTradeContracts
                            If filledNowFromTrades > _lastObservedFilledContracts Then
                                _lastObservedFilledContracts = filledNowFromTrades
                            End If

                            ' If completely filled, stop on terminal state
                            'If String.Equals(ordState, "filled", StringComparison.OrdinalIgnoreCase) AndAlso _pendingFutContracts > 0 Then
                            ' SyncLock _lock
                            '_pendingFutContracts = Math.Max(0, _pendingFutContracts - sliceContracts)
                            'End SyncLock
                            'End If

                        Else
                            ' 4) Fallback: derive deltas from order state if trades are not available yet
                            Dim filledNow As Integer = st.Value(Of Integer?)("filled_contracts").GetValueOrDefault(0)
                            If filledNow = 0 Then
                                Dim filledAmtUsd2 = st.Value(Of Decimal?)("filled_amount").GetValueOrDefault(0D)
                                If filledAmtUsd2 > 0D Then
                                    filledNow = CInt(Math.Round(filledAmtUsd2 / 10D, MidpointRounding.AwayFromZero))
                                End If
                            End If

                            Dim avgPxOrder As Decimal = st.Value(Of Decimal?)("average_price").GetValueOrDefault(0D)
                            Dim deltaInc As Integer = Math.Max(0, filledNow - _lastObservedFilledContracts)

                            If deltaInc > 0 AndAlso avgPxOrder > 0D Then
                                RaiseEvent Info($"poll: state delta={deltaInc} avg={avgPxOrder:0.00}")
                                SyncLock _lock
                                    _pendingFutContracts = Math.Max(0, _pendingFutContracts - deltaInc)
                                End SyncLock
                                Await PlaceSpotIOCForContractsAsync(deltaInc, avgPxOrder)
                                _lastObservedFilledContracts = filledNow
                            End If
                        End If

                        ' 5) Stop conditions on terminal order state
                        If ordState = "filled" OrElse ordState = "cancelled" OrElse ordState = "rejected" Then
                            ' Terminal guard: if fully filled (no pending), or cancelled/rejected,
                            ' release the UI immediately even if private order update is delayed.
                            If (String.Equals(ordState, "filled", StringComparison.OrdinalIgnoreCase) AndAlso _pendingFutContracts = 0) _
     OrElse ordState = "cancelled" _
     OrElse ordState = "rejected" Then
                                _lastFutOrderId = Nothing
                                SetActive(False)
                                ' Defensive: ensure re-quote is not left running (usually already stopped)
                                StopRequoteLoop()
                            End If
                            Exit While
                        End If


                        doDelay = True
                    End If

                Catch ex As TaskCanceledException
                    Exit While
                Catch
                    doDelay = True
                End Try

                If doDelay Then
                    Try
                        Await Task.Delay(RequoteIntervalMs, ct)
                    Catch
                        Exit While
                    End Try
                End If
            End While
        End Function



        Private Async Function PlaceSpotIOCForContractsAsync(newDeltaContracts As Integer, refPx As Decimal) As Task
            If newDeltaContracts <= 0 OrElse refPx <= 0D Then Return

            ' Convert inverse futures contracts to BTC
            Dim btc As Decimal = (newDeltaContracts * 10D) / refPx

            ' Snap to exchange increments with no residual carry
            Await RefreshInstrumentSpecsAsync()
            Dim amt As Decimal = RoundDownToStep(btc, _spotAmountStep)
            If amt < _spotMinAmount Then
                RaiseEvent Info($"hedge: computed {btc:0.########} BTC < min {_spotMinAmount}; skipped (market_limit)")
                Return
            End If

            ' Market‑Limit: taker execute now, remainder rests at exec price; no price sent
            Dim spotOrder = Await _api.PlaceOrderAsync(
    instrument:=SpotInstrument,
    side:="buy",
    amount:=amt,
    orderType:="market_limit",
    tif:="good_til_cancelled",
    label:="ContangoSpotML_Enter"
  )
            TrackOrder(spotOrder)

            ' NEW: proactively accrue live hedge so CloseMonitor can unwind even if TradeUpdate lags
            _openSpotHedgeBtc += amt

            RaiseEvent Info($"Spot market_limit hedge placed amt={amt:0.########} (no residual mode)")
        End Function




        ' ============ Watchdog: cancel unfilled futures on timeout or decay ============

        Private Sub StartHedgeFallbackTimer()
            Try
                Task.Run(AddressOf HedgeFallbackTimerAsync)
            Catch
            End Try
        End Sub

        Private Async Function HedgeFallbackTimerAsync() As Task
            Try
                Await Task.Delay(_hedgeTimeoutSeconds * 1000).ConfigureAwait(False)
                Await HedgeFallbackAsync().ConfigureAwait(False)
            Catch
            End Try
        End Function

        Private Async Function HedgeFallbackAsync() As Task
            Dim cancelNow As Boolean = False
            Dim oid As String = Nothing
            Dim basisNow As Decimal = _monitor.BasisMid

            SyncLock _lock
                If _pendingFutContracts > 0 AndAlso _hedgeWatchTsUtc <> Date.MinValue Then
                    Dim elapsed = (DateTime.UtcNow - _hedgeWatchTsUtc).TotalSeconds
                    If elapsed >= _hedgeTimeoutSeconds OrElse basisNow < Math.Max(0D, EntryThreshold - 0.0005D) Then
                        cancelNow = True
                        oid = _lastFutOrderId
                        _hedgeWatchTsUtc = Date.MinValue
                    End If
                End If
            End SyncLock

            If cancelNow AndAlso Not String.IsNullOrEmpty(oid) Then
                Try
                    Await _api.CancelOrderAsync(oid)
                    RaiseEvent Info($"Watchdog: cancelled resting futures order id={oid}")
                    StopFillMonitorLoop()
                    StopCloseMonitorLoop()

                Catch
                End Try
                StopRequoteLoop()
                SyncLock _lock
                    _pendingFutContracts = 0
                    _hedgeWatchTsUtc = Date.MinValue
                End SyncLock
                _lastFutOrderId = Nothing
                SetActive(False)
            End If

        End Function

        ' ============ Admin ops ============

        Public Async Function RollToNextWeeklyAsync(indexPrice As Decimal) As Task
            If String.IsNullOrEmpty(FuturesInstrument) Then Throw New ApplicationException("No active weekly instrument")

            ' Close current short via reduce_only LIMIT at best ask (maker)
            Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
            Dim curContracts As Integer = 0
            For Each p In posArr
                Dim o = p.Value(Of JObject)()
                If o.Value(Of String)("instrument_name") = FuturesInstrument Then
                    curContracts = Math.Abs(o.Value(Of Integer)("size"))
                    Exit For
                End If
            Next
            If curContracts > 0 Then
                Await RefreshInstrumentSpecsAsync()
                Dim ask = If(_monitor.WeeklyFutureBestAsk > 0D, _monitor.WeeklyFutureBestAsk, _monitor.WeeklyFutureMark)
                If ask <= 0D Then ask = _monitor.WeeklyFutureMark
                Dim pxAsk = RoundToTick(ask, _futTick)

                Dim closeBuy = Await _api.PlaceOrderAsync(
  instrument:=FuturesInstrument,
  side:="buy",
  amount:=curContracts,              ' <-- USD notional
  price:=pxAsk,
  orderType:="limit",
  tif:="good_til_cancelled",
  postOnly:=True,
  reduceOnly:=True,
  label:="ContangoRollClose"
)

                TrackOrder(closeBuy)
                Dim o = closeBuy?("order")?.Value(Of JObject)()
                _lastCloseOrderId = o?.Value(Of String)("order_id")
                StartCloseRequoteLoop()
                StartCloseMonitorLoop()
            End If

            ' Pick next weekly beyond current expiry
            Dim curExp = ExpiryUtc
            Await DiscoverNearestWeeklyAsync(curExp)
        End Function



        Public Async Function CloseAllAsync() As Task
            Try
                ' 1) Read current futures position size for the selected weekly (USD notional for inverse)
                Dim amtUsdShort As Integer = 0
                Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
                For Each p In posArr
                    Dim o = p.Value(Of JObject)()
                    If o.Value(Of String)("instrument_name") = FuturesInstrument Then
                        Dim sz = o.Value(Of Integer)("size")   ' inverse: negative for short, positive for long, unit ~= USD notional blocks
                        If sz < 0 Then amtUsdShort = Math.Abs(sz)
                        Exit For
                    End If
                Next

                ' 2) Place reduce_only buy LIMIT at best ask (rounded) if short exists
                If amtUsdShort > 0 Then
                    Await RefreshInstrumentSpecsAsync()
                    Dim ask = If(_monitor.WeeklyFutureBestAsk > 0D, _monitor.WeeklyFutureBestAsk, _monitor.WeeklyFutureMark)
                    If ask <= 0D Then ask = _monitor.WeeklyFutureMark
                    Dim pxAsk = RoundToTick(ask, _futTick)

                    Dim closeBuy = Await _api.PlaceOrderAsync(
        instrument:=FuturesInstrument,
        side:="buy",
        amount:=amtUsdShort,                 ' USD notional
        price:=pxAsk,
        orderType:="limit",
        tif:="good_til_cancelled",
        postOnly:=True,
        reduceOnly:=True,
        label:="ContangoClose_ROL"
      )
                    TrackOrder(closeBuy)

                    ' Log exchange-acknowledged price to match platform
                    Dim ackPx As Decimal = pxAsk
                    Dim ackId As String = Nothing
                    Try
                        Dim o = closeBuy?("order")?.Value(Of JObject)()
                        ackId = o?.Value(Of String)("order_id")
                        Dim pxa = o?.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                        If pxa > 0D Then ackPx = pxa
                    Catch
                    End Try
                    _lastCloseOrderId = ackId

                    RaiseEvent Info($"CloseAll: futures reduce_only LIMIT sent px={ackPx:0.00} contracts={amtUsdShort} id={_lastCloseOrderId}")

                    ' 3) Start close re-quote and monitor for spot unwind
                    StartCloseRequoteLoop()
                    StartCloseMonitorLoop()

                Else
                    RaiseEvent Info("CloseAll: no short futures position detected.")
                End If

            Catch ex As Exception
                RaiseEvent Info("CloseAll error: " & ex.Message)
            End Try

            ' Keep entry-side loops off and UI released while the close runs
            StopRequoteLoop()
            StopFillMonitorLoop()
            SyncLock _lock
                _pendingFutContracts = 0
                _hedgeWatchTsUtc = Date.MinValue
            End SyncLock
            _lastFutOrderId = Nothing
            SetActive(False)
        End Function





        Private Sub StartCloseRequoteLoop()
            Try
                StopCloseRequoteLoop()
                If String.IsNullOrEmpty(_lastCloseOrderId) Then Return
                _closeRequoteCts = New CancellationTokenSource()
                Task.Run(Function() CloseRequoteLoopAsync(_closeRequoteCts.Token))
            Catch
            End Try
        End Sub

        Private Sub StopCloseRequoteLoop()
            Try
                _closeRequoteCts?.Cancel()
            Catch
            End Try
        End Sub

        Private Async Function CloseRequoteLoopAsync(ct As Threading.CancellationToken) As Task
            While Not ct.IsCancellationRequested
                Dim doDelay As Boolean = False
                Try
                    If String.IsNullOrEmpty(_lastCloseOrderId) Then Exit While

                    ' Pull current order state
                    Dim cur = Await _api.GetOrderStateAsync(_lastCloseOrderId)
                    If cur Is Nothing Then
                        doDelay = True
                    Else
                        Dim ordState As String = cur.Value(Of String)("order_state")
                        If String.IsNullOrEmpty(ordState) Then ordState = cur.Value(Of String)("state")
                        If ordState = "filled" OrElse ordState = "cancelled" OrElse ordState = "rejected" Then
                            Exit While
                        End If

                        ' Compute current target ask
                        Dim bestAsk = If(_monitor.WeeklyFutureBestAsk > 0D, _monitor.WeeklyFutureBestAsk, _monitor.WeeklyFutureMark)
                        If bestAsk <= 0D Then
                            doDelay = True
                        Else
                            Dim targetPx = RoundToTick(bestAsk, _futTick)
                            Dim curPx As Decimal = cur?.Value(Of Decimal?)("price").GetValueOrDefault(0D)

                            ' Tick delta vs current resting
                            Dim tickSteps As Integer = 0
                            If _futTick > 0D AndAlso curPx > 0D Then
                                tickSteps = CInt(Math.Round((targetPx - curPx) / _futTick, MidpointRounding.AwayFromZero))
                            End If

                            ' Use public min guard and a local cap for max ticks
                            Dim maxTicks As Integer = 3
                            If Math.Abs(tickSteps) >= RequoteMinTicks AndAlso Math.Abs(tickSteps) <= maxTicks Then
                                Dim edited As Boolean = False

                                ' 1) Try in-place edit first (preserve queue)
                                Try
                                    Dim editRes = Await _api.EditOrderAsync(_lastCloseOrderId, targetPx, Nothing, True)
                                    edited = True

                                    ' Log exchange-acknowledged price
                                    Dim eordPx As Decimal = targetPx
                                    Try
                                        Dim eord = editRes?("order")?.Value(Of JObject)()
                                        Dim pxAck = eord?.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                                        If pxAck > 0D Then eordPx = pxAck
                                    Catch
                                    End Try
                                    RaiseEvent Info($"Close re-quote (edit): price -> {eordPx:0.00}")
                                Catch
                                    edited = False
                                End Try

                                ' 2) If edit failed, cancel + repost
                                If Not edited Then
                                    Try
                                        Await _api.CancelOrderAsync(_lastCloseOrderId)

                                        ' Amount: USD notional from current order state; safe unwrap
                                        Dim amtUsdVal As Decimal = cur?.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                                        Dim amtUsd As Integer = CInt(Math.Round(amtUsdVal, MidpointRounding.AwayFromZero))

                                        Dim repost = Await _api.PlaceOrderAsync(
                  instrument:=FuturesInstrument,
                  side:="buy",
                  amount:=amtUsd,           ' USD notional
                  price:=targetPx,
                  orderType:="limit",
                  tif:="good_til_cancelled",
                  postOnly:=True,
                  reduceOnly:=True,
                  label:=$"ContangoCloseRequote_{DateTime.UtcNow:HHmmss}"
                )
                                        Dim ro = repost?("order")?.Value(Of JObject)()
                                        Dim newId = ro?.Value(Of String)("order_id")
                                        If Not String.IsNullOrEmpty(newId) Then _lastCloseOrderId = newId

                                        ' Log exchange-acknowledged price from repost
                                        Dim roPx As Decimal = targetPx
                                        Try
                                            Dim pxAck = ro?.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                                            If pxAck > 0D Then roPx = pxAck
                                        Catch
                                        End Try
                                        RaiseEvent Info($"Close re-quote (repost): price -> {roPx:0.00}, id={_lastCloseOrderId}")
                                    Catch
                                        ' ignore; pace and retry
                                    End Try
                                End If

                            Else
                                doDelay = True
                            End If
                        End If
                    End If

                Catch ex As TaskCanceledException
                    Exit While
                Catch
                    doDelay = True
                End Try

                If doDelay Then
                    Try
                        Await Task.Delay(RequoteIntervalMs, ct)
                    Catch
                        Exit While
                    End Try
                End If
            End While
        End Function





        'Close monitor : when futures is flat and spot hedge exists, unwind spot

        Private _closeMonCts As Threading.CancellationTokenSource = Nothing

        Private Sub StartCloseMonitorLoop()
            Try
                StopCloseMonitorLoop()
                _closeMonCts = New Threading.CancellationTokenSource()
                Task.Run(Function() CloseMonitorLoopAsync(_closeMonCts.Token))
                RaiseEvent Info("CloseMonitor: started (watching futures to unwind spot).")
            Catch
            End Try
        End Sub

        Private Sub StopCloseMonitorLoop()
            Try
                _closeMonCts?.Cancel()
            Catch
            End Try
        End Sub

        Private Async Function CloseMonitorLoopAsync(ct As Threading.CancellationToken) As Task
            While Not ct.IsCancellationRequested
                Dim doDelay As Boolean = False
                Try
                    ' Query current size of the selected weekly future
                    Dim curSz As Integer = 0
                    Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
                    For Each p In posArr
                        Dim o = p.Value(Of JObject)()
                        If o.Value(Of String)("instrument_name") = FuturesInstrument Then
                            curSz = o.Value(Of Integer)("size")
                            Exit For
                        End If
                    Next

                    ' If futures is flat and there is spot hedge to unwind, send market_limit sell
                    If curSz = 0 AndAlso _openSpotHedgeBtc >= _spotMinAmount Then
                        Await RefreshInstrumentSpecsAsync()
                        Dim amt = RoundDownToStep(_openSpotHedgeBtc, _spotAmountStep)
                        If amt >= _spotMinAmount Then
                            Dim sell = Await _api.PlaceOrderAsync(
            instrument:=SpotInstrument,
            side:="sell",
            amount:=amt,
            orderType:="market_limit",
            tif:="good_til_cancelled",
            label:="ContangoSpotML_Close"
          )
                            TrackOrder(sell)
                            RaiseEvent Info($"CloseMonitor: Spot market_limit sell placed amt={amt:0.########}")
                        Else
                            RaiseEvent Info("CloseMonitor: spot amount below min_trade_amount; nothing to sell.")
                        End If
                        Exit While
                    End If

                    doDelay = True
                Catch ex As TaskCanceledException
                    Exit While
                Catch
                    doDelay = True
                End Try

                If doDelay Then
                    Try
                        Await Task.Delay(300, ct)
                    Catch
                        Exit While
                    End Try
                End If
            End While
        End Function


    End Class
End Namespace
