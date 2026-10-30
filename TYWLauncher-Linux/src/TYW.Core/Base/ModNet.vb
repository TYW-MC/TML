Imports System.Net
Imports System.Net.Http

''' <summary>
''' 网络访问。对应 Windows 版 ModNet 的 ServerRequest / NetGet。
''' </summary>
Public Module ModNet

    Public ReadOnly ServerAddress As String = "http://Launcher.mc.tywmc.cn:17403/"

    Private ReadOnly Client As HttpClient = CreateClient()

    Private Function CreateClient() As HttpClient
        Try
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
            ServicePointManager.Expect100Continue = False
        Catch
        End Try
        Dim h As New HttpClientHandler With {.AutomaticDecompression = DecompressionMethods.GZip}
        Dim c As New HttpClient(h) With {.Timeout = TimeSpan.FromSeconds(12)}
        Try
            c.DefaultRequestHeaders.UserAgent.ParseAdd("TYWLauncher/Linux")
        Catch
        End Try
        Return c
    End Function

    ''' <summary>请求服务端接口（自动带加密协商头），返回明文文本。失败返回 Nothing。</summary>
    Public Function GetServer(path As String) As String
        Try
            Dim url = If(path.StartsWith("http"), path, ServerAddress & path.TrimStart("/"c))
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                Dim added As Boolean = req.Headers.TryAddWithoutValidation("X-TYW-Enc", "1")
                If Not added Then ModLog.Warn("无法附加加密协商头")
                Using resp = Client.SendAsync(req).GetAwaiter().GetResult()
                    If Not resp.IsSuccessStatusCode Then Return Nothing
                    Dim bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                    Dim plain = ModCrypto.Decrypt(bytes)
                    If plain IsNot Nothing Then Return Text.Encoding.UTF8.GetString(plain)
                    Return Text.Encoding.UTF8.GetString(bytes)
                End Using
            End Using
        Catch ex As Exception
            ModLog.Err(ex, "请求 " & path)
            Return Nothing
        End Try
    End Function

    Public Function GetUrl(url As String) As String
        Try
            Return Client.GetStringAsync(url).GetAwaiter().GetResult()
        Catch ex As Exception
            ModLog.Err(ex, "请求 " & url)
            Return Nothing
        End Try
    End Function

End Module
