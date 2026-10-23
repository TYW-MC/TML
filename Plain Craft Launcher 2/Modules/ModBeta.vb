Imports Newtonsoft.Json.Linq

''' <summary>
''' 内测版（Beta 通道）：内测码校验、通道切换、启动前联网校验。
''' 内测版由 Debug 配置编译（BETA 宏），没有自动更新，也不提供检查更新。
''' </summary>
Module ModBeta

    ''' <summary>内测版启动前的联网校验结果。</summary>
    Public Enum BetaGate
        ''' <summary>校验通过，可以正常使用。</summary>
        Ok = 0
        ''' <summary>资格失效或无法连接，需要切回正式版。</summary>
        SwitchRelease = 1
        ''' <summary>用户选择退出。</summary>
        [Exit] = 2
    End Enum

    Public Structure BetaResult
        Public Ok As Boolean
        Public Message As String
        Public VersionCode As Integer
    End Structure

    ''' <summary>通道名 → 显示名。</summary>
    Friend Function ChannelDisplay(Channel As String) As String
        Select Case Channel
            Case "Beta" : Return "内测版"
            Case "Snapshot" : Return "快照版"
            Case Else : Return "正式版"
        End Select
    End Function

    ''' <summary>通道名 → 服务端 www 中的文件名。</summary>
    Friend Function ChannelExeFile(Channel As String) As String
        Select Case Channel
            Case "Beta" : Return "TYW Minecraft Launcher Beta.exe"
            Case "Snapshot" : Return "TYW Minecraft Launcher Snapshot.exe"
            Case Else : Return "TYW Minecraft Launcher.exe"
        End Select
    End Function

    ''' <summary>
    ''' 把设备码 + 内测码送到服务端校验（GET /beta）。
    ''' 服务端会顺带返回内测版版本号，方便客户端判断是否需要重新下载内测版程序。
    ''' </summary>
    Friend Function VerifyBetaCode(Code As String) As BetaResult
        Dim Url As String = ServerAddress & "beta?ident=" & Uri.EscapeDataString(DeviceCode) &
                            "&code=" & Uri.EscapeDataString(If(Code, "").Trim)
        Dim Raw As String = NetRequestByLoader(Url, 12000)
        Dim Json As JObject = JObject.Parse(Raw)
        Dim R As New BetaResult With {
            .Ok = CBool(If(Json("ok") IsNot Nothing, Json("ok"), False)),
            .Message = If(Json("msg") Is Nothing, "", Json("msg").ToString),
            .VersionCode = If(Json("version_code") Is Nothing, 0, CInt(Json("version_code")))}
        Return R
    End Function

    ''' <summary>
    ''' 下载指定通道的启动器并替换重启（Release / Snapshot / Beta）。
    ''' 用于内测版体验开关、取消内测、快照开关切换时的程序本体替换。
    ''' </summary>
    Friend Function SwitchChannel(Channel As String) As Boolean
        Dim Url As String = ServerAddress & ChannelExeFile(Channel)
        Dim TargetName As String = ChannelDisplay(Channel)
        Dim UpdatePath As String = PathTemp & "TYW Launcher\Update.exe"
        FileUtils.Delete(UpdatePath)
        Hint("正在下载" & TargetName & "启动器……")
        Dim DlLoader As New LoaderDownload("下载" & TargetName & "启动器",
            New List(Of NetFile) From {New NetFile({Url}, UpdatePath)})
        Dim Combo As New LoaderCombo(Of Integer)("下载" & TargetName & "启动器", {DlLoader})
        Combo.Start()
        LoaderTaskbarAdd(Combo)
        If FrmMain IsNot Nothing Then
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        End If
        Combo.WaitForExit()
        '下载被取消或失败：不替换程序、不结束当前进程，直接返回 False，由调用方回滚通道状态
        '（之前不管结果都会替换并 EndProgram，导致下载失败时启动器直接关掉打不开）
        If Combo.State <> LoadState.Finished Then
            Logger.Warn($"下载{TargetName}启动器未完成（{Combo.State}），已放弃切换，启动器继续运行")
            FileUtils.Delete(UpdatePath)
            Hint("已取消下载" & TargetName & "，启动器保持在当前版本", HintType.Blue, True)
            Return False
        End If
        '兜底校验：必须是完整下载下来的 exe（大小合理且带 MZ 头），否则同样不做替换
        Dim Size As Long = 0
        Try
            If IO.File.Exists(UpdatePath) Then Size = New IO.FileInfo(UpdatePath).Length
        Catch
        End Try
        Dim IsExe As Boolean = False
        If Size > 2 * 1024 * 1024 Then
            Try
                Dim Head(1) As Byte
                Using Fs As IO.FileStream = IO.File.OpenRead(UpdatePath)
                    Fs.Read(Head, 0, 2)
                End Using
                IsExe = (Head(0) = &H4D AndAlso Head(1) = &H5A) 'MZ
            Catch
            End Try
        End If
        If Not IsExe Then
            Logger.Warn($"下载{TargetName}启动器得到的文件异常（{Size} 字节），已放弃切换")
            FileUtils.Delete(UpdatePath)
            Hint("下载" & TargetName & "启动器失败，启动器保持在当前版本", HintType.Red, True)
            Return False
        End If
        Hint(TargetName & "下载完成，即将替换并重启……", HintType.Green)
        '启动替换进程：--update 旧进程PID 旧文件 新文件 是否重启
        Dim NewProcess As New ProcessStartInfo(UpdatePath) With {
            .Arguments = $"--update {Process.GetCurrentProcess().Id} ""{PathExe}"" ""{UpdatePath}"" True",
            .WindowStyle = ProcessWindowStyle.Hidden,
            .UseShellExecute = False}
        Process.Start(NewProcess)
        FrmMain.EndProgram(False)
        Return True
    End Function

    ''' <summary>
    ''' 内测版启动前的联网校验：必须能连上服务端，并且设备的内测码仍然有效。
    ''' 无法连接或资格失效时由调用方决定切回正式版或退出。
    ''' </summary>
    Friend Function CheckBetaUsable() As BetaGate
        If Not ServerEnabled Then Return BetaGate.Ok 'Server not configured: skip the online check
        Dim Msg As String
        Dim IsNetwork As Boolean = False
        Hint("正在校验内测资格……")
        Try
            ReadServerNotice(8000) '先确认服务端可达
            Dim Result = VerifyBetaCode(Settings.Get(Of String)("BetaCode"))
            If Result.Ok Then
                Logger.Info($"内测版校验通过（服务端内测版版本码 {Result.VersionCode}）")
                Return BetaGate.Ok
            End If
            Msg = "内测资格已失效：" & If(Result.Message = "", "内测码无效或已被停用", Result.Message)
        Catch ex As Exception
            IsNetwork = IsNetworkIssue(ex)
            Msg = "内测版必须联网并连接服务端才能使用。" & vbCrLf &
                  "当前无法连接服务端：" & ServerAddress & vbCrLf &
                  "请检查网络后重新启动；也可以先切回正式版。"
            Logger.Warn(ex, "内测版联网校验失败")
        End Try
        Dim Choice As Integer = 2
        RunInUiWait(
            Sub()
                Choice = MyMsgBox(Msg & vbCrLf & vbCrLf & "是否切回正式版？" & vbCrLf &
                                  "（切回后内测码会从本机移除，需要重新输入才能再进内测版）",
                                  "内测版无法使用", "切回正式版", "退出", IsWarn:=True)
            End Sub)
        If Choice = 1 Then Return BetaGate.SwitchRelease
        Return BetaGate.Exit
    End Function

    ''' <summary>清除本机内测状态（切回正式版 / 快照版前调用）。</summary>
    Friend Sub ClearBetaState()
        Settings.Set("BetaEnabled", False)
        Settings.Set("BetaCode", "")
    End Sub

End Module
