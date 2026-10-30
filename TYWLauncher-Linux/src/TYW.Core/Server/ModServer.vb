Imports System.Text.Json

''' <summary>服务端公告。</summary>
Public Class NoticeData
    Public Property Title As String = ""
    Public Property Code As String = ""
    Public Property Content As String = ""
    Public Property Detail As String = ""
End Class

''' <summary>
''' 服务端接口。对应 Windows 版 ModServer。
''' </summary>
Public Module ModServer

    ''' <summary>当前通道：正式版。</summary>
    Public Const Channel As String = "Release"

    Private _notice As NoticeData = Nothing

    ''' <summary>
    ''' 读取 Notice.cfg。十段格式：
    ''' 标题|快照码|正式码|正式公告|快照公告|内测码|内测公告|正式详情|快照详情|内测详情
    ''' </summary>
    Public Function ReadNotice(Optional force As Boolean = False) As NoticeData
        If _notice IsNot Nothing AndAlso Not force Then Return _notice
        Dim raw = ModNet.GetServer("Notice.cfg")
        If String.IsNullOrWhiteSpace(raw) Then Return Nothing
        Try
            Dim parts = raw.Trim().Split("|"c)
            If parts.Length < 7 Then Return Nothing
            Dim n As New NoticeData With {
                .Title = parts(0),
                .Code = parts(2),
                .Content = parts(3).Replace("\n", Environment.NewLine),
                .Detail = If(parts.Length > 7, parts(7).Replace("\n", Environment.NewLine), "")
            }
            _notice = n
            Return n
        Catch ex As Exception
            ModLog.Err(ex, "解析公告")
            Return Nothing
        End Try
    End Function

    ''' <summary>检查启动器版本更新。没有更新返回 Nothing。</summary>
    Public Function CheckUpdate(versionCode As Integer) As String
        Dim raw = ModNet.GetServer("config.json")
        If String.IsNullOrWhiteSpace(raw) Then Return Nothing
        Try
            Using doc = JsonDocument.Parse(raw)
                Dim root = doc.RootElement
                Dim codeEl As JsonElement
                Dim latest As Integer = 0
                If root.TryGetProperty("LauncherVersionCode", codeEl) Then
                    latest = codeEl.GetInt32()
                End If
                If latest > versionCode Then
                    Dim url As String = ""
                    Dim urlEl As JsonElement
                    If root.TryGetProperty("LauncherUrl", urlEl) Then url = urlEl.GetString()
                    Return If(String.IsNullOrEmpty(url), "update", url)
                End If
            End Using
        Catch ex As Exception
            ModLog.Err(ex, "检查更新")
        End Try
        Return Nothing
    End Function

End Module
