<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmMainPageV2
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmMainPageV2))
        btnChangeForm = New Button()
        txtLogs = New RichTextBox()
        btnClose = New Button()
        btnClearLog = New Button()
        MarginControl = New GroupBox()
        txtMaxSlippageATR = New TextBox()
        chkMaxSlippageATR = New CheckBox()
        chkMarketStopLoss = New CheckBox()
        CustomLabel7 = New CustomLabel()
        btnMark = New Button()
        txtMarketStopLoss = New TextBox()
        btnRefreshLiveData = New Button()
        btnEstimateMargins = New Button()
        CustomLabel15 = New CustomLabel()
        CustomLabel14 = New CustomLabel()
        CustomLabel11 = New CustomLabel()
        CustomLabel10 = New CustomLabel()
        CustomLabel9 = New CustomLabel()
        CustomLabel8 = New CustomLabel()
        txtComms = New TextBox()
        txtTPOffset = New TextBox()
        txtTriggerOffset = New TextBox()
        txtStopLoss = New TextBox()
        txtTrigger = New TextBox()
        txtTakeProfit = New TextBox()
        TradeButtons = New GroupBox()
        Label1 = New Label()
        ProgressBar1 = New ProgressBar()
        btnSell = New Button()
        btnBuy = New Button()
        txtTopAsk = New CustomTextBox()
        txtTopBid = New CustomTextBox()
        btnMarket = New Button()
        btnCancelAllOpen = New Button()
        btnTrail = New Button()
        btnReduceMarket = New Button()
        btnLimit = New Button()
        btnReduceLimit = New Button()
        btnNoSpread = New Button()
        AccountInfo = New GroupBox()
        CustomLabel1 = New CustomLabel()
        CustomLabel17 = New CustomLabel()
        CustomLabel16 = New CustomLabel()
        lblUSDSession = New Label()
        lblBTCSession = New Label()
        lblUSDEquity = New Label()
        lblBTCEquity = New Label()
        lblEquiv = New Label()
        Label3 = New Label()
        lblBalance = New Label()
        Label50 = New Label()
        lblIndexPrice = New Label()
        ConnectionStatus = New GroupBox()
        radHeartBeat = New RadioButton()
        btnConnect = New Button()
        lblStatus = New Label()
        Label2 = New Label()
        PlacedOrders = New GroupBox()
        btnTPOffset = New Button()
        btnEditSLPrice = New Button()
        btnEditTPPrice = New Button()
        lblOrderStatus = New CustomLabel()
        CustomLabel3 = New CustomLabel()
        txtPlacedStopLossPrice = New CustomTextBox()
        txtPlacedTrigStopPrice = New CustomTextBox()
        txtPlacedPrice = New CustomTextBox()
        txtPlacedTakeProfitPrice = New CustomTextBox()
        lblPlacedStopLossPrice = New CustomLabel()
        lblPlacedTrigStopPrice = New CustomLabel()
        lblPlacedPrice = New CustomLabel()
        lblPlacedTakeProfitPrice = New CustomLabel()
        lblPnL = New CustomLabel()
        CustomLabel2 = New CustomLabel()
        txtAmount = New CustomTextBox()
        OrderAmount = New GroupBox()
        ManualTPSL = New GroupBox()
        CustomLabel5 = New CustomLabel()
        txtManualSL = New CustomTextBox()
        txtManualTP = New CustomTextBox()
        CustomLabel4 = New CustomLabel()
        btnViewTrades = New Button()
        lblEstimatedLiquidation = New CustomLabel()
        lblEstimatedLeverage = New CustomLabel()
        lblInitialMargin = New CustomLabel()
        lblMaintenanceMargin = New CustomLabel()
        ToolTip1 = New ToolTip(components)
        MarginControl.SuspendLayout()
        TradeButtons.SuspendLayout()
        AccountInfo.SuspendLayout()
        ConnectionStatus.SuspendLayout()
        PlacedOrders.SuspendLayout()
        OrderAmount.SuspendLayout()
        ManualTPSL.SuspendLayout()
        SuspendLayout()
        ' 
        ' btnChangeForm
        ' 
        btnChangeForm.BackColor = Color.MediumSeaGreen
        btnChangeForm.Cursor = Cursors.Hand
        btnChangeForm.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnChangeForm.Location = New Point(908, 6)
        btnChangeForm.Name = "btnChangeForm"
        btnChangeForm.Size = New Size(65, 50)
        btnChangeForm.TabIndex = 81
        btnChangeForm.Text = "V1"
        btnChangeForm.UseVisualStyleBackColor = False
        ' 
        ' txtLogs
        ' 
        txtLogs.BackColor = Color.Black
        txtLogs.BorderStyle = BorderStyle.FixedSingle
        txtLogs.CausesValidation = False
        txtLogs.Font = New Font("Calibri", 8F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        txtLogs.ForeColor = SystemColors.ButtonFace
        txtLogs.Location = New Point(759, 111)
        txtLogs.Name = "txtLogs"
        txtLogs.ScrollBars = RichTextBoxScrollBars.Vertical
        txtLogs.Size = New Size(314, 294)
        txtLogs.TabIndex = 82
        txtLogs.Text = ""
        ' 
        ' btnClose
        ' 
        btnClose.BackColor = Color.Crimson
        btnClose.Cursor = Cursors.Hand
        btnClose.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnClose.Location = New Point(976, 6)
        btnClose.Name = "btnClose"
        btnClose.Size = New Size(65, 50)
        btnClose.TabIndex = 83
        btnClose.Text = " - X -"
        btnClose.UseVisualStyleBackColor = False
        ' 
        ' btnClearLog
        ' 
        btnClearLog.BackColor = Color.DodgerBlue
        btnClearLog.Cursor = Cursors.Hand
        btnClearLog.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnClearLog.Location = New Point(759, 6)
        btnClearLog.Name = "btnClearLog"
        btnClearLog.Size = New Size(65, 50)
        btnClearLog.TabIndex = 84
        btnClearLog.Text = "Clear"
        btnClearLog.UseVisualStyleBackColor = False
        ' 
        ' MarginControl
        ' 
        MarginControl.Controls.Add(txtMaxSlippageATR)
        MarginControl.Controls.Add(chkMaxSlippageATR)
        MarginControl.Controls.Add(chkMarketStopLoss)
        MarginControl.Controls.Add(CustomLabel7)
        MarginControl.Controls.Add(btnMark)
        MarginControl.Controls.Add(txtMarketStopLoss)
        MarginControl.Controls.Add(btnRefreshLiveData)
        MarginControl.Controls.Add(btnEstimateMargins)
        MarginControl.Controls.Add(CustomLabel15)
        MarginControl.Controls.Add(CustomLabel14)
        MarginControl.Controls.Add(CustomLabel11)
        MarginControl.Controls.Add(CustomLabel10)
        MarginControl.Controls.Add(CustomLabel9)
        MarginControl.Controls.Add(CustomLabel8)
        MarginControl.Controls.Add(txtComms)
        MarginControl.Controls.Add(txtTPOffset)
        MarginControl.Controls.Add(txtTriggerOffset)
        MarginControl.Controls.Add(txtStopLoss)
        MarginControl.Controls.Add(txtTrigger)
        MarginControl.Controls.Add(txtTakeProfit)
        MarginControl.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        MarginControl.ForeColor = SystemColors.ButtonFace
        MarginControl.Location = New Point(257, 171)
        MarginControl.Name = "MarginControl"
        MarginControl.Size = New Size(496, 234)
        MarginControl.TabIndex = 97
        MarginControl.TabStop = False
        MarginControl.Text = "MARGINS"
        ' 
        ' txtMaxSlippageATR
        ' 
        txtMaxSlippageATR.BackColor = Color.Gainsboro
        txtMaxSlippageATR.BorderStyle = BorderStyle.FixedSingle
        txtMaxSlippageATR.Font = New Font("Calibri", 14F)
        txtMaxSlippageATR.Location = New Point(412, 180)
        txtMaxSlippageATR.Name = "txtMaxSlippageATR"
        txtMaxSlippageATR.Size = New Size(74, 42)
        txtMaxSlippageATR.TabIndex = 198
        txtMaxSlippageATR.Text = "0.6"
        txtMaxSlippageATR.TextAlign = HorizontalAlignment.Center
        ' 
        ' chkMaxSlippageATR
        ' 
        chkMaxSlippageATR.AutoSize = True
        chkMaxSlippageATR.BackColor = SystemColors.ActiveCaptionText
        chkMaxSlippageATR.Checked = True
        chkMaxSlippageATR.CheckState = CheckState.Checked
        chkMaxSlippageATR.Font = New Font("Calibri", 14F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        chkMaxSlippageATR.ForeColor = Color.DodgerBlue
        chkMaxSlippageATR.Location = New Point(280, 178)
        chkMaxSlippageATR.Name = "chkMaxSlippageATR"
        chkMaxSlippageATR.Size = New Size(132, 39)
        chkMaxSlippageATR.TabIndex = 197
        chkMaxSlippageATR.Text = "ATRSlip:"
        ToolTip1.SetToolTip(chkMaxSlippageATR, "Max price diff. allowed during initial order placement" & vbCrLf & "repositioning attempts. Calculated by % of ATR (Ex." & vbCrLf & "0.6 = 60% of ATR).")
        chkMaxSlippageATR.UseVisualStyleBackColor = False
        ' 
        ' chkMarketStopLoss
        ' 
        chkMarketStopLoss.AutoSize = True
        chkMarketStopLoss.BackColor = SystemColors.ActiveCaptionText
        chkMarketStopLoss.Checked = True
        chkMarketStopLoss.CheckState = CheckState.Checked
        chkMarketStopLoss.Font = New Font("Calibri", 14F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        chkMarketStopLoss.ForeColor = Color.Crimson
        chkMarketStopLoss.Location = New Point(9, 178)
        chkMarketStopLoss.Name = "chkMarketStopLoss"
        chkMarketStopLoss.Size = New Size(111, 39)
        chkMarketStopLoss.TabIndex = 196
        chkMarketStopLoss.Text = "M. SL:"
        ToolTip1.SetToolTip(chkMarketStopLoss, "Price distance allowed from the 1st placed stop loss price" & vbCrLf & "for auto stop loss repositioning. Market stop loss order" & vbCrLf & "is placed if > this price distance.")
        chkMarketStopLoss.UseVisualStyleBackColor = False
        ' 
        ' CustomLabel7
        ' 
        CustomLabel7.AutoSize = True
        CustomLabel7.Font = New Font("Calibri", 10F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        CustomLabel7.ForeColor = Color.WhiteSmoke
        CustomLabel7.Location = New Point(201, 156)
        CustomLabel7.Name = "CustomLabel7"
        CustomLabel7.Size = New Size(72, 24)
        CustomLabel7.TabIndex = 124
        CustomLabel7.Text = "SL Price"
        ToolTip1.SetToolTip(CustomLabel7, "To mark the current stop loss price as the start " & vbCrLf & "of counting the distance before placing a market " & vbCrLf & "stop loss order.")
        ' 
        ' btnMark
        ' 
        btnMark.BackColor = Color.Crimson
        btnMark.Cursor = Cursors.Hand
        btnMark.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnMark.Location = New Point(202, 177)
        btnMark.Name = "btnMark"
        btnMark.Size = New Size(68, 45)
        btnMark.TabIndex = 123
        btnMark.Text = "Mark"
        btnMark.UseVisualStyleBackColor = False
        ' 
        ' txtMarketStopLoss
        ' 
        txtMarketStopLoss.BackColor = Color.Gainsboro
        txtMarketStopLoss.BorderStyle = BorderStyle.FixedSingle
        txtMarketStopLoss.Font = New Font("Calibri", 14F)
        txtMarketStopLoss.Location = New Point(126, 180)
        txtMarketStopLoss.Name = "txtMarketStopLoss"
        txtMarketStopLoss.Size = New Size(64, 42)
        txtMarketStopLoss.TabIndex = 121
        txtMarketStopLoss.Text = "70"
        txtMarketStopLoss.TextAlign = HorizontalAlignment.Center
        ' 
        ' btnRefreshLiveData
        ' 
        btnRefreshLiveData.BackColor = Color.LimeGreen
        btnRefreshLiveData.Cursor = Cursors.Hand
        btnRefreshLiveData.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnRefreshLiveData.Location = New Point(202, 87)
        btnRefreshLiveData.Name = "btnRefreshLiveData"
        btnRefreshLiveData.Size = New Size(68, 45)
        btnRefreshLiveData.TabIndex = 120
        btnRefreshLiveData.Text = "Ref. L."
        btnRefreshLiveData.UseVisualStyleBackColor = False
        ' 
        ' btnEstimateMargins
        ' 
        btnEstimateMargins.BackColor = Color.DeepSkyBlue
        btnEstimateMargins.Cursor = Cursors.Hand
        btnEstimateMargins.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnEstimateMargins.Location = New Point(202, 42)
        btnEstimateMargins.Name = "btnEstimateMargins"
        btnEstimateMargins.Size = New Size(68, 42)
        btnEstimateMargins.TabIndex = 119
        btnEstimateMargins.Text = "Est."
        ToolTip1.SetToolTip(btnEstimateMargins, "Estimate (local calculation only) liquidation price" & vbCrLf & "and margins before placing orders.")
        btnEstimateMargins.UseVisualStyleBackColor = False
        ' 
        ' CustomLabel15
        ' 
        CustomLabel15.AutoSize = True
        CustomLabel15.Font = New Font("Calibri", 14F)
        CustomLabel15.ForeColor = Color.WhiteSmoke
        CustomLabel15.Location = New Point(276, 85)
        CustomLabel15.Name = "CustomLabel15"
        CustomLabel15.Size = New Size(115, 35)
        CustomLabel15.TabIndex = 84
        CustomLabel15.Text = "Comms.:"
        ToolTip1.SetToolTip(CustomLabel15, "Est. price distance needed to cover loss from" & vbCrLf & "comms paid for market orders.")
        ' 
        ' CustomLabel14
        ' 
        CustomLabel14.AutoSize = True
        CustomLabel14.Font = New Font("Calibri", 14F)
        CustomLabel14.ForeColor = Color.WhiteSmoke
        CustomLabel14.Location = New Point(276, 44)
        CustomLabel14.Name = "CustomLabel14"
        CustomLabel14.Size = New Size(104, 35)
        CustomLabel14.TabIndex = 83
        CustomLabel14.Text = "T.P. Off.:"
        ToolTip1.SetToolTip(CustomLabel14, resources.GetString("CustomLabel14.ToolTip"))
        ' 
        ' CustomLabel11
        ' 
        CustomLabel11.AutoSize = True
        CustomLabel11.Font = New Font("Calibri", 14F)
        CustomLabel11.ForeColor = Color.WhiteSmoke
        CustomLabel11.Location = New Point(280, 133)
        CustomLabel11.Name = "CustomLabel11"
        CustomLabel11.Size = New Size(97, 35)
        CustomLabel11.TabIndex = 82
        CustomLabel11.Text = "Trig.O.:"
        ' 
        ' CustomLabel10
        ' 
        CustomLabel10.AutoSize = True
        CustomLabel10.Font = New Font("Calibri", 14F)
        CustomLabel10.ForeColor = Color.WhiteSmoke
        CustomLabel10.Location = New Point(6, 133)
        CustomLabel10.Name = "CustomLabel10"
        CustomLabel10.Size = New Size(105, 35)
        CustomLabel10.TabIndex = 81
        CustomLabel10.Text = "S. Loss.:"
        ToolTip1.SetToolTip(CustomLabel10, "Stop Loss Price Distance from Trigger Price" & vbCrLf & "This will place a Stop Loss Limit Order from " & vbCrLf & "best price on orderbook to price distance" & vbCrLf & "(stated here) from the current trigger price." & vbCrLf)
        ' 
        ' CustomLabel9
        ' 
        CustomLabel9.AutoSize = True
        CustomLabel9.Font = New Font("Calibri", 14F)
        CustomLabel9.ForeColor = Color.WhiteSmoke
        CustomLabel9.Location = New Point(6, 85)
        CustomLabel9.Name = "CustomLabel9"
        CustomLabel9.Size = New Size(94, 35)
        CustomLabel9.TabIndex = 80
        CustomLabel9.Text = "Trig. P.:"
        ToolTip1.SetToolTip(CustomLabel9, "Price Distance from placed price for " & vbCrLf & "triggering Stop Loss limit order")
        ' 
        ' CustomLabel8
        ' 
        CustomLabel8.AutoSize = True
        CustomLabel8.Font = New Font("Calibri", 14F)
        CustomLabel8.ForeColor = Color.WhiteSmoke
        CustomLabel8.Location = New Point(6, 41)
        CustomLabel8.Name = "CustomLabel8"
        CustomLabel8.Size = New Size(94, 35)
        CustomLabel8.TabIndex = 79
        CustomLabel8.Text = "T.Prof.:"
        ToolTip1.SetToolTip(CustomLabel8, "Take Profit Limit Order Price Distance" & vbCrLf & "This places a take profit limit order at" & vbCrLf & "the price distance stated here from the " & vbCrLf & "placed order price.")
        ' 
        ' txtComms
        ' 
        txtComms.BackColor = Color.Gainsboro
        txtComms.BorderStyle = BorderStyle.FixedSingle
        txtComms.Font = New Font("Calibri", 14F)
        txtComms.Location = New Point(412, 90)
        txtComms.Name = "txtComms"
        txtComms.Size = New Size(74, 42)
        txtComms.TabIndex = 78
        txtComms.Text = "30"
        txtComms.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtTPOffset
        ' 
        txtTPOffset.BackColor = Color.Gainsboro
        txtTPOffset.BorderStyle = BorderStyle.FixedSingle
        txtTPOffset.Font = New Font("Calibri", 14F)
        txtTPOffset.Location = New Point(412, 45)
        txtTPOffset.Name = "txtTPOffset"
        txtTPOffset.Size = New Size(74, 42)
        txtTPOffset.TabIndex = 77
        txtTPOffset.Text = "60"
        txtTPOffset.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtTriggerOffset
        ' 
        txtTriggerOffset.BackColor = Color.Gainsboro
        txtTriggerOffset.BorderStyle = BorderStyle.FixedSingle
        txtTriggerOffset.Font = New Font("Calibri", 14F)
        txtTriggerOffset.Location = New Point(412, 135)
        txtTriggerOffset.Name = "txtTriggerOffset"
        txtTriggerOffset.Size = New Size(74, 42)
        txtTriggerOffset.TabIndex = 73
        txtTriggerOffset.Text = "30"
        txtTriggerOffset.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtStopLoss
        ' 
        txtStopLoss.BackColor = Color.Gainsboro
        txtStopLoss.BorderStyle = BorderStyle.FixedSingle
        txtStopLoss.Font = New Font("Calibri", 14F)
        txtStopLoss.Location = New Point(126, 133)
        txtStopLoss.Name = "txtStopLoss"
        txtStopLoss.Size = New Size(64, 42)
        txtStopLoss.TabIndex = 69
        txtStopLoss.Text = "30"
        txtStopLoss.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtTrigger
        ' 
        txtTrigger.BackColor = Color.Gainsboro
        txtTrigger.BorderStyle = BorderStyle.FixedSingle
        txtTrigger.Font = New Font("Calibri", 14F)
        txtTrigger.Location = New Point(126, 87)
        txtTrigger.Name = "txtTrigger"
        txtTrigger.Size = New Size(64, 42)
        txtTrigger.TabIndex = 68
        txtTrigger.Text = "60"
        txtTrigger.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtTakeProfit
        ' 
        txtTakeProfit.BackColor = Color.Gainsboro
        txtTakeProfit.BorderStyle = BorderStyle.FixedSingle
        txtTakeProfit.Font = New Font("Calibri", 14F)
        txtTakeProfit.Location = New Point(126, 42)
        txtTakeProfit.Name = "txtTakeProfit"
        txtTakeProfit.Size = New Size(64, 42)
        txtTakeProfit.TabIndex = 67
        txtTakeProfit.Text = "60"
        txtTakeProfit.TextAlign = HorizontalAlignment.Center
        ToolTip1.SetToolTip(txtTakeProfit, vbCrLf)
        ' 
        ' TradeButtons
        ' 
        TradeButtons.Controls.Add(Label1)
        TradeButtons.Controls.Add(ProgressBar1)
        TradeButtons.Controls.Add(btnSell)
        TradeButtons.Controls.Add(btnBuy)
        TradeButtons.Controls.Add(txtTopAsk)
        TradeButtons.Controls.Add(txtTopBid)
        TradeButtons.Controls.Add(btnMarket)
        TradeButtons.Controls.Add(btnCancelAllOpen)
        TradeButtons.Controls.Add(btnTrail)
        TradeButtons.Controls.Add(btnReduceMarket)
        TradeButtons.Controls.Add(btnLimit)
        TradeButtons.Controls.Add(btnReduceLimit)
        TradeButtons.Controls.Add(btnNoSpread)
        TradeButtons.Font = New Font("Calibri", 18F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        TradeButtons.ForeColor = SystemColors.ControlLight
        TradeButtons.Location = New Point(5, 405)
        TradeButtons.Name = "TradeButtons"
        TradeButtons.Size = New Size(533, 444)
        TradeButtons.TabIndex = 98
        TradeButtons.TabStop = False
        TradeButtons.Text = "Long"
        ' 
        ' Label1
        ' 
        Label1.Font = New Font("Calibri", 12F, FontStyle.Bold Or FontStyle.Underline, GraphicsUnit.Point, CByte(0))
        Label1.ForeColor = Color.DeepSkyBlue
        Label1.Location = New Point(400, 319)
        Label1.Name = "Label1"
        Label1.Size = New Size(116, 63)
        Label1.TabIndex = 45
        Label1.Text = "Awaiting Orders"
        ' 
        ' ProgressBar1
        ' 
        ProgressBar1.Location = New Point(399, 385)
        ProgressBar1.MarqueeAnimationSpeed = 200
        ProgressBar1.Name = "ProgressBar1"
        ProgressBar1.Size = New Size(117, 52)
        ProgressBar1.Style = ProgressBarStyle.Continuous
        ProgressBar1.TabIndex = 44
        ' 
        ' btnSell
        ' 
        btnSell.BackColor = Color.DarkRed
        btnSell.Cursor = Cursors.Hand
        btnSell.FlatAppearance.BorderColor = Color.White
        btnSell.FlatAppearance.BorderSize = 0
        btnSell.FlatStyle = FlatStyle.Popup
        btnSell.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnSell.Location = New Point(269, 50)
        btnSell.Name = "btnSell"
        btnSell.Size = New Size(256, 89)
        btnSell.TabIndex = 43
        btnSell.Text = "SELL"
        btnSell.UseVisualStyleBackColor = False
        ' 
        ' btnBuy
        ' 
        btnBuy.BackColor = Color.Lime
        btnBuy.Cursor = Cursors.Hand
        btnBuy.FlatAppearance.BorderColor = Color.White
        btnBuy.FlatAppearance.BorderSize = 2
        btnBuy.FlatStyle = FlatStyle.Flat
        btnBuy.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnBuy.ForeColor = Color.Black
        btnBuy.Location = New Point(7, 50)
        btnBuy.Name = "btnBuy"
        btnBuy.Size = New Size(254, 89)
        btnBuy.TabIndex = 42
        btnBuy.Text = "BUY"
        btnBuy.UseVisualStyleBackColor = False
        ' 
        ' txtTopAsk
        ' 
        txtTopAsk.BackColor = Color.Black
        txtTopAsk.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtTopAsk.ForeColor = Color.Crimson
        txtTopAsk.Location = New Point(297, 146)
        txtTopAsk.Name = "txtTopAsk"
        txtTopAsk.Size = New Size(200, 47)
        txtTopAsk.TabIndex = 40
        txtTopAsk.Text = "0"
        txtTopAsk.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtTopBid
        ' 
        txtTopBid.BackColor = Color.Black
        txtTopBid.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtTopBid.ForeColor = Color.MediumSeaGreen
        txtTopBid.Location = New Point(35, 146)
        txtTopBid.Name = "txtTopBid"
        txtTopBid.Size = New Size(200, 47)
        txtTopBid.TabIndex = 32
        txtTopBid.Text = "0"
        txtTopBid.TextAlign = HorizontalAlignment.Center
        ' 
        ' btnMarket
        ' 
        btnMarket.BackColor = Color.SeaGreen
        btnMarket.Cursor = Cursors.Hand
        btnMarket.FlatStyle = FlatStyle.Flat
        btnMarket.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnMarket.Location = New Point(7, 201)
        btnMarket.Name = "btnMarket"
        btnMarket.Size = New Size(125, 115)
        btnMarket.TabIndex = 31
        btnMarket.Text = "Mkt. BUY"
        btnMarket.UseVisualStyleBackColor = False
        ' 
        ' btnCancelAllOpen
        ' 
        btnCancelAllOpen.BackColor = Color.DodgerBlue
        btnCancelAllOpen.Cursor = Cursors.Hand
        btnCancelAllOpen.FlatStyle = FlatStyle.Flat
        btnCancelAllOpen.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnCancelAllOpen.ForeColor = SystemColors.ButtonFace
        btnCancelAllOpen.Location = New Point(269, 322)
        btnCancelAllOpen.Name = "btnCancelAllOpen"
        btnCancelAllOpen.Size = New Size(125, 115)
        btnCancelAllOpen.TabIndex = 30
        btnCancelAllOpen.Text = "Cancel All Open"
        btnCancelAllOpen.UseVisualStyleBackColor = False
        ' 
        ' btnTrail
        ' 
        btnTrail.BackColor = Color.ForestGreen
        btnTrail.Cursor = Cursors.Hand
        btnTrail.FlatStyle = FlatStyle.Flat
        btnTrail.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnTrail.Location = New Point(138, 201)
        btnTrail.Name = "btnTrail"
        btnTrail.Size = New Size(125, 115)
        btnTrail.TabIndex = 29
        btnTrail.Text = "Trail BUY"
        btnTrail.UseVisualStyleBackColor = False
        ' 
        ' btnReduceMarket
        ' 
        btnReduceMarket.BackColor = Color.Red
        btnReduceMarket.Cursor = Cursors.Hand
        btnReduceMarket.FlatStyle = FlatStyle.Flat
        btnReduceMarket.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnReduceMarket.Location = New Point(7, 322)
        btnReduceMarket.Name = "btnReduceMarket"
        btnReduceMarket.Size = New Size(125, 115)
        btnReduceMarket.TabIndex = 28
        btnReduceMarket.Text = "Mkt. Rdc. Sell"
        btnReduceMarket.UseVisualStyleBackColor = False
        ' 
        ' btnLimit
        ' 
        btnLimit.BackColor = Color.DarkGreen
        btnLimit.Cursor = Cursors.Hand
        btnLimit.FlatStyle = FlatStyle.Flat
        btnLimit.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnLimit.Location = New Point(400, 201)
        btnLimit.Name = "btnLimit"
        btnLimit.Size = New Size(125, 115)
        btnLimit.TabIndex = 21
        btnLimit.Text = "Limit BUY"
        btnLimit.UseVisualStyleBackColor = False
        ' 
        ' btnReduceLimit
        ' 
        btnReduceLimit.BackColor = Color.DarkOrange
        btnReduceLimit.Cursor = Cursors.Hand
        btnReduceLimit.FlatStyle = FlatStyle.Flat
        btnReduceLimit.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnReduceLimit.Location = New Point(138, 322)
        btnReduceLimit.Name = "btnReduceLimit"
        btnReduceLimit.Size = New Size(125, 115)
        btnReduceLimit.TabIndex = 20
        btnReduceLimit.Text = "Reduce Sell"
        btnReduceLimit.UseVisualStyleBackColor = False
        ' 
        ' btnNoSpread
        ' 
        btnNoSpread.BackColor = Color.Green
        btnNoSpread.Cursor = Cursors.Hand
        btnNoSpread.FlatStyle = FlatStyle.Flat
        btnNoSpread.Font = New Font("Calibri", 14F, FontStyle.Bold)
        btnNoSpread.Location = New Point(269, 201)
        btnNoSpread.Name = "btnNoSpread"
        btnNoSpread.Size = New Size(125, 116)
        btnNoSpread.TabIndex = 9
        btnNoSpread.Text = "No Sprd. Buy"
        btnNoSpread.UseVisualStyleBackColor = False
        ' 
        ' AccountInfo
        ' 
        AccountInfo.Controls.Add(CustomLabel1)
        AccountInfo.Controls.Add(CustomLabel17)
        AccountInfo.Controls.Add(CustomLabel16)
        AccountInfo.Controls.Add(lblUSDSession)
        AccountInfo.Controls.Add(lblBTCSession)
        AccountInfo.Controls.Add(lblUSDEquity)
        AccountInfo.Controls.Add(lblBTCEquity)
        AccountInfo.Controls.Add(lblEquiv)
        AccountInfo.Controls.Add(Label3)
        AccountInfo.Controls.Add(lblBalance)
        AccountInfo.Controls.Add(Label50)
        AccountInfo.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        AccountInfo.ForeColor = SystemColors.ButtonFace
        AccountInfo.Location = New Point(257, 6)
        AccountInfo.Name = "AccountInfo"
        AccountInfo.Size = New Size(496, 166)
        AccountInfo.TabIndex = 103
        AccountInfo.TabStop = False
        AccountInfo.Text = "ACCOUNT INFO"
        ' 
        ' CustomLabel1
        ' 
        CustomLabel1.AutoSize = True
        CustomLabel1.Font = New Font("Calibri", 14F)
        CustomLabel1.ForeColor = Color.WhiteSmoke
        CustomLabel1.Location = New Point(6, 124)
        CustomLabel1.Name = "CustomLabel1"
        CustomLabel1.Size = New Size(150, 35)
        CustomLabel1.TabIndex = 113
        CustomLabel1.Text = "Session P/L:"
        ToolTip1.SetToolTip(CustomLabel1, "Session = 24 hours")
        ' 
        ' CustomLabel17
        ' 
        CustomLabel17.AutoSize = True
        CustomLabel17.Font = New Font("Calibri", 14F)
        CustomLabel17.ForeColor = Color.WhiteSmoke
        CustomLabel17.Location = New Point(6, 89)
        CustomLabel17.Name = "CustomLabel17"
        CustomLabel17.Size = New Size(95, 35)
        CustomLabel17.TabIndex = 112
        CustomLabel17.Text = "Equity:"
        ' 
        ' CustomLabel16
        ' 
        CustomLabel16.AutoSize = True
        CustomLabel16.Font = New Font("Calibri", 14F)
        CustomLabel16.ForeColor = Color.WhiteSmoke
        CustomLabel16.Location = New Point(6, 54)
        CustomLabel16.Name = "CustomLabel16"
        CustomLabel16.Size = New Size(111, 35)
        CustomLabel16.TabIndex = 111
        CustomLabel16.Text = "Balance:"
        ' 
        ' lblUSDSession
        ' 
        lblUSDSession.AutoSize = True
        lblUSDSession.Font = New Font("Calibri", 14F)
        lblUSDSession.ForeColor = SystemColors.ControlLight
        lblUSDSession.Location = New Point(360, 127)
        lblUSDSession.Name = "lblUSDSession"
        lblUSDSession.Size = New Size(29, 35)
        lblUSDSession.TabIndex = 109
        lblUSDSession.Text = "0"
        ' 
        ' lblBTCSession
        ' 
        lblBTCSession.AutoSize = True
        lblBTCSession.Font = New Font("Calibri", 14F)
        lblBTCSession.ForeColor = SystemColors.ControlLight
        lblBTCSession.Location = New Point(192, 127)
        lblBTCSession.Name = "lblBTCSession"
        lblBTCSession.Size = New Size(29, 35)
        lblBTCSession.TabIndex = 108
        lblBTCSession.Text = "0"
        ' 
        ' lblUSDEquity
        ' 
        lblUSDEquity.AutoSize = True
        lblUSDEquity.Font = New Font("Calibri", 14F)
        lblUSDEquity.ForeColor = SystemColors.ControlLight
        lblUSDEquity.Location = New Point(360, 92)
        lblUSDEquity.Name = "lblUSDEquity"
        lblUSDEquity.Size = New Size(29, 35)
        lblUSDEquity.TabIndex = 107
        lblUSDEquity.Text = "0"
        ' 
        ' lblBTCEquity
        ' 
        lblBTCEquity.AutoSize = True
        lblBTCEquity.Font = New Font("Calibri", 14F)
        lblBTCEquity.ForeColor = SystemColors.ControlLight
        lblBTCEquity.Location = New Point(192, 91)
        lblBTCEquity.Name = "lblBTCEquity"
        lblBTCEquity.Size = New Size(29, 35)
        lblBTCEquity.TabIndex = 106
        lblBTCEquity.Text = "0"
        ' 
        ' lblEquiv
        ' 
        lblEquiv.AutoSize = True
        lblEquiv.Font = New Font("Calibri", 14F)
        lblEquiv.ForeColor = SystemColors.ControlLight
        lblEquiv.Location = New Point(360, 60)
        lblEquiv.Name = "lblEquiv"
        lblEquiv.Size = New Size(29, 35)
        lblEquiv.TabIndex = 105
        lblEquiv.Text = "0"
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Font = New Font("Calibri", 14F, FontStyle.Bold Or FontStyle.Underline)
        Label3.ForeColor = SystemColors.ControlLight
        Label3.Location = New Point(359, 21)
        Label3.Name = "Label3"
        Label3.Size = New Size(135, 35)
        Label3.TabIndex = 104
        Label3.Text = "USD Value"
        ' 
        ' lblBalance
        ' 
        lblBalance.AutoSize = True
        lblBalance.Font = New Font("Calibri", 14F)
        lblBalance.ForeColor = SystemColors.ControlLight
        lblBalance.Location = New Point(192, 56)
        lblBalance.Name = "lblBalance"
        lblBalance.Size = New Size(29, 35)
        lblBalance.TabIndex = 103
        lblBalance.Text = "0"
        ' 
        ' Label50
        ' 
        Label50.AutoSize = True
        Label50.Font = New Font("Calibri", 14F, FontStyle.Bold Or FontStyle.Underline)
        Label50.ForeColor = SystemColors.ControlLight
        Label50.Location = New Point(193, 19)
        Label50.Name = "Label50"
        Label50.Size = New Size(58, 35)
        Label50.TabIndex = 110
        Label50.Text = "BTC"
        ' 
        ' lblIndexPrice
        ' 
        lblIndexPrice.Font = New Font("Calibri", 18F, FontStyle.Bold Or FontStyle.Underline, GraphicsUnit.Point, CByte(0))
        lblIndexPrice.ForeColor = Color.MediumSeaGreen
        lblIndexPrice.Location = New Point(51, 6)
        lblIndexPrice.Name = "lblIndexPrice"
        lblIndexPrice.Size = New Size(188, 54)
        lblIndexPrice.TabIndex = 104
        lblIndexPrice.Text = "0"
        ' 
        ' ConnectionStatus
        ' 
        ConnectionStatus.Controls.Add(radHeartBeat)
        ConnectionStatus.Controls.Add(btnConnect)
        ConnectionStatus.Controls.Add(lblStatus)
        ConnectionStatus.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        ConnectionStatus.ForeColor = SystemColors.ButtonFace
        ConnectionStatus.Location = New Point(5, 112)
        ConnectionStatus.Name = "ConnectionStatus"
        ConnectionStatus.Size = New Size(246, 164)
        ConnectionStatus.TabIndex = 106
        ConnectionStatus.TabStop = False
        ConnectionStatus.Text = "Connection Status"
        ' 
        ' radHeartBeat
        ' 
        radHeartBeat.Appearance = Appearance.Button
        radHeartBeat.BackColor = Color.Black
        radHeartBeat.FlatAppearance.BorderSize = 0
        radHeartBeat.FlatStyle = FlatStyle.Flat
        radHeartBeat.ForeColor = Color.Black
        radHeartBeat.Location = New Point(28, 36)
        radHeartBeat.Name = "radHeartBeat"
        radHeartBeat.Size = New Size(26, 26)
        radHeartBeat.TabIndex = 112
        radHeartBeat.UseVisualStyleBackColor = False
        ' 
        ' btnConnect
        ' 
        btnConnect.BackColor = Color.DodgerBlue
        btnConnect.Cursor = Cursors.Hand
        btnConnect.FlatStyle = FlatStyle.Popup
        btnConnect.Font = New Font("Calibri", 16F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnConnect.ForeColor = SystemColors.ControlText
        btnConnect.Location = New Point(28, 75)
        btnConnect.Name = "btnConnect"
        btnConnect.Size = New Size(187, 68)
        btnConnect.TabIndex = 107
        btnConnect.Text = "Connect!"
        btnConnect.UseVisualStyleBackColor = False
        ' 
        ' lblStatus
        ' 
        lblStatus.Font = New Font("Calibri", 14F, FontStyle.Bold)
        lblStatus.ForeColor = Color.Red
        lblStatus.Location = New Point(54, 33)
        lblStatus.Name = "lblStatus"
        lblStatus.Size = New Size(142, 39)
        lblStatus.TabIndex = 106
        lblStatus.Text = "Websocket"
        lblStatus.TextAlign = ContentAlignment.TopCenter
        ' 
        ' Label2
        ' 
        Label2.Font = New Font("Calibri", 18F, FontStyle.Bold Or FontStyle.Underline, GraphicsUnit.Point, CByte(0))
        Label2.ForeColor = Color.MediumSeaGreen
        Label2.Location = New Point(24, 6)
        Label2.Name = "Label2"
        Label2.Size = New Size(39, 54)
        Label2.TabIndex = 107
        Label2.Text = "$"
        ' 
        ' PlacedOrders
        ' 
        PlacedOrders.Controls.Add(btnTPOffset)
        PlacedOrders.Controls.Add(btnEditSLPrice)
        PlacedOrders.Controls.Add(btnEditTPPrice)
        PlacedOrders.Controls.Add(lblOrderStatus)
        PlacedOrders.Controls.Add(CustomLabel3)
        PlacedOrders.Controls.Add(txtPlacedStopLossPrice)
        PlacedOrders.Controls.Add(txtPlacedTrigStopPrice)
        PlacedOrders.Controls.Add(txtPlacedPrice)
        PlacedOrders.Controls.Add(txtPlacedTakeProfitPrice)
        PlacedOrders.Controls.Add(lblPlacedStopLossPrice)
        PlacedOrders.Controls.Add(lblPlacedTrigStopPrice)
        PlacedOrders.Controls.Add(lblPlacedPrice)
        PlacedOrders.Controls.Add(lblPlacedTakeProfitPrice)
        PlacedOrders.Controls.Add(lblPnL)
        PlacedOrders.Controls.Add(CustomLabel2)
        PlacedOrders.Font = New Font("Calibri", 16F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        PlacedOrders.ForeColor = Color.WhiteSmoke
        PlacedOrders.Location = New Point(544, 502)
        PlacedOrders.Name = "PlacedOrders"
        PlacedOrders.Size = New Size(529, 347)
        PlacedOrders.TabIndex = 109
        PlacedOrders.TabStop = False
        PlacedOrders.Text = "Placed Long"
        ' 
        ' btnTPOffset
        ' 
        btnTPOffset.BackColor = Color.FromArgb(CByte(255), CByte(128), CByte(0))
        btnTPOffset.Cursor = Cursors.Hand
        btnTPOffset.Font = New Font("Calibri", 10F, FontStyle.Bold)
        btnTPOffset.ForeColor = SystemColors.ActiveCaptionText
        btnTPOffset.Location = New Point(379, 247)
        btnTPOffset.Name = "btnTPOffset"
        btnTPOffset.Size = New Size(107, 47)
        btnTPOffset.TabIndex = 56
        btnTPOffset.Text = "TS"
        btnTPOffset.UseVisualStyleBackColor = False
        ' 
        ' btnEditSLPrice
        ' 
        btnEditSLPrice.BackColor = Color.Crimson
        btnEditSLPrice.Cursor = Cursors.Hand
        btnEditSLPrice.Font = New Font("Calibri", 10F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        btnEditSLPrice.ForeColor = SystemColors.ControlLight
        btnEditSLPrice.Location = New Point(379, 194)
        btnEditSLPrice.Name = "btnEditSLPrice"
        btnEditSLPrice.Size = New Size(107, 47)
        btnEditSLPrice.TabIndex = 55
        btnEditSLPrice.Text = "Edit T.S."
        btnEditSLPrice.UseVisualStyleBackColor = False
        ' 
        ' btnEditTPPrice
        ' 
        btnEditTPPrice.BackColor = Color.DeepSkyBlue
        btnEditTPPrice.Cursor = Cursors.Hand
        btnEditTPPrice.Font = New Font("Calibri", 10F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        btnEditTPPrice.ForeColor = SystemColors.ControlLight
        btnEditTPPrice.Location = New Point(379, 93)
        btnEditTPPrice.Name = "btnEditTPPrice"
        btnEditTPPrice.Size = New Size(107, 47)
        btnEditTPPrice.TabIndex = 54
        btnEditTPPrice.Text = "Edit T.P."
        btnEditTPPrice.UseVisualStyleBackColor = False
        ' 
        ' lblOrderStatus
        ' 
        lblOrderStatus.AutoSize = True
        lblOrderStatus.FlatStyle = FlatStyle.Flat
        lblOrderStatus.Font = New Font("Calibri", 14F)
        lblOrderStatus.ForeColor = Color.DeepSkyBlue
        lblOrderStatus.Location = New Point(173, 43)
        lblOrderStatus.Name = "lblOrderStatus"
        lblOrderStatus.Size = New Size(199, 35)
        lblOrderStatus.TabIndex = 11
        lblOrderStatus.Text = "Awaiting Orders"
        ' 
        ' CustomLabel3
        ' 
        CustomLabel3.AutoSize = True
        CustomLabel3.Font = New Font("Calibri", 14F)
        CustomLabel3.ForeColor = Color.WhiteSmoke
        CustomLabel3.Location = New Point(48, 43)
        CustomLabel3.Name = "CustomLabel3"
        CustomLabel3.Size = New Size(113, 35)
        CustomLabel3.TabIndex = 10
        CustomLabel3.Text = "STATUS :"
        ' 
        ' txtPlacedStopLossPrice
        ' 
        txtPlacedStopLossPrice.BackColor = Color.WhiteSmoke
        txtPlacedStopLossPrice.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtPlacedStopLossPrice.ForeColor = SystemColors.WindowText
        txtPlacedStopLossPrice.Location = New Point(173, 248)
        txtPlacedStopLossPrice.Name = "txtPlacedStopLossPrice"
        txtPlacedStopLossPrice.Size = New Size(200, 47)
        txtPlacedStopLossPrice.TabIndex = 9
        txtPlacedStopLossPrice.Text = "0"
        txtPlacedStopLossPrice.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtPlacedTrigStopPrice
        ' 
        txtPlacedTrigStopPrice.BackColor = Color.WhiteSmoke
        txtPlacedTrigStopPrice.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtPlacedTrigStopPrice.ForeColor = SystemColors.WindowText
        txtPlacedTrigStopPrice.Location = New Point(173, 197)
        txtPlacedTrigStopPrice.Name = "txtPlacedTrigStopPrice"
        txtPlacedTrigStopPrice.Size = New Size(200, 47)
        txtPlacedTrigStopPrice.TabIndex = 8
        txtPlacedTrigStopPrice.Text = "0"
        txtPlacedTrigStopPrice.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtPlacedPrice
        ' 
        txtPlacedPrice.BackColor = Color.WhiteSmoke
        txtPlacedPrice.Enabled = False
        txtPlacedPrice.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtPlacedPrice.ForeColor = SystemColors.MenuHighlight
        txtPlacedPrice.Location = New Point(173, 146)
        txtPlacedPrice.Name = "txtPlacedPrice"
        txtPlacedPrice.Size = New Size(200, 47)
        txtPlacedPrice.TabIndex = 7
        txtPlacedPrice.Text = "0"
        txtPlacedPrice.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtPlacedTakeProfitPrice
        ' 
        txtPlacedTakeProfitPrice.BackColor = Color.WhiteSmoke
        txtPlacedTakeProfitPrice.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtPlacedTakeProfitPrice.ForeColor = SystemColors.WindowText
        txtPlacedTakeProfitPrice.Location = New Point(173, 95)
        txtPlacedTakeProfitPrice.Name = "txtPlacedTakeProfitPrice"
        txtPlacedTakeProfitPrice.Size = New Size(200, 47)
        txtPlacedTakeProfitPrice.TabIndex = 6
        txtPlacedTakeProfitPrice.Text = "0"
        txtPlacedTakeProfitPrice.TextAlign = HorizontalAlignment.Center
        ' 
        ' lblPlacedStopLossPrice
        ' 
        lblPlacedStopLossPrice.AutoSize = True
        lblPlacedStopLossPrice.Font = New Font("Calibri", 14F)
        lblPlacedStopLossPrice.ForeColor = Color.WhiteSmoke
        lblPlacedStopLossPrice.Location = New Point(22, 254)
        lblPlacedStopLossPrice.Name = "lblPlacedStopLossPrice"
        lblPlacedStopLossPrice.Size = New Size(136, 35)
        lblPlacedStopLossPrice.TabIndex = 4
        lblPlacedStopLossPrice.Text = "Stop Loss :"
        ' 
        ' lblPlacedTrigStopPrice
        ' 
        lblPlacedTrigStopPrice.AutoSize = True
        lblPlacedTrigStopPrice.Font = New Font("Calibri", 14F)
        lblPlacedTrigStopPrice.ForeColor = Color.WhiteSmoke
        lblPlacedTrigStopPrice.Location = New Point(24, 201)
        lblPlacedTrigStopPrice.Name = "lblPlacedTrigStopPrice"
        lblPlacedTrigStopPrice.Size = New Size(135, 35)
        lblPlacedTrigStopPrice.TabIndex = 3
        lblPlacedTrigStopPrice.Text = "Trig. Stop :"
        ' 
        ' lblPlacedPrice
        ' 
        lblPlacedPrice.AutoSize = True
        lblPlacedPrice.Font = New Font("Calibri", 14F)
        lblPlacedPrice.ForeColor = Color.WhiteSmoke
        lblPlacedPrice.Location = New Point(21, 150)
        lblPlacedPrice.Name = "lblPlacedPrice"
        lblPlacedPrice.Size = New Size(139, 35)
        lblPlacedPrice.TabIndex = 2
        lblPlacedPrice.Text = "Entry Buy :"
        ' 
        ' lblPlacedTakeProfitPrice
        ' 
        lblPlacedTakeProfitPrice.AutoSize = True
        lblPlacedTakeProfitPrice.Font = New Font("Calibri", 14F)
        lblPlacedTakeProfitPrice.ForeColor = Color.WhiteSmoke
        lblPlacedTakeProfitPrice.Location = New Point(12, 97)
        lblPlacedTakeProfitPrice.Name = "lblPlacedTakeProfitPrice"
        lblPlacedTakeProfitPrice.Size = New Size(149, 35)
        lblPlacedTakeProfitPrice.TabIndex = 1
        lblPlacedTakeProfitPrice.Text = "Take Profit :"
        ' 
        ' lblPnL
        ' 
        lblPnL.AutoSize = True
        lblPnL.Font = New Font("Calibri", 16F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblPnL.ForeColor = Color.Chartreuse
        lblPnL.Location = New Point(257, 303)
        lblPnL.Name = "lblPnL"
        lblPnL.Size = New Size(33, 39)
        lblPnL.TabIndex = 5
        lblPnL.Text = "0"
        ' 
        ' CustomLabel2
        ' 
        CustomLabel2.AutoSize = True
        CustomLabel2.Font = New Font("Calibri", 14F)
        CustomLabel2.ForeColor = Color.WhiteSmoke
        CustomLabel2.Location = New Point(93, 303)
        CustomLabel2.Name = "CustomLabel2"
        CustomLabel2.Size = New Size(65, 35)
        CustomLabel2.TabIndex = 0
        CustomLabel2.Text = "P/L :"
        ' 
        ' txtAmount
        ' 
        txtAmount.BackColor = Color.WhiteSmoke
        txtAmount.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtAmount.ForeColor = SystemColors.WindowText
        txtAmount.Location = New Point(22, 49)
        txtAmount.Name = "txtAmount"
        txtAmount.Size = New Size(200, 47)
        txtAmount.TabIndex = 112
        txtAmount.Text = "10"
        txtAmount.TextAlign = HorizontalAlignment.Center
        ' 
        ' OrderAmount
        ' 
        OrderAmount.Controls.Add(txtAmount)
        OrderAmount.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        OrderAmount.ForeColor = SystemColors.ButtonFace
        OrderAmount.Location = New Point(5, 280)
        OrderAmount.Name = "OrderAmount"
        OrderAmount.Size = New Size(246, 125)
        OrderAmount.TabIndex = 108
        OrderAmount.TabStop = False
        OrderAmount.Text = "AMOUNT($)"
        ' 
        ' ManualTPSL
        ' 
        ManualTPSL.Controls.Add(CustomLabel5)
        ManualTPSL.Controls.Add(txtManualSL)
        ManualTPSL.Controls.Add(txtManualTP)
        ManualTPSL.Controls.Add(CustomLabel4)
        ManualTPSL.ForeColor = SystemColors.ControlLight
        ManualTPSL.Location = New Point(545, 413)
        ManualTPSL.Name = "ManualTPSL"
        ManualTPSL.Size = New Size(528, 95)
        ManualTPSL.TabIndex = 114
        ManualTPSL.TabStop = False
        ' 
        ' CustomLabel5
        ' 
        CustomLabel5.AutoSize = True
        CustomLabel5.Font = New Font("Calibri", 12F)
        CustomLabel5.ForeColor = Color.WhiteSmoke
        CustomLabel5.Location = New Point(316, 17)
        CustomLabel5.Name = "CustomLabel5"
        CustomLabel5.Size = New Size(104, 29)
        CustomLabel5.TabIndex = 117
        CustomLabel5.Text = "Stop Loss"
        ' 
        ' txtManualSL
        ' 
        txtManualSL.BackColor = Color.WhiteSmoke
        txtManualSL.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtManualSL.ForeColor = SystemColors.WindowText
        txtManualSL.Location = New Point(269, 45)
        txtManualSL.Name = "txtManualSL"
        txtManualSL.Size = New Size(200, 47)
        txtManualSL.TabIndex = 115
        txtManualSL.Text = "0"
        txtManualSL.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtManualTP
        ' 
        txtManualTP.BackColor = Color.WhiteSmoke
        txtManualTP.Font = New Font("Calibri", 16F, FontStyle.Bold)
        txtManualTP.ForeColor = SystemColors.WindowText
        txtManualTP.Location = New Point(63, 45)
        txtManualTP.Name = "txtManualTP"
        txtManualTP.Size = New Size(200, 47)
        txtManualTP.TabIndex = 114
        txtManualTP.Text = "0"
        txtManualTP.TextAlign = HorizontalAlignment.Center
        ' 
        ' CustomLabel4
        ' 
        CustomLabel4.AutoSize = True
        CustomLabel4.Font = New Font("Calibri", 12F)
        CustomLabel4.ForeColor = Color.WhiteSmoke
        CustomLabel4.Location = New Point(108, 17)
        CustomLabel4.Name = "CustomLabel4"
        CustomLabel4.Size = New Size(116, 29)
        CustomLabel4.TabIndex = 116
        CustomLabel4.Text = "Take Profit"
        ' 
        ' btnViewTrades
        ' 
        btnViewTrades.BackColor = Color.DeepSkyBlue
        btnViewTrades.Cursor = Cursors.Hand
        btnViewTrades.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnViewTrades.Location = New Point(826, 6)
        btnViewTrades.Name = "btnViewTrades"
        btnViewTrades.Size = New Size(80, 50)
        btnViewTrades.TabIndex = 115
        btnViewTrades.Text = "Results"
        btnViewTrades.UseVisualStyleBackColor = False
        ' 
        ' lblEstimatedLiquidation
        ' 
        lblEstimatedLiquidation.AutoSize = True
        lblEstimatedLiquidation.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblEstimatedLiquidation.ForeColor = Color.Gray
        lblEstimatedLiquidation.Location = New Point(759, 59)
        lblEstimatedLiquidation.Name = "lblEstimatedLiquidation"
        lblEstimatedLiquidation.Size = New Size(113, 24)
        lblEstimatedLiquidation.TabIndex = 114
        lblEstimatedLiquidation.Text = "Est. Liq: N/A"
        ' 
        ' lblEstimatedLeverage
        ' 
        lblEstimatedLeverage.AutoSize = True
        lblEstimatedLeverage.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblEstimatedLeverage.ForeColor = Color.Gray
        lblEstimatedLeverage.Location = New Point(759, 83)
        lblEstimatedLeverage.Name = "lblEstimatedLeverage"
        lblEstimatedLeverage.Size = New Size(85, 24)
        lblEstimatedLeverage.TabIndex = 116
        lblEstimatedLeverage.Text = "Lev.: N/A"
        ' 
        ' lblInitialMargin
        ' 
        lblInitialMargin.AutoSize = True
        lblInitialMargin.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblInitialMargin.ForeColor = Color.Gray
        lblInitialMargin.Location = New Point(924, 60)
        lblInitialMargin.Name = "lblInitialMargin"
        lblInitialMargin.Size = New Size(76, 24)
        lblInitialMargin.TabIndex = 117
        lblInitialMargin.Text = "IM: N/A"
        ' 
        ' lblMaintenanceMargin
        ' 
        lblMaintenanceMargin.AutoSize = True
        lblMaintenanceMargin.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblMaintenanceMargin.ForeColor = Color.Gray
        lblMaintenanceMargin.Location = New Point(924, 84)
        lblMaintenanceMargin.Name = "lblMaintenanceMargin"
        lblMaintenanceMargin.Size = New Size(88, 24)
        lblMaintenanceMargin.TabIndex = 118
        lblMaintenanceMargin.Text = "MM: N/A"
        ' 
        ' frmMainPageV2
        ' 
        AutoScaleMode = AutoScaleMode.None
        BackColor = SystemColors.Desktop
        ClientSize = New Size(1080, 856)
        Controls.Add(TradeButtons)
        Controls.Add(OrderAmount)
        Controls.Add(lblMaintenanceMargin)
        Controls.Add(lblInitialMargin)
        Controls.Add(lblEstimatedLeverage)
        Controls.Add(lblEstimatedLiquidation)
        Controls.Add(btnViewTrades)
        Controls.Add(PlacedOrders)
        Controls.Add(lblIndexPrice)
        Controls.Add(Label2)
        Controls.Add(ConnectionStatus)
        Controls.Add(txtLogs)
        Controls.Add(MarginControl)
        Controls.Add(AccountInfo)
        Controls.Add(btnClearLog)
        Controls.Add(btnClose)
        Controls.Add(btnChangeForm)
        Controls.Add(ManualTPSL)
        Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        Name = "frmMainPageV2"
        Text = "Deribit Order Placement App V2.2"
        TopMost = True
        MarginControl.ResumeLayout(False)
        MarginControl.PerformLayout()
        TradeButtons.ResumeLayout(False)
        TradeButtons.PerformLayout()
        AccountInfo.ResumeLayout(False)
        AccountInfo.PerformLayout()
        ConnectionStatus.ResumeLayout(False)
        PlacedOrders.ResumeLayout(False)
        PlacedOrders.PerformLayout()
        OrderAmount.ResumeLayout(False)
        OrderAmount.PerformLayout()
        ManualTPSL.ResumeLayout(False)
        ManualTPSL.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub
    Friend WithEvents btnChangeForm As Button
    Friend WithEvents txtLogs As RichTextBox
    Friend WithEvents btnClose As Button
    Friend WithEvents btnClearLog As Button
    Friend WithEvents MarginControl As GroupBox
    Friend WithEvents txtComms As TextBox
    Friend WithEvents txtTPOffset As TextBox
    Friend WithEvents txtTriggerOffset As TextBox
    Friend WithEvents txtStopLoss As TextBox
    Friend WithEvents txtTrigger As TextBox
    Friend WithEvents txtTakeProfit As TextBox
    Friend WithEvents TradeButtons As GroupBox
    Friend WithEvents btnMarket As Button
    Friend WithEvents btnCancelAllOpen As Button
    Friend WithEvents btnTrail As Button
    Friend WithEvents btnReduceMarket As Button
    Friend WithEvents btnLimit As Button
    Friend WithEvents btnReduceLimit As Button
    Friend WithEvents btnNoSpread As Button
    Friend WithEvents CustomLabel9 As CustomLabel
    Friend WithEvents CustomLabel8 As CustomLabel
    Friend WithEvents lblPnL As CustomLabel
    Friend WithEvents lblPlacedStopLossPrice As CustomLabel
    Friend WithEvents lblPlacedTrigStopPrice As CustomLabel
    Friend WithEvents lblPlacedPrice As CustomLabel
    Friend WithEvents AccountInfo As GroupBox
    Friend WithEvents lblPlacedTakeProfitPrice As CustomLabel
    Friend WithEvents CustomLabel2 As CustomLabel
    Friend WithEvents CustomLabel1 As CustomLabel
    Friend WithEvents lblUSDSession As Label
    Friend WithEvents lblBTCSession As Label
    Friend WithEvents lblUSDEquity As Label
    Friend WithEvents lblBTCEquity As Label
    Friend WithEvents lblEquiv As Label
    Friend WithEvents Label3 As Label
    Friend WithEvents lblBalance As Label
    Friend WithEvents Label50 As Label
    Friend WithEvents lblIndexPrice As Label
    Friend WithEvents ConnectionStatus As GroupBox
    Friend WithEvents lblStatus As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents lblTime As CustomLabel
    Friend WithEvents lblTopBid As CustomLabel
    Friend WithEvents lblTopAsk As CustomLabel
    Friend WithEvents PlacedOrders As GroupBox
    Friend WithEvents CustomLabel10 As CustomLabel
    Friend WithEvents CustomLabel14 As CustomLabel
    Friend WithEvents CustomLabel11 As CustomLabel
    Friend WithEvents CustomLabel15 As CustomLabel
    Friend WithEvents CustomTextBox5 As CustomTextBox
    Friend WithEvents CustomTextBox3 As CustomTextBox
    Friend WithEvents txtTopBid As CustomTextBox
    Friend WithEvents txtPlacedTakeProfitPrice As CustomTextBox
    Friend WithEvents CustomTextBox6 As CustomTextBox
    Friend WithEvents CustomLabel17 As CustomLabel
    Friend WithEvents CustomLabel16 As CustomLabel
    Friend WithEvents CustomLabel19 As CustomLabel
    Friend WithEvents txtLAmount As CustomTextBox
    Friend WithEvents txtSAmount As CustomTextBox
    Friend WithEvents txtPlacedStopLossPrice As CustomTextBox
    Friend WithEvents txtPlacedTrigStopPrice As CustomTextBox
    Friend WithEvents txtPlacedPrice As CustomTextBox
    Friend WithEvents txtAmount As CustomTextBox
    Friend WithEvents OrderAmount As GroupBox
    Friend WithEvents btnConnect As Button
    Friend WithEvents radHeartBeat As RadioButton
    Friend WithEvents txtTopAsk As CustomTextBox
    Friend WithEvents btnSell As Button
    Friend WithEvents btnBuy As Button
    Friend WithEvents Label1 As Label
    Friend WithEvents ProgressBar1 As ProgressBar
    Friend WithEvents lblOrderStatus As CustomLabel
    Friend WithEvents CustomLabel3 As CustomLabel
    Friend WithEvents btnTPOffset As Button
    Friend WithEvents btnEditSLPrice As Button
    Friend WithEvents btnEditTPPrice As Button
    Friend WithEvents ManualTPSL As GroupBox
    Friend WithEvents CustomLabel5 As CustomLabel
    Friend WithEvents txtManualSL As CustomTextBox
    Friend WithEvents txtManualTP As CustomTextBox
    Friend WithEvents CustomLabel4 As CustomLabel
    Friend WithEvents btnViewTrades As Button
    Friend WithEvents lblEstimatedLiquidation As CustomLabel
    Friend WithEvents lblEstimatedLeverage As CustomLabel
    Friend WithEvents lblInitialMargin As CustomLabel
    Friend WithEvents lblMaintenanceMargin As CustomLabel
    Friend WithEvents btnEstimateMargins As Button
    Friend WithEvents btnRefreshLiveData As Button
    Friend WithEvents txtMarketStopLoss As TextBox
    Friend WithEvents btnMark As Button
    Friend WithEvents CustomLabel7 As CustomLabel
    Friend WithEvents chkMarketStopLoss As CheckBox
    Friend WithEvents txtMaxSlippageATR As TextBox
    Friend WithEvents chkMaxSlippageATR As CheckBox
    Friend WithEvents ToolTip1 As ToolTip
End Class
