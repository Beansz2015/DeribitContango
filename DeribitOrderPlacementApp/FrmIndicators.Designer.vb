<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FrmIndicators
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
        components = New ComponentModel.Container()
        lblDMI = New Label()
        lblMACD = New Label()
        lblRSI = New Label()
        lblStoch = New Label()
        lblDMITitle = New Label()
        lblMACDTitle = New Label()
        lblRSITitle = New Label()
        lblStochTitle = New Label()
        txtIndLogs = New RichTextBox()
        redHeartBeat = New RadioButton()
        lblEMATitle = New Label()
        lblEMA = New Label()
        lblVWAPTitle = New Label()
        lblVWAP = New Label()
        lblScore = New Label()
        lblATRTitle = New Label()
        lblATR = New Label()
        btnATR = New Button()
        btnAutoTrade = New Button()
        GroupBox1 = New GroupBox()
        txtTestTime = New TextBox()
        Label2 = New Label()
        Label1 = New Label()
        btnBacktest = New Button()
        btnAutoTradeSettings = New Button()
        IndicatorsToolTip = New ToolTip(components)
        GroupBox1.SuspendLayout()
        SuspendLayout()
        ' 
        ' lblDMI
        ' 
        lblDMI.AutoSize = True
        lblDMI.Font = New Font("Calibri", 14F)
        lblDMI.ForeColor = SystemColors.ControlLight
        lblDMI.Location = New Point(110, 122)
        lblDMI.Name = "lblDMI"
        lblDMI.Size = New Size(99, 35)
        lblDMI.TabIndex = 104
        lblDMI.Text = "Neutral"
        ' 
        ' lblMACD
        ' 
        lblMACD.AutoSize = True
        lblMACD.Font = New Font("Calibri", 14F)
        lblMACD.ForeColor = SystemColors.ControlLight
        lblMACD.Location = New Point(110, 157)
        lblMACD.Name = "lblMACD"
        lblMACD.Size = New Size(99, 35)
        lblMACD.TabIndex = 105
        lblMACD.Text = "Neutral"
        ' 
        ' lblRSI
        ' 
        lblRSI.AutoSize = True
        lblRSI.Font = New Font("Calibri", 14F)
        lblRSI.ForeColor = SystemColors.ControlLight
        lblRSI.Location = New Point(110, 192)
        lblRSI.Name = "lblRSI"
        lblRSI.Size = New Size(99, 35)
        lblRSI.TabIndex = 106
        lblRSI.Text = "Neutral"
        ' 
        ' lblStoch
        ' 
        lblStoch.AutoSize = True
        lblStoch.Font = New Font("Calibri", 14F)
        lblStoch.ForeColor = SystemColors.ControlLight
        lblStoch.Location = New Point(110, 227)
        lblStoch.Name = "lblStoch"
        lblStoch.Size = New Size(99, 35)
        lblStoch.TabIndex = 107
        lblStoch.Text = "Neutral"
        ' 
        ' lblDMITitle
        ' 
        lblDMITitle.AutoSize = True
        lblDMITitle.Font = New Font("Calibri", 14F)
        lblDMITitle.ForeColor = SystemColors.ControlLight
        lblDMITitle.Location = New Point(5, 122)
        lblDMITitle.Name = "lblDMITitle"
        lblDMITitle.Size = New Size(63, 35)
        lblDMITitle.TabIndex = 118
        lblDMITitle.Text = "DMI"
        IndicatorsToolTip.SetToolTip(lblDMITitle, "ADX > 40 = Strong | > 30 = Normal | else = Weak" & vbCrLf & "Strength upgrade if within 3 candles after crossover." & vbCrLf & "ADX > 22 only considered.")
        ' 
        ' lblMACDTitle
        ' 
        lblMACDTitle.AutoSize = True
        lblMACDTitle.Font = New Font("Calibri", 14F)
        lblMACDTitle.ForeColor = SystemColors.ControlLight
        lblMACDTitle.Location = New Point(5, 157)
        lblMACDTitle.Name = "lblMACDTitle"
        lblMACDTitle.Size = New Size(87, 35)
        lblMACDTitle.TabIndex = 119
        lblMACDTitle.Text = "MACD"
        IndicatorsToolTip.SetToolTip(lblMACDTitle, "MACD + EMA confirmation = 1 score" & vbCrLf & "Confirmation with own histogram (<-10 | >10)" & vbCrLf & "within 3 candle time period = Strength upgrade")
        ' 
        ' lblRSITitle
        ' 
        lblRSITitle.AutoSize = True
        lblRSITitle.Font = New Font("Calibri", 14F)
        lblRSITitle.ForeColor = SystemColors.ControlLight
        lblRSITitle.Location = New Point(5, 192)
        lblRSITitle.Name = "lblRSITitle"
        lblRSITitle.Size = New Size(50, 35)
        lblRSITitle.TabIndex = 120
        lblRSITitle.Text = "RSI"
        IndicatorsToolTip.SetToolTip(lblRSITitle, "RSI + Stochastic confirmation = 1 score" & vbCrLf & "Has momentum signal if heading strongly" & vbCrLf & "in one direction." & vbCrLf & "Strength upgrade if within 3 candles in " & vbCrLf & "overbought/sold region (<15 or >85)")
        ' 
        ' lblStochTitle
        ' 
        lblStochTitle.AutoSize = True
        lblStochTitle.Font = New Font("Calibri", 14F)
        lblStochTitle.ForeColor = SystemColors.ControlLight
        lblStochTitle.Location = New Point(5, 227)
        lblStochTitle.Name = "lblStochTitle"
        lblStochTitle.Size = New Size(79, 35)
        lblStochTitle.TabIndex = 121
        lblStochTitle.Text = "Stoch"
        IndicatorsToolTip.SetToolTip(lblStochTitle, "Stoch < 25  (Strong Buy) < 40 (Normal)" & vbCrLf & "> 60 (Normal Sell) > 75 (Strong Sell)" & vbCrLf & "Else Weak Buy/Sell" & vbCrLf & "Strength upgrade if within 3 candles of crossover." & vbCrLf & "Stoch + RSI confirmation = 1 score")
        ' 
        ' txtIndLogs
        ' 
        txtIndLogs.AutoWordSelection = True
        txtIndLogs.BackColor = SystemColors.ActiveCaptionText
        txtIndLogs.BorderStyle = BorderStyle.FixedSingle
        txtIndLogs.Cursor = Cursors.IBeam
        txtIndLogs.Font = New Font("Calibri", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        txtIndLogs.ForeColor = SystemColors.InactiveBorder
        txtIndLogs.Location = New Point(12, 312)
        txtIndLogs.Name = "txtIndLogs"
        txtIndLogs.Size = New Size(485, 247)
        txtIndLogs.TabIndex = 122
        txtIndLogs.Text = ""
        ' 
        ' redHeartBeat
        ' 
        redHeartBeat.Appearance = Appearance.Button
        redHeartBeat.BackColor = Color.Black
        redHeartBeat.FlatAppearance.BorderSize = 0
        redHeartBeat.FlatStyle = FlatStyle.Flat
        redHeartBeat.ForeColor = Color.Black
        redHeartBeat.Location = New Point(12, 17)
        redHeartBeat.Name = "redHeartBeat"
        redHeartBeat.Size = New Size(26, 26)
        redHeartBeat.TabIndex = 123
        redHeartBeat.UseVisualStyleBackColor = False
        ' 
        ' lblEMATitle
        ' 
        lblEMATitle.AutoSize = True
        lblEMATitle.Font = New Font("Calibri", 14F)
        lblEMATitle.ForeColor = SystemColors.ControlLight
        lblEMATitle.Location = New Point(5, 87)
        lblEMATitle.Name = "lblEMATitle"
        lblEMATitle.Size = New Size(69, 35)
        lblEMATitle.TabIndex = 125
        lblEMATitle.Text = "EMA"
        IndicatorsToolTip.SetToolTip(lblEMATitle, "VWAP + EMA confirmation = 1 score" & vbCrLf & "<10D = Weak | <25D = Normal | Else = Strong" & vbCrLf & "Strength upgrade if within 3 candles of crossover.")
        ' 
        ' lblEMA
        ' 
        lblEMA.AutoSize = True
        lblEMA.Font = New Font("Calibri", 14F)
        lblEMA.ForeColor = SystemColors.ControlLight
        lblEMA.Location = New Point(110, 87)
        lblEMA.Name = "lblEMA"
        lblEMA.Size = New Size(99, 35)
        lblEMA.TabIndex = 124
        lblEMA.Text = "Neutral"
        ' 
        ' lblVWAPTitle
        ' 
        lblVWAPTitle.AutoSize = True
        lblVWAPTitle.Font = New Font("Calibri", 14F)
        lblVWAPTitle.ForeColor = SystemColors.ControlLight
        lblVWAPTitle.Location = New Point(5, 52)
        lblVWAPTitle.Name = "lblVWAPTitle"
        lblVWAPTitle.Size = New Size(85, 35)
        lblVWAPTitle.TabIndex = 127
        lblVWAPTitle.Text = "VWAP"
        IndicatorsToolTip.SetToolTip(lblVWAPTitle, "VWAP + EMA confirmation = 1 score" & vbCrLf & "Only Buy/Sell bias = 1 score, no tiers.")
        ' 
        ' lblVWAP
        ' 
        lblVWAP.AutoSize = True
        lblVWAP.Font = New Font("Calibri", 14F)
        lblVWAP.ForeColor = SystemColors.ControlLight
        lblVWAP.Location = New Point(110, 52)
        lblVWAP.Name = "lblVWAP"
        lblVWAP.Size = New Size(99, 35)
        lblVWAP.TabIndex = 126
        lblVWAP.Text = "Neutral"
        ' 
        ' lblScore
        ' 
        lblScore.AutoSize = True
        lblScore.Font = New Font("Calibri", 20F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblScore.ForeColor = Color.DodgerBlue
        lblScore.Location = New Point(110, 5)
        lblScore.Name = "lblScore"
        lblScore.Size = New Size(0, 49)
        lblScore.TabIndex = 129
        ' 
        ' lblATRTitle
        ' 
        lblATRTitle.AutoSize = True
        lblATRTitle.Font = New Font("Calibri", 14F)
        lblATRTitle.ForeColor = SystemColors.ControlLight
        lblATRTitle.Location = New Point(5, 262)
        lblATRTitle.Name = "lblATRTitle"
        lblATRTitle.Size = New Size(58, 35)
        lblATRTitle.TabIndex = 130
        lblATRTitle.Text = "ATR"
        IndicatorsToolTip.SetToolTip(lblATRTitle, "ATR value based on AutoTradeSettings ATR form's" & vbCrLf & "txtATR Length setting.")
        ' 
        ' lblATR
        ' 
        lblATR.AutoSize = True
        lblATR.Font = New Font("Calibri", 14F)
        lblATR.ForeColor = SystemColors.ControlLight
        lblATR.Location = New Point(110, 262)
        lblATR.Name = "lblATR"
        lblATR.Size = New Size(99, 35)
        lblATR.TabIndex = 131
        lblATR.Text = "Neutral"
        ' 
        ' btnATR
        ' 
        btnATR.BackColor = Color.ForestGreen
        btnATR.Cursor = Cursors.Hand
        btnATR.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnATR.Location = New Point(409, 708)
        btnATR.Name = "btnATR"
        btnATR.Size = New Size(88, 136)
        btnATR.TabIndex = 153
        btnATR.Text = "Paste ATR"
        IndicatorsToolTip.SetToolTip(btnATR, "Multiplies ATR reading on this form with AutoTradeSettings" & vbCrLf & "Take Profit/Stop Loss ATR multipliers to the main form's" & vbCrLf & "Take Profit/Trigger price distance settings.")
        btnATR.UseVisualStyleBackColor = False
        ' 
        ' btnAutoTrade
        ' 
        btnAutoTrade.BackColor = Color.Red
        btnAutoTrade.Cursor = Cursors.Hand
        btnAutoTrade.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnAutoTrade.Location = New Point(13, 708)
        btnAutoTrade.Name = "btnAutoTrade"
        btnAutoTrade.Size = New Size(217, 136)
        btnAutoTrade.TabIndex = 156
        btnAutoTrade.Text = "AUTO: OFF"
        btnAutoTrade.UseVisualStyleBackColor = False
        ' 
        ' GroupBox1
        ' 
        GroupBox1.Controls.Add(txtTestTime)
        GroupBox1.Controls.Add(Label2)
        GroupBox1.Controls.Add(Label1)
        GroupBox1.Controls.Add(btnBacktest)
        GroupBox1.Font = New Font("Calibri", 14F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        GroupBox1.ForeColor = SystemColors.ButtonFace
        GroupBox1.Location = New Point(12, 571)
        GroupBox1.Name = "GroupBox1"
        GroupBox1.Size = New Size(485, 131)
        GroupBox1.TabIndex = 157
        GroupBox1.TabStop = False
        GroupBox1.Text = "Backtesting Section"
        ' 
        ' txtTestTime
        ' 
        txtTestTime.BackColor = Color.Black
        txtTestTime.BorderStyle = BorderStyle.FixedSingle
        txtTestTime.Font = New Font("Calibri", 14F)
        txtTestTime.ForeColor = Color.White
        txtTestTime.Location = New Point(85, 76)
        txtTestTime.Name = "txtTestTime"
        txtTestTime.Size = New Size(64, 42)
        txtTestTime.TabIndex = 149
        txtTestTime.Text = "1440"
        txtTestTime.TextAlign = HorizontalAlignment.Center
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Font = New Font("Calibri", 14F)
        Label2.ForeColor = SystemColors.ControlLight
        Label2.Location = New Point(149, 78)
        Label2.Name = "Label2"
        Label2.Size = New Size(69, 35)
        Label2.TabIndex = 150
        Label2.Text = "mins"
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Font = New Font("Calibri", 14F)
        Label1.ForeColor = SystemColors.ControlLight
        Label1.Location = New Point(32, 38)
        Label1.Name = "Label1"
        Label1.Size = New Size(237, 35)
        Label1.TabIndex = 148
        Label1.Text = "Time Range to Test:"
        ' 
        ' btnBacktest
        ' 
        btnBacktest.BackColor = Color.DodgerBlue
        btnBacktest.Cursor = Cursors.Hand
        btnBacktest.Font = New Font("Calibri", 10F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnBacktest.Location = New Point(319, 24)
        btnBacktest.Name = "btnBacktest"
        btnBacktest.Size = New Size(160, 99)
        btnBacktest.TabIndex = 147
        btnBacktest.Text = "TEST!"
        btnBacktest.UseVisualStyleBackColor = False
        ' 
        ' btnAutoTradeSettings
        ' 
        btnAutoTradeSettings.BackColor = Color.Chocolate
        btnAutoTradeSettings.Cursor = Cursors.Hand
        btnAutoTradeSettings.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnAutoTradeSettings.Location = New Point(236, 708)
        btnAutoTradeSettings.Name = "btnAutoTradeSettings"
        btnAutoTradeSettings.Size = New Size(167, 136)
        btnAutoTradeSettings.TabIndex = 158
        btnAutoTradeSettings.Text = "Auto Trade Settings"
        btnAutoTradeSettings.UseVisualStyleBackColor = False
        ' 
        ' FrmIndicators
        ' 
        AutoScaleDimensions = New SizeF(10F, 25F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = SystemColors.ActiveCaptionText
        ClientSize = New Size(512, 856)
        Controls.Add(btnAutoTradeSettings)
        Controls.Add(GroupBox1)
        Controls.Add(btnAutoTrade)
        Controls.Add(btnATR)
        Controls.Add(lblATR)
        Controls.Add(lblATRTitle)
        Controls.Add(lblScore)
        Controls.Add(lblVWAPTitle)
        Controls.Add(lblVWAP)
        Controls.Add(lblEMATitle)
        Controls.Add(lblEMA)
        Controls.Add(redHeartBeat)
        Controls.Add(txtIndLogs)
        Controls.Add(lblStochTitle)
        Controls.Add(lblRSITitle)
        Controls.Add(lblMACDTitle)
        Controls.Add(lblDMITitle)
        Controls.Add(lblStoch)
        Controls.Add(lblRSI)
        Controls.Add(lblMACD)
        Controls.Add(lblDMI)
        Name = "FrmIndicators"
        Opacity = 0.75R
        Text = "Indicators"
        TopMost = True
        GroupBox1.ResumeLayout(False)
        GroupBox1.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents lblDMI As Label
    Friend WithEvents lblMACD As Label
    Friend WithEvents lblRSI As Label
    Friend WithEvents lblStoch As Label
    Friend WithEvents lblDMITitle As Label
    Friend WithEvents lblMACDTitle As Label
    Friend WithEvents lblRSITitle As Label
    Friend WithEvents lblStochTitle As Label
    Friend WithEvents txtIndLogs As RichTextBox
    Friend WithEvents redHeartBeat As RadioButton
    Friend WithEvents lblEMATitle As Label
    Friend WithEvents lblEMA As Label
    Friend WithEvents lblVWAPTitle As Label
    Friend WithEvents lblVWAP As Label
    Friend WithEvents lblScore As Label
    Friend WithEvents lblATRTitle As Label
    Friend WithEvents lblATR As Label
    Friend WithEvents btnATR As Button
    Friend WithEvents btnAutoTrade As Button
    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents txtTestTime As TextBox
    Friend WithEvents Label2 As Label
    Friend WithEvents Label1 As Label
    Friend WithEvents btnBacktest As Button
    Friend WithEvents btnAutoTradeSettings As Button
    Friend WithEvents IndicatorsToolTip As ToolTip
End Class
