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
        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

    ''' <summary>全部支持的语言（顺序即设置页下拉框顺序，索引即存储值）。</summary>
    Public ReadOnly Languages As New List(Of LangInfo) From {
        New LangInfo With {.Code = "zh_CN", .Name = "简体中文（默认）"},
        New LangInfo With {.Code = "en_US", .Name = "English"},
        New LangInfo With {.Code = "ja_JP", .Name = "日本語"},
        New LangInfo With {.Code = "ru_RU", .Name = "Русский"},
        New LangInfo With {.Code = "id_ID", .Name = "Bahasa Indonesia"}
    }

    ''' <summary>当前语言代码（默认中文）。</summary>
    Public CurrentLang As String = "zh_CN"

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

    Public Sub OnLanguageSettingChanged(Value As Object)
        SetLangByIndex(Value)
        '★ 不能在 SelectionChanged 事件管线里同步重翻整个窗口：会阻塞输入、干扰下拉框收起流程。
        '  延迟到本轮布局/输入处理完（Background 优先级）再执行，并加防重入保护。
        If IsTranslating Then Return
        IsTranslating = True
        Application.Current.Dispatcher.BeginInvoke(
            Sub()
                Try
                    TranslateWindow()
                Catch ex As Exception
                    Logger.Warn(ex, "[Lang] 应用语言失败")
                Finally
                    IsTranslating = False
                End Try
            End Sub, System.Windows.Threading.DispatcherPriority.Background)
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

    ''' <summary>翻译并写回一个字符串属性；值没变化时绝不写，避免巡检反复触发重排。</summary>
    Private Sub ApplyText(Owner As Object, Prop As String, Current As String, Setter As Action(Of String))
        Try
            If Not ShouldTouch(Owner, Prop, Current) Then Return
            Dim NewVal As String = Trans(Owner, Prop, Current)
            If Not String.IsNullOrEmpty(NewVal) AndAlso NewVal <> Current Then Setter(NewVal)
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>翻译元素的工具提示（字符串形式）。</summary>
    Private Sub TranslateToolTip(Fe As FrameworkElement)
        Try
            Dim Tip = ToolTipService.GetToolTip(Fe)
            Dim S = TryCast(Tip, String)
            If S Is Nothing Then Return
            ApplyText(Fe, "tooltip", S, Sub(V) ToolTipService.SetToolTip(Fe, V))
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>反射查找 Text 属性的结果缓存（巡检每 800ms 扫整窗，不缓存会白白浪费 CPU）。</summary>
    Private ReadOnly TextPropCache As New Dictionary(Of Type, Reflection.PropertyInfo)

    Private Function GetTextProp(T As Type) As Reflection.PropertyInfo
        SyncLock TextPropCache
            Dim P As Reflection.PropertyInfo = Nothing
            If TextPropCache.TryGetValue(T, P) Then Return P
            P = T.GetProperty("Text", GetType(String))
            TextPropCache(T) = P
            Return P
        End SyncLock
    End Function

#End Region

#Region "可视化树翻译"

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

        '1. 工具提示
        TranslateToolTip(Fe)

        Try
            '2. TextBlock
            '★ 关键：XAML 里写 Text="中文" 时，Inlines 集合已经带了一个隐式 Run（Count=1）。
            '   旧逻辑要求 Count=0 才翻译 → 所有 TextBlock 全被跳过，这是界面残留中文的主因。
            Dim Tb = TryCast(El, TextBlock)
            If Tb IsNot Nothing Then
                If Tb.Inlines.Count <= 1 Then
                    ApplyText(Tb, "text", Tb.Text, Sub(V) Tb.Text = V)
                Else
                    '多 Run 富文本：逐段翻译，保留原有格式
                    For Each Inl As Inline In Tb.Inlines
                        Dim R = TryCast(Inl, Run)
                        If R IsNot Nothing Then ApplyText(R, "text", R.Text, Sub(V) R.Text = V)
                    Next
                End If
                Return
            End If

            '3. 卡片 / 列表项
            Dim Card = TryCast(El, MyCard)
            If Card IsNot Nothing Then ApplyText(Card, "title", Card.Title, Sub(V) Card.Title = V)

            Dim ListItem = TryCast(El, MyListItem)
            If ListItem IsNot Nothing Then
                ApplyText(ListItem, "title", ListItem.Title, Sub(V) ListItem.Title = V)
                ApplyText(ListItem, "info", ListItem.Info, Sub(V) ListItem.Info = V)
            End If

            '搜索框：只翻提示文字，Text 是用户输入
            Dim SearchBox = TryCast(El, MySearchBox)
            If SearchBox IsNot Nothing Then ApplyText(SearchBox, "hint", SearchBox.HintText, Sub(V) SearchBox.HintText = V)

            '4. 输入框 / 下拉框：只翻提示文字，绝不动 Text（用户输入、文件路径等）
            Dim MtBox = TryCast(El, MyTextBox)
            If MtBox IsNot Nothing Then
                ApplyText(MtBox, "hint", MtBox.HintText, Sub(V) MtBox.HintText = V)
                Return
            End If

            Dim McBox = TryCast(El, MyComboBox)
            If McBox IsNot Nothing Then
                ApplyText(McBox, "hint", McBox.HintText, Sub(V) McBox.HintText = V)
                '★ 下拉项在弹出层（Popup）里，折叠时不生成容器、不在可视树上，
                '  必须直接遍历 Items 集合，否则下拉框选项永远是中文。
                Try
                    For Each Item As Object In McBox.Items
                        Dim Ci = TryCast(Item, ContentControl)
                        If Ci IsNot Nothing Then
                            Dim ItemText = TryCast(Ci.Content, String)
                            If ItemText IsNot Nothing Then
                                ApplyText(Ci, "content", ItemText, Sub(V) Ci.Content = V)
                            Else
                                Dim ItemTb = TryCast(Ci.Content, TextBlock)
                                If ItemTb IsNot Nothing Then ApplyText(ItemTb, "text", ItemTb.Text, Sub(V) ItemTb.Text = V)
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
                If HeaderText IsNot Nothing Then ApplyText(Mmi, "header", HeaderText, Sub(V) Mmi.Header = V)
                Return
            End If
            Dim Hcc = TryCast(El, HeaderedContentControl)
            If Hcc IsNot Nothing Then
                Dim HeaderText = TryCast(Hcc.Header, String)
                If HeaderText IsNot Nothing Then ApplyText(Hcc, "header", HeaderText, Sub(V) Hcc.Header = V)
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
                    If S IsNot Nothing Then ApplyText(Fe, "text", S, Sub(V) TextProp.SetValue(El, V, Nothing))
                End If
            End If

            '7. 通用内容控件（Button / Label / ComboBoxItem ...）
            Dim Cc = TryCast(El, ContentControl)
            If Cc IsNot Nothing Then
                Dim S = TryCast(Cc.Content, String)
                If S IsNot Nothing Then ApplyText(Cc, "content", S, Sub(V) Cc.Content = V)
            End If
        Catch ex As Exception
        End Try
    End Sub

#End Region

End Module
