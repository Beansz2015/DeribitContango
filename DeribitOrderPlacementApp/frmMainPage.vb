Imports System.Globalization
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Reflection.Metadata
Imports Newtonsoft.Json.Linq
Imports System.Net.WebSockets
Imports System.Threading
Imports System.Text
Public Class frmMainPage

    Private accessToken As String
    Private tokenExpiration As DateTime
    Public committedStopLossPrice As String
    Public committedTriggerPrice As String
    Public committedTakeProfitPrice As String
    Public primaryOrderID, stopLossOrderID, takeProfitOrderID, TrailOrderID As String
    Public BTCPrice As Decimal

    'Below are common functions
    ' Validation function to check if the input is a valid decimal
    Private Function ValidateDecimalInputAndMoveFocus(currentTextBox As TextBox, nextControl As Control) As Boolean
        Dim input As String = currentTextBox.Text
        Dim takeProfit As Decimal

        If Decimal.TryParse(input, takeProfit) Then
            ' Input is a valid decimal, move the focus to the next control
            nextControl.Focus()
            Return True
        Else
            ' Input is not a valid decimal, display an error message
            MessageBox.Show("Please enter a valid decimal number.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ' Optionally, you can clear the textbox or select the text for correction
            currentTextBox.SelectAll()
            Return False
        End If
    End Function

    Private Function ValidateDecimalInput(currentTextBox As TextBox, nextControl As Control) As Boolean
        Dim input As String = currentTextBox.Text
        Dim takeProfit As Decimal

        If Decimal.TryParse(input, takeProfit) Then
            ' Input is a valid decimal?
            Return True
        Else
            ' Input is not a valid decimal, display an error message
            MessageBox.Show("Please enter a valid decimal number.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ' Optionally, you can clear the textbox or select the text for correction
            currentTextBox.Focus()
            currentTextBox.SelectAll()
            Return False
        End If
    End Function


    Private Shared ReadOnly client As New HttpClient With {
    .BaseAddress = New Uri("https://asia.deribit.com")
}
    Private Sub LogError(message As String)
        ' Implement logging logic here (e.g., write to a file or logging system)
        Console.WriteLine("Error: " & message)
    End Sub

    ' Define a function to add colored text
    Private Sub AppendColoredText(rtb As RichTextBox, text As String, color As Color)
        rtb.SelectionStart = rtb.TextLength
        rtb.SelectionLength = 0
        rtb.SelectionColor = color
        rtb.AppendText(text)
        rtb.SelectionColor = rtb.ForeColor ' Reset color back to default
    End Sub

    'Functions below here are to check order-related statuses and prices
    '------------------------------------------------------------------------------------------------------------------------------

    'Function to cancel all existing open orders
    Private Async Function CancelAllOpenOrdersAsync() As Task
        Try
            timerTopBid.Stop()
            timerTopAsk.Stop()

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Retrieve all open orders for the specified instrument
            'Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name=BTC-PERPETUAL")
            'If response.IsSuccessStatusCode Then
            ' Dim responseBody = Await response.Content.ReadAsStringAsync()
            ' Dim jsonResponse = JObject.Parse(responseBody)

            ' Check if there are any open orders
            ' Dim orders = jsonResponse("result")
            'If orders IsNot Nothing AndAlso orders.Count > 0 Then

            ' Prepare the payload for the cancel order
            Dim payload As New JObject(
                        New JProperty("jsonrpc", "2.0"),
                        New JProperty("id", 10),
                        New JProperty("method", "private/cancel_all_by_instrument"),
                        New JProperty("params", New JObject(
                        New JProperty("instrument_name", "BTC-PERPETUAL"),
                        New JProperty("type", "all")
                ))
            )
            Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

            ' Make the POST request to cancel the orders
            Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/cancel_all_by_instrument", content)

            ' Read the response content as a string
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()

            If response.IsSuccessStatusCode Then
                Try
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Orders cancelled.", Color.Green)

                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + ex.Message, Color.Red)
                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + ex.Message, Color.Red)
        End Try
    End Function

    Private Async Function CheckOrderStatus(orderId As String) As Task(Of String)
        Dim response = Await client.GetAsync($"/api/v2/private/get_order_state?order_id={orderId}")
        If response.IsSuccessStatusCode Then
            Dim orderContent = Await response.Content.ReadAsStringAsync()
            Dim orderDetails = JObject.Parse(orderContent)
            Dim orderState = orderDetails("result")("order_state").ToString()

            If orderState = "filled" Then
                AppendColoredText(RichTextBox1, Environment.NewLine + orderId + " exec.", Color.Green)
                Return "Filled"
                Exit Function
            ElseIf (orderState = "untriggered") Then
                Return "Untriggered"
                Exit Function
            ElseIf (orderState = "open") Then
                Return "Open"
                Exit Function
            ElseIf (orderState = "cancelled") Then
                Return "Cancelled"
                Exit Function
            End If
        Else
            Return "Exception"
        End If
        Return ""
    End Function

    'Checks if there are existing open positions
    Private Async Function CheckOpenPosition() As Task(Of Boolean)
        Dim response = Await client.GetAsync($"/api/v2/private/get_position?instrument_name=BTC-PERPETUAL")

        If response.IsSuccessStatusCode Then
            Dim content = Await response.Content.ReadAsStringAsync()
            'Dim json = JObject.Parse(content)

            Dim size As Decimal = JObject.Parse(content)("result")("size").ToObject(Of Decimal)()
            If size <> 0 Then
                timerTopBid.Stop()
                timerTopAsk.Stop()
                Return True
            End If

            'If json("result")("size").ToObject(Of Decimal)() <> 0 Then
            ' A position is open if size is not zero
            'timerTopBid.Stop()
            'timerTopAsk.Stop()
            'Return True
            'End If
        End If

        Return False
    End Function

    'Function to get the price of the current open order ID
    Private Async Function GetOrderPriceAsync(orderId As String) As Task(Of Decimal?)
        Dim orderPrice As Decimal? = Nothing

        Try
            ' Make the API request to get the order state by ID
            Dim response = Await client.GetAsync($"/api/v2/private/get_order_state?order_id={orderId}")

            If response.IsSuccessStatusCode Then
                Dim content = Await response.Content.ReadAsStringAsync()
                Dim json = JObject.Parse(content)

                ' Check if the result field contains the order data
                If json("result") IsNot Nothing Then
                    ' Retrieve the price from the JSON response
                    orderPrice = Decimal.Parse(json("result")("price").ToString())
                Else
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Order data not found in the response.", Color.Yellow)
                End If
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + $"Failed to retrieve order details: {Await response.Content.ReadAsStringAsync()}", Color.Yellow)
            End If
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + $"An error occurred: {ex.Message}", Color.Red)
        End Try

        Return orderPrice
    End Function

    'Pulls the price of the 1st open order
    Private Async Function GetPrimaryOpenOrderPriceAsync() As Task(Of Decimal?)
        Dim primaryOrderPrice As Decimal? = Nothing

        Try
            ' Make an API request to get all open orders for the specified instrument
            Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name=BTC-PERPETUAL")

            If response.IsSuccessStatusCode Then
                Dim content = Await response.Content.ReadAsStringAsync()
                Dim json = JObject.Parse(content)

                ' Check if there are open orders in the result
                If json("result") IsNot Nothing AndAlso json("result").HasValues Then
                    ' Assuming the primary order is the first in the list, retrieve its price
                    primaryOrderPrice = Decimal.Parse(json("result")(0)("price").ToString())
                Else
                    AppendColoredText(RichTextBox1, Environment.NewLine + "No open orders found for this instrument.", Color.Yellow)
                End If
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + $"Failed to retrieve open orders: {Await response.Content.ReadAsStringAsync()}", Color.Yellow)
            End If
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + $"An error occurred: {ex.Message}", Color.Red)
        End Try
        Return primaryOrderPrice
    End Function

    'Check if there are any open orders
    Private Async Function CheckOpenOrdersAsync() As Task(Of Integer)
        Dim openOrderCount As Integer = 0

        ' Define the endpoint to fetch open orders
        Dim endpoint As String = "/api/v2/private/get_open_orders_by_instrument?instrument_name=BTC-PERPETUAL"

        ' Make the API call
        Dim response = Await client.GetAsync(endpoint)

        If response.IsSuccessStatusCode Then
            Dim content = Await response.Content.ReadAsStringAsync()
            Dim json = JObject.Parse(content)

            ' Check if we have orders in the result
            If json("result") IsNot Nothing Then
                Dim orders = json("result").Children()

                ' Count the number of open orders
                openOrderCount = orders.Count()
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "No orders found.", Color.Yellow)
            End If
        Else
            AppendColoredText(RichTextBox1, Environment.NewLine + $"Error fetching open orders: {response.ReasonPhrase}", Color.Yellow)
        End If

        Return openOrderCount
    End Function

    ' Function to handle order error status update
    Private Sub OrderStatus(button As Button, label As Label, progressBar As ProgressBar, Status As String)

        label.Text = Status

        If Status = "Order Error." Then
            button.Enabled = True
            label.ForeColor = Color.Red
            progressBar.Value = 0
        ElseIf Status = "Order Placed." Then
            button.Enabled = True
            label.ForeColor = Color.LimeGreen
            progressBar.Value = 100
        ElseIf Status = "Prep Payload." Then
            button.Enabled = False
            label.ForeColor = Color.DarkOrange
            progressBar.Value = 20
        ElseIf Status = "TP Order Edited." Then
            button.Enabled = True
            label.ForeColor = Color.DodgerBlue
            progressBar.Value = 100
        ElseIf Status = "SL Order Edited." Then
            button.Enabled = True
            label.ForeColor = Color.DarkRed
            progressBar.Value = 100
        ElseIf Status = "Market Placed!" Then
            button.Enabled = True
            label.ForeColor = Color.DarkRed
            progressBar.Value = 100
        End If

    End Sub

    Public Sub ButtonDisabler()
        btnMarketBuy.Enabled = False
        btnBuyLimit.Enabled = False
        btnSellLimit.Enabled = False
        btnMarketSell.Enabled = False
        btnEditTPBuyPrice.Enabled = False
        btnEditSLBuyPrice.Enabled = False
        btnEditSLSellPrice.Enabled = False
        btnEditTPSellPrice.Enabled = False
        btnLCancelAllOpen.Enabled = False
        btnSCancelAllOpen.Enabled = False
    End Sub

    'Below is code to pull non-authenticated data like price only

    'Below section pulls the latest index price
    Private Async Function GetBTCUSDCPriceAsync() As Task(Of Decimal)
        Try
            ' Make the GET request to fetch the BTC/USDC index price
            'Dim response As HttpResponseMessage = Await client.GetAsync("/api/v2/public/get_index_price?index_name=btc_usdc")
            Dim response As HttpResponseMessage = Await client.GetAsync("/api/v2/public/ticker?instrument_name=BTC-PERPETUAL")

            ' Ensure the request was successful
            If Not response.IsSuccessStatusCode Then
                LogError("Failed to get BTC/USDC price:  " & response.ReasonPhrase)
                Return 0
            End If

            ' Parse the response content
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()
            'Dim json As JObject = JObject.Parse(responseBody)

            Dim price As Decimal = JObject.Parse(responseBody)("result")("last_price").ToObject(Of Decimal)()

            'Dim price As Decimal = json("result")("last_price").Value(Of Decimal)

            Return price
        Catch ex As Exception
            LogError("Exception in GetBTCUSDCPriceAsync: " & ex.Message)
            Return 0
        End Try
    End Function


    ' Make additional API calls to determine which order is stop loss and which is take profit
    Private Async Function IdentifyOTCOrdersAsync(otocoOrderIds As List(Of String)) As Task(Of (String, String))
        Dim privateStopLossOrderId As String = String.Empty
        Dim privateTakeProfitOrderId As String = String.Empty

        ' Loop through each OTOCO order ID to determine its type
        For Each orderId As String In otocoOrderIds
            ' Make an API call to fetch order details for each OTOCO order ID
            Dim orderDetailsResponse = Await client.GetAsync($"/api/v2/private/get_order_state?order_id={orderId}")

            If orderDetailsResponse.IsSuccessStatusCode Then
                Dim orderDetailsContent = Await orderDetailsResponse.Content.ReadAsStringAsync()
                Dim orderDetailsJson = JObject.Parse(orderDetailsContent)

                ' Ensure the response contains order details
                If orderDetailsJson("result") IsNot Nothing Then
                    Dim orderType = orderDetailsJson("result")("order_type").ToString()

                    ' Determine if this order is a stop loss or take profit order
                    If orderType = "stop_limit" OrElse orderType = "stop_market" Then

                        privateStopLossOrderId = orderId
                        committedStopLossPrice = orderDetailsJson("result")("price").ToString()
                        committedTriggerPrice = orderDetailsJson("result")("trigger_price").ToString()

                    ElseIf orderType = "limit" Then
                        privateTakeProfitOrderId = orderId
                        committedTakeProfitPrice = orderDetailsJson("result")("price").ToString()

                    End If
                End If
            Else
                ' Handle potential errors when fetching order details
                'RichTextBox1.AppendText($"Error fetching details for OTOCO order {orderId}: {Await orderDetailsResponse.Content.ReadAsStringAsync()}")

                'Uncomment below if error from executing this function from ExecuteBuyOrderAsync / ExecuteSellOrderAsync
                'AppendColoredText(RichTextBox1, Environment.NewLine + "OTOCO ID error.", Color.Yellow)
            End If
        Next

        Return (privateStopLossOrderId, privateTakeProfitOrderId)
    End Function

    Private Async Function EditOrdersAsync(editOrderType As String) As Task

        Try

            'To prep variables for POST payload depending on which button was clicked
            '---------------------------------------------------------------------------
            Dim amount As String
            Dim editOrderID As String
            Dim takeprofit As String
            Dim triggerprice As String
            Dim calc As Decimal
            Dim stoploss As String
            Dim Ltrailoffset As String
            Dim Strailoffset As String

            If editOrderType = "LongTP" Then
                amount = txtLAmount.Text
                editOrderID = takeProfitOrderID
                takeprofit = txtPlacedTakeProfitBuyPrice.Text
                Await PostEditTPOrdersAsync(amount, editOrderID, takeprofit)
                AppendColoredText(RichTextBox1, Environment.NewLine + "Ed.TP Buy $" + txtPlacedTakeProfitBuyPrice.Text, Color.Green)
            ElseIf editOrderType = "LongTS" Then
                amount = txtLAmount.Text
                editOrderID = stopLossOrderID
                triggerprice = txtPlacedTriggerStopBuyPrice.Text
                calc = Decimal.Parse(triggerprice) - Decimal.Parse(txtLStopLoss.Text)
                stoploss = calc.ToString("F2")
                Await PostEditTSOrdersAsync(amount, editOrderID, triggerprice, stoploss)
                txtPlacedStopLossBuyPrice.Text = stoploss
                AppendColoredText(RichTextBox1, Environment.NewLine + "Ed.TS Buy $" + txtPlacedStopLossBuyPrice.Text, Color.Green)
            ElseIf editOrderType = "ShortTP" Then
                amount = txtSAmount.Text
                editOrderID = takeProfitOrderID
                takeprofit = txtPlacedTakeProfitSellPrice.Text
                Await PostEditTPOrdersAsync(amount, editOrderID, takeprofit)
                AppendColoredText(RichTextBox1, Environment.NewLine + "Ed.TP Sell $" + txtPlacedTakeProfitSellPrice.Text, Color.Green)
            ElseIf editOrderType = "ShortTS" Then
                amount = txtSAmount.Text
                editOrderID = stopLossOrderID
                triggerprice = txtPlacedTriggerStopSellPrice.Text
                calc = Decimal.Parse(triggerprice) + Decimal.Parse(txtSStopLoss.Text)
                stoploss = calc.ToString("F2")
                Await PostEditTSOrdersAsync(amount, editOrderID, triggerprice, stoploss)
                txtPlacedStopLossSellPrice.Text = stoploss
                AppendColoredText(RichTextBox1, Environment.NewLine + "Ed.TS Sell $" + txtPlacedStopLossSellPrice.Text, Color.Green)
            ElseIf editOrderType = "LongTrail" Then
                amount = txtLAmount.Text
                editOrderID = TrailOrderID
                Ltrailoffset = txtLTPOffset.Text.Trim()
                Await LTrailStopEdit(amount, editOrderID, Ltrailoffset)
                AppendColoredText(RichTextBox1, Environment.NewLine + "L. Trail Trg. Ed.", Color.Green)
            ElseIf editOrderType = "ShortTrail" Then
                amount = txtSAmount.Text
                editOrderID = TrailOrderID
                Strailoffset = txtSTPOffset.Text.Trim()
                Await STrailStopEdit(amount, editOrderID, Strailoffset)
                AppendColoredText(RichTextBox1, Environment.NewLine + "S. Trail Trg. Ed.", Color.Green)
            End If

            '---------------------------------------------------------------------------
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try

    End Function

    'For posting Take profit price edits
    Private Async Function PostEditTPOrdersAsync(amount As String, editOrderID As String, takeprofit As String) As Task

        Try

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Prepare the payload for the edit order

            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/edit"),
                New JProperty("params", New JObject(
                New JProperty("instrument_name", "BTC-PERPETUAL"),
                New JProperty("amount", amount),
                New JProperty("order_id", editOrderID),
                New JProperty("price", takeprofit)
                ))
            )

            Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/edit", content)

            ' Read the response content as a string
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error posting Take Profit edit order: " & responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try
    End Function

    'For posting Trigger Stop price edits
    Private Async Function PostEditTSOrdersAsync(amount As String, editOrderID As String, triggerprice As String, stoploss As String) As Task

        Try

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Prepare the payload for the edit order

            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/edit"),
                New JProperty("params", New JObject(
                New JProperty("instrument_name", "BTC-PERPETUAL"),
                New JProperty("amount", amount),
                New JProperty("order_id", editOrderID),
            New JProperty("trigger_price", triggerprice),
            New JProperty("price", stoploss)
                ))
            )

            Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/edit", content)

            ' Read the response content as a string
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error posting Stop Loss edit order: " & responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try
    End Function

    'For posting timer bid/ask open order price edits
    Private Async Function PostEditOpenOrdersAsync(editOrderID As String, amount As Decimal, openorderprice As Decimal, triggeropenorderprice As Decimal) As Task

        Try

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Prepare the payload for the edit order
            If String.IsNullOrEmpty(triggeropenorderprice) Or (triggeropenorderprice < 1) Then

                Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/edit"),
                New JProperty("params", New JObject(
                New JProperty("amount", amount),
                New JProperty("order_id", editOrderID),
                New JProperty("price", openorderprice)
))
            )

                Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

                ' Make the POST request to place the order
                Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/edit", content)

                ' Read the response content as a string
                Dim responseBody As String = Await response.Content.ReadAsStringAsync()

                If response.IsSuccessStatusCode Then
                    If timerLStopLoss.Enabled = True Then
                        AppendColoredText(RichTextBox1, Environment.NewLine + $"Timer Long ST Order {editOrderID}->${openorderprice}", Color.LightGreen)
                    ElseIf timerSStopLoss.Enabled = True Then
                        AppendColoredText(RichTextBox1, Environment.NewLine + $"Timer Short ST Order {editOrderID}->${openorderprice}", Color.LightGreen)
                    Else
                        AppendColoredText(RichTextBox1, Environment.NewLine + $"Order {editOrderID}->${openorderprice}", Color.LightGreen)
                    End If
                Else
                    If Await CheckOpenPosition() = False Then
                        If timerLStopLoss.Enabled = True Then
                            AppendColoredText(RichTextBox1, Environment.NewLine + "Timer Long Stop Loss Error" + responseBody, Color.Yellow)
                        ElseIf timerSStopLoss.Enabled = True Then
                            AppendColoredText(RichTextBox1, Environment.NewLine + "Timer Short Stop Loss Error" + responseBody, Color.Yellow)
                        Else
                            AppendColoredText(RichTextBox1, Environment.NewLine + "Open Order Edit Error" + responseBody, Color.Yellow)
                        End If
                    End If
                End If

            ElseIf triggeropenorderprice > 0 Then
                Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/edit"),
                New JProperty("params", New JObject(
                New JProperty("instrument_name", "BTC-PERPETUAL"),
                New JProperty("amount", amount),
                New JProperty("order_id", editOrderID),
                New JProperty("price", openorderprice),
                New JProperty("trigger_price", triggeropenorderprice)
                ))
            )

                Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

                ' Make the POST request to place the order
                Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/edit", content)

                ' Read the response content as a string
                Dim responseBody As String = Await response.Content.ReadAsStringAsync()

                If response.IsSuccessStatusCode Then
                    If timerLStopLoss.Enabled = True Then
                        AppendColoredText(RichTextBox1, Environment.NewLine + $"Timer Long ST Trig. Order {editOrderID}->${openorderprice}", Color.LightGreen)
                    ElseIf timerSStopLoss.Enabled = True Then
                        AppendColoredText(RichTextBox1, Environment.NewLine + $"Timer Short ST Trig. Order {editOrderID}->${openorderprice}", Color.LightGreen)
                    Else
                        AppendColoredText(RichTextBox1, Environment.NewLine + $"Order {editOrderID}->${openorderprice},Trig.${triggeropenorderprice}", Color.LightGreen)
                    End If

                Else
                    If Await CheckOpenPosition() = False Then
                        If timerLStopLoss.Enabled = True Then
                            AppendColoredText(RichTextBox1, Environment.NewLine + "Timer Long Stop Loss Trig. Error" + responseBody, Color.Yellow)
                        ElseIf timerSStopLoss.Enabled = True Then
                            AppendColoredText(RichTextBox1, Environment.NewLine + "Timer Short Stop Loss Trig. Error" + responseBody, Color.Yellow)
                        Else
                            AppendColoredText(RichTextBox1, Environment.NewLine + "Open Trig. Order Edit Error" + responseBody, Color.Yellow)
                        End If
                    End If
                End If
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)
        End Try
    End Function

    Private Async Function LTrailStopEdit(amount As String, editOrderID As String, Ltrailoffset As String) As Task

        Try

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Prepare the payload for the edit order

            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/edit"),
                New JProperty("params", New JObject(
                New JProperty("instrument_name", "BTC-PERPETUAL"),
                New JProperty("amount", amount),
                New JProperty("order_id", editOrderID),
                New JProperty("trigger_offset", Ltrailoffset)
                ))
            )

            Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/edit", content)

            ' Read the response content as a string
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error posting Long Trail edit order: " & responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)
        End Try
    End Function

    Private Async Function STrailStopEdit(amount As String, editOrderID As String, Strailoffset As String) As Task

        Try

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Prepare the payload for the edit order

            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/edit"),
                New JProperty("params", New JObject(
                New JProperty("instrument_name", "BTC-PERPETUAL"),
                New JProperty("amount", amount),
                New JProperty("order_id", editOrderID),
                New JProperty("trigger_offset", Strailoffset)
                ))
            )

            Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/edit", content)

            ' Read the response content as a string
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error posting Short Trail edit order: " & responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)
        End Try
    End Function

    'The section below gets the top bid price
    Private Async Function GetTopBidPriceAsync() As Task(Of Decimal)
        Try

            ' Make the GET request to fetch the order book
            Dim response As HttpResponseMessage = Await client.GetAsync("/api/v2/public/get_order_book?instrument_name=BTC-PERPETUAL")

            ' Ensure the request was successful
            If Not response.IsSuccessStatusCode Then
                LogError("Failed to get top bid price: " & response.ReasonPhrase)
                Return 1
            Else
                ' Read the response content as a string
                Dim responseBody As String = Await response.Content.ReadAsStringAsync()

                ' Parse the JSON response to get the highest bid price
                'Dim json As JObject = JObject.Parse(responseBody)

                Dim topBid As Decimal = JObject.Parse(responseBody)("result")("best_bid_price").ToObject(Of Decimal)()
                'Dim topBid As Decimal = json("result")("best_bid_price").Value(Of Double)()

                Return topBid
            End If

        Catch ex As Exception
            LogError("Exception in GetTopBidPriceAsync: " & ex.Message)
            Return 0
        End Try

    End Function

    'The section below gets the top ask price
    Private Async Function GetTopAskPriceAsync() As Task(Of Decimal)
        Try

            ' Make the GET request to fetch the order book
            Dim response As HttpResponseMessage = Await client.GetAsync("/api/v2/public/get_order_book?instrument_name=BTC-PERPETUAL")

            ' Ensure the request was successful
            If Not response.IsSuccessStatusCode Then
                LogError("Failed to get top ask price: " & response.ReasonPhrase)
                Return 1
            Else
                ' Read the response content as a string
                Dim responseBody As String = Await response.Content.ReadAsStringAsync()

                ' Parse the JSON response to get the highest bid price
                'Dim json As JObject = JObject.Parse(responseBody)

                Dim topAsk As Decimal = JObject.Parse(responseBody)("result")("best_ask_price").ToObject(Of Decimal)()
                'Dim topAsk As Decimal = json("result")("best_ask_price").Value(Of Double)()

                Return topAsk
            End If

        Catch ex As Exception
            LogError("Exception in GetTopAskPriceAsync: " & ex.Message)
            Return 0
        End Try
    End Function

    ' Function to update all orders in parallel
    Private Async Function UpdateMultipleOrdersInParallelAsync(orderUpdates As Dictionary(Of String, (amount As Decimal, PrimaryPrice As Decimal, TriggerPrice As Decimal))) As Task
        ' Create a list to hold the tasks for each order update
        Dim tasks As New List(Of Task)

        ' Loop through each order and add a task for each update request
        For Each orderId As String In orderUpdates.Keys
            Dim prices = orderUpdates(orderId)

            ' Queue up each update request as a task
            tasks.Add(PostEditOpenOrdersAsync(orderId, prices.amount, prices.PrimaryPrice, prices.TriggerPrice))
        Next

        ' Await all tasks to complete in parallel
        Try
            Await Task.WhenAll(tasks)
            'AppendColoredText(RichTextBox1, Environment.NewLine + "All orders updated successfully.", Color.Green)
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "An error occurred during order updates: " & ex.Message, Color.Red)
        End Try
    End Function

    '------------------------------------------------------------------------------------------------------------------------------
    'Below is code to pull authenticated data like balances

    Private Async Function EnsureAuthenticationAsync(clientId As String, clientSecret As String) As Task
        If String.IsNullOrEmpty(accessToken) OrElse DateTime.UtcNow >= tokenExpiration Then
            Await AuthenticateAsync(clientId, clientSecret)
        End If
    End Function

    Private Async Function AuthenticateAsync(clientId As String, clientSecret As String) As Task

        ' Create the authentication request content as JSON
        Dim authContent As New JObject(
        New JProperty("jsonrpc", "2.0"),
        New JProperty("id", 1),
        New JProperty("method", "public/auth"),
        New JProperty("params", New JObject(
            New JProperty("grant_type", "client_credentials"),
            New JProperty("client_id", clientId),
            New JProperty("client_secret", clientSecret)
        ))
    )

        Dim content As New StringContent(authContent.ToString(), Encoding.UTF8, "application/json")

        ' Make the POST request to authenticate
        Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/public/auth", content)

        ' Read the response content as a string
        Dim responseBody As String = Await response.Content.ReadAsStringAsync()

        If response.IsSuccessStatusCode Then
            ' Parse the JSON response to get the access token
            Dim json As JObject = JObject.Parse(responseBody)
            accessToken = json("result")("access_token").Value(Of String)()
            Dim expiresIn As Integer = json("result")("expires_in").Value(Of Integer)()
            tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 30) ' Subtract 30 seconds for a buffer

            'Give successful status update
            lblStatus.Text = "Connected!"
            lblStatus.ForeColor = Color.LimeGreen
        Else
            Throw New Exception("Authentication failed: " & responseBody)
        End If
    End Function

    Private Async Function GetBtcAccountInfoAsync() As Task(Of (Balance As Decimal, Equity As Decimal))
        Try
            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Make the GET request to fetch the account summary
            Dim response As HttpResponseMessage = Await client.GetAsync("/api/v2/private/get_account_summary?currency=BTC")

            ' Read the response content as a string
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()

            If response.IsSuccessStatusCode Then
                ' Parse the JSON response to get the BTC balance and equity
                Dim json As JObject = JObject.Parse(responseBody)
                Dim btcBalance As Decimal = json("result")("balance").Value(Of Decimal)()
                Dim btcEquity As Decimal = json("result")("equity").Value(Of Decimal)()

                ' Start timers or other logic
                CallTimer1Tick()
                'CalltimerEntryPriceLong()
                'CalltimerEntryPriceShort()

                Timer1.Start()
                'timerEntryPriceLong.Start()
                'timerEntryPriceShort.Start()

                ' Return both the balance and equity
                Return (btcBalance, btcEquity)
            Else
                Throw New Exception("Failed to get account summary: " & responseBody)
            End If

        Catch ex As Exception
            LogError("Exception in GetBtcAccountInfoAsync: " & ex.Message)
            Return (0D, 0D) ' Return a tuple with default values
        End Try

    End Function




    'Below are all the event handlers

    Private Async Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
        Try

            'Ensure authentication and get the access token

            'Testnet API
            'Await EnsureAuthenticationAsync("YEwHDiU5", "iGNfQP-HgSje-ECSmNUG3NT6AEETuYe9IMoXVilmAes")

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Call the function to get the BTC balance/equity

            Dim accountInfo = Await GetBtcAccountInfoAsync()
            Dim btcBalance = accountInfo.Balance
            Dim btcEquity = accountInfo.Equity
            Dim btcSession = btcEquity - btcBalance

            ' Update the label with the BTC balance
            lblBalance.Text = btcBalance.ToString("F8") ' BTC balances are usually displayed with 8 decimal places

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

            lblBTCEquity.Text = btcEquity.ToString("F8")
            lblBTCSession.Text = btcSession.ToString("F8")

            If btnConnect.Text = "Connect!" Then
                AppendColoredText(RichTextBox1, Environment.NewLine + "Connected.", Color.DodgerBlue)
            ElseIf btnConnect.Text = "Update!" Then
                AppendColoredText(RichTextBox1, Environment.NewLine + "Balance Updated.", Color.DodgerBlue)
            End If

            btnConnect.Text = "Update!"
            btnConnect.BackColor = Color.Lime

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Connect failed.", Color.Red)
        End Try
    End Sub

    'Below are timer-related functions
    Private Async Function UpdateBTCUSDPrice() As Task
        Try
            ' Call the function to get the BTC/USDC index price
            Dim price As Decimal = Await GetBTCUSDCPriceAsync()

            ' Update the label with the fetched price
            BTCPrice = price
            lblLastPrice.Text = price.ToString("C", CultureInfo.CreateSpecificCulture("en-US"))

            'Update equivalent USD value of BTC balance with current price
            Dim equivbal As Decimal = lblBalance.Text * price
            lblEquiv.Text = equivbal.ToString("C", CultureInfo.CreateSpecificCulture("en-US"))

            'Update equivalent USD value of BTC equity with current price
            Dim equitybal As Decimal = lblBTCEquity.Text * price
            lblUSDEquity.Text = equitybal.ToString("C", CultureInfo.CreateSpecificCulture("en-US"))

            'Update equivalent USD value of BTC session with current price
            Dim sessionbal As Decimal = lblBTCSession.Text * price
            lblUSDSession.Text = sessionbal.ToString("C", CultureInfo.CreateSpecificCulture("en-US"))

            'Calculate the price increase/decrease in order to cover commission costs
            If txtLAmount.Text > 0 Then
                Dim LAmount As Decimal = txtLAmount.Text
                Dim LBreakEvenPrice As Decimal = (LAmount / ((LAmount / price) * 0.9995))
                Dim LBreakEven As Decimal = Math.Ceiling(LBreakEvenPrice - price)
                txtLComms.Text = LBreakEven.ToString
            End If

            If txtSAmount.Text > 0 Then
                Dim SAmount As Decimal = txtSAmount.Text
                Dim SBTCAmt As Decimal = (SAmount / price)
                Dim SCommCost As Decimal = (SAmount * 0.0005)
                Dim SBreakEven As Decimal = Math.Ceiling(SCommCost / SBTCAmt)
                txtSComms.Text = SBreakEven.ToString
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Update BTC/USD Price issue: " & ex.Message, Color.Red)
        End Try
    End Function

    Private Async Function UpdateTopBidPrice() As Task
        Try
            ' Call the function to get the lowest ask price and deduct by $0.50
            Dim topBid As Decimal = Await GetTopBidPriceAsync()

            ' Update the label with the highest bid price
            txtLEntryPrice.Text = topBid.ToString("F2")

            'Calculates Take Profit price
            Dim LTakeProfit = Decimal.Parse(lblLTakeProfit.Text)
            Dim LTakeProfitPrice = topBid + LTakeProfit
            txtLTakeProfitPrice.Text = LTakeProfitPrice.ToString("F2")

            'Calculate estimated profit
            Dim LAmount = Decimal.Parse(txtLAmount.Text)
            Dim estProfit = (LTakeProfit / topBid) * LAmount
            lblLEstProfit.Text = estProfit.ToString("F2")

            'Calculates Trigger price
            Dim LTrigger = Decimal.Parse(lblLTrigger.Text)
            Dim LTriggerPrice = topBid - LTrigger
            txtLTriggerPrice.Text = LTriggerPrice.ToString("F2")

            'Calculates Stop Loss price
            Dim LStopLoss = Decimal.Parse(lblLStopLoss.Text)
            Dim LStopLossPrice = topBid - LTrigger - LStopLoss
            txtLStopLossPrice.Text = LStopLossPrice.ToString("F2")

            'Calculate estimated loss
            Dim estLoss = ((LTrigger + LStopLoss) / topBid) * LAmount
            lblLEstLoss.Text = estLoss.ToString("F2")

            'Calculate current P/L
            Dim LPlacedBuyPrice = Decimal.Parse(txtPlacedBuyPrice.Text)

            If LPlacedBuyPrice > 0 Then

                'Calculate based on executed price, diff to current top bid and multiplied with amount placed / top bid
                Dim LCurrentPL = (topBid - LPlacedBuyPrice) * (LAmount / topBid)

                lblLCurrentPL.Text = LCurrentPL.ToString("F2")
                If topBid < LPlacedBuyPrice Then
                    lblLCurrentPL.ForeColor = Color.Red
                Else
                    lblLCurrentPL.ForeColor = Color.Chartreuse
                End If
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Update Top Bid Price Issue: " & ex.Message, Color.Red)
        End Try
    End Function

    Private Async Function UpdateTopAskPrice() As Task
        Try

            ' Call the function to get the highest bid price and add $0.50
            Dim topAsk = Await GetTopAskPriceAsync()


            ' Update the label with the highest ask price
            txtSEntryPrice.Text = topAsk.ToString("F2")

            'Calculates Take Profit price
            Dim STakeProfit = Decimal.Parse(lblSTakeProfit.Text)
            Dim STakeProfitPrice = topAsk - STakeProfit
            txtSTakeProfitPrice.Text = STakeProfitPrice.ToString("F2")

            'Calculate estimated profit
            Dim SAmount = Decimal.Parse(txtSAmount.Text)
            Dim estProfit = (STakeProfit / topAsk) * SAmount
            lblSEstProfit.Text = estProfit.ToString("F2")

            'Calculates Trigger price
            Dim STrigger = Decimal.Parse(lblSTrigger.Text)
            Dim STriggerPrice = topAsk + STrigger
            txtSTriggerPrice.Text = STriggerPrice.ToString("F2")

            'Calculates Stop Loss price
            Dim SStopLoss = Decimal.Parse(lblSStopLoss.Text)
            Dim SStopLossPrice = topAsk + STrigger + SStopLoss
            txtSStopLossPrice.Text = SStopLossPrice.ToString("F2")

            'Calculate estimated loss
            Dim estLoss = ((STrigger + SStopLoss) / topAsk) * SAmount
            lblSEstLoss.Text = estLoss.ToString("F2")

            'Calculate current P/L
            Dim SPlacedSellPrice = Decimal.Parse(txtPlacedSellPrice.Text)

            If SPlacedSellPrice > 0 Then

                'Calculate based on executed price, diff to current top ask and multiplied with amount placed / top ask
                Dim SCurrentPL = (SPlacedSellPrice - topAsk) * (SAmount / topAsk)

                lblSCurrentPL.Text = SCurrentPL.ToString("F2")
                If topAsk > SPlacedSellPrice Then
                    lblSCurrentPL.ForeColor = Color.Red
                Else
                    lblSCurrentPL.ForeColor = Color.Chartreuse
                End If

            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Update Top Ask Price Issue: " & ex.Message, Color.Red)
        End Try
    End Function

    Private Async Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        If isClosing Then Exit Sub ' Prevents running the Tick event if form is closing
        Try
            Await UpdateBTCUSDPrice()
            Await UpdateTopBidPrice()
            Await UpdateTopAskPrice()

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Error at Primary Timer: " & ex.Message, Color.Red)
        End Try
    End Sub

    'Private Async Sub timerEntryPriceLong_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick

    'End Sub '

    'Private Async Sub timerEntryPriceShort_Tick(sender As Object, e As EventArgs) Handles timerEntryPriceShort.Tick

    'End Sub

    Private Async Sub timerLTrail_Tick(sender As Object, e As EventArgs) Handles timerLTrail.Tick
        Try

            'Dim targetOffset = Decimal.Parse(txtLTPOffset.Text.Trim) + Decimal.Parse(txtLComms.Text.Trim)

            If BTCPrice >= Decimal.Parse(txtPlacedBuyPrice.Text.Trim()) + (Decimal.Parse(txtLTPOffset.Text.Trim) + Decimal.Parse(txtLComms.Text.Trim)) Then

                ' Ensure authentication
                Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

                ' Set the authorization header with the access token
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

                ' Make the GET request to fetch all active orders for the instrument "BTC-PERPETUAL"
                Dim instrumentName = "BTC-PERPETUAL"
                Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name={instrumentName}")

                ' Read the response content as a string
                Dim responseBody = Await response.Content.ReadAsStringAsync

                If response.IsSuccessStatusCode Then
                    Try
                        ' Parse the JSON response
                        Dim json = JObject.Parse(responseBody)

                        ' Clear the RichTextBox before displaying new data
                        ' orderList.Clear()

                        ' List to store order IDs
                        Dim orderIds As New List(Of String)

                        ' Loop through each active order and extract the order ID
                        For Each order In json("result")
                            Dim orderId = order("order_id").Value(Of String)
                            orderIds.Add(orderId)
                        Next

                        If orderIds.Single IsNot Nothing Then

                            ' RichTextBox1.AppendText(Environment.NewLine + orderIds.Single)

                            TrailOrderID = orderIds.Single

                            'Start order editing calls
                            Await EditOrdersAsync("LongTrail")

                        ElseIf orderIds.Single = Nothing Then
                            timerLTrail.Stop()
                        End If
                        'Loading indicators
                        '                    OrderStatus(btnEditTPBuyPrice, lblOrderStatusLong, PBLong, "TP Order Edited.")


                    Catch ex As Exception
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)
                        timerLTrail.Stop()
                    End Try
                Else
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error retrieving orders: " & responseBody, Color.Yellow)
                End If
            End If
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try
    End Sub

    Private Async Sub timerSTrail_Tick(sender As Object, e As EventArgs) Handles timerSTrail.Tick
        Try

            'Dim targetOffset = Decimal.Parse(txtLTPOffset.Text.Trim) + Decimal.Parse(txtLComms.Text.Trim)

            If BTCPrice <= Decimal.Parse(txtPlacedSellPrice.Text.Trim()) - (Decimal.Parse(txtSTPOffset.Text.Trim) + Decimal.Parse(txtSComms.Text.Trim)) Then

                ' Ensure authentication
                Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

                ' Set the authorization header with the access token
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

                ' Make the GET request to fetch all active orders for the instrument "BTC-PERPETUAL"
                Dim instrumentName = "BTC-PERPETUAL"
                Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name={instrumentName}")

                ' Read the response content as a string
                Dim responseBody = Await response.Content.ReadAsStringAsync

                If response.IsSuccessStatusCode Then
                    Try
                        ' Parse the JSON response
                        Dim json = JObject.Parse(responseBody)


                        ' Clear the RichTextBox before displaying new data
                        ' orderList.Clear()

                        ' List to store order IDs
                        Dim orderIds As New List(Of String)

                        ' Loop through each active order and extract the order ID
                        For Each order In json("result")
                            Dim orderId = order("order_id").Value(Of String)
                            orderIds.Add(orderId)
                        Next

                        If orderIds.Single IsNot Nothing Then

                            'RichTextBox1.AppendText(Environment.NewLine + orderIds.Single)

                            TrailOrderID = orderIds.Single

                            'Start order editing calls
                            Await EditOrdersAsync("ShortTrail")

                            'Loading indicators
                            '                    OrderStatus(btnEditTPBuyPrice, lblOrderStatusLong, PBLong, "TP Order Edited.")
                        ElseIf orderIds.Single = Nothing Then
                            timerSTrail.Stop()
                        End If

                    Catch ex As Exception
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)
                        timerSTrail.Stop()
                    End Try
                Else
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error retrieving orders: " & responseBody, Color.Yellow)
                End If
            End If
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try
    End Sub

    Private isProcessingOrder As Boolean = False

    Private Async Sub timerTopBid_Tick(sender As Object, e As EventArgs) Handles timerTopBid.Tick

        timerTopBid.Stop()

        If isProcessingOrder Then Exit Sub

        'Automatically edits order and replaces on top of bid orderbook if not at top
        Dim LPlacedBuyPrice = Decimal.Parse(txtPlacedBuyPrice.Text)
        'Dim LTopBid As Decimal = Await GetTopBidPriceAsync()
        Dim LTopBid As Decimal = Decimal.Parse(txtLEntryPrice.Text)

        If LPlacedBuyPrice < LTopBid Then
            If Await CheckOpenPosition() = False Then

                isProcessingOrder = True

                Try
                    ' Define a dictionary with each order ID and its desired primary and trigger prices
                    Dim orderUpdates As New Dictionary(Of String, (Amount As Decimal, PrimaryPrice As Decimal, TriggerPrice As Decimal)) From {
        {primaryOrderID, (Convert.ToDecimal(txtLAmount.Text), LTopBid, 0)},
        {takeProfitOrderID, (Convert.ToDecimal(txtLAmount.Text), Convert.ToDecimal(txtLTakeProfitPrice.Text), 0)},
        {stopLossOrderID, (Convert.ToDecimal(txtLAmount.Text), Convert.ToDecimal(txtLStopLossPrice.Text), Convert.ToDecimal(txtLTriggerPrice.Text))}
    }
                    txtPlacedBuyPrice.Text = LTopBid
                    txtPlacedTakeProfitBuyPrice.Text = txtLTakeProfitPrice.Text
                    txtPlacedStopLossBuyPrice.Text = txtLStopLossPrice.Text
                    txtPlacedTriggerStopBuyPrice.Text = txtLTriggerPrice.Text

                    ' Call the function to update all orders in parallel
                    Await UpdateMultipleOrdersInParallelAsync(orderUpdates)

                    AppendColoredText(RichTextBox1, Environment.NewLine + "Adjusted all bid orders.", Color.Green)

                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error adjusting bid orders.", Color.Red)
                Finally
                    isProcessingOrder = False
                End Try
            Else
                timerTopBid.Stop()
                AppendColoredText(RichTextBox1, Environment.NewLine + "Long Position Entered.", Color.Crimson)
                btnConnect.Text = "In Position!"
                btnConnect.BackColor = Color.Crimson
                isProcessingOrder = False

                'timerLStopLoss.Start()
                timerCheckPosition.Start()

                Exit Sub
            End If
        End If
        timerTopBid.Start()
    End Sub

    Private Async Sub timerTopAsk_Tick(sender As Object, e As EventArgs) Handles timerTopAsk.Tick

        timerTopAsk.Stop()

        If isProcessingOrder Then Exit Sub

        'Automatically cancels order and replaces on top of ask orderbook if not at top

        Dim SPlacedSellPrice = Decimal.Parse(txtPlacedSellPrice.Text)
        'Dim StopAsk As Decimal = Await GetTopAskPriceAsync()
        Dim StopAsk As Decimal = Decimal.Parse(txtSEntryPrice.Text)

        If SPlacedSellPrice > StopAsk Then
            If Await CheckOpenPosition() = False Then

                isProcessingOrder = True

                Try
                    ' Define a dictionary with each order ID and its desired primary and trigger prices
                    Dim orderUpdates As New Dictionary(Of String, (Amount As Decimal, PrimaryPrice As Decimal, TriggerPrice As Decimal)) From {
        {primaryOrderID, (Convert.ToDecimal(txtSAmount.Text), StopAsk, 0)},
        {takeProfitOrderID, (Convert.ToDecimal(txtSAmount.Text), Convert.ToDecimal(txtSTakeProfitPrice.Text), 0)},
        {stopLossOrderID, (Convert.ToDecimal(txtSAmount.Text), Convert.ToDecimal(txtSStopLossPrice.Text), Convert.ToDecimal(txtSTriggerPrice.Text))}
    }

                    txtPlacedSellPrice.Text = StopAsk
                    txtPlacedTakeProfitSellPrice.Text = txtSTakeProfitPrice.Text
                    txtPlacedStopLossSellPrice.Text = txtSStopLossPrice.Text
                    txtPlacedTriggerStopSellPrice.Text = txtSTriggerPrice.Text

                    ' Call the function to update all orders in parallel
                    Await UpdateMultipleOrdersInParallelAsync(orderUpdates)

                    AppendColoredText(RichTextBox1, Environment.NewLine + "Adjusted all ask orders.", Color.Green)

                Catch ex As Exception

                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error adjusting ask order.", Color.Red)
                Finally
                    isProcessingOrder = False

                End Try
            Else
                timerTopAsk.Stop()
                AppendColoredText(RichTextBox1, Environment.NewLine + "Short Position Entered.", Color.Crimson)
                btnConnect.Text = "In Position!"
                btnConnect.BackColor = Color.Crimson
                isProcessingOrder = False

                'timerSStopLoss.Start()
                timerCheckPosition.Start()

                Exit Sub
            End If
        End If
        timerTopAsk.Start()
    End Sub

    Private Sub CallTimer1Tick()
        ' Call the Timer1_Tick event handler directly
        Timer1_Tick(Timer1, EventArgs.Empty)
    End Sub

    'Private Sub CalltimerEntryPriceLong()
    '   timerEntryPriceLong_Tick(timerEntryPriceLong, EventArgs.Empty)
    'End Sub

    Private Sub CallTimerLTrailTick()
        ' Call the Timer1_Tick event handler directly
        timerLTrail_Tick(timerLTrail, EventArgs.Empty)
    End Sub

    Private Sub CallTimerSTrailTick()
        ' Call the Timer1_Tick event handler directly
        timerSTrail_Tick(timerSTrail, EventArgs.Empty)
    End Sub

    '-----------------------------------------------------------------------------------------------------------------

    Private Sub Label26_click(sender As Object, e As EventArgs)

    End Sub

    Private Sub selectallclick(sender As Object, e As EventArgs) Handles txtLTakeProfit.Click, txtLTrigger.Click, txtLStopLoss.Click, txtLTakeProfitPrice.Click, txtLEntryPrice.Click, txtLTriggerPrice.Click, txtLStopLossPrice.Click, txtSStopLossPrice.Click, txtSTriggerPrice.Click, txtSTakeProfitPrice.Click, txtSTakeProfit.Enter, txtSTrigger.Enter, txtSStopLoss.Enter, txtLTakeProfit.Enter, txtLTrigger.Enter, txtLStopLoss.Enter, txtLTakeProfitPrice.Enter, txtLEntryPrice.Enter, txtLTriggerPrice.Enter, txtLStopLossPrice.Enter, txtSStopLossPrice.Enter, txtSTriggerPrice.Enter, txtSEntryPrice.Enter, txtSTakeProfitPrice.Enter, txtSEntryPrice.Click, txtSTakeProfit.Click, txtSTrigger.Click, txtSStopLoss.Click, txtLAmount.Click, txtLAmount.Enter, txtSAmount.Click, txtSAmount.Enter, txtPlacedTakeProfitBuyPrice.Click, txtPlacedTriggerStopBuyPrice.Click, txtPlacedTriggerStopSellPrice.Click, txtPlacedTakeProfitSellPrice.Click, txtLTriggerOffset.Click, txtLTPOffset.Click, txtLTPOffset.Enter, txtLComms.Click, txtLComms.Enter, txtLStartOffset.Click, txtLStartOffset.Enter, txtSTriggerOffset.Click, txtSTriggerOffset.Enter, txtSTPOffset.Click, txtSTPOffset.Enter, txtSComms.Click, txtSComms.Enter, txtSStartOffset.Click, txtSStartOffset.Enter, txtLTriggerOffset.Enter
        'Cast the sender to a TextBox
        Dim txtBox = CType(sender, TextBox)

        'Select all text in the TextBox
        txtBox.SelectionStart = 0
        txtBox.SelectionLength = txtBox.Text.Length
    End Sub

    'The main Buy/Sell functions
    Private Async Function ExecuteBuyOrderAsync(TypeOfOrder As String) As Task
        Try

            timerTopBid.Stop()
            timerTopAsk.Stop()

            'Stop order placement if no open order or more than 1 open order

            ' If Await CheckOpenPosition() = False Then

            ' Dim count As Integer = Await CheckOpenOrdersAsync()
            'If (count < 1) Or (count = 3) Then
            'If count > 2 Then Await CancelAllOpenOrdersAsync()

            Dim entryPrice As Decimal
            Dim amount As Decimal
            Dim LtakeProfitPrice As Decimal
            Dim takeprofitprice As Decimal
            Dim LstopLossTriggerPrice As Decimal
            Dim stopLossTriggerPrice As Decimal
            Dim LstopLossPrice As Decimal
            Dim stopLossPrice As Decimal
            Dim triggeroffset As Decimal

            'Loading indicators
            If (TypeOfOrder = "NoSpread") Or (TypeOfOrder = "Timer") Then
                OrderStatus(btnBuyNoSpread, lblOrderStatusLong, PBLong, "Prep Payload.")
            ElseIf TypeOfOrder = "Limit" Then
                OrderStatus(btnBuy, lblOrderStatusLong, PBLong, "Prep Payload.")
            End If

            'Ensure authentication

            'Testnet API
            'Await EnsureAuthenticationAsync("YEwHDiU5", "iGNfQP-HgSje-ECSmNUG3NT6AEETuYe9IMoXVilmAes")

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            If (TypeOfOrder = "NoSpread") Or (TypeOfOrder = "Timer") Then
                ' Fetch values from textboxes and modify to fit no spread values

                entryPrice = Await GetTopAskPriceAsync()
                'entryPrice = Decimal.Parse(txtSEntryPrice.Text)
                entryPrice = entryPrice - 0.5

                amount = Decimal.Parse(txtLAmount.Text.Trim)

                LtakeProfitPrice = Decimal.Parse(txtLTakeProfit.Text)
                takeprofitprice = entryPrice + LtakeProfitPrice

                LstopLossTriggerPrice = Decimal.Parse(txtLTrigger.Text)
                stopLossTriggerPrice = entryPrice - LstopLossTriggerPrice

                LstopLossPrice = Decimal.Parse(txtLStopLoss.Text)
                stopLossPrice = entryPrice - LstopLossTriggerPrice - LstopLossPrice

                triggeroffset = Decimal.Parse(txtLTriggerOffset.Text.Trim)

            ElseIf TypeOfOrder = "Limit" Then
                entryPrice = Await GetTopBidPriceAsync()
                'entryPrice = Decimal.Parse(txtLEntryPrice.Text.Trim)
                amount = Decimal.Parse(txtLAmount.Text.Trim)
                takeprofitprice = Decimal.Parse(txtLTakeProfitPrice.Text.Trim)
                stopLossTriggerPrice = Decimal.Parse(txtLTriggerPrice.Text.Trim)
                stopLossPrice = Decimal.Parse(txtLStopLossPrice.Text.Trim)
                triggeroffset = Decimal.Parse(txtLTriggerOffset.Text.Trim)
            End If

            ' Validate that all required fields are filled
            If String.IsNullOrEmpty(entryPrice) OrElse String.IsNullOrEmpty(amount) OrElse String.IsNullOrEmpty(takeprofitprice) OrElse String.IsNullOrEmpty(stopLossTriggerPrice) OrElse String.IsNullOrEmpty(stopLossPrice) OrElse String.IsNullOrEmpty(triggeroffset) Then
                MessageBox.Show("Please fill in all required fields.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            PBLong.Value = 40

            ' Prepare the payload for the linked order
            Dim payload As New JObject(
            New JProperty("jsonrpc", "2.0"),
            New JProperty("id", 2),
            New JProperty("method", "private/buy"),
            New JProperty("params", New JObject(
                New JProperty("instrument_name", "BTC-PERPETUAL"),
                New JProperty("amount", amount),
                New JProperty("type", "limit"),
                New JProperty("label", "EntryLimitOrder"),
                New JProperty("price", entryPrice),
                New JProperty("time_in_force", "good_til_cancelled"),
                New JProperty("post_only", True),
                New JProperty("linked_order_type", "one_triggers_one_cancels_other"),
                New JProperty("trigger_fill_condition", "first_hit"),
                New JProperty("reject_post_only", False),
                New JProperty("otoco_config", New JArray(
                    New JObject(
                        New JProperty("amount", amount),
                        New JProperty("direction", "sell"),
                        New JProperty("type", "limit"),
                        New JProperty("label", "TakeLimitProfit"),
                        New JProperty("price", takeprofitprice),
                        New JProperty("reduce_only", True),
                        New JProperty("time_in_force", "good_til_cancelled"),
                        New JProperty("post_only", True)
                    ),
                    New JObject(
                    New JProperty("amount", amount),
                    New JProperty("direction", "sell"),
                    New JProperty("type", "stop_limit"),
                    New JProperty("trigger_price", stopLossTriggerPrice), ' Base trigger price
                    New JProperty("trigger_offset", triggeroffset), ' Offset for dynamic adjustment
                    New JProperty("price", stopLossPrice), ' Stop loss limit price
                    New JProperty("label", "StopLossOrder"),
                    New JProperty("reduce_only", True),
                    New JProperty("time_in_force", "good_til_cancelled"),
                    New JProperty("post_only", True),
                    New JProperty("trigger", "last_price")
                    )
                ))
            ))
        )

            PBLong.Value = 60

            Dim content As New StringContent(payload.ToString, Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response = Await client.PostAsync("/api/v2/private/buy", content)

            lblOrderStatusLong.Text = "Sending Payload."
            PBLong.Value = 80

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync

            If response.IsSuccessStatusCode Then


                btnSellLimit.Enabled = True
                btnMarketSell.Enabled = True
                btnEditTPBuyPrice.Enabled = True
                btnEditSLBuyPrice.Enabled = True
                btnLCancelAllOpen.Enabled = True
                'ButtonPushed = False

                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)


                    'The below code is for getting OTOCO order IDs
                    '---------------------------------------------
                    ' Retrieve the order ID from the JSON response
                    primaryOrderID = json("result")("order")("order_id").Value(Of String)()

                    'Await CheckOrderStatus(primaryOrderID)

                    ' Extract the OTOCO order IDs
                    Dim otocoOrderIds = json("result")("order")("oto_order_ids").Select(Function(token) token.ToString()).ToList()

                    ' Ensure there are OTOCO orders in the response
                    If otocoOrderIds.Count >= 2 Then

                        ' Use the IdentifyOTCOrdersAsync function to correctly determine the stop loss and take profit order IDs

                        Dim otocoOrderResult = Await IdentifyOTCOrdersAsync(otocoOrderIds)

                        ' Extract the values from the tuple
                        stopLossOrderID = otocoOrderResult.Item1
                        takeProfitOrderID = otocoOrderResult.Item2

                        ' Check if both order IDs are successfully identified
                        If Not String.IsNullOrEmpty(stopLossOrderID) AndAlso Not String.IsNullOrEmpty(takeProfitOrderID) Then
                            'MessageBox.Show("Primary and linked OTOCO order IDs retrieved successfully.", "Order IDs", MessageBoxButtons.OK, MessageBoxIcon.Information)
                            txtPlacedTakeProfitBuyPrice.Text = committedTakeProfitPrice.ToString(CultureInfo.InvariantCulture)
                            txtPlacedStopLossBuyPrice.Text = committedStopLossPrice.ToString(CultureInfo.InvariantCulture)
                            txtPlacedTriggerStopBuyPrice.Text = committedTriggerPrice.ToString(CultureInfo.InvariantCulture)

                        Else

                            If Await CheckOpenPosition() = False Then
                                AppendColoredText(RichTextBox1, Environment.NewLine + "OTOCO ID error @ Buy Function.", Color.Yellow)
                            Else
                                txtPlacedTakeProfitBuyPrice.Text = takeprofitprice.ToString(CultureInfo.InvariantCulture)
                                txtPlacedStopLossBuyPrice.Text = stopLossPrice.ToString(CultureInfo.InvariantCulture)
                                txtPlacedTriggerStopBuyPrice.Text = stopLossTriggerPrice.ToString(CultureInfo.InvariantCulture)
                            End If

                        End If
                    Else
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Error placing order: " + responseBody, Color.Yellow)
                    End If
                    '---------------------------------------------


                    ' Navigate through the JSON to get the price value
                    Dim price = json("result")("order")("price").Value(Of Decimal)

                    ' Display the price in the Placed Buy Prices TextBoxes
                    txtPlacedBuyPrice.Text = price.ToString(CultureInfo.InvariantCulture)

                    If (TypeOfOrder = "NoSpread") Or (TypeOfOrder = "Timer") Then
                        'Loading indicators
                        OrderStatus(btnBuyNoSpread, lblOrderStatusLong, PBLong, "Order Placed.")
                        AppendColoredText(RichTextBox1, Environment.NewLine + "NS Buy $" + txtPlacedBuyPrice.Text, Color.Green)
                    Else TypeOfOrder = "Limit"
                        OrderStatus(btnBuy, lblOrderStatusLong, PBLong, "Order Placed.")
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Buy $" + txtPlacedBuyPrice.Text, Color.Green)
                    End If

                    'Est. true profit/loss
                    Dim estProfit = ((takeprofitprice - price) / price) * amount
                    Dim estLoss = ((price - stopLossPrice) / price) * amount
                    lblLEstTrueProf.Text = estProfit.ToString("F2")
                    lblLEstTrueLoss.Text = estLoss.ToString("F2")


                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " + ex.Message, Color.Red)

                    'Loading indicators
                    If (TypeOfOrder = "NoSpread") Or (TypeOfOrder = "Timer") Then
                        OrderStatus(btnBuyNoSpread, lblOrderStatusLong, PBLong, "Order Error.")
                    Else TypeOfOrder = "Limit"
                        OrderStatus(btnBuy, lblOrderStatusLong, PBLong, "Order Error.")
                    End If

                End Try

            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error placing order: " + responseBody, Color.Yellow)

                'Loading indicators
                If (TypeOfOrder = "NoSpread") Or (TypeOfOrder = "Timer") Then
                    OrderStatus(btnBuyNoSpread, lblOrderStatusLong, PBLong, "Order Error.")
                Else TypeOfOrder = "Limit"
                    OrderStatus(btnBuy, lblOrderStatusLong, PBLong, "Order Error.")
                End If
            End If
            '    End If
            ' End If

            timerTopBid.Stop()
            timerTopAsk.Stop()

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)

            'Loading indicators
            If (TypeOfOrder = "NoSpread") Or (TypeOfOrder = "Timer") Then
                OrderStatus(btnBuyNoSpread, lblOrderStatusLong, PBLong, "Order Error.")
            Else TypeOfOrder = "Limit"
                OrderStatus(btnBuy, lblOrderStatusLong, PBLong, "Order Error.")
            End If

        End Try
    End Function

    Private Async Function ExecuteSellOrderAsync(TypeOfSOrder As String) As Task
        Try
            timerTopBid.Stop()
            timerTopAsk.Stop()
            'Await CheckOrderStatus(primaryOrderID)

            'Stop order placement if no open order or more than 1 open order
            ' Dim count As Integer = Await CheckOpenOrdersAsync()
            '  If ((count < 1) Or (count = 3)) Then
            'If count > 0 Then Await CancelAllOpenOrdersAsync()

            'If Await CheckOpenPosition() = False Then



            Dim entryPrice As Decimal
            Dim amount As Decimal
            Dim StakeProfitPrice As Decimal
            Dim takeprofitprice As Decimal
            Dim SstopLossTriggerPrice As Decimal
            Dim stopLossTriggerPrice As Decimal
            Dim SstopLossPrice As Decimal
            Dim stopLossPrice As Decimal
            Dim triggeroffset As Decimal

            'Loading indicators
            If (TypeOfSOrder = "NoSpread") Or (TypeOfSOrder = "Timer") Then
                OrderStatus(btnSellNoSpread, lblOrderStatusShort, PBShort, "Prep Payload.")
            Else TypeOfSOrder = "Limit"
                OrderStatus(btnSell, lblOrderStatusShort, PBShort, "Prep Payload.")
            End If

            'Ensure authentication

            'Testnet API
            'Await EnsureAuthenticationAsync("YEwHDiU5", "iGNfQP-HgSje-ECSmNUG3NT6AEETuYe9IMoXVilmAes")

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            If (TypeOfSOrder = "NoSpread") Or (TypeOfSOrder = "Timer") Then
                ' Fetch values from textboxes and modify to fit no spread values

                entryPrice = Await GetTopBidPriceAsync()
                'entryPrice = Decimal.Parse(txtLEntryPrice.Text)
                entryPrice = entryPrice + 0.5

                amount = txtSAmount.Text.Trim

                StakeProfitPrice = Decimal.Parse(lblSTakeProfit.Text)
                takeprofitprice = entryPrice - StakeProfitPrice

                SstopLossTriggerPrice = Decimal.Parse(lblSTrigger.Text)
                stopLossTriggerPrice = entryPrice + SstopLossTriggerPrice

                SstopLossPrice = Decimal.Parse(lblSStopLoss.Text)
                stopLossPrice = entryPrice + SstopLossTriggerPrice + SstopLossPrice

                triggeroffset = txtSTriggerOffset.Text.Trim
            Else TypeOfSOrder = "Limit"
                entryPrice = Await GetTopAskPriceAsync()
                'entryPrice = txtSEntryPrice.Text.Trim
                amount = txtSAmount.Text.Trim
                takeprofitprice = txtSTakeProfitPrice.Text.Trim
                stopLossTriggerPrice = txtSTriggerPrice.Text.Trim
                stopLossPrice = txtSStopLossPrice.Text.Trim
                triggeroffset = txtSTriggerOffset.Text.Trim
            End If

            ' Validate that all required fields are filled
            If String.IsNullOrEmpty(entryPrice) OrElse String.IsNullOrEmpty(amount) OrElse String.IsNullOrEmpty(takeprofitprice) OrElse String.IsNullOrEmpty(stopLossTriggerPrice) OrElse String.IsNullOrEmpty(stopLossPrice) Then
                MessageBox.Show("Please fill in all required fields.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            PBShort.Value = 40

            ' Prepare the payload for the linked order
            Dim payload As New JObject(
        New JProperty("jsonrpc", "2.0"),
        New JProperty("id", 2),
        New JProperty("method", "private/sell"),
        New JProperty("params", New JObject(
            New JProperty("instrument_name", "BTC-PERPETUAL"),
            New JProperty("amount", amount),
            New JProperty("type", "limit"),
            New JProperty("label", "EntryLimitOrder"),
            New JProperty("price", entryPrice),
            New JProperty("time_in_force", "good_til_cancelled"),
            New JProperty("post_only", True),
            New JProperty("linked_order_type", "one_triggers_one_cancels_other"),
            New JProperty("trigger_fill_condition", "first_hit"),
            New JProperty("reject_post_only", False),
            New JProperty("otoco_config", New JArray(
                New JObject(
                    New JProperty("amount", amount),
                    New JProperty("direction", "buy"),
                    New JProperty("type", "limit"),
                    New JProperty("label", "TakeLimitProfit"),
                    New JProperty("price", takeprofitprice),
                    New JProperty("reduce_only", True),
                    New JProperty("time_in_force", "good_til_cancelled"),
                    New JProperty("post_only", True)
                ),
                New JObject(
                New JProperty("amount", amount),
                New JProperty("direction", "buy"),
                New JProperty("type", "stop_limit"),
                New JProperty("trigger_price", stopLossTriggerPrice), ' Base trigger price
                New JProperty("trigger_offset", triggeroffset), ' Offset for dynamic adjustment
                New JProperty("price", stopLossPrice), ' Stop loss limit price
                New JProperty("label", "StopLimitLoss"),
                New JProperty("reduce_only", True),
                New JProperty("time_in_force", "good_til_cancelled"),
                New JProperty("post_only", True),
                New JProperty("trigger", "last_price")
                )
            ))
        ))
    )

            PBShort.Value = 60

            Dim content As New StringContent(payload.ToString, Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response = Await client.PostAsync("/api/v2/private/sell", content)

            lblOrderStatusShort.Text = "Sending Payload."
            PBShort.Value = 80

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync

            If response.IsSuccessStatusCode Then

                'Loading indicators
                OrderStatus(btnSellNoSpread, lblOrderStatusShort, PBShort, "Order Placed.")

                btnMarketBuy.Enabled = True
                btnBuyLimit.Enabled = True
                btnEditSLSellPrice.Enabled = True
                btnEditTPSellPrice.Enabled = True
                btnSCancelAllOpen.Enabled = True

                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)


                    'The below code is for getting OTOCO order IDs
                    '---------------------------------------------
                    ' Retrieve the order ID from the JSON response
                    primaryOrderID = json("result")("order")("order_id").Value(Of String)

                    'Await CheckOrderStatus(primaryOrderID)

                    ' Extract the OTOCO order IDs
                    Dim otocoOrderIds = json("result")("order")("oto_order_ids").Select(Function(token) token.ToString).ToList

                    ' Ensure there are OTOCO orders in the response
                    If otocoOrderIds.Count >= 2 Then

                        ' Use the IdentifyOTCOrdersAsync function to correctly determine the stop loss and take profit order IDs

                        Dim otocoOrderResult = Await IdentifyOTCOrdersAsync(otocoOrderIds)

                        ' Extract the values from the tuple
                        stopLossOrderID = otocoOrderResult.Item1
                        takeProfitOrderID = otocoOrderResult.Item2


                        ' Check if both order IDs are successfully identified
                        If Not String.IsNullOrEmpty(stopLossOrderID) AndAlso Not String.IsNullOrEmpty(takeProfitOrderID) Then
                            'MessageBox.Show("Primary and linked OTOCO order IDs retrieved successfully.", "Order IDs", MessageBoxButtons.OK, MessageBoxIcon.Information)
                            txtPlacedTakeProfitSellPrice.Text = committedTakeProfitPrice.ToString(CultureInfo.InvariantCulture)
                            txtPlacedStopLossSellPrice.Text = committedStopLossPrice.ToString(CultureInfo.InvariantCulture)
                            txtPlacedTriggerStopSellPrice.Text = committedTriggerPrice.ToString(CultureInfo.InvariantCulture)

                        Else
                            If Await CheckOpenPosition() = False Then
                                AppendColoredText(RichTextBox1, Environment.NewLine + "OTOCO ID error @ Sell Function.", Color.Yellow)
                            Else
                                txtPlacedTakeProfitSellPrice.Text = takeprofitprice.ToString(CultureInfo.InvariantCulture)
                                txtPlacedStopLossSellPrice.Text = stopLossPrice.ToString(CultureInfo.InvariantCulture)
                                txtPlacedTriggerStopSellPrice.Text = stopLossTriggerPrice.ToString(CultureInfo.InvariantCulture)
                            End If
                        End If
                    Else
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Error placing order: " + responseBody, Color.Yellow)
                    End If

                    '---------------------------------------------


                    ' Navigate through the JSON to get the price value
                    Dim price = json("result")("order")("price").Value(Of Decimal)

                    ' Display the price in the Placed Sell Prices TextBoxes
                    txtPlacedSellPrice.Text = price.ToString(CultureInfo.InvariantCulture)

                    If (TypeOfSOrder = "NoSpread") Or (TypeOfSOrder = "Timer") Then
                        'Loading indicators
                        OrderStatus(btnSellNoSpread, lblOrderStatusShort, PBShort, "Order Placed.")
                        AppendColoredText(RichTextBox1, Environment.NewLine + "NS Sell $" + txtPlacedSellPrice.Text, Color.Green)
                    Else TypeOfSOrder = "Limit"
                        OrderStatus(btnSell, lblOrderStatusShort, PBShort, "Order Placed.")
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Sell $" + txtPlacedSellPrice.Text, Color.Green)
                    End If

                    'Est. true profit/loss
                    Dim estProfit = (price - takeprofitprice) / price * amount
                    Dim estLoss = (stopLossPrice - price) / price * amount
                    lblSEstTrueProf.Text = estProfit.ToString("F2")
                    lblSEstTrueLoss.Text = estLoss.ToString("F2")

                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " + ex.Message, Color.Red)

                    'Loading indicators
                    If (TypeOfSOrder = "NoSpread") Or (TypeOfSOrder = "Timer") Then
                        OrderStatus(btnSellNoSpread, lblOrderStatusShort, PBShort, "Order Error.")
                    Else TypeOfSOrder = "Limit"
                        OrderStatus(btnSell, lblOrderStatusShort, PBShort, "Order Error.")
                    End If

                End Try

            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error placing order: " + responseBody, Color.Yellow)

                'Loading indicators
                If (TypeOfSOrder = "NoSpread") Or (TypeOfSOrder = "Timer") Then
                    OrderStatus(btnSellNoSpread, lblOrderStatusShort, PBShort, "Order Error.")
                Else TypeOfSOrder = "Limit"
                    OrderStatus(btnSell, lblOrderStatusShort, PBShort, "Order Error.")
                End If

            End If
            'End If
            ' End If
            timerTopBid.Stop()
            timerTopAsk.Stop()
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)

            'Loading indicators
            If (TypeOfSOrder = "NoSpread") Or (TypeOfSOrder = "Timer") Then
                OrderStatus(btnSellNoSpread, lblOrderStatusShort, PBShort, "Order Error.")
            Else TypeOfSOrder = "Limit"
                OrderStatus(btnSell, lblOrderStatusShort, PBShort, "Order Error.")
            End If

        End Try
    End Function

    Private Async Function ExecuteMarketOrderAsync(TypeOfOrder As String) As Task
        Try

            timerTopBid.Stop()
            timerTopAsk.Stop()

            'Loading indicators
            If TypeOfOrder = "MarketBuy" Then
                OrderStatus(btnMarketLong, lblOrderStatusLong, PBLong, "Prep Payload.")
            ElseIf TypeOfOrder = "MarketSell" Then
                OrderStatus(btnMarketShort, lblOrderStatusShort, PBShort, "Prep Payload.")
            End If

            Dim amount As Decimal
            Dim entryPrice As Decimal
            Dim LtakeProfitPrice As Decimal
            Dim takeprofitprice As Decimal
            Dim LstopLossTriggerPrice As Decimal
            Dim stopLossTriggerPrice As Decimal
            Dim LstopLossPrice As Decimal
            Dim stopLossPrice As Decimal
            Dim triggeroffset As Decimal
            Dim orderDirection As String = ""
            Dim OTOCODirection As String = ""
            Dim orderPOST As String = ""

            'Ensure authentication

            'Testnet API
            'Await EnsureAuthenticationAsync("YEwHDiU5", "iGNfQP-HgSje-ECSmNUG3NT6AEETuYe9IMoXVilmAes")

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            If TypeOfOrder = "MarketBuy" Then
                ' Fetch values from textboxes and modify to fit no spread values

                amount = Decimal.Parse(txtLAmount.Text.Trim)
                entryPrice = Decimal.Parse(txtLEntryPrice.Text.Trim)

                LtakeProfitPrice = Decimal.Parse(txtLTakeProfit.Text)
                takeprofitprice = entryPrice + LtakeProfitPrice

                LstopLossTriggerPrice = Decimal.Parse(txtLTrigger.Text)
                stopLossTriggerPrice = entryPrice - LstopLossTriggerPrice

                LstopLossPrice = Decimal.Parse(txtLStopLoss.Text)
                stopLossPrice = entryPrice - LstopLossTriggerPrice - LstopLossPrice

                triggeroffset = Decimal.Parse(txtLTriggerOffset.Text.Trim)

                orderDirection = "private/buy"
                OTOCODirection = "sell"
                orderPOST = "/api/v2/private/buy"

            ElseIf TypeOfOrder = "MarketSell" Then
                amount = Decimal.Parse(txtLAmount.Text.Trim)
                entryPrice = Decimal.Parse(txtLEntryPrice.Text.Trim)

                LtakeProfitPrice = Decimal.Parse(txtLTakeProfit.Text)
                takeprofitprice = entryPrice + LtakeProfitPrice

                LstopLossTriggerPrice = Decimal.Parse(txtLTrigger.Text)
                stopLossTriggerPrice = entryPrice - LstopLossTriggerPrice

                LstopLossPrice = Decimal.Parse(txtLStopLoss.Text)
                stopLossPrice = entryPrice - LstopLossTriggerPrice - LstopLossPrice

                triggeroffset = Decimal.Parse(txtLTriggerOffset.Text.Trim)

                orderDirection = "private/sell"
                OTOCODirection = "buy"
                orderPOST = "/api/v2/private/sell"

            End If

            ' Validate that all required fields are filled
            If String.IsNullOrEmpty(entryPrice) OrElse String.IsNullOrEmpty(amount) OrElse String.IsNullOrEmpty(takeprofitprice) OrElse String.IsNullOrEmpty(stopLossTriggerPrice) OrElse String.IsNullOrEmpty(stopLossPrice) OrElse String.IsNullOrEmpty(triggeroffset) Then
                MessageBox.Show("Please fill in all required fields.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            If TypeOfOrder = "MarketBuy" Then
                PBLong.Value = 40
            ElseIf TypeOfOrder = "MarketSell" Then
                PBShort.Value = 40
            End If


            ' Prepare the payload for the linked order
            Dim payload As New JObject(
            New JProperty("jsonrpc", "2.0"),
            New JProperty("id", 3),
            New JProperty("method", orderDirection),
            New JProperty("params", New JObject(
                New JProperty("instrument_name", "BTC-PERPETUAL"),
                New JProperty("amount", amount),
                New JProperty("type", "market"),
                New JProperty("label", "EntryMarketOrder"),
                New JProperty("time_in_force", "good_til_cancelled"),
                New JProperty("linked_order_type", "one_triggers_one_cancels_other"),
                New JProperty("trigger_fill_condition", "first_hit"),
                New JProperty("otoco_config", New JArray(
                    New JObject(
                        New JProperty("amount", amount),
                        New JProperty("direction", OTOCODirection),
                        New JProperty("type", "limit"),
                        New JProperty("label", "TakeLimitProfit"),
                        New JProperty("price", takeprofitprice),
                        New JProperty("reduce_only", True),
                        New JProperty("time_in_force", "good_til_cancelled"),
                        New JProperty("post_only", True)
                    ),
                    New JObject(
                    New JProperty("amount", amount),
                    New JProperty("direction", OTOCODirection),
                    New JProperty("type", "stop_limit"),
                    New JProperty("trigger_price", stopLossTriggerPrice), ' Base trigger price
                    New JProperty("trigger_offset", triggeroffset), ' Offset for dynamic adjustment
                    New JProperty("price", stopLossPrice), ' Stop loss limit price
                    New JProperty("label", "StopLossOrder"),
                    New JProperty("reduce_only", True),
                    New JProperty("time_in_force", "good_til_cancelled"),
                    New JProperty("post_only", True),
                    New JProperty("trigger", "last_price")
                    )
                ))
            ))
        )

            If TypeOfOrder = "MarketBuy" Then
                PBLong.Value = 60
            ElseIf TypeOfOrder = "MarketSell" Then
                PBShort.Value = 60
            End If

            Dim content As New StringContent(payload.ToString, Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response = Await client.PostAsync(orderPOST, content)



            If TypeOfOrder = "MarketBuy" Then
                PBLong.Value = 80
                lblOrderStatusLong.Text = "Sending Payload."
            ElseIf TypeOfOrder = "MarketSell" Then
                PBShort.Value = 80
                lblOrderStatusShort.Text = "Sending Payload."
            End If


            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync

            If response.IsSuccessStatusCode Then


                If TypeOfOrder = "MarketBuy" Then
                    btnSellLimit.Enabled = True
                    btnMarketSell.Enabled = True
                    btnEditTPBuyPrice.Enabled = True
                    btnEditSLBuyPrice.Enabled = True
                    btnLCancelAllOpen.Enabled = True
                ElseIf TypeOfOrder = "MarketSell" Then
                    btnBuyLimit.Enabled = True
                    btnMarketBuy.Enabled = True
                    btnEditTPSellPrice.Enabled = True
                    btnEditSLSellPrice.Enabled = True
                    btnSCancelAllOpen.Enabled = True
                End If


                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)


                    'The below code is for getting OTOCO order IDs
                    '---------------------------------------------
                    ' Retrieve the order ID from the JSON response
                    primaryOrderID = json("result")("order")("order_id").Value(Of String)()

                    'Await CheckOrderStatus(primaryOrderID)

                    ' Extract the OTOCO order IDs
                    Dim otocoOrderIds = json("result")("order")("oto_order_ids").Select(Function(token) token.ToString()).ToList()

                    ' Ensure there are OTOCO orders in the response
                    If otocoOrderIds.Count >= 2 Then

                        ' Use the IdentifyOTCOrdersAsync function to correctly determine the stop loss and take profit order IDs

                        Dim otocoOrderResult = Await IdentifyOTCOrdersAsync(otocoOrderIds)

                        ' Extract the values from the tuple
                        stopLossOrderID = otocoOrderResult.Item1
                        takeProfitOrderID = otocoOrderResult.Item2

                        ' Check if both order IDs are successfully identified
                        If Not String.IsNullOrEmpty(stopLossOrderID) AndAlso Not String.IsNullOrEmpty(takeProfitOrderID) Then
                            'MessageBox.Show("Primary and linked OTOCO order IDs retrieved successfully.", "Order IDs", MessageBoxButtons.OK, MessageBoxIcon.Information)
                            txtPlacedTakeProfitBuyPrice.Text = committedTakeProfitPrice.ToString(CultureInfo.InvariantCulture)
                            txtPlacedStopLossBuyPrice.Text = committedStopLossPrice.ToString(CultureInfo.InvariantCulture)
                            txtPlacedTriggerStopBuyPrice.Text = committedTriggerPrice.ToString(CultureInfo.InvariantCulture)

                        Else
                            'RichTextBox1.AppendText("Failed to determine linked OTOCO order IDs correctly.")
                            AppendColoredText(RichTextBox1, Environment.NewLine + "OTOCO ID error @ Market Order function", Color.Yellow)
                        End If
                    Else
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Error placing order: " + responseBody, Color.Yellow)
                    End If

                    '---------------------------------------------


                    ' Navigate through the JSON to get the price value
                    Dim price = json("result")("order")("price").Value(Of Decimal)

                    If TypeOfOrder = "MarketBuy" Then
                        ' Display the price in the Placed Buy Prices TextBoxes
                        txtPlacedBuyPrice.Text = price.ToString(CultureInfo.InvariantCulture)
                        OrderStatus(btnMarketLong, lblOrderStatusLong, PBLong, "Order Placed.")
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Market Buy $" + txtPlacedBuyPrice.Text, Color.Green)

                        'Est. true profit/loss
                        Dim estProfit = ((takeprofitprice - price) / price) * amount
                        Dim estLoss = ((price - stopLossPrice) / price) * amount
                        lblLEstTrueProf.Text = estProfit.ToString("F2")
                        lblLEstTrueLoss.Text = estLoss.ToString("F2")
                    ElseIf TypeOfOrder = "MarketSell" Then
                        ' Display the price in the Placed Buy Prices TextBoxes
                        txtPlacedSellPrice.Text = price.ToString(CultureInfo.InvariantCulture)
                        OrderStatus(btnMarketShort, lblOrderStatusShort, PBShort, "Order Placed.")
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Market Sell $" + txtPlacedSellPrice.Text, Color.Green)

                        'Est. true profit/loss
                        Dim estProfit = ((price - takeprofitprice) / price) * amount
                        Dim estLoss = ((stopLossPrice - price) / price) * amount
                        lblSEstTrueProf.Text = estProfit.ToString("F2")
                        lblSEstTrueLoss.Text = estLoss.ToString("F2")
                    End If


                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " + ex.Message, Color.Red)

                    'Loading indicators
                    If TypeOfOrder = "MarketBuy" Then
                        OrderStatus(btnMarketLong, lblOrderStatusLong, PBLong, "Order Error.")
                    ElseIf TypeOfOrder = "MarketSell" Then
                        OrderStatus(btnMarketShort, lblOrderStatusShort, PBShort, "Order Error.")
                    End If

                End Try

            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error placing order: " + responseBody, Color.Yellow)

                'Loading indicators
                If TypeOfOrder = "MarketBuy" Then
                    OrderStatus(btnMarketLong, lblOrderStatusLong, PBLong, "Order Error.")
                ElseIf TypeOfOrder = "MarketSell" Then
                    OrderStatus(btnMarketShort, lblOrderStatusShort, PBShort, "Order Error.")
                End If
            End If
            '    End If
            ' End If

            timerTopBid.Stop()
            timerTopAsk.Stop()

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)

            'Loading indicators
            If TypeOfOrder = "MarketBuy" Then
                OrderStatus(btnMarketLong, lblOrderStatusLong, PBLong, "Order Error.")
            ElseIf TypeOfOrder = "MarketSell" Then
                OrderStatus(btnMarketShort, lblOrderStatusShort, PBShort, "Order Error.")
            End If

        End Try
    End Function

    '-----------------------------------------------------------------------------------------------------------------

    Private Sub txtLTakeProfit_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLTakeProfit.KeyPress, txtLTakeProfit.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            If ValidateDecimalInputAndMoveFocus(txtLTakeProfit, txtLTrigger) Then
                lblLTakeProfit.Text = txtLTakeProfit.Text
            End If

        End If
    End Sub

    Private Sub txtLTrigger_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLTrigger.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            If ValidateDecimalInputAndMoveFocus(txtLTrigger, txtLStopLoss) Then
                lblLTrigger.Text = txtLTrigger.Text
            End If
        End If
    End Sub

    Private Sub txtLStopLoss_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLStopLoss.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            If ValidateDecimalInputAndMoveFocus(txtLStopLoss, txtLAmount) Then
                lblLStopLoss.Text = txtLStopLoss.Text
            End If
        End If

    End Sub

    Private Sub txtLEntryPrice_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLEntryPrice.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            ValidateDecimalInputAndMoveFocus(txtLEntryPrice, btnBuyNoSpread)

        End If
    End Sub

    'Private Sub CalltimerEntryPriceShort()
    'timerEntryPriceShort_Tick(timerEntryPriceShort, EventArgs.Empty)
    'End Sub

    Private Sub txtSTakeProfit_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtSTakeProfit.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            If ValidateDecimalInputAndMoveFocus(txtSTakeProfit, txtSTrigger) Then

                ' Update the lblLTakeProfit with the text from txtLTakeProfit
                lblSTakeProfit.Text = txtSTakeProfit.Text
            End If
        End If
    End Sub

    Private Sub txtSTrigger_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtSTrigger.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            If ValidateDecimalInputAndMoveFocus(txtSTrigger, txtSStopLoss) Then

                ' Update the lblLTakeProfit with the text from txtLTakeProfit
                lblSTrigger.Text = txtSTrigger.Text
            End If
        End If
    End Sub

    Private Sub txtSStopLoss_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtSStopLoss.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            If ValidateDecimalInputAndMoveFocus(txtSStopLoss, txtSAmount) Then

                ' Update the lblLTakeProfit with the text from txtLTakeProfit
                lblSStopLoss.Text = txtSStopLoss.Text
            End If
        End If
    End Sub

    Private Sub txtSEntryPrice_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtSEntryPrice.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            ValidateDecimalInputAndMoveFocus(txtSEntryPrice, btnSellNoSpread)

        End If
    End Sub



    Private Sub txtLTakeProfitPrice_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLTakeProfitPrice.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            ValidateDecimalInputAndMoveFocus(txtLTakeProfitPrice, txtLTriggerPrice)

        End If
    End Sub

    Private Sub txtLTriggerPrice_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLTriggerPrice.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            ValidateDecimalInputAndMoveFocus(txtLTriggerPrice, txtLStopLossPrice)

        End If
    End Sub

    Private Sub txtLStopLossPrice_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLStopLossPrice.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            ValidateDecimalInputAndMoveFocus(txtLStopLossPrice, btnBuyNoSpread)

        End If
    End Sub

    'This is the Buy button event handler with linked orders

    Private Async Sub btnBuy_Click(sender As Object, e As EventArgs) Handles btnBuy.Click
        Await ExecuteBuyOrderAsync("Limit")
        timerTopBid.Start()
    End Sub

    'This is the Buy button event handler with linked orders where entry price is $0.50 less than lowest ask price
    Private Async Sub btnBuyNoSpread_Click(sender As Object, e As EventArgs) Handles btnBuyNoSpread.Click
        Await ExecuteBuyOrderAsync("NoSpread")
        timerTopBid.Start()
    End Sub

    'Below is a reduce-only buy limit order button
    Private Async Sub btnBuyLimit_Click(sender As Object, e As EventArgs) Handles btnBuyLimit.Click
        Try

            'Stop order placement if no open order or more than 1 open order
            Dim count As Integer = Await CheckOpenOrdersAsync()
            If count > 0 Then Await CancelAllOpenOrdersAsync()

            'Loading indicators
            OrderStatus(btnBuyLimit, lblOrderStatusLong, PBLong, "Prep Payload.")

            'Ensure authentication

            'Testnet API
            'Await EnsureAuthenticationAsync("YEwHDiU5", "iGNfQP-HgSje-ECSmNUG3NT6AEETuYe9IMoXVilmAes")

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")


            ' Fetch values from textboxes
            Dim entryPrice As String = txtLEntryPrice.Text
            Dim amount As String = txtLAmount.Text

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            PBLong.Value = 40

            ' Prepare the payload for the simple buy limit order
            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/buy"),
                New JProperty("params", New JObject(
                    New JProperty("instrument_name", "BTC-PERPETUAL"),
                    New JProperty("amount", amount),
                    New JProperty("type", "limit"),
                    New JProperty("price", entryPrice),
                    New JProperty("reduce_only", True),
                    New JProperty("time_in_force", "good_til_cancelled"),
                    New JProperty("post_only", True)
                ))
            )

            PBLong.Value = 60

            Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/buy", content)

            lblOrderStatusLong.Text = "Sending Payload."
            PBLong.Value = 80

            ' Read the response content as a string
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()

            If response.IsSuccessStatusCode Then
                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)

                    ' Navigate through the JSON to get the price value
                    Dim price = json("result")("order")("price").Value(Of Decimal)
                    txtPlacedBuyPrice.Text = price.ToString(CultureInfo.InvariantCulture)

                    ' Retrieve the order ID from the JSON response
                    primaryOrderID = json("result")("order")("order_id").Value(Of String)()

                    ' Await CheckOrderStatus(primaryOrderID)

                    'Loading indicators
                    OrderStatus(btnBuyLimit, lblOrderStatusLong, PBLong, "Order Placed.")

                    AppendColoredText(RichTextBox1, Environment.NewLine + "Red. Buy $" + txtPlacedBuyPrice.Text, Color.Green)


                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)

                    'Loading indicators
                    OrderStatus(btnBuyLimit, lblOrderStatusLong, PBLong, "Order Error.")

                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error placing limit order: " & responseBody, Color.Yellow)

                'Loading indicators
                OrderStatus(btnBuyLimit, lblOrderStatusLong, PBLong, "Order Error.")
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)

            'Loading indicators
            OrderStatus(btnBuyLimit, lblOrderStatusLong, PBLong, "Order Error.")
        End Try
    End Sub

    'This is the Sell button event handler with linked orders
    Private Async Sub btnSell_Click(sender As Object, e As EventArgs) Handles btnSell.Click
        Await ExecuteSellOrderAsync("Limit")
        timerTopAsk.Start()
    End Sub

    'This is the Sell button event handler with linked orders where entry price is $0.50 more than highest bid price
    Private Async Sub btnSellNoSpread_Click(sender As Object, e As EventArgs) Handles btnSellNoSpread.Click
        Await ExecuteSellOrderAsync("NoSpread")
        timerTopAsk.Start()
    End Sub

    'Below is a simple sell limit order button
    Private Async Sub btnSellLimit_Click(sender As Object, e As EventArgs) Handles btnSellLimit.Click
        Try

            'Stop order placement if no open order or more than 1 open order
            Dim count As Integer = Await CheckOpenOrdersAsync()
            If count > 0 Then Await CancelAllOpenOrdersAsync()


            'Loading indicators
            OrderStatus(btnSellLimit, lblOrderStatusShort, PBShort, "Prep Payload.")

            'Ensure authentication

            'Testnet API
            'Await EnsureAuthenticationAsync("YEwHDiU5", "iGNfQP-HgSje-ECSmNUG3NT6AEETuYe9IMoXVilmAes")

            'Play Account API
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")


            ' Fetch values from textboxes
            Dim entryPrice As String = txtSEntryPrice.Text
            Dim amount As String = txtSAmount.Text

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            PBShort.Value = 40

            ' Prepare the payload for the simple sell limit order
            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/sell"),
                New JProperty("params", New JObject(
                    New JProperty("instrument_name", "BTC-PERPETUAL"),
                    New JProperty("amount", amount),
                    New JProperty("type", "limit"),
                    New JProperty("price", entryPrice),
                    New JProperty("reduce_only", True),
                    New JProperty("time_in_force", "good_til_cancelled"),
                    New JProperty("post_only", True)
                ))
            )

            PBShort.Value = 60

            Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response As HttpResponseMessage = Await client.PostAsync("/api/v2/private/sell", content)

            lblOrderStatusShort.Text = "Sending Payload."
            PBShort.Value = 80

            ' Read the response content as a string
            Dim responseBody As String = Await response.Content.ReadAsStringAsync()

            If response.IsSuccessStatusCode Then
                'Loading indicators
                OrderStatus(btnSellLimit, lblOrderStatusShort, PBShort, "Order Placed.")

                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)

                    ' Navigate through the JSON to get the price value
                    Dim price = json("result")("order")("price").Value(Of Decimal)

                    ' Retrieve the order ID from the JSON response
                    primaryOrderID = json("result")("order")("order_id").Value(Of String)()

                    'Await CheckOrderStatus(primaryOrderID)

                    ' Display the price in the Placed Sell Prices TextBoxes
                    txtPlacedSellPrice.Text = price.ToString(CultureInfo.InvariantCulture)

                    AppendColoredText(RichTextBox1, Environment.NewLine + "Red.Sell $" + txtPlacedSellPrice.Text, Color.Green)

                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)

                    'Loading indicators
                    OrderStatus(btnSellLimit, lblOrderStatusShort, PBShort, "Order Error.")
                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error placing limit order: " & responseBody, Color.Yellow)

                'Loading indicators
                OrderStatus(btnSellLimit, lblOrderStatusShort, PBShort, "Order Error.")
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)

            'Loading indicators
            OrderStatus(btnSellLimit, lblOrderStatusShort, PBShort, "Order Error.")
        End Try
    End Sub

    Private Sub btnClearBuy_Click(sender As Object, e As EventArgs)
        txtPlacedBuyPrice.Clear()
    End Sub

    Private Sub btnClearSell_Click(sender As Object, e As EventArgs)
        txtPlacedSellPrice.Clear()
    End Sub

    Private Sub txtLTakeProfit_Leave(sender As Object, e As EventArgs) Handles txtLTakeProfit.Leave

        'Validate the input and move focus if valid
        If ValidateDecimalInput(txtLTakeProfit, txtLTrigger) Then
            lblLTakeProfit.Text = txtLTakeProfit.Text
        End If
    End Sub

    Private Sub txtLTrigger_Leave(sender As Object, e As EventArgs) Handles txtLTrigger.Leave
        If ValidateDecimalInput(txtLTrigger, txtLStopLoss) Then
            lblLTrigger.Text = txtLTrigger.Text
        End If
    End Sub

    Private Sub txtLStopLoss_Leave(sender As Object, e As EventArgs) Handles txtLStopLoss.Leave, txtLStopLoss.Leave
        If ValidateDecimalInput(txtLStopLoss, txtLAmount) Then
            lblLStopLoss.Text = txtLStopLoss.Text
        End If
    End Sub

    Private Sub txtSTakeProfit_Leave(sender As Object, e As EventArgs) Handles txtSTakeProfit.Leave
        'Validate the input and move focus if valid
        If ValidateDecimalInput(txtSTakeProfit, txtSTrigger) Then

            ' Update the lblLTakeProfit with the text from txtLTakeProfit
            lblSTakeProfit.Text = txtSTakeProfit.Text
        End If
    End Sub

    Private Sub txtSTrigger_Leave(sender As Object, e As EventArgs) Handles txtSTrigger.Leave
        'Validate the input and move focus if valid
        If ValidateDecimalInput(txtSTrigger, txtSStopLoss) Then

            ' Update the lblLTakeProfit with the text from txtLTakeProfit
            lblSTrigger.Text = txtSTrigger.Text
        End If
    End Sub

    Private Sub txtSStopLoss_Leave(sender As Object, e As EventArgs) Handles txtSStopLoss.Leave
        'Validate the input and move focus if valid
        If ValidateDecimalInput(txtSStopLoss, txtSAmount) Then

            ' Update the lblLTakeProfit with the text from txtLTakeProfit
            lblSStopLoss.Text = txtSStopLoss.Text
        End If
    End Sub

    Private Sub frmMainPage_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        lblLTakeProfit.Text = txtLTakeProfit.Text
        lblSTakeProfit.Text = txtSTakeProfit.Text
        lblLTrigger.Text = txtLTrigger.Text
        lblSTrigger.Text = txtSTrigger.Text
        lblLStopLoss.Text = txtLStopLoss.Text
        lblSStopLoss.Text = txtSStopLoss.Text

    End Sub

    Private Sub txtLAmount_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLAmount.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            ValidateDecimalInputAndMoveFocus(txtLAmount, txtLTakeProfitPrice)

        End If
    End Sub

    Private Sub txtSAmount_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtSAmount.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            'Validate the input and move focus if valid
            ValidateDecimalInputAndMoveFocus(txtSAmount, txtSStopLossPrice)

        End If
    End Sub

    Private Sub btnClearBuy_Click_1(sender As Object, e As EventArgs) Handles btnClearBuy.Click
        txtPlacedTakeProfitBuyPrice.Text = 0
        txtPlacedBuyPrice.Text = 0
        txtPlacedTriggerStopBuyPrice.Text = 0
        txtPlacedStopLossBuyPrice.Text = 0
        PBLong.Value = 0
        lblOrderStatusLong.ForeColor = Color.DodgerBlue
        lblOrderStatusLong.Text = "Waiting Orders."
        lblLEstTrueProf.Text = 0
        lblLEstTrueLoss.Text = 0
        lblLCurrentPL.Text = 0
        lblLCurrentPL.ForeColor = Color.Chartreuse
        AppendColoredText(RichTextBox1, Environment.NewLine + "Clear Buy", Color.Green)
        timerTopBid.Stop()
        timerTopAsk.Stop()

        If btnConnect.Text = "In Position!" Then
            AppendColoredText(RichTextBox1, Environment.NewLine + "Position Exited.", Color.Crimson)
            btnConnect.Text = "Update!"
            btnConnect.BackColor = Color.Lime
        End If

        ButtonDisabler()

    End Sub

    Private Sub btnClearSell_Click_1(sender As Object, e As EventArgs) Handles btnClearSell.Click
        txtPlacedTakeProfitSellPrice.Text = 0
        txtPlacedSellPrice.Text = 0
        txtPlacedTriggerStopSellPrice.Text = 0
        txtPlacedStopLossSellPrice.Text = 0
        PBShort.Value = 0
        lblOrderStatusShort.ForeColor = Color.DodgerBlue
        lblOrderStatusShort.Text = "Waiting Orders."
        lblSEstTrueProf.Text = 0
        lblSEstTrueLoss.Text = 0
        lblSCurrentPL.ForeColor = Color.Chartreuse
        lblSCurrentPL.Text = 0
        AppendColoredText(RichTextBox1, Environment.NewLine + "Clear Sell", Color.Green)
        timerTopBid.Stop()
        timerTopAsk.Stop()

        If btnConnect.Text = "In Position!" Then
            AppendColoredText(RichTextBox1, Environment.NewLine + "Position Exited.", Color.Crimson)
            btnConnect.Text = "Update!"
            btnConnect.BackColor = Color.Lime
        End If

        ButtonDisabler()

    End Sub

    Private Async Sub btnEditTPBuyPrice_Click(sender As Object, e As EventArgs) Handles btnEditTPBuyPrice.Click

        Try
            btnEditTPBuyPrice.Enabled = False

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Make the GET request to fetch all active orders for the instrument "BTC-PERPETUAL"
            Dim instrumentName = "BTC-PERPETUAL"
            Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name={instrumentName}")

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync

            If response.IsSuccessStatusCode Then
                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)

                    ' Clear the RichTextBox before displaying new data
                    ' orderList.Clear()

                    ' List to store order IDs
                    Dim orderIds As New List(Of String)

                    ' Loop through each active order and extract the order ID
                    For Each order In json("result")
                        Dim orderId = order("order_id").Value(Of String)
                        orderIds.Add(orderId)
                    Next

                    ' Call IdentifyOTCOrdersAsync with the list of order IDs
                    Dim orderListIDs = Await IdentifyOTCOrdersAsync(orderIds)
                    stopLossOrderID = orderListIDs.Item1
                    takeProfitOrderID = orderListIDs.Item2

                    ' Display the identified stop loss and take profit order IDs
                    'If Not String.IsNullOrEmpty(stopLossOrderID) Then
                    'orderList.AppendText(Environment.NewLine & "Stop Loss Order ID: " & stopLossOrderID)
                    'End If
                    '       If Not String.IsNullOrEmpty(takeProfitOrderID) Then
                    '      orderList.AppendText(Environment.NewLine & "Take Profit Order ID: " & takeProfitOrderID)
                    'End If

                    'Start order editing calls
                    Await EditOrdersAsync("LongTP")

                    'Loading indicators
                    OrderStatus(btnEditTPBuyPrice, lblOrderStatusLong, PBLong, "TP Order Edited.")

                    'MessageBox.Show("Order IDs retrieved and processed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)

                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error retrieving orders: " & responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try
    End Sub

    Private Async Sub btnEditSLBuyPrice_Click(sender As Object, e As EventArgs) Handles btnEditSLBuyPrice.Click
        Try
            btnEditSLBuyPrice.Enabled = False

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Make the GET request to fetch all active orders for the instrument "BTC-PERPETUAL"
            Dim instrumentName As String = "BTC-PERPETUAL"
            Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name={instrumentName}")

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync()

            If response.IsSuccessStatusCode Then
                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)

                    ' Clear the RichTextBox before displaying new data
                    'orderList.Clear()

                    ' List to store order IDs
                    Dim orderIds As New List(Of String)()

                    ' Loop through each active order and extract the order ID
                    For Each order In json("result")
                        Dim orderId = order("order_id").Value(Of String)()
                        orderIds.Add(orderId)
                    Next

                    ' Call IdentifyOTCOrdersAsync with the list of order IDs
                    Dim orderListIDs = Await IdentifyOTCOrdersAsync(orderIds)
                    stopLossOrderID = orderListIDs.Item1
                    takeProfitOrderID = orderListIDs.Item2

                    ' Display the identified stop loss and take profit order IDs
                    'If Not String.IsNullOrEmpty(stopLossOrderID) Then
                    ' orderList.AppendText(Environment.NewLine & "Stop Loss Order ID: " & stopLossOrderID)
                    ' End If
                    '        If Not String.IsNullOrEmpty(takeProfitOrderID) Then
                    '       orderList.AppendText(Environment.NewLine & "Take Profit Order ID: " & takeProfitOrderID)
                    'End If

                    'Start order editing calls
                    Await EditOrdersAsync("LongTS")

                    'Loading indicators
                    OrderStatus(btnEditSLBuyPrice, lblOrderStatusLong, PBLong, "SL Order Edited.")

                    'MessageBox.Show("Order IDs retrieved and processed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)
                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error retrieving orders: " & responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try
    End Sub

    Private Async Sub btnEditTPSellPrice_Click(sender As Object, e As EventArgs) Handles btnEditTPSellPrice.Click
        Try
            btnEditTPSellPrice.Enabled = False

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Make the GET request to fetch all active orders for the instrument "BTC-PERPETUAL"
            Dim instrumentName As String = "BTC-PERPETUAL"
            Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name={instrumentName}")

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync()

            If response.IsSuccessStatusCode Then
                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)

                    ' Clear the RichTextBox before displaying new data
                    ' orderList.Clear()

                    ' List to store order IDs
                    Dim orderIds As New List(Of String)()

                    ' Loop through each active order and extract the order ID
                    For Each order In json("result")
                        Dim orderId = order("order_id").Value(Of String)()
                        orderIds.Add(orderId)
                    Next

                    ' Call IdentifyOTCOrdersAsync with the list of order IDs
                    Dim orderListIDs = Await IdentifyOTCOrdersAsync(orderIds)
                    stopLossOrderID = orderListIDs.Item1
                    takeProfitOrderID = orderListIDs.Item2

                    ' Display the identified stop loss and take profit order IDs
                    'If Not String.IsNullOrEmpty(stopLossOrderID) Then
                    'orderList.AppendText(Environment.NewLine & "Stop Loss Order ID: " & stopLossOrderID)
                    'End If
                    '       If Not String.IsNullOrEmpty(takeProfitOrderID) Then
                    '      orderList.AppendText(Environment.NewLine & "Take Profit Order ID: " & takeProfitOrderID)
                    'End If

                    'Start order editing calls
                    Await EditOrdersAsync("ShortTP")

                    'Loading indicators
                    OrderStatus(btnEditTPSellPrice, lblOrderStatusShort, PBShort, "TP Order Edited.")

                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)
                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error retrieving orders: " & responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try
    End Sub

    Private Async Sub btnEditSLSellPrice_Click(sender As Object, e As EventArgs) Handles btnEditSLSellPrice.Click
        Try
            btnEditSLSellPrice.Enabled = False

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Make the GET request to fetch all active orders for the instrument "BTC-PERPETUAL"
            Dim instrumentName As String = "BTC-PERPETUAL"
            Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name={instrumentName}")

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync()

            If response.IsSuccessStatusCode Then
                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)

                    ' Clear the RichTextBox before displaying new data
                    'orderList.Clear()

                    ' List to store order IDs
                    Dim orderIds As New List(Of String)()

                    ' Loop through each active order and extract the order ID
                    For Each order In json("result")
                        Dim orderId = order("order_id").Value(Of String)()
                        orderIds.Add(orderId)
                    Next

                    ' Call IdentifyOTCOrdersAsync with the list of order IDs
                    Dim orderListIDs = Await IdentifyOTCOrdersAsync(orderIds)
                    stopLossOrderID = orderListIDs.Item1
                    takeProfitOrderID = orderListIDs.Item2

                    ' Display the identified stop loss and take profit order IDs
                    'If Not String.IsNullOrEmpty(stopLossOrderID) Then
                    ' orderList.AppendText(Environment.NewLine & "Stop Loss Order ID: " & stopLossOrderID)
                    ' End If
                    '        If Not String.IsNullOrEmpty(takeProfitOrderID) Then
                    '       orderList.AppendText(Environment.NewLine & "Take Profit Order ID: " & takeProfitOrderID)
                    'End If

                    'Start order editing calls
                    Await EditOrdersAsync("ShortTS")

                    'Loading indicators
                    OrderStatus(btnEditSLSellPrice, lblOrderStatusShort, PBShort, "SL Order Edited.")

                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)
                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error retrieving orders: " & responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " & ex.Message, Color.Red)
        End Try
    End Sub

    Private Async Sub btnMarketBuy_Click(sender As Object, e As EventArgs) Handles btnMarketBuy.Click
        Try

            'Stop order placement if no open order or more than 1 open order
            Dim count = Await CheckOpenOrdersAsync()
            If count > 0 Then Await CancelAllOpenOrdersAsync()


            'Loading indicators
            OrderStatus(btnMarketBuy, lblOrderStatusLong, PBLong, "Prep Payload.")

            ' Fetch the amount from the textbox
            Dim amount = txtLAmount.Text.Trim

            ' Validate that the amount is not empty
            If String.IsNullOrEmpty(amount) Then
                MessageBox.Show("Please enter the amount.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Prepare the payload for the market buy order
            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/buy"),
                New JProperty("params", New JObject(
                    New JProperty("instrument_name", "BTC-PERPETUAL"),
                    New JProperty("amount", amount),
                    New JProperty("type", "market"),
                    New JProperty("reduce_only", True)
                ))
            )

            ' Convert the payload to StringContent for the HTTP request
            Dim content As New StringContent(payload.ToString, Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response = Await client.PostAsync("/api/v2/private/buy", content)

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync

            ' Check if the request was successful
            If response.IsSuccessStatusCode Then
                'Loading indicators
                OrderStatus(btnBuyLimit, lblOrderStatusLong, PBLong, "Market Placed!")
                AppendColoredText(RichTextBox1, Environment.NewLine + "Market L. BTC " + txtLAmount.Text, Color.Green)
            Else
                'Loading indicators
                OrderStatus(btnMarketBuy, lblOrderStatusLong, PBLong, "Order Error.")
                AppendColoredText(RichTextBox1, Environment.NewLine + "Market Buy Error: " + responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            OrderStatus(btnMarketBuy, lblOrderStatusLong, PBLong, "Order Error.")
            AppendColoredText(RichTextBox1, Environment.NewLine + "Market Buy Ex.: " + ex.Message, Color.Red)
        End Try
    End Sub

    Private Async Sub btnMarketSell_Click(sender As Object, e As EventArgs) Handles btnMarketSell.Click
        Try

            'Stop order placement if no open order or more than 1 open order
            Dim count As Integer = Await CheckOpenOrdersAsync()
            If count > 0 Then Await CancelAllOpenOrdersAsync()


            'Loading indicators
            OrderStatus(btnMarketSell, lblOrderStatusShort, PBShort, "Prep Payload.")

            ' Fetch the amount from the textbox
            Dim amount = txtSAmount.Text.Trim

            ' Validate that the amount is not empty
            If String.IsNullOrEmpty(amount) Then
                MessageBox.Show("Please enter the amount.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Prepare the payload for the market sell order
            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 1),
                New JProperty("method", "private/sell"),
                New JProperty("params", New JObject(
                    New JProperty("instrument_name", "BTC-PERPETUAL"),
                    New JProperty("amount", amount),
                    New JProperty("type", "market"),
                    New JProperty("reduce_only", True)
                ))
            )

            ' Convert the payload to StringContent for the HTTP request
            Dim content As New StringContent(payload.ToString, Encoding.UTF8, "application/json")

            ' Make the POST request to place the order
            Dim response = Await client.PostAsync("/api/v2/private/sell", content)

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync()

            ' Check if the request was successful
            If response.IsSuccessStatusCode Then
                'Loading indicators
                OrderStatus(btnMarketSell, lblOrderStatusShort, PBShort, "Market Placed!")
                AppendColoredText(RichTextBox1, Environment.NewLine + "Market Sell BTC " + txtSAmount.Text, Color.Green)

            Else
                'Loading indicators
                OrderStatus(btnMarketSell, lblOrderStatusShort, PBShort, "Order Error.")
                AppendColoredText(RichTextBox1, Environment.NewLine + "Market Sell BTC error: " + responseBody, Color.Yellow)
            End If

        Catch ex As Exception
            OrderStatus(btnMarketSell, lblOrderStatusShort, PBShort, "Order Error.")
            AppendColoredText(RichTextBox1, Environment.NewLine + "Market Sell Ex.: " + ex.Message, Color.Red)
        End Try
    End Sub

    Private Async Sub btnLTrailBuy_Click(sender As Object, e As EventArgs) Handles btnLTrailBuy.Click
        Try

            'Stop order placement if no open order or more than 1 open order
            Dim count = Await CheckOpenOrdersAsync()
            If count > 0 Then Await CancelAllOpenOrdersAsync()


            'Loading indicators
            OrderStatus(btnLTrailBuy, lblOrderStatusLong, PBLong, "Prep Payload.")

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Fetch the values from the textboxes
            Dim entryPrice = Decimal.Parse(txtLEntryPrice.Text.Trim)
            Dim amount = Decimal.Parse(txtLAmount.Text.Trim)
            Dim startOffset = Decimal.Parse(txtLStartOffset.Text.Trim)

            ' Validate that all required fields are filled
            If String.IsNullOrEmpty(entryPrice) OrElse String.IsNullOrEmpty(amount) OrElse String.IsNullOrEmpty(startOffset) Then
                MessageBox.Show("Please fill in all required fields.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Set the authorization header
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            PBLong.Value = 40

            ' Prepare the payload for the limit buy order with linked trailing stop loss
            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 3),
                New JProperty("method", "private/buy"),
                New JProperty("params", New JObject(
                    New JProperty("instrument_name", "BTC-PERPETUAL"),
                    New JProperty("amount", amount),
                    New JProperty("type", "limit"),
                    New JProperty("label", "EntryLimitBuyTrail"),
                    New JProperty("price", entryPrice),
                    New JProperty("time_in_force", "good_til_cancelled"),
                    New JProperty("post_only", True),
                    New JProperty("linked_order_type", "one_triggers_other"),
                    New JProperty("trigger_fill_condition", "first_hit"),
                    New JProperty("reject_post_only", False),
                    New JProperty("otoco_config", New JArray(
                        New JObject(
                            New JProperty("amount", amount),
                            New JProperty("direction", "sell"),
                            New JProperty("type", "trailing_stop"),
                            New JProperty("label", "TraillingStopLossSell"),
                            New JProperty("trail_offset", startOffset), ' Offset for the trailing stop
                            New JProperty("trigger_offset", startOffset), ' Offset for the trailing stop
                            New JProperty("reduce_only", True),
                            New JProperty("trigger", "last_price"),
                            New JProperty("time_in_force", "good_til_cancelled")
                        )
                    ))
                ))
            )

            PBLong.Value = 60

            ' Send the request to place the order
            Dim content As New StringContent(payload.ToString, Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("/api/v2/private/buy", content)

            lblOrderStatusLong.Text = "Sending Payload."
            PBLong.Value = 80

            ' Check the response
            Dim responseBody = Await response.Content.ReadAsStringAsync
            If response.IsSuccessStatusCode Then

                'Loading indicators
                OrderStatus(btnLTrailBuy, lblOrderStatusLong, PBLong, "Order Placed.")

                btnSellLimit.Enabled = True
                btnMarketSell.Enabled = True

                txtPlacedBuyPrice.Text = entryPrice
                txtPlacedTriggerStopBuyPrice.Text = startOffset

                AppendColoredText(RichTextBox1, Environment.NewLine + "Buy T.S. $" + txtPlacedBuyPrice.Text, Color.Green)

                'Start timer to check if price rises above entry price + (comms + take profit)
                CallTimerLTrailTick()
                timerLTrail.Start()

            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Failed to place order: " & responseBody, Color.Yellow)
                OrderStatus(btnLTrailBuy, lblOrderStatusLong, PBLong, "Order Error.")
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)
            'Loading indicators
            OrderStatus(btnLTrailBuy, lblOrderStatusLong, PBLong, "Order Error.")
        End Try
    End Sub

    Private Async Sub btnSTrailSell_Click(sender As Object, e As EventArgs) Handles btnSTrailSell.Click
        Try

            'Stop order placement if no open order or more than 1 open order
            Dim count = Await CheckOpenOrdersAsync()
            If count > 0 Then Await CancelAllOpenOrdersAsync()


            'Loading indicators
            OrderStatus(btnSTrailSell, lblOrderStatusShort, PBShort, "Prep Payload.")

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Fetch the values from the textboxes
            Dim entryPrice = Decimal.Parse(txtSEntryPrice.Text.Trim)
            Dim amount = Decimal.Parse(txtSAmount.Text.Trim)
            Dim startOffset = Decimal.Parse(txtSStartOffset.Text.Trim)

            ' Validate that all required fields are filled
            If String.IsNullOrEmpty(entryPrice) OrElse String.IsNullOrEmpty(amount) OrElse String.IsNullOrEmpty(startOffset) Then
                MessageBox.Show("Please fill in all required fields.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Set the authorization header
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            PBShort.Value = 40

            ' Prepare the payload for the limit buy order with linked trailing stop loss
            Dim payload As New JObject(
                New JProperty("jsonrpc", "2.0"),
                New JProperty("id", 3),
                New JProperty("method", "private/sell"),
                New JProperty("params", New JObject(
                    New JProperty("instrument_name", "BTC-PERPETUAL"),
                    New JProperty("amount", amount),
                    New JProperty("type", "limit"),
                    New JProperty("label", "EntryLimitSellTrail"),
                    New JProperty("price", entryPrice),
                    New JProperty("time_in_force", "good_til_cancelled"),
                    New JProperty("post_only", True),
                    New JProperty("linked_order_type", "one_triggers_other"),
                    New JProperty("trigger_fill_condition", "first_hit"),
                    New JProperty("reject_post_only", False),
                    New JProperty("otoco_config", New JArray(
                        New JObject(
                            New JProperty("amount", amount),
                            New JProperty("direction", "buy"),
                            New JProperty("type", "trailing_stop"),
                            New JProperty("label", "TraillingStopLossBuy"),
                            New JProperty("trail_offset", startOffset), ' Offset for the trailing stop
                            New JProperty("trigger_offset", startOffset), ' Offset for the trailing stop
                            New JProperty("reduce_only", True),
                            New JProperty("trigger", "last_price"),
                            New JProperty("time_in_force", "good_til_cancelled")
                        )
                    ))
                ))
            )

            PBShort.Value = 60

            ' Send the request to place the order
            Dim content As New StringContent(payload.ToString, Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("/api/v2/private/sell", content)

            lblOrderStatusShort.Text = "Sending Payload."
            PBShort.Value = 80

            ' Check the response
            Dim responseBody = Await response.Content.ReadAsStringAsync
            If response.IsSuccessStatusCode Then

                'Loading indicators
                OrderStatus(btnSTrailSell, lblOrderStatusShort, PBShort, "Order Placed.")

                btnBuyLimit.Enabled = True
                btnMarketBuy.Enabled = True

                txtPlacedSellPrice.Text = entryPrice
                txtPlacedTriggerStopSellPrice.Text = startOffset

                AppendColoredText(RichTextBox1, Environment.NewLine + "Sell T.S. $" + txtPlacedSellPrice.Text, Color.Green)

                'Start timer to check if price drops below entry price - (comms + take profit)
                CallTimerSTrailTick()
                timerSTrail.Start()

            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Failed to place order: " & responseBody, Color.Yellow)
                'Loading indicators
                OrderStatus(btnSTrailSell, lblOrderStatusShort, PBShort, "Order Error.")
            End If

        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)
            'Loading indicators
            OrderStatus(btnSTrailSell, lblOrderStatusShort, PBShort, "Order Error.")
        End Try
    End Sub

    Private Async Sub btnLTPOffset_Click(sender As Object, e As EventArgs) Handles btnLTPOffset.Click
        Try
            Dim targetOffset = Decimal.Parse(txtLTPOffset.Text.Trim) + Decimal.Parse(txtLComms.Text.Trim)

            'If BTCPrice >= Decimal.Parse(txtPlacedBuyPrice.Text.Trim()) + targetOffset Then

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Make the GET request to fetch all active orders for the instrument "BTC-PERPETUAL"
            Dim instrumentName = "BTC-PERPETUAL"
            Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name={instrumentName}")

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync

            If response.IsSuccessStatusCode Then
                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)

                    ' Clear the RichTextBox before displaying new data
                    ' orderList.Clear()

                    ' List to store order IDs
                    Dim orderIds As New List(Of String)

                    ' Loop through each active order and extract the order ID
                    For Each order In json("result")
                        Dim orderId = order("order_id").Value(Of String)
                        orderIds.Add(orderId)
                    Next

                    'RichTextBox1.AppendText(Environment.NewLine + orderIds.Single)

                    TrailOrderID = orderIds.Single

                    'Start order editing calls
                    Await EditOrdersAsync("LongTrail")

                    'Loading indicators
                    '                    OrderStatus(btnEditTPBuyPrice, lblOrderStatusLong, PBLong, "TP Order Edited.")


                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " & ex.Message, Color.Red)
                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error retrieving orders: " & responseBody, Color.Yellow)
            End If
            ' End If
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)
        End Try


    End Sub

    Private Async Sub btnSTPOffset_Click(sender As Object, e As EventArgs) Handles btnSTPOffset.Click

        Try
            Dim targetOffset = Decimal.Parse(txtSTPOffset.Text.Trim) + Decimal.Parse(txtSComms.Text.Trim)

            'If BTCPrice >= Decimal.Parse(txtPlacedBuyPrice.Text.Trim()) + targetOffset Then

            ' Ensure authentication
            Await EnsureAuthenticationAsync("YZCnDmWo", "EUKusjG9fnmMgsBmPl9TmHod5Otuan8YCnaMy1DvEgA")

            ' Set the authorization header with the access token
            client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)

            ' Make the GET request to fetch all active orders for the instrument "BTC-PERPETUAL"
            Dim instrumentName = "BTC-PERPETUAL"
            Dim response = Await client.GetAsync($"/api/v2/private/get_open_orders_by_instrument?instrument_name={instrumentName}")

            ' Read the response content as a string
            Dim responseBody = Await response.Content.ReadAsStringAsync

            If response.IsSuccessStatusCode Then
                Try
                    ' Parse the JSON response
                    Dim json = JObject.Parse(responseBody)

                    ' Clear the RichTextBox before displaying new data
                    ' orderList.Clear()

                    ' List to store order IDs
                    Dim orderIds As New List(Of String)

                    ' Loop through each active order and extract the order ID
                    For Each order In json("result")
                        Dim orderId = order("order_id").Value(Of String)
                        orderIds.Add(orderId)
                    Next

                    'RichTextBox1.AppendText(Environment.NewLine + orderIds.Single)

                    TrailOrderID = orderIds.Single

                    'Start order editing calls
                    Await EditOrdersAsync("ShortTrail")

                    'Loading indicators
                    '                    OrderStatus(btnEditTPBuyPrice, lblOrderStatusLong, PBLong, "TP Order Edited.")


                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error parsing JSON: " + ex.Message, Color.Red)

                End Try
            Else
                AppendColoredText(RichTextBox1, Environment.NewLine + "Error retrieving orders: " & responseBody, Color.Yellow)
            End If
            ' End If
        Catch ex As Exception
            AppendColoredText(RichTextBox1, Environment.NewLine + "Exception: " + ex.Message, Color.Red)
        End Try

    End Sub

    Private Async Sub btnLCancelAllOpen_Click(sender As Object, e As EventArgs) Handles btnLCancelAllOpen.Click
        Dim count As Integer = Await CheckOpenOrdersAsync()
        If count > 0 Then
            Await CancelAllOpenOrdersAsync()
            AppendColoredText(RichTextBox1, Environment.NewLine + "All open orders cancelled.", Color.Green)
            timerTopBid.Stop()
            timerTopAsk.Stop()
        Else
            AppendColoredText(RichTextBox1, Environment.NewLine + "No orders found.", Color.Yellow)
        End If
    End Sub

    Private Async Sub btnSCancelAllOpen_Click(sender As Object, e As EventArgs) Handles btnSCancelAllOpen.Click
        Dim count As Integer = Await CheckOpenOrdersAsync()
        If count > 0 Then
            Await CancelAllOpenOrdersAsync()
            AppendColoredText(RichTextBox1, Environment.NewLine + "All open orders cancelled.", Color.Green)
            timerTopBid.Stop()
            timerTopAsk.Stop()
        Else
            AppendColoredText(RichTextBox1, Environment.NewLine + "No orders found.", Color.Yellow)
        End If
    End Sub

    Private isClosing As Boolean = False

    Private Sub frmMainPage_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        isClosing = True
        Try
            If Timer1.Enabled Then Timer1.Dispose()
            If timerSTrail.Enabled Then timerSTrail.Dispose()
            If timerLTrail.Enabled Then timerLTrail.Dispose()
            If timerTopBid.Enabled Then timerTopBid.Dispose()
            If timerTopAsk.Enabled Then timerTopAsk.Dispose()
            If timerLStopLoss.Enabled Then timerLStopLoss.Dispose()
            If timerSStopLoss.Enabled Then timerSStopLoss.Dispose()

            DisposeHttpClient() ' Ensure HttpClient is disposed

        Catch ex As Exception
            MessageBox.Show("Error during closing: " & ex.Message, "Closing Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            ' Ensure the application exits cleanly
            Application.Exit()
        End Try
    End Sub

    Private Async Sub btnMarketLong_Click(sender As Object, e As EventArgs) Handles btnMarketLong.Click
        Await ExecuteMarketOrderAsync("MarketBuy")
    End Sub

    Private Async Sub btnMarketShort_Click(sender As Object, e As EventArgs) Handles btnMarketShort.Click
        Await ExecuteMarketOrderAsync("MarketSell")
    End Sub

    Private Async Sub timerLStopLoss_Tick(sender As Object, e As EventArgs) Handles timerLStopLoss.Tick

        If isProcessingOrder Then Exit Sub

        'Automatically cancels order and replaces on top of bid orderbook if not at top
        Dim PlacedStopLossBuyPrice As Decimal = Decimal.Parse(txtPlacedStopLossBuyPrice.Text)
        Dim LTopAsk As Decimal = Decimal.Parse(txtSEntryPrice.Text)

        If PlacedStopLossBuyPrice > LTopAsk Then
            timerLStopLoss.Stop()
            If Await CheckOpenPosition() = True Then

                isProcessingOrder = True

                Try

                    Dim checkstatus = Await CheckOrderStatus(stopLossOrderID)
                    If checkstatus = "Untriggered" Then


                        Dim Lamount As Decimal = Decimal.Parse(txtLAmount.Text)
                        Dim openorderprice As Decimal = LTopAsk
                        Dim triggeropenorderprice As Decimal = LTopAsk

                        Await PostEditOpenOrdersAsync(stopLossOrderID, Lamount, openorderprice, triggeropenorderprice)

                        txtPlacedStopLossBuyPrice.Text = LTopAsk
                        txtPlacedTriggerStopBuyPrice.Text = LTopAsk
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Adjusted long stop loss.", Color.Green)

                    ElseIf checkstatus = "Open" Then
                        Dim Lamount As Decimal = Decimal.Parse(txtLAmount.Text)
                        Dim openorderprice As Decimal = LTopAsk

                        Await PostEditOpenOrdersAsync(stopLossOrderID, Lamount, openorderprice, 0)

                        txtPlacedStopLossBuyPrice.Text = LTopAsk
                        txtPlacedTriggerStopBuyPrice.Text = LTopAsk
                        AppendColoredText(RichTextBox1, Environment.NewLine + "Adjusted long stop loss.", Color.Green)
                    Else
                        timerLStopLoss.Stop()
                        timerSStopLoss.Stop()
                        timerTopBid.Stop()
                        timerTopAsk.Stop()

                        AppendColoredText(RichTextBox1, Environment.NewLine + "Long Position Exited.", Color.Crimson)

                        PBLong.Value = 0
                        lblOrderStatusLong.ForeColor = Color.DodgerBlue
                        lblOrderStatusLong.Text = "Waiting Orders."

                        btnConnect.Text = "Update!"
                        btnConnect.BackColor = Color.Lime

                        ButtonDisabler()

                        isProcessingOrder = False
                        Exit Sub
                    End If

                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error adjusting long stop loss.", Color.Red)
                Finally
                    isProcessingOrder = False
                End Try
            Else
                timerLStopLoss.Stop()
                timerSStopLoss.Stop()
                timerTopBid.Stop()
                timerTopAsk.Stop()

                AppendColoredText(RichTextBox1, Environment.NewLine + "Long Position Exited.", Color.Crimson)

                PBLong.Value = 0
                lblOrderStatusLong.ForeColor = Color.DodgerBlue
                lblOrderStatusLong.Text = "Waiting Orders."

                btnConnect.Text = "Update!"
                btnConnect.BackColor = Color.Lime

                ButtonDisabler()

                isProcessingOrder = False
                Exit Sub
            End If

            timerLStopLoss.Start()

        End If

    End Sub

    Private Async Sub timerSStopLoss_Tick(sender As Object, e As EventArgs) Handles timerSStopLoss.Tick

        If isProcessingOrder Then Exit Sub

        'Automatically cancels order and replaces on top of bid orderbook if not at top
        Dim PlacedStopLossSellPrice As Decimal = Decimal.Parse(txtPlacedStopLossSellPrice.Text)
        Dim LTopBid As Decimal = Decimal.Parse(txtLEntryPrice.Text)

        If PlacedStopLossSellPrice > LTopBid Then
            timerSStopLoss.Stop()
            If Await CheckOpenPosition() = True Then

                isProcessingOrder = True

                Try
                    Dim checkstatus = Await CheckOrderStatus(stopLossOrderID)
                    If checkstatus = "Untriggered" Then

                        Dim Samount As Decimal = Decimal.Parse(txtSAmount.Text)
                        Dim openorderprice As Decimal = LTopBid
                        Dim triggeropenorderprice As Decimal = LTopBid

                        Await PostEditOpenOrdersAsync(stopLossOrderID, Samount, openorderprice, triggeropenorderprice)

                        AppendColoredText(RichTextBox1, Environment.NewLine + "Adjusted short stop loss.", Color.Green)
                        txtPlacedStopLossSellPrice.Text = LTopBid
                        txtPlacedTriggerStopSellPrice.Text = LTopBid

                    ElseIf checkstatus = "Open" Then

                        Dim Samount As Decimal = Decimal.Parse(txtSAmount.Text)
                        Dim openorderprice As Decimal = LTopBid

                        Await PostEditOpenOrdersAsync(stopLossOrderID, Samount, openorderprice, 0)

                        AppendColoredText(RichTextBox1, Environment.NewLine + "Adjusted short stop loss.", Color.Green)
                        txtPlacedStopLossSellPrice.Text = LTopBid
                        txtPlacedTriggerStopSellPrice.Text = LTopBid

                    Else
                        timerSStopLoss.Stop()
                        timerLStopLoss.Stop()
                        timerTopBid.Stop()
                        timerTopAsk.Stop()

                        AppendColoredText(RichTextBox1, Environment.NewLine + "Short Position Exited.", Color.Crimson)

                        PBShort.Value = 0
                        lblOrderStatusShort.ForeColor = Color.DodgerBlue
                        lblOrderStatusShort.Text = "Waiting Orders."

                        btnConnect.Text = "Update!"
                        btnConnect.BackColor = Color.Lime

                        ButtonDisabler()

                        isProcessingOrder = False
                        Exit Sub
                    End If

                Catch ex As Exception
                    AppendColoredText(RichTextBox1, Environment.NewLine + "Error adjusting short stop loss.", Color.Red)
                Finally
                    isProcessingOrder = False
                End Try
            Else
                timerSStopLoss.Stop()
                timerLStopLoss.Stop()
                timerTopBid.Stop()
                timerTopAsk.Stop()

                AppendColoredText(RichTextBox1, Environment.NewLine + "Short Position Exited.", Color.Crimson)

                PBShort.Value = 0
                lblOrderStatusShort.ForeColor = Color.DodgerBlue
                lblOrderStatusShort.Text = "Waiting Orders."

                btnConnect.Text = "Update!"
                btnConnect.BackColor = Color.Lime

                ButtonDisabler()

                isProcessingOrder = False
                Exit Sub
            End If
            timerSStopLoss.Start()
        End If

    End Sub

    Private Sub btnClearLog_Click(sender As Object, e As EventArgs) Handles btnClearLog.Click
        RichTextBox1.Clear()
    End Sub

    Private Sub txtLTPOffset_Leave(sender As Object, e As EventArgs) Handles txtLTPOffset.Leave
        ValidateDecimalInput(txtLTPOffset, txtLComms)
    End Sub

    Private Sub txtLComms_Leave(sender As Object, e As EventArgs) Handles txtLComms.Leave
        ValidateDecimalInput(txtLComms, txtLStartOffset)
    End Sub

    Private Sub txtLStartOffset_Leave(sender As Object, e As EventArgs) Handles txtLStartOffset.Leave
        ValidateDecimalInput(txtLStartOffset, txtLAmount)
    End Sub

    Private Sub txtSTPOffset_Leave(sender As Object, e As EventArgs) Handles txtSTPOffset.Leave
        ValidateDecimalInput(txtSTPOffset, txtSComms)
    End Sub

    Private Sub txtSComms_Leave(sender As Object, e As EventArgs) Handles txtSComms.Leave
        ValidateDecimalInput(txtSComms, txtSStartOffset)
    End Sub

    Private Sub txtSStartOffset_Leave(sender As Object, e As EventArgs) Handles txtSStartOffset.Leave
        ValidateDecimalInput(txtSStartOffset, txtSAmount)
    End Sub

    Private Sub txtLTPOffset_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLTPOffset.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            ValidateDecimalInputAndMoveFocus(txtLTPOffset, txtLComms)
        End If
    End Sub

    Private Sub txtLComms_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLComms.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            ValidateDecimalInputAndMoveFocus(txtLComms, txtLStartOffset)
        End If
    End Sub

    Private Sub txtLStartOffset_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtLStartOffset.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            ValidateDecimalInputAndMoveFocus(txtLStartOffset, txtLAmount)
        End If
    End Sub

    Private Sub txtSTPOffset_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtSTPOffset.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            ValidateDecimalInputAndMoveFocus(txtSTPOffset, txtSComms)
        End If
    End Sub

    Private Sub txtSComms_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtSComms.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            ValidateDecimalInputAndMoveFocus(txtSComms, txtSStartOffset)
        End If
    End Sub

    Private Sub txtSStartOffset_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtSStartOffset.KeyPress
        ' Check if the pressed key is Enter (KeyChar = 13 is Enter)
        If e.KeyChar = ChrW(Keys.Enter) Then
            ' Prevent the beep sound on Enter key press
            e.Handled = True

            ValidateDecimalInputAndMoveFocus(txtSStartOffset, txtSAmount)
        End If
    End Sub

    ' Method to dispose of HttpClient
    Public Shared Sub DisposeHttpClient()
        If client IsNot Nothing Then
            client.Dispose()
        End If
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Try
            If Timer1.Enabled Then Timer1.Dispose()
            If timerSTrail.Enabled Then timerSTrail.Dispose()
            If timerLTrail.Enabled Then timerLTrail.Dispose()
            If timerTopBid.Enabled Then timerTopBid.Dispose()
            If timerTopAsk.Enabled Then timerTopAsk.Dispose()
            If timerLStopLoss.Enabled Then timerLStopLoss.Dispose()
            If timerSStopLoss.Enabled Then timerSStopLoss.Dispose()



            DisposeHttpClient() ' Ensure HttpClient is disposed

        Catch ex As Exception
            MessageBox.Show("Error during closing: " & ex.Message, "Closing Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            ' Ensure the application exits cleanly
            Application.Exit()
        End Try
    End Sub

    Private Async Sub timerCheckPosition_Tick(sender As Object, e As EventArgs) Handles timerCheckPosition.Tick
        If Await CheckOpenPosition() = False Then

            timerLStopLoss.Stop()
            timerSStopLoss.Stop()
            timerTopBid.Stop()
            timerTopAsk.Stop()

            AppendColoredText(RichTextBox1, Environment.NewLine + "Position Exited.", Color.Crimson)

            PBLong.Value = 0
            lblOrderStatusLong.ForeColor = Color.DodgerBlue
            lblOrderStatusLong.Text = "Waiting Orders."

            PBShort.Value = 0
            lblOrderStatusShort.ForeColor = Color.DodgerBlue
            lblOrderStatusShort.Text = "Waiting Orders."

            btnConnect.Text = "Update!"
            btnConnect.BackColor = Color.Lime

            ButtonDisabler()

            timerCheckPosition.Stop()
        End If
    End Sub

    Private Sub btnChangeForm_Click(sender As Object, e As EventArgs) Handles btnChangeForm.Click
        frmMainPageV2.Show()
        Me.Hide()
    End Sub
End Class
