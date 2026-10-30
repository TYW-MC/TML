using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using TYW.Desktop.Controls;

namespace TYW.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly PclNavButton[] _nav = new PclNavButton[5];
    private Control? _pageLeft;
    private Control? _pageRight;
    private int _page = -1;
    private readonly RotateTransform _rotate = new(0);
    private readonly TranslateTransform _translate = new(0, 0);

    public MainWindow()
    {
        InitializeComponent();

        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        ExtendClientAreaToDecorationsHint = false;

        // 软件渲染（llvmpipe）下窗口阴影的模糊很贵，可用 TYW_NO_SHADOW=1 关掉做对比
        if (Environment.GetEnvironmentVariable("TYW_NO_SHADOW") == "1")
            BorderForm.BoxShadow = default;

        // 入场变换：旋转 -4°、下移 60（对应 FormMain.xaml 的 TransformRotate / TransformPos）
        var tg = new TransformGroup();
        tg.Children.Add(_rotate);
        tg.Children.Add(_translate);
        PanBack.RenderTransform = tg;

        _nav[0] = BtnTitleSelect0;
        _nav[1] = BtnTitleSelect1;
        _nav[2] = BtnTitleSelect2;
        _nav[3] = BtnTitleSelect3;
        _nav[4] = BtnTitleSelect4;
        for (int i = 0; i < _nav.Length; i++)
        {
            int idx = i;
            _nav[i].Click += (_, _) => SwitchPage(idx);
        }

        BtnTitleClose.Click += (_, _) => Close();
        BtnTitleMin.Click += (_, _) => WindowState = WindowState.Minimized;

        // 无边框窗口靠标题栏拖动
        PanTitle.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };

        // 左栏底色宽度跟随左栏实际宽度
        PanMainLeft.SizeChanged += (_, e) => RectLeftBackground.Width = Math.Max(0, e.NewSize.Width);

        Opened += (_, _) =>
        {
            var navEnv = Environment.GetEnvironmentVariable("TYW_SNAPSHOT_NAV");
            SwitchPage(int.TryParse(navEnv, out var navIdx) ? navIdx : 0);

            // 先把初态摆好（不可见），等首帧布局/渲染完成后再启动补间，
            // 否则软件渲染下首帧阻塞会让动画在阻塞期间“空转完”，用户看到的是硬切。
            if (Environment.GetEnvironmentVariable("TYW_NO_ANIM") != "1")
            {
                PanBack.Opacity = 0;
                _rotate.Angle = -4;
                _translate.Y = 60;
                Dispatcher.UIThread.Post(() => _ = AnimateWindowIn(),
                    Avalonia.Threading.DispatcherPriority.Loaded);
            }

            AfterShown();
        };
    }

    /// <summary>对应 FormMain 的窗口入场：旋转 -4°、下移 60、渐显。</summary>
    private async System.Threading.Tasks.Task AnimateWindowIn()
    {
        if (Environment.GetEnvironmentVariable("TYW_NO_ANIM") == "1") return;
        var t1 = Ani.Tween(0, 1, 300, v => PanBack.Opacity = v, Ani.EaseOutFluent);
        var t2 = Ani.Tween(-4, 0, 320, v => _rotate.Angle = v, Ani.EaseOutFluent);
        var t3 = Ani.Tween(60, 0, 320, v => _translate.Y = v, Ani.EaseOutFluent);
        await System.Threading.Tasks.Task.WhenAll(t1, t2, t3);
    }

    public void SwitchPage(int index)
    {
        if (_page == index) return;
        _page = index;
        for (int i = 0; i < _nav.Length; i++) _nav[i].NavChecked = i == index;

        Control left;
        Control right;
        if (index == 0)
        {
            var l = new PageLaunchLeft();
            var r = new PageLaunchRight();
            l.Attach(this, r);
            left = l;
            right = r;
        }
        else
        {
            var (sl, sr) = StaticPage.Create(index);
            left = sl;
            right = sr;
        }

        _pageLeft = left;
        _pageRight = right;
        PanMainLeft.Child = left;
        PanMainRight.Child = right;

        if (Environment.GetEnvironmentVariable("TYW_NO_ANIM") != "1")
        {
            left.Opacity = 0;
            right.Opacity = 0;
            _ = Ani.Tween(0, 1, 200, v => left.Opacity = v, Ani.EaseOutFluent, 60);
            _ = Ani.Tween(0, 1, 220, v => right.Opacity = v, Ani.EaseOutFluent, 60);
        }
    }

    // ================= 调试快照：TYW_SNAPSHOT=路径 时把窗口渲染成 png 后退出 =================

    private void AfterShown()
    {
        // 动画数值探针：TYW_ANIM_LOG=文件 时按 16ms 采样入场动画的 opacity/angle/y（不渲染，不干扰时序）
        var logPath = Environment.GetEnvironmentVariable("TYW_ANIM_LOG");
        if (!string.IsNullOrEmpty(logPath))
        {
            var swLog = System.Diagnostics.Stopwatch.StartNew();
            var logger = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            logger.Tick += (_, _) =>
            {
                try
                {
                    File.AppendAllText(logPath,
                        $"{swLog.Elapsed.TotalMilliseconds,7:F0} {PanBack.Opacity:F3} {_rotate.Angle,7:F2} {_translate.Y,6:F1}{Environment.NewLine}");
                }
                catch { }
                if (swLog.Elapsed.TotalMilliseconds > 1500) logger.Stop();
            };
            logger.Start();
        }

        // 动画逐帧取证：TYW_ANIM_SHOTS=目录 时按动画进度抓帧（配合 TYW_ANIM_SLOW 慢放）
        var shots = Environment.GetEnvironmentVariable("TYW_ANIM_SHOTS");
        if (!string.IsNullOrEmpty(shots))
        {
            try { Directory.CreateDirectory(shots); } catch { }
            int totalMs = 320 * Ani.Slow;
            const int n = 9;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                DispatcherTimer.RunOnce(() =>
                {
                    try
                    {
                        SaveSnapshot(Path.Combine(shots, $"a{idx}.png"));
                        File.AppendAllText(Path.Combine(shots, "values.txt"),
                            $"{totalMs * idx / (n - 1.0),6:F0}ms  opacity={PanBack.Opacity:F3}  angle={_rotate.Angle:F2}  y={_translate.Y:F1}{Environment.NewLine}");
                    }
                    catch { }
                }, TimeSpan.FromMilliseconds(totalMs * i / (n - 1.0)));
            }
            if (Environment.GetEnvironmentVariable("TYW_ANIM_SHOTS_EXIT") == "1")
                DispatcherTimer.RunOnce(Close, TimeSpan.FromMilliseconds(totalMs + 500));
        }

        var path = Environment.GetEnvironmentVariable("TYW_SNAPSHOT");
        if (string.IsNullOrEmpty(path)) return;

        DispatcherTimer.RunOnce(() =>
        {
            try
            {
                SaveSnapshot(path);
                Console.WriteLine("snapshot saved: " + path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("snapshot failed: " + ex.Message);
            }
            if (Environment.GetEnvironmentVariable("TYW_SNAPSHOT_EXIT") == "1") Close();
        }, TimeSpan.FromMilliseconds(1200));
    }

    public void SaveSnapshot(string path)
    {
        int w = (int)Math.Ceiling(ClientSize.Width);
        int h = (int)Math.Ceiling(ClientSize.Height);
        if (w <= 0 || h <= 0) { w = (int)Width; h = (int)Height; }
        var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        bmp.Render(this);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bmp.Save(path);
    }
}
