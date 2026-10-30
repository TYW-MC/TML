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
            Case "Beta" : Return "TYW Launcher Beta.exe"
            Case "Snapshot" : Return "TYW Launcher Snapshot.exe"
            Case Else : Return "TYW Launcher.exe"
        End Select
    End Function

    ''' <summary>
    ''' 把设备码 + 内测码送到服务端校验（GET /beta）。
    ''' 服务端会顺带返回内测版版本号，方便客户端判断是否需要重新下载内测版程序。
    ''' </summary>
    Friend Function VerifyBetaCode(Code As String) As BetaResult
        Dim Url As String = ServerAddress & "beta?ident=" & Uri.EscapeDataString(DeviceCode) &
                            "&code=" & Uri.EscapeDataString(If(Code, "").Trim)
        Dim Raw As String = ServerRequest(Url, 12000)
        Dim Json As JObject = JObject.Parse(Raw)
        Dim R As New BetaResult With {
            .Ok = CBool(If(Json("ok") IsNot Nothing, Json("ok"), False)),
            .Message = If(Json("msg") Is Nothing, "", Json("msg").ToString),
            .VersionCode = If(Json("version_code") Is Nothing, 0, CInt(Json("version_code")))}
        Return R
    End Function

    ''' <summary>
    ''' 把设备码 + 单次更新码送到服务端校验（GET /betaupdate）。
    ''' 服务端校验通过会立即把更新码标记为已用；客户端更新完成后再上报 done=1 删除记录。
    ''' 已是最新版本时服务端不消耗更新码。
    ''' </summary>
    Friend Function VerifyBetaUpdateCode(Code As String) As BetaResult
        Dim Url As String = ServerAddress & "betaupdate?ident=" & Uri.EscapeDataString(DeviceCode) &
                            "&code=" & Uri.EscapeDataString(If(Code, "").Trim) &
                            "&ver=" & VersionCode
        Dim Raw As String = ServerRequest(Url, 12000)
        Dim Json As JObject = JObject.Parse(Raw)
        Dim R As New BetaResult With {
            .Ok = CBool(If(Json("ok") IsNot Nothing, Json("ok"), False)),
            .Message = If(Json("msg") Is Nothing, "", Json("msg").ToString),
            .VersionCode = If(Json("version_code") Is Nothing, 0, CInt(Json("version_code")))}
        Return R
    End Function

    ''' <summary>
    ''' 内测版更新完成后上报服务端（GET /betaupdate?...&amp;done=1）：删除已用的更新码记录。
    ''' 尽力而为：失败只写日志，不影响更新流程。
    ''' </summary>
    Friend Sub ReportBetaUpdateDone(Code As String)
        Try
            Dim Url As String = ServerAddress & "betaupdate?ident=" & Uri.EscapeDataString(DeviceCode) &
                                "&code=" & Uri.EscapeDataString(If(Code, "").Trim) & "&done=1"
            ServerRequest(Url, 8000)
            Logger.Info("内测版更新完成已上报服务端，更新码记录已删除")
        Catch ex As Exception
            Logger.Warn(ex, "内测版更新完成上报失败（不影响更新，服务端记录可由管理员手动删除）")
        End Try
    End Sub

    ''' <summary>
    ''' 下载指定通道的启动器并替换重启（Release / Snapshot / Beta）。
    ''' 用于内测版体验开关、取消内测、快照开关切换时的程序本体替换。
    ''' OnDownloaded：下载校验完成、即将替换重启前回调（用于内测版更新完成上报）。
    ''' Ident / Key：下载内测版程序时附带的服务端凭据（设备码 + 内测资格码或单次更新码）；
    ''' 服务端会校验，缺失或无效会 403 拒绝下载。
    ''' </summary>
    Friend Function SwitchChannel(Channel As String, Optional OnDownloaded As Action = Nothing,
                                  Optional Ident As String = "", Optional Key As String = "") As Boolean
        Dim Url As String = ServerAddress & ChannelExeFile(Channel)
        If Channel = "Beta" AndAlso Key <> "" Then
            Url &= "?ident=" & Uri.EscapeDataString(If(Ident, DeviceCode)) &
                   "&key=" & Uri.EscapeDataString(Key)
        End If
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
        '更新完成回调（如内测版更新码的上报删除）：趁旧进程还在，先把结果告诉服务端
        If OnDownloaded IsNot Nothing Then
            Try
                OnDownloaded()
            Catch ex As Exception
                Logger.Warn(ex, "更新完成回调失败")
            End Try
        End If
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
        Dim Msg As String
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
            Msg = "内测版必须联网并连接服务端才能使用。" & vbCrLf &
                  "当前无法连接服务端：" & ServerAddress & vbCrLf &
                  "请检查网络后重新启动；也可以先切回正式版。"
            Logger.Warn(ex, "内测版联网校验失败")
        End Try
        '处理方式：切回正式版 / 重新输入内测码（验证通过即可继续使用内测版）/ 退出
        Do
            Dim Choice As Integer = 3
            RunInUiWait(
                Sub()
                    Choice = MyMsgBox(Msg & vbCrLf & vbCrLf &
                                      "可以切回正式版，也可以输入新的内测码（校验通过即可继续使用内测版）。" & vbCrLf &
                                      "（切回后内测码会从本机移除，需要重新输入才能再进内测版）",
                                      "内测版无法使用", "切回正式版", "输入内测码", "退出", IsWarn:=True)
                End Sub)
            Select Case Choice
                Case 1
                    '切回正式版
                    Return BetaGate.SwitchRelease
                Case 2
                    '输入新的内测码：校验通过就继续用内测版，失败则回退正式版
                    Dim NewCode As String = Nothing
                    RunInUiWait(
                        Sub()
                            NewCode = MyMsgBoxInput("输入内测码",
                                "内测码由服务端管理员按设备码发放。" & vbCrLf &
                                "你的设备码：" & DeviceCode & vbCrLf & vbCrLf &
                                "校验通过后会继续使用内测版。", "", HintText:="内测码", Button1:="校验")
                        End Sub)
                    If NewCode Is Nothing OrElse NewCode.Trim = "" Then
                        '取消输入：回到选择界面
                        Continue Do
                    End If
                    Dim Verified As Boolean = False
                    Dim VerifyErr As String = ""
                    Try
                        Dim R2 = VerifyBetaCode(NewCode.Trim())
                        If R2.Ok Then
                            Verified = True
                        Else
                            VerifyErr = If(R2.Message = "", "内测码无效或已被停用", R2.Message)
                        End If
                    Catch ex As Exception
                        VerifyErr = If(IsNetworkIssue(ex), "连不上服务器，无法校验新内测码。", ex.Message)
                    End Try
                    If Verified Then
                        Settings.Set("BetaCode", NewCode.Trim())
                        Settings.Set("BetaEnabled", True)
                        Logger.Info("内测版：重新输入内测码并校验通过，继续使用内测版")
                        Hint("内测码校验通过，继续使用内测版", HintType.Green)
                        Return BetaGate.Ok
                    End If
                    '新内测码无效：按约定回退正式版
                    Msg = "新内测码校验失败：" & VerifyErr
                    RunInUiWait(Sub() MyMsgBox(Msg & vbCrLf & "启动器将切回正式版。", "内测版", IsWarn:=True))
                    Return BetaGate.SwitchRelease
                Case Else
                    '退出
                    Return BetaGate.Exit
            End Select
        Loop
    End Function

    ''' <summary>清除本机内测状态（切回正式版 / 快照版前调用）。</summary>
    Friend Sub ClearBetaState()
        Settings.Set("BetaEnabled", False)
        Settings.Set("BetaCode", "")
    End Sub

End Module
