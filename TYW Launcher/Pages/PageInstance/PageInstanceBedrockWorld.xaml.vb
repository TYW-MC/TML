Public Class PageInstanceBedrockWorld
    Implements IRefreshable

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

    Private ReadOnly Property WorldFolder As String
        Get
            Return ModBedrock.BedrockWorldFolder(Preview, BuildType)
        End Get
    End Property

    Private Sub PageOther_Loaded() Handles Me.Loaded
        If Instance Is Nothing OrElse Not Instance.IsBedrock Then Return
        AniControlEnabled += 1
        Try
            LabNote.Text = $"这些地图由{If(Preview, "预览版", "正式版")}基岩版本体共享，不属于某个具体版本。" & vbCrLf &
                           "导入时会把地图文件复制到游戏的 minecraftWorlds 文件夹，之后在游戏的「世界」列表里就能看到。"
        Finally
            AniControlEnabled -= 1
        End Try
        ReloadList()
    End Sub

    Public Sub ReloadList()
        If Instance Is Nothing OrElse Not Instance.IsBedrock Then Return
        Dim Folder = WorldFolder
        RunInNewThread(
            Sub()
                Try
                    Dim Items As List(Of ModBedrock.BedrockWorldInfo)
                    If DirectoryUtils.Exists(Folder) Then
                        Items = ModBedrock.BedrockWorldList(Preview, BuildType)
                    Else
                        Items = New List(Of ModBedrock.BedrockWorldInfo)
                    End If
                    RunInUi(Sub() RenderList(Items))
                Catch ex As Exception
                    Logger.Error(ex, "加载基岩版地图列表失败")
                    RunInUi(Sub() Hint("加载地图列表失败：" & ex.Message, HintType.Red))
                End Try
            End Sub, "Bedrock World List")
    End Sub

    Private Sub RenderList(Items As List(Of ModBedrock.BedrockWorldInfo))
        PanList.Children.Clear()
        If Not Items.Any Then
            PanListBack.Visibility = Visibility.Collapsed
            PanEmpty.Visibility = Visibility.Visible
            Return
        End If
        PanEmpty.Visibility = Visibility.Collapsed
        PanListBack.Visibility = Visibility.Visible
        For Each World In Items
            Dim InfoText As New List(Of String) From {FormatSize(World.Size)}
            If World.Modified > New Date(2000, 1, 1) Then InfoText.Add("最后游玩 " & World.Modified.ToString("yyyy-MM-dd HH:mm"))
            If World.Name <> World.FolderName Then InfoText.Add(World.FolderName)
            Dim Item As New MyListItem With {
                .Title = World.Name,
                .Info = String.Join(" · ", InfoText),
                .Type = MyListItem.CheckType.Clickable,
                .Height = 42
            }
            If World.Icon <> "" Then Item.Logo = World.Icon
            Dim Target As ModBedrock.BedrockWorldInfo = World
            AddHandler Item.Click, Sub()
                                       Dim Msg As String = $"地图：{Target.Name}" & vbCrLf &
                                                           $"大小：{FormatSize(Target.Size)}" & If(Target.Modified > New Date(2000, 1, 1),
                                                                                              "，最后游玩：" & Target.Modified.ToString("yyyy-MM-dd HH:mm"), "") & vbCrLf & vbCrLf &
                                                           "你可以打开它所在的文件夹，或者把它从游戏里删除。"
                                       Select Case MyMsgBox(Msg, "地图操作", "打开文件夹", "删除", "取消")
                                           Case 1
                                               Try
                                                   StartProcess("explorer.exe", """" & Target.Path & """")
                                               Catch ex As Exception
                                                   Logger.Error(ex, "打开地图文件夹失败")
                                                   Hint("打开文件夹失败：" & ex.Message, HintType.Red)
                                               End Try
                                           Case 2
                                               If MyMsgBox($"确定要删除地图「{Target.Name}」吗？" & vbCrLf & vbCrLf &
                                                           "地图文件会从游戏目录中彻底删除，无法恢复。", "删除地图", "删除", "取消", IsWarn:=True) = 1 Then
                                                   Try
                                                       ModBedrock.BedrockWorldDelete(Target)
                                                       Hint("已删除 " & Target.Name, HintType.Green)
                                                   Catch ex As Exception
                                                       Logger.Error(ex, "删除基岩版地图失败")
                                                       Hint("删除失败：" & ex.Message, HintType.Red)
                                                   End Try
                                                   ReloadList()
                                               End If
                                       End Select
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
            Dim Folder = WorldFolder
            If Not DirectoryUtils.Exists(Folder) Then Directory.CreateDirectory(Folder)
            StartProcess("explorer.exe", """" & Folder.TrimEnd("\"c) & """")
        Catch ex As Exception
            Logger.Error(ex, "打开基岩版地图文件夹失败")
            Hint("打开文件夹失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnImport_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnImport.Click, BtnHintImport.Click
        Dim Files = Dialogs.SelectFile("选择要导入的地图", True,
                                       filter:={({"mcworld", "zip", "mctemplate"}, "基岩版地图文件")})
        If Files Is Nothing OrElse Not Files.Any Then Return
        Hint("正在导入地图……", HintType.Blue)
        Dim FileList As String() = Files.ToArray()
        RunInNewThread(
            Sub()
                Dim Result = ModBedrock.BedrockWorldImport(FileList, Preview, BuildType)
                RunInUi(
                    Sub()
                        If Result.Failed = 0 Then
                            Hint($"已导入 {Result.Imported} 个地图，进入游戏后在「世界」列表里就能看到。", HintType.Green)
                        Else
                            Hint($"导入完成：成功 {Result.Imported} 个，失败 {Result.Failed} 个（详见日志）。", HintType.Red)
                        End If
                        ReloadList()
                    End Sub)
            End Sub, "Bedrock World Import")
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRefresh.Click
        ReloadList()
    End Sub

    Public Sub Refresh() Implements IRefreshable.Refresh
        ReloadList()
    End Sub

End Class
