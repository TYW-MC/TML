Public Class PageOtherAbout

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherAbout_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        ItemAboutPcl.Info = ItemAboutPcl.Info.Replace("%VERSION%", VersionDisplay).Replace("%VERSIONCODE%", VersionCode).Replace("%BRANCH%", CInt(BuildType))
        '赞助者栏目已删除（识别码入口搬到 设置 - 其他 - 启动器）
        'XAML 走的是预生成的 BAML，改 XAML 不会生效，所以这里在运行期把整张卡片从页面上摘掉
        Try
            For i As Integer = PanMain.Children.Count - 1 To 0 Step -1
                Dim Card = TryCast(PanMain.Children(i), MyCard)
                If Card IsNot Nothing AndAlso Card.Title = "赞助者" Then PanMain.Children.RemoveAt(i)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "移除关于页赞助者栏目失败")
        End Try

        '在「关于」「特别鸣谢」两张卡片底部加上改版说明
        Try
            For Each Child As UIElement In PanMain.Children
                Dim Card = TryCast(Child, MyCard)
                If Card Is Nothing Then Continue For
                If Card.Title = "关于" OrElse Card.Title = "特别鸣谢" Then AppendReworkLine(Card)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "添加改版说明失败")
        End Try

        '特别鸣谢里的 PCL 统一写成「原PCL」；「PCL 内群的各位」这一条保持原样
        Try
            For Each Child As UIElement In PanMain.Children
                Dim Card = TryCast(Child, MyCard)
                If Card Is Nothing OrElse Card.Title <> "特别鸣谢" Then Continue For
                RenameCreditPcl(Card)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "修改鸣谢文案失败")
        End Try

    End Sub

    ''' <summary>把鸣谢条目里的 PCL 写成「原PCL」，「PCL 内群的各位」这一条保持原样。</summary>
    Private Sub RenameCreditPcl(Card As MyCard)
        For Each Item In FindListItems(Card.BorderChild)
            If Item.Title = "PCL 内群的各位" Then Continue For
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
