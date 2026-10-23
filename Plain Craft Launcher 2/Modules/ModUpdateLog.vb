''' <summary>
''' 内置更新日志：每个版本一段，互不覆盖，与服端公告完全分开。
''' 发布新版本时，把 VersionCode 加 1，并在下面的 Select Case 里加一条对应版本码的记录即可。
''' </summary>
Module ModUpdateLog

    ''' <summary>当前版本内置的更新日志。没有登记的版本返回空列表，不会弹出空窗口。</summary>
    Friend Function GetUpdateLog(Code As Integer) As (Version As String, Items As String())
        Select Case Code
            Case 112 '1.1.1 正式版
                Return ("1.1.1", New String() {
                    "启动器内置更新日志：更新后首次打开会显示本版本改了什么",
                    "修复打开版本选择时报「无法创建未知类型 MyMenuItem」的问题",
                    "修复部分运行期界面因程序集改名而无法加载的问题",
                    "个性化中的隐藏主题、自定义配色不再需要解锁，全部可直接使用",
                    "没网或连不上服务器时只在左下角提示，不再算作错误、不再上报",
                    "正版登录遇到 IP 异常时，会自动改用直连重试一次（不走系统代理）",
                    "服务端公告改为正式版、快照版各自独立，更新日志不再占用公告"})
            Case 113 '1.1.1 快照版
                Return ("1.1.1 快照版", New String() {
                    "启动器内置更新日志：更新后首次打开会显示本版本改了什么",
                    "修复打开版本选择时报「无法创建未知类型 MyMenuItem」的问题",
                    "修复部分运行期界面因程序集改名而无法加载的问题",
                    "个性化中的隐藏主题、自定义配色不再需要解锁，全部可直接使用",
                    "没网或连不上服务器时只在左下角提示，不再算作错误、不再上报",
                    "正版登录遇到 IP 异常时，会自动改用直连重试一次（不走系统代理）",
                    "服务端公告改为正式版、快照版各自独立，更新日志不再占用公告"})
            Case 114 '1.1.2 正式版
                Return ("1.1.2", New String() {
                    "服务端公告改成「同一条只弹一次」，改了公告内容才会再弹",
                    "删除「关于」页的赞助者栏目（连同赞助、土豆码按钮一起移除）",
                    "「复制识别码」搬到「设置 - 其他 - 启动器」里，随用随点",
                    "自动更新在所有版本默认开启，正式版与快照版一致，不再需要土豆码",
                    "沿用 1.1.1 的内置更新日志、按通道分开的服务端公告"})
            Case 115 '1.1.2 快照版
                Return ("1.1.2 快照版", New String() {
                    "服务端公告改成「同一条只弹一次」，改了公告内容才会再弹",
                    "删除「关于」页的赞助者栏目（连同赞助、土豆码按钮一起移除）",
                    "「复制识别码」搬到「设置 - 其他 - 启动器」里，随用随点",
                    "自动更新在所有版本默认开启，正式版与快照版一致，不再需要土豆码",
                    "沿用 1.1.1 的内置更新日志、按通道分开的服务端公告"})
            Case 116 '1.1.3 正式版
                Return ("1.1.3", New String() {
                    "新增「内测版」通道：设置 - 其他 - 启动器 勾选「内测版体验」，输入内测码即可进入",
                    "内测码由服务端按设备码发放，可随时停用或删除，资格失效会自动切回正式版",
                    "内测版没有自动更新与检查更新，并且必须联网连上服务端才能使用",
                    "取消内测版体验会切回正式版（开了快照版测试则切回快照版）",
                    "错误上报补充版本号、版本类型（正式版 / 快照版 / 内测版）与设备码",
                    "修复关闭快照版测试功能后仍然检查快照版更新的问题"})
            Case 117 '1.1.3 快照版
                Return ("1.1.3 快照版", New String() {
                    "新增「内测版」通道：设置 - 其他 - 启动器 勾选「内测版体验」，输入内测码即可进入",
                    "内测码由服务端按设备码发放，可随时停用或删除，资格失效会自动切回正式版",
                    "内测版没有自动更新与检查更新，并且必须联网连上服务端才能使用",
                    "取消内测版体验会切回正式版（开了快照版测试则切回快照版）",
                    "错误上报补充版本号、版本类型（正式版 / 快照版 / 内测版）与设备码",
                    "修复关闭快照版测试功能后仍然检查快照版更新的问题"})
            Case 118 '1.1.3 内测版
                Return ("1.1.3 内测版", New String() {
                    "这是内测版：功能最新，但可能随时出错，也没有自动更新与检查更新",
                    "内测版必须联网并连接服务端才能使用，连不上只能切回正式版或退出",
                    "服务端可以随时停用或删除你的设备内测资格",
                    "匿名错误上报强制开启，无法关闭",
                    "想退出内测可在 设置 - 其他 中取消「内测版体验」"})
            Case 119 '1.1.4 正式版
                Return ("1.1.4", New String() {
                    "修复内测版体验取消下载后仍然会替换成内测版 / 启动器被关掉的问题",
                    "下载被取消或失败时会自动恢复原状：内测状态回滚、复选框打回原样、启动器继续运行",
                    "内测版里「自动更新」现在显示为关闭（内测版本身不提供自动更新）",
                    "切换通道时增加了文件完整性校验，下载不完整不会替换程序"})
            Case 120 '1.1.4 快照版
                Return ("1.1.4 快照版", New String() {
                    "修复内测版体验取消下载后仍然会替换成内测版 / 启动器被关掉的问题",
                    "下载被取消或失败时会自动恢复原状：内测状态回滚、复选框打回原样、启动器继续运行",
                    "内测版里「自动更新」现在显示为关闭（内测版本身不提供自动更新）",
                    "切换通道时增加了文件完整性校验，下载不完整不会替换程序"})
            Case 121 '1.1.4 内测版
                Return ("1.1.4 内测版", New String() {
                    "内测版不提供自动更新，「自动更新」始终显示为关闭且不可修改",
                    "匿名错误上报强制开启，无法关闭",
                    "内测版必须联网并连接服务端才能使用",
                    "下载内测版程序失败或被取消时不会再关掉启动器，会自动回到原来的版本"})
            Case 122 '1.1.5 正式版
                Return ("1.1.5", New String() {
                    "换成更清晰的启动器图标",
                    "关于页与特别鸣谢页底部标注「根据 PCL 改版」，鸣谢里的 PCL 写作「原PCL」",
                    "客户端内其余 PCL 字样统一换成 TYW Launcher（崩溃日志名、离线皮肤资源包名、整合包导出文件名）",
                    "沿用 1.1.4 的修复：取消下载自动回滚、内测版自动更新显示为关闭"})
            Case 123 '1.1.5 快照版
                Return ("1.1.5 快照版", New String() {
                    "换成更清晰的启动器图标",
                    "关于页与特别鸣谢页底部标注「根据 PCL 改版」，鸣谢里的 PCL 写作「原PCL」",
                    "客户端内其余 PCL 字样统一换成 TYW Launcher",
                    "沿用 1.1.4 的修复：取消下载自动回滚、内测版自动更新显示为关闭"})
            Case 124 '1.1.5 内测版
                Return ("1.1.5 内测版", New String() {
                    "换成更清晰的启动器图标，其余与 1.1.5 正式版一致",
                    "内测版不提供自动更新，「自动更新」始终显示为关闭",
                    "内测版必须联网并连接服务端才能使用"})
            Case 125 '1.1.6 正式版
                Return ("1.1.6", New String() {
                    "新增设备封禁：被服务端封禁的设备启动器将无法使用，启动时会提示封禁原因",
                    "网络无法连接服务端时不受影响，仍可正常使用"})
            Case 126 '1.1.6 快照版
                Return ("1.1.6 快照版", New String() {
                    "新增设备封禁：被服务端封禁的设备启动器将无法使用，启动时会提示封禁原因",
                    "网络无法连接服务端时不受影响，仍可正常使用"})
            Case 127 '1.1.6 内测版
                Return ("1.1.6 内测版", New String() {
                    "新增设备封禁：被服务端封禁的设备启动器将无法使用，启动时会提示封禁原因",
                    "内测版不提供自动更新，「自动更新」始终显示为关闭",
                    "内测版必须联网并连接服务端才能使用"})
            Case 128 '1.1.7 正式版
                Return ("1.1.7", New String() {
                    "「检查更新」的更新详情改为独立显示：优先用服务端下发的更新详情，留空时显示启动器内置更新日志",
                    "服务端公告只负责启动弹窗，不再混进更新详情里",
                    "修复设备封禁可能未生效的问题，封禁操作现在会弹窗确认",
                    "沿用 1.1.6 的新增设备封禁"})
            Case 129 '1.1.7 快照版
                Return ("1.1.7 快照版", New String() {
                    "「检查更新」的更新详情改为独立显示：优先用服务端下发的更新详情，留空时显示启动器内置更新日志",
                    "服务端公告只负责启动弹窗，不再混进更新详情里",
                    "修复设备封禁可能未生效的问题，封禁操作现在会弹窗确认"})
            Case 130 '1.1.7 内测版
                Return ("1.1.7 内测版", New String() {
                    "服务端公告只负责启动弹窗，与更新详情彻底分开",
                    "修复设备封禁可能未生效的问题",
                    "内测版不提供自动更新，「自动更新」始终显示为关闭",
                    "内测版必须联网并连接服务端才能使用"})
            Case 137 '1.1.10 正式版
                Return ("1.1.10", New String() {
                    "修复「内存优化」报错：找不到 EmptyWorkingSet 入口点（改用 psapi.dll 调用）",
                    "“已更新至”弹窗改为读取内置更新日志，不再列出上游 PCL 2.x 的老日志",
                    "跨多个版本更新时按版本标注每条的出处",
                    "“完整更新日志”改为显示本通道历次更新内容"})
            Case 138 '1.1.10 快照版
                Return ("1.1.10 快照版", New String() {
                    "修复「内存优化」报错：找不到 EmptyWorkingSet 入口点（改用 psapi.dll 调用）",
                    "“已更新至”弹窗改为读取内置更新日志，不再列出上游 PCL 2.x 的老日志"})
            Case 139 '1.1.10 内测版
                Return ("1.1.10 内测版", New String() {
                    "修复「内存优化」报错：找不到 EmptyWorkingSet 入口点（改用 psapi.dll 调用）",
                    "“已更新至”弹窗改为读取内置更新日志，不再列出上游 PCL 2.x 的老日志",
                    "内测版不提供自动更新，「自动更新」始终显示为关闭"})
            Case Else
                Return (VersionBaseName, New String() {})
        End Select
    End Function

    ''' <summary>从日志标题判断它属于哪个通道（标题后缀带「快照版」「内测版」）。</summary>
    ''' <remarks>不能用版本码取余来判断：早期 1.1.1/1.1.2 的版本码排布和现在不一致，会串通道。</remarks>
    Private Function LogChannelKey(Version As String) As String
        If Version.Contains("内测版") Then Return "Beta"
        If Version.Contains("快照版") Then Return "Snapshot"
        Return "Release"
    End Function

    ''' <summary>当前程序所在通道。</summary>
    Private Function CurrentChannelKey() As String
        Select Case BuildType
            Case BuildTypes.Beta : Return "Beta"
            Case BuildTypes.Snapshot : Return "Snapshot"
            Case Else : Return "Release"
        End Select
    End Function

    ''' <summary>取出同一通道下 (FromCode, ToCode] 之间所有已登记的更新日志，按版本码从旧到新。</summary>
    Friend Function GetLogsBetween(FromCode As Integer, ToCode As Integer) As List(Of (Version As String, Items As String()))
        Dim Result As New List(Of (Version As String, Items As String()))
        Dim Channel As String = CurrentChannelKey()
        For Code = Math.Max(FromCode + 1, 0) To ToCode
            Dim Info = GetUpdateLog(Code)
            If Info.Items Is Nothing OrElse Info.Items.Length = 0 Then Continue For
            If LogChannelKey(Info.Version) <> Channel Then Continue For
            Result.Add(Info)
        Next
        Return Result
    End Function

    ''' <summary>本通道所有已登记的更新日志，按版本码从新到旧（给「完整更新日志」用）。</summary>
    Friend Function GetAllLogs() As List(Of (Version As String, Items As String()))
        Dim Result As New List(Of (Version As String, Items As String()))
        Dim Channel As String = CurrentChannelKey()
        For Code = VersionCode To 0 Step -1
            Dim Info = GetUpdateLog(Code)
            If Info.Items Is Nothing OrElse Info.Items.Length = 0 Then Continue For
            If LogChannelKey(Info.Version) <> Channel Then Continue For
            Result.Add(Info)
        Next
        Return Result
    End Function

    ''' <summary>新版本首次打开时展示一次内置更新日志（首次安装不显示）。</summary>
    Friend Sub ShowUpdateLogIfNeeded()
        Try
            If Not FrmMain.IsUpdateLogPending Then Return
            FrmMain.IsUpdateLogPending = False '只显示一次
            Dim LogInfo = GetUpdateLog(VersionCode)
            If LogInfo.Items Is Nothing OrElse LogInfo.Items.Length = 0 Then Return
            Dim Text As String =
                "TYW Launcher 已经更新到 " & LogInfo.Version & " 版：" & vbCrLf & vbCrLf &
                String.Join(vbCrLf, LogInfo.Items.Select(Function(Item) "· " & Item))
            Logger.Info($"显示更新日志：{LogInfo.Version}（版本码 {VersionCode}，共 {LogInfo.Items.Length} 条）")
            MyMsgBox(Text, "更新日志")
        Catch ex As Exception
            Logger.Warn(ex, "显示更新日志失败")
        End Try
    End Sub

End Module
