Imports Newtonsoft.Json.Linq

Namespace DeribitContango
    Public Class ContangoPositionManager
        Private ReadOnly _api As DeribitApiClient
        Private ReadOnly _db As ContangoDatabase
        Private ReadOnly _monitor As ContangoBasisMonitor

        ' Runtime state
        Public Property SpotInstrument As String = "BTC_USDC"
        Public Property FuturesInstrument As String = "" ' e.g., inverse weekly BTC: "BTC-27SEP25"
        Public Property FuturesIsInverse As Boolean = True ' Using inverse weekly with 10 USD/contract sizing
        Public Property ExpiryUtc As DateTime

        ' Inputs
        Public Property EntryThreshold As Decimal = 0.0025D ' 25 bps weekly
        Public Property TargetUsd As Decimal = 0D
        Public Property TargetBtc As Decimal = 0D
        Public Property UseUsdInput As Boolean = True
        Public Property MaxSlippageBps As Decimal = 5D

        ' Order tracking
        Private ReadOnly _openOrderIds As New HashSet(Of String)()
        Private ReadOnly _lock As New Object()

        Public Sub New(api As DeribitApiClient, db As ContangoDatabase, monitor As ContangoBasisMonitor)
            _api = api
            _db = db
            _monitor = monitor

            AddHandler _api.OrderUpdate, AddressOf OnOrderUpdate
            AddHandler _api.TradeUpdate, AddressOf OnTradeUpdate
        End Sub

        Public Async Function DiscoverNearestWeeklyAsync() As Task
            ' Get all BTC futures and choose nearest unexpired weekly inverse future (BTC-settled)
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
                    ' Keep the soonest expiry
                    If expUtc > DateTime.UtcNow AndAlso expUtc < bestExp Then
                        bestExp = expUtc
                        bestName = name
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
        End Function

        Public Function ComputeContractsFromUsd(usdNotional As Decimal, futPriceUsd As Decimal) As Integer
            ' Inverse weekly: 10 USD per contract
            Dim contracts As Integer = CInt(Math.Round(usdNotional / 10D, MidpointRounding.AwayFromZero))
            If contracts < 1 Then contracts = 1
            Return contracts
        End Function

        Public Function ComputeBtcFromContracts(contracts As Integer, fillPriceUsd As Decimal) As Decimal
            ' For inverse, BTC size from filled contracts at price P: BTC = (contracts * 10) / P
            If fillPriceUsd <= 0 Then Return 0D
            Return (contracts * 10D) / fillPriceUsd
        End Function

        Public Function NormalizeInputs(indexPrice As Decimal) As (targetUsd As Decimal, targetBtc As Decimal)
            If UseUsdInput Then
                Dim tBtc = If(indexPrice > 0D, TargetUsd / indexPrice, 0D)
                ' Sanity fit to at least 10 USD equivalent
                If TargetUsd < 10D Then Throw New ApplicationException("USD amount too small for 10 USD/contract granularity")
                Return (TargetUsd, tBtc)
            Else
                Dim tUsd = TargetBtc * indexPrice
                If tUsd < 10D Then Throw New ApplicationException("BTC amount too small for 10 USD/contract granularity")
                Return (tUsd, TargetBtc)
            End If
        End Function

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

            ' 1) Place post_only+GTC short on weekly inverse future near best bid
            Dim price = If(futBestBid > 0, futBestBid, _monitor.FutureMid)
            If price <= 0 Then Throw New ApplicationException("Future price unavailable")
            Dim contracts = ComputeContractsFromUsd(usdN, price)

            Dim futLabel = $"ContangoFutShort_{DateTime.UtcNow:HHmmss}"
            Dim futOrder = Await _api.PlaceOrderAsync(
              instrument:=FuturesInstrument,
              side:="sell",
              amount:=Nothing,
              contracts:=contracts,
              price:=price,
              orderType:="limit",
              tif:="good_til_cancelled",
              postOnly:=True,
              reduceOnly:=False,
              label:=futLabel
            )
            TrackOrder(futOrder)

            ' 2) Wait for fills via events; on fill, sweep spot BTC_USDC IOC marketable limit
            ' The actual execution is driven by OnTradeUpdate accumulating fills and then hedging spot
        End Function

        Public Async Function RollToNextWeeklyAsync(indexPrice As Decimal) As Task
            ' Close current future reduce_only, then open next-week short
            If String.IsNullOrEmpty(FuturesInstrument) Then Throw New ApplicationException("No active weekly instrument")
            ' Fetch next weekly first to avoid gaps
            Dim current = FuturesInstrument
            Await DiscoverNearestWeeklyAsync()
            Dim nextInstr = FuturesInstrument

            ' Step 1: Close current inverse short: buy reduce_only IOC
            ' Query current position size for this instrument
            Dim posArr = Await _api.GetPositionsAsync("BTC", "future")
            Dim curContracts As Integer = 0
            For Each p In posArr
                Dim o = p.Value(Of JObject)()
                If o.Value(Of String)("instrument_name") = current Then
                    curContracts = Math.Abs(o.Value(Of Integer)("size"))
                End If
            Next
            If curContracts > 0 Then
                Dim closeBuy = Await _api.PlaceOrderAsync(
                  instrument:=current,
                  side:="buy",
                  amount:=Nothing,
                  contracts:=curContracts,
                  price:=Nothing,
                  orderType:="market",
                  tif:="immediate_or_cancel",
                  postOnly:=Nothing,
                  reduceOnly:=True,
                  label:="ContangoRollClose"
                )
                TrackOrder(closeBuy)
            End If

            ' Step 2: Open next short (post_only GTC); price will be handled by caller via latest book
            ' This can be triggered by UI calling EnterBasisAsync after DiscoverNearestWeeklyAsync
        End Function

        Public Async Function CloseAllAsync() As Task
            ' Best-effort: cancel resting futures, close any inverse futures using reduce_only,
            ' and unwind spot BTC_USDC
            Try
                ' Futures
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
            ' Spot unwind is optional for a basis close if spot is net long; caller can execute sell on BTC_USDC as needed
        End Function

        Private Sub TrackOrder(orderObj As JObject)
            Dim oid = orderObj.Value(Of JObject)("order")?.Value(Of String)("order_id")
            If Not String.IsNullOrEmpty(oid) Then
                SyncLock _lock
                    _openOrderIds.Add(oid)
                End SyncLock
            End If
        End Sub

        ' Handle order lifecycle
        Private Sub OnOrderUpdate(currency As String, payload As JObject)
            ' payload has fields including order_id, state, filled_amount/filled_contracts, instrument_name
            Dim orderId = payload.Value(Of String)("order_id")
            Dim state = payload.Value(Of String)("state")
            If String.IsNullOrEmpty(orderId) Then Return

            Dim isOpenTracked As Boolean = False
            SyncLock _lock
                isOpenTracked = _openOrderIds.Contains(orderId)
                If isOpenTracked AndAlso (state = "filled" OrElse state = "cancelled" OrElse state = "rejected") Then
                    _openOrderIds.Remove(orderId)
                End If
            End SyncLock
        End Sub

        Private Async Sub OnTradeUpdate(currency As String, payload As JObject)
            ' When futures short fills, hedge with spot BTC_USDC IOC
            Dim instrument = payload.Value(Of String)("instrument_name")
            If String.IsNullOrEmpty(instrument) Then Return

            If instrument = FuturesInstrument Then
                ' Aggregate contracts filled
                Dim trades = payload("trades")
                If trades Is Nothing OrElse trades.Type <> JTokenType.Array Then Return
                Dim totalContracts As Integer = 0
                Dim avgPrice As Decimal = 0D
                Dim w As Decimal = 0D

                For Each t In trades
                    Dim side = t.Value(Of String)("side")
                    If side <> "sell" Then Continue For ' For our short, fill side is "sell"
                    Dim c As Integer = t.Value(Of Integer?)("contracts").GetValueOrDefault(0)
                    Dim px As Decimal = t.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                    Dim fee = t.Value(Of Decimal?)("fee").GetValueOrDefault(0D)
                    Dim feeCcy = t.Value(Of String)("fee_currency")
                    totalContracts += c
                    If px > 0 Then
                        avgPrice = (avgPrice * w + px * c) / Math.Max(1, w + c)
                        w += c
                    End If
                    ' Persist trade
                    _db.SaveTrade(
                      t.Value(Of String)("trade_id"),
                      DateTime.UtcNow,
                      side,
                      instrument,
                      Nothing,
                      c,
                      px,
                      feeCcy,
                      fee
                    )
                Next

                If totalContracts > 0 AndAlso avgPrice > 0 Then
                    Dim btcToBuy = ComputeBtcFromContracts(totalContracts, avgPrice)
                    ' Compute a marketable limit price for IOC on BTC_USDC
                    Dim limitUp = _monitor.SpotMid * (1D + MaxSlippageBps / 10000D)
                    Dim spotBuy = Await _api.PlaceOrderAsync(
                      instrument:=SpotInstrument,
                      side:="buy",
                      amount:=btcToBuy,
                      contracts:=Nothing,
                      price:=limitUp,
                      orderType:="limit",
                      tif:="immediate_or_cancel",
                      postOnly:=Nothing,
                      reduceOnly:=Nothing,
                      label:="ContangoSpotIOC"
                    )
                    TrackOrder(spotBuy)
                End If
            ElseIf instrument = SpotInstrument Then
                ' Persist spot trades
                Dim trades = payload("trades")
                If trades Is Nothing OrElse trades.Type <> JTokenType.Array Then Return
                For Each t In trades
                    Dim side = t.Value(Of String)("side")
                    Dim amt = t.Value(Of Decimal?)("amount").GetValueOrDefault(0D)
                    Dim px = t.Value(Of Decimal?)("price").GetValueOrDefault(0D)
                    Dim fee = t.Value(Of Decimal?)("fee").GetValueOrDefault(0D)
                    Dim feeCcy = t.Value(Of String)("fee_currency")
                    _db.SaveTrade(
                      t.Value(Of String)("trade_id"),
                      DateTime.UtcNow,
                      side,
                      instrument,
                      amt,
                      Nothing,
                      px,
                      feeCcy,
                      fee
                    )
                Next
            End If
        End Sub
    End Class
End Namespace
