using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TYW.Desktop.Controls;

/// <summary>
/// 左栏列表项。对应 Windows 版 <c>Controls/MyListItem.xaml</c>：
/// 选中时左侧出现 5px 主色竖条，图标列宽 34，标题 14 号，行高默认 36。
/// </summary>
public class PclListItem : Border
{
    private readonly Border _check;
    private readonly Path _logo;
    private readonly TextBlock _lab;
    private bool _checked;
    private bool _hover;

    public PclListItem()
    {
        Height = 36;
        Background = Pcl.Res("ColorBrushSemiTransparent");
        Cursor = new Cursor(StandardCursorType.Hand);
        CornerRadius = new CornerRadius(0);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));    // 选中竖条
        grid.ColumnDefinitions.Add(new ColumnDefinition(34, GridUnitType.Pixel));   // 图标
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));     // 标题
        grid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));    // 右padding

        _check = new Border
        {
            Width = 5,
            CornerRadius = new CornerRadius(2),
            Background = Pcl.Res("ColorBrush3"),
            Margin = new Thickness(-1, 6, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false
        };
        Grid.SetColumn(_check, 0);
        grid.Children.Add(_check);

        _logo = new Path
        {
            Margin = new Thickness(6, 8, 4, 8),
            Stretch = Stretch.Uniform,
            Fill = Pcl.Res("ColorBrush3"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_logo, 1);
        grid.Children.Add(_logo);

        _lab = new TextBlock
        {
            FontSize = 14,
            FontFamily = Pcl.FontFamily,
            Foreground = Pcl.Res("ColorBrush1"),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(_lab, 2);
        grid.Children.Add(_lab);

        Child = grid;

        PointerEntered += (_, _) => { _hover = true; Refresh(); };
        PointerExited += (_, _) => { _hover = false; Refresh(); };
        PointerReleased += (_, _) => Click?.Invoke(this, System.EventArgs.Empty);
    }

    public string Title
    {
        get => _lab.Text ?? "";
        set => _lab.Text = value ?? "";
    }

    public Geometry? Logo
    {
        get => _logo.Data;
        set
        {
            _logo.Data = value;
            _logo.IsVisible = value is not null;
        }
    }

    public bool Checked
    {
        get => _checked;
        set { _checked = value; Refresh(); }
    }

    public event System.EventHandler? Click;

    private void Refresh()
    {
        _check.IsVisible = _checked;
        if (_checked)
        {
            Background = Pcl.Res("ColorBrushSemiWhite");
            _lab.Foreground = Pcl.Res("ColorBrush3");
        }
        else if (_hover)
        {
            Background = Pcl.Res("ColorBrush7");
            _lab.Foreground = Pcl.Res("ColorBrush1");
        }
        else
        {
            Background = Pcl.Res("ColorBrushSemiTransparent");
            _lab.Foreground = Pcl.Res("ColorBrush1");
        }
    }
}
