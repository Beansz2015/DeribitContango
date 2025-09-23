<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class AutoTradeSettings
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
        txtCircuitBreaker = New TextBox()
        lblBacktestTitle = New Label()
        GroupBox1 = New GroupBox()
        txtEndTime = New TextBox()
        txtStartTime = New TextBox()
        Label7 = New Label()
        Label8 = New Label()
        GroupBox2 = New GroupBox()
        Label4 = New Label()
        txtSL = New TextBox()
        lblTestATRSL = New Label()
        Label3 = New Label()
        txtTP = New TextBox()
        lblTestATRTP = New Label()
        txtATRLimit = New TextBox()
        lblATRLimit = New Label()
        txtATR = New TextBox()
        lblTestATR = New Label()
        GroupBox3 = New GroupBox()
        Label6 = New Label()
        txtSScore = New TextBox()
        lblTestSScore = New Label()
        Label5 = New Label()
        txtLScore = New TextBox()
        lblTestLScore = New Label()
        Label1 = New Label()
        Label2 = New Label()
        txtCooloff = New TextBox()
        Label9 = New Label()
        txtTrendStrength = New TextBox()
        Label10 = New Label()
        Label11 = New Label()
        AutoTradingToolTip = New ToolTip(components)
        GroupBox1.SuspendLayout()
        GroupBox2.SuspendLayout()
        GroupBox3.SuspendLayout()
        SuspendLayout()
        ' 
        ' txtCircuitBreaker
        ' 
        txtCircuitBreaker.BackColor = Color.Black
        txtCircuitBreaker.BorderStyle = BorderStyle.FixedSingle
        txtCircuitBreaker.Font = New Font("Calibri", 14F)
        txtCircuitBreaker.ForeColor = Color.White
        txtCircuitBreaker.Location = New Point(350, 477)
        txtCircuitBreaker.Name = "txtCircuitBreaker"
        txtCircuitBreaker.Size = New Size(64, 42)
        txtCircuitBreaker.TabIndex = 185
        txtCircuitBreaker.Text = "-1"
        txtCircuitBreaker.TextAlign = HorizontalAlignment.Center
        ' 
        ' lblBacktestTitle
        ' 
        lblBacktestTitle.AutoSize = True
        lblBacktestTitle.Font = New Font("Calibri", 18F, FontStyle.Bold Or FontStyle.Underline, GraphicsUnit.Point, CByte(0))
        lblBacktestTitle.ForeColor = SystemColors.ControlLight
        lblBacktestTitle.Location = New Point(103, 20)
        lblBacktestTitle.Name = "lblBacktestTitle"
        lblBacktestTitle.Size = New Size(318, 44)
        lblBacktestTitle.TabIndex = 164
        lblBacktestTitle.Text = "AutoTrading Section"
        ' 
        ' GroupBox1
        ' 
        GroupBox1.Controls.Add(txtEndTime)
        GroupBox1.Controls.Add(txtStartTime)
        GroupBox1.Controls.Add(Label7)
        GroupBox1.Controls.Add(Label8)
        GroupBox1.Font = New Font("Calibri", 14F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        GroupBox1.ForeColor = SystemColors.ButtonFace
        GroupBox1.Location = New Point(18, 67)
        GroupBox1.Name = "GroupBox1"
        GroupBox1.Size = New Size(482, 96)
        GroupBox1.TabIndex = 188
        GroupBox1.TabStop = False
        GroupBox1.Text = "Exclusion Time Range"
        AutoTradingToolTip.SetToolTip(GroupBox1, "Start and end time daily where autotrading is forbidden.")
        ' 
        ' txtEndTime
        ' 
        txtEndTime.BackColor = Color.Black
        txtEndTime.BorderStyle = BorderStyle.FixedSingle
        txtEndTime.Font = New Font("Calibri", 14F)
        txtEndTime.ForeColor = Color.White
        txtEndTime.Location = New Point(396, 41)
        txtEndTime.Name = "txtEndTime"
        txtEndTime.Size = New Size(82, 42)
        txtEndTime.TabIndex = 191
        txtEndTime.Text = "22:00"
        txtEndTime.TextAlign = HorizontalAlignment.Center
        ' 
        ' txtStartTime
        ' 
        txtStartTime.BackColor = Color.Black
        txtStartTime.BorderStyle = BorderStyle.FixedSingle
        txtStartTime.Font = New Font("Calibri", 14F)
        txtStartTime.ForeColor = Color.White
        txtStartTime.Location = New Point(150, 41)
        txtStartTime.Name = "txtStartTime"
        txtStartTime.Size = New Size(82, 42)
        txtStartTime.TabIndex = 188
        txtStartTime.Text = "21:30"
        txtStartTime.TextAlign = HorizontalAlignment.Center
        ' 
        ' Label7
        ' 
        Label7.AutoSize = True
        Label7.Font = New Font("Calibri", 14F)
        Label7.ForeColor = SystemColors.ControlLight
        Label7.Location = New Point(264, 43)
        Label7.Name = "Label7"
        Label7.Size = New Size(129, 35)
        Label7.TabIndex = 189
        Label7.Text = "End Time:"
        ' 
        ' Label8
        ' 
        Label8.AutoSize = True
        Label8.Font = New Font("Calibri", 14F)
        Label8.ForeColor = SystemColors.ControlLight
        Label8.Location = New Point(11, 41)
        Label8.Name = "Label8"
        Label8.Size = New Size(139, 35)
        Label8.TabIndex = 190
        Label8.Text = "Start Time:"
        ' 
        ' GroupBox2
        ' 
        GroupBox2.Controls.Add(Label4)
        GroupBox2.Controls.Add(txtSL)
        GroupBox2.Controls.Add(lblTestATRSL)
        GroupBox2.Controls.Add(Label3)
        GroupBox2.Controls.Add(txtTP)
        GroupBox2.Controls.Add(lblTestATRTP)
        GroupBox2.Controls.Add(txtATRLimit)
        GroupBox2.Controls.Add(lblATRLimit)
        GroupBox2.Controls.Add(txtATR)
        GroupBox2.Controls.Add(lblTestATR)
        GroupBox2.Font = New Font("Calibri", 14F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        GroupBox2.ForeColor = SystemColors.ButtonFace
        GroupBox2.Location = New Point(18, 164)
        GroupBox2.Name = "GroupBox2"
        GroupBox2.Size = New Size(482, 167)
        GroupBox2.TabIndex = 192
        GroupBox2.TabStop = False
        GroupBox2.Text = "ATR Settings"
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Font = New Font("Calibri", 10F)
        Label4.ForeColor = SystemColors.ControlLight
        Label4.Location = New Point(396, 129)
        Label4.Name = "Label4"
        Label4.Size = New Size(55, 24)
        Label4.TabIndex = 188
        Label4.Text = "x ATR"
        ' 
        ' txtSL
        ' 
        txtSL.BackColor = Color.Black
        txtSL.BorderStyle = BorderStyle.FixedSingle
        txtSL.Font = New Font("Calibri", 14F)
        txtSL.ForeColor = Color.White
        txtSL.Location = New Point(396, 84)
        txtSL.Name = "txtSL"
        txtSL.Size = New Size(64, 42)
        txtSL.TabIndex = 187
        txtSL.Text = "1.5"
        txtSL.TextAlign = HorizontalAlignment.Center
        ' 
        ' lblTestATRSL
        ' 
        lblTestATRSL.AutoSize = True
        lblTestATRSL.Font = New Font("Calibri", 14F)
        lblTestATRSL.ForeColor = SystemColors.ControlLight
        lblTestATRSL.Location = New Point(256, 84)
        lblTestATRSL.Name = "lblTestATRSL"
        lblTestATRSL.Size = New Size(130, 35)
        lblTestATRSL.TabIndex = 186
        lblTestATRSL.Text = "Stop Loss:"
        AutoTradingToolTip.SetToolTip(lblTestATRSL, "Stop Loss Trigger price distance will be " & vbCrLf & "determined by ATR multiplier here." & vbCrLf)
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Font = New Font("Calibri", 10F)
        Label3.ForeColor = SystemColors.ControlLight
        Label3.Location = New Point(151, 129)
        Label3.Name = "Label3"
        Label3.Size = New Size(55, 24)
        Label3.TabIndex = 185
        Label3.Text = "x ATR"
        ' 
        ' txtTP
        ' 
        txtTP.BackColor = Color.Black
        txtTP.BorderStyle = BorderStyle.FixedSingle
        txtTP.Font = New Font("Calibri", 14F)
        txtTP.ForeColor = Color.White
        txtTP.Location = New Point(151, 84)
        txtTP.Name = "txtTP"
        txtTP.Size = New Size(64, 42)
        txtTP.TabIndex = 184
        txtTP.Text = "2.0"
        txtTP.TextAlign = HorizontalAlignment.Center
        ' 
        ' lblTestATRTP
        ' 
        lblTestATRTP.AutoSize = True
        lblTestATRTP.Font = New Font("Calibri", 14F)
        lblTestATRTP.ForeColor = SystemColors.ControlLight
        lblTestATRTP.Location = New Point(11, 86)
        lblTestATRTP.Name = "lblTestATRTP"
        lblTestATRTP.Size = New Size(143, 35)
        lblTestATRTP.TabIndex = 183
        lblTestATRTP.Text = "Take Profit:"
        AutoTradingToolTip.SetToolTip(lblTestATRTP, "Take Profit price distance will be determined" & vbCrLf & "by ATR multiplier here.")
        ' 
        ' txtATRLimit
        ' 
        txtATRLimit.BackColor = Color.Black
        txtATRLimit.BorderStyle = BorderStyle.FixedSingle
        txtATRLimit.Font = New Font("Calibri", 14F)
        txtATRLimit.ForeColor = Color.White
        txtATRLimit.Location = New Point(396, 36)
        txtATRLimit.Name = "txtATRLimit"
        txtATRLimit.Size = New Size(64, 42)
        txtATRLimit.TabIndex = 181
        txtATRLimit.Text = "70"
        txtATRLimit.TextAlign = HorizontalAlignment.Center
        ' 
        ' lblATRLimit
        ' 
        lblATRLimit.AutoSize = True
        lblATRLimit.Font = New Font("Calibri", 14F)
        lblATRLimit.ForeColor = SystemColors.ControlLight
        lblATRLimit.Location = New Point(256, 36)
        lblATRLimit.Name = "lblATRLimit"
        lblATRLimit.Size = New Size(147, 35)
        lblATRLimit.TabIndex = 182
        lblATRLimit.Text = "Avg. Range:"
        AutoTradingToolTip.SetToolTip(lblATRLimit, "An auto trade is allowed only if ATR is above" & vbCrLf & "this limit.")
        ' 
        ' txtATR
        ' 
        txtATR.BackColor = Color.Black
        txtATR.BorderStyle = BorderStyle.FixedSingle
        txtATR.Font = New Font("Calibri", 14F)
        txtATR.ForeColor = Color.White
        txtATR.Location = New Point(150, 36)
        txtATR.Name = "txtATR"
        txtATR.Size = New Size(64, 42)
        txtATR.TabIndex = 172
        txtATR.Text = "14"
        txtATR.TextAlign = HorizontalAlignment.Center
        ' 
        ' lblTestATR
        ' 
        lblTestATR.AutoSize = True
        lblTestATR.Font = New Font("Calibri", 14F)
        lblTestATR.ForeColor = SystemColors.ControlLight
        lblTestATR.Location = New Point(11, 36)
        lblTestATR.Name = "lblTestATR"
        lblTestATR.Size = New Size(101, 35)
        lblTestATR.TabIndex = 171
        lblTestATR.Text = "Length:"
        AutoTradingToolTip.SetToolTip(lblTestATR, "No. of candles ATR is based on for other" & vbCrLf & "ATR calculations.")
        ' 
        ' GroupBox3
        ' 
        GroupBox3.Controls.Add(Label6)
        GroupBox3.Controls.Add(txtSScore)
        GroupBox3.Controls.Add(lblTestSScore)
        GroupBox3.Controls.Add(Label5)
        GroupBox3.Controls.Add(txtLScore)
        GroupBox3.Controls.Add(lblTestLScore)
        GroupBox3.Font = New Font("Calibri", 14F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        GroupBox3.ForeColor = SystemColors.ButtonFace
        GroupBox3.Location = New Point(18, 332)
        GroupBox3.Name = "GroupBox3"
        GroupBox3.Size = New Size(482, 111)
        GroupBox3.TabIndex = 192
        GroupBox3.TabStop = False
        GroupBox3.Text = "Signal Score"
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Font = New Font("Calibri", 10F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        Label6.ForeColor = SystemColors.ControlLight
        Label6.Location = New Point(396, 26)
        Label6.Name = "Label6"
        Label6.Size = New Size(67, 24)
        Label6.TabIndex = 183
        Label6.Text = "-1 : -21"
        ' 
        ' txtSScore
        ' 
        txtSScore.BackColor = Color.Black
        txtSScore.BorderStyle = BorderStyle.FixedSingle
        txtSScore.Font = New Font("Calibri", 14F)
        txtSScore.ForeColor = Color.White
        txtSScore.Location = New Point(396, 51)
        txtSScore.Name = "txtSScore"
        txtSScore.Size = New Size(64, 42)
        txtSScore.TabIndex = 182
        txtSScore.Text = "-12"
        txtSScore.TextAlign = HorizontalAlignment.Center
        ' 
        ' lblTestSScore
        ' 
        lblTestSScore.AutoSize = True
        lblTestSScore.Font = New Font("Calibri", 14F)
        lblTestSScore.ForeColor = SystemColors.ControlLight
        lblTestSScore.Location = New Point(256, 51)
        lblTestSScore.Name = "lblTestSScore"
        lblTestSScore.Size = New Size(142, 35)
        lblTestSScore.TabIndex = 181
        lblTestSScore.Text = "Short Trgt.:"
        AutoTradingToolTip.SetToolTip(lblTestSScore, "Signal score must be <= this limit before auto trade" & vbCrLf & "Short orders are allowed. Only -21 -> -1 range." & vbCrLf)
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Font = New Font("Calibri", 10F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        Label5.ForeColor = SystemColors.ControlLight
        Label5.Location = New Point(155, 26)
        Label5.Name = "Label5"
        Label5.Size = New Size(55, 24)
        Label5.TabIndex = 180
        Label5.Text = "1 : 21"
        ' 
        ' txtLScore
        ' 
        txtLScore.BackColor = Color.Black
        txtLScore.BorderStyle = BorderStyle.FixedSingle
        txtLScore.Font = New Font("Calibri", 14F)
        txtLScore.ForeColor = Color.White
        txtLScore.Location = New Point(151, 51)
        txtLScore.Name = "txtLScore"
        txtLScore.Size = New Size(64, 42)
        txtLScore.TabIndex = 179
        txtLScore.Text = "12"
        txtLScore.TextAlign = HorizontalAlignment.Center
        ' 
        ' lblTestLScore
        ' 
        lblTestLScore.AutoSize = True
        lblTestLScore.Font = New Font("Calibri", 14F)
        lblTestLScore.ForeColor = SystemColors.ControlLight
        lblTestLScore.Location = New Point(11, 51)
        lblTestLScore.Name = "lblTestLScore"
        lblTestLScore.Size = New Size(135, 35)
        lblTestLScore.TabIndex = 178
        lblTestLScore.Text = "Long Trgt.:"
        AutoTradingToolTip.SetToolTip(lblTestLScore, "Signal score must be => this limit before auto trade" & vbCrLf & "Long orders are allowed. Only 1 -> 21 range.")
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Font = New Font("Calibri", 14F)
        Label1.ForeColor = SystemColors.ControlLight
        Label1.Location = New Point(11, 477)
        Label1.Name = "Label1"
        Label1.Size = New Size(258, 35)
        Label1.TabIndex = 193
        Label1.Text = "Max Loss Limit (USD):"
        AutoTradingToolTip.SetToolTip(Label1, "Max loss in USD before auto trader is turned off.")
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Font = New Font("Calibri", 14F)
        Label2.ForeColor = SystemColors.ControlLight
        Label2.Location = New Point(11, 527)
        Label2.Name = "Label2"
        Label2.Size = New Size(253, 35)
        Label2.TabIndex = 197
        Label2.Text = "Trade Cooloff Period:"
        AutoTradingToolTip.SetToolTip(Label2, "Auto trade is temporarily disabled for this period of" & vbCrLf & "time after a position is exited or an attempt is stopped." & vbCrLf & "Auto trade is disabled by default during an ongoing " & vbCrLf & "attempt.")
        ' 
        ' txtCooloff
        ' 
        txtCooloff.BackColor = Color.Black
        txtCooloff.BorderStyle = BorderStyle.FixedSingle
        txtCooloff.Font = New Font("Calibri", 14F)
        txtCooloff.ForeColor = Color.White
        txtCooloff.Location = New Point(350, 527)
        txtCooloff.Name = "txtCooloff"
        txtCooloff.Size = New Size(64, 42)
        txtCooloff.TabIndex = 198
        txtCooloff.Text = "5"
        txtCooloff.TextAlign = HorizontalAlignment.Center
        ' 
        ' Label9
        ' 
        Label9.AutoSize = True
        Label9.Font = New Font("Calibri", 10F)
        Label9.ForeColor = SystemColors.ControlLight
        Label9.Location = New Point(420, 535)
        Label9.Name = "Label9"
        Label9.Size = New Size(50, 24)
        Label9.TabIndex = 199
        Label9.Text = "mins"
        ' 
        ' txtTrendStrength
        ' 
        txtTrendStrength.BackColor = Color.Black
        txtTrendStrength.BorderStyle = BorderStyle.FixedSingle
        txtTrendStrength.Font = New Font("Calibri", 14F)
        txtTrendStrength.ForeColor = Color.White
        txtTrendStrength.Location = New Point(350, 577)
        txtTrendStrength.Name = "txtTrendStrength"
        txtTrendStrength.Size = New Size(64, 42)
        txtTrendStrength.TabIndex = 201
        txtTrendStrength.Text = "0.5"
        txtTrendStrength.TextAlign = HorizontalAlignment.Center
        ' 
        ' Label10
        ' 
        Label10.AutoSize = True
        Label10.Font = New Font("Calibri", 14F)
        Label10.ForeColor = SystemColors.ControlLight
        Label10.Location = New Point(11, 577)
        Label10.Name = "Label10"
        Label10.Size = New Size(303, 35)
        Label10.TabIndex = 200
        Label10.Text = "EMA Diff. Trend Strength:"
        AutoTradingToolTip.SetToolTip(Label10, "Auto trade only allowed if diff between EMA50 and EMA200" & vbCrLf & "(EMA50 - EMA200) / EMA200 * 100 is more than the value " & vbCrLf & "set here.")
        ' 
        ' Label11
        ' 
        Label11.AutoSize = True
        Label11.Font = New Font("Calibri", 10F)
        Label11.ForeColor = SystemColors.ControlLight
        Label11.Location = New Point(420, 587)
        Label11.Name = "Label11"
        Label11.Size = New Size(89, 24)
        Label11.TabIndex = 202
        Label11.Text = "0.3% - 1%"
        ' 
        ' AutoTradeSettings
        ' 
        AutoScaleDimensions = New SizeF(10F, 25F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = SystemColors.ActiveCaptionText
        ClientSize = New Size(512, 856)
        Controls.Add(Label11)
        Controls.Add(txtTrendStrength)
        Controls.Add(Label10)
        Controls.Add(Label9)
        Controls.Add(txtCooloff)
        Controls.Add(Label2)
        Controls.Add(Label1)
        Controls.Add(GroupBox3)
        Controls.Add(GroupBox2)
        Controls.Add(GroupBox1)
        Controls.Add(txtCircuitBreaker)
        Controls.Add(lblBacktestTitle)
        Name = "AutoTradeSettings"
        StartPosition = FormStartPosition.Manual
        Text = "AutoTradeSettings"
        TopMost = True
        GroupBox1.ResumeLayout(False)
        GroupBox1.PerformLayout()
        GroupBox2.ResumeLayout(False)
        GroupBox2.PerformLayout()
        GroupBox3.ResumeLayout(False)
        GroupBox3.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub
    Friend WithEvents txtCircuitBreaker As TextBox
    Friend WithEvents lblBacktestTitle As Label
    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents txtEndTime As TextBox
    Friend WithEvents txtStartTime As TextBox
    Friend WithEvents Label7 As Label
    Friend WithEvents Label8 As Label
    Friend WithEvents GroupBox2 As GroupBox
    Friend WithEvents txtATRLimit As TextBox
    Friend WithEvents lblATRLimit As Label
    Friend WithEvents txtATR As TextBox
    Friend WithEvents lblTestATR As Label
    Friend WithEvents Label3 As Label
    Friend WithEvents txtTP As TextBox
    Friend WithEvents lblTestATRTP As Label
    Friend WithEvents Label4 As Label
    Friend WithEvents txtSL As TextBox
    Friend WithEvents lblTestATRSL As Label
    Friend WithEvents GroupBox3 As GroupBox
    Friend WithEvents Label6 As Label
    Friend WithEvents txtSScore As TextBox
    Friend WithEvents lblTestSScore As Label
    Friend WithEvents Label5 As Label
    Friend WithEvents txtLScore As TextBox
    Friend WithEvents lblTestLScore As Label
    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents txtCooloff As TextBox
    Friend WithEvents Label9 As Label
    Friend WithEvents txtTrendStrength As TextBox
    Friend WithEvents Label10 As Label
    Friend WithEvents Label11 As Label
    Friend WithEvents AutoTradingToolTip As ToolTip
End Class
