Public Class PclLogger
    Inherits FileLogger

    ''' <inheritdoc/>
    Public Overrides Function Format(Text As String, Level As LogLevel, FilePath As String, Ex As Exception) As String
        Text = MyBase.Format(Text, Level, FilePath, Ex)
        Text = FilterUserName(FilterAccessToken(Text, "*"c), "*"c)
        Return Text
    End Function

    ''' <inheritdoc/>
    Public Overrides Sub HandleBehavior(RawMessage As String, FormattedMessage As String, Behavior As LogBehavior, Ex As Exception)
        If IsProgramEnding Then Return
        MyBase.HandleBehavior(RawMessage, FormattedMessage, Behavior, Ex)
        Dim BriefText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(False))
        BriefText = FilterUserName(FilterAccessToken(BriefText, "*"c), "*"c)
        Dim DetailText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(True))
        DetailText = FilterUserName(FilterAccessToken(DetailText, "*"c), "*"c)
        '没网 / 连不上服务器 属于网络问题，不算错误：不上报、不弹错误框，只在左下角提示
        If Behavior >= LogBehavior.Toast Then
            If IsNetworkIssue(Ex, RawMessage) Then
                HintNetworkIssue()
                Return
            End If
            '自动上报（服务端存入 User-Errors）。先落盘再发送，断网时缓存到本地，联网后自动补报
            ReportErrorToServer(DetailText)
        End If
        Select Case Behavior
            Case LogBehavior.None
                '啥也不干
            Case LogBehavior.ToastIfDebug
                If BuildType = BuildTypes.Debug OrElse ModeDebug Then Hint("[调试模式] " & BriefText, HintType.Blue, False)
            Case LogBehavior.Toast
                Hint(BriefText, HintType.Red, False)
            Case LogBehavior.Alert
                MyMsgBox(DetailText, "错误", IsWarn:=True)
            Case LogBehavior.AlertThenFeedback
                '错误已自动上报，这里只告知用户，不需要手动点“反馈”提交
                If PageSetupSystem.IsLauncherNewest Then
                    MyMsgBox(DetailText & vbCrLf & vbCrLf & "此问题已自动上报到服务端，不需要你手动反馈。", "错误", IsWarn:=True)
                Else
                    MyMsgBox(DetailText & vbCrLf & vbCrLf & "此问题已自动上报到服务端。" & vbCrLf & "将 TYW Launcher 更新至最新版或许可以解决这个问题……", "错误", IsWarn:=True)
                End If
            Case LogBehavior.AlertThenCrash
                Static FirstTrigger As Boolean = True
                If FirstTrigger Then
                    '首次触发
                    FirstTrigger = False
                    '错误已自动上报，这里只告知用户
                    If PageSetupSystem.IsLauncherNewest Then
                        MsgBox(DetailText & vbCrLf & vbCrLf & "此问题已自动上报到服务端，不需要你手动反馈。程序即将关闭。", MsgBoxStyle.Critical, "错误")
                    Else
                        MsgBox(DetailText & vbCrLf & vbCrLf & "此问题已自动上报到服务端。" & vbCrLf & "将 TYW Launcher 更新至最新版或许可以解决这个问题……", MsgBoxStyle.Critical, "错误")
                    End If
                Else
                    '多次触发，直接使程序崩溃（这通常代表着在其他线程循环触发严重异常）
                    Thread.Sleep(2000)
                End If
                FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Select
        '遥测（已停用）
        If Behavior >= LogBehavior.Toast Then Telemetry("错误日志", "Exception", DetailText)
    End Sub

End Class
