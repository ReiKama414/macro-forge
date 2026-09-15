using System.Windows;
using System.Windows.Media;

namespace MacroForge.App;

/// <summary>One palette for surfaces, focus, selection, illustrations and native chrome.</summary>
public static class ThemePalette
{
    public static Color AccentFor(string name) => name switch
    {
        "青" => Color.FromRgb(34, 211, 238),
        "藍" => Color.FromRgb(79, 140, 255),
        "紫" => Color.FromRgb(167, 120, 255),
        "橘" => Color.FromRgb(255, 159, 67),
        _ => Color.FromRgb(82, 239, 153)
    };

    public static Color Mix(Color color, byte background, double amount) => Color.FromRgb(
        (byte)(background + (color.R - background) * amount),
        (byte)(background + (color.G - background) * amount),
        (byte)(background + (color.B - background) * amount));

    public static void Apply(ResourceDictionary resources, string name)
    {
        var accent = AccentFor(name);
        resources["AccentColor"] = accent;
        foreach (var key in new[] { "Accent", "AccentDim", "StrokeHot", "Success" })
            resources[key] = new SolidColorBrush(key == "AccentDim" ? Mix(accent, 0, .65) : accent);
        foreach (var (key, level, tint) in new (string, byte, double)[]
        {
            ("Bg", 7, .025), ("PanelAlt", 21, .045), ("InputBg", 21, .035),
            ("Stroke", 40, .045), ("ControlBorder", 61, .055),
            ("AccentSurface", 13, .14), ("AccentHover", 21, .18),
            ("Muted", 153, .035), ("ChromeSurface", 9, .045)
        }) resources[key] = new SolidColorBrush(Mix(accent, level, tint));
        resources["Panel"] = new LinearGradientBrush(Mix(accent, 13, .04), Mix(accent, 7, .02), 80);
        resources["Stage"] = new RadialGradientBrush(Mix(accent, 9, .065), Mix(accent, 3, .012));
        resources["ChromeGlow"] = new RadialGradientBrush(Mix(accent, 14, .10), Mix(accent, 8, .015))
        { Center = new Point(.5, .8), RadiusX = .6, RadiusY = 2 };
        if (Application.Current is { } app)
        {
            foreach (Window window in app.Windows)
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                WindowChromeHelper.ApplyRoundedCyberChrome(handle, accent);
            }
        }
    }
}
