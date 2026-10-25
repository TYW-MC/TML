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
    ''' 基岩版下载目录。
    ''' </summary>
    Public ReadOnly Property BedrockDownloadPath As String
        Get
            Return Paths.AppDataThenName & "Bedrock\"
        End Get
    End Property

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
                If Entry.BuildType <> "UWP" Then Continue For 'GDK 包无法用 Add-AppxPackage 安装
                Dim Vars As JArray = TryCast(Ver("Variations"), JArray)
                If Vars Is Nothing Then Continue For
                For Each VarToken As JToken In Vars
                    Dim Var As JObject = TryCast(VarToken, JObject)
                    If Var Is Nothing Then Continue For
                    Dim Arch As String = If(Var("Arch")?.ToString(), "")
                    Dim Status As Integer = Val(If(Var("ArchivalStatus")?.ToString(), "0"))
                    Dim Metas As JArray = TryCast(Var("MetaData"), JArray)
                    If Arch = "" OrElse Metas Is Nothing OrElse Status < 2 Then Continue For
                    'MetaData 中 UWP 类型为 Update ID（GUID），跳过可能的 URL
                    Dim UpdateId As String = Nothing
                    For Each Meta As JToken In Metas
                        Dim S As String = Meta?.ToString()
                        If S.StartsWithF("http") Then Continue For
                        If S.Length = 36 AndAlso S.Count(Function(c) c = "-"c) = 4 Then
                            UpdateId = S
                            Exit For
                        End If
                    Next
                    If UpdateId Is Nothing Then Continue For
                    Dim Md5 As String = If(Var("MD5")?.ToString(), "").ToLower
                    Entry.Variations(Arch) = New KeyValuePair(Of String, String)(UpdateId, Md5)
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
    ''' 必须在工作线程调用。
    ''' </summary>
    Public Function BedrockDownload(Entry As BedrockVersionEntry, Optional Arch As String = Nothing, Optional LoaderToSyncProgress As LoaderBase = Nothing) As String
        If Arch Is Nothing Then Arch = BedrockBestArch()
        Dim UpdateId = Entry.GetUpdateId(Arch)
        If UpdateId Is Nothing Then Throw New Exception($"该版本不提供 {Arch} 架构的安装包")
        Dim Url = BedrockGetDownloadUrl(UpdateId)
        Directory.CreateDirectory(BedrockDownloadPath)
        Dim FileName As String = $"Minecraft-{Entry.Version}-{Arch}.appx"
        Dim LocalPath As String = BedrockDownloadPath & FileName
        Logger.Info($"[Bedrock] 开始下载基岩版 {Entry.Version}（{Arch}）：{Url}")
        '通过 FileChecker 让下载引擎自动进行 MD5 校验
        Dim Expected = Entry.GetMd5(Arch)
        Dim Checker As FileChecker = If(Expected = "", New FileChecker, New FileChecker With {.Hash = Expected})
        NetDownloadByLoader({Url}, LocalPath, LoaderToSyncProgress, Checker)
        If Not File.Exists(LocalPath) Then Throw New WebException("基岩版安装包下载失败：文件未生成")
        Return LocalPath
    End Function

    ''' <summary>
    ''' 使用 PowerShell 的 Add-AppxPackage 安装基岩版。
    ''' 必须在工作线程调用，可能耗时较长。
    ''' </summary>
    Public Sub BedrockInstallAppx(Path As String)
        If Not File.Exists(Path) Then Throw New FileNotFoundException("安装包不存在", Path)
        Logger.Info($"[Bedrock] 开始安装：{Path}")
        Dim Psi As New ProcessStartInfo With {
            .FileName = "powershell.exe",
            .Arguments = $"-NoProfile -NonInteractive -Command ""Add-AppxPackage -Path '{Path}'""",
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

#Region "已安装检测与启动"

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

End Module
