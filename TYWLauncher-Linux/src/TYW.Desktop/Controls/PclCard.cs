using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TYW.Desktop.Controls;

/// <summary>
/// 提示条。对应 Windows 版 <c>Controls/MyHint.xaml</c>：圆角 3、1px 边框、浅色底、左侧色条、文字 12.5。
/// </summary>
public class PclHint : Border
{
    private readonly TextBlock _text;

    public PclHint()
    {
        CornerRadius = new CornerRadius(3);
        BorderThickness = new Thickness(1);
        BorderBrush = Pcl.Res("ColorBrush6");
        Background = Pcl.Res("ColorBrush8");
        Padding = new Thickness(10, 6, 10, 6);

        _text = new TextBlock
        {
            FontSize = 12.5,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrush3"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Child = _text;
    }

    public string Text
    {
        get => _text.Text ?? "";
        // 注意：正式版右栏不显示提示条，可见性由调用方（快照版 / 内测版）显式设置，
        // 这里只改文字，不能顺带改动 IsVisible，否则 XAML 里写的 IsVisible="False" 会被覆盖。
        set => _text.Text = value ?? "";
    }

    /// <summary>主题：Red=警告（对应 MyHint Theme="Red"），其余为蓝色提示。</summary>
    public string Theme
    {
        set
        {
            if (string.Equals(value, "Red", System.StringComparison.OrdinalIgnoreCase))
            {
                BorderThickness = new Thickness(3, 1, 1, 1);
                BorderBrush = new SolidColorBrush(Color.Parse("#99FF4444"));
                Background = new SolidColorBrush(Color.Parse("#ffdddf"));
                _text.Foreground = new SolidColorBrush(Color.Parse("#bf0b0b"));
            }
            else
            {
                BorderThickness = new Thickness(1);
                BorderBrush = Pcl.Res("ColorBrush6");
                Background = Pcl.Res("ColorBrush8");
                _text.Foreground = Pcl.Res("ColorBrush3");
            }
        }
    }
}

/// <summary>
/// 卡片。对应 Windows 版 <c>Controls/MyCard.xaml</c>：白底、圆角 6、1px Gray6 边框、标题 12.5 灰字。
/// </summary>
public class PclCard : Border
{
    private readonly TextBlock _title;
    private readonly StackPanel _body;

    public PclCard()
    {
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        BorderBrush = Pcl.Res("ColorBrushGray6");
        Background = Brushes.White;

        _title = new TextBlock
        {
            FontSize = 12.5,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrushGray2"),
            Margin = new Thickness(15, 10, 15, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _body = new StackPanel();
        Body = _body;

        var stack = new StackPanel();
        stack.Children.Add(_title);
        stack.Children.Add(_body);
        Child = stack;
    }

    public StackPanel Body { get; }

    public string Title
    {
        get => _title.Text ?? "";
        set
        {
            _title.Text = value ?? "";
            _title.IsVisible = !string.IsNullOrEmpty(value);
        }
    }
}
