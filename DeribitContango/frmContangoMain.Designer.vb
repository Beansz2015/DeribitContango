<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmContangoMain
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
        pnlConnectionStatus = New Panel()
        lblLastUpdate = New Label()
        lblRateLimit = New Label()
        lblConnectionStatus = New Label()
        pnlMarketData = New Panel()
        lblAnnualizedReturn = New Label()
        Label4 = New Label()
        lblBasisSpread = New Label()
        Label3 = New Label()
        lblWeeklyFuturesPrice = New Label()
        Label2 = New Label()
        lblSpotPrice = New Label()
        Label1 = New Label()
        pnlPositionManagement = New Panel()
        grpCurrentPosition = New GroupBox()
        btnClosePosition = New Button()
        lblDaysToExpiry = New Label()
        lblUnrealizedPnL = New Label()
        lblPositionStatus = New Label()
        grpPositionEntry = New GroupBox()
        btnRollPosition = New Button()
        btnExecuteCashCarry = New Button()
        nudMinBasisThreshold = New NumericUpDown()
        Label6 = New Label()
        nudPositionSize = New NumericUpDown()
        Label5 = New Label()
        pnlTradingLog = New Panel()
        btnClearLogs = New Button()
        txtLogs = New RichTextBox()
        Label7 = New Label()
        pnlConnectionStatus.SuspendLayout()
        pnlMarketData.SuspendLayout()
        pnlPositionManagement.SuspendLayout()
        grpCurrentPosition.SuspendLayout()
        grpPositionEntry.SuspendLayout()
        CType(nudMinBasisThreshold, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudPositionSize, ComponentModel.ISupportInitialize).BeginInit()
        pnlTradingLog.SuspendLayout()
        SuspendLayout()
        ' 
        ' pnlConnectionStatus
        ' 
        pnlConnectionStatus.BackColor = SystemColors.AppWorkspace
        pnlConnectionStatus.Controls.Add(lblLastUpdate)
        pnlConnectionStatus.Controls.Add(lblRateLimit)
        pnlConnectionStatus.Controls.Add(lblConnectionStatus)
        pnlConnectionStatus.Dock = DockStyle.Top
        pnlConnectionStatus.Location = New Point(0, 0)
        pnlConnectionStatus.Name = "pnlConnectionStatus"
        pnlConnectionStatus.Size = New Size(1178, 60)
        pnlConnectionStatus.TabIndex = 0
        ' 
        ' lblLastUpdate
        ' 
        lblLastUpdate.AutoSize = True
        lblLastUpdate.ForeColor = SystemColors.HotTrack
        lblLastUpdate.Location = New Point(380, 15)
        lblLastUpdate.MinimumSize = New Size(200, 25)
        lblLastUpdate.Name = "lblLastUpdate"
        lblLastUpdate.Size = New Size(200, 25)
        lblLastUpdate.TabIndex = 2
        lblLastUpdate.Text = "Last Update: --:--:--"
        ' 
        ' lblRateLimit
        ' 
        lblRateLimit.AutoSize = True
        lblRateLimit.ForeColor = SystemColors.HotTrack
        lblRateLimit.Location = New Point(220, 15)
        lblRateLimit.MinimumSize = New Size(150, 25)
        lblRateLimit.Name = "lblRateLimit"
        lblRateLimit.Size = New Size(150, 25)
        lblRateLimit.TabIndex = 1
        lblRateLimit.Text = "Rate: 0/50"
        ' 
        ' lblConnectionStatus
        ' 
        lblConnectionStatus.AutoSize = True
        lblConnectionStatus.ForeColor = SystemColors.HotTrack
        lblConnectionStatus.Location = New Point(10, 15)
        lblConnectionStatus.MinimumSize = New Size(200, 25)
        lblConnectionStatus.Name = "lblConnectionStatus"
        lblConnectionStatus.Size = New Size(200, 25)
        lblConnectionStatus.TabIndex = 0
        lblConnectionStatus.Text = "Status: Connecting..."
        ' 
        ' pnlMarketData
        ' 
        pnlMarketData.BackColor = Color.WhiteSmoke
        pnlMarketData.Controls.Add(lblAnnualizedReturn)
        pnlMarketData.Controls.Add(Label4)
        pnlMarketData.Controls.Add(lblBasisSpread)
        pnlMarketData.Controls.Add(Label3)
        pnlMarketData.Controls.Add(lblWeeklyFuturesPrice)
        pnlMarketData.Controls.Add(Label2)
        pnlMarketData.Controls.Add(lblSpotPrice)
        pnlMarketData.Controls.Add(Label1)
        pnlMarketData.Dock = DockStyle.Left
        pnlMarketData.Location = New Point(0, 60)
        pnlMarketData.MinimumSize = New Size(350, 0)
        pnlMarketData.Name = "pnlMarketData"
        pnlMarketData.Size = New Size(350, 684)
        pnlMarketData.TabIndex = 1
        ' 
        ' lblAnnualizedReturn
        ' 
        lblAnnualizedReturn.AutoSize = True
        lblAnnualizedReturn.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblAnnualizedReturn.ForeColor = Color.DarkGreen
        lblAnnualizedReturn.Location = New Point(10, 255)
        lblAnnualizedReturn.MinimumSize = New Size(150, 30)
        lblAnnualizedReturn.Name = "lblAnnualizedReturn"
        lblAnnualizedReturn.Size = New Size(150, 32)
        lblAnnualizedReturn.TabIndex = 8
        lblAnnualizedReturn.Text = "0.0%"
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Font = New Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label4.ForeColor = SystemColors.Desktop
        Label4.Location = New Point(10, 230)
        Label4.MinimumSize = New Size(200, 25)
        Label4.Name = "Label4"
        Label4.Size = New Size(200, 28)
        Label4.TabIndex = 7
        Label4.Text = "Annualized Return"
        ' 
        ' lblBasisSpread
        ' 
        lblBasisSpread.AutoSize = True
        lblBasisSpread.Font = New Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblBasisSpread.ForeColor = Color.Green
        lblBasisSpread.Location = New Point(10, 185)
        lblBasisSpread.MinimumSize = New Size(150, 30)
        lblBasisSpread.Name = "lblBasisSpread"
        lblBasisSpread.Size = New Size(150, 38)
        lblBasisSpread.TabIndex = 6
        lblBasisSpread.Text = "0.000%"
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Font = New Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label3.ForeColor = SystemColors.Desktop
        Label3.Location = New Point(10, 160)
        Label3.MinimumSize = New Size(200, 25)
        Label3.Name = "Label3"
        Label3.Size = New Size(206, 28)
        Label3.TabIndex = 5
        Label3.Text = "Weekly Basis Spread"
        ' 
        ' lblWeeklyFuturesPrice
        ' 
        lblWeeklyFuturesPrice.AutoSize = True
        lblWeeklyFuturesPrice.Font = New Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblWeeklyFuturesPrice.ForeColor = Color.Purple
        lblWeeklyFuturesPrice.Location = New Point(10, 115)
        lblWeeklyFuturesPrice.MinimumSize = New Size(200, 30)
        lblWeeklyFuturesPrice.Name = "lblWeeklyFuturesPrice"
        lblWeeklyFuturesPrice.Size = New Size(200, 38)
        lblWeeklyFuturesPrice.TabIndex = 4
        lblWeeklyFuturesPrice.Text = "$0.00"
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Font = New Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label2.ForeColor = SystemColors.Desktop
        Label2.Location = New Point(10, 90)
        Label2.MinimumSize = New Size(200, 25)
        Label2.Name = "Label2"
        Label2.Size = New Size(210, 28)
        Label2.TabIndex = 3
        Label2.Text = "Weekly Futures Price"
        ' 
        ' lblSpotPrice
        ' 
        lblSpotPrice.AutoSize = True
        lblSpotPrice.Font = New Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblSpotPrice.ForeColor = SystemColors.HotTrack
        lblSpotPrice.Location = New Point(10, 45)
        lblSpotPrice.MinimumSize = New Size(200, 25)
        lblSpotPrice.Name = "lblSpotPrice"
        lblSpotPrice.Size = New Size(200, 38)
        lblSpotPrice.TabIndex = 2
        lblSpotPrice.Text = "$0.00"
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Font = New Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label1.ForeColor = SystemColors.Desktop
        Label1.Location = New Point(10, 20)
        Label1.MinimumSize = New Size(200, 25)
        Label1.Name = "Label1"
        Label1.Size = New Size(200, 28)
        Label1.TabIndex = 1
        Label1.Text = "BTC Spot Price"
        ' 
        ' pnlPositionManagement
        ' 
        pnlPositionManagement.BackColor = Color.White
        pnlPositionManagement.Controls.Add(grpCurrentPosition)
        pnlPositionManagement.Controls.Add(grpPositionEntry)
        pnlPositionManagement.Dock = DockStyle.Fill
        pnlPositionManagement.Location = New Point(350, 60)
        pnlPositionManagement.Name = "pnlPositionManagement"
        pnlPositionManagement.Size = New Size(828, 684)
        pnlPositionManagement.TabIndex = 2
        ' 
        ' grpCurrentPosition
        ' 
        grpCurrentPosition.Controls.Add(btnClosePosition)
        grpCurrentPosition.Controls.Add(lblDaysToExpiry)
        grpCurrentPosition.Controls.Add(lblUnrealizedPnL)
        grpCurrentPosition.Controls.Add(lblPositionStatus)
        grpCurrentPosition.Location = New Point(10, 140)
        grpCurrentPosition.Name = "grpCurrentPosition"
        grpCurrentPosition.Size = New Size(400, 100)
        grpCurrentPosition.TabIndex = 1
        grpCurrentPosition.TabStop = False
        grpCurrentPosition.Text = "Current Position"
        ' 
        ' btnClosePosition
        ' 
        btnClosePosition.BackColor = Color.LightCoral
        btnClosePosition.Enabled = False
        btnClosePosition.Location = New Point(206, 20)
        btnClosePosition.Name = "btnClosePosition"
        btnClosePosition.Size = New Size(182, 30)
        btnClosePosition.TabIndex = 8
        btnClosePosition.Text = "Close Position"
        btnClosePosition.UseVisualStyleBackColor = False
        ' 
        ' lblDaysToExpiry
        ' 
        lblDaysToExpiry.AutoSize = True
        lblDaysToExpiry.Font = New Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblDaysToExpiry.ForeColor = SystemColors.Desktop
        lblDaysToExpiry.Location = New Point(200, 50)
        lblDaysToExpiry.MinimumSize = New Size(200, 25)
        lblDaysToExpiry.Name = "lblDaysToExpiry"
        lblDaysToExpiry.Size = New Size(200, 25)
        lblDaysToExpiry.TabIndex = 7
        lblDaysToExpiry.Text = "Days to Expiry: --"
        ' 
        ' lblUnrealizedPnL
        ' 
        lblUnrealizedPnL.AutoSize = True
        lblUnrealizedPnL.Font = New Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblUnrealizedPnL.ForeColor = SystemColors.Desktop
        lblUnrealizedPnL.Location = New Point(15, 50)
        lblUnrealizedPnL.MinimumSize = New Size(200, 25)
        lblUnrealizedPnL.Name = "lblUnrealizedPnL"
        lblUnrealizedPnL.Size = New Size(200, 25)
        lblUnrealizedPnL.TabIndex = 6
        lblUnrealizedPnL.Text = "Unrealized PL: $0.00"
        ' 
        ' lblPositionStatus
        ' 
        lblPositionStatus.AutoSize = True
        lblPositionStatus.Font = New Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblPositionStatus.ForeColor = SystemColors.Desktop
        lblPositionStatus.Location = New Point(15, 25)
        lblPositionStatus.MinimumSize = New Size(200, 25)
        lblPositionStatus.Name = "lblPositionStatus"
        lblPositionStatus.Size = New Size(200, 25)
        lblPositionStatus.TabIndex = 5
        lblPositionStatus.Text = "No active position"
        ' 
        ' grpPositionEntry
        ' 
        grpPositionEntry.Controls.Add(btnRollPosition)
        grpPositionEntry.Controls.Add(btnExecuteCashCarry)
        grpPositionEntry.Controls.Add(nudMinBasisThreshold)
        grpPositionEntry.Controls.Add(Label6)
        grpPositionEntry.Controls.Add(nudPositionSize)
        grpPositionEntry.Controls.Add(Label5)
        grpPositionEntry.Location = New Point(10, 10)
        grpPositionEntry.Name = "grpPositionEntry"
        grpPositionEntry.Size = New Size(400, 120)
        grpPositionEntry.TabIndex = 0
        grpPositionEntry.TabStop = False
        grpPositionEntry.Text = "Position Entry"
        ' 
        ' btnRollPosition
        ' 
        btnRollPosition.BackColor = Color.LightBlue
        btnRollPosition.Enabled = False
        btnRollPosition.Location = New Point(190, 83)
        btnRollPosition.Name = "btnRollPosition"
        btnRollPosition.Size = New Size(120, 35)
        btnRollPosition.TabIndex = 10
        btnRollPosition.Text = "Roll Position"
        btnRollPosition.UseVisualStyleBackColor = False
        ' 
        ' btnExecuteCashCarry
        ' 
        btnExecuteCashCarry.BackColor = Color.LightGreen
        btnExecuteCashCarry.Enabled = False
        btnExecuteCashCarry.Location = New Point(15, 83)
        btnExecuteCashCarry.Name = "btnExecuteCashCarry"
        btnExecuteCashCarry.Size = New Size(171, 35)
        btnExecuteCashCarry.TabIndex = 9
        btnExecuteCashCarry.Text = "Execute Cash-Carry"
        btnExecuteCashCarry.UseVisualStyleBackColor = False
        ' 
        ' nudMinBasisThreshold
        ' 
        nudMinBasisThreshold.DecimalPlaces = 3
        nudMinBasisThreshold.Increment = New Decimal(New Integer() {1, 0, 0, 196608})
        nudMinBasisThreshold.Location = New Point(192, 52)
        nudMinBasisThreshold.Maximum = New Decimal(New Integer() {1, 0, 0, 0})
        nudMinBasisThreshold.Minimum = New Decimal(New Integer() {1, 0, 0, 196608})
        nudMinBasisThreshold.Name = "nudMinBasisThreshold"
        nudMinBasisThreshold.Size = New Size(100, 31)
        nudMinBasisThreshold.TabIndex = 8
        nudMinBasisThreshold.Value = New Decimal(New Integer() {2, 0, 0, 196608})
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Font = New Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        Label6.ForeColor = SystemColors.Desktop
        Label6.Location = New Point(15, 55)
        Label6.MinimumSize = New Size(200, 25)
        Label6.Name = "Label6"
        Label6.Size = New Size(200, 25)
        Label6.TabIndex = 7
        Label6.Text = "Min Basis Threshold:"
        ' 
        ' nudPositionSize
        ' 
        nudPositionSize.DecimalPlaces = 4
        nudPositionSize.Increment = New Decimal(New Integer() {1, 0, 0, 196608})
        nudPositionSize.Location = New Point(192, 22)
        nudPositionSize.Maximum = New Decimal(New Integer() {10, 0, 0, 0})
        nudPositionSize.Minimum = New Decimal(New Integer() {1, 0, 0, 196608})
        nudPositionSize.Name = "nudPositionSize"
        nudPositionSize.Size = New Size(100, 31)
        nudPositionSize.TabIndex = 6
        nudPositionSize.Value = New Decimal(New Integer() {1, 0, 0, 131072})
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Font = New Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        Label5.ForeColor = SystemColors.Desktop
        Label5.Location = New Point(15, 25)
        Label5.MinimumSize = New Size(200, 25)
        Label5.Name = "Label5"
        Label5.Size = New Size(200, 25)
        Label5.TabIndex = 4
        Label5.Text = "Position Size (BTC):"
        ' 
        ' pnlTradingLog
        ' 
        pnlTradingLog.Controls.Add(btnClearLogs)
        pnlTradingLog.Controls.Add(txtLogs)
        pnlTradingLog.Controls.Add(Label7)
        pnlTradingLog.Dock = DockStyle.Bottom
        pnlTradingLog.ForeColor = Color.Black
        pnlTradingLog.Location = New Point(350, 494)
        pnlTradingLog.Name = "pnlTradingLog"
        pnlTradingLog.Size = New Size(828, 250)
        pnlTradingLog.TabIndex = 3
        ' 
        ' btnClearLogs
        ' 
        btnClearLogs.Location = New Point(700, 0)
        btnClearLogs.Name = "btnClearLogs"
        btnClearLogs.Size = New Size(90, 33)
        btnClearLogs.TabIndex = 9
        btnClearLogs.Text = "Clear Logs"
        btnClearLogs.UseVisualStyleBackColor = True
        ' 
        ' txtLogs
        ' 
        txtLogs.BackColor = Color.Black
        txtLogs.Font = New Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        txtLogs.ForeColor = Color.Lime
        txtLogs.Location = New Point(10, 36)
        txtLogs.Name = "txtLogs"
        txtLogs.ReadOnly = True
        txtLogs.ScrollBars = RichTextBoxScrollBars.Vertical
        txtLogs.Size = New Size(780, 200)
        txtLogs.TabIndex = 8
        txtLogs.Text = ""
        ' 
        ' Label7
        ' 
        Label7.AutoSize = True
        Label7.Font = New Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label7.ForeColor = Color.White
        Label7.Location = New Point(10, 5)
        Label7.MinimumSize = New Size(200, 25)
        Label7.Name = "Label7"
        Label7.Size = New Size(200, 28)
        Label7.TabIndex = 7
        Label7.Text = "Trading Log"
        ' 
        ' frmContangoMain
        ' 
        AutoScaleDimensions = New SizeF(10F, 25F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = SystemColors.ActiveCaptionText
        ClientSize = New Size(1178, 744)
        Controls.Add(pnlTradingLog)
        Controls.Add(pnlPositionManagement)
        Controls.Add(pnlMarketData)
        Controls.Add(pnlConnectionStatus)
        Name = "frmContangoMain"
        StartPosition = FormStartPosition.CenterScreen
        Text = "DeribitContango v0.2 - BTC Basis Trading"
        pnlConnectionStatus.ResumeLayout(False)
        pnlConnectionStatus.PerformLayout()
        pnlMarketData.ResumeLayout(False)
        pnlMarketData.PerformLayout()
        pnlPositionManagement.ResumeLayout(False)
        grpCurrentPosition.ResumeLayout(False)
        grpCurrentPosition.PerformLayout()
        grpPositionEntry.ResumeLayout(False)
        grpPositionEntry.PerformLayout()
        CType(nudMinBasisThreshold, ComponentModel.ISupportInitialize).EndInit()
        CType(nudPositionSize, ComponentModel.ISupportInitialize).EndInit()
        pnlTradingLog.ResumeLayout(False)
        pnlTradingLog.PerformLayout()
        ResumeLayout(False)
    End Sub

    Friend WithEvents pnlConnectionStatus As Panel
    Friend WithEvents lblConnectionStatus As Label
    Friend WithEvents lblLastUpdate As Label
    Friend WithEvents lblRateLimit As Label
    Friend WithEvents pnlMarketData As Panel
    Friend WithEvents Label3 As Label
    Friend WithEvents lblWeeklyFuturesPrice As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents lblSpotPrice As Label
    Friend WithEvents Label1 As Label
    Friend WithEvents lblAnnualizedReturn As Label
    Friend WithEvents Label4 As Label
    Friend WithEvents lblBasisSpread As Label
    Friend WithEvents pnlPositionManagement As Panel
    Friend WithEvents grpPositionEntry As GroupBox
    Friend WithEvents Label5 As Label
    Friend WithEvents btnExecuteCashCarry As Button
    Friend WithEvents nudMinBasisThreshold As NumericUpDown
    Friend WithEvents Label6 As Label
    Friend WithEvents nudPositionSize As NumericUpDown
    Friend WithEvents grpCurrentPosition As GroupBox
    Friend WithEvents lblPositionStatus As Label
    Friend WithEvents btnRollPosition As Button
    Friend WithEvents btnClosePosition As Button
    Friend WithEvents lblDaysToExpiry As Label
    Friend WithEvents lblUnrealizedPnL As Label
    Friend WithEvents pnlTradingLog As Panel
    Friend WithEvents btnClearLogs As Button
    Friend WithEvents txtLogs As RichTextBox
    Friend WithEvents Label7 As Label
End Class
