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

    ''' <summary>
    ''' 今日人品值（0-100）：按当天日期生成，同一天无论抽多少次都是同一个数字，跨天才会变化。
    ''' </summary>
    Public Shared Function JrrpValue() As Integer
        Dim SeedText As String = "TYW-Jrrp-" & Format(Date.Now, "yyyyMMdd")
        Dim Hash As Byte() = System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(SeedText))
        Return CInt(CLng(BitConverter.ToUInt32(Hash, 0)) Mod 101)
    End Function

    ''' <summary>
    ''' 按人品值给出分档评语：每 10 点为一档（0-9、10-19 …… 90-99，100 单独一档）。
    ''' </summary>
    Public Shared Function JrrpComment(Value As Integer) As String
        Select Case Value
            Case 100
                Return "满人品！今天干什么都顺，记得把好运分一点给别人。"
            Case Is >= 90
                Return "欧气满满，钻石矿脉正在向你招手。"
            Case Is >= 80
                Return "运气极佳，适合挑战一直不敢打的地牢。"
            Case Is >= 70
                Return "手气挺好，赶紧去挖矿或者开箱吧。"
            Case Is >= 60
                Return "运气不错，适合出门探索新地形。"
            Case Is >= 50
                Return "普普通通的一天，稳扎稳打就好。"
            Case Is >= 40
                Return "差一点点就过半了，别灰心。"
            Case Is >= 30
                Return "今天有点平淡，做点简单的事吧。"
            Case Is >= 20
                Return "不太顺，先整理整理箱子、修修房子。"
            Case Is >= 10
                Return "运气有点背，下矿记得带水桶和床。"
            Case Else
                Return "今天连方块都不想放，早点休息吧。"
        End Select
    End Function

    Public Shared Sub Jrrp()
        Dim Value As Integer = JrrpValue()
        Hint($"今日人品为 {Value} 点！{JrrpComment(Value)}", If(Value < 50, HintType.Red, HintType.Green))
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

    ''' <summary>
    ''' 对全系统中工作集大于 20MB 的进程逐个 EmptyWorkingSet，返回成功的个数。
    ''' 供提权进程（--deepmem 参数自启）调用，此时已具备管理员权限。
    ''' </summary>
    Public Shared Function DeepOptimizeAllProcesses() As Integer
        Dim Count As Integer = 0
        For Each P As Process In Process.GetProcesses()
            Try
                If P.WorkingSet64 < 20 * 1024 * 1024 Then Continue For
                Dim Ok As Boolean = False
                Try
                    Ok = EmptyWorkingSet(P.Handle)
                Catch ex As EntryPointNotFoundException
                    Ok = False
                End Try
                If Not Ok Then
                    Try
                        Ok = SetProcessWorkingSetSize(P.Handle, New IntPtr(-1), New IntPtr(-1))
                    Catch
                    End Try
                End If
                If Ok Then Count += 1
            Catch
            Finally
                Try
                    P.Dispose()
                Catch
                End Try
            End Try
        Next
        Return Count
    End Function

    ''' <summary>
    ''' 提权深度内存优化：先清理启动器自身，再以管理员权限自启一个启动器进程（--deepmem 参数，
    ''' UAC 弹窗显示为 TYW Launcher），由它对全系统高内存进程做深度整理。
    ''' 必须在非 UI 线程调用（内部会等待提权进程结束）。
    ''' </summary>
    Public Shared Sub DeepMemoryOptimize(ShowHint As Boolean)
        MemoryOptimize(False) '第一步：先清启动器自己（不需要提权）
        Dim ResultFile As String = PathTemp & "DeepMemResult.txt"
        Try
            If File.Exists(ResultFile) Then File.Delete(ResultFile)
        Catch
        End Try
        Try
            '以管理员权限自启启动器：UAC 弹窗显示 TYW Launcher 而不是 PowerShell
            Dim Psi As New ProcessStartInfo(PathExe) With {
                .Arguments = "--deepmem """ & ResultFile & """",
                .Verb = "runas",
                .WindowStyle = ProcessWindowStyle.Hidden,
                .UseShellExecute = True}
            Dim P = Process.Start(Psi)
            If P IsNot Nothing Then P.WaitForExit(120000)
            Dim CountValue As Integer = -1
            Try
                If File.Exists(ResultFile) Then Integer.TryParse(File.ReadAllText(ResultFile).Trim, CountValue)
            Catch
            End Try
            If Not ShowHint Then Return
            If CountValue > 0 Then
                Hint($"内存优化完成！已整理 {CountValue} 个进程的内存。", HintType.Green)
            ElseIf CountValue = 0 Then
                Hint("内存优化完成！", HintType.Green)
            Else
                '提权进程没能写回结果（例如被安全软件拦截）：不视为失败
                Hint("内存优化完成！", HintType.Green)
            End If
        Catch ex As Exception
            Dim W32 = TryCast(ex, System.ComponentModel.Win32Exception)
            If W32 IsNot Nothing AndAlso W32.NativeErrorCode = 1223 Then
                If ShowHint Then Hint("已取消管理员授权，仅清理了启动器自身的内存", HintType.Blue)
            Else
                If ShowHint Then Hint("深度内存优化失败：" & ex.Message, HintType.Red)
                Logger.Error(ex, "提权深度内存优化失败")
            End If
        End Try
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


    Private Sub BtnTestMemory_Click(sender As Object, e As EventArgs) Handles BtnTestMemory.Click
        Hint("正在请求管理员权限进行内存优化……")
        RunInNewThread(Sub() DeepMemoryOptimize(True), "Deep Memory Optimize")
    End Sub
    Private Sub BtnTestRubbish_Click(sender As Object, e As EventArgs) Handles BtnTestRubbish.Click
        If MyMsgBox("即将清理 TYW Launcher 的缓存目录（Cache / Download），清理后相关文件会自动重新下载。" & vbCrLf & "确定继续吗？", "垃圾清理", "确定", "取消") = 1 Then
            RubbishClear()
        End If
    End Sub
    Private Sub BtnTestJrrp_Click(sender As Object, e As EventArgs) Handles BtnTestJrrp.Click
        Dim Value As Integer = JrrpValue()
        '低于 50 时用警告样式（红色标题与按钮）提示
        MyMsgBox($"你今日的人品值为：{vbCrLf}{Value} 点{vbCrLf}{vbCrLf}{JrrpComment(Value)}", "今日人品", IsWarn:=Value < 50)
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
