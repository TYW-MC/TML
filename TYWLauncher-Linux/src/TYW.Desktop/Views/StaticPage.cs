using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TYW.Desktop.Controls;
using TYW;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TYW.Desktop.Views;

/// <summary>
/// 通用页面：左栏列表（条目与图标取自 Windows 版对应 Left.xaml）+ 右栏卡片内容。
/// 页面 1=下载 2=联机 3=设置 4=更多。
/// </summary>
public static class StaticPage
{
    public static (Control Left, Control Right) Create(int index)
    {
        (string Title, string Logo)[] items;
        double width;
        switch (index)
        {
            case 1: items = PageDefs.Download; width = 168; break;
            case 2: items = Array.Empty<(string Title, string Logo)>(); width = 0; break; // 联机页原版左栏为空
            case 3: items = PageDefs.Setup; width = 168; break;
            default: items = PageDefs.Other; width = 168; break;
        }

        var left = new Border { Width = width, Background = Brushes.Transparent };
        var list = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        left.Child = list;

        var right = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        var host = new StackPanel { Margin = new Thickness(25, 25, 25, 10) };
        right.Content = host;

        void Show(int i)
        {
            host.Children.Clear();
            host.Children.Add(BuildContent(index, items, i, () => Show(i)));
        }

        var buttons = new List<PclListItem>();
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            var item = new PclListItem { Title = items[i].Title, Height = 36 };
            if (!string.IsNullOrEmpty(items[i].Logo))
            {
                try { item.Logo = Geometry.Parse(items[i].Logo); } catch { }
            }
            item.Click += (_, _) =>
            {
                for (int k = 0; k < buttons.Count; k++) buttons[k].Checked = k == idx;
                Show(idx);
            };
            buttons.Add(item);
            list.Children.Add(item);
        }
        if (buttons.Count > 0)
        {
            buttons[0].Checked = true;
            Show(0);
        }
        else
        {
            Show(0);
        }

        return (left, right);
    }

    private static Control BuildContent(int page, (string Title, string Logo)[] items, int index, Action rebuild)
    {
        var host = new StackPanel();
        var name = items.Length > index ? items[index].Title : "";

        switch (page)
        {
            case 1:
                if (name == "原版游戏")
                {
                    host.Children.Add(VanillaInstaller(rebuild));
                }
                else
                {
                    host.Children.Add(Card(name, new[]
                    {
                        "Linux 版暂未接入「" + name + "」的在线下载。",
                        "你可以把 Windows 版 .minecraft 里的对应内容直接复制到 ~/.minecraft 使用。"
                    }));
                }
                host.Children.Add(InstalledVersionsCard());
                break;

            case 2:
                host.Children.Add(Card("联机", new[]
                {
                    "Linux 版联机使用 EasyTier，与 Windows 版同一套房间。",
                    "创建房间、加入房间、房间号分享功能正在移植中。"
                }));
                break;

            case 3:
                host.Children.Add(SettingsCard(name));
                break;

            default:
                host.Children.Add(Card(name, new[]
                {
                    page == 4 && name == "帮助"
                        ? "TYW Launcher Linux 版。界面、配色、控件位置全部按 Windows 版复刻。"
                        : "「" + name + "」内容正在移植中。"
                }));
                break;
        }
        return host;
    }

    // ===================== 原版安装 =====================

    /// <summary>
    /// 对应下载页「原版游戏」：选择版本 → 下载安装 → 进度。
    /// 版本清单来自 BMCLAPI 镜像，装完直接能在启动页选到。
    /// </summary>
    private static Control VanillaInstaller(Action rebuild)
    {
        var card = new PclCard { Title = "安装原版游戏", Margin = new Thickness(0, 0, 0, 15) };
        var stack = new StackPanel { Margin = new Thickness(15, 8, 15, 13), Spacing = 10 };

        var combo = new ComboBox
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
            Foreground = Pcl.Res("ColorBrushGray1"),
            PlaceholderText = "正在获取版本列表…",
            ItemsSource = new List<string>()
        };
        stack.Children.Add(combo);

        var btn = new PclButton
        {
            Text = "开始安装",
            ColorType = PclButton.ColorState.Highlight,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabledButton = false
        };
        stack.Children.Add(btn);

        var bar = new ProgressBar
        {
            Height = 4,
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            IsVisible = false,
            Foreground = Pcl.Res("ColorBrush1"),
            Background = Pcl.Res("ColorBrushGray6"),
            CornerRadius = new CornerRadius(2)
        };
        stack.Children.Add(bar);

        var status = new TextBlock
        {
            Text = "",
            FontSize = 12,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrushGray2"),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(status);

        card.Body.Children.Add(stack);

        var releases = new List<McVersionSummary>();

        _ = Task.Run(() =>
        {
            try
            {
                var all = ModDownload.Manifest();
                var rel = all.Where(v => v.Type == "release").ToList();
                Dispatcher.UIThread.Post(() =>
                {
                    releases.Clear();
                    releases.AddRange(rel);
                    combo.ItemsSource = rel.Select(v => v.Id).ToList();
                    if (rel.Count > 0) combo.SelectedIndex = 0;
                    combo.PlaceholderText = "选择要安装的版本";
                    btn.IsEnabledButton = rel.Count > 0;
                });
            }
            catch (Exception ex)
            {
                ModLog.Err(ex, "获取版本清单");
                Dispatcher.UIThread.Post(() =>
                {
                    combo.PlaceholderText = "获取版本列表失败";
                    status.Text = "无法连接下载源（" + ex.Message + "）。请检查网络后重新进入本页。";
                });
            }
        });

        var busy = false;
        var canceled = false;

        btn.Click += (_, _) =>
        {
            if (busy || combo.SelectedIndex < 0 || combo.SelectedIndex >= releases.Count) return;
            var target = releases[combo.SelectedIndex];
            busy = true;
            canceled = false;
            btn.IsEnabledButton = false;
            btn.Text = "安装中…";
            bar.IsVisible = true;
            bar.Value = 0;
            status.Text = "准备安装 " + target.Id;

            _ = Task.Run(() =>
            {
                try
                {
                    ModDownload.Install(target.Id, target.Url, (p, stage) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            bar.Value = p;
                            status.Text = stage;
                        });
                    }, () => canceled);

                    Dispatcher.UIThread.Post(() =>
                    {
                        status.Text = "「" + target.Id + "」安装完成，可以在启动页直接开始游戏。";
                        btn.Text = "开始安装";
                        btn.IsEnabledButton = true;
                        bar.Value = 1;
                        busy = false;
                        rebuild();
                    });
                }
                catch (Exception ex)
                {
                    ModLog.Err(ex, "安装版本 " + target.Id);
                    Dispatcher.UIThread.Post(() =>
                    {
                        status.Text = "安装失败：" + ex.Message;
                        btn.Text = "重试安装";
                        btn.IsEnabledButton = true;
                        bar.IsVisible = false;
                        busy = false;
                    });
                }
            });
        };

        return card;
    }

    private static PclCard Card(string title, IEnumerable<string> lines)
    {
        var card = new PclCard { Title = title, Margin = new Thickness(0, 0, 0, 15) };
        var stack = new StackPanel { Margin = new Thickness(15, 6, 15, 13), Spacing = 6 };
        foreach (var line in lines)
        {
            stack.Children.Add(new TextBlock
            {
                Text = line,
                FontSize = 12.5,
                FontFamily = Pcl.FontFamily,
                Foreground = Pcl.Res("ColorBrush1"),
                TextWrapping = TextWrapping.Wrap
            });
        }
        card.Body.Children.Add(stack);
        return card;
    }

    private static PclCard InstalledVersionsCard()
    {
        var card = new PclCard { Title = "已安装版本", Margin = new Thickness(0, 0, 0, 15) };
        var stack = new StackPanel { Margin = new Thickness(15, 6, 15, 13), Spacing = 4 };
        var list = ModVersion.ListVersions();
        if (list.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "还没有安装任何版本。",
                FontSize = 12.5,
                FontFamily = Pcl.FontFamily,
                Foreground = Pcl.Res("ColorBrushGray3")
            });
        }
        foreach (var v in list)
        {
            stack.Children.Add(new TextBlock
            {
                Text = v.Id,
                FontSize = 12.5,
                FontFamily = Pcl.FontFamily,
                Foreground = Pcl.Res("ColorBrush1")
            });
        }
        card.Body.Children.Add(stack);
        return card;
    }

    private static PclCard SettingsCard(string name)
    {
        var card = new PclCard { Title = name, Margin = new Thickness(0, 0, 0, 15) };
        var stack = new StackPanel { Margin = new Thickness(15, 6, 15, 13), Spacing = 6 };

        switch (name)
        {
            case "启动":
                stack.Children.Add(ComboRow("Java 路径", JavaOptions(), JavaSelected(),
                    i => SaveJava(i)));
                stack.Children.Add(ComboRow("内存分配", RamOptions(), RamSelected(), i => SaveRam(i)));
                stack.Children.Add(Row("游戏目录", ModPaths.McFolder));
                stack.Children.Add(ComboRow("下载源", new List<string> { "BMCLAPI 镜像（推荐）", "Mojang 官方" },
                    ModSettings.GetBool("UseOfficialSource") ? 1 : 0, i =>
                    {
                        ModSettings.SetValue("UseOfficialSource", i == 1);
                        ModSettings.Save();
                        ApplyMirror();
                    }));
                break;

            case "联机":
                stack.Children.Add(Row("联机方式", "EasyTier（与 Windows 版同一套房间）"));
                stack.Children.Add(Row("状态", "移植中，敬请期待"));
                break;

            case "个性化":
                stack.Children.Add(Row("界面主题", "浅色（与 Windows 版一致）"));
                stack.Children.Add(Row("语言", "简体中文"));
                break;

            default:
                stack.Children.Add(Row("TYW Launcher", "Linux 版 1.0"));
                stack.Children.Add(Row("界面框架", "Avalonia 12"));
                stack.Children.Add(Row("数据目录", ModPaths.Root));
                stack.Children.Add(Row("日志目录", ModPaths.LogDir));
                break;
        }

        card.Body.Children.Add(stack);
        return card;
    }

    private static List<string> JavaOptions()
    {
        var list = new List<string> { "自动选择（推荐）" };
        foreach (var j in ModJava.ListJava()) list.Add(j.Path + "    " + j.Display);
        return list;
    }

    private static int JavaSelected()
    {
        var saved = ModSettings.GetString("LaunchJavaExe");
        if (string.IsNullOrEmpty(saved)) return 0;
        var list = ModJava.ListJava();
        for (int i = 0; i < list.Count; i++)
            if (list[i].Path == saved) return i + 1;
        return 0;
    }

    private static void SaveJava(int index)
    {
        if (index <= 0)
        {
            ModSettings.SetValue("LaunchJavaExe", "");
        }
        else
        {
            var list = ModJava.ListJava();
            if (index - 1 < list.Count) ModSettings.SetValue("LaunchJavaExe", list[index - 1].Path);
        }
        ModSettings.Save();
    }

    private static readonly int[] RamValues = { 0, 1024, 2048, 4096, 6144, 8192, 12288, 16384 };

    private static List<string> RamOptions()
    {
        var list = new List<string> { "自动" };
        foreach (var mb in RamValues)
            if (mb > 0) list.Add(mb + " MB");
        return list;
    }

    private static int RamSelected()
    {
        var cur = ModSettings.GetInt("LaunchRam", 0);
        var i = Array.IndexOf(RamValues, cur);
        return i < 0 ? 0 : i;
    }

    private static void SaveRam(int index)
    {
        if (index < 0 || index >= RamValues.Length) return;
        ModSettings.SetValue("LaunchRam", RamValues[index].ToString());
        ModSettings.Save();
    }

    /// <summary>按设置切换下载源。</summary>
    public static void ApplyMirror()
    {
        ModDownload.Mirror = ModSettings.GetBool("UseOfficialSource")
            ? "https://launchermeta.mojang.com"
            : "https://bmclapi2.bangbang93.com";
    }

    /// <summary>左标题右下拉的设置行。</summary>
    private static Control ComboRow(string title, List<string> options, int selected, Action<int> changed)
    {
        var grid = new Grid { Height = 34 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(210)));

        var t = new TextBlock
        {
            Text = title,
            FontSize = 12.5,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrush1"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var combo = new ComboBox
        {
            FontSize = 12.5,
            FontFamily = Pcl.FontFamily,
            Height = 28,
            MinHeight = 28,
            Background = Pcl.Res("ColorBrushWhite"),
            BorderBrush = Pcl.Res("ColorBrushGray5"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Pcl.Res("ColorBrushGray1"),
            ItemsSource = options,
            SelectedIndex = Math.Clamp(selected, 0, Math.Max(0, options.Count - 1)),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        combo.SelectionChanged += (_, _) => changed(combo.SelectedIndex);
        Grid.SetColumn(combo, 1);
        grid.Children.Add(t);
        grid.Children.Add(combo);
        return grid;
    }

    private static Control Row(string title, string value)
    {
        var grid = new Grid { Height = 32 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var t = new TextBlock
        {
            Text = title,
            FontSize = 12.5,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrush1"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var v = new TextBlock
        {
            Text = value,
            FontSize = 12.5,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrushGray2"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(v, 1);
        grid.Children.Add(t);
        grid.Children.Add(v);
        return grid;
    }
}
