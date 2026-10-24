'' <summary>TYW Launcher 多语言框架：词典加载、运行时文本翻译、可视化树遍历。</summary>
Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents
Imports System.Windows.Threading
Imports Newtonsoft.Json

Public Module ModLang

#Region "语言定义"

    Public Class LangInfo
        Public Code As String
        Public Name As String
        ''' <summary>该语言的原生名称（简体中文 / English / 日本語 …），用于「自动设置为 X」这类文案。</summary>
        Public LocalName As String
        ''' <summary>对应的 Minecraft 游戏语言代码（zh_cn / en_us / ja_jp …）。</summary>
        Public GameLang As String
        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

    ''' <summary>全部支持的语言（顺序即设置页下拉框顺序，索引即存储值）。</summary>
    Public ReadOnly Languages As New List(Of LangInfo) From {
        New LangInfo With {.Code = "zh_CN", .Name = "简体中文（默认）", .LocalName = "简体中文", .GameLang = "zh_cn"},
        New LangInfo With {.Code = "en_US", .Name = "English", .LocalName = "English", .GameLang = "en_us"},
        New LangInfo With {.Code = "ja_JP", .Name = "日本語", .LocalName = "日本語", .GameLang = "ja_jp"},
        New LangInfo With {.Code = "ru_RU", .Name = "Русский", .LocalName = "Русский", .GameLang = "ru_ru"},
        New LangInfo With {.Code = "id_ID", .Name = "Bahasa Indonesia", .LocalName = "Bahasa Indonesia", .GameLang = "id_id"}
    }

    ''' <summary>当前语言代码（默认中文）。</summary>
    Public CurrentLang As String = "zh_CN"

    ''' <summary>当前语言对应的 Minecraft 游戏语言代码（如 zh_cn），供「游戏语言跟随启动器」使用。</summary>
    Public ReadOnly Property GameLang As String
        Get
            For Each LI As LangInfo In Languages
                If LI.Code = CurrentLang Then Return LI.GameLang
            Next
            Return "zh_cn"
        End Get
    End Property

    ''' <summary>当前语言的原生名称。</summary>
    Public ReadOnly Property CurrentLocalName As String
        Get
            For Each LI As LangInfo In Languages
                If LI.Code = CurrentLang Then Return LI.LocalName
            Next
            Return "简体中文"
        End Get
    End Property

    ''' <summary>语言切换完成后触发（供设置页刷新「自动设置为 X」这类动态文案）。</summary>
    Public Event LanguageChanged As EventHandler

#End Region

#Region "词典与翻译"

    Private ReadOnly Dicts As New Dictionary(Of String, Dictionary(Of String, String))
    Private ReadOnly CjkRegex As New Regex("[\u4e00-\u9fff\u3000-\u303f\uff01-\uff5e]")
    Private ReadOnly LoadLock As New Object

    Public Function HasCJK(Text As String) As Boolean
        Return Not String.IsNullOrEmpty(Text) AndAlso CjkRegex.IsMatch(Text)
    End Function

    ''' <summary>该字符串是语言自身的名称（如 "日本語"、"Русский"）——永远保持原文，不参与翻译。</summary>
    Private Function IsLangName(S As String) As Boolean
        For Each LI As LangInfo In Languages
            If LI.Name = S Then Return True
        Next
        Return False
    End Function

    ''' <summary>从嵌入资源加载全部词典（缺文件则跳过，运行时回退中文）。</summary>
    Public Sub InitLangs()
        SyncLock LoadLock
            For Each LI As LangInfo In Languages
                If LI.Code = "zh_CN" Then Continue For
                Try
                    Dim ResName As String = "LangPack." & LI.Code & ".json"
                    Dim Stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(ResName)
                    If Stream Is Nothing Then
                        Logger.Warn("[Lang] 未找到嵌入词典 " & ResName & "，该语言将回退中文")
                        Continue For
                    End If
                    Using Stream
                        Using Reader As New StreamReader(Stream, System.Text.Encoding.UTF8)
                            Dicts(LI.Code) = JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(Reader.ReadToEnd())
                        End Using
                    End Using
                    Logger.Info("[Lang] 已加载词典 " & ResName & "（" & Dicts(LI.Code).Count & " 条）")
                Catch ex As Exception
                    Logger.Warn(ex, "[Lang] 加载词典 " & LI.Code & " 失败")
                End Try
            Next
        End SyncLock
    End Sub

    ''' <summary>翻译一个字符串：查当前语言词典，未命中或为中文时原样返回。</summary>
    Public Function T(Src As String) As String
        If String.IsNullOrEmpty(Src) OrElse CurrentLang = "zh_CN" Then Return Src
        Dim Dict As Dictionary(Of String, String) = Nothing
        If Not Dicts.TryGetValue(CurrentLang, Dict) OrElse Dict Is Nothing Then Return Src
        Dim Trans As String = Nothing
        If Dict.TryGetValue(Src, Trans) AndAlso Not String.IsNullOrEmpty(Trans) Then Return Trans
        '★ 多行文本：弹窗正文常用 A & vbCrLf & B 拼接，整串查不中词典，逐行翻译后再拼回。
        If Src.IndexOf(vbCrLf) >= 0 OrElse Src.IndexOf(ChrW(10)) >= 0 Then
            Dim Lines As String() = Src.Replace(vbCrLf, ChrW(10)).Split(ChrW(10))
            Dim AnyChanged As Boolean = False
            For i As Integer = 0 To Lines.Length - 1
                Dim LineTrans As String = Nothing
                If Dict.TryGetValue(Lines(i), LineTrans) AndAlso Not String.IsNullOrEmpty(LineTrans) Then
                    Lines(i) = LineTrans
                    AnyChanged = True
                End If
            Next
            If AnyChanged Then Return String.Join(vbCrLf, Lines)
        End If
        Return Src
    End Function

    ''' <summary>根据设置值（索引字符串）应用语言。</summary>
    Public Sub SetLangByIndex(Value As Object)
        Dim Index As Integer = 0
        Try
            Index = Convert.ToInt32(Value)
        Catch ex As Exception
            Index = 0
        End Try
        If Index < 0 OrElse Index >= Languages.Count Then Index = 0
        Dim NewLang = Languages(Index).Code
        If NewLang = CurrentLang Then Return
        CurrentLang = NewLang
    End Sub

    ''' <summary>语言设置改变时调用：重新翻译已显示的界面。</summary>
    Private IsTranslating As Boolean = False

    ''' <summary>语言设置的注册表位置：三个通道（正式/快照/内测）共用同一份，改一处三通道同时生效。</summary>
    Private ReadOnly LangRegPath As String = $"HKEY_CURRENT_USER\Software\{RegFolder}"

    ''' <summary>读取语言索引：优先注册表（三通道共享），没有则回退本通道的 Setup.ini。</summary>
    Public Function LoadLanguageIndex() As Integer
        Try
            If RegistryUtils.Has(LangRegPath, "SystemLanguage") Then
                Dim I As Integer
                Dim V As Object = RegistryUtils.TryRead(LangRegPath, "SystemLanguage", Nothing)
                If V IsNot Nothing AndAlso Integer.TryParse(V.ToString(), I) Then Return I
            End If
        Catch ex As Exception
        End Try
        Try
            Return Convert.ToInt32(Settings.Get("SystemLanguage"))
        Catch ex As Exception
            Return 0
        End Try
    End Function

    ''' <summary>语言索引落盘到注册表（三通道共享）。</summary>
    Public Sub SaveLanguageIndex(Index As Integer)
        Try
            RegistryUtils.Write(LangRegPath, "SystemLanguage", Index)
        Catch ex As Exception
            Logger.Warn(ex, "[Lang] 写入共享语言设置失败")
        End Try
    End Sub

    Public Sub OnLanguageSettingChanged(Value As Object)
        SetLangByIndex(Value)
        '★ 写注册表：另外两个通道下次启动时也读这份设置，实现三通道语言互通
        Try
            SaveLanguageIndex(Convert.ToInt32(Value))
        Catch ex As Exception
        End Try
        '★ 不能在 SelectionChanged 事件管线里同步重翻整个窗口：会阻塞输入、干扰下拉框收起流程。
        '  延迟到本轮布局/输入处理完（Background 优先级）再执行，并加防重入保护。
        If IsTranslating Then Return
        IsTranslating = True
        Application.Current.Dispatcher.InvokeAsync(
            Sub()
                Try
                    TranslateWindow()
                    '★ 已经离开可视树的缓存页面（Frame 导航历史）也要跟着换，
                    '  否则切回中文时那些页面会一直停在英文——这就是「改回来部分页面不还原」的原因。
                    RetranslateTracked()
                Catch ex As Exception
                    Logger.Warn(ex, "[Lang] 应用语言失败")
                Finally
                    IsTranslating = False
                End Try
                '通知页面刷新自行维护的动态文案（如「自动设置为 X」）
                Try
                    RaiseEvent LanguageChanged(Nothing, EventArgs.Empty) 'Module 中不能用 Me，事件 sender 传 Nothing
                Catch ex As Exception
                End Try
            End Sub, System.Windows.Threading.DispatcherPriority.Background)
    End Sub

    ''' <summary>
    ''' 语言设置原先存在启动器目录的 Setup.ini（三个通道各存一份），现改为注册表（HKCU\Software\PCLDebug）。
    ''' 首次升级时把 ini 里的旧值迁到注册表，避免用户选的语言被重置成中文。
    ''' </summary>
    Public Sub MigrateLanguageSetting()
        Try
            If RegistryUtils.Has($"HKEY_CURRENT_USER\Software\{RegFolder}", "SystemLanguage") Then Return
            Dim Old As String = ReadIni("Setup", "SystemLanguage", "")
            Dim Idx As Integer = 0
            If Not String.IsNullOrWhiteSpace(Old) AndAlso Integer.TryParse(Old, Idx) AndAlso Idx > 0 Then
                RegistryUtils.Write($"HKEY_CURRENT_USER\Software\{RegFolder}", "SystemLanguage", Old)
                Logger.Info($"[Lang] 已将语言设置从 Setup.ini 迁移到注册表：{Old}")
            End If
        Catch ex As Exception
            Logger.Warn(ex, "[Lang] 迁移语言设置失败")
        End Try
    End Sub

#End Region

#Region "原文记忆（支持任意次切换）"

    ''' <summary>
    ''' ★ 换语言的关键：第一次翻译时把「中文原文」存进弱引用表，
    ''' 之后每次切换都按原文查词典翻译（否则翻译后的英文文本查不中词典，导致只能切换一次）。
    ''' 切回中文时直接还原原文。
    ''' </summary>
    Private ReadOnly OrigStore As New System.Runtime.CompilerServices.ConditionalWeakTable(Of Object, Object)

    Private Function GetOrigMap(Fe As Object) As Dictionary(Of String, String)
        Dim M As Dictionary(Of String, String) = Nothing
        If Not OrigStore.TryGetValue(Fe, M) OrElse M Is Nothing Then
            M = New Dictionary(Of String, String)
            OrigStore.Remove(Fe)
            OrigStore.Add(Fe, M)
        End If
        Return M
    End Function

    ''' <summary>按属性名取「应显示的文本」：中文时还原原文，其他语言时按原文查词典。</summary>
    Private Function Trans(Fe As Object, Prop As String, Current As String) As String
        Try
            Dim M = GetOrigMap(Fe)
            If Not M.ContainsKey(Prop) Then M(Prop) = Current
            Dim Orig = M(Prop)
            If CurrentLang = "zh_CN" Then Return Orig
            If IsLangName(Orig) Then Return Orig '语言名永远原样
            Return T(Orig)
        Catch ex As Exception
            Return Current
        End Try
    End Function

    ''' <summary>该属性是否需要处理：当前值含中文，或此前已记录过原文（说明翻过，需跟随语言切换）。</summary>
    Private Function ShouldTouch(Owner As Object, Prop As String, Current As String) As Boolean
        If String.IsNullOrEmpty(Current) Then Return False
        '单个汉字基本是人名缩写 / 单字符号（关于页的赞助者名单），不参与翻译
        If Current.Length = 1 Then Return False
        If HasCJK(Current) Then Return True
        Dim M As Dictionary(Of String, String) = Nothing
        Return OrigStore.TryGetValue(Owner, M) AndAlso M IsNot Nothing AndAlso M.ContainsKey(Prop)
    End Function

    ''' <summary>
    ''' 翻译并写回一个字符串属性；值没变化时绝不写，避免巡检反复触发重排。
    ''' Setter 第一个参数是元素本身（不能闭包捕获元素，否则追踪表会强引用它导致页面无法回收）。
    ''' </summary>
    Private Sub ApplyText(Owner As Object, Prop As String, Current As String, Setter As Action(Of Object, String))
        Try
            If Not ShouldTouch(Owner, Prop, Current) Then Return
            Track(Owner, Prop, Setter)
            Dim NewVal As String = Trans(Owner, Prop, Current)
            If Not String.IsNullOrEmpty(NewVal) AndAlso NewVal <> Current Then Setter(Owner, NewVal)
        Catch ex As Exception
        End Try
    End Sub

#End Region

#Region "追踪表（切回中文时覆盖不可见页面）"

    ''' <summary>
    ''' 被翻译过的元素登记表（弱引用）。
    ''' 为什么需要：页面离开后实例可能还活在 Frame 的导航缓存里、却不在可视树上，
    ''' 整窗遍历够不到它；切回中文时那些页面就会一直停在英文。这里逐个重刷一遍即可。
    ''' </summary>
    Private Class TrackedItem
        Public WeakEl As WeakReference
        Public Prop As String
        Public Setter As Action(Of Object, String)
    End Class

    Private ReadOnly Tracked As New List(Of TrackedItem)

    Private Sub Track(Owner As Object, Prop As String, Setter As Action(Of Object, String))
        Try
            Dim M = GetOrigMap(Owner)
            Dim Key As String = ChrW(1) & "tracked:" & Prop
            If M.ContainsKey(Key) Then Return '同一元素同一属性只登记一次
            M(Key) = "1"
            SyncLock Tracked
                Tracked.Add(New TrackedItem With {.WeakEl = New WeakReference(Owner), .Prop = Prop, .Setter = Setter})
            End SyncLock
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>把所有登记过的元素按当前语言重刷一遍（含不在可视树上的缓存页面）。</summary>
    Public Sub RetranslateTracked()
        SyncLock Tracked
            For i As Integer = Tracked.Count - 1 To 0 Step -1
                Dim It = Tracked(i)
                Dim El As Object = Nothing
                Try
                    El = It.WeakEl.Target
                Catch ex As Exception
                End Try
                If El Is Nothing Then
                    Tracked.RemoveAt(i) '元素已被回收，清掉
                    Continue For
                End If
                Try
                    Dim M As Dictionary(Of String, String) = Nothing
                    If Not OrigStore.TryGetValue(El, M) OrElse M Is Nothing Then Continue For
                    Dim Orig As String = Nothing
                    If Not M.TryGetValue(It.Prop, Orig) Then Continue For
                    Dim NewVal As String = If(CurrentLang = "zh_CN", Orig, T(Orig))
                    If Not String.IsNullOrEmpty(NewVal) Then It.Setter(El, NewVal)
                Catch ex As Exception
                End Try
            Next
        End SyncLock
    End Sub

#End Region

#Region "可视化树翻译"

    ''' <summary>翻译元素的工具提示（字符串形式）。</summary>
    Private Sub TranslateToolTip(Fe As FrameworkElement)
        Try
            Dim Tip = ToolTipService.GetToolTip(Fe)
            Dim S = TryCast(Tip, String)
            If S Is Nothing Then Return
            ApplyText(Fe, "tooltip", S, Sub(E, V) ToolTipService.SetToolTip(DirectCast(E, FrameworkElement), V))
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>反射查找 Text 属性的结果缓存（巡检每 800ms 扫整窗，不缓存会白白浪费 CPU）。</summary>
    Private ReadOnly TextPropCache As New Dictionary(Of Type, Reflection.PropertyInfo)

    ''' <summary>排除在自动翻译之外的元素（文案由代码自行维护，例如「自动设置为 X」）。</summary>
    Private ReadOnly Ignored As New System.Runtime.CompilerServices.ConditionalWeakTable(Of Object, Object)

    Public Sub IgnoreElement(El As Object)
        If El Is Nothing Then Return
        Try
            Ignored.Remove(El)
            Ignored.Add(El, True)
        Catch ex As Exception
        End Try
    End Sub

    Private Function IsIgnored(El As Object) As Boolean
        Try
            Dim Dummy As Object = Nothing
            Return Ignored.TryGetValue(El, Dummy)
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Function GetTextProp(T As Type) As Reflection.PropertyInfo
        SyncLock TextPropCache
            Dim P As Reflection.PropertyInfo = Nothing
            If TextPropCache.TryGetValue(T, P) Then Return P
            P = T.GetProperty("Text", GetType(String))
            TextPropCache(T) = P
            Return P
        End SyncLock
    End Function

    ''' <summary>注册全局 Loaded 处理器 + 兜底巡检定时器，在启动时调用一次。</summary>
    Public Sub RegisterGlobalHook()
        EventManager.RegisterClassHandler(GetType(FrameworkElement), FrameworkElement.LoadedEvent,
            New RoutedEventHandler(AddressOf OnElementLoaded))
        StartWatchdog()
    End Sub

    Private Sub OnElementLoaded(Sender As Object, E As RoutedEventArgs)
        '★ 不能因为页面切换动画进行中（AniControlEnabled <> 0）就跳过：
        '  页面在动画期间加载时 Loaded 只触发一次，一旦跳过就永久保持中文。
        If CurrentLang = "zh_CN" Then Return
        Dim Fe = TryCast(Sender, FrameworkElement)
        If Fe Is Nothing Then Return
        Try
            If TypeOf Fe Is Window OrElse TypeOf Fe Is Page OrElse TypeOf Fe Is MyCard Then
                TranslateTree(Fe)
            Else
                TranslateElement(Fe)
            End If
        Catch ex As Exception
            '翻译失败不影响正常功能
        End Try
    End Sub

    ''' <summary>整窗翻译：主可视树 + 标题栏（标题栏按钮不在 PanMain 下）。</summary>
    Public Sub TranslateWindow()
        If FrmMain Is Nothing Then Return
        TranslateTree(FrmMain)
        If FrmMain.PanTitleSelect IsNot Nothing Then
            For Each Child As Object In FrmMain.PanTitleSelect.Children
                Try
                    TranslateTree(Child)
                Catch ex As Exception
                End Try
            Next
        End If
    End Sub

    ''' <summary>
    ''' 兜底巡检：界面文本可能被代码在翻译之后重新写回中文（页面刷新、标题栏重设、动态状态文字等），
    ''' 用低频定时器再扫一遍，保证切到其他语言后界面不会残留中文。
    ''' </summary>
    Private Watchdog As DispatcherTimer

    Private Sub StartWatchdog()
        If Watchdog IsNot Nothing Then Return
        Watchdog = New DispatcherTimer(DispatcherPriority.Background) With {.Interval = TimeSpan.FromMilliseconds(800)}
        AddHandler Watchdog.Tick,
            Sub()
                Try
                    If CurrentLang = "zh_CN" OrElse IsTranslating Then Return
                    If FrmMain Is Nothing OrElse FrmMain.PanMain Is Nothing Then Return
                    If FrmMain.WindowState = WindowState.Minimized Then Return
                    IsTranslating = True
                    TranslateWindow()
                Catch ex As Exception
                Finally
                    IsTranslating = False
                End Try
            End Sub
        Watchdog.Start()
    End Sub

    ''' <summary>递归翻译一棵可视树。</summary>
    Public Sub TranslateTree(Root As DependencyObject)
        Try
            TranslateElement(Root)
        Catch ex As Exception
        End Try
        Dim Count As Integer
        Try
            Count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(Root)
        Catch ex As Exception
            Return '不是 Visual 的节点（如 ContentElement）不参与遍历，也不能中断整轮翻译
        End Try
        For i As Integer = 0 To Count - 1
            Try
                TranslateTree(System.Windows.Media.VisualTreeHelper.GetChild(Root, i))
            Catch ex As Exception
            End Try
        Next
    End Sub

    ''' <summary>翻译单个元素自身携带的文本（不动输入框内容等动态值）。</summary>
    Public Sub TranslateElement(El As Object)
        Dim Fe = TryCast(El, FrameworkElement)
        If Fe Is Nothing Then Return
        If IsIgnored(Fe) Then Return

        '1. 工具提示
        TranslateToolTip(Fe)

        Try
            '2. TextBlock
            '★ 关键：XAML 里写 Text="中文" 时，Inlines 集合已经带了一个隐式 Run（Count=1）。
            '   旧逻辑要求 Count=0 才翻译 → 所有 TextBlock 全被跳过，这是界面残留中文的主因。
            Dim Tb = TryCast(El, TextBlock)
            If Tb IsNot Nothing Then
                If Tb.Inlines.Count <= 1 Then
                    ApplyText(Tb, "text", Tb.Text, Sub(E, V) DirectCast(E, TextBlock).Text = V)
                Else
                    '多 Run 富文本：逐段翻译，保留原有格式
                    For Each Inl As Inline In Tb.Inlines
                        Dim R = TryCast(Inl, Run)
                        If R IsNot Nothing Then ApplyText(R, "text", R.Text, Sub(E, V) DirectCast(E, Run).Text = V)
                    Next
                End If
                Return
            End If

            '3. 卡片 / 列表项
            Dim Card = TryCast(El, MyCard)
            If Card IsNot Nothing Then ApplyText(Card, "title", Card.Title, Sub(E, V) DirectCast(E, MyCard).Title = V)

            Dim ListItem = TryCast(El, MyListItem)
            If ListItem IsNot Nothing Then
                ApplyText(ListItem, "title", ListItem.Title, Sub(E, V) DirectCast(E, MyListItem).Title = V)
                ApplyText(ListItem, "info", ListItem.Info, Sub(E, V) DirectCast(E, MyListItem).Info = V)
            End If

            '搜索框：只翻提示文字，Text 是用户输入
            Dim SearchBox = TryCast(El, MySearchBox)
            If SearchBox IsNot Nothing Then ApplyText(SearchBox, "hint", SearchBox.HintText, Sub(E, V) DirectCast(E, MySearchBox).HintText = V)

            '4. 输入框 / 下拉框：只翻提示文字，绝不动 Text（用户输入、文件路径等）
            Dim MtBox = TryCast(El, MyTextBox)
            If MtBox IsNot Nothing Then
                ApplyText(MtBox, "hint", MtBox.HintText, Sub(E, V) DirectCast(E, MyTextBox).HintText = V)
                Return
            End If

            Dim McBox = TryCast(El, MyComboBox)
            If McBox IsNot Nothing Then
                ApplyText(McBox, "hint", McBox.HintText, Sub(E, V) DirectCast(E, MyComboBox).HintText = V)
                '★ 下拉项在弹出层（Popup）里，折叠时不生成容器、不在可视树上，
                '  必须直接遍历 Items 集合，否则下拉框选项永远是中文。
                Try
                    For Each Item As Object In McBox.Items
                        Dim Ci = TryCast(Item, ContentControl)
                        If Ci IsNot Nothing Then
                            Dim ItemText = TryCast(Ci.Content, String)
                            If ItemText IsNot Nothing Then
                                ApplyText(Ci, "content", ItemText, Sub(E, V) DirectCast(E, ContentControl).Content = V)
                            Else
                                Dim ItemTb = TryCast(Ci.Content, TextBlock)
                                If ItemTb IsNot Nothing Then ApplyText(ItemTb, "text", ItemTb.Text, Sub(E, V) DirectCast(E, TextBlock).Text = V)
                            End If
                            Dim ItemFe = TryCast(Ci, FrameworkElement)
                            If ItemFe IsNot Nothing Then TranslateToolTip(ItemFe)
                        End If
                    Next
                Catch ex As Exception
                End Try
                Return
            End If

            '5. 菜单 / 分组标题
            Dim Mmi = TryCast(El, MenuItem)
            If Mmi IsNot Nothing Then
                Dim HeaderText = TryCast(Mmi.Header, String)
                If HeaderText IsNot Nothing Then ApplyText(Mmi, "header", HeaderText, Sub(E, V) DirectCast(E, MenuItem).Header = V)
                Return
            End If
            Dim Hcc = TryCast(El, HeaderedContentControl)
            If Hcc IsNot Nothing Then
                Dim HeaderText = TryCast(Hcc.Header, String)
                If HeaderText IsNot Nothing Then ApplyText(Hcc, "header", HeaderText, Sub(E, V) DirectCast(E, HeaderedContentControl).Header = V)
            End If

            '6. PCL 自定义控件（文字在自定义的 Text 属性里，不在 Content 中）
            '   ★ 必须用白名单：MySearchBox 也有 Text 属性，但那是用户输入的搜索词，翻了会毁掉搜索。
            If TypeOf El Is MyButton OrElse TypeOf El Is MyTextButton OrElse
               TypeOf El Is MyExtraTextButton OrElse TypeOf El Is MyIconTextButton OrElse
               TypeOf El Is MyCheckBox OrElse TypeOf El Is MyRadioButton OrElse
               TypeOf El Is MyRadioBox OrElse TypeOf El Is MyHint OrElse
               TypeOf El Is MyLoading Then
                Dim TextProp = GetTextProp(El.GetType())
                If TextProp IsNot Nothing AndAlso TextProp.CanRead AndAlso TextProp.CanWrite Then
                    Dim S = TryCast(TextProp.GetValue(El, Nothing), String)
                    If S IsNot Nothing Then ApplyText(Fe, "text", S, Sub(E, V) TextProp.SetValue(E, V, Nothing))
                End If
            End If

            '7. 通用内容控件（Button / Label / ComboBoxItem ...）
            Dim Cc = TryCast(El, ContentControl)
            If Cc IsNot Nothing Then
                Dim S = TryCast(Cc.Content, String)
                If S IsNot Nothing Then ApplyText(Cc, "content", S, Sub(E, V) DirectCast(E, ContentControl).Content = V)
            End If
        Catch ex As Exception
        End Try
    End Sub

#End Region

End Module
