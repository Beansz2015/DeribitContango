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
        Me.grpConnection = New System.Windows.Forms.GroupBox()
        Me.btnDiscoverWeekly = New System.Windows.Forms.Button()
        Me.lblConn = New System.Windows.Forms.Label()
        Me.btnConnect = New System.Windows.Forms.Button()
        Me.txtClientSecret = New System.Windows.Forms.TextBox()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.txtClientId = New System.Windows.Forms.TextBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.grpMarket = New System.Windows.Forms.GroupBox()
        Me.lblAnnual = New System.Windows.Forms.Label()
        Me.Label16 = New System.Windows.Forms.Label()
        Me.lblBasis = New System.Windows.Forms.Label()
        Me.Label14 = New System.Windows.Forms.Label()
        Me.lblFutMark = New System.Windows.Forms.Label()
        Me.Label12 = New System.Windows.Forms.Label()
        Me.lblFutAsk = New System.Windows.Forms.Label()
        Me.lblFutBid = New System.Windows.Forms.Label()
        Me.Label9 = New System.Windows.Forms.Label()
        Me.Label8 = New System.Windows.Forms.Label()
        Me.lblSpotAsk = New System.Windows.Forms.Label()
        Me.lblSpotBid = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.lblIndex = New System.Windows.Forms.Label()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.grpTime = New System.Windows.Forms.GroupBox()
        Me.lblNowMYT = New System.Windows.Forms.Label()
        Me.lblNowUTC = New System.Windows.Forms.Label()
        Me.lblExpiryMYT = New System.Windows.Forms.Label()
        Me.lblExpiryUTC = New System.Windows.Forms.Label()
        Me.Label20 = New System.Windows.Forms.Label()
        Me.Label19 = New System.Windows.Forms.Label()
        Me.Label18 = New System.Windows.Forms.Label()
        Me.Label17 = New System.Windows.Forms.Label()
        Me.grpEntry = New System.Windows.Forms.GroupBox()
        Me.numSlippageBps = New System.Windows.Forms.NumericUpDown()
        Me.Label23 = New System.Windows.Forms.Label()
        Me.numThreshold = New System.Windows.Forms.NumericUpDown()
        Me.Label22 = New System.Windows.Forms.Label()
        Me.btnCloseAll = New System.Windows.Forms.Button()
        Me.btnRoll = New System.Windows.Forms.Button()
        Me.btnEnter = New System.Windows.Forms.Button()
        Me.txtAmount = New System.Windows.Forms.TextBox()
        Me.lblAmountUnits = New System.Windows.Forms.Label()
        Me.radBTC = New System.Windows.Forms.RadioButton()
        Me.radUSD = New System.Windows.Forms.RadioButton()
        Me.Label21 = New System.Windows.Forms.Label()
        Me.grpLog = New System.Windows.Forms.GroupBox()
        Me.txtLog = New System.Windows.Forms.RichTextBox()
        Me.grpConnection.SuspendLayout()
        Me.grpMarket.SuspendLayout()
        Me.grpTime.SuspendLayout()
        Me.grpEntry.SuspendLayout()
        CType(Me.numSlippageBps, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numThreshold, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.grpLog.SuspendLayout()
        Me.SuspendLayout()
        '
        'grpConnection
        '
        Me.grpConnection.Controls.Add(Me.btnDiscoverWeekly)
        Me.grpConnection.Controls.Add(Me.lblConn)
        Me.grpConnection.Controls.Add(Me.btnConnect)
        Me.grpConnection.Controls.Add(Me.txtClientSecret)
        Me.grpConnection.Controls.Add(Me.Label2)
        Me.grpConnection.Controls.Add(Me.txtClientId)
        Me.grpConnection.Controls.Add(Me.Label1)
        Me.grpConnection.Location = New System.Drawing.Point(12, 12)
        Me.grpConnection.Name = "grpConnection"
        Me.grpConnection.Size = New System.Drawing.Size(776, 86)
        Me.grpConnection.TabIndex = 0
        Me.grpConnection.TabStop = False
        Me.grpConnection.Text = "Connection"
        '
        'btnDiscoverWeekly
        '
        Me.btnDiscoverWeekly.Location = New System.Drawing.Point(677, 19)
        Me.btnDiscoverWeekly.Name = "btnDiscoverWeekly"
        Me.btnDiscoverWeekly.Size = New System.Drawing.Size(93, 23)
        Me.btnDiscoverWeekly.TabIndex = 6
        Me.btnDiscoverWeekly.Text = "Discover Weekly"
        Me.btnDiscoverWeekly.UseVisualStyleBackColor = True
        '
        'lblConn
        '
        Me.lblConn.AutoSize = True
        Me.lblConn.Location = New System.Drawing.Point(13, 56)
        Me.lblConn.Name = "lblConn"
        Me.lblConn.Size = New System.Drawing.Size(113, 15)
        Me.lblConn.TabIndex = 5
        Me.lblConn.Text = "Disconnected (WS)"
        '
        'btnConnect
        '
        Me.btnConnect.Location = New System.Drawing.Point(596, 19)
        Me.btnConnect.Name = "btnConnect"
        Me.btnConnect.Size = New System.Drawing.Size(75, 23)
        Me.btnConnect.TabIndex = 4
        Me.btnConnect.Text = "Connect"
        Me.btnConnect.UseVisualStyleBackColor = True
        '
        'txtClientSecret
        '
        Me.txtClientSecret.Location = New System.Drawing.Point(359, 20)
        Me.txtClientSecret.Name = "txtClientSecret"
        Me.txtClientSecret.PasswordChar = Global.Microsoft.VisualBasic.ChrW(42)
        Me.txtClientSecret.Size = New System.Drawing.Size(231, 23)
        Me.txtClientSecret.TabIndex = 3
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(279, 23)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(74, 15)
        Me.Label2.TabIndex = 2
        Me.Label2.Text = "ClientSecret"
        '
        'txtClientId
        '
        Me.txtClientId.Location = New System.Drawing.Point(77, 20)
        Me.txtClientId.Name = "txtClientId"
        Me.txtClientId.Size = New System.Drawing.Size(196, 23)
        Me.txtClientId.TabIndex = 1
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(13, 23)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(50, 15)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "ClientID"
        '
        'grpMarket
        '
        Me.grpMarket.Controls.Add(Me.lblAnnual)
        Me.grpMarket.Controls.Add(Me.Label16)
        Me.grpMarket.Controls.Add(Me.lblBasis)
        Me.grpMarket.Controls.Add(Me.Label14)
        Me.grpMarket.Controls.Add(Me.lblFutMark)
        Me.grpMarket.Controls.Add(Me.Label12)
        Me.grpMarket.Controls.Add(Me.lblFutAsk)
        Me.grpMarket.Controls.Add(Me.lblFutBid)
        Me.grpMarket.Controls.Add(Me.Label9)
        Me.grpMarket.Controls.Add(Me.Label8)
        Me.grpMarket.Controls.Add(Me.lblSpotAsk)
        Me.grpMarket.Controls.Add(Me.lblSpotBid)
        Me.grpMarket.Controls.Add(Me.Label5)
        Me.grpMarket.Controls.Add(Me.Label4)
        Me.grpMarket.Controls.Add(Me.lblIndex)
        Me.grpMarket.Controls.Add(Me.Label3)
        Me.grpMarket.Location = New System.Drawing.Point(12, 104)
        Me.grpMarket.Name = "grpMarket"
        Me.grpMarket.Size = New System.Drawing.Size(776, 114)
        Me.grpMarket.TabIndex = 1
        Me.grpMarket.TabStop = False
        Me.grpMarket.Text = "Market"
        '
        'lblAnnual
        '
        Me.lblAnnual.AutoSize = True
        Me.lblAnnual.Location = New System.Drawing.Point(628, 74)
        Me.lblAnnual.Name = "lblAnnual"
        Me.lblAnnual.Size = New System.Drawing.Size(37, 15)
        Me.lblAnnual.TabIndex = 15
        Me.lblAnnual.Text = "0.00%"
        '
        'Label16
        '
        Me.Label16.AutoSize = True
        Me.Label16.Location = New System.Drawing.Point(570, 74)
        Me.Label16.Name = "Label16"
        Me.Label16.Size = New System.Drawing.Size(52, 15)
        Me.Label16.TabIndex = 14
        Me.Label16.Text = "Annual:"
        '
        'lblBasis
        '
        Me.lblBasis.AutoSize = True
        Me.lblBasis.Location = New System.Drawing.Point(628, 48)
        Me.lblBasis.Name = "lblBasis"
        Me.lblBasis.Size = New System.Drawing.Size(37, 15)
        Me.lblBasis.TabIndex = 13
        Me.lblBasis.Text = "0.00%"
        '
        'Label14
        '
        Me.Label14.AutoSize = True
        Me.Label14.Location = New System.Drawing.Point(581, 48)
        Me.Label14.Name = "Label14"
        Me.Label14.Size = New System.Drawing.Size(41, 15)
        Me.Label14.TabIndex = 12
        Me.Label14.Text = "Basis:"
        '
        'lblFutMark
        '
        Me.lblFutMark.AutoSize = True
        Me.lblFutMark.Location = New System.Drawing.Point(384, 74)
        Me.lblFutMark.Name = "lblFutMark"
        Me.lblFutMark.Size = New System.Drawing.Size(34, 15)
        Me.lblFutMark.TabIndex = 11
        Me.lblFutMark.Text = "0.00"
        '
        'Label12
        '
        Me.Label12.AutoSize = True
        Me.Label12.Location = New System.Drawing.Point(311, 74)
        Me.Label12.Name = "Label12"
        Me.Label12.Size = New System.Drawing.Size(67, 15)
        Me.Label12.TabIndex = 10
        Me.Label12.Text = "Fut Mark:"
        '
        'lblFutAsk
        '
        Me.lblFutAsk.AutoSize = True
        Me.lblFutAsk.Location = New System.Drawing.Point(384, 48)
        Me.lblFutAsk.Name = "lblFutAsk"
        Me.lblFutAsk.Size = New System.Drawing.Size(34, 15)
        Me.lblFutAsk.TabIndex = 9
        Me.lblFutAsk.Text = "0.00"
        '
        'lblFutBid
        '
        Me.lblFutBid.AutoSize = True
        Me.lblFutBid.Location = New System.Drawing.Point(384, 23)
        Me.lblFutBid.Name = "lblFutBid"
        Me.lblFutBid.Size = New System.Drawing.Size(34, 15)
        Me.lblFutBid.TabIndex = 8
        Me.lblFutBid.Text = "0.00"
        '
        'Label9
        '
        Me.Label9.AutoSize = True
        Me.Label9.Location = New System.Drawing.Point(318, 48)
        Me.Label9.Name = "Label9"
        Me.Label9.Size = New System.Drawing.Size(60, 15)
        Me.Label9.TabIndex = 7
        Me.Label9.Text = "Fut Ask:"
        '
        'Label8
        '
        Me.Label8.AutoSize = True
        Me.Label8.Location = New System.Drawing.Point(320, 23)
        Me.Label8.Name = "Label8"
        Me.Label8.Size = New System.Drawing.Size(58, 15)
        Me.Label8.TabIndex = 6
        Me.Label8.Text = "Fut Bid:"
        '
        'lblSpotAsk
        '
        Me.lblSpotAsk.AutoSize = True
        Me.lblSpotAsk.Location = New System.Drawing.Point(77, 74)
        Me.lblSpotAsk.Name = "lblSpotAsk"
        Me.lblSpotAsk.Size = New System.Drawing.Size(34, 15)
        Me.lblSpotAsk.TabIndex = 5
        Me.lblSpotAsk.Text = "0.00"
        '
        'lblSpotBid
        '
        Me.lblSpotBid.AutoSize = True
        Me.lblSpotBid.Location = New System.Drawing.Point(77, 48)
        Me.lblSpotBid.Name = "lblSpotBid"
        Me.lblSpotBid.Size = New System.Drawing.Size(34, 15)
        Me.lblSpotBid.TabIndex = 4
        Me.lblSpotBid.Text = "0.00"
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Location = New System.Drawing.Point(9, 74)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(62, 15)
        Me.Label5.TabIndex = 3
        Me.Label5.Text = "Spot Ask:"
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(11, 48)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(60, 15)
        Me.Label4.TabIndex = 2
        Me.Label4.Text = "Spot Bid:"
        '
        'lblIndex
        '
        Me.lblIndex.AutoSize = True
        Me.lblIndex.Location = New System.Drawing.Point(77, 23)
        Me.lblIndex.Name = "lblIndex"
        Me.lblIndex.Size = New System.Drawing.Size(34, 15)
        Me.lblIndex.TabIndex = 1
        Me.lblIndex.Text = "0.00"
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(26, 23)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(45, 15)
        Me.Label3.TabIndex = 0
        Me.Label3.Text = "Index:"
        '
        'grpTime
        '
        Me.grpTime.Controls.Add(Me.lblNowMYT)
        Me.grpTime.Controls.Add(Me.lblNowUTC)
        Me.grpTime.Controls.Add(Me.lblExpiryMYT)
        Me.grpTime.Controls.Add(Me.lblExpiryUTC)
        Me.grpTime.Controls.Add(Me.Label20)
        Me.grpTime.Controls.Add(Me.Label19)
        Me.grpTime.Controls.Add(Me.Label18)
        Me.grpTime.Controls.Add(Me.Label17)
        Me.grpTime.Location = New System.Drawing.Point(12, 224)
        Me.grpTime.Name = "grpTime"
        Me.grpTime.Size = New System.Drawing.Size(776, 92)
        Me.grpTime.TabIndex = 2
        Me.grpTime.TabStop = False
        Me.grpTime.Text = "Time / Expiry"
        '
        'lblNowMYT
        '
        Me.lblNowMYT.AutoSize = True
        Me.lblNowMYT.Location = New System.Drawing.Point(512, 58)
        Me.lblNowMYT.Name = "lblNowMYT"
        Me.lblNowMYT.Size = New System.Drawing.Size(168, 15)
        Me.lblNowMYT.TabIndex = 7
        Me.lblNowMYT.Text = "0000-00-00 00:00:00 (MYT)"
        '
        'lblNowUTC
        '
        Me.lblNowUTC.AutoSize = True
        Me.lblNowUTC.Location = New System.Drawing.Point(512, 28)
        Me.lblNowUTC.Name = "lblNowUTC"
        Me.lblNowUTC.Size = New System.Drawing.Size(169, 15)
        Me.lblNowUTC.TabIndex = 6
        Me.lblNowUTC.Text = "0000-00-00 00:00:00 (UTC)"
        '
        'lblExpiryMYT
        '
        Me.lblExpiryMYT.AutoSize = True
        Me.lblExpiryMYT.Location = New System.Drawing.Point(109, 58)
        Me.lblExpiryMYT.Name = "lblExpiryMYT"
        Me.lblExpiryMYT.Size = New System.Drawing.Size(168, 15)
        Me.lblExpiryMYT.TabIndex = 5
        Me.lblExpiryMYT.Text = "0000-00-00 00:00:00 (MYT)"
        '
        'lblExpiryUTC
        '
        Me.lblExpiryUTC.AutoSize = True
        Me.lblExpiryUTC.Location = New System.Drawing.Point(109, 28)
        Me.lblExpiryUTC.Name = "lblExpiryUTC"
        Me.lblExpiryUTC.Size = New System.Drawing.Size(169, 15)
        Me.lblExpiryUTC.TabIndex = 4
        Me.lblExpiryUTC.Text = "0000-00-00 00:00:00 (UTC)"
        '
        'Label20
        '
        Me.Label20.AutoSize = True
        Me.Label20.Location = New System.Drawing.Point(443, 58)
        Me.Label20.Name = "Label20"
        Me.Label20.Size = New System.Drawing.Size(63, 15)
        Me.Label20.TabIndex = 3
        Me.Label20.Text = "Now MYT:"
        '
        'Label19
        '
        Me.Label19.AutoSize = True
        Me.Label19.Location = New System.Drawing.Point(444, 28)
        Me.Label19.Name = "Label19"
        Me.Label19.Size = New System.Drawing.Size(62, 15)
        Me.Label19.TabIndex = 2
        Me.Label19.Text = "Now UTC:"
        '
        'Label18
        '
        Me.Label18.AutoSize = True
        Me.Label18.Location = New System.Drawing.Point(32, 58)
        Me.Label18.Name = "Label18"
        Me.Label18.Size = New System.Drawing.Size(71, 15)
        Me.Label18.TabIndex = 1
        Me.Label18.Text = "Expiry MYT:"
        '
        'Label17
        '
        Me.Label17.AutoSize = True
        Me.Label17.Location = New System.Drawing.Point(33, 28)
        Me.Label17.Name = "Label17"
        Me.Label17.Size = New System.Drawing.Size(70, 15)
        Me.Label17.TabIndex = 0
        Me.Label17.Text = "Expiry UTC:"
        '
        'grpEntry
        '
        Me.grpEntry.Controls.Add(Me.numSlippageBps)
        Me.grpEntry.Controls.Add(Me.Label23)
        Me.grpEntry.Controls.Add(Me.numThreshold)
        Me.grpEntry.Controls.Add(Me.Label22)
        Me.grpEntry.Controls.Add(Me.btnCloseAll)
        Me.grpEntry.Controls.Add(Me.btnRoll)
        Me.grpEntry.Controls.Add(Me.btnEnter)
        Me.grpEntry.Controls.Add(Me.txtAmount)
        Me.grpEntry.Controls.Add(Me.lblAmountUnits)
        Me.grpEntry.Controls.Add(Me.radBTC)
        Me.grpEntry.Controls.Add(Me.radUSD)
        Me.grpEntry.Controls.Add(Me.Label21)
        Me.grpEntry.Location = New System.Drawing.Point(12, 322)
        Me.grpEntry.Name = "grpEntry"
        Me.grpEntry.Size = New System.Drawing.Size(776, 106)
        Me.grpEntry.TabIndex = 3
        Me.grpEntry.TabStop = False
        Me.grpEntry.Text = "Entry / Risk"
        '
        'numSlippageBps
        '
        Me.numSlippageBps.DecimalPlaces = 0
        Me.numSlippageBps.Increment = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numSlippageBps.Location = New System.Drawing.Point(542, 62)
        Me.numSlippageBps.Maximum = New Decimal(New Integer() {1000, 0, 0, 0})
        Me.numSlippageBps.Minimum = New Decimal(New Integer() {0, 0, 0, 0})
        Me.numSlippageBps.Name = "numSlippageBps"
        Me.numSlippageBps.Size = New System.Drawing.Size(68, 23)
        Me.numSlippageBps.TabIndex = 11
        Me.numSlippageBps.Value = New Decimal(New Integer() {5, 0, 0, 0})
        '
        'Label23
        '
        Me.Label23.AutoSize = True
        Me.Label23.Location = New System.Drawing.Point(444, 64)
        Me.Label23.Name = "Label23"
        Me.Label23.Size = New System.Drawing.Size(92, 15)
        Me.Label23.TabIndex = 10
        Me.Label23.Text = "Max Slippage bps"
        '
        'numThreshold
        '
        Me.numThreshold.DecimalPlaces = 4
        Me.numThreshold.Increment = New Decimal(New Integer() {1, 0, 0, 262144})
        Me.numThreshold.Location = New System.Drawing.Point(542, 28)
        Me.numThreshold.Maximum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.numThreshold.Minimum = New Decimal(New Integer() {0, 0, 0, 0})
        Me.numThreshold.Name = "numThreshold"
        Me.numThreshold.Size = New System.Drawing.Size(68, 23)
        Me.numThreshold.TabIndex = 9
        Me.numThreshold.Value = New Decimal(New Integer() {25, 0, 0, 262144})
        '
        'Label22
        '
        Me.Label22.AutoSize = True
        Me.Label22.Location = New System.Drawing.Point(452, 30)
        Me.Label22.Name = "Label22"
        Me.Label22.Size = New System.Drawing.Size(84, 15)
        Me.Label22.TabIndex = 8
        Me.Label22.Text = "Entry Threshold"
        '
        'btnCloseAll
        '
        Me.btnCloseAll.Location = New System.Drawing.Point(661, 62)
        Me.btnCloseAll.Name = "btnCloseAll"
        Me.btnCloseAll.Size = New System.Drawing.Size(109, 23)
        Me.btnCloseAll.TabIndex = 7
        Me.btnCloseAll.Text = "Close All"
        Me.btnCloseAll.UseVisualStyleBackColor = True
        '
        'btnRoll
        '
        Me.btnRoll.Location = New System.Drawing.Point(661, 22)
        Me.btnRoll.Name = "btnRoll"
        Me.btnRoll.Size = New System.Drawing.Size(109, 23)
        Me.btnRoll.TabIndex = 6
        Me.btnRoll.Text = "Roll to Next"
        Me.btnRoll.UseVisualStyleBackColor = True
        '
        'btnEnter
        '
        Me.btnEnter.Location = New System.Drawing.Point(350, 27)
        Me.btnEnter.Name = "btnEnter"
        Me.btnEnter.Size = New System.Drawing.Size(85, 58)
        Me.btnEnter.TabIndex = 5
        Me.btnEnter.Text = "Enter Basis"
        Me.btnEnter.UseVisualStyleBackColor = True
        '
        'txtAmount
        '
        Me.txtAmount.Location = New System.Drawing.Point(86, 62)
        Me.txtAmount.Name = "txtAmount"
        Me.txtAmount.Size = New System.Drawing.Size(243, 23)
        Me.txtAmount.TabIndex = 4
        Me.txtAmount.Text = "1000"
        '
        'lblAmountUnits
        '
        Me.lblAmountUnits.AutoSize = True
        Me.lblAmountUnits.Location = New System.Drawing.Point(13, 65)
        Me.lblAmountUnits.Name = "lblAmountUnits"
        Me.lblAmountUnits.Size = New System.Drawing.Size(67, 15)
        Me.lblAmountUnits.TabIndex = 3
        Me.lblAmountUnits.Text = "Amount USD"
        '
        'radBTC
        '
        Me.radBTC.AutoSize = True
        Me.radBTC.Location = New System.Drawing.Point(153, 28)
        Me.radBTC.Name = "radBTC"
        Me.radBTC.Size = New System.Drawing.Size(48, 19)
        Me.radBTC.TabIndex = 2
        Me.radBTC.Text = "BTC"
        Me.radBTC.UseVisualStyleBackColor = True
        '
        'radUSD
        '
        Me.radUSD.AutoSize = True
        Me.radUSD.Checked = True
        Me.radUSD.Location = New System.Drawing.Point(86, 28)
        Me.radUSD.Name = "radUSD"
        Me.radUSD.Size = New System.Drawing.Size(49, 19)
        Me.radUSD.TabIndex = 1
        Me.radUSD.TabStop = True
        Me.radUSD.Text = "USD"
        Me.radUSD.UseVisualStyleBackColor = True
        '
        'Label21
        '
        Me.Label21.AutoSize = True
        Me.Label21.Location = New System.Drawing.Point(13, 30)
        Me.Label21.Name = "Label21"
        Me.Label21.Size = New System.Drawing.Size(67, 15)
        Me.Label21.TabIndex = 0
        Me.Label21.Text = "Size Input:"
        '
        'grpLog
        '
        Me.grpLog.Controls.Add(Me.txtLog)
        Me.grpLog.Location = New System.Drawing.Point(12, 434)
        Me.grpLog.Name = "grpLog"
        Me.grpLog.Size = New System.Drawing.Size(776, 204)
        Me.grpLog.TabIndex = 4
        Me.grpLog.TabStop = False
        Me.grpLog.Text = "Log"
        '
        'txtLog
        '
        Me.txtLog.Dock = System.Windows.Forms.DockStyle.Fill
        Me.txtLog.Location = New System.Drawing.Point(3, 19)
        Me.txtLog.Name = "txtLog"
        Me.txtLog.Size = New System.Drawing.Size(770, 182)
        Me.txtLog.TabIndex = 0
        Me.txtLog.Text = ""
        '
        'frmContangoMain
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(800, 650)
        Me.Controls.Add(Me.grpLog)
        Me.Controls.Add(Me.grpEntry)
        Me.Controls.Add(Me.grpTime)
        Me.Controls.Add(Me.grpMarket)
        Me.Controls.Add(Me.grpConnection)
        Me.Name = "frmContangoMain"
        Me.Text = "Deribit Contango Basis Trader"
        Me.grpConnection.ResumeLayout(False)
        Me.grpConnection.PerformLayout()
        Me.grpMarket.ResumeLayout(False)
        Me.grpMarket.PerformLayout()
        Me.grpTime.ResumeLayout(False)
        Me.grpTime.PerformLayout()
        Me.grpEntry.ResumeLayout(False)
        Me.grpEntry.PerformLayout()
        CType(Me.numSlippageBps, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numThreshold, System.ComponentModel.ISupportInitialize).EndInit()
        Me.grpLog.ResumeLayout(False)
        Me.ResumeLayout(False)

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
End Class
