Public Class PageSetupSystem


    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '内测版体验开关（XAML 走预生成的 BAML，改 XAML 不生效，所以这里在运行期创建）
        BuildBetaCheck()

        '识别码入口：从“关于 - 赞助者”搬到“设置 - 其他 - 启动器”，所有版本都常驻显示
        '土豆码本身已无意义（全部功能已解锁），隐藏按钮只留识别码
        PanDonate.Visibility = Visibility.Visible
        BtnSystemUnlock.Visibility = Visibility.Collapsed
        '两行按钮在 XAML 里都排在 Grid.Row=9 会重叠，这里把识别码这一行挪到下一行
        Grid.SetRow(PanDonate, If(BetaRowAdded, 11, 10))

        '内测版限制：匿名上报强制开启且不可取消；不提供自动更新与检查更新
        '★ 必须放在 Reload() 之后：Reload 会把设置里的真实值刷回控件，
        '  放前面的话“自动更新”会先显示为勾选状态，再被覆盖前就已经画出来了

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
        '内测版：把自动更新显示成关闭（不写回设置，退出内测版后原来的勾选状态还在）
        If IsBetaChannel Then ApplyBetaRestrictions()
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化启动器页设置")
            Hint("已初始化启动器页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化启动器页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, "关闭", (v / 10 + 0.1) & "x")
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

    '识别码/土豆码替代入口
    Private Sub BtnSystemIdentify_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemIdentify.Click
        PageOtherAbout.CopyIdentify()
    End Sub
    Private Sub BtnSystemUnlock_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemUnlock.Click
        InputPotatoCode(False)
    End Sub

    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint("部分调试信息将在刷新或启动器重启后切换显示！",, False)
    End Sub

    '快照版测试功能：切换通道时进行二次确认，确认后立即检查对应通道的版本并更新重启
    Private Sub CheckSystemSnapshotTest_Change(sender As Object, user As Boolean) Handles CheckSystemSnapshotTest.Change
        If AniControlEnabled <> 0 Then Return
        If CheckSystemSnapshotTest.Checked Then
            '开启：切换到快照通道
            If MyMsgBox("开启快照版测试功能后，启动器将切换到服务端的快照版更新通道，并立即检查一次快照版更新，有新版本时会自动下载并重启。" & vbCrLf &
                        "快照版包含尚未正式发布的新功能，可能不稳定，甚至无法正常启动。" & vbCrLf & vbCrLf &
                        "确定要开启吗？", "快照版测试功能", "确定开启", "取消", IsWarn:=True) = 2 Then
                '取消：恢复为关闭
                AniControlEnabled += 1
                CheckSystemSnapshotTest.Checked = False
                AniControlEnabled -= 1
                Settings.Set("SystemSnapshotTest", False)
                Return
            End If
            Settings.Set("SystemSnapshotTest", True)
            CheckChannelUpdateAndRestart()
        Else
            '关闭：切回正式通道
            If MyMsgBox("关闭快照版测试功能后，启动器将切回正式版通道，并下载正式版程序替换后重启。" & vbCrLf &
                        "（若正式版比当前快照版旧，也会换回正式版程序）" & vbCrLf & vbCrLf &
                        "确定要关闭吗？", "快照版测试功能", "确定关闭", "取消", IsWarn:=True) = 2 Then
                '取消：恢复为开启
                AniControlEnabled += 1
                CheckSystemSnapshotTest.Checked = True
                AniControlEnabled -= 1
                Settings.Set("SystemSnapshotTest", True)
                Return
            End If
            Settings.Set("SystemSnapshotTest", False)
            CheckChannelUpdateAndRestart()
        End If
    End Sub

    '自动更新
    Private Sub ComboSystemActivity_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemActivity.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemActivity.SelectedIndex <> 2 Then Return
        If MyMsgBox("若选择此项，即使在将来出现严重问题时，你也无法获取相关通知。" & vbCrLf &
                    "例如，如果发现某个版本游戏存在严重 Bug，你可能就会因为无法得到通知而导致无法预知的后果。" & vbCrLf & vbCrLf &
                    "一般选择 仅在有重要通知时显示公告 就可以让你尽量不受打扰了。" & vbCrLf &
                    "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            If e.RemovedItems.Count > 0 Then ComboSystemActivity.SelectedItem = e.RemovedItems(0)
        End If
    End Sub
    Private Sub ComboSystemUpdate_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemUpdate.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemUpdate.SelectedIndex <> 3 Then Return
        If MyMsgBox("若选择此项，即使在启动器将来出现严重问题时，你也无法获取更新并获得修复。" & vbCrLf &
                    "例如，如果官方修改了登录方式，从而导致现有启动器无法登录，你可能就会因为无法更新而无法开始游戏。" & vbCrLf & vbCrLf &
                    "一般选择 仅在有重大漏洞更新时显示提示 就可以让你尽量不受打扰了。" & vbCrLf &
                    "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            If e.RemovedItems.Count > 0 Then ComboSystemUpdate.SelectedItem = e.RemovedItems(0)
        End If
    End Sub
    Private Sub BtnSystemUpdate_Click(sender As Object, e As EventArgs) Handles BtnSystemUpdate.Click
        UpdateCheckByButton()
    End Sub
    ''' <summary>
    ''' 启动器是否已经是最新版？
    ''' 若返回 Nothing，则代表无更新缓存文件或出错。
    ''' </summary>
    Public Shared Function IsLauncherNewest() As Boolean?
        Try
            '确认服务器公告是否正常
            Dim ServerContent As String = If(FileUtils.TryReadAsString(PathTemp & "Cache\Notice.cfg"), "")
            If ServerContent.Split("|").Count < 3 Then Return Nothing
            '确认是否为最新
            Return GetChannelCode(ServerContent.Split("|"), If(IsBetaChannel, "Beta", If(IsSnapshotChannel, "Snapshot", "Release"))) <= VersionCode
        Catch ex As Exception
            Logger.Error(ex, "确认启动器更新失败")
            Return Nothing
        End Try
    End Function

#Region "内测版体验"

    ''' <summary>内测版体验复选框（运行期创建，见 BuildBetaCheck）。</summary>
    Private BetaCheck As MyCheckBox
    ''' <summary>是否已经插入过内测版行（避免重复加载时重复插入）。</summary>
    Private BetaRowAdded As Boolean = False

    ''' <summary>
    ''' 在“启动器”卡片里插入「内测版体验」复选框（位置在「出错匿名上报」上面）。
    ''' XAML 是预生成 BAML，改 XAML 不会生效，所以这里在运行期插行。
    ''' </summary>
    Private Sub BuildBetaCheck()
        If BetaRowAdded Then Return
        If Not ServerEnabled Then Return 'Server not configured: the beta feature is unavailable
        Dim Host As Grid = TryCast(CheckSystemAutoUpdate.Parent, Grid)
        If Host Is Nothing Then Return
        BetaRowAdded = True
        Try
            '在第 8 行（出错匿名上报那行）前插一行，后面的控件整体下移
            Host.RowDefinitions.Insert(8, New RowDefinition With {.Height = GridLength.Auto})
            For Each Child As UIElement In Host.Children
                Dim Row = Grid.GetRow(Child)
                If Row >= 8 Then Grid.SetRow(Child, Row + 1)
            Next
            BetaCheck = New MyCheckBox With {
                .Text = "内测版体验",
                .Height = 22,
                .Margin = New Thickness(0, 5, 0, 4),
                .Checked = (BuildType = BuildTypes.Beta OrElse Settings.Get(Of Boolean)("BetaEnabled"))}
            AddHandler BetaCheck.Change, AddressOf BetaCheck_Change
            Host.Children.Add(BetaCheck)
            Grid.SetRow(BetaCheck, 8)
            Grid.SetColumn(BetaCheck, 0)
            Grid.SetColumnSpan(BetaCheck, 2)
            BetaCheck.ToolTip = "勾选后需要输入服务端按设备码发放的内测码，校验通过会下载内测版程序并重启。" & vbCrLf &
                                "内测版没有自动更新与检查更新，并且必须联网并连接服务端才能使用。"
        Catch ex As Exception
            Logger.Warn(ex, "创建内测版体验复选框失败")
        End Try
    End Sub

    ''' <summary>内测版的界面限制：匿名上报强制开启且不能取消，自动更新与检查更新不可用。</summary>
    Private Sub ApplyBetaRestrictions()
        Try
            Settings.Set("SystemErrorReport", True)
            AniControlEnabled += 1
            CheckSystemErrorReport.Checked = True
            CheckSystemAutoUpdate.Checked = False
            AniControlEnabled -= 1
            CheckSystemErrorReport.IsEnabled = False
            CheckSystemErrorReport.ToolTip = "内测版必须保持匿名的错误上报开启，无法取消。"
            CheckSystemAutoUpdate.IsEnabled = False
            CheckSystemAutoUpdate.ToolTip = "内测版不提供自动更新，新版本由服务端人工发布。"
            CheckSystemSnapshotTest.IsEnabled = False
            CheckSystemSnapshotTest.ToolTip = "内测版已经是独立通道，不参与快照版测试。"
            ComboSystemUpdate.IsEnabled = False
            BtnSystemUpdate.IsEnabled = False
            BtnSystemUpdate.ToolTip = "内测版不提供检查更新。"
            Logger.Info("内测版限制已应用：匿名上报强制开启，自动更新显示为关闭且不可修改，检查更新不可用")
        Catch ex As Exception
            Logger.Warn(ex, "应用内测版界面限制失败")
        End Try
    End Sub

    ''' <summary>把复选框恢复成指定状态（不触发业务逻辑）。</summary>
    Private Sub RevertBetaCheck(Value As Boolean)
        If BetaCheck Is Nothing Then Return
        AniControlEnabled += 1
        BetaCheck.Checked = Value
        AniControlEnabled -= 1
    End Sub

    ''' <summary>勾选/取消「内测版体验」。</summary>
    Private Sub BetaCheck_Change(sender As Object, user As Boolean)
        If AniControlEnabled <> 0 Then Return
        If BetaCheck Is Nothing Then Return
        If BetaCheck.Checked Then
            EnterBeta()
        Else
            LeaveBeta()
        End If
    End Sub

    ''' <summary>勾选：输入内测码 → 联网校验 → 下载内测版并重启。</summary>
    Private Sub EnterBeta()
        '记录当前的通道状态：下载被取消或失败时要原样恢复
        Dim PrevEnabled As Boolean = Settings.Get(Of Boolean)("BetaEnabled")
        Dim PrevCode As String = Settings.Get(Of String)("BetaCode")
        Dim Code As String = MyMsgBoxInput("内测版体验",
            "内测码由服务端管理员按设备码发放。" & vbCrLf &
            "你的设备码：" & DeviceCode & vbCrLf & vbCrLf &
            "校验通过后会下载内测版程序并重启。内测版没有自动更新，并且必须联网并连接服务端才能使用。",
            Settings.Get(Of String)("BetaCode"), HintText:="内测码", Button1:="校验并进入")
        If Code Is Nothing OrElse Code.Trim = "" Then
            RevertBetaCheck(PrevEnabled)
            Return
        End If
        Code = Code.Trim()
        Hint("正在校验内测码……")
        Dim Verified As Boolean = False
        Dim ErrMsg As String = ""
        Dim Worker As New System.Threading.Thread(
            Sub()
                Try
                    Dim Result = VerifyBetaCode(Code)
                    If Result.Ok Then
                        Settings.Set("BetaCode", Code)
                        Settings.Set("BetaEnabled", True)
                        Verified = True
                    Else
                        ErrMsg = If(Result.Message = "", "内测码无效或已被停用", Result.Message)
                    End If
                Catch ex As Exception
                    ErrMsg = If(IsNetworkIssue(ex), "连不上服务器，请检查网络后重试。", ex.Message)
                End Try
            End Sub)
        Worker.IsBackground = True
        Worker.Start()
        Worker.Join()
        If Not Verified Then
            RevertBetaCheck(PrevEnabled)
            MyMsgBox("内测码校验失败：" & ErrMsg, "内测版体验", IsWarn:=True)
            Return
        End If
        Hint("内测码校验通过，正在下载内测版……", HintType.Green)
        RunInNewThread(
            Sub()
                '下载被取消或失败时 SwitchChannel 返回 False：恢复原来的状态，复选框打回原样，进程继续运行
                If SwitchChannel("Beta") Then Return
                RunInUi(
                    Sub()
                        Settings.Set("BetaEnabled", PrevEnabled)
                        Settings.Set("BetaCode", PrevCode)
                        RevertBetaCheck(PrevEnabled)
                        Hint("内测版下载未完成，已恢复原状（内测码校验通过，可稍后重新勾选）", HintType.Blue)
                    End Sub)
            End Sub, "切换到内测版")
    End Sub

    ''' <summary>取消：切回正式版（开了快照版测试则切回快照版）。</summary>
    Private Sub LeaveBeta()
        Dim Target As String = If(Settings.Get(Of Boolean)("SystemSnapshotTest"), "Snapshot", "Release")
        If MyMsgBox("取消「内测版体验」后，启动器会切回" & ChannelDisplay(Target) & "，下载对应程序替换后重启。" & vbCrLf &
                    "本机保存的内测码会被移除，需要重新输入才能再次进入内测版。" & vbCrLf & vbCrLf &
                    "确定要取消吗？", "内测版体验", "确定取消", "继续内测", IsWarn:=True) = 2 Then
            RevertBetaCheck(True)
            Return
        End If
        Dim PrevCode As String = Settings.Get(Of String)("BetaCode")
        ClearBetaState()
        Hint("正在切回" & ChannelDisplay(Target) & "……")
        RunInNewThread(
            Sub()
                '切回失败（下载取消 / 网络中断）：仍然是内测版，把内测状态恢复回去
                If SwitchChannel(Target) Then Return
                RunInUi(
                    Sub()
                        Settings.Set("BetaCode", PrevCode)
                        Settings.Set("BetaEnabled", True)
                        RevertBetaCheck(True)
                        Hint("切回" & ChannelDisplay(Target) & "未完成，仍然是内测版", HintType.Blue)
                    End Sub)
            End Sub, "切回" & ChannelDisplay(Target))
    End Sub

#End Region

#Region "导出 / 导入设置"

    Private Sub BtnSystemSettingExp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingExp.Click
        Hint("该功能尚在开发中！")
    End Sub
    Private Sub BtnSystemSettingImp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingImp.Click
        Hint("该功能尚在开发中！")
    End Sub

#End Region

End Class
