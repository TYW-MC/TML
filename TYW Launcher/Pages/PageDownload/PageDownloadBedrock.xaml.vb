Imports System.Threading

Public Class PageDownloadBedrock

#Region "加载"

    '防止重复并发加载版本列表
    Private IsLoadingList As Boolean = False

    Private Sub PageDownloadBedrock_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        RefreshInstalled()
        If ModBedrock.VersionList Is Nothing AndAlso Not IsLoadingList Then
            LoadListRun(False)
        Else
            RenderList()
        End If
    End Sub

    ''' <summary>
    ''' 刷新已安装检测与启动按钮。
    ''' </summary>
    Private Sub RefreshInstalled()
        LabInstalled.Text = ModLang.T("正在检测已安装的基岩版……")
        BtnLaunch.Visibility = Visibility.Collapsed
        RunInNewThread(Sub()
                           Dim Retail = ModBedrock.BedrockGetInstalled(False)
                           Dim Preview = ModBedrock.BedrockGetInstalled(True)
                           RunInUi(Sub()
                                       If Retail IsNot Nothing OrElse Preview IsNot Nothing Then
                                           Dim Texts As New List(Of String)
                                           If Retail IsNot Nothing Then Texts.Add("正式版 " & Retail)
                                           If Preview IsNot Nothing Then Texts.Add("预览版 " & Preview)
                                           LabInstalled.Text = ModLang.T("已安装：%TEXT%").Replace("%TEXT%", String.Join("；", Texts))
                                           BtnLaunch.Visibility = Visibility.Visible
                                           BtnLaunch.Tag = Retail IsNot Nothing 'True = 启动正式版
                                       Else
                                           LabInstalled.Text = ModLang.T("未检测到已安装的基岩版")
                                           BtnLaunch.Visibility = Visibility.Collapsed
                                       End If
                                   End Sub)
                       End Sub)
    End Sub

    Private Sub BtnLaunch_Click(sender As Object, e As EventArgs) Handles BtnLaunch.Click
        ModBedrock.BedrockLaunch(Not CBool(BtnLaunch.Tag))
    End Sub

    ''' <summary>供其他页面调用：刷新已安装检测。</summary>
    Public Sub RefreshInstalledPublic()
        RefreshInstalled()
    End Sub

#End Region

#Region "版本列表"

    Private Sub RadioRetail_Checked(sender As Object, e As RouteEventArgs) Handles RadioRetail.Check, RadioPreview.Check
        If RadioRetail.Tag Is Nothing OrElse RadioPreview.Tag Is Nothing Then Return '尚未初始化
        RenderList()
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As EventArgs) Handles BtnRefresh.Click
        LoadListRun(True)
    End Sub

    ''' <summary>
    ''' 供侧栏刷新按钮调用：强制刷新版本列表与已安装检测。
    ''' </summary>
    Public Sub RefreshList()
        RefreshInstalled()
        LoadListRun(True)
    End Sub

    ''' <summary>
    ''' 在工作线程拉取版本列表并渲染。
    ''' </summary>
    Private Sub LoadListRun(Force As Boolean)
        If IsLoadingList Then Return
        IsLoadingList = True
        RadioRetail.Tag = Nothing
        RadioPreview.Tag = Nothing
        LabLoading.Visibility = Visibility.Visible
        PanList.Children.Clear()
        RunInThread(Sub()
                        Try
                            ModBedrock.BedrockVersionListLoad(Force)
                            RunInUi(Sub()
                                        HintList.Visibility = Visibility.Collapsed
                                        LabLoading.Visibility = Visibility.Collapsed
                                        RadioRetail.Tag = New Object
                                        RadioPreview.Tag = New Object
                                        RenderList()
                                    End Sub)
                        Catch ex As Exception
                            Logger.Error(ex, "加载基岩版版本列表失败")
                            RunInUi(Sub()
                                        HintList.Text = "版本列表加载失败：" & ex.Message
                                        HintList.Visibility = Visibility.Visible
                                        LabLoading.Visibility = Visibility.Collapsed
                                        RadioRetail.Tag = New Object
                                        RadioPreview.Tag = New Object
                                        RenderList()
                                    End Sub)
                        Finally
                            IsLoadingList = False
                        End Try
                    End Sub)
    End Sub

    ''' <summary>
    ''' 根据当前渠道渲染版本列表。
    ''' </summary>
    Private Sub RenderList()
        If RadioRetail.Tag Is Nothing OrElse RadioPreview.Tag Is Nothing Then Return
        PanList.Children.Clear()
        If ModBedrock.VersionList Is Nothing Then Return
        Dim Preview As Boolean = RadioPreview.Checked
        Dim List As List(Of BedrockVersionEntry) = ModBedrock.BedrockVersionFilter(Preview)
        If Not List.Any Then
            Dim Empty As New TextBlock With {.Text = ModLang.T("没有可用的版本"), .Opacity = 0.6, .Margin = New Thickness(0, 6, 0, 6)}
            ModLang.IgnoreElement(Empty)
            PanList.Children.Add(Empty)
            Return
        End If
        Dim BestArch As String = ModBedrock.BedrockBestArch()
        For Each Entry In List.Take(150) '最多显示 150 个，避免卡顿
            Dim Item As New MyListItem With {
                .Title = Entry.Version,
                .Type = MyListItem.CheckType.Clickable,
                .Tag = Entry,
                .MinPaddingRight = 50,
                .Height = 40,
                .IsScaleAnimationEnabled = False
            }
            Dim InfoParts As New List(Of String)
            If Entry.ReleaseDate <> "" Then InfoParts.Add(Entry.ReleaseDate)
            If Entry.BuildType = "GDK" Then InfoParts.Add("GDK（需 LeviLauncher）")
            If Entry.Type = "Beta" Then InfoParts.Add(ModLang.T("旧测试版"))
            If Entry.Variations.ContainsKey(BestArch) Then
                InfoParts.Add(BestArch)
            Else
                InfoParts.Add(Entry.Variations.Keys.First())
            End If
            Item.Info = String.Join("　", InfoParts)
            AddHandler Item.Click, Sub(sender As Object, e As MouseEventArgs)
                                       Dim Clicked As BedrockVersionEntry = CType(CType(sender, MyListItem).Tag, BedrockVersionEntry)
                                       DownloadRun(Clicked)
                                   End Sub
            PanList.Children.Add(Item)
        Next
    End Sub

#End Region

#Region "下载与安装"

    Private Sub DownloadRun(Entry As BedrockVersionEntry)
        RunInNewThread(Sub()
                           If ModBedrock.BedrockDownloadAndInstall(Entry) Then RefreshInstalled()
                       End Sub, "Bedrock Download")
    End Sub

#End Region

End Class
