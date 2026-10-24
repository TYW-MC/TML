Public Class PageOtherAbout

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherAbout_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then
            RefreshVersionLine() '语言可能在别处被改过，回来时刷新一次
            Return
        End If
        IsLoaded = True

        AddHandler ModLang.LanguageChanged, AddressOf OnLanguageChanged
        RefreshVersionLine()

        '赞助者栏目已删除（识别码入口搬到 设置 - 其他 - 启动器）
        'XAML 走的是预生成的 BAML，改 XAML 不会生效，所以这里在运行期把整张卡片从页面上摘掉
        Try
            For i As Integer = PanMain.Children.Count - 1 To 0 Step -1
                Dim Card = TryCast(PanMain.Children(i), MyCard)
                If Card IsNot Nothing AndAlso IsCardTitle(Card, "赞助者") Then PanMain.Children.RemoveAt(i)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "移除关于页赞助者栏目失败")
        End Try

        '在「关于」「特别鸣谢」两张卡片底部加上改版说明
        Try
            For Each Child As UIElement In PanMain.Children
                Dim Card = TryCast(Child, MyCard)
                If Card Is Nothing Then Continue For
                If IsCardTitle(Card, "关于") OrElse IsCardTitle(Card, "特别鸣谢") Then AppendReworkLine(Card)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "添加改版说明失败")
        End Try

        '在「关于」卡片底部加上 Mojang 使用指南要求的非官方声明
        Try
            For Each Child As UIElement In PanMain.Children
                Dim Card = TryCast(Child, MyCard)
                If Card Is Nothing OrElse Not IsCardTitle(Card, "关于") Then Continue For
                AppendDisclaimerLine(Card)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "添加非官方声明失败")
        End Try

        '特别鸣谢里的 PCL 统一写成「原PCL」；「PCL 内群的各位」这一条保持原样
        Try
            For Each Child As UIElement In PanMain.Children
                Dim Card = TryCast(Child, MyCard)
                If Card Is Nothing OrElse Not IsCardTitle(Card, "特别鸣谢") Then Continue For
                RenameCreditPcl(Card)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "修改鸣谢文案失败")
        End Try

    End Sub

    Private Sub PageOtherAbout_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        Try
            RemoveHandler ModLang.LanguageChanged, AddressOf OnLanguageChanged
        Catch ex As Exception
        End Try
    End Sub

    Private Sub OnLanguageChanged(sender As Object, e As EventArgs)
        RefreshVersionLine()
    End Sub

    ''' <summary>
    ''' 当前版本那一行：模板串和通道名都要过翻译。
    ''' 以前只翻了模板、%VERSION% 直接塞 VersionDisplay，结果英文界面里冒出「Current version: 正式版 1.1.15」。
    ''' </summary>
    Private Sub RefreshVersionLine()
        Try
            Dim VerName As String = ModLang.T(BuildTypeDisplay) & " " & VersionBaseName
            ItemAboutPcl.Info = ModLang.T("当前版本：%VERSION%（%VERSIONCODE%.%BRANCH%）").
                Replace("%VERSION%", VerName).
                Replace("%VERSIONCODE%", VersionCode).
                Replace("%BRANCH%", CInt(BuildType))
            '这一行由本页自己维护，别让巡检和追踪表再插手（否则会按中文原文又翻一遍）
            ModLang.IgnoreElement(ItemAboutPcl)
        Catch ex As Exception
            Logger.Warn(ex, "刷新版本行失败")
        End Try
    End Sub

    ''' <summary>卡片标题比较：既认中文原文，也认翻译后的文本（切到英文后标题已经不是中文了）。</summary>
    Private Function IsCardTitle(Card As MyCard, Chinese As String) As Boolean
        If Card Is Nothing Then Return False
        If Card.Title = Chinese Then Return True
        Dim Tr As String = ModLang.T(Chinese)
        Return Tr <> Chinese AndAlso Card.Title = Tr
    End Function

    ''' <summary>把鸣谢条目里的 PCL 写成「原PCL」，「PCL 内群的各位」这一条保持原样。</summary>
    Private Sub RenameCreditPcl(Card As MyCard)
        Dim KeepTitle As String = ModLang.T("PCL 内群的各位")
        For Each Item In FindListItems(Card.BorderChild)
            If Item.Title = "PCL 内群的各位" OrElse Item.Title = KeepTitle Then Continue For
            Dim NewTitle As String = ReplacePclWord(Item.Title)
            If NewTitle <> Item.Title Then Item.Title = NewTitle
            Dim NewInfo As String = ReplacePclWord(Item.Info)
            If NewInfo <> Item.Info Then Item.Info = NewInfo
        Next
    End Sub

    ''' <summary>递归找出控件树里的全部 MyListItem。</summary>
    Private Function FindListItems(Root As DependencyObject) As List(Of MyListItem)
        Dim Result As New List(Of MyListItem)
        If Root Is Nothing Then Return Result
        Dim Pending As New Stack(Of DependencyObject)
        Pending.Push(Root)
        Do While Pending.Count > 0
            Dim Node = Pending.Pop()
            Dim Item = TryCast(Node, MyListItem)
            If Item IsNot Nothing Then Result.Add(Item)
            For Each Child As Object In LogicalTreeHelper.GetChildren(Node)
                Dim Dep = TryCast(Child, DependencyObject)
                If Dep IsNot Nothing Then Pending.Push(Dep)
            Next
        Loop
        Return Result
    End Function

    ''' <summary>把独立的 PCL 换成「原PCL」，已经是「原PCL」的不重复叠加。</summary>
    Private Function ReplacePclWord(Text As String) As String
        If String.IsNullOrEmpty(Text) Then Return Text
        Return System.Text.RegularExpressions.Regex.Replace(Text, "(?<!原)(?<![A-Za-z0-9])PCL(?![A-Za-z0-9])", "原PCL")
    End Function

    ''' <summary>在卡片内容底部追加一行「根据 PCL 改版」。</summary>
    Private Sub AppendReworkLine(Card As MyCard)
        Dim Old = Card.BorderChild
        If Old Is Nothing Then Return
        Dim Host As New StackPanel
        Card.BorderChild = Nothing
        Host.Children.Add(Old)
        Host.Children.Add(New TextBlock With {
            .Text = "根据 PCL 改版",
            .FontSize = 12,
            .Margin = New Thickness(21, 2, 21, 12),
            .HorizontalAlignment = HorizontalAlignment.Right,
            .Foreground = New SolidColorBrush(Color.FromRgb(150, 150, 150))})
        Card.BorderChild = Host
    End Sub

    ''' <summary>在卡片底部追加 Mojang 使用指南要求的非官方声明。</summary>
    Private Sub AppendDisclaimerLine(Card As MyCard)
        Dim Host = TryCast(Card.BorderChild, StackPanel)
        If Host Is Nothing Then Return
        Host.Children.Add(New TextBlock With {
            .Text = "NOT AN OFFICIAL MINECRAFT PRODUCT. NOT APPROVED BY OR ASSOCIATED WITH MOJANG OR MICROSOFT.",
            .FontSize = 10,
            .TextWrapping = TextWrapping.Wrap,
            .Margin = New Thickness(21, 0, 21, 12),
            .HorizontalAlignment = HorizontalAlignment.Right,
            .Foreground = New SolidColorBrush(Color.FromRgb(150, 150, 150))})
    End Sub

    Public Shared Sub CopyIdentify() Handles BtnDonateCopy.Click
        ClipboardSet(DeviceCode)
    End Sub
    Private Sub BtnDonateCodeInput_Click() Handles BtnDonateInput.Click
        InputPotatoCode(False)
    End Sub
    Private Sub BtnDonateOutput_Click(sender As Object, e As EventArgs) Handles BtnDonateOutput.Click
        GeneratePotatoCode()
    End Sub
    Private Sub BtnAboutUpdate_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAboutUpdate.Click
        UpdateCheckByButton()
    End Sub

End Class
