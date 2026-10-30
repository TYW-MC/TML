using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
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

    private bool _launching;
    private volatile bool _cancelRequested;
    private double _progress;

    /// <summary>微软登录的取消标志（登录线程读取）。</summary>
    private volatile bool _msCancel;
    /// <summary>用户主动要求改用配对码。</summary>
    private volatile bool _msForceDevice;
    private bool _msLoggingIn;

    public PageLaunchLeft()
    {
        InitializeComponent();

        RadioLoginType5.Click += (_, _) => SetLoginType(5);
        RadioLoginType0.Click += (_, _) => SetLoginType(0);
        BtnLaunch.Click += (_, _) => _ = OnLaunchClick();
        BtnVersion.Click += (_, _) => _main?.SwitchPage(1);
        BtnCancelLaunch.Click += (_, _) => _cancelRequested = true;

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
        TryRefreshMsToken();
    }

    /// <summary>上次登录的令牌过期了就静默续期，避免进游戏时才报错。</summary>
    private void TryRefreshMsToken()
    {
        if (ModSettings.GetInt("LoginType", 0) != 5) return;
        var rt = ModSettings.GetString("MsRefreshToken", "");
        if (rt.Length == 0) return;
        long exp = 0;
        long.TryParse(ModSettings.GetString("MsExpiresAt", "0"), out exp);
        if (exp > DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;

        _ = Task.Run(() =>
        {
            try
            {
                var acc = ModMsLogin.Refresh(rt);
                ModSettings.SetValue("MsName", acc.Name);
                ModSettings.SetValue("MsUuid", acc.Uuid);
                ModSettings.SetValue("MsToken", acc.AccessToken);
                ModSettings.SetValue("MsRefreshToken", acc.RefreshToken);
                ModSettings.SetValue("MsExpiresAt", acc.ExpiresAt.ToString());
                ModSettings.Save();
                Dispatcher.UIThread.Post(() => { if (RadioLoginType5.NavChecked) BuildLoginArea(); });
                ModLog.Info("微软登录已自动续期：" + acc.Name);
            }
            catch (Exception ex)
            {
                ModLog.Err(ex, "微软登录续期失败");
            }
        });
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
        PanTypeOne.IsVisible = false;
        PanType.IsVisible = true;
        if (RadioLoginType5.NavChecked)
        {
            // 已经登录过就直接显示账号
            if (!string.IsNullOrEmpty(ModSettings.GetString("MsName", "")))
            {
                ShowLoggedIn();
                return;
            }
            PanLogin.Children.Add(BuildMsPage());
        }
        else
        {
            PanLogin.Children.Add(BuildLegacyPage());
        }
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
        btn.Click += (_, _) => StartMsLogin();
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

    // ===================== 微软登录 =====================

    /// <summary>
    /// 优先用系统浏览器做标准登录（本地回环接收回调，登录完自动回来）；
    /// 浏览器不可用或用户要求时，改用配对码流程（与 Windows 版一致）。
    /// </summary>
    private void StartMsLogin()
    {
        if (_msLoggingIn) return;
        _msLoggingIn = true;
        _msCancel = false;
        _msForceDevice = false;

        var stack = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        var title = Pcl.Text("正在打开浏览器…", 12.5, "ColorBrush1");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(title);

        var codeBlock = new TextBlock
        {
            Text = "",
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrush1"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2),
            IsVisible = false
        };
        stack.Children.Add(codeBlock);

        var tip = new TextBlock
        {
            Text = "请在浏览器里登录微软账号，完成后会自动回到启动器。",
            FontSize = 11.5,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrushGray2"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 250
        };
        stack.Children.Add(tip);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var btnDevice = new PclButton { Text = "改用配对码", Height = 28, Width = 96 };
        var btnOpen = new PclButton { Text = "重新打开", Height = 28, Width = 84 };
        var btnCancel = new PclButton { Text = "取消", Height = 28, Width = 60 };
        buttons.Children.Add(btnDevice);
        buttons.Children.Add(btnOpen);
        buttons.Children.Add(btnCancel);
        stack.Children.Add(buttons);

        PanLogin.Children.Clear();
        PanLogin.Children.Add(stack);

        string deviceUrl = null;
        btnOpen.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(deviceUrl)) OpenInBrowser(deviceUrl);
        };
        btnOpen.IsVisible = false;
        btnDevice.Click += (_, _) =>
        {
            _msForceDevice = true;
            tip.Text = "正在申请配对码…";
        };
        btnCancel.Click += (_, _) =>
        {
            _msCancel = true;
            tip.Text = "已取消登录";
        };

        _ = Task.Run(() =>
        {
            try
            {
                // 第一选择：浏览器授权码 + 本地回调
                var acc = ModMsLogin.LoginWithBrowser(
                    url => Dispatcher.UIThread.Post(() => OpenInBrowser(url)),
                    () => _msCancel || _msForceDevice,
                    s => Dispatcher.UIThread.Post(() => { if (!_msForceDevice) tip.Text = s; }));

                // 回退：配对码
                if (acc == null && !_msCancel)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        title.Text = "请在浏览器中输入配对码";
                        codeBlock.IsVisible = true;
                        codeBlock.Text = "· · · · · ·";
                        tip.Text = "正在申请配对码…";
                        btnDevice.IsVisible = false;
                        btnOpen.IsVisible = true;
                    });

                    acc = ModMsLogin.Login(
                        dc => Dispatcher.UIThread.Post(() =>
                        {
                            codeBlock.Text = dc.UserCode;
                            deviceUrl = dc.VerificationUri;
                            tip.Text = "配对码已复制到剪贴板。浏览器没自动打开的话，点「重新打开」。";
                            OpenInBrowser(dc.VerificationUri);
                            CopyText(dc.UserCode);
                        }),
                        () => _msCancel,
                        s => Dispatcher.UIThread.Post(() => tip.Text = s));
                }

                if (acc != null)
                {
                    Dispatcher.UIThread.Post(() => OnMsSuccess(acc));
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _msLoggingIn = false;
                        BuildLoginArea();
                    });
                }
            }
            catch (OperationCanceledException)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _msLoggingIn = false;
                    BuildLoginArea();
                });
            }
            catch (Exception ex)
            {
                ModLog.Err(ex, "微软登录");
                Dispatcher.UIThread.Post(() =>
                {
                    _msLoggingIn = false;
                    tip.Text = ex.Message;
                    tip.Foreground = Pcl.Res("ColorBrushRed") as IBrush ?? tip.Foreground;
                    btnDevice.IsVisible = true;
                });
            }
        });
    }

    private void OnMsSuccess(MsAccount acc)
    {
        _msLoggingIn = false;
        ModSettings.SetValue("MsName", acc.Name);
        ModSettings.SetValue("MsUuid", acc.Uuid);
        ModSettings.SetValue("MsToken", acc.AccessToken);
        ModSettings.SetValue("MsRefreshToken", acc.RefreshToken);
        ModSettings.SetValue("MsExpiresAt", acc.ExpiresAt.ToString());
        ModSettings.SetValue("LoginType", "5");
        ModSettings.Save();
        ModLog.Info("微软登录成功：" + acc.Name);
        BuildLoginArea();
    }

    /// <summary>已登录：头像 + 名字 + 退出。</summary>
    private void ShowLoggedIn()
    {
        PanType.IsVisible = false;
        PanTypeOne.IsVisible = true;
        LabTypeOne.Text = ModSettings.GetString("MsName", "");
        try { PathTypeOne.Data = Geometry.Parse(LogoIcon.Person); } catch { }

        var stack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(MakeSkin());

        var name = Pcl.Text(ModSettings.GetString("MsName", "玩家"), 13, "ColorBrush1");
        name.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(name);

        var btnOut = new PclButton
        {
            Text = "退出登录",
            Height = 26,
            Width = 84,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        btnOut.Click += (_, _) =>
        {
            ModSettings.SetValue("MsName", "");
            ModSettings.SetValue("MsUuid", "");
            ModSettings.SetValue("MsToken", "");
            ModSettings.SetValue("MsRefreshToken", "");
            ModSettings.Save();
            BuildLoginArea();
        };
        stack.Children.Add(btnOut);

        PanLogin.Children.Clear();
        PanLogin.Children.Add(stack);
    }

    /// <summary>用系统默认浏览器打开网页（Linux 走 xdg-open）。</summary>
    private static void OpenInBrowser(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(url);
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // 没有 xdg-open 时退回 ShellExecute
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ModLog.Err(ex, "打开浏览器");
            }
        }
    }

    private void CopyText(string text)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            _ = top?.Clipboard?.SetTextAsync(text ?? "");
        }
        catch
        {
            // 剪贴板失败不影响登录
        }
    }

    /// <summary>
    /// 对应 Pages/PageLaunch/PageLoginLegacy.xaml（离线）：
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

    private async Task OnLaunchClick()
    {
        if (_launching) return;
        if (_selected is null)
        {
            _right?.ShowHint("还没有安装任何版本，请先在下载页安装一个版本。");
            return;
        }

        // 离线：把玩家名落盘（对应 Windows 版 Legacy 登录的用户名缓存）
        string playerName;
        if (!RadioLoginType5.NavChecked)
        {
            playerName = (_comboName?.Text ?? "").Trim();
            if (playerName.Length == 0 || !McOffline.IsValidName(playerName))
            {
                _right?.ShowHint("请输入合法的游戏用户名（3-16 位，仅英文、数字、下划线）。");
                return;
            }
            ModSettings.SetValue("OfflineName", playerName);
            ModSettings.SetValue("OfflineUuid", McOffline.UuidOf(playerName));
        }
        else
        {
            playerName = ModSettings.GetString("MsName", "");
            if (playerName.Length == 0)
            {
                _right?.ShowHint("还没有登录正版账号，请先点「登录」。");
                return;
            }
        }

        ModSettings.SetValue("VersionSelect", _selected.Id);
        ModSettings.Save();
        ModLog.Info("准备启动 " + _selected.Id);

        var opt = new LaunchOptions
        {
            VersionId = _selected.Id,
            PlayerName = playerName,
            Uuid = RadioLoginType5.NavChecked
                    ? ModSettings.GetString("MsUuid", "")
                    : ModSettings.GetString("OfflineUuid", McOffline.UuidOf(playerName)),
            AccessToken = RadioLoginType5.NavChecked
                    ? ModSettings.GetString("MsToken", "0")
                    : "0",
            UserType = RadioLoginType5.NavChecked ? "msa" : "legacy",
            JavaPath = ModSettings.GetString("LaunchJavaExe", ""),
            MaxRamMb = ModSettings.GetInt("LaunchRam", 2048),
            GameDir = ModPaths.McFolder
        };

        _launching = true;
        _cancelRequested = false;
        _progress = 0;
        SetProgress(0, "正在启动");
        await EnterLaunching();

        try
        {
            var proc = await Task.Run(() => ModLaunch.Launch(opt, (stage, prog) =>
            {
                if (_cancelRequested) throw new OperationCanceledException();
                Dispatcher.UIThread.Post(() => SetProgress(prog, stage));
            }));

            SetProgress(1, "启动成功，祝游戏愉快");
            await Task.Delay(900);
            await ExitLaunching();
        }
        catch (OperationCanceledException)
        {
            SetProgress(_progress, "已取消启动");
            await Task.Delay(700);
            await ExitLaunching();
        }
        catch (Exception ex)
        {
            ModLog.Err(ex, "启动失败");
            SetProgressError(ex.Message);
            await Task.Delay(4000);
            await ExitLaunching();
        }
        finally
        {
            _launching = false;
        }
    }

    /// <summary>进入启动中：PanInput 淡出缩小，PanLaunching 上移淡入。</summary>
    private async Task EnterLaunching()
    {
        PanLaunching.IsVisible = true;
        PanLaunching.Opacity = 0;
        var tt = (TranslateTransform)PanLaunching.RenderTransform!;
        tt.Y = 14;

        _ = Ani.Tween(1, 0, 170, v =>
        {
            PanInput.Opacity = v;
            if (PanInput.RenderTransform is ScaleTransform st)
            {
                st.ScaleX = 0.97 + 0.03 * v;
                st.ScaleY = 0.97 + 0.03 * v;
            }
        }, Ani.EaseInFluent);

        await Ani.Tween(0, 1, 260, v => PanLaunching.Opacity = v, Ani.EaseOutFluent, 90);
        await Ani.Tween(14, 0, 260, v => tt.Y = v, Ani.EaseOutFluent);
        PanInput.IsVisible = false;
    }

    private async Task ExitLaunching()
    {
        var tt = (TranslateTransform)PanLaunching.RenderTransform!;
        await Ani.Tween(1, 0, 160, v => PanLaunching.Opacity = v, Ani.EaseInFluent);
        PanLaunching.IsVisible = false;

        PanInput.IsVisible = true;
        if (PanInput.RenderTransform is ScaleTransform st)
        {
            st.ScaleX = 1;
            st.ScaleY = 1;
        }
        _ = Ani.Tween(0, 1, 220, v => PanInput.Opacity = v);
        tt.Y = 0;
    }

    /// <summary>进度环：平滑过渡到目标进度并更新阶段文字。</summary>
    private void SetProgress(double p, string stage)
    {
        p = Math.Clamp(p, 0, 1);
        LabStage.Text = stage;
        LabStage.Foreground = Pcl.Res("ColorBrushGray2");
        var from = _progress;
        _progress = p;
        if (Math.Abs(p - from) < 0.005)
        {
            DrawRing(p);
            return;
        }
        _ = Ani.Tween(from, p, 220, DrawRing, Ani.EaseOutFluent);
    }

    private void SetProgressError(string message)
    {
        LabStage.Text = message;
        LabStage.Foreground = Pcl.Res("ColorBrushRed") as IBrush ?? Pcl.Res("ColorBrush1");
        LabPercent.Text = "!";
        PathProgress.Data = null;
    }

    private void DrawRing(double p)
    {
        LabPercent.Text = ((int)Math.Round(p * 100)) + "%";
        PathProgress.Data = ArcGeometry(p, 38, 38, 33.5);
    }

    /// <summary>生成从 12 点方向顺时针扫过 p 圈（0~1）的圆弧。</summary>
    private static Geometry? ArcGeometry(double p, double cx, double cy, double r)
    {
        if (p <= 0.0005) return null;
        if (p >= 0.9995)
        {
            return new EllipseGeometry(new Rect(cx - r, cy - r, r * 2, r * 2));
        }
        double sweep = p * 360;
        double a0 = -90 * Math.PI / 180;
        double a1 = (-90 + sweep) * Math.PI / 180;
        var start = new Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0));
        var end = new Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));
        var fig = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        fig.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(r, r),
            IsLargeArc = sweep > 180,
            SweepDirection = SweepDirection.Clockwise
        });
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return geo;
    }
}
