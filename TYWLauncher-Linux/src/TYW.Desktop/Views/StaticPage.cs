using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TYW.Desktop.Controls;
using TYW;
using System;
using System.Collections.Generic;
using System.Linq;

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
            host.Children.Add(BuildContent(index, items, i));
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

    private static Control BuildContent(int page, (string Title, string Logo)[] items, int index)
    {
        var host = new StackPanel();
        var name = items.Length > index ? items[index].Title : "";

        switch (page)
        {
            case 1:
                host.Children.Add(Card("下载", new[]
                {
                    "这里是「" + name + "」分类。Linux 版的下载器正在移植中。",
                    "已安装的版本会显示在下方，装好后可以直接在启动页开始游戏。"
                }));
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
        var stack = new StackPanel { Margin = new Thickness(15, 6, 15, 13), Spacing = 10 };

        stack.Children.Add(Row("版本隔离", "各版本独立存档与模组"));
        stack.Children.Add(Row("Java 路径", "自动检测"));
        stack.Children.Add(Row("内存分配", "自动"));
        stack.Children.Add(Row("语言", "简体中文"));
        stack.Children.Add(Row("界面主题", "浅色（与 Windows 版一致）"));

        card.Body.Children.Add(stack);
        return card;
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
