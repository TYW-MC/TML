Public Class PageOtherTest

    ''' <summary>
    ''' 开始一个自定义下载任务（百宝箱/自定义事件使用）。
    ''' </summary>
    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Try
            Dim CleanName As String = Path.GetFileName(Url.Split("?"c)(0).Split("#"c)(0))
            If String.IsNullOrEmpty(Folder) Then Folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) & "\Downloads\"
            If String.IsNullOrEmpty(FileName) OrElse FileName = "未知" Then FileName = CleanName
            If String.IsNullOrEmpty(FileName) Then FileName = "下载文件_" & Format(Now, "yyyyMMdd_HHmmss")
            Directory.CreateDirectory(Folder)
            Dim LocalPath As String = Folder & If(Folder.EndsWithF("\"), "", "\") & FileName
            Dim Task As New LoaderDownload(FileName, New List(Of NetFile) From {New NetFile({Url}, LocalPath)})
            Dim Combo As New LoaderCombo(Of Integer)("下载：" & FileName, {Task})
            Combo.Start()
            LoaderTaskbarAdd(Combo)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            Hint("已开始下载 " & FileName & "，保存于 " & Folder, HintType.Green)
        Catch ex As Exception
            Hint("下载失败：" & ex.Message, HintType.Red)
            Logger.Error(ex, "自定义下载失败")
        End Try
    End Sub

    Public Shared Sub Jrrp()
        Hint("今日人品为 " & RandomInteger(0, 100) & " 点！", HintType.Green)
    End Sub

    Public Shared Sub RubbishClear()
        Try
            Dim Cleaned As Long = 0
            Dim Dirs As New List(Of String) From {PathTemp & "Cache", PathTemp & "Download"}
            For Each CleanDir In Dirs
                If Directory.Exists(CleanDir) Then
                    Try
                        Cleaned += New DirectoryInfo(CleanDir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(Function(f) f.Length)
                    Catch ignored As Exception
                    End Try
                    Directory.Delete(CleanDir, True)
                End If
            Next
            Hint("垃圾清理完成，共释放 " & Math.Round(Cleaned / 1024 / 1024, 1) & " MB", HintType.Green)
        Catch ex As Exception
            Hint("垃圾清理失败：" & ex.Message, HintType.Red)
            Logger.Error(ex, "垃圾清理失败")
        End Try
    End Sub

    '★ EmptyWorkingSet 的导出名在 psapi.dll 里；kernel32.dll 里是改名后的 K32EmptyWorkingSet。
    '  按原名去 kernel32 找会抛 EntryPointNotFoundException（早期版本就是这么写错的）。
    <Runtime.InteropServices.DllImport("psapi.dll", SetLastError:=True)>
    Private Shared Function EmptyWorkingSet(hProcess As IntPtr) As Boolean
    End Function

    '备用方案：kernel32 里的 SetProcessWorkingSetSize，最小/最大值都传 -1 时等同于清空工作集
    <Runtime.InteropServices.DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function SetProcessWorkingSetSize(hProcess As IntPtr, minWorkingSet As IntPtr, maxWorkingSet As IntPtr) As Boolean
    End Function

    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        Try
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            Using P = Process.GetCurrentProcess()
                Dim Ok As Boolean
                Try
                    Ok = EmptyWorkingSet(P.Handle)
                Catch ex As EntryPointNotFoundException
                    Ok = False
                End Try
                If Not Ok Then
                    'psapi 找不到该导出（极精简的运行环境）时退回 SetProcessWorkingSetSize，仍失败就静默跳过
                    Try
                        SetProcessWorkingSetSize(P.Handle, New IntPtr(-1), New IntPtr(-1))
                    Catch ex As Exception
                        Logger.Error(ex, "内存优化（备用方案）失败")
                    End Try
                End If
            End Using
            If ShowHint Then Hint("内存优化完成！", HintType.Green)
        Catch ex As Exception
            If ShowHint Then Hint("内存优化失败", HintType.Red)
            Logger.Error(ex, "内存优化失败")
        End Try
    End Sub

    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        MemoryOptimize(ShowHint)
    End Sub

    Public Shared Function GetRandomCave() As String
        Static Caves As New List(Of String) From {
            "与其最终离去，不如从未归来。",
            "在方寸屏幕之间，亦有山川湖海。",
            "每一个像素，都是世界的一块拼图。",
            "红石电路的尽头，是逻辑的浪漫。",
            "世界很大，区块未加载的地方更远。"
        }
        Return Caves(RandomInteger(0, Caves.Count - 1))
    End Function

    Public Shared Function GetRandomHint() As String
        Static Hints As New List(Of String) From {
            "按下 Ctrl + Tab 可以快速切换页面。",
            "在主页右键可以隐藏卡片。",
            "多版本共存时，记得为每个版本单独设置 Java。",
            "下载慢可以试试切换下载源（设置 → 系统 → 下载源）。",
            "游戏崩溃了？查看崩溃分析或许能直接找到原因。"
        }
        Return Hints(RandomInteger(0, Hints.Count - 1))
    End Function

    Public Shared Function GetRandomPresetHint() As String
        Static Presets As New List(Of String) From {
            "自定义主页可以用来展示服务器公告。",
            "背景音乐支持播放列表。",
            "版本隔离可以避免不同版本的 Mod 冲突。"
        }
        Return Presets(RandomInteger(0, Presets.Count - 1))
    End Function

    '页面按钮事件
    Private Sub BtnTestMemory_Click(sender As Object, e As EventArgs) Handles BtnTestMemory.Click
        MemoryOptimize(True)
    End Sub
    Private Sub BtnTestRubbish_Click(sender As Object, e As EventArgs) Handles BtnTestRubbish.Click
        If MyMsgBox("即将清理 TYW Launcher 的缓存目录（Cache / Download），清理后相关文件会自动重新下载。" & vbCrLf & "确定继续吗？", "垃圾清理", "确定", "取消") = 1 Then
            RubbishClear()
        End If
    End Sub
    Private Sub BtnTestJrrp_Click(sender As Object, e As EventArgs) Handles BtnTestJrrp.Click
        MyMsgBox("你今日的人品值为：" & vbCrLf & RandomInteger(0, 100) & " 点", "今日人品", "再来一次", "关闭")
    End Sub
    Private Sub BtnTestHint_Click(sender As Object, e As EventArgs) Handles BtnTestHint.Click
        MyMsgBox(GetRandomHint(), "随机提示")
    End Sub
    Private Sub BtnTestCave_Click(sender As Object, e As EventArgs) Handles BtnTestCave.Click
        MyMsgBox(GetRandomCave(), "随机格言")
    End Sub
    Private Sub BtnTestDownload_Click(sender As Object, e As EventArgs) Handles BtnTestDownload.Click
        Dim Url As String = TextTestDownloadUrl.Text.Trim
        If String.IsNullOrEmpty(Url) Then
            MyMsgBox("请先输入下载地址。", "文件下载")
            Return
        End If
        StartCustomDownload(Url, TextTestDownloadName.Text.Trim)
    End Sub

End Class
