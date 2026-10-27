Public Class PageInstanceBedrockPack
    Implements IRefreshable

    ''' <summary>
    ''' True 表示行为包，False 表示资源包。由 PageInstanceLeft.PageGet 在创建时设置。
    ''' </summary>
    Public IsBehavior As Boolean = False

    Private ReadOnly Property Instance As McInstance
        Get
            Return PageInstanceLeft.Instance
        End Get
    End Property

    Private ReadOnly Property Preview As Boolean
        Get
            Return Instance IsNot Nothing AndAlso Instance.BedrockChannel = "preview"
        End Get
    End Property

    Private ReadOnly Property BuildType As String
        Get
            Return If(Instance?.BedrockBuildType, "UWP")
        End Get
    End Property

    Private ReadOnly Property PackFolder As String
        Get
            Return ModBedrock.BedrockPackFolder(Preview, BuildType, IsBehavior)
        End Get
    End Property

    Private Sub PageOther_Loaded() Handles Me.Loaded
        If Instance Is Nothing OrElse Not Instance.IsBedrock Then Return
        AniControlEnabled += 1
        Try
            LabEmptyTitle.Text = If(IsBehavior, "尚未安装行为包", "尚未安装资源包")
            LabEmptyDesc.Text = If(IsBehavior,
                "行为包可以改变游戏玩法。你可以导入 .mcpack 或 .mcaddon 文件，也可以直接把包放进游戏的 behavior_packs 文件夹。",
                "资源包可以改变材质、音效与界面。你可以导入 .mcpack 或 .mcaddon 文件，也可以直接把包放进游戏的 resource_packs 文件夹。")
            LabNote.Text = $"这些包由{If(Preview, "预览版", "正式版")}基岩版本体共享，不属于某个具体版本。" & vbCrLf &
                           "导入的包会直接解压到游戏目录，导入后请在游戏内 设置 → 全局资源 中启用。"
        Finally
            AniControlEnabled -= 1
        End Try
        ReloadList()
    End Sub

    Public Sub ReloadList()
        If Instance Is Nothing OrElse Not Instance.IsBedrock Then Return
        Dim Folder = PackFolder
        RunInNewThread(
            Sub()
                Try
                    Dim Items As List(Of ModBedrock.BedrockPackInfo)
                    If DirectoryUtils.Exists(Folder) Then
                        Items = ModBedrock.BedrockPackList(Preview, BuildType, IsBehavior)
                    Else
                        Items = New List(Of ModBedrock.BedrockPackInfo)
                    End If
                    RunInUi(Sub() RenderList(Items))
                Catch ex As Exception
                    Logger.Error(ex, "加载基岩版包列表失败")
                    RunInUi(Sub() Hint("加载包列表失败：" & ex.Message, HintType.Red))
                End Try
            End Sub, "Bedrock Pack List")
    End Sub

    Private Sub RenderList(Items As List(Of ModBedrock.BedrockPackInfo))
        PanList.Children.Clear()
        If Not Items.Any Then
            PanListBack.Visibility = Visibility.Collapsed
            PanEmpty.Visibility = Visibility.Visible
            Return
        End If
        PanEmpty.Visibility = Visibility.Collapsed
        PanListBack.Visibility = Visibility.Visible
        For Each Pack In Items
            Dim InfoText As New List(Of String)
            If Pack.Version <> "" Then InfoText.Add("版本 " & Pack.Version)
            InfoText.Add(FormatSize(Pack.Size))
            If Pack.IsFile Then InfoText.Add("未解压的 .mcpack")
            Dim Item As New MyListItem With {
                .Title = Pack.Name,
                .Info = String.Join(" · ", InfoText),
                .Type = MyListItem.CheckType.Clickable,
                .Height = 42
            }
            If Pack.Description <> "" Then Item.ToolTip = Pack.Description
            Dim Target As ModBedrock.BedrockPackInfo = Pack
            AddHandler Item.Click, Sub()
                                       If MyMsgBox($"是否删除{If(IsBehavior, "行为包", "资源包")}「{Target.Name}」？" & vbCrLf & vbCrLf &
                                                   "该操作会把包从游戏目录中移除，不可撤销。", "删除包", "删除", "取消", IsWarn:=True) = 1 Then
                                           Try
                                               ModBedrock.BedrockPackDelete(Target)
                                               Hint("已删除 " & Target.Name, HintType.Green)
                                           Catch ex As Exception
                                               Logger.Error(ex, "删除基岩版包失败")
                                               Hint("删除失败：" & ex.Message, HintType.Red)
                                           End Try
                                           ReloadList()
                                       End If
                                   End Sub
            PanList.Children.Add(Item)
        Next
    End Sub

    Private Shared Function FormatSize(Size As Long) As String
        If Size >= 1024L * 1024L Then Return (Size / 1024.0 / 1024.0).ToString("0.0") & " MB"
        If Size >= 1024 Then Return (Size / 1024.0).ToString("0") & " KB"
        Return Size & " B"
    End Function

    Private Sub BtnOpen_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpen.Click, BtnHintOpen.Click
        Try
            Dim Folder = PackFolder
            If Not DirectoryUtils.Exists(Folder) Then Directory.CreateDirectory(Folder)
            StartProcess("explorer.exe", """" & Folder.TrimEnd("\"c) & """")
        Catch ex As Exception
            Logger.Error(ex, "打开基岩版包文件夹失败")
            Hint("打开文件夹失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnImport_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnImport.Click, BtnHintImport.Click
        Dim Files = Dialogs.SelectFile(If(IsBehavior, "选择要导入的行为包", "选择要导入的资源包"), True,
                                       filter:={({"mcpack", "mcaddon", "zip"}, "基岩版包文件")})
        If Files Is Nothing OrElse Not Files.Any Then Return
        Hint("正在导入……", HintType.Blue)
        Dim FileList As String() = Files.ToArray()
        RunInNewThread(
            Sub()
                Dim Result = ModBedrock.BedrockPackImport(FileList, Preview, BuildType)
                RunInUi(
                    Sub()
                        If Result.Failed = 0 Then
                            Hint($"已导入 {Result.Imported} 个包，可在游戏内的全局资源中启用。", HintType.Green)
                        Else
                            Hint($"导入完成：成功 {Result.Imported} 个，失败 {Result.Failed} 个（详见日志）。", HintType.Red)
                        End If
                        ReloadList()
                    End Sub)
            End Sub, "Bedrock Pack Import")
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRefresh.Click
        ReloadList()
    End Sub

    Public Sub Refresh() Implements IRefreshable.Refresh
        ReloadList()
    End Sub

End Class
