Imports System.IO

''' <summary>
''' Linux 下的路径约定（对应 Windows 版 ModBase / Path）。
''' </summary>
Public Module ModPaths

    Private ReadOnly Home As String =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)

    ''' <summary>启动器数据目录：~/.local/share/TYWLauncher</summary>
    Public ReadOnly Root As String =
        If(Environment.GetEnvironmentVariable("TYW_DATA_DIR"),
           Path.Combine(Home, ".local", "share", "TYWLauncher"))

    ''' <summary>游戏目录：~/.minecraft</summary>
    Public ReadOnly McFolder As String =
        Path.Combine(Home, ".minecraft")

    Public ReadOnly LogDir As String = Path.Combine(Root, "Log")
    Public ReadOnly CacheDir As String = Path.Combine(Root, "Cache")
    Public ReadOnly TempDir As String = Path.Combine(Root, "Temp")

    Public ReadOnly LogFile As String = Path.Combine(LogDir, "TYWLauncher.log")
    Public ReadOnly SettingFile As String = Path.Combine(Root, "setup.ini")

    Public Sub EnsureAll()
        For Each d In {Root, LogDir, CacheDir, TempDir}
            Try
                If Not Directory.Exists(d) Then Directory.CreateDirectory(d)
            Catch
            End Try
        Next
    End Sub

End Module
