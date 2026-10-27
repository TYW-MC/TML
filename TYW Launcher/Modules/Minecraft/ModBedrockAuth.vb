' 基岩版微软账户登录：通过 Windows 的 WAM（Web 账户管理器）拿到 MSA 令牌，
' 再换 Xbox Live 用户令牌与 XSTS 令牌，得到玩家的 Gamertag / XUID。
' 基岩版游戏本身的登录由游戏通过同一套 Windows 账户完成，这里登录的账户会和游戏共用。

Imports System.IO
Imports System.Net
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json.Linq
Imports Windows.Foundation
Imports Windows.Security.Authentication.Web.Core
Imports Windows.Security.Credentials

Public Module ModBedrockAuth

#Region "账号数据"

    ''' <summary>已登录的基岩版（Xbox）账户信息。</summary>
    Public Class BedrockAccount
        Public Gamertag As String = ""
        Public Xuid As String = ""
        Public Uhs As String = ""
        Public Xsts As String = ""
        Public AccountId As String = ""
        Public ExpiresUtc As Date

        Public ReadOnly Property IsExpired As Boolean
            Get
                Return ExpiresUtc <= Date.UtcNow.AddMinutes(5)
            End Get
        End Property

        Public ReadOnly Property Display As String
            Get
                Return If(AccountId = "", Gamertag, Gamertag & "（" & AccountId & "）")
            End Get
        End Property
    End Class

    ''' <summary>读取已保存的账户（只读缓存，不会联网刷新）。没有登录过时返回 Nothing。</summary>
    Public Function GetCachedAccount() As BedrockAccount
        Dim Gt = Settings.Get(Of String)("BedrockGamertag")
        If String.IsNullOrEmpty(Gt) Then Return Nothing
        Return New BedrockAccount With {
            .Gamertag = Gt,
            .Xuid = Settings.Get(Of String)("BedrockXuid"),
            .AccountId = Settings.Get(Of String)("BedrockAccountEmail"),
            .Xsts = Settings.Get(Of String)("BedrockXsts"),
            .Uhs = Settings.Get(Of String)("BedrockXstsUhs"),
            .ExpiresUtc = Date.FromBinary(Settings.Get(Of Long)("BedrockXstsExpires"))}
    End Function

    Public Sub ClearAccount()
        Settings.Set("BedrockGamertag", "")
        Settings.Set("BedrockXuid", "")
        Settings.Set("BedrockAccountEmail", "")
        Settings.Set("BedrockXsts", "")
        Settings.Set("BedrockXstsUhs", "")
        Settings.Set("BedrockXstsExpires", 0L)
    End Sub

    Private Sub SaveAccount(Account As BedrockAccount)
        Settings.Set("BedrockGamertag", Account.Gamertag)
        Settings.Set("BedrockXuid", Account.Xuid)
        Settings.Set("BedrockAccountEmail", Account.AccountId)
        Settings.Set("BedrockXsts", Account.Xsts)
        Settings.Set("BedrockXstsUhs", Account.Uhs)
        Settings.Set("BedrockXstsExpires", Account.ExpiresUtc.ToBinary())
    End Sub

#End Region

#Region "WAM 原生调用"

    Private Const WamClassName As String = "Windows.Security.Authentication.Web.Core.WebAuthenticationCoreManager"
    Private Const IidWamInterop As String = "f4b8e804-811e-4436-b69c-44cb67b72084"
    Private Const IidTokenResultOp As String = "0a815852-7c44-5674-b3d2-fa2e4c1e46c9"
    Private Const MsaProviderName As String = "https://login.microsoft.com"
    Private Const MsaAuthority As String = "consumers"
    Private Const XblScope As String = "service::user.auth.xboxlive.com::MBI_SSL"
    Private Const XboxClientId As String = "00000000402b5328"

    <DllImport("combase.dll", CharSet:=CharSet.Unicode)>
    Private Function RoInitialize(initType As Integer) As Integer
    End Function

    <DllImport("combase.dll", CharSet:=CharSet.Unicode)>
    Private Function WindowsCreateString(src As String, length As UInteger, ByRef hstr As IntPtr) As Integer
    End Function

    <DllImport("combase.dll")>
    Private Function RoGetActivationFactory(activatableClassId As IntPtr, ByRef iid As Guid, ByRef factory As IntPtr) As Integer
    End Function

    <DllImport("combase.dll")>
    Private Function WindowsDeleteString(hstr As IntPtr) As Integer
    End Function

    'IWebAuthenticationCoreManagerInterop::RequestTokenForWindowAsync(HWND, IWebTokenRequest*, REFIID, void**)
    <UnmanagedFunctionPointer(CallingConvention.StdCall)>
    Private Delegate Function DRequestTokenForWindow(thisPtr As IntPtr, hwnd As IntPtr, request As IntPtr,
                                                     ByRef iid As Guid, ByRef operation As IntPtr) As Integer

    Private Function GetWamInterop() As IntPtr
        Dim HStr As IntPtr
        If WindowsCreateString(WamClassName, CUInt(WamClassName.Length), HStr) <> 0 Then Return IntPtr.Zero
        Dim Iid As New Guid(IidWamInterop)
        Dim Factory As IntPtr
        Dim Hr = RoGetActivationFactory(HStr, Iid, Factory)
        WindowsDeleteString(HStr)
        If Hr <> 0 OrElse Factory = IntPtr.Zero Then Return IntPtr.Zero
        Return Factory
    End Function

    ''' <summary>等待 WinRT 的 IAsyncOperation 完成，返回结果对象。</summary>
    Private Function AwaitOperation(Of TResult)(Operation As IAsyncOperation(Of TResult), TimeoutMs As Integer) As TResult
        Dim Waited As Integer = 0
        Do While Operation.Status = AsyncStatus.Started
            Thread.Sleep(50)
            Waited += 50
            If Waited > TimeoutMs Then Throw New Exception("等待微软账户响应超时")
        Loop
        If Operation.Status <> AsyncStatus.Completed Then
            Throw New Exception("微软账户操作失败（0x" & Operation.ErrorCode.ToString("X8") & "）")
        End If
        Return Operation.GetResults()
    End Function

    ''' <summary>向 WAM 请求 MSA 令牌。Hwnd 用于承载系统登录框，Prompt 控制是否强制弹出选择账户界面。</summary>
    Private Function RequestMsaTicket(Hwnd As IntPtr, Prompt As WebTokenRequestPromptType) As (Ticket As String, AccountId As String)
        Dim ProviderOp = WebAuthenticationCoreManager.FindAccountProviderAsync(MsaProviderName, MsaAuthority)
        Dim Provider = AwaitOperation(ProviderOp, 60000)
        If Provider Is Nothing Then Throw New Exception("系统中没有可用的微软账户提供程序")
        Dim Request As New WebTokenRequest(Provider, XblScope, XboxClientId, Prompt)

        Dim Factory = GetWamInterop()
        If Factory = IntPtr.Zero Then Throw New Exception("无法创建 Windows 账户管理器接口")
        Try
            Dim RequestPtr = Marshal.GetIUnknownForObject(Request)
            Dim Vtbl = Marshal.ReadIntPtr(Factory)
            Dim Fn = Marshal.ReadIntPtr(Vtbl, 6 * IntPtr.Size)
            Dim Caller = CType(Marshal.GetDelegateForFunctionPointer(Fn, GetType(DRequestTokenForWindow)), DRequestTokenForWindow)
            Dim Operation As IntPtr
            Dim OpIid As New Guid(IidTokenResultOp)
            Dim Hr = Caller(Factory, Hwnd, RequestPtr, OpIid, Operation)
            If Hr <> 0 OrElse Operation = IntPtr.Zero Then Throw New Exception("请求微软账户令牌失败（0x" & Hr.ToString("X8") & "）")

            Dim AsyncOp = DirectCast(Marshal.GetObjectForIUnknown(Operation), IAsyncOperation(Of WebTokenRequestResult))
            Dim Result = AwaitOperation(AsyncOp, 10 * 60 * 1000)
            If Result Is Nothing Then Throw New Exception("微软账户没有返回结果")
            If Result.ResponseStatus <> WebTokenRequestStatus.Success Then
                Dim Reason = If(Result.ResponseError Is Nothing, Result.ResponseStatus.ToString(), Result.ResponseError.ErrorMessage)
                Throw New Exception("微软账户登录未完成：" & Reason)
            End If
            If Result.ResponseData.Count = 0 Then Throw New Exception("微软账户没有返回令牌")
            Dim Response = Result.ResponseData(0)
            Dim AccountId = If(Response.WebAccount Is Nothing, "", Response.WebAccount.UserName)
            Return (Response.Token, AccountId)
        Finally
            Marshal.Release(Factory)
        End Try
    End Function

#End Region

#Region "Xbox Live 认证"

    Private Function PostJson(Url As String, Body As String, Optional Authorization As String = "", Optional Contract As String = "1") As JObject
        Dim Request = CType(WebRequest.Create(Url), HttpWebRequest)
        Request.Method = "POST"
        Request.ContentType = "application/json"
        Request.Accept = "application/json"
        Request.UserAgent = "TYW Launcher"
        Request.Timeout = 30000
        Request.ServicePoint.Expect100Continue = False
        Request.Headers.Add("x-xbl-contract-version", Contract)
        If Authorization <> "" Then Request.Headers.Add("Authorization", Authorization)
        Dim Bytes = Encoding.UTF8.GetBytes(Body)
        Request.ContentLength = Bytes.Length
        Using Stream = Request.GetRequestStream()
            Stream.Write(Bytes, 0, Bytes.Length)
        End Using
        Try
            Using Response = CType(Request.GetResponse(), HttpWebResponse)
                Using Reader As New StreamReader(Response.GetResponseStream(), Encoding.UTF8)
                    Return JObject.Parse(Reader.ReadToEnd())
                End Using
            End Using
        Catch ex As WebException
            Dim Detail As String = ""
            If ex.Response IsNot Nothing Then
                Try
                    Using Reader As New StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)
                        Detail = Reader.ReadToEnd()
                    End Using
                Catch
                End Try
            End If
            Throw New Exception("Xbox 认证请求失败：" & ex.Message & If(Detail = "", "", " " & Detail))
        End Try
    End Function

    ''' <summary>用 MSA 令牌换 Xbox Live 用户令牌。</summary>
    Private Function XblUserAuth(Ticket As String) As String
        Dim Body As New JObject From {
            {"Properties", New JObject From {
                {"AuthMethod", "RPS"},
                {"SiteName", "user.auth.xboxlive.com"},
                {"RpsTicket", Ticket}}},
            {"RelyingParty", "http://auth.xboxlive.com"},
            {"TokenType", "JWT"}}
        Dim Json = PostJson("https://user.auth.xboxlive.com/user/authenticate", Body.ToString(Newtonsoft.Json.Formatting.None))
        Dim Token = If(Json("Token")?.ToString(), "")
        If Token = "" Then Throw New Exception("Xbox Live 用户令牌为空")
        Return Token
    End Function

    ''' <summary>用用户令牌换 XSTS 令牌与身份声明。</summary>
    Private Function XstsAuth(UserToken As String) As BedrockAccount
        Dim Body As New JObject From {
            {"Properties", New JObject From {
                {"SandboxId", "RETAIL"},
                {"UserTokens", New JArray From {UserToken}}}},
            {"RelyingParty", "http://xboxlive.com"},
            {"TokenType", "JWT"}}
        Dim Json = PostJson("https://xsts.auth.xboxlive.com/xsts/authorize", Body.ToString(Newtonsoft.Json.Formatting.None))
        Dim Token = If(Json("Token")?.ToString(), "")
        Dim Claims = TryCast(Json("DisplayClaims"), JObject)
        Dim Xui = TryCast(Claims?("xui"), JArray)
        If Token = "" OrElse Xui Is Nothing OrElse Xui.Count = 0 Then
            Throw New Exception("Xbox Live 授权失败，可能是该账户没有 Xbox 档案")
        End If
        Dim First = CType(Xui(0), JObject)
        Dim NotAfter = If(Json("NotAfter")?.ToString(), "")
        Dim Expires As Date = Date.UtcNow.AddHours(8)
        If NotAfter <> "" Then
            Dim Parsed As Date
            If Date.TryParse(NotAfter, Globalization.CultureInfo.InvariantCulture,
                             Globalization.DateTimeStyles.AdjustToUniversal Or Globalization.DateTimeStyles.AssumeUniversal, Parsed) Then
                Expires = Parsed
            End If
        End If
        Return New BedrockAccount With {
            .Xsts = Token,
            .Uhs = If(First("uhs")?.ToString(), ""),
            .Xuid = If(First("xid")?.ToString(), ""),
            .Gamertag = If(First("gtg")?.ToString(), ""),
            .ExpiresUtc = Expires}
    End Function

#End Region

#Region "对外入口"

    ''' <summary>在独立的 MTA 线程上执行 WinRT 调用，并把异常带回调用方。</summary>
    Private Function RunOnMta(Of TResult)(Work As Func(Of TResult)) As TResult
        Dim Result As TResult = Nothing
        Dim Failure As Exception = Nothing
        Dim Done As New ManualResetEventSlim(False)
        Dim Worker As New Thread(
            Sub()
                Try
                    Try
                        RoInitialize(1) 'RO_INIT_MULTITHREADED
                    Catch
                    End Try
                    Result = Work()
                Catch ex As Exception
                    Failure = ex
                Finally
                    Done.Set()
                End Try
            End Sub)
        Worker.SetApartmentState(ApartmentState.MTA)
        Worker.IsBackground = True
        Worker.Start()
        Done.Wait()
        Done.Dispose()
        If Failure IsNot Nothing Then Throw Failure
        Return Result
    End Function

    ''' <summary>
    ''' 登录微软账户并换到 Xbox Live 身份。Hwnd 用于承载系统登录框；ForceUi 为真时强制弹出账户选择。
    ''' 必须在后台线程调用（内部会阻塞等待系统登录框）。
    ''' </summary>
    Public Function Login(Hwnd As IntPtr, ForceUi As Boolean) As BedrockAccount
        Dim Result As BedrockAccount = RunOnMta(
            Function()
                Dim Msa = RequestMsaTicket(Hwnd, If(ForceUi, WebTokenRequestPromptType.ForceAuthentication, WebTokenRequestPromptType.Default))
                Dim UserToken = XblUserAuth(Msa.Ticket)
                Dim Account = XstsAuth(UserToken)
                Account.AccountId = Msa.AccountId
                Return Account
            End Function)
        If Result Is Nothing Then Throw New Exception("登录被取消")
        SaveAccount(Result)
        Return Result
    End Function

    ''' <summary>
    ''' 确保客户端已登录可用于基岩版的 Xbox 账户。
    ''' 缓存有效就直接用；缓存过期先静默续期；都不行就弹出 Windows 的微软账户登录窗口让用户登录。
    ''' 必须在后台线程调用。用户取消登录时抛出异常。
    ''' </summary>
    Public Function EnsureSignedIn(Hwnd As IntPtr) As BedrockAccount
        Dim Cached = GetCachedAccount()
        If Cached IsNot Nothing AndAlso Not Cached.IsExpired Then Return Cached
        '先试静默续期：Windows 里已经登录过账户时不会弹窗
        Try
            Dim Silent = RequestWithPrompt(Hwnd, WebTokenRequestPromptType.Default)
            If Silent IsNot Nothing Then
                SaveAccount(Silent)
                Return Silent
            End If
        Catch ex As Exception
            Logger.Warn(ex, "静默续期 Xbox 账户失败，改为让用户登录")
        End Try
        '需要用户登录：直接弹 Windows 的微软账户登录窗口
        Dim Account = RequestWithPrompt(Hwnd, WebTokenRequestPromptType.ForceAuthentication)
        SaveAccount(Account)
        Return Account
    End Function

    ''' <summary>按指定的提示类型向 Windows 账户管理器取账户，并换到 Xbox 身份。</summary>
    Private Function RequestWithPrompt(Hwnd As IntPtr, Prompt As WebTokenRequestPromptType) As BedrockAccount
        Return RunOnMta(
            Function()
                Dim Msa = RequestMsaTicket(Hwnd, Prompt)
                Dim Account = XstsAuth(XblUserAuth(Msa.Ticket))
                Account.AccountId = Msa.AccountId
                Return Account
            End Function)
    End Function

#End Region

End Module
