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
    public static readonly DependencyProperty ButtonsProperty =
        DependencyProperty.Register(nameof(Buttons), typeof(IEnumerable<MouseButtonDefinition>), typeof(MouseLayoutCanvas),
            new PropertyMetadata(null, (_, __) => ((MouseLayoutCanvas)_).Rebuild()));

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(nameof(Layout), typeof(LayoutModel), typeof(MouseLayoutCanvas),
            new PropertyMetadata(null, (_, __) => ((MouseLayoutCanvas)_).Rebuild()));

    public static readonly DependencyProperty EditModeProperty =
        DependencyProperty.Register(nameof(EditMode), typeof(bool), typeof(MouseLayoutCanvas),
            new PropertyMetadata(false, (_, __) => ((MouseLayoutCanvas)_).Rebuild()));

    public static readonly DependencyProperty SelectedButtonIdProperty =
        DependencyProperty.Register(nameof(SelectedButtonId), typeof(string), typeof(MouseLayoutCanvas),
            new PropertyMetadata(null, (_, __) => ((MouseLayoutCanvas)_).Rebuild()));

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

    public MouseLayoutCanvas()
    {
        Width = 440;
        Height = 540;
        Background = Brushes.Transparent;
        ClipToBounds = false;
        Loaded += (_, _) => Rebuild();
        SizeChanged += (_, _) => Rebuild();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ButtonsProperty)
        {
            if (e.OldValue is INotifyCollectionChanged oldCol)
                oldCol.CollectionChanged -= OnButtonsChanged;
            if (e.NewValue is INotifyCollectionChanged newCol)
                newCol.CollectionChanged += OnButtonsChanged;
        }
    }

    private void OnButtonsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    public void Rebuild()
    {
        Children.Clear();
        DrawBody();
        if (Buttons is null || Layout is null)
            return;

        foreach (var button in Buttons)
        {
            var node = Layout.Buttons.FirstOrDefault(n => n.ButtonId == button.Id);
            var (x, y) = node is null
                ? LayoutFactory.DefaultPosition(button, 0, 1)
                : (node.X, node.Y);
            AddNode(button, x, y);
        }
    }

    private void DrawBody()
    {
        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        var cx = w / 2;
        var bodyW = w * 0.40;
        var bodyH = h * 0.66;
        var left = cx - bodyW / 2;
        var top = h * 0.12;

        // Soft ambient glow under the mouse
        var glow = new Ellipse
        {
            Width = bodyW * 1.25,
            Height = bodyH * 0.55,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0x55, 0x2C, 0xE5, 0x8B),
                Color.FromArgb(0x00, 0x2C, 0xE5, 0x8B)),
            Effect = new BlurEffect { Radius = 28 }
        };
        SetLeft(glow, cx - glow.Width / 2);
        SetTop(glow, top + bodyH * 0.55);
        Children.Add(glow);

        // Organic gaming-mouse silhouette (brand-agnostic)
        var bodyGeo = Geometry.Parse(
            "M 0.50,0.02 " +
            "C 0.72,0.02 0.90,0.14 0.93,0.34 " +
            "C 0.96,0.52 0.92,0.70 0.84,0.84 " +
            "C 0.76,0.96 0.62,1.00 0.50,1.00 " +
            "C 0.38,1.00 0.24,0.96 0.16,0.84 " +
            "C 0.08,0.70 0.04,0.52 0.07,0.34 " +
            "C 0.10,0.14 0.28,0.02 0.50,0.02 Z");
        var body = new Path
        {
            Data = bodyGeo,
            Width = bodyW,
            Height = bodyH,
            Stretch = Stretch.Fill,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.3, 0),
                EndPoint = new Point(0.8, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x14, 0x2A, 0x20), 0),
                    new GradientStop(Color.FromRgb(0x0A, 0x16, 0x11), 0.45),
                    new GradientStop(Color.FromRgb(0x05, 0x0C, 0x09), 1)
                }
            },
            Stroke = new LinearGradientBrush(
                Color.FromRgb(0x2C, 0xE5, 0x8B),
                Color.FromRgb(0x1A, 0x4A, 0x34),
                90),
            StrokeThickness = 1.8,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x2C, 0xE5, 0x8B),
                BlurRadius = 22,
                ShadowDepth = 0,
                Opacity = 0.35
            }
        };
        SetLeft(body, left);
        SetTop(body, top);
        Children.Add(body);

        // Specular highlight on top shell
        var sheen = new Path
        {
            Data = Geometry.Parse("M 0.22,0.08 C 0.38,0.02 0.62,0.02 0.78,0.08 C 0.70,0.18 0.30,0.18 0.22,0.08 Z"),
            Width = bodyW * 0.72,
            Height = bodyH * 0.16,
            Stretch = Stretch.Fill,
            Fill = new LinearGradientBrush(
                Color.FromArgb(0x55, 0xEA, 0xFF, 0xF4),
                Color.FromArgb(0x00, 0xEA, 0xFF, 0xF4),
                90),
            IsHitTestVisible = false
        };
        SetLeft(sheen, cx - sheen.Width / 2);
        SetTop(sheen, top + bodyH * 0.04);
        Children.Add(sheen);

        // Left / right primary pad split
        var split = new Line
        {
            X1 = cx,
            Y1 = top + bodyH * 0.10,
            X2 = cx,
            Y2 = top + bodyH * 0.42,
            Stroke = new SolidColorBrush(Color.FromArgb(0xAA, 0x2C, 0xE5, 0x8B)),
            StrokeThickness = 1.2,
            Opacity = 0.55
        };
        Children.Add(split);

        // Scroll wheel well
        var well = new Rectangle
        {
            Width = 22,
            Height = 52,
            RadiusX = 11,
            RadiusY = 11,
            Fill = new SolidColorBrush(Color.FromRgb(0x05, 0x0A, 0x08)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x24, 0x5C, 0x42)),
            StrokeThickness = 1
        };
        SetLeft(well, cx - well.Width / 2);
        SetTop(well, top + bodyH * 0.16);
        Children.Add(well);

        var wheel = new Rectangle
        {
            Width = 14,
            Height = 40,
            RadiusX = 7,
            RadiusY = 7,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0x5C, 0xFF, 0xB0),
                Color.FromRgb(0x17, 0xA8, 0x68),
                90),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x2C, 0xE5, 0x8B),
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.85
            }
        };
        SetLeft(wheel, cx - wheel.Width / 2);
        SetTop(wheel, top + bodyH * 0.18);
        Children.Add(wheel);

        // Wheel notches
        for (var i = 0; i < 5; i++)
        {
            var notch = new Line
            {
                X1 = cx - 4,
                X2 = cx + 4,
                Y1 = top + bodyH * 0.20 + 6 + i * 6.5,
                Y2 = top + bodyH * 0.20 + 6 + i * 6.5,
                Stroke = new SolidColorBrush(Color.FromArgb(0x90, 0x04, 0x14, 0x0C)),
                StrokeThickness = 1.2
            };
            Children.Add(notch);
        }

        // Side contour accents (suggest side buttons without branding)
        var sideL = new Path
        {
            Data = Geometry.Parse("M 0.08,0.38 C 0.02,0.48 0.02,0.60 0.08,0.70"),
            Width = bodyW,
            Height = bodyH,
            Stretch = Stretch.Fill,
            Stroke = new SolidColorBrush(Color.FromRgb(0x2C, 0xE5, 0x8B)),
            StrokeThickness = 2,
            Opacity = 0.35,
            IsHitTestVisible = false
        };
        SetLeft(sideL, left);
        SetTop(sideL, top);
        Children.Add(sideL);

        var sideR = new Path
        {
            Data = Geometry.Parse("M 0.92,0.38 C 0.98,0.48 0.98,0.60 0.92,0.70"),
            Width = bodyW,
            Height = bodyH,
            Stretch = Stretch.Fill,
            Stroke = new SolidColorBrush(Color.FromRgb(0x2C, 0xE5, 0x8B)),
            StrokeThickness = 2,
            Opacity = 0.35,
            IsHitTestVisible = false
        };
        SetLeft(sideR, left);
        SetTop(sideR, top);
        Children.Add(sideR);

        // Sensor / logo plate at palm rest
        var plate = new Ellipse
        {
            Width = bodyW * 0.28,
            Height = bodyH * 0.10,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, 0x2C, 0xE5, 0x8B)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x2C)),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        SetLeft(plate, cx - plate.Width / 2);
        SetTop(plate, top + bodyH * 0.72);
        Children.Add(plate);
    }

    private void AddNode(MouseButtonDefinition button, double nx, double ny)
    {
        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        var selected = button.Id == SelectedButtonId;
        var fill = selected ? Color.FromRgb(0x2C, 0xE5, 0x8B) : Color.FromRgb(0x0A, 0x16, 0x11);
        var border = new Border
        {
            Width = 76,
            Height = 34,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(fill),
            BorderBrush = new SolidColorBrush(selected
                ? Color.FromRgb(0x5C, 0xFF, 0xB0)
                : Color.FromRgb(0x24, 0x5C, 0x42)),
            Effect = selected
                ? new DropShadowEffect
                {
                    Color = Color.FromRgb(0x2C, 0xE5, 0x8B),
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.9
                }
                : new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.55
                },
            BorderThickness = new Thickness(selected ? 1.8 : 1.2),
            Cursor = EditMode ? Cursors.SizeAll : Cursors.Hand,
            Tag = button.Id,
            Child = new TextBlock
            {
                Text = ShortLabel(button),
                Foreground = new SolidColorBrush(selected
                    ? Color.FromRgb(0x04, 0x14, 0x0C)
                    : Color.FromRgb(0xEA, 0xFF, 0xF4)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var left = nx * w - border.Width / 2;
        var top = ny * h - border.Height / 2;
        SetLeft(border, left);
        SetTop(border, top);
        border.MouseLeftButtonDown += OnNodeDown;
        border.MouseLeftButtonUp += OnNodeUp;
        border.MouseMove += OnNodeMove;
        Children.Add(border);
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
        SetLeft(_dragVisual, pos.X - _dragVisual.Width / 2);
        SetTop(_dragVisual, pos.Y - _dragVisual.Height / 2);
    }

    private void CommitDrag()
    {
        if (_dragVisual is null || _dragId is null)
            return;
        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        var x = (GetLeft(_dragVisual) + _dragVisual.Width / 2) / w;
        var y = (GetTop(_dragVisual) + _dragVisual.Height / 2) / h;
        ButtonMoved?.Invoke(_dragId, x, y);
    }

    private static string ShortLabel(MouseButtonDefinition button)
    {
        if (button.RawButtonIndex == 1) return "Left";
        if (button.RawButtonIndex == 2) return "Right";
        if (button.RawButtonIndex == 3) return "Mid";
        if (button.RawButtonIndex is >= 4 and <= 32) return $"B{button.RawButtonIndex}";
        return button.DisplayName.Length > 8 ? button.DisplayName[..8] : button.DisplayName;
    }
}
