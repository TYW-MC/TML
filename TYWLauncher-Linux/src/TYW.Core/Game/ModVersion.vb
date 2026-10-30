Imports System.IO
Imports System.Text.Json

''' <summary>本地已安装的版本。</summary>
Public Class McVersionItem
    Public Property Id As String = ""
    Public Property Type As String = ""
    Public Property Folder As String = ""
    Public Property JsonPath As String = ""

    Public ReadOnly Property Display As String
        Get
            Return Id
        End Get
    End Property
End Class

''' <summary>
''' 扫描 ~/.minecraft/versions。对应 Windows 版 McInstance 的版本识别。
''' </summary>
Public Module ModVersion

    Private _cache As List(Of McVersionItem) = Nothing

    ''' <summary>列出全部已安装的 Java 版实例（不含基岩版，Linux 版不支持基岩版）。</summary>
    Public Function ListVersions(Optional force As Boolean = False) As List(Of McVersionItem)
        If _cache IsNot Nothing AndAlso Not force Then Return _cache
        Dim result As New List(Of McVersionItem)
        Try
            Dim dir = Path.Combine(ModPaths.McFolder, "versions")
            If Directory.Exists(dir) Then
                For Each subDir In Directory.GetDirectories(dir)
                    Dim verName = Path.GetFileName(subDir)
                    Dim json = Path.Combine(subDir, verName & ".json")
                    If Not File.Exists(json) Then Continue For
                    Dim item As New McVersionItem With {.Id = verName, .Folder = subDir, .JsonPath = json}
                    item.Type = ReadType(json)
                    ' 基岩版实例（Windows 端写入的伪造 JSON）在 Linux 上没有意义，跳过
                    If item.Type = "bedrock" Then Continue For
                    result.Add(item)
                Next
            End If
        Catch ex As Exception
            ModLog.Err(ex, "扫描版本")
        End Try
        result.Sort(Function(a, b) String.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase))
        _cache = result
        Return result
    End Function

    Private Function ReadType(jsonPath As String) As String
        Try
            Using doc = JsonDocument.Parse(File.ReadAllText(jsonPath))
                Dim root = doc.RootElement
                Dim bedrock As JsonElement
                If root.TryGetProperty("bedrock", bedrock) Then Return "bedrock"
                Dim tEl As JsonElement
                If root.TryGetProperty("type", tEl) Then Return tEl.GetString()
            End Using
        Catch
        End Try
        Return "unknown"
    End Function

    ''' <summary>取上次选择的版本；不存在时返回第一个。</summary>
    Public Function SelectedOrDefault() As McVersionItem
        Dim list = ListVersions()
        If list.Count = 0 Then Return Nothing
        Dim sel = ModSettings.GetString("VersionSelect")
        If Not String.IsNullOrEmpty(sel) Then
            Dim hit = list.FirstOrDefault(Function(v) v.Id = sel)
            If hit IsNot Nothing Then Return hit
        End If
        Return list(0)
    End Function

    Public Sub Invalidate()
        _cache = Nothing
    End Sub

End Module
