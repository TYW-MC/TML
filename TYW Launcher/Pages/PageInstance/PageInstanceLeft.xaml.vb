Public Class PageInstanceLeft
    Implements IRefreshable

    ''' <summary>
    ''' 当前显示设置的 MC 版本。
    ''' </summary>
    Public Shared Instance As McInstance = Nothing

    Private Sub PageInstanceLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageSetupUI.HiddenRefresh()
        '资源包 / 行为包沿用 Mod 项的图标，避免在 XAML 里重复一大段 SVG
        If ItemResPack.Logo = "" Then ItemResPack.Logo = ItemMod.Logo
        If ItemBehPack.Logo = "" Then ItemBehPack.Logo = ItemMod.Logo
    End Sub

    Public Sub RefreshModDisabled() Handles Me.Loaded
        Dim IsBedrock As Boolean = Instance IsNot Nothing AndAlso Instance.IsBedrock
        '基岩版没有 Java 版意义上的「修改」与「导出」，Mod 管理换成资源包 / 行为包管理
        ItemModify.Visibility = If(IsBedrock, Visibility.Collapsed, Visibility.Visible)
        ItemExport.Visibility = If(IsBedrock, Visibility.Collapsed, Visibility.Visible)
        ItemResPack.Visibility = If(IsBedrock, Visibility.Visible, Visibility.Collapsed)
        ItemBehPack.Visibility = If(IsBedrock, Visibility.Visible, Visibility.Collapsed)
        If IsBedrock Then
            ItemMod.Visibility = Visibility.Collapsed
            ItemModDisabled.Visibility = Visibility.Collapsed
        ElseIf Instance IsNot Nothing AndAlso Instance.Modable Then
            ItemMod.Visibility = Visibility.Visible
            ItemModDisabled.Visibility = Visibility.Collapsed
        Else
            ItemMod.Visibility = Visibility.Collapsed
            ItemModDisabled.Visibility = Visibility.Visible
        End If
        '当前子页面在新版本下已经不可用（例如从 Java 版切到基岩版时正停在「导出」），回到概览
        Dim Invalid As Boolean = False
        Select Case PageID
            Case FormMain.PageSubType.InstanceModify, FormMain.PageSubType.InstanceExport,
                 FormMain.PageSubType.InstanceMod, FormMain.PageSubType.InstanceModDisabled
                Invalid = IsBedrock
            Case FormMain.PageSubType.InstanceResPack, FormMain.PageSubType.InstanceBehPack
                Invalid = Not IsBedrock
        End Select
        If Invalid Then
            RunInUi(
                Sub()
                    ItemOverall.Checked = True
                    PageChange(FormMain.PageSubType.InstanceOverall)
                End Sub)
        End If
    End Sub

#Region "页面切换"

    ''' <summary>
    ''' 当前页面的编号。从 0 开始计算。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.Default

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As FrameworkElement, e As RouteEventArgs) Handles ItemOverall.Check, ItemMod.Check, ItemModDisabled.Check, ItemSetup.Check, ItemModify.Check, ItemExport.Check, ItemResPack.Check, ItemBehPack.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.InstanceOverall
                If FrmInstanceOverall Is Nothing Then FrmInstanceOverall = New PageInstanceOverall
                Return FrmInstanceOverall
            Case FormMain.PageSubType.InstanceMod
                If FrmInstanceMod Is Nothing Then FrmInstanceMod = New PageInstanceMod
                Return FrmInstanceMod
            Case FormMain.PageSubType.InstanceModDisabled
                If FrmInstanceModDisabled Is Nothing Then FrmInstanceModDisabled = New PageInstanceModDisabled
                Return FrmInstanceModDisabled
            Case FormMain.PageSubType.InstanceSetup
                If FrmInstanceSetup Is Nothing Then FrmInstanceSetup = New PageInstanceSetup
                Return FrmInstanceSetup
            Case FormMain.PageSubType.InstanceExport
                If FrmInstanceExport Is Nothing Then FrmInstanceExport = New PageInstanceExport
                Return FrmInstanceExport
            Case FormMain.PageSubType.InstanceModify
                If FrmDownloadModify Is Nothing Then FrmDownloadModify = New PageDownloadModify
                Return FrmDownloadModify
            Case FormMain.PageSubType.InstanceResPack
                If FrmInstanceResPack Is Nothing Then FrmInstanceResPack = New PageInstanceBedrockPack With {.IsBehavior = False}
                Return FrmInstanceResPack
            Case FormMain.PageSubType.InstanceBehPack
                If FrmInstanceBehPack Is Nothing Then FrmInstanceBehPack = New PageInstanceBedrockPack With {.IsBehavior = True}
                Return FrmInstanceBehPack
            Case Else
                Throw New Exception("未知的版本设置子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' 切换现有页面。
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        If Instance IsNot Nothing AndAlso McInstanceLock.IsLocked(Instance) Then
            Hint("该版本正在被修改，请稍后再切换页面！", HintType.Red)
            Return
        End If
        AniControlEnabled += 1
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
        Catch ex As Exception
            Logger.Error(ex, $"切换分页面失败（ID {ID}）")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight") '停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
            AaCode(Sub()
                       CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                       FrmMain.PanMainRight.Child = FrmMain.PageRight
                       FrmMain.PageRight.Opacity = 0
                   End Sub, 130),
            AaCode(Sub()
                       '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                       FrmMain.PageRight.Opacity = 1
                       FrmMain.PageRight.PageOnEnter()
                   End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

#End Region

    Public Sub Refresh_Click(sender As Object, e As EventArgs) '由边栏按钮匿名调用
        Refresh(Val(sender.Tag))
    End Sub
    Public Sub Refresh() Implements IRefreshable.Refresh
        Refresh(FrmMain.PageCurrentSub)
    End Sub
    Public Sub Refresh(SubType As FormMain.PageSubType)
        If Instance IsNot Nothing AndAlso McInstanceLock.IsLocked(Instance) Then
            Hint("该版本正在被修改，请稍后再刷新！", HintType.Red)
            Return
        End If
        Select Case SubType
            Case FormMain.PageSubType.InstanceMod
                PageInstanceMod.Refresh()
                ItemMod.Checked = True
            Case FormMain.PageSubType.InstanceExport
                FrmInstanceExport?.RefreshAll()
                ItemExport.Checked = True
            Case FormMain.PageSubType.InstanceModify
                FrmDownloadModify?.PageLoaderRestart()
                ItemModify.Checked = True
            Case FormMain.PageSubType.InstanceResPack
                FrmInstanceResPack?.ReloadList()
                ItemResPack.Checked = True
            Case FormMain.PageSubType.InstanceBehPack
                FrmInstanceBehPack?.ReloadList()
                ItemBehPack.Checked = True
        End Select
    End Sub

    Public Sub Reset(sender As Object, e As EventArgs)
        If MyMsgBox("是否要初始化该版本的版本独立设置？该操作不可撤销。", "初始化确认",, "取消", IsWarn:=True) = 1 Then
            If IsNothing(FrmInstanceSetup) Then FrmInstanceSetup = New PageInstanceSetup
            FrmInstanceSetup.Reset()
            ItemSetup.Checked = True
        End If
    End Sub

    '加载当前版本将要选择的 Java
    Public Shared CurrentJavaWorker As New RedoableWorker(Of Java)(Function(c, p) SelectOrDownloadJava(PageInstanceLeft.Instance, False, c, p))
    Public Shared Sub ReloadCurrentJava() Handles Me.Loaded
        If AniControlEnabled <> 0 Then Return
        If PageInstanceLeft.Instance Is Nothing Then Return
        CurrentJavaWorker.Start()
    End Sub

End Class
