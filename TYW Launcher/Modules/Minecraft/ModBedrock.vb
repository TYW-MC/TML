Imports Newtonsoft.Json.Linq

''' <summary>
''' 基岩版（Minecraft for Windows UWP）的版本信息条目。
''' </summary>
Public Class BedrockVersionEntry

    ''' <summary>
    ''' 显示用版本号，如 "1.21.114"、"1.21.100.24"。
    ''' </summary>
    Public Version As String = ""

    ''' <summary>
    ''' 版本类型：Release（正式版）/ Preview（预览版）/ Beta（旧版测试）。
    ''' </summary>
    Public Type As String = ""

    ''' <summary>
    ''' 构建类型：UWP / GDK。仅 UWP 可通过 Add-AppxPackage 安装。
    ''' </summary>
    Public BuildType As String = ""

    ''' <summary>
    ''' 发布日期（ISO 格式字符串），可能为空。
    ''' </summary>
    Public ReleaseDate As String = ""

    ''' <summary>
    ''' 安装包 ID，如 "1.2111401"，可能为空。
    ''' </summary>
    Public Id As String = ""

    ''' <summary>
    ''' 各架构的 Update ID（FE3 用于换取下载直链）与 MD5。
    ''' Key 为架构名（x64/x86/arm），Value 为 (UpdateId, Md5)。
    ''' </summary>
    Public Variations As New Dictionary(Of String, KeyValuePair(Of String, String))

    ''' <summary>
    ''' 获取指定架构的 Update ID，若该架构不可用则返回 Nothing。
    ''' </summary>
    Public Function GetUpdateId(Arch As String) As String
        Dim Var As KeyValuePair(Of String, String) = Nothing
        If Variations.TryGetValue(Arch, Var) AndAlso Not String.IsNullOrEmpty(Var.Key) Then Return Var.Key
        Return Nothing
    End Function

    ''' <summary>
    ''' 获取指定架构的 MD5 校验值，可能返回空字符串。
    ''' </summary>
    Public Function GetMd5(Arch As String) As String
        Dim Var As KeyValuePair(Of String, String) = Nothing
        If Variations.TryGetValue(Arch, Var) Then Return Var.Value
        Return ""
    End Function

End Class

Public Module ModBedrock

#Region "常量"

    ''' <summary>
    ''' mcappx.com 提供的基岩版版本库 JSON 数据源（v2）。
    ''' </summary>
    Public Const BedrockListUrl As String = "https://data.mcappx.com/v2/bedrock.json"

    ''' <summary>
    ''' FE3 secured 端点，用于将 Update ID 换取下载直链。
    ''' </summary>
    Public Const Fe3Secured As String = "https://fe3cr.delivery.mp.microsoft.com/ClientWebService/client.asmx/secured"

    ''' <summary>
    ''' 正式版的包家族名。
    ''' </summary>
    Public Const PfnRetail As String = "Microsoft.MinecraftUWP_8wekyb3d8bbwe"

    ''' <summary>
    ''' 预览版的包家族名。
    ''' </summary>
    Public Const PfnPreview As String = "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe"

    ''' <summary>
    ''' 基岩版实例的目录名，与 Java 版一样以版本号命名，如 "基岩版 1.21.114"。
    ''' </summary>
    Public Function BedrockInstanceName(Entry As BedrockVersionEntry) As String
        Return "基岩版 " & Entry.Version
    End Function

    ''' <summary>
    ''' 基岩版实例的版本文件夹，与 Java 版一样位于 versions\版本名\。
    ''' </summary>
    Public Function BedrockInstanceFolder(Entry As BedrockVersionEntry) As String
        Return PathUtils.AddSlashSuffix(McFolderSelected) & "versions\" & BedrockInstanceName(Entry) & "\"
    End Function

    ''' <summary>
    ''' 基岩版安装包的存放目录，位于对应实例的版本文件夹下。
    ''' </summary>
    Public Function BedrockPackageFolder(Entry As BedrockVersionEntry) As String
        Return BedrockInstanceFolder(Entry) & "bedrock\"
    End Function

#End Region

#Region "版本列表"

    ''' <summary>
    ''' 已加载的版本列表缓存（已按日期降序排列）。
    ''' </summary>
    Public VersionList As List(Of BedrockVersionEntry)

    ''' <summary>
    ''' 版本列表的加载时间，用于缓存。
    ''' </summary>
    Private VersionListTime As New DateTime(0)

    ''' <summary>
    ''' 拉取并解析基岩版版本列表。
    ''' 仅保留 UWP 构建（GDK 的 msixvc 无法通过 Add-AppxPackage 安装）。
    ''' 失败时抛出异常。
    ''' </summary>
    Public Function BedrockVersionListLoad(Optional Force As Boolean = False) As List(Of BedrockVersionEntry)
        If Not Force AndAlso VersionList IsNot Nothing AndAlso (DateTime.Now - VersionListTime).TotalMinutes < 10 Then
            Return VersionList
        End If
        Dim JsonText = NetRequestByClientRetry(BedrockListUrl, RequireJson:=True)
        Dim Json As JObject = JObject.Parse(JsonText)
        Dim Data As JObject = If(Json("From_mcappx.com"), Json)
        Dim Result As New List(Of BedrockVersionEntry)
        For Each Prop As JProperty In Data.Properties()
            Try
                Dim Ver As JObject = Prop.Value
                Dim Entry As New BedrockVersionEntry With {
                    .Version = Prop.Name,
                    .Type = If(Ver("Type")?.ToString(), ""),
                    .BuildType = If(Ver("BuildType")?.ToString(), "UWP"),
                    .ReleaseDate = If(Ver("Date")?.ToString(), ""),
                    .Id = If(Ver("ID")?.ToString(), "")
                }
                Dim Vars As JArray = TryCast(Ver("Variations"), JArray)
                If Vars Is Nothing Then Continue For
                For Each VarToken As JToken In Vars
                    Dim Var As JObject = TryCast(VarToken, JObject)
                    If Var Is Nothing Then Continue For
                    Dim Arch As String = If(Var("Arch")?.ToString(), "")
                    Dim Status As Integer = Val(If(Var("ArchivalStatus")?.ToString(), "0"))
                    Dim Metas As JArray = TryCast(Var("MetaData"), JArray)
                    If Arch = "" OrElse Metas Is Nothing OrElse Status < 2 Then Continue For
                    'UWP 条目的 MetaData 为 Update ID（GUID），GDK 条目为直链 URL
                    Dim UpdateIdOrUrl As String = Nothing
                    For Each Meta As JToken In Metas
                        Dim S As String = Meta?.ToString()
                        If S Is Nothing OrElse S = "" Then Continue For
                        If S.StartsWithF("http") Then
                            UpdateIdOrUrl = S
                            Exit For
                        End If
                        If S.Length = 36 AndAlso S.Count(Function(c) c = "-"c) = 4 Then
                            UpdateIdOrUrl = S
                            Exit For
                        End If
                    Next
                    If UpdateIdOrUrl Is Nothing Then Continue For
                    Dim Md5 As String = If(Var("MD5")?.ToString(), "").ToLower
                    Entry.Variations(Arch) = New KeyValuePair(Of String, String)(UpdateIdOrUrl, Md5)
                Next
                If Entry.Variations.Any Then Result.Add(Entry)
            Catch ex As Exception
                Logger.Error(ex, "解析基岩版版本条目失败（" & Prop.Name & "）")
            End Try
        Next
        Result = SortByReleaseDate(Result)
        VersionList = Result
        VersionListTime = DateTime.Now
        Logger.Info($"[Bedrock] 版本列表加载完成，共 {Result.Count} 个 UWP 版本")
        Return Result
    End Function

    Private Function SortByReleaseDate(List As List(Of BedrockVersionEntry)) As List(Of BedrockVersionEntry)
        Return List.OrderByDescending(Function(e) If(e.ReleaseDate, "")).ToList()
    End Function

    ''' <summary>
    ''' 按渠道筛选版本列表。
    ''' </summary>
    ''' <param name="Preview">True 为预览版/旧测试版，False 为正式版。</param>
    Public Function BedrockVersionFilter(Preview As Boolean, Optional Force As Boolean = False) As List(Of BedrockVersionEntry)
        Dim All = BedrockVersionListLoad(Force)
        If Preview Then
            Return All.Where(Function(e) e.Type = "Preview" OrElse e.Type = "Beta").ToList()
        Else
            Return All.Where(Function(e) e.Type = "Release").ToList()
        End If
    End Function

#End Region

#Region "FE3 直链获取"

    ''' <summary>
    ''' 通过 FE3 GetExtendedUpdateInfo2 将 Update ID 换取下载直链。
    ''' 失败时抛出异常。
    ''' </summary>
    Public Function BedrockGetDownloadUrl(UpdateId As String) As String
        If UpdateId Is Nothing OrElse UpdateId.Length <> 36 Then Throw New ArgumentException("Update ID 格式不正确：" & If(UpdateId, "Nothing"))
        Dim Body As String =
            "<Envelope xmlns=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"" xmlns:u=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"">" &
            "<Header><a:Action mustUnderstand=""1"">http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/GetExtendedUpdateInfo2</a:Action>" &
            $"<a:To mustUnderstand=""1"">{Fe3Secured}</a:To>" &
            "<Security mustUnderstand=""1"" xmlns=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">" &
            "<WindowsUpdateTicketsToken xmlns=""http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization"" u:id=""ClientMSA""></WindowsUpdateTicketsToken></Security></Header>" &
            "<Body><GetExtendedUpdateInfo2 xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"">" &
            $"<updateIDs><UpdateIdentity><UpdateID>{UpdateId}</UpdateID><RevisionNumber>1</RevisionNumber></UpdateIdentity></updateIDs>" &
            "<infoTypes><XmlUpdateFragmentType>FileUrl</XmlUpdateFragmentType><XmlUpdateFragmentType>FileDecryption</XmlUpdateFragmentType></infoTypes>" &
            "<deviceAttributes>FlightRing=RP;</deviceAttributes></GetExtendedUpdateInfo2></Body></Envelope>"
        Dim Result As String = ""
        For Attempt = 1 To 3
            Try
                Result = NetRequestByClient(Fe3Secured, HttpMethod.Post, Body, ContentType:="application/soap+xml; charset=utf-8", Accept:="*/*", Timeout:=45000, MakeLog:=False)
                Exit For
            Catch ex As Exception
                If Attempt = 3 Then Throw New WebException("获取基岩版下载直链失败（Update ID：" & UpdateId & "）", ex)
                Thread.Sleep(500)
            End Try
        Next
        '从 FileLocation 中选取包直链（URL 最长的是完整包，较短的是 BlockMap）
        Dim Urls As New List(Of String)
        Dim Index As Integer = 0
        While True
            Dim StartPos = Result.IndexOf("<Url>", Index)
            If StartPos < 0 Then Exit While
            Dim EndPos = Result.IndexOf("</Url>", StartPos)
            If EndPos < 0 Then Exit While
            Urls.Add(Result.Substring(StartPos + 5, EndPos - StartPos - 5).Replace("&amp;", "&"))
            Index = EndPos + 6
        End While
        If Not Urls.Any Then Throw New WebException("该版本的 Update ID 已失效，无法获取下载直链（Update ID：" & UpdateId & "）")
        Return Urls.OrderByDescending(Function(u) u.Length).First()
    End Function

#End Region

#Region "下载与安装"

    ''' <summary>
    ''' 当前系统对应的最佳架构。
    ''' </summary>
    Public Function BedrockBestArch() As String
        If Environment.Is64BitOperatingSystem Then Return "x64"
        'ARM64 系统也可能运行于模拟，保守返回 x86（x86 包在 x86/x64/ARM 上均可通过模拟安装运行）
        Return "x86"
    End Function

    ''' <summary>
    ''' 下载指定版本的基岩版安装包到本地并返回文件路径。
    ''' UWP 版为 .appx（Update ID 换直链），GDK 版为 .msixvc（mcappx 直链）。
    ''' 必须在工作线程调用。
    ''' </summary>
    Public Function BedrockDownload(Entry As BedrockVersionEntry, Optional Arch As String = Nothing, Optional LoaderToSyncProgress As LoaderBase = Nothing) As String
        If Arch Is Nothing Then Arch = BedrockBestArch()
        Dim UpdateIdOrUrl = Entry.GetUpdateId(Arch)
        If UpdateIdOrUrl Is Nothing Then Throw New Exception($"该版本不提供 {Arch} 架构的安装包")
        Dim IsGdk As Boolean = UpdateIdOrUrl.StartsWithF("http")
        Dim Url As String = If(IsGdk, UpdateIdOrUrl, BedrockGetDownloadUrl(UpdateIdOrUrl))
        Directory.CreateDirectory(BedrockPackageFolder(Entry))
        Dim FileName As String = $"Minecraft-{Entry.Version}-{Arch}.{If(IsGdk, "msixvc", "appx")}"
        Dim LocalPath As String = BedrockPackageFolder(Entry) & FileName
        Logger.Info($"[Bedrock] 开始下载基岩版 {Entry.Version}（{Arch}，{If(IsGdk, "GDK", "UWP")}）：{Url}")
        '通过 FileChecker 让下载引擎自动进行 MD5 校验
        Dim Expected = Entry.GetMd5(Arch)
        Dim Checker As FileChecker = If(Expected = "", New FileChecker, New FileChecker With {.Hash = Expected})
        NetDownloadByLoader({Url}, LocalPath, LoaderToSyncProgress, Checker)
        If Not File.Exists(LocalPath) Then Throw New WebException("基岩版安装包下载失败：文件未生成")
        Return LocalPath
    End Function

    ''' <summary>
    ''' 使用 PowerShell 的 Add-AppxPackage 安装基岩版。
    ''' 带 -ForceUpdateFromAnyVersion，允许覆盖同版本重装与降级（否则会报 0x80073CFB）。
    ''' 必须在工作线程调用，可能耗时较长。
    ''' </summary>
    Public Sub BedrockInstallAppx(Path As String)
        If Not File.Exists(Path) Then Throw New FileNotFoundException("安装包不存在", Path)
        Logger.Info($"[Bedrock] 开始安装：{Path}")
        '先让 PowerShell 把控制台输出切到 UTF-8，避免中文错误信息变成乱码
        Dim Command As String =
            "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false); " &
            $"Add-AppxPackage -Path '{Path}' -ForceUpdateFromAnyVersion; " &
            "exit $LASTEXITCODE"
        Dim Psi As New ProcessStartInfo With {
            .FileName = "powershell.exe",
            .Arguments = $"-NoProfile -NonInteractive -Command ""{Command}""",
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .StandardOutputEncoding = Encoding.UTF8,
            .StandardErrorEncoding = Encoding.UTF8
        }
        Using P = StartProcess(Psi)
            Dim OutputTask = P.StandardOutput.ReadToEndAsync()
            Dim ErrorTask = P.StandardError.ReadToEndAsync()
            If Not P.WaitForExit(300000) Then '最长等待 5 分钟
                Try : P.Kill() : Catch : End Try
                Throw New TimeoutException("基岩版安装超时（5 分钟）")
            End If
            Dim Output As String = OutputTask.Result & vbCrLf & ErrorTask.Result
            If P.ExitCode <> 0 Then
                Throw New Exception("基岩版安装失败：" & Output.Trim())
            End If
            Logger.Info("[Bedrock] 安装完成" & If(Output.Trim() = "", "", "：" & Output.Trim()))
        End Using
    End Sub

#End Region

#Region "公共下载安装流程"

    ''' <summary>正在下载安装的版本号（跨页面共享防重复），Nothing 表示空闲。</summary>
    Public InstallingBedrockVersion As String = Nothing

    ''' <summary>
    ''' 下载并安装指定基岩版版本。必须在工作线程调用。
    ''' 下载与安装会作为任务出现在右下角的下载列表中。
    ''' 防重复：若有任务进行中则提示并返回 False。
    ''' </summary>
    Public Function BedrockDownloadAndInstall(Entry As BedrockVersionEntry) As Boolean
        If InstallingBedrockVersion IsNot Nothing Then
            Hint($"正在下载安装基岩版 {InstallingBedrockVersion}，请稍候……", HintType.Blue)
            Return False
        End If
        InstallingBedrockVersion = Entry.Version
        Try
            Dim Arch = BedrockBestArch()
            If Entry.GetUpdateId(Arch) Is Nothing Then Arch = Entry.Variations.Keys.First()
            Dim IsGdk As Boolean = Entry.BuildType = "GDK"
            Dim UpdateIdOrUrl As String = Entry.GetUpdateId(Arch)
            Dim IsUrl As Boolean = UpdateIdOrUrl.StartsWithF("http")
            Hint($"正在获取基岩版 {Entry.Version} 的下载直链……", HintType.Blue)
            Dim Url As String = If(IsUrl, UpdateIdOrUrl, BedrockGetDownloadUrl(UpdateIdOrUrl))
            Directory.CreateDirectory(BedrockPackageFolder(Entry))
            Dim LocalPath As String = BedrockPackageFolder(Entry) & $"Minecraft-{Entry.Version}-{Arch}.{If(IsGdk, "msixvc", "appx")}"
            Dim Expected As String = Entry.GetMd5(Arch)
            Dim Checker As FileChecker = If(Expected = "", New FileChecker, New FileChecker With {.Hash = Expected})

            '构建任务：下载 + 安装，加入任务栏后右下角会显示进度
            Dim SubLoaders As New List(Of LoaderBase) From {
                New LoaderDownload("下载基岩版安装包 " & Entry.Version,
                                   New List(Of NetFile) From {New NetFile({Url}, LocalPath, Checker)}) With {.ProgressWeight = 20}
            }
            If Not IsGdk Then
                Dim InstallWorker As New RedoableWorker(
                    Sub(c, p)
                        p.Set(0.2)
                        BedrockInstallAppx(LocalPath)
                        p.Set(0.95)
                    End Sub)
                SubLoaders.Add(New LoaderWorker("安装基岩版 " & Entry.Version, InstallWorker) With {.ProgressWeight = 5})
            End If
            Dim Loader As New LoaderCombo(Of String)("安装基岩版 " & Entry.Version, SubLoaders)
            Loader.Start(Entry.Version)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            Loader.WaitForExit()

            '写入版本记录：与 Java 版一样放在 versions\版本名\ 下
            BedrockInstanceWrite(Entry, Arch, LocalPath)

            If IsGdk Then
                'GDK 版是 MSIXVC 包：系统组件装不了，但运行依赖（Gaming Services）可以由启动器自动补齐
                Hint($"GDK 版 {Entry.Version} 下载完成，正在检查运行依赖……", HintType.Green)
                Dim DepReady As Boolean = BedrockEnsureGamingServices()
                RunInUiWait(Sub()
                                Dim DepText As String = If(DepReady, "运行所需的 Gaming Services 依赖已就绪。", "Gaming Services 依赖尚未装好，请先在微软商店安装。")
                                If MyMsgBox($"GDK 版本 {Entry.Version} 已下载完成，并已添加到版本列表。" & vbCrLf & vbCrLf & DepText & vbCrLf &
                                            "MSIXVC 安装包无法由系统组件直接安装，需要用 GDK 工具（wdapp install）或开源工具 LeviLauncher 部署（需要微软账户拥有基岩版权限）。" & vbCrLf & vbCrLf &
                                            "是否打开安装包所在文件夹？", "GDK 版本下载完成", "打开文件夹", "完成") = 1 Then
                                    StartProcess("explorer.exe", $"/select,""{LocalPath}""")
                                End If
                            End Sub)
                Return False
            End If

            Hint($"基岩版 {Entry.Version} 安装完成！已添加到版本列表，可在启动页选择并启动。", HintType.Green)
            Return True
        Catch ex As Exception
            Logger.Error(ex, "下载或安装基岩版失败")
            Hint("下载或安装失败：" & ex.Message, HintType.Red)
            Return False
        Finally
            InstallingBedrockVersion = Nothing
        End Try
    End Function

#End Region

#Region "版本目录"

    ''' <summary>
    ''' 将基岩版的日期文本转换为 ISO 时间，失败时返回当前时间。
    ''' </summary>
    Private Function BedrockIsoTime(Raw As String) As String
        Dim Time As Date
        If Date.TryParse(Raw, Time) Then Return Time.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'")
        Return Date.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'")
    End Function

    ''' <summary>
    ''' 在 versions\版本名\ 下创建与 Java 版结构一致的版本记录，使基岩版出现在版本列表中。
    ''' 该记录只是一个标记，游戏本体由系统 Appx 提供。必须在工作线程调用。
    ''' </summary>
    Public Sub BedrockInstanceWrite(Entry As BedrockVersionEntry, Arch As String, PkgPath As String)
        Dim Folder As String = BedrockInstanceFolder(Entry)
        Directory.CreateDirectory(Folder)
        Directory.CreateDirectory(Folder & "TYW Launcher\")
        Dim PkgRelative As String = ""
        If PkgPath IsNot Nothing AndAlso PkgPath.StartsWithF(Folder) Then PkgRelative = PkgPath.Substring(Folder.Length).Replace("/"c, "\"c)
        Dim Json As New JObject From {
            {"id", BedrockInstanceName(Entry)},
            {"type", "release"},
            {"mainClass", "net.minecraft.client.main.Main"},
            {"minimumLauncherVersion", 21},
            {"releaseTime", BedrockIsoTime(Entry.ReleaseDate)},
            {"time", BedrockIsoTime(Entry.ReleaseDate)},
            {"assets", "legacy"},
            {"assetIndex", New JObject From {{"id", "legacy"}, {"url", ""}, {"sha1", ""}, {"size", 0}, {"totalSize", 0}}},
            {"downloads", New JObject},
            {"libraries", New JArray},
            {"arguments", New JObject From {{"game", New JArray}, {"jvm", New JArray}}},
            {"bedrock", True},
            {"bedrockChannel", If(Entry.Type = "Release", "retail", "preview")},
            {"bedrockVersion", Entry.Version},
            {"bedrockBuildType", If(Entry.BuildType = "", "UWP", Entry.BuildType)},
            {"bedrockArch", Arch},
            {"bedrockPackage", PkgRelative}
        }
        Dim JsonPath As String = Folder & BedrockInstanceName(Entry) & ".json"
        Try
            File.WriteAllText(JsonPath, Json.ToString(), Encoding.UTF8)
        Catch ex As Exception
            Throw New Exception($"写入基岩版版本记录失败（{JsonPath}）", ex)
        End Try
        Logger.Info($"[Bedrock] 已写入基岩版版本记录：{JsonPath}")
    End Sub

#End Region

#Region "已安装检测与启动"

    ''' <summary>
    ''' 比较两个 "." 分隔的版本号大小。
    ''' 返回正数表示 A 更新，负数表示 B 更新，0 表示相同。
    ''' </summary>
    Public Function BedrockVersionCompare(A As String, B As String) As Integer
        If A = B Then Return 0
        Dim Pa = A.Split("."c), Pb = B.Split("."c)
        For i As Integer = 0 To Math.Max(Pa.Length, Pb.Length) - 1
            Dim Va As Long = If(i < Pa.Length, Val(Pa(i)), 0)
            Dim Vb As Long = If(i < Pb.Length, Val(Pb(i)), 0)
            If Va <> Vb Then Return If(Va > Vb, 1, -1)
        Next
        Return 0
    End Function

    ''' <summary>
    ''' 查询已安装的基岩版版本号，未安装则返回 Nothing。
    ''' 必须在工作线程调用。
    ''' </summary>
    Public Function BedrockGetInstalled(Preview As Boolean) As String
        Dim PfnName As String = If(Preview, PfnPreview, PfnRetail).Split("_"c)(0)
        Dim Psi As New ProcessStartInfo With {
            .FileName = "powershell.exe",
            .Arguments = $"-NoProfile -NonInteractive -Command ""(Get-AppxPackage -Name '{PfnName}').Version""",
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .StandardOutputEncoding = Encoding.UTF8
        }
        Try
            Using P = StartProcess(Psi)
                If Not P.WaitForExit(15000) Then
                    Try : P.Kill() : Catch : End Try
                    Return Nothing
                End If
                Dim Result As String = P.StandardOutput.ReadToEnd().Trim()
                If Result = "" OrElse Not Result.Contains("."c) Then Return Nothing
                Return Result
            End Using
        Catch ex As Exception
            Logger.Error(ex, "查询已安装基岩版失败")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 启动基岩版。
    ''' </summary>
    Public Sub BedrockLaunch(Preview As Boolean)
        Dim Pfn As String = If(Preview, PfnPreview, PfnRetail)
        Logger.Info("[Bedrock] 启动基岩版（" & If(Preview, "预览版", "正式版") & "）")
        StartProcess("explorer.exe", $"shell:AppsFolder\{Pfn}!App")
    End Sub

#End Region

#Region "资源包与行为包"

    ''' <summary>
    ''' 基岩版的一个资源包 / 行为包条目。
    ''' </summary>
    Public Class BedrockPackInfo
        Public Name As String = ""
        Public Description As String = ""
        Public Version As String = ""
        Public Uuid As String = ""
        Public Size As Long = 0
        ''' <summary>包所在路径（文件夹，或未解压的 .mcpack 文件）。</summary>
        Public Path As String = ""
        Public IsBehavior As Boolean = False
        Public IsFile As Boolean = False
    End Class

    ''' <summary>
    ''' 基岩版游戏的 com.mojang 目录。
    ''' UWP 版位于 %LOCALAPPDATA%\Packages\包家族名\LocalState\games\com.mojang，
    ''' GDK 版位于 %APPDATA%\Minecraft Bedrock\Users\Shared\games\com.mojang。
    ''' 优先返回实际存在的那个；都不存在时返回该渠道的默认位置。
    ''' </summary>
    Public Function BedrockMojangFolder(Preview As Boolean, BuildType As String) As String
        Dim UwpPath As String = PathUtils.AddSlashSuffix(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) &
                                "Packages\" & If(Preview, PfnPreview, PfnRetail) & "\LocalState\games\com.mojang\"
        Dim GdkPath As String = PathUtils.AddSlashSuffix(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)) &
                                "Minecraft Bedrock\Users\Shared\games\com.mojang\"
        If BuildType = "GDK" Then
            If DirectoryUtils.Exists(GdkPath) Then Return GdkPath
        Else
            If DirectoryUtils.Exists(UwpPath) Then Return UwpPath
        End If
        If DirectoryUtils.Exists(GdkPath) Then Return GdkPath
        Return UwpPath
    End Function

    ''' <summary>
    ''' 资源包 / 行为包的存放目录。
    ''' </summary>
    Public Function BedrockPackFolder(Preview As Boolean, BuildType As String, IsBehavior As Boolean) As String
        Return BedrockMojangFolder(Preview, BuildType) & If(IsBehavior, "behavior_packs\", "resource_packs\")
    End Function

    Private Function ReadPackManifest(Folder As String) As JObject
        Dim ManifestPath As String = PathUtils.AddSlashSuffix(Folder) & "manifest.json"
        If Not FileUtils.Exists(ManifestPath) Then Return Nothing
        Try
            Return JObject.Parse(IO.File.ReadAllText(ManifestPath, Encoding.UTF8))
        Catch ex As Exception
            Logger.Warn(ex, "读取基岩版包清单失败：" & ManifestPath)
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 解析 manifest.json 中的显示名、版本、描述，并根据模块类型判断是资源包还是行为包。
    ''' </summary>
    Private Function ParsePackManifest(Folder As String, ByRef IsBehavior As Boolean) As BedrockPackInfo
        Dim Info As New BedrockPackInfo With {.Path = Folder}
        Dim Json = ReadPackManifest(Folder)
        If Json Is Nothing Then
            Info.Name = Path.GetFileName(Folder.TrimEnd("\"c))
            Info.Description = "缺少 manifest.json，可能不是有效的基岩版包"
            Return Info
        End If
        Dim Header = TryCast(Json("header"), JObject)
        If Header IsNot Nothing Then
            Info.Name = If(Header("name")?.ToString(), "")
            Info.Description = If(Header("description")?.ToString(), "")
            Info.Uuid = If(Header("uuid")?.ToString(), "")
            Dim Ver = TryCast(Header("version"), JArray)
            If Ver IsNot Nothing Then Info.Version = String.Join(".", Ver.Select(Function(v) v.ToString()))
        End If
        If String.IsNullOrEmpty(Info.Name) Then Info.Name = Path.GetFileName(Folder.TrimEnd("\"c))
        '根据模块类型判断是资源包还是行为包
        Dim Modules = TryCast(Json("modules"), JArray)
        If Modules IsNot Nothing Then
            For Each ModToken In Modules
                Dim ModType As String = If(ModToken("type")?.ToString(), "").ToLower
                If ModType.Contains("resource") Then
                    IsBehavior = False
                    Exit For
                ElseIf ModType.Contains("data") OrElse ModType.Contains("behavior") Then
                    IsBehavior = True
                    Exit For
                End If
            Next
        End If
        Return Info
    End Function

    ''' <summary>
    ''' 列出已安装的资源包或行为包。
    ''' </summary>
    Public Function BedrockPackList(Preview As Boolean, BuildType As String, IsBehavior As Boolean) As List(Of BedrockPackInfo)
        Dim Result As New List(Of BedrockPackInfo)
        Dim Folder As String = BedrockPackFolder(Preview, BuildType, IsBehavior)
        If Not DirectoryUtils.Exists(Folder) Then Return Result
        '已解压的包文件夹
        For Each SubDir In Directory.GetDirectories(Folder)
            Try
                Dim PackIsBehavior As Boolean = IsBehavior
                Dim Info = ParsePackManifest(SubDir, PackIsBehavior)
                Info.IsBehavior = IsBehavior
                Info.Size = BedrockFolderSize(SubDir)
                Result.Add(Info)
            Catch ex As Exception
                Logger.Warn(ex, "读取基岩版包失败：" & SubDir)
            End Try
        Next
        '未解压、直接塞进目录的 .mcpack
        For Each PkgFile In Directory.GetFiles(Folder, "*.mcpack")
            Result.Add(New BedrockPackInfo With {
                .Name = Path.GetFileName(PkgFile).Replace(".mcpack", ""),
                .Description = "未解压的 .mcpack 文件",
                .Path = PkgFile, .IsFile = True, .IsBehavior = IsBehavior,
                .Size = New FileInfo(PkgFile).Length})
        Next
        Return Result.OrderBy(Function(pk) pk.Name).ToList()
    End Function

    Private Function BedrockFolderSize(Folder As String) As Long
        Try
            Return Directory.GetFiles(Folder, "*", SearchOption.AllDirectories).Sum(Function(f) New FileInfo(f).Length)
        Catch
            Return 0
        End Try
    End Function

    Private Sub BedrockCopyFolder(Source As String, Dest As String)
        Directory.CreateDirectory(Dest)
        For Each SourceFile In Directory.GetFiles(Source)
            IO.File.Copy(SourceFile, PathUtils.AddSlashSuffix(Dest) & Path.GetFileName(SourceFile), True)
        Next
        For Each SubDir In Directory.GetDirectories(Source)
            BedrockCopyFolder(SubDir, PathUtils.AddSlashSuffix(Dest) & Path.GetFileName(SubDir.TrimEnd("\"c)))
        Next
    End Sub

    ''' <summary>
    ''' 递归找出所有含 manifest.json 的目录（最多向下两层），即一个独立的包。
    ''' </summary>
    Private Sub FindPackRoots(Folder As String, Roots As List(Of String), Optional Depth As Integer = 0)
        If FileUtils.Exists(PathUtils.AddSlashSuffix(Folder) & "manifest.json") Then
            Roots.Add(Folder)
            Return
        End If
        If Depth >= 2 Then Return
        For Each SubDir In Directory.GetDirectories(Folder)
            FindPackRoots(SubDir, Roots, Depth + 1)
        Next
    End Sub

    ''' <summary>
    ''' 导入 .mcpack / .mcaddon / .zip。自动按 manifest 判断放到资源包还是行为包目录。
    ''' 必须在工作线程调用。返回 (成功数, 失败数)。
    ''' </summary>
    Public Function BedrockPackImport(Files As IEnumerable(Of String), Preview As Boolean, BuildType As String) As (Imported As Integer, Failed As Integer)
        Dim Imported As Integer = 0, Failed As Integer = 0
        Dim Temp As String = PathTemp & "BedrockPackTemp\"
        For Each PkgPath In Files
            Try
                If Directory.Exists(Temp) Then Directory.Delete(Temp, True)
                Directory.CreateDirectory(Temp)
                FileUtils.ExtractToDirectory(PkgPath, Temp)
                Dim Roots As New List(Of String)
                FindPackRoots(Temp.TrimEnd("\"c), Roots)
                If Not Roots.Any Then Throw New Exception("压缩包内没有找到 manifest.json，不是有效的基岩版资源包 / 行为包")
                For Each Root In Roots
                    Dim PackIsBehavior As Boolean = False
                    Dim Info = ParsePackManifest(Root, PackIsBehavior)
                    Dim TargetDir As String = BedrockPackFolder(Preview, BuildType, PackIsBehavior)
                    Directory.CreateDirectory(TargetDir)
                    '目标文件夹名用清单里的名字，重名时追加序号
                    Dim BaseName As String = Info.Name
                    For Each InvalidChar As Char In Path.GetInvalidFileNameChars()
                        BaseName = BaseName.Replace(InvalidChar, "_"c)
                    Next
                    If BaseName.Trim = "" Then BaseName = Path.GetFileName(Root.TrimEnd("\"c))
                    Dim Target As String = PathUtils.AddSlashSuffix(TargetDir) & BaseName
                    Dim Index As Integer = 2
                    While DirectoryUtils.Exists(Target)
                        Target = PathUtils.AddSlashSuffix(TargetDir) & BaseName & "_" & Index
                        Index += 1
                    End While
                    BedrockCopyFolder(Root, Target)
                    Imported += 1
                    Logger.Info($"[Bedrock] 已导入{If(PackIsBehavior, "行为包", "资源包")}：{Info.Name} → {Target}")
                Next
            Catch ex As Exception
                Failed += 1
                Logger.Error(ex, "导入基岩版包失败：" & PkgPath)
            Finally
                Try
                    If Directory.Exists(Temp) Then Directory.Delete(Temp, True)
                Catch
                End Try
            End Try
        Next
        Return (Imported, Failed)
    End Function

    ''' <summary>
    ''' 删除一个已安装的包。
    ''' </summary>
    Public Sub BedrockPackDelete(Info As BedrockPackInfo)
        If Info.IsFile Then
            IO.File.Delete(Info.Path)
        Else
            Directory.Delete(Info.Path, True)
        End If
    End Sub

#End Region

#Region "GDK 依赖"

    ''' <summary>
    ''' 查询已安装的 Gaming Services 版本，未安装返回空字符串。必须在工作线程调用。
    ''' </summary>
    Public Function BedrockGamingServicesVersion() As String
        Try
            Return RunPowerShellRead("(Get-AppxPackage -Name 'Microsoft.GamingServices').Version")
        Catch ex As Exception
            Logger.Warn(ex, "查询 Gaming Services 失败")
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' 查询已安装的 GDK 版 Minecraft 包家族名，未安装返回空字符串。必须在工作线程调用。
    ''' </summary>
    Public Function BedrockGdkPackageName() As String
        Try
            Dim Names = RunPowerShellRead("(Get-AppxPackage -Name '*Minecraft*' | Where-Object { $_.PackageFullName -like '*Gdk*' -or $_.Name -like '*Gdk*' } | Select-Object -First 1).Name")
            Return If(Names Is Nothing, "", Names.Trim())
        Catch ex As Exception
            Logger.Warn(ex, "查询 GDK 版基岩版失败")
            Return ""
        End Try
    End Function

    Private Function RunPowerShellRead(Command As String) As String
        Dim Psi As New ProcessStartInfo With {
            .FileName = "powershell.exe",
            .Arguments = $"-NoProfile -NonInteractive -Command ""{Command}""",
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .StandardOutputEncoding = Encoding.UTF8
        }
        Using P = StartProcess(Psi)
            If Not P.WaitForExit(15000) Then
                Try : P.Kill() : Catch : End Try
                Return ""
            End If
            Return P.StandardOutput.ReadToEnd().Trim()
        End Using
    End Function

    ''' <summary>
    ''' 确保 Gaming Services 已安装：缺失时用 winget 从微软商店安装，失败则打开商店页面。
    ''' 必须在工作线程调用。返回是否已就绪。
    ''' </summary>
    Public Function BedrockEnsureGamingServices() As Boolean
        If BedrockGamingServicesVersion() <> "" Then Return True
        Hint("正在安装 GDK 版所需的 Gaming Services 依赖……", HintType.Blue)
        Logger.Info("[Bedrock] 未找到 Gaming Services，尝试通过 winget 安装")
        Try
            Dim Psi As New ProcessStartInfo With {
                .FileName = "winget",
                .Arguments = "install --id 9MWPM2CQNLHN --source msstore --accept-package-agreements --accept-source-agreements --disable-interactivity",
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8
            }
            Using P = StartProcess(Psi)
                P.StandardOutput.ReadToEnd() '不等待输出内容，仅避免管道阻塞
                P.WaitForExit(600000)
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "通过 winget 安装 Gaming Services 失败")
        End Try
        If BedrockGamingServicesVersion() <> "" Then
            Hint("Gaming Services 依赖安装完成", HintType.Green)
            Return True
        End If
        Hint("Gaming Services 未能自动安装，正在打开微软商店页面……", HintType.Red)
        StartProcess("explorer.exe", "ms-windows-store://pdp/?ProductId=9MWPM2CQNLHN")
        Return False
    End Function

#End Region

#Region "账号登录组件"

    ''' <summary>
    ''' 基岩版的微软账户登录由游戏自身通过 Xbox 身份系统完成，启动器无法代替它登录。
    ''' 这里只检查登录所依赖的系统组件是否齐全，返回缺失组件的中文名称（空列表表示齐全）。
    ''' 通过 AppModel 仓库的注册表项判断，避免每次启动都拉起 PowerShell。
    ''' </summary>
    Public Function BedrockMissingSignInComponents() As List(Of String)
        Dim Missing As New List(Of String)
        If Not IsAppxFamilyRegistered("Microsoft.XboxIdentityProvider_8wekyb3d8bbwe") Then
            Missing.Add("Xbox Identity Provider（Xbox 身份提供程序）")
        End If
        If Not IsAppxFamilyRegistered("Microsoft.GamingServices_8wekyb3d8bbwe") Then
            Missing.Add("Gaming Services（Xbox 游戏服务）")
        End If
        Return Missing
    End Function

    ''' <summary>该应用包家族是否为当前用户注册过（等价于 Get-AppxPackage 能查到，但不启动进程）。</summary>
    Private Function IsAppxFamilyRegistered(Family As String) As Boolean
        Try
            Using Key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    "Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Families\" & Family)
                Return Key IsNot Nothing
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "查询应用包家族失败：" & Family)
            Return True '查询失败时不误报缺失
        End Try
    End Function

    ''' <summary>
    ''' 安装基岩版登录所需组件：Gaming Services 走 winget，Xbox Identity Provider 缺失时打开商店搜索页。
    ''' 必须在工作线程调用。返回是否全部就绪。
    ''' </summary>
    Public Function BedrockInstallSignInComponents() As Boolean
        Dim Ready As Boolean = True
        If BedrockSignInComponentVersion("Microsoft.GamingServices") = "" Then
            Ready = BedrockEnsureGamingServices() AndAlso Ready
        End If
        If BedrockSignInComponentVersion("Microsoft.XboxIdentityProvider") = "" Then
            Hint("正在打开微软商店的 Xbox Identity Provider 页面……", HintType.Blue)
            StartProcess("explorer.exe", "ms-windows-store://search/?query=Xbox%20Identity%20Provider")
            Ready = False
        End If
        Return Ready
    End Function

    Private Function BedrockSignInComponentVersion(Name As String) As String
        Try
            Return RunPowerShellRead($"(Get-AppxPackage -Name '{Name}').Version")
        Catch ex As Exception
            Logger.Warn(ex, "查询组件失败：" & Name)
            Return "unknown" '查询失败时不误判为缺失
        End Try
    End Function

#End Region

End Module
