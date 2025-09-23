Public Class AutoTradeSettings

    Private ReadOnly _hostIndicators As Form  ' Reference to frmIndicators

    Public Sub New(hostIndicators As Form)
        InitializeComponent()
        _hostIndicators = hostIndicators
    End Sub

    Private Sub AutoTradeSettings_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Initial positioning
        StickToHost()

        ' Attach event handlers to follow host movement
        If _hostIndicators IsNot Nothing Then
            AddHandler _hostIndicators.LocationChanged, AddressOf HostMovedOrResized
            AddHandler _hostIndicators.SizeChanged, AddressOf HostMovedOrResized
        End If

        Me.Hide()
    End Sub
    Private Sub HostMovedOrResized(sender As Object, e As EventArgs)
        StickToHost()
    End Sub

    Private Sub StickToHost()
        If _hostIndicators Is Nothing OrElse _hostIndicators.IsDisposed Then Return

        ' Position AutoTradeSettings to the right of frmIndicators
        Me.StartPosition = FormStartPosition.Manual
        Me.Location = New Point(_hostIndicators.Right + 6, _hostIndicators.Top)
    End Sub

    ' Clean up event handlers when form closes
    Private Sub AutoTradeSettings_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        If _hostIndicators IsNot Nothing Then
            RemoveHandler _hostIndicators.LocationChanged, AddressOf HostMovedOrResized
            RemoveHandler _hostIndicators.SizeChanged, AddressOf HostMovedOrResized
        End If
    End Sub

End Class