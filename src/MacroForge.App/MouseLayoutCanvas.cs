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
    private Color Neon => (AccentBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x2C, 0xE5, 0x8B);
    private Color NeonSoft => Color.Multiply(Neon, 1.25f);
    private Color Edge => ThemePalette.Mix(Neon, 70, .23);
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

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(MouseLayoutCanvas),
            new PropertyMetadata(Brushes.LimeGreen, (d, _) => ((MouseLayoutCanvas)d).Rebuild()));

    public static readonly DependencyProperty ButtonLabelsProperty = DependencyProperty.Register(nameof(ButtonLabels),
        typeof(IReadOnlyDictionary<string, string>), typeof(MouseLayoutCanvas), new PropertyMetadata(null, (d, _) => ((MouseLayoutCanvas)d).Rebuild()));
    public IReadOnlyDictionary<string, string>? ButtonLabels
    {
        get => (IReadOnlyDictionary<string, string>?)GetValue(ButtonLabelsProperty);
        set => SetValue(ButtonLabelsProperty, value);
    }

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

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }
    public static readonly DependencyProperty SideViewProperty = DependencyProperty.Register(nameof(SideView), typeof(bool), typeof(MouseLayoutCanvas), new PropertyMetadata(false, (d, _) => ((MouseLayoutCanvas)d).Rebuild()));
    public bool SideView { get => (bool)GetValue(SideViewProperty); set => SetValue(SideViewProperty, value); }

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
        Width = 640;
        Height = 480;
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
        if (_dragVisual is not null) return;
        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        if (double.IsNaN(w) || double.IsNaN(h) || w < 8 || h < 8)
            return;

        Children.Clear();
        _bodyW = w * (SideView ? 0.55 : 0.33);
        _bodyH = h * (SideView ? 0.85 : 0.81);
        _bodyLeft = (w - _bodyW) / 2;
        _bodyTop = h * 0.13;

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
        var extrasForLayout = all.Where(b => !LayoutFactory.IsPrimary(b)).ToList();
        var slot = Math.Max(0, extrasForLayout.FindIndex(b => b.Id == button.Id));
        var original = LayoutFactory.DefaultPosition(button, slot, Math.Max(1, extrasForLayout.Count));
        if (node is not null && (Math.Abs(node.X - original.X) > .001 || Math.Abs(node.Y - original.Y) > .001))
            return (node.X, node.Y);
        if (button.RawButtonIndex == 1) return (.17, .23);
        if (button.RawButtonIndex == 2) return (.83, .23);
        if (button.RawButtonIndex == 3) return (.50, .065);
        if (button.RawButtonIndex == 4) return (.15, .49);
        if (button.RawButtonIndex == 5) return (.15, .70);
        var remaining = all.Where(b => b.RawButtonIndex is not (1 or 2 or 3 or 4 or 5)).ToList();
        var remainingIndex = remaining.FindIndex(b => b.Id == button.Id);
        if (remainingIndex >= 0) return (.84, .43 + remainingIndex * (.47 / Math.Max(1, remaining.Count - 1)));
        var extras = all.Where(b => !LayoutFactory.IsPrimary(b)).ToList();
        var index = extras.FindIndex(b => b.Id == button.Id);
        return LayoutFactory.DefaultPosition(button, Math.Max(0, index), Math.Max(1, extras.Count));
    }

    private Point BodyPoint(double nx, double ny) =>
        new(_bodyLeft + nx * _bodyW, _bodyTop + ny * _bodyH);

    // All chassis parts share a coordinate space; stretching each path separately
    // expands small details over the entire mouse and distorts hit targets.
    private Geometry BodyGeometry(string path)
    {
        var geometry = Geometry.Parse(path).Clone();
        geometry.Transform = new ScaleTransform(_bodyW, _bodyH);
        return geometry;
    }

    private void DrawChassis(List<MouseButtonDefinition> buttons)
    {
        var artwork = new MouseArtwork { Width = _bodyW, Height = _bodyH, AccentBrush = AccentBrush, SideView = SideView, IsHitTestVisible = false };
        Place(artwork, _bodyLeft, _bodyTop);
        if (SideView) return;
        AddPad(buttons.FirstOrDefault(b => b.RawButtonIndex == 1), "M .12,.04 L .46,.04 L .46,.46 L .12,.46 Z");
        AddPad(buttons.FirstOrDefault(b => b.RawButtonIndex == 2), "M .55,.04 L .89,.04 L .89,.46 L .55,.46 Z");
        AddPad(buttons.FirstOrDefault(b => b.RawButtonIndex == 3), "M .47,.10 L .54,.10 L .54,.28 L .47,.28 Z");
    }

    private void AddPad(MouseButtonDefinition? button, string geometry)
    {
        var selected = button is not null && button.Id == SelectedButtonId;
        var pad = new Path
        {
            Data = BodyGeometry(geometry),
            Width = _bodyW,
            Height = _bodyH,
            Stretch = Stretch.None,
            Fill = new SolidColorBrush(selected
                ? Color.FromArgb(0x14, Neon.R, Neon.G, Neon.B)
                : Color.FromArgb(0x00, Neon.R, Neon.G, Neon.B)),
            Stroke = Brushes.Transparent,
            StrokeThickness = 0,
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
                ? Color.FromArgb(0x14, Neon.R, Neon.G, Neon.B)
                : Colors.Transparent);
        }
        Place(pad, _bodyLeft, _bodyTop);
    }

    /// <summary>Thin line from the label pill to the physical spot it represents.</summary>
    private void DrawLeader(MouseButtonDefinition button, Point pill)
    {
        var anchor = AnchorFor(button, pill);
        var selected = button.Id == SelectedButtonId;
        var line = new Polyline
        {
            Points = new PointCollection { pill, new Point((pill.X + anchor.X) / 2, pill.Y), anchor },
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
        if (SideView) return button.RawButtonIndex switch
        {
            1 => BodyPoint(.16, .62), 2 => BodyPoint(.27, .57), 3 => BodyPoint(.13, .60),
            4 => BodyPoint(.36, .60), 5 => BodyPoint(.52, .59), _ => BodyPoint(.83, .76)
        };
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
            Text = button.RawButtonIndex is 4 or 5 ? $"側鍵 {button.RawButtonIndex - 3} (Button {button.RawButtonIndex})" : ShortLabel(button),
            Foreground = new SolidColorBrush(Label),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var border = new Border
        {
            Width = 158,
            Height = 58,
            Padding = new Thickness(10, 0, 10, 0),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(selected ? ThemePalette.Mix(Neon, 12, .13) : ThemePalette.Mix(Neon, 22, .035)),
            BorderBrush = new SolidColorBrush(selected ? NeonSoft : Edge),
            BorderThickness = new Thickness(selected ? 1.8 : 1.2),
            Effect = selected
                ? new DropShadowEffect { Color = Neon, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.35 }
                : new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Opacity = 0.5 },
            Cursor = EditMode ? Cursors.SizeAll : Cursors.Hand,
            Tag = button.Id,
            Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children =
            {
                text,
                new TextBlock { Text = ButtonLabels?.GetValueOrDefault(button.Id) ?? "預設按鍵", Foreground = new SolidColorBrush(selected ? Neon : Color.FromRgb(158,168,165)), FontSize = 11, Margin = new Thickness(0,6,0,0), MaxWidth = 132, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center }
            } },
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
        var width = border.Width;
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
        if (EditMode)
        {
            _dragId = id;
            _dragVisual = border;
            border.CaptureMouse();
        }
        ButtonClicked?.Invoke(id);
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
        Rebuild();
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
        if (button.RawButtonIndex == 3) return "滾輪";
        var name = string.IsNullOrWhiteSpace(button.DisplayName) ? "按鍵" : button.DisplayName;
        if (button.RawButtonIndex is >= 4 and <= 32 && (name.StartsWith("按鍵", StringComparison.Ordinal) || name.StartsWith("Button", StringComparison.OrdinalIgnoreCase)))
            return $"按鍵 {button.RawButtonIndex}";
        return name.Length > 7 ? name[..7] : name;
    }
}
