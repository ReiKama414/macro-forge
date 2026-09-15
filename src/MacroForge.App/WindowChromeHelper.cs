using System.Runtime.InteropServices;

namespace MacroForge.App;

/// <summary>
/// Windows 11 composition attributes: rounded window corners and a tinted border.
/// Silently no-ops on builds that do not support the attributes.
/// </summary>
internal static class WindowChromeHelper
{
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyRoundedCyberChrome(IntPtr hwnd, System.Windows.Media.Color? accent = null)
    {
        if (hwnd == IntPtr.Zero)
            return;

        var corner = DWMWCP_ROUND;
        TrySet(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner);

        var color = accent ?? System.Windows.Media.Color.FromRgb(53, 66, 61);
        var border = color.R | color.G << 8 | color.B << 16;
        TrySet(hwnd, DWMWA_BORDER_COLOR, ref border);
    }

    private static void TrySet(IntPtr hwnd, int attribute, ref int value)
    {
        try
        {
            DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Older Windows without dwmapi support.
        }
        catch (EntryPointNotFoundException)
        {
            // Attribute unsupported on this build.
        }
    }
}
