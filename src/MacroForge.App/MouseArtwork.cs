using System.Windows;
using System.Windows.Media;

namespace MacroForge.App;

/// <summary>Resolution independent generic mouse artwork; it does not impersonate a detected model.</summary>
public sealed class MouseArtwork : FrameworkElement
{
    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(MouseArtwork),
        new FrameworkPropertyMetadata(Brushes.MediumSeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush AccentBrush { get => (Brush)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public static readonly DependencyProperty SideViewProperty = DependencyProperty.Register(nameof(SideView), typeof(bool), typeof(MouseArtwork), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool SideView { get => (bool)GetValue(SideViewProperty); set => SetValue(SideViewProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.PushTransform(new ScaleTransform(ActualWidth / 240, ActualHeight / 430));
        var accent = (AccentBrush as SolidColorBrush)?.Color ?? ThemePalette.AccentFor("綠");
        Brush Solid(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
        void Shape(string data, Brush fill, Brush? stroke = null, double thickness = 1) =>
            dc.DrawGeometry(fill, stroke is null ? null : new Pen(stroke, thickness), Geometry.Parse(data));
        if (SideView)
        {
            var sideShell = new LinearGradientBrush(Color.FromRgb(71, 77, 78), Color.FromRgb(11, 15, 16), 75);
            Shape("M7,306 C27,207 85,121 140,132 C182,139 211,209 233,297 L228,342 Q127,374 12,350 Z", sideShell, Solid("#7A827D"), 1.2);
            Shape("M12,319 Q117,352 229,313 L228,342 Q127,374 12,350 Z", Solid("#111718"), Solid("#434C48"));
            Shape("M13,329 Q112,363 227,327", Brushes.Transparent, AccentBrush, 1.5);
            Shape("M17,293 Q52,227 112,232 L113,251 Q59,251 17,302 Z", Solid("#1B2223"), Solid("#64716B"));
            dc.DrawRoundedRectangle(Solid("#313A38"), new Pen(Solid("#89958F"), 1), new Rect(72, 248, 30, 17), 5, 5);
            dc.DrawRoundedRectangle(Solid("#313A38"), new Pen(Solid("#89958F"), 1), new Rect(112, 245, 30, 17), 5, 5);
            dc.Pop();
            return;
        }
        var glow = new RadialGradientBrush(Color.FromArgb(75, accent.R, accent.G, accent.B), Colors.Transparent);
        dc.DrawEllipse(glow, null, new Point(120, 318), 120, 110);
        const string body = "M119,5 C181,2 220,48 224,122 C226,175 208,212 216,268 C229,345 207,415 126,422 C44,428 15,370 24,289 C29,233 17,183 20,128 C22,61 54,10 119,5 Z";
        var shell = new LinearGradientBrush { StartPoint = new Point(0, .3), EndPoint = new Point(1, .65) };
        shell.GradientStops.Add(new GradientStop(Color.FromRgb(12, 15, 16), 0));
        shell.GradientStops.Add(new GradientStop(Color.FromRgb(68, 73, 73), .15));
        shell.GradientStops.Add(new GradientStop(Color.FromRgb(30, 34, 35), .35));
        shell.GradientStops.Add(new GradientStop(Color.FromRgb(18, 22, 23), .70));
        shell.GradientStops.Add(new GradientStop(Color.FromRgb(49, 56, 55), .93));
        shell.GradientStops.Add(new GradientStop(Color.FromRgb(8, 12, 13), 1));
        Shape(body, shell, Solid("#75817E"), 1.2);
        // Fine stippling gives the palm a matte finish at full size without a raster asset.
        var texture = new SolidColorBrush(Color.FromArgb(14, 230, 235, 234));
        dc.PushClip(Geometry.Parse(body));
        for (int y = 14; y < 419; y += 4)
            for (int x = 16; x < 223; x += 4)
                dc.DrawEllipse(texture, null, new Point(x + (y % 8 == 0 ? 2 : 0), y), .45, .45);
        dc.Pop();
        var pad = new LinearGradientBrush(Color.FromRgb(57, 63, 64), Color.FromRgb(23, 28, 29), 20);
        Shape("M111,8 C62,14 32,58 30,121 L33,193 Q61,211 110,193 Z", pad, Solid("#080B0C"), 2);
        Shape("M129,7 C181,10 211,60 214,120 L208,185 Q173,203 130,193 Z", pad, Solid("#080B0C"), 2);
        Shape("M118,8 L118,177", Brushes.Transparent, Solid("#9CA4A0"), .5);
        dc.DrawRoundedRectangle(Solid("#070A0B"), new Pen(Solid("#444D4C"), 1), new Rect(108, 43, 24, 76), 11, 11);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(70, 75, 75), Colors.Black, 0), new Pen(AccentBrush, 1), new Rect(113, 50, 14, 60), 6, 6);
        for (int y = 55; y < 106; y += 5)
            dc.DrawLine(new Pen(Solid("#77807B"), 1), new Point(114, y), new Point(126, y));
        dc.DrawRoundedRectangle(Solid("#151B1C"), new Pen(Solid("#88918A"), .8), new Rect(18, 170, 7, 38), 3, 3);
        dc.DrawRoundedRectangle(Solid("#151B1C"), new Pen(Solid("#88918A"), .8), new Rect(20, 218, 7, 38), 3, 3);
        Shape("M26,265 Q16,339 41,374", Brushes.Transparent, AccentBrush, 1.6);
        Shape("M215,273 Q225,333 205,372", Brushes.Transparent, new SolidColorBrush(Color.FromArgb(100, accent.R, accent.G, accent.B)), 1);
        dc.PushTransform(new TranslateTransform(101, 319));
        Shape("M0,28 L17,0 L30,0 L44,24 L31,46 L22,46 L34,25 L25,10 L21,10 L10,28 L21,28 L26,20 L31,29 L25,38 L5,38 Z", Brushes.Transparent, AccentBrush, 1.2);
        dc.Pop();
        dc.Pop();
    }
}
