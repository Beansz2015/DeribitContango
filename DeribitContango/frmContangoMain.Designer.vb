<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmContangoMain
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
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
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        grpConnection = New GroupBox()
        btnDiscoverWeekly = New Button()
        lblConn = New Label()
        btnConnect = New Button()
        txtClientSecret = New TextBox()
        Label2 = New Label()
        txtClientId = New TextBox()
        Label1 = New Label()
        grpMarket = New GroupBox()
        lblMedianBasis = New Label()
        Label11 = New Label()
        lblAnnual = New Label()
        Label16 = New Label()
        lblBasis = New Label()
        Label14 = New Label()
        lblFutMark = New Label()
        Label12 = New Label()
        lblFutAsk = New Label()
        lblFutBid = New Label()
        Label9 = New Label()
        Label8 = New Label()
        lblSpotAsk = New Label()
        lblSpotBid = New Label()
        Label5 = New Label()
        Label4 = New Label()
        lblIndex = New Label()
        Label3 = New Label()
        grpTime = New GroupBox()
        lblNowMYT = New Label()
        lblNowUTC = New Label()
        lblExpiryMYT = New Label()
        lblExpiryUTC = New Label()
        Label20 = New Label()
        Label19 = New Label()
        Label18 = New Label()
        Label17 = New Label()
        grpEntry = New GroupBox()
        chkAutoEnterAfterRoll = New CheckBox()
        numBasisSampleMs = New NumericUpDown()
        Label7 = New Label()
        numBasisWindowMs = New NumericUpDown()
        Label6 = New Label()
        lblMinUSD = New Label()
        numRequoteMs = New NumericUpDown()
        LabelRQMs = New Label()
        numRequoteTicks = New NumericUpDown()
        LabelRQTicks = New Label()
        numSlippageBps = New NumericUpDown()
        Label23 = New Label()
        numThreshold = New NumericUpDown()
        Label22 = New Label()
        btnCloseAll = New Button()
        btnRoll = New Button()
        btnEnter = New Button()
        txtAmount = New TextBox()
        lblAmountUnits = New Label()
        radBTC = New RadioButton()
        radUSD = New RadioButton()
        Label21 = New Label()
        grpLog = New GroupBox()
        txtLog = New RichTextBox()
        grpConnection.SuspendLayout()
        grpMarket.SuspendLayout()
        grpTime.SuspendLayout()
        grpEntry.SuspendLayout()
        CType(numBasisSampleMs, ComponentModel.ISupportInitialize).BeginInit()
        CType(numBasisWindowMs, ComponentModel.ISupportInitialize).BeginInit()
        CType(numRequoteMs, ComponentModel.ISupportInitialize).BeginInit()
        CType(numRequoteTicks, ComponentModel.ISupportInitialize).BeginInit()
        CType(numSlippageBps, ComponentModel.ISupportInitialize).BeginInit()
        CType(numThreshold, ComponentModel.ISupportInitialize).BeginInit()
        grpLog.SuspendLayout()
        SuspendLayout()
        ' 
        ' grpConnection
        ' 
        grpConnection.BackColor = Color.LightSteelBlue
        grpConnection.Controls.Add(btnDiscoverWeekly)
        grpConnection.Controls.Add(lblConn)
        grpConnection.Controls.Add(btnConnect)
        grpConnection.Controls.Add(txtClientSecret)
        grpConnection.Controls.Add(Label2)
        grpConnection.Controls.Add(txtClientId)
        grpConnection.Controls.Add(Label1)
        grpConnection.ForeColor = Color.MidnightBlue
        grpConnection.Location = New Point(17, 20)
        grpConnection.Margin = New Padding(4, 5, 4, 5)
        grpConnection.Name = "grpConnection"
        grpConnection.Padding = New Padding(4, 5, 4, 5)
        grpConnection.Size = New Size(1109, 143)
        grpConnection.TabIndex = 0
        grpConnection.TabStop = False
        grpConnection.Text = "Connection"
        ' 
        ' btnDiscoverWeekly
        ' 
        btnDiscoverWeekly.BackColor = Color.Khaki
        btnDiscoverWeekly.Location = New Point(967, 32)
        btnDiscoverWeekly.Margin = New Padding(4, 5, 4, 5)
        btnDiscoverWeekly.Name = "btnDiscoverWeekly"
        btnDiscoverWeekly.Size = New Size(133, 38)
        btnDiscoverWeekly.TabIndex = 6
        btnDiscoverWeekly.Text = "Discover"
        btnDiscoverWeekly.UseVisualStyleBackColor = False
        ' 
        ' lblConn
        ' 
        lblConn.AutoSize = True
        lblConn.Location = New Point(19, 93)
        lblConn.Margin = New Padding(4, 0, 4, 0)
        lblConn.Name = "lblConn"
        lblConn.Size = New Size(161, 25)
        lblConn.TabIndex = 5
        lblConn.Text = "Disconnected (WS)"
        ' 
        ' btnConnect
        ' 
        btnConnect.BackColor = Color.PaleGreen
        btnConnect.Location = New Point(851, 32)
        btnConnect.Margin = New Padding(4, 5, 4, 5)
        btnConnect.Name = "btnConnect"
        btnConnect.Size = New Size(107, 38)
        btnConnect.TabIndex = 4
        btnConnect.Text = "Connect"
        btnConnect.UseVisualStyleBackColor = False
        ' 
        ' txtClientSecret
        ' 
        txtClientSecret.Location = New Point(513, 33)
        txtClientSecret.Margin = New Padding(4, 5, 4, 5)
        txtClientSecret.Name = "txtClientSecret"
        txtClientSecret.PasswordChar = "*"c
        txtClientSecret.Size = New Size(328, 31)
        txtClientSecret.TabIndex = 3
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Location = New Point(399, 38)
        Label2.Margin = New Padding(4, 0, 4, 0)
        Label2.Name = "Label2"
        Label2.Size = New Size(104, 25)
        Label2.TabIndex = 2
        Label2.Text = "ClientSecret"
        ' 
        ' txtClientId
        ' 
        txtClientId.Location = New Point(110, 33)
        txtClientId.Margin = New Padding(4, 5, 4, 5)
        txtClientId.Name = "txtClientId"
        txtClientId.Size = New Size(278, 31)
        txtClientId.TabIndex = 1
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Location = New Point(19, 38)
        Label1.Margin = New Padding(4, 0, 4, 0)
        Label1.Name = "Label1"
        Label1.Size = New Size(74, 25)
        Label1.TabIndex = 0
        Label1.Text = "ClientID"
        ' 
        ' grpMarket
        ' 
        grpMarket.BackColor = Color.LemonChiffon
        grpMarket.Controls.Add(lblMedianBasis)
        grpMarket.Controls.Add(Label11)
        grpMarket.Controls.Add(lblAnnual)
        grpMarket.Controls.Add(Label16)
        grpMarket.Controls.Add(lblBasis)
        grpMarket.Controls.Add(Label14)
        grpMarket.Controls.Add(lblFutMark)
        grpMarket.Controls.Add(Label12)
        grpMarket.Controls.Add(lblFutAsk)
        grpMarket.Controls.Add(lblFutBid)
        grpMarket.Controls.Add(Label9)
        grpMarket.Controls.Add(Label8)
        grpMarket.Controls.Add(lblSpotAsk)
        grpMarket.Controls.Add(lblSpotBid)
        grpMarket.Controls.Add(Label5)
        grpMarket.Controls.Add(Label4)
        grpMarket.Controls.Add(lblIndex)
        grpMarket.Controls.Add(Label3)
        grpMarket.ForeColor = Color.DarkOliveGreen
        grpMarket.Location = New Point(17, 173)
        grpMarket.Margin = New Padding(4, 5, 4, 5)
        grpMarket.Name = "grpMarket"
        grpMarket.Padding = New Padding(4, 5, 4, 5)
        grpMarket.Size = New Size(1109, 190)
        grpMarket.TabIndex = 1
        grpMarket.TabStop = False
        grpMarket.Text = "Market"
        ' 
        ' lblMedianBasis
        ' 
        lblMedianBasis.AutoSize = True
        lblMedianBasis.Location = New Point(899, 38)
        lblMedianBasis.Margin = New Padding(4, 0, 4, 0)
        lblMedianBasis.Name = "lblMedianBasis"
        lblMedianBasis.Size = New Size(61, 25)
        lblMedianBasis.TabIndex = 17
        lblMedianBasis.Text = "0.00%"
        ' 
        ' Label11
        ' 
        Label11.AutoSize = True
        Label11.Location = New Point(766, 38)
        Label11.Margin = New Padding(4, 0, 4, 0)
        Label11.Name = "Label11"
        Label11.Size = New Size(119, 25)
        Label11.TabIndex = 16
        Label11.Text = "Median Basis:"
        ' 
        ' lblAnnual
        ' 
        lblAnnual.AutoSize = True
        lblAnnual.Location = New Point(897, 123)
        lblAnnual.Margin = New Padding(4, 0, 4, 0)
        lblAnnual.Name = "lblAnnual"
        lblAnnual.Size = New Size(61, 25)
        lblAnnual.TabIndex = 15
        lblAnnual.Text = "0.00%"
        ' 
        ' Label16
        ' 
        Label16.AutoSize = True
        Label16.Location = New Point(814, 123)
        Label16.Margin = New Padding(4, 0, 4, 0)
        Label16.Name = "Label16"
        Label16.Size = New Size(71, 25)
        Label16.TabIndex = 14
        Label16.Text = "Annual:"
        ' 
        ' lblBasis
        ' 
        lblBasis.AutoSize = True
        lblBasis.Location = New Point(897, 80)
        lblBasis.Margin = New Padding(4, 0, 4, 0)
        lblBasis.Name = "lblBasis"
        lblBasis.Size = New Size(61, 25)
        lblBasis.TabIndex = 13
        lblBasis.Text = "0.00%"
        ' 
        ' Label14
        ' 
        Label14.AutoSize = True
        Label14.Location = New Point(830, 80)
        Label14.Margin = New Padding(4, 0, 4, 0)
        Label14.Name = "Label14"
        Label14.Size = New Size(55, 25)
        Label14.TabIndex = 12
        Label14.Text = "Basis:"
        ' 
        ' lblFutMark
        ' 
        lblFutMark.AutoSize = True
        lblFutMark.Location = New Point(549, 123)
        lblFutMark.Margin = New Padding(4, 0, 4, 0)
        lblFutMark.Name = "lblFutMark"
        lblFutMark.Size = New Size(46, 25)
        lblFutMark.TabIndex = 11
        lblFutMark.Text = "0.00"
        ' 
        ' Label12
        ' 
        Label12.AutoSize = True
        Label12.Location = New Point(444, 123)
        Label12.Margin = New Padding(4, 0, 4, 0)
        Label12.Name = "Label12"
        Label12.Size = New Size(86, 25)
        Label12.TabIndex = 10
        Label12.Text = "Fut Mark:"
        ' 
        ' lblFutAsk
        ' 
        lblFutAsk.AutoSize = True
        lblFutAsk.Location = New Point(549, 80)
        lblFutAsk.Margin = New Padding(4, 0, 4, 0)
        lblFutAsk.Name = "lblFutAsk"
        lblFutAsk.Size = New Size(46, 25)
        lblFutAsk.TabIndex = 9
        lblFutAsk.Text = "0.00"
        ' 
        ' lblFutBid
        ' 
        lblFutBid.AutoSize = True
        lblFutBid.Location = New Point(549, 38)
        lblFutBid.Margin = New Padding(4, 0, 4, 0)
        lblFutBid.Name = "lblFutBid"
        lblFutBid.Size = New Size(46, 25)
        lblFutBid.TabIndex = 8
        lblFutBid.Text = "0.00"
        ' 
        ' Label9
        ' 
        Label9.AutoSize = True
        Label9.Location = New Point(454, 80)
        Label9.Margin = New Padding(4, 0, 4, 0)
        Label9.Name = "Label9"
        Label9.Size = New Size(75, 25)
        Label9.TabIndex = 7
        Label9.Text = "Fut Ask:"
        ' 
        ' Label8
        ' 
        Label8.AutoSize = True
        Label8.Location = New Point(457, 38)
        Label8.Margin = New Padding(4, 0, 4, 0)
        Label8.Name = "Label8"
        Label8.Size = New Size(71, 25)
        Label8.TabIndex = 6
        Label8.Text = "Fut Bid:"
        ' 
        ' lblSpotAsk
        ' 
        lblSpotAsk.AutoSize = True
        lblSpotAsk.Location = New Point(110, 123)
        lblSpotAsk.Margin = New Padding(4, 0, 4, 0)
        lblSpotAsk.Name = "lblSpotAsk"
        lblSpotAsk.Size = New Size(46, 25)
        lblSpotAsk.TabIndex = 5
        lblSpotAsk.Text = "0.00"
        ' 
        ' lblSpotBid
        ' 
        lblSpotBid.AutoSize = True
        lblSpotBid.Location = New Point(110, 80)
        lblSpotBid.Margin = New Padding(4, 0, 4, 0)
        lblSpotBid.Name = "lblSpotBid"
        lblSpotBid.Size = New Size(46, 25)
        lblSpotBid.TabIndex = 4
        lblSpotBid.Text = "0.00"
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Location = New Point(13, 123)
        Label5.Margin = New Padding(4, 0, 4, 0)
        Label5.Name = "Label5"
        Label5.Size = New Size(88, 25)
        Label5.TabIndex = 3
        Label5.Text = "Spot Ask:"
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Location = New Point(16, 80)
        Label4.Margin = New Padding(4, 0, 4, 0)
        Label4.Name = "Label4"
        Label4.Size = New Size(84, 25)
        Label4.TabIndex = 2
        Label4.Text = "Spot Bid:"
        ' 
        ' lblIndex
        ' 
        lblIndex.AutoSize = True
        lblIndex.Location = New Point(110, 38)
        lblIndex.Margin = New Padding(4, 0, 4, 0)
        lblIndex.Name = "lblIndex"
        lblIndex.Size = New Size(46, 25)
        lblIndex.TabIndex = 1
        lblIndex.Text = "0.00"
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Location = New Point(37, 38)
        Label3.Margin = New Padding(4, 0, 4, 0)
        Label3.Name = "Label3"
        Label3.Size = New Size(59, 25)
        Label3.TabIndex = 0
        Label3.Text = "Index:"
        ' 
        ' grpTime
        ' 
        grpTime.BackColor = Color.Honeydew
        grpTime.Controls.Add(lblNowMYT)
        grpTime.Controls.Add(lblNowUTC)
        grpTime.Controls.Add(lblExpiryMYT)
        grpTime.Controls.Add(lblExpiryUTC)
        grpTime.Controls.Add(Label20)
        grpTime.Controls.Add(Label19)
        grpTime.Controls.Add(Label18)
        grpTime.Controls.Add(Label17)
        grpTime.ForeColor = Color.DarkGreen
        grpTime.Location = New Point(17, 373)
        grpTime.Margin = New Padding(4, 5, 4, 5)
        grpTime.Name = "grpTime"
        grpTime.Padding = New Padding(4, 5, 4, 5)
        grpTime.Size = New Size(1109, 153)
        grpTime.TabIndex = 2
        grpTime.TabStop = False
        grpTime.Text = "Time / Expiry"
        ' 
        ' lblNowMYT
        ' 
        lblNowMYT.AutoSize = True
        lblNowMYT.Location = New Point(731, 97)
        lblNowMYT.Margin = New Padding(4, 0, 4, 0)
        lblNowMYT.Name = "lblNowMYT"
        lblNowMYT.Size = New Size(229, 25)
        lblNowMYT.TabIndex = 7
        lblNowMYT.Text = "0000-00-00 00:00:00 (MYT)"
        ' 
        ' lblNowUTC
        ' 
        lblNowUTC.AutoSize = True
        lblNowUTC.Location = New Point(731, 47)
        lblNowUTC.Margin = New Padding(4, 0, 4, 0)
        lblNowUTC.Name = "lblNowUTC"
        lblNowUTC.Size = New Size(225, 25)
        lblNowUTC.TabIndex = 6
        lblNowUTC.Text = "0000-00-00 00:00:00 (UTC)"
        ' 
        ' lblExpiryMYT
        ' 
        lblExpiryMYT.AutoSize = True
        lblExpiryMYT.Location = New Point(156, 97)
        lblExpiryMYT.Margin = New Padding(4, 0, 4, 0)
        lblExpiryMYT.Name = "lblExpiryMYT"
        lblExpiryMYT.Size = New Size(229, 25)
        lblExpiryMYT.TabIndex = 5
        lblExpiryMYT.Text = "0000-00-00 00:00:00 (MYT)"
        ' 
        ' lblExpiryUTC
        ' 
        lblExpiryUTC.AutoSize = True
        lblExpiryUTC.Location = New Point(156, 47)
        lblExpiryUTC.Margin = New Padding(4, 0, 4, 0)
        lblExpiryUTC.Name = "lblExpiryUTC"
        lblExpiryUTC.Size = New Size(225, 25)
        lblExpiryUTC.TabIndex = 4
        lblExpiryUTC.Text = "0000-00-00 00:00:00 (UTC)"
        ' 
        ' Label20
        ' 
        Label20.AutoSize = True
        Label20.Location = New Point(633, 97)
        Label20.Margin = New Padding(4, 0, 4, 0)
        Label20.Name = "Label20"
        Label20.Size = New Size(93, 25)
        Label20.TabIndex = 3
        Label20.Text = "Now MYT:"
        ' 
        ' Label19
        ' 
        Label19.AutoSize = True
        Label19.Location = New Point(634, 47)
        Label19.Margin = New Padding(4, 0, 4, 0)
        Label19.Name = "Label19"
        Label19.Size = New Size(89, 25)
        Label19.TabIndex = 2
        Label19.Text = "Now UTC:"
        ' 
        ' Label18
        ' 
        Label18.AutoSize = True
        Label18.Location = New Point(46, 97)
        Label18.Margin = New Padding(4, 0, 4, 0)
        Label18.Name = "Label18"
        Label18.Size = New Size(103, 25)
        Label18.TabIndex = 1
        Label18.Text = "Expiry MYT:"
        ' 
        ' Label17
        ' 
        Label17.AutoSize = True
        Label17.Location = New Point(47, 47)
        Label17.Margin = New Padding(4, 0, 4, 0)
        Label17.Name = "Label17"
        Label17.Size = New Size(99, 25)
        Label17.TabIndex = 0
        Label17.Text = "Expiry UTC:"
        ' 
        ' grpEntry
        ' 
        grpEntry.BackColor = Color.Lavender
        grpEntry.Controls.Add(chkAutoEnterAfterRoll)
        grpEntry.Controls.Add(numBasisSampleMs)
        grpEntry.Controls.Add(Label7)
        grpEntry.Controls.Add(numBasisWindowMs)
        grpEntry.Controls.Add(Label6)
        grpEntry.Controls.Add(lblMinUSD)
        grpEntry.Controls.Add(numRequoteMs)
        grpEntry.Controls.Add(LabelRQMs)
        grpEntry.Controls.Add(numRequoteTicks)
        grpEntry.Controls.Add(LabelRQTicks)
        grpEntry.Controls.Add(numSlippageBps)
        grpEntry.Controls.Add(Label23)
        grpEntry.Controls.Add(numThreshold)
        grpEntry.Controls.Add(Label22)
        grpEntry.Controls.Add(btnCloseAll)
        grpEntry.Controls.Add(btnRoll)
        grpEntry.Controls.Add(btnEnter)
        grpEntry.Controls.Add(txtAmount)
        grpEntry.Controls.Add(lblAmountUnits)
        grpEntry.Controls.Add(radBTC)
        grpEntry.Controls.Add(radUSD)
        grpEntry.Controls.Add(Label21)
        grpEntry.ForeColor = Color.MidnightBlue
        grpEntry.Location = New Point(17, 537)
        grpEntry.Margin = New Padding(4, 5, 4, 5)
        grpEntry.Name = "grpEntry"
        grpEntry.Padding = New Padding(4, 5, 4, 5)
        grpEntry.Size = New Size(1109, 233)
        grpEntry.TabIndex = 3
        grpEntry.TabStop = False
        grpEntry.Text = "Entry / Risk"
        ' 
        ' chkAutoEnterAfterRoll
        ' 
        chkAutoEnterAfterRoll.AutoSize = True
        chkAutoEnterAfterRoll.Checked = True
        chkAutoEnterAfterRoll.CheckState = CheckState.Checked
        chkAutoEnterAfterRoll.Location = New Point(944, 22)
        chkAutoEnterAfterRoll.Name = "chkAutoEnterAfterRoll"
        chkAutoEnterAfterRoll.Size = New Size(156, 29)
        chkAutoEnterAfterRoll.TabIndex = 21
        chkAutoEnterAfterRoll.Text = "Enter After Roll"
        chkAutoEnterAfterRoll.UseVisualStyleBackColor = True
        ' 
        ' numBasisSampleMs
        ' 
        numBasisSampleMs.Increment = New Decimal(New Integer() {10, 0, 0, 0})
        numBasisSampleMs.Location = New Point(484, 75)
        numBasisSampleMs.Margin = New Padding(4, 5, 4, 5)
        numBasisSampleMs.Maximum = New Decimal(New Integer() {500, 0, 0, 0})
        numBasisSampleMs.Minimum = New Decimal(New Integer() {20, 0, 0, 0})
        numBasisSampleMs.Name = "numBasisSampleMs"
        numBasisSampleMs.Size = New Size(97, 31)
        numBasisSampleMs.TabIndex = 20
        numBasisSampleMs.Value = New Decimal(New Integer() {500, 0, 0, 0})
        ' 
        ' Label7
        ' 
        Label7.AutoSize = True
        Label7.Location = New Point(360, 78)
        Label7.Margin = New Padding(4, 0, 4, 0)
        Label7.Name = "Label7"
        Label7.Size = New Size(115, 25)
        Label7.TabIndex = 19
        Label7.Text = "Basis Sample"
        ' 
        ' numBasisWindowMs
        ' 
        numBasisWindowMs.Increment = New Decimal(New Integer() {100, 0, 0, 0})
        numBasisWindowMs.Location = New Point(484, 37)
        numBasisWindowMs.Margin = New Padding(4, 5, 4, 5)
        numBasisWindowMs.Maximum = New Decimal(New Integer() {10000, 0, 0, 0})
        numBasisWindowMs.Minimum = New Decimal(New Integer() {200, 0, 0, 0})
        numBasisWindowMs.Name = "numBasisWindowMs"
        numBasisWindowMs.Size = New Size(97, 31)
        numBasisWindowMs.TabIndex = 18
        numBasisWindowMs.Value = New Decimal(New Integer() {3000, 0, 0, 0})
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Location = New Point(359, 40)
        Label6.Margin = New Padding(4, 0, 4, 0)
        Label6.Name = "Label6"
        Label6.Size = New Size(122, 25)
        Label6.TabIndex = 17
        Label6.Text = "Basis Window"
        ' 
        ' lblMinUSD
        ' 
        lblMinUSD.AutoSize = True
        lblMinUSD.Font = New Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblMinUSD.Location = New Point(144, 139)
        lblMinUSD.Margin = New Padding(4, 0, 4, 0)
        lblMinUSD.Name = "lblMinUSD"
        lblMinUSD.Size = New Size(142, 21)
        lblMinUSD.TabIndex = 16
        lblMinUSD.Text = "Min USD for 1 step"
        ' 
        ' numRequoteMs
        ' 
        numRequoteMs.Increment = New Decimal(New Integer() {50, 0, 0, 0})
        numRequoteMs.Location = New Point(1043, 150)
        numRequoteMs.Margin = New Padding(4, 5, 4, 5)
        numRequoteMs.Maximum = New Decimal(New Integer() {5000, 0, 0, 0})
        numRequoteMs.Minimum = New Decimal(New Integer() {100, 0, 0, 0})
        numRequoteMs.Name = "numRequoteMs"
        numRequoteMs.Size = New Size(97, 31)
        numRequoteMs.TabIndex = 13
        numRequoteMs.Value = New Decimal(New Integer() {300, 0, 0, 0})
        ' 
        ' LabelRQMs
        ' 
        LabelRQMs.AutoSize = True
        LabelRQMs.Location = New Point(880, 153)
        LabelRQMs.Margin = New Padding(4, 0, 4, 0)
        LabelRQMs.Name = "LabelRQMs"
        LabelRQMs.Size = New Size(171, 25)
        LabelRQMs.TabIndex = 14
        LabelRQMs.Text = "Re-quote ms (100+)"
        ' 
        ' numRequoteTicks
        ' 
        numRequoteTicks.Location = New Point(762, 116)
        numRequoteTicks.Margin = New Padding(4, 5, 4, 5)
        numRequoteTicks.Maximum = New Decimal(New Integer() {10, 0, 0, 0})
        numRequoteTicks.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        numRequoteTicks.Name = "numRequoteTicks"
        numRequoteTicks.Size = New Size(97, 31)
        numRequoteTicks.TabIndex = 12
        numRequoteTicks.Value = New Decimal(New Integer() {2, 0, 0, 0})
        ' 
        ' LabelRQTicks
        ' 
        LabelRQTicks.AutoSize = True
        LabelRQTicks.Location = New Point(599, 119)
        LabelRQTicks.Margin = New Padding(4, 0, 4, 0)
        LabelRQTicks.Name = "LabelRQTicks"
        LabelRQTicks.Size = New Size(160, 25)
        LabelRQTicks.TabIndex = 15
        LabelRQTicks.Text = "Re-quote min ticks"
        ' 
        ' numSlippageBps
        ' 
        numSlippageBps.Location = New Point(762, 76)
        numSlippageBps.Margin = New Padding(4, 5, 4, 5)
        numSlippageBps.Maximum = New Decimal(New Integer() {1000, 0, 0, 0})
        numSlippageBps.Name = "numSlippageBps"
        numSlippageBps.Size = New Size(97, 31)
        numSlippageBps.TabIndex = 11
        numSlippageBps.Value = New Decimal(New Integer() {5, 0, 0, 0})
        ' 
        ' Label23
        ' 
        Label23.AutoSize = True
        Label23.Location = New Point(605, 80)
        Label23.Margin = New Padding(4, 0, 4, 0)
        Label23.Name = "Label23"
        Label23.Size = New Size(154, 25)
        Label23.TabIndex = 10
        Label23.Text = "Max Slippage bps"
        ' 
        ' numThreshold
        ' 
        numThreshold.DecimalPlaces = 4
        numThreshold.Increment = New Decimal(New Integer() {1, 0, 0, 262144})
        numThreshold.Location = New Point(762, 37)
        numThreshold.Margin = New Padding(4, 5, 4, 5)
        numThreshold.Maximum = New Decimal(New Integer() {1, 0, 0, 0})
        numThreshold.Name = "numThreshold"
        numThreshold.Size = New Size(97, 31)
        numThreshold.TabIndex = 9
        numThreshold.Value = New Decimal(New Integer() {25, 0, 0, 262144})
        ' 
        ' Label22
        ' 
        Label22.AutoSize = True
        Label22.Location = New Point(624, 40)
        Label22.Margin = New Padding(4, 0, 4, 0)
        Label22.Name = "Label22"
        Label22.Size = New Size(135, 25)
        Label22.TabIndex = 8
        Label22.Text = "Entry Threshold"
        ' 
        ' btnCloseAll
        ' 
        btnCloseAll.BackColor = Color.MistyRose
        btnCloseAll.Location = New Point(944, 103)
        btnCloseAll.Margin = New Padding(4, 5, 4, 5)
        btnCloseAll.Name = "btnCloseAll"
        btnCloseAll.Size = New Size(156, 38)
        btnCloseAll.TabIndex = 7
        btnCloseAll.Text = "Close All"
        btnCloseAll.UseVisualStyleBackColor = False
        ' 
        ' btnRoll
        ' 
        btnRoll.BackColor = Color.LightGoldenrodYellow
        btnRoll.Location = New Point(944, 59)
        btnRoll.Margin = New Padding(4, 5, 4, 5)
        btnRoll.Name = "btnRoll"
        btnRoll.Size = New Size(156, 38)
        btnRoll.TabIndex = 6
        btnRoll.Text = "Roll to Next"
        btnRoll.UseVisualStyleBackColor = False
        ' 
        ' btnEnter
        ' 
        btnEnter.BackColor = Color.LightSkyBlue
        btnEnter.Location = New Point(360, 118)
        btnEnter.Margin = New Padding(4, 5, 4, 5)
        btnEnter.Name = "btnEnter"
        btnEnter.Size = New Size(221, 88)
        btnEnter.TabIndex = 5
        btnEnter.Text = "Enter Basis"
        btnEnter.UseVisualStyleBackColor = False
        ' 
        ' txtAmount
        ' 
        txtAmount.Location = New Point(144, 103)
        txtAmount.Margin = New Padding(4, 5, 4, 5)
        txtAmount.Name = "txtAmount"
        txtAmount.Size = New Size(183, 31)
        txtAmount.TabIndex = 4
        txtAmount.Text = "1000"
        ' 
        ' lblAmountUnits
        ' 
        lblAmountUnits.AutoSize = True
        lblAmountUnits.Location = New Point(19, 108)
        lblAmountUnits.Margin = New Padding(4, 0, 4, 0)
        lblAmountUnits.Name = "lblAmountUnits"
        lblAmountUnits.Size = New Size(117, 25)
        lblAmountUnits.TabIndex = 3
        lblAmountUnits.Text = "Amount USD"
        ' 
        ' radBTC
        ' 
        radBTC.AutoSize = True
        radBTC.Location = New Point(219, 47)
        radBTC.Margin = New Padding(4, 5, 4, 5)
        radBTC.Name = "radBTC"
        radBTC.Size = New Size(65, 29)
        radBTC.TabIndex = 2
        radBTC.Text = "BTC"
        radBTC.UseVisualStyleBackColor = True
        ' 
        ' radUSD
        ' 
        radUSD.AutoSize = True
        radUSD.Checked = True
        radUSD.Location = New Point(123, 47)
        radUSD.Margin = New Padding(4, 5, 4, 5)
        radUSD.Name = "radUSD"
        radUSD.Size = New Size(72, 29)
        radUSD.TabIndex = 1
        radUSD.TabStop = True
        radUSD.Text = "USD"
        radUSD.UseVisualStyleBackColor = True
        ' 
        ' Label21
        ' 
        Label21.AutoSize = True
        Label21.Location = New Point(19, 50)
        Label21.Margin = New Padding(4, 0, 4, 0)
        Label21.Name = "Label21"
        Label21.Size = New Size(94, 25)
        Label21.TabIndex = 0
        Label21.Text = "Size Input:"
        ' 
        ' grpLog
        ' 
        grpLog.BackColor = Color.White
        grpLog.Controls.Add(txtLog)
        grpLog.ForeColor = Color.Black
        grpLog.Location = New Point(17, 780)
        grpLog.Margin = New Padding(4, 5, 4, 5)
        grpLog.Name = "grpLog"
        grpLog.Padding = New Padding(4, 5, 4, 5)
        grpLog.Size = New Size(1109, 340)
        grpLog.TabIndex = 4
        grpLog.TabStop = False
        grpLog.Text = "Log"
        ' 
        ' txtLog
        ' 
        txtLog.BackColor = Color.White
        txtLog.Dock = DockStyle.Fill
        txtLog.Location = New Point(4, 29)
        txtLog.Margin = New Padding(4, 5, 4, 5)
        txtLog.Name = "txtLog"
        txtLog.Size = New Size(1101, 306)
        txtLog.TabIndex = 0
        txtLog.Text = ""
        ' 
        ' frmContangoMain
        ' 
        AutoScaleDimensions = New SizeF(10F, 25F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = Color.WhiteSmoke
        ClientSize = New Size(1143, 1140)
        Controls.Add(grpLog)
        Controls.Add(grpEntry)
        Controls.Add(grpTime)
        Controls.Add(grpMarket)
        Controls.Add(grpConnection)
        Margin = New Padding(4, 5, 4, 5)
        Name = "frmContangoMain"
        Text = "Deribit Contango Basis Trader - V2.1c"
        grpConnection.ResumeLayout(False)
        grpConnection.PerformLayout()
        grpMarket.ResumeLayout(False)
        grpMarket.PerformLayout()
        grpTime.ResumeLayout(False)
        grpTime.PerformLayout()
        grpEntry.ResumeLayout(False)
        grpEntry.PerformLayout()
        CType(numBasisSampleMs, ComponentModel.ISupportInitialize).EndInit()
        CType(numBasisWindowMs, ComponentModel.ISupportInitialize).EndInit()
        CType(numRequoteMs, ComponentModel.ISupportInitialize).EndInit()
        CType(numRequoteTicks, ComponentModel.ISupportInitialize).EndInit()
        CType(numSlippageBps, ComponentModel.ISupportInitialize).EndInit()
        CType(numThreshold, ComponentModel.ISupportInitialize).EndInit()
        grpLog.ResumeLayout(False)
        ResumeLayout(False)

    End Sub

    Friend WithEvents grpConnection As GroupBox
    Friend WithEvents btnDiscoverWeekly As Button
    Friend WithEvents lblConn As Label
    Friend WithEvents btnConnect As Button
    Friend WithEvents txtClientSecret As TextBox
    Friend WithEvents Label2 As Label
    Friend WithEvents txtClientId As TextBox
    Friend WithEvents Label1 As Label
    Friend WithEvents grpMarket As GroupBox
    Friend WithEvents lblAnnual As Label
    Friend WithEvents Label16 As Label
    Friend WithEvents lblBasis As Label
    Friend WithEvents Label14 As Label
    Friend WithEvents lblFutMark As Label
    Friend WithEvents Label12 As Label
    Friend WithEvents lblFutAsk As Label
    Friend WithEvents lblFutBid As Label
    Friend WithEvents Label9 As Label
    Friend WithEvents Label8 As Label
    Friend WithEvents lblSpotAsk As Label
    Friend WithEvents lblSpotBid As Label
    Friend WithEvents Label5 As Label
    Friend WithEvents Label4 As Label
    Friend WithEvents lblIndex As Label
    Friend WithEvents Label3 As Label
    Friend WithEvents grpTime As GroupBox
    Friend WithEvents lblNowMYT As Label
    Friend WithEvents lblNowUTC As Label
    Friend WithEvents lblExpiryMYT As Label
    Friend WithEvents lblExpiryUTC As Label
    Friend WithEvents Label20 As Label
    Friend WithEvents Label19 As Label
    Friend WithEvents Label18 As Label
    Friend WithEvents Label17 As Label
    Friend WithEvents grpEntry As GroupBox
    Friend WithEvents numSlippageBps As NumericUpDown
    Friend WithEvents Label23 As Label
    Friend WithEvents numThreshold As NumericUpDown
    Friend WithEvents Label22 As Label
    Friend WithEvents btnCloseAll As Button
    Friend WithEvents btnRoll As Button
    Friend WithEvents btnEnter As Button
    Friend WithEvents txtAmount As TextBox
    Friend WithEvents lblAmountUnits As Label
    Friend WithEvents radBTC As RadioButton
    Friend WithEvents radUSD As RadioButton
    Friend WithEvents Label21 As Label
    Friend WithEvents grpLog As GroupBox
    Friend WithEvents txtLog As RichTextBox
    Friend WithEvents numRequoteTicks As NumericUpDown
    Friend WithEvents LabelRQTicks As Label
    Friend WithEvents numRequoteMs As NumericUpDown
    Friend WithEvents LabelRQMs As Label
    Friend WithEvents lblMinUSD As Label
    Friend WithEvents numBasisWindowMs As NumericUpDown
    Friend WithEvents Label6 As Label
    Friend WithEvents numBasisSampleMs As NumericUpDown
    Friend WithEvents Label7 As Label
    Friend WithEvents lblMedianBasis As Label
    Friend WithEvents Label11 As Label
    Friend WithEvents chkAutoEnterAfterRoll As CheckBox
End Class
