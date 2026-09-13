using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using MacroForge.Core.Layout;
using MacroForge.Core.Model;

namespace MacroForge.App;

public sealed class MouseLayoutCanvas : Canvas
{
    private static readonly Color Neon = Color.FromRgb(0x2C, 0xE5, 0x8B);
    private static readonly Color NeonSoft = Color.FromRgb(0x5C, 0xFF, 0xB0);
    private static readonly Color Shell = Color.FromRgb(0x10, 0x21, 0x19);
    private static readonly Color ShellDeep = Color.FromRgb(0x05, 0x0A, 0x08);
    private static readonly Color Edge = Color.FromRgb(0x24, 0x5C, 0x42);
    private static readonly Color Ink = Color.FromRgb(0x04, 0x14, 0x0C);
    private static readonly Color Label = Color.FromRgb(0xEA, 0xFF, 0xF4);

    public static readonly DependencyProperty ButtonsProperty =
        DependencyProperty.Register(nameof(Buttons), typeof(IEnumerable<MouseButtonDefinition>), typeof(MouseLayoutCanvas),
            new PropertyMetadata(null, (d, _) => ((MouseLayoutCanvas)d).Rebuild()));

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(nameof(Layout), typeof(LayoutModel), typeof(MouseLayoutCanvas),
            new PropertyMetadata(null, (d, _) => ((MouseLayoutCanvas)d).Rebuild()));

    public static readonly DependencyProperty EditModeProperty =
        DependencyProperty.Register(nameof(EditMode), typeof(bool), typeof(MouseLayoutCanvas),
            new PropertyMetadata(false, (d, _) => ((MouseLayoutCanvas)d).Rebuild()));

    public static readonly DependencyProperty SelectedButtonIdProperty =
        DependencyProperty.Register(nameof(SelectedButtonId), typeof(string), typeof(MouseLayoutCanvas),
            new PropertyMetadata(null, (d, _) => ((MouseLayoutCanvas)d).Rebuild()));

    public IEnumerable<MouseButtonDefinition>? Buttons
    {
        get => (IEnumerable<MouseButtonDefinition>?)GetValue(ButtonsProperty);
        set => SetValue(ButtonsProperty, value);
    }

    public LayoutModel? Layout
    {
        get => (LayoutModel?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public bool EditMode
    {
        get => (bool)GetValue(EditModeProperty);
        set => SetValue(EditModeProperty, value);
    }

    public string? SelectedButtonId
    {
        get => (string?)GetValue(SelectedButtonIdProperty);
        set => SetValue(SelectedButtonIdProperty, value);
    }

    public event Action<string>? ButtonClicked;
    public event Action<string, double, double>? ButtonMoved;

    private string? _dragId;
    private Border? _dragVisual;

    private double _bodyLeft;
    private double _bodyTop;
    private double _bodyW;
    private double _bodyH;

    public MouseLayoutCanvas()
    {
        Width = 440;
        Height = 540;
        Background = Brushes.Transparent;
        Loaded += (_, _) => Rebuild();
        SizeChanged += (_, _) => Rebuild();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != ButtonsProperty)
            return;
        if (e.OldValue is INotifyCollectionChanged oldCol)
            oldCol.CollectionChanged -= OnButtonsChanged;
        if (e.NewValue is INotifyCollectionChanged newCol)
            newCol.CollectionChanged += OnButtonsChanged;
    }

    private void OnButtonsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    public void Rebuild()
    {
        Children.Clear();

        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        _bodyW = w * 0.42;
        _bodyH = h * 0.66;
        _bodyLeft = (w - _bodyW) / 2;
        _bodyTop = h * 0.11;

        var list = Buttons?.ToList() ?? new List<MouseButtonDefinition>();
        DrawChassis(list);

        if (Layout is null || list.Count == 0)
            return;

        // Leader lines first so the pills sit on top of them.
        foreach (var button in list)
        {
            var (x, y) = PositionOf(button, list);
            DrawLeader(button, new Point(x * w, y * h));
        }

        foreach (var button in list)
        {
            var (x, y) = PositionOf(button, list);
            AddNode(button, x * w, y * h);
        }
    }

    private (double X, double Y) PositionOf(MouseButtonDefinition button, List<MouseButtonDefinition> all)
    {
        var node = Layout?.Buttons.FirstOrDefault(n => n.ButtonId == button.Id);
        if (node is not null)
            return (node.X, node.Y);
        var extras = all.Where(b => !LayoutFactory.IsPrimary(b)).ToList();
        var index = extras.FindIndex(b => b.Id == button.Id);
        return LayoutFactory.DefaultPosition(button, Math.Max(0, index), Math.Max(1, extras.Count));
    }

    private Point BodyPoint(double nx, double ny) =>
        new(_bodyLeft + nx * _bodyW, _bodyTop + ny * _bodyH);

    private void DrawChassis(List<MouseButtonDefinition> buttons)
    {
        // Ambient glow beneath the chassis.
        var glow = new Ellipse
        {
            Width = _bodyW * 1.3,
            Height = _bodyH * 0.5,
            Fill = new RadialGradientBrush(Color.FromArgb(0x4A, Neon.R, Neon.G, Neon.B),
                                           Color.FromArgb(0x00, Neon.R, Neon.G, Neon.B)),
            Effect = new BlurEffect { Radius = 30 },
            IsHitTestVisible = false
        };
        Place(glow, _bodyLeft + _bodyW / 2 - glow.Width / 2, _bodyTop + _bodyH * 0.62);

        var body = new Path
        {
            Data = Geometry.Parse(
                "M 0.50,0.02 " +
                "C 0.72,0.02 0.90,0.14 0.93,0.34 " +
                "C 0.96,0.52 0.92,0.70 0.84,0.85 " +
                "C 0.76,0.97 0.62,1.00 0.50,1.00 " +
                "C 0.38,1.00 0.24,0.97 0.16,0.85 " +
                "C 0.08,0.70 0.04,0.52 0.07,0.34 " +
                "C 0.10,0.14 0.28,0.02 0.50,0.02 Z"),
            Width = _bodyW,
            Height = _bodyH,
            Stretch = Stretch.Fill,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.25, 0),
                EndPoint = new Point(0.8, 1),
                GradientStops =
                {
                    new GradientStop(Shell, 0),
                    new GradientStop(Color.FromRgb(0x0A, 0x16, 0x11), 0.5),
                    new GradientStop(ShellDeep, 1)
                }
            },
            Stroke = new SolidColorBrush(Edge),
            StrokeThickness = 1.6,
            IsHitTestVisible = false
        };
        Place(body, _bodyLeft, _bodyTop);

        // Primary pads: clicking the physical region selects that button.
        var left = buttons.FirstOrDefault(b => b.RawButtonIndex == 1);
        var right = buttons.FirstOrDefault(b => b.RawButtonIndex == 2);
        var middle = buttons.FirstOrDefault(b => b.RawButtonIndex == 3);

        AddPad(left, "M 0.47,0.03 C 0.29,0.04 0.11,0.16 0.08,0.34 C 0.07,0.38 0.065,0.41 0.065,0.45 L 0.47,0.45 Z");
        AddPad(right, "M 0.53,0.03 C 0.71,0.04 0.89,0.16 0.92,0.34 C 0.93,0.38 0.935,0.41 0.935,0.45 L 0.53,0.45 Z");

        // Scroll wheel well and wheel (middle button).
        var well = new Rectangle
        {
            Width = _bodyW * 0.12,
            Height = _bodyH * 0.20,
            RadiusX = _bodyW * 0.06,
            RadiusY = _bodyW * 0.06,
            Fill = new SolidColorBrush(ShellDeep),
            Stroke = new SolidColorBrush(Edge),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        Place(well, _bodyLeft + _bodyW / 2 - well.Width / 2, _bodyTop + _bodyH * 0.10);

        var wheelSelected = middle is not null && middle.Id == SelectedButtonId;
        var wheel = new Rectangle
        {
            Width = _bodyW * 0.075,
            Height = _bodyH * 0.155,
            RadiusX = _bodyW * 0.04,
            RadiusY = _bodyW * 0.04,
            Fill = new LinearGradientBrush(wheelSelected ? NeonSoft : Neon,
                                           Color.FromRgb(0x17, 0xA8, 0x68), 90),
            Effect = new DropShadowEffect
            {
                Color = Neon,
                BlurRadius = wheelSelected ? 22 : 12,
                ShadowDepth = 0,
                Opacity = wheelSelected ? 1 : 0.7
            },
            Cursor = Cursors.Hand,
            Tag = middle?.Id
        };
        if (middle is not null)
            wheel.MouseLeftButtonDown += OnRegionDown;
        else
            wheel.IsHitTestVisible = false;
        Place(wheel, _bodyLeft + _bodyW / 2 - wheel.Width / 2, _bodyTop + _bodyH * 0.122);

        // Side thumb-button plate (only drawn when the device reports extra buttons).
        if (buttons.Any(b => b.RawButtonIndex is 4 or 5))
        {
            var plate = new Path
            {
                Data = Geometry.Parse("M 0.075,0.36 C 0.02,0.44 0.02,0.60 0.075,0.68"),
                Width = _bodyW,
                Height = _bodyH,
                Stretch = Stretch.Fill,
                Stroke = new SolidColorBrush(Neon),
                StrokeThickness = 2.4,
                Opacity = 0.4,
                IsHitTestVisible = false
            };
            Place(plate, _bodyLeft, _bodyTop);
        }

        // Palm accent line, purely decorative.
        var palm = new Path
        {
            Data = Geometry.Parse("M 0.30,0.74 C 0.42,0.80 0.58,0.80 0.70,0.74"),
            Width = _bodyW,
            Height = _bodyH,
            Stretch = Stretch.Fill,
            Stroke = new SolidColorBrush(Edge),
            StrokeThickness = 1.4,
            Opacity = 0.8,
            IsHitTestVisible = false
        };
        Place(palm, _bodyLeft, _bodyTop);
    }

    private void AddPad(MouseButtonDefinition? button, string geometry)
    {
        var selected = button is not null && button.Id == SelectedButtonId;
        var pad = new Path
        {
            Data = Geometry.Parse(geometry),
            Width = _bodyW,
            Height = _bodyH,
            Stretch = Stretch.Fill,
            Fill = new SolidColorBrush(selected
                ? Color.FromArgb(0x59, Neon.R, Neon.G, Neon.B)
                : Color.FromArgb(0x14, Neon.R, Neon.G, Neon.B)),
            Stroke = new SolidColorBrush(selected ? Neon : Edge),
            StrokeThickness = selected ? 1.8 : 1,
            Cursor = Cursors.Hand,
            Tag = button?.Id
        };
        if (button is null)
        {
            pad.IsHitTestVisible = false;
        }
        else
        {
            pad.MouseLeftButtonDown += OnRegionDown;
            pad.MouseEnter += (_, _) => pad.Fill = new SolidColorBrush(Color.FromArgb(0x38, Neon.R, Neon.G, Neon.B));
            pad.MouseLeave += (_, _) => pad.Fill = new SolidColorBrush(button.Id == SelectedButtonId
                ? Color.FromArgb(0x59, Neon.R, Neon.G, Neon.B)
                : Color.FromArgb(0x14, Neon.R, Neon.G, Neon.B));
        }
        Place(pad, _bodyLeft, _bodyTop);
    }

    /// <summary>Thin line from the label pill to the physical spot it represents.</summary>
    private void DrawLeader(MouseButtonDefinition button, Point pill)
    {
        var anchor = AnchorFor(button, pill);
        var selected = button.Id == SelectedButtonId;
        var line = new Line
        {
            X1 = pill.X,
            Y1 = pill.Y,
            X2 = anchor.X,
            Y2 = anchor.Y,
            Stroke = new SolidColorBrush(selected ? Neon : Edge),
            StrokeThickness = selected ? 1.4 : 1,
            Opacity = selected ? 0.95 : 0.5,
            IsHitTestVisible = false
        };
        Children.Add(line);

        var dot = new Ellipse
        {
            Width = selected ? 7 : 5,
            Height = selected ? 7 : 5,
            Fill = new SolidColorBrush(selected ? Neon : Edge),
            IsHitTestVisible = false
        };
        Place(dot, anchor.X - dot.Width / 2, anchor.Y - dot.Height / 2);
    }

    private Point AnchorFor(MouseButtonDefinition button, Point pill)
    {
        switch (button.RawButtonIndex)
        {
            case 1: return BodyPoint(0.26, 0.22);
            case 2: return BodyPoint(0.74, 0.22);
            case 3: return BodyPoint(0.50, 0.20);
            case 4: return BodyPoint(0.045, 0.58);
            case 5: return BodyPoint(0.045, 0.42);
        }

        // Project the pill direction onto the chassis outline.
        var center = BodyPoint(0.5, 0.5);
        var dx = pill.X - center.X;
        var dy = pill.Y - center.Y;
        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
            return BodyPoint(0.5, 0.78);
        var a = _bodyW / 2 * 0.92;
        var b = _bodyH / 2 * 0.92;
        var k = 1.0 / Math.Sqrt(dx * dx / (a * a) + dy * dy / (b * b));
        return new Point(center.X + dx * k, center.Y + dy * k);
    }

    private void AddNode(MouseButtonDefinition button, double cx, double cy)
    {
        var selected = button.Id == SelectedButtonId;
        var text = new TextBlock
        {
            Text = ShortLabel(button),
            Foreground = new SolidColorBrush(selected ? Ink : Label),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var border = new Border
        {
            MinWidth = 62,
            Height = 30,
            Padding = new Thickness(10, 0, 10, 0),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(selected ? Neon : Color.FromRgb(0x0A, 0x16, 0x11)),
            BorderBrush = new SolidColorBrush(selected ? NeonSoft : Edge),
            BorderThickness = new Thickness(selected ? 1.8 : 1.2),
            Effect = selected
                ? new DropShadowEffect { Color = Neon, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.9 }
                : new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Opacity = 0.5 },
            Cursor = EditMode ? Cursors.SizeAll : Cursors.Hand,
            Tag = button.Id,
            Child = text,
            ToolTip = button.DisplayName
        };

        if (!selected)
        {
            border.MouseEnter += (_, _) => border.BorderBrush = new SolidColorBrush(Neon);
            border.MouseLeave += (_, _) => border.BorderBrush = new SolidColorBrush(Edge);
        }

        border.MouseLeftButtonDown += OnNodeDown;
        border.MouseLeftButtonUp += OnNodeUp;
        border.MouseMove += OnNodeMove;

        border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = Math.Max(border.MinWidth, border.DesiredSize.Width);
        Place(border, cx - width / 2, cy - border.Height / 2);
    }

    private void Place(UIElement element, double left, double top)
    {
        SetLeft(element, left);
        SetTop(element, top);
        Children.Add(element);
    }

    private void OnRegionDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            ButtonClicked?.Invoke(id);
            e.Handled = true;
        }
    }

    private void OnNodeDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string id)
            return;
        ButtonClicked?.Invoke(id);
        if (!EditMode)
            return;
        _dragId = id;
        _dragVisual = border;
        border.CaptureMouse();
        e.Handled = true;
    }

    private void OnNodeUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragVisual is null || _dragId is null)
            return;
        _dragVisual.ReleaseMouseCapture();
        CommitDrag();
        _dragVisual = null;
        _dragId = null;
    }

    private void OnNodeMove(object sender, MouseEventArgs e)
    {
        if (_dragVisual is null || _dragId is null || e.LeftButton != MouseButtonState.Pressed)
            return;
        var pos = e.GetPosition(this);
        SetLeft(_dragVisual, pos.X - _dragVisual.ActualWidth / 2);
        SetTop(_dragVisual, pos.Y - _dragVisual.ActualHeight / 2);
    }

    private void CommitDrag()
    {
        if (_dragVisual is null || _dragId is null)
            return;
        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        var x = (GetLeft(_dragVisual) + _dragVisual.ActualWidth / 2) / w;
        var y = (GetTop(_dragVisual) + _dragVisual.ActualHeight / 2) / h;
        ButtonMoved?.Invoke(_dragId, x, y);
    }

    private static string ShortLabel(MouseButtonDefinition button)
    {
        if (button.RawButtonIndex == 1) return "左鍵";
        if (button.RawButtonIndex == 2) return "右鍵";
        if (button.RawButtonIndex == 3) return "中鍵";
        var name = string.IsNullOrWhiteSpace(button.DisplayName) ? "按鍵" : button.DisplayName;
        if (button.RawButtonIndex is >= 4 and <= 32 && name.StartsWith("按鍵", StringComparison.Ordinal))
            return $"B{button.RawButtonIndex}";
        return name.Length > 7 ? name[..7] : name;
    }
}
