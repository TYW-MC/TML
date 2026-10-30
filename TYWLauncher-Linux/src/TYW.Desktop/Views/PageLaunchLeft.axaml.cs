using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using TYW.Desktop.Controls;
using TYW;

namespace TYW.Desktop.Views;

public partial class PageLaunchLeft : UserControl
{
    private MainWindow? _main;
    private PageLaunchRight? _right;
    private McVersionItem? _selected;
    private bool _init;

    /// <summary>离线页的玩家名输入框（对应 PageLoginLegacy 的 ComboName）。</summary>
    private ComboBox? _comboName;

    public PageLaunchLeft()
    {
        InitializeComponent();

        RadioLoginType5.Click += (_, _) => SetLoginType(5);
        RadioLoginType0.Click += (_, _) => SetLoginType(0);
        BtnLaunch.Click += (_, _) => OnLaunchClick();
        BtnVersion.Click += (_, _) => _main?.SwitchPage(1);

        Loaded += (_, _) => Build();
    }

    public void Attach(MainWindow main, PageLaunchRight right)
    {
        _main = main;
        _right = right;
    }

    private void Build()
    {
        if (_init) return;
        _init = true;

        SetLoginType(ModSettings.GetInt("LoginType", 0) == 5 ? 5 : 0);
        BuildLoginArea();
        RefreshVersion();
    }

    /// <summary>正版 / 离线切换：对应 PageLaunchLeft 的 RadioLoginType5 / 0。</summary>
    private void SetLoginType(int type)
    {
        RadioLoginType5.NavChecked = type == 5;
        RadioLoginType0.NavChecked = type == 0;
        ModSettings.SetValue("LoginType", type.ToString());
        ModSettings.Save();
        if (_init) BuildLoginArea();
    }

    private void RefreshVersion()
    {
        var list = ModVersion.ListVersions();
        _selected = ModVersion.SelectedOrDefault();
        if (_selected is null)
        {
            LabVersion.Text = "未找到任何版本，请先在下载页安装";
            BtnLaunch.Text = "开始游戏";
            BtnLaunch.IsEnabledButton = false;
            return;
        }
        LabVersion.Text = _selected.Id;
        BtnLaunch.Text = "开始游戏";
        BtnLaunch.IsEnabledButton = true;
    }

    // ===================== PanLogin：对应 PageLoginMs / PageLoginLegacy =====================

    private void BuildLoginArea()
    {
        PanLogin.Children.Clear();
        PanLogin.Children.Add(RadioLoginType5.NavChecked ? BuildMsPage() : BuildLegacyPage());
    }

    /// <summary>
    /// 对应 Pages/PageLaunch/PageLoginMs.xaml（正版未登录）：
    /// Grid Margin 0,10,0,0，列 * / Auto，行 Auto / 25 / Auto / Auto
    ///   ├ 人形图标（Width 40，ColorBrush2，Opacity 0.7，跨两列）
    ///   ├ 账号下拉（第 3 行，第 1 列）
    ///   ├ 「登录」按钮 Height 28 Width 60 Margin 10,0,0,0（第 3 行，第 2 列）
    ///   └ 「» 购买正版」「» 前往官网」Opacity 0.3 Margin 2,30,2,0 居中
    /// </summary>
    private Control BuildMsPage()
    {
        var g = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        g.RowDefinitions.Add(new RowDefinition(new GridLength(25)));
        g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var icon = new Path
        {
            Data = Geometry.Parse(LogoIcon.Person),
            Fill = Pcl.Res("ColorBrush2"),
            Opacity = 0.7,
            Width = 40,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumnSpan(icon, 2);
        g.Children.Add(icon);

        var combo = MakeComboBox(false, "");
        Grid.SetRow(combo, 2);
        g.Children.Add(combo);

        var btn = new PclButton
        {
            Text = "登录",
            ColorType = PclButton.ColorState.Highlight,
            Width = 60,
            Margin = new Thickness(10, 0, 0, 0)
        };
        btn.Click += (_, _) => _right?.ShowHint("微软登录正在接入，稍后可用。");
        Grid.SetRow(btn, 2);
        Grid.SetColumn(btn, 1);
        g.Children.Add(btn);

        var links = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(2, 30, 2, 0),
            Opacity = 0.3,
            Spacing = 30
        };
        links.Children.Add(MakeLink("»  购买正版", "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj"));
        links.Children.Add(MakeLink("»  前往官网", "https://www.minecraft.net/zh-hans"));
        Grid.SetRow(links, 3);
        Grid.SetColumnSpan(links, 2);
        g.Children.Add(links);

        return g;
    }

    /// <summary>
    /// 对应 Pages/PageLaunch/PageLoginLegacy.xaml（离线）：
    /// StackPanel Margin 0,0,0,5
    ///   ├ MyHint（红色，仅在名字非法时显示）
    ///   ├ 皮肤预览 64×64
    ///   └ 可输入的下拉框 Height 28，占位文字「游戏用户名」
    /// </summary>
    private Control BuildLegacyPage()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 5) };

        var hint = new PclHint
        {
            Text = "玩家名如果包含英文、数字、下划线以外的内容，可能会无法进入 Minecraft 1.18 以后的世界！",
            IsVisible = false,
            Margin = new Thickness(0, 0, 0, 15)
        };
        stack.Children.Add(hint);

        stack.Children.Add(MakeSkin());

        _comboName = MakeComboBox(true, "游戏用户名");
        _comboName.Text = ModSettings.GetString("OfflineName", ModSettings.GetString("PlayerName", ""));
        _comboName.PropertyChanged += (_, e) =>
        {
            if (e.Property != ComboBox.TextProperty) return;
            var name = (_comboName.Text ?? "").Trim();
            hint.IsVisible = name.Length > 0 && !McOffline.IsValidName(name);
        };
        stack.Children.Add(_comboName);

        return stack;
    }

    /// <summary>64×64 皮肤预览（对应 MySkin：SameSize、圆角、半透明底）。</summary>
    private static Control MakeSkin()
    {
        var host = new Border
        {
            Width = 64,
            Height = 64,
            CornerRadius = new CornerRadius(6),
            Background = Pcl.Res("ColorBrushSemiTransparent"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15),
            ClipToBounds = true
        };

        var canvas = new Canvas { Width = 64, Height = 64 };
        void Rect(double x, double y, double w, double h, string hex)
        {
            canvas.Children.Add(new Rectangle
            {
                Width = w,
                Height = h,
                Fill = new SolidColorBrush(Color.Parse(hex)),
                [Canvas.LeftProperty] = x,
                [Canvas.TopProperty] = y
            });
        }
        // 正面 2D 皮肤：头 / 身体 / 手臂 / 腿（近似原版 Steve 配色）
        Rect(22, 6, 20, 18, "#C69C6D");   // 头
        Rect(22, 6, 20, 6, "#3F2A1C");    // 头发
        Rect(26, 15, 4, 4, "#FFFFFF");    // 左眼
        Rect(34, 15, 4, 4, "#FFFFFF");
        Rect(27, 16, 2, 3, "#5A3CA8");    // 瞳孔
        Rect(35, 16, 2, 3, "#5A3CA8");
        Rect(26, 22, 12, 2, "#A87C52");   // 嘴部阴影
        Rect(24, 25, 16, 22, "#00A8A8");  // 上衣
        Rect(18, 25, 6, 20, "#C69C6D");   // 左臂
        Rect(40, 25, 6, 20, "#C69C6D");   // 右臂
        Rect(24, 47, 7, 12, "#4B4BB0");   // 左腿
        Rect(33, 47, 7, 12, "#4B4BB0");
        Rect(24, 57, 7, 3, "#4A4A4A");    // 鞋
        Rect(33, 57, 7, 3, "#4A4A4A");

        host.Child = canvas;
        return host;
    }

    /// <summary>对应 MyComboBox：白底、Gray5 边框、圆角 3、高度 28。</summary>
    private static ComboBox MakeComboBox(bool editable, string placeholder)
    {
        return new ComboBox
        {
            Height = 28,
            MinHeight = 28,
            FontSize = 12.5,
            FontFamily = Pcl.FontFamily,
            Background = Pcl.Res("ColorBrushWhite"),
            BorderBrush = Pcl.Res("ColorBrushGray5"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEditable = editable,
            PlaceholderText = placeholder,
            PlaceholderForeground = Pcl.Res("ColorBrushGray4"),
            Foreground = Pcl.Res("ColorBrushGray1")
        };
    }

    /// <summary>对应 MyTextButton：文字按钮，点击打开网页。</summary>
    private static Control MakeLink(string text, string url)
    {
        var tb = Pcl.Text(text, 12.5, "ColorBrush1");
        tb.Cursor = new Cursor(StandardCursorType.Hand);
        tb.PointerReleased += (_, _) => OpenUrl(url);
        return tb;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ModLog.Err(ex, "打开网页");
        }
    }

    // ===================== 启动 =====================

    private void OnLaunchClick()
    {
        if (_selected is null) return;

        // 离线：把玩家名落盘（对应 Windows 版 Legacy 登录的用户名缓存）
        if (!RadioLoginType5.NavChecked && _comboName is not null)
        {
            var name = (_comboName.Text ?? "").Trim();
            if (name.Length == 0 || !McOffline.IsValidName(name))
            {
                _right?.ShowHint("请输入合法的游戏用户名（3-16 位，仅英文、数字、下划线）。");
                return;
            }
            ModSettings.SetValue("OfflineName", name);
            ModSettings.SetValue("OfflineUuid", McOffline.UuidOf(name));
        }

        ModSettings.SetValue("VersionSelect", _selected.Id);
        ModSettings.Save();
        ModLog.Info("准备启动 " + _selected.Id);
        _right?.ShowLog();
    }
}
