using System.Runtime.InteropServices;
using MacroForge.Core.Native;

namespace MacroForge.Core.Input;

public static class RawInputRegistration
{
    public static bool Register(IntPtr hwnd)
    {
        var size = (uint)Marshal.SizeOf<RAWINPUTDEVICE>();
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = Win32.HID_USAGE_PAGE_GENERIC,
                usUsage = Win32.HID_USAGE_GENERIC_MOUSE,
                dwFlags = Win32.RIDEV_INPUTSINK | Win32.RIDEV_DEVNOTIFY,
                hwndTarget = hwnd
            },
            new RAWINPUTDEVICE
            {
                usUsagePage = Win32.HID_USAGE_PAGE_GENERIC,
                usUsage = Win32.HID_USAGE_GENERIC_KEYBOARD,
                dwFlags = Win32.RIDEV_INPUTSINK | Win32.RIDEV_DEVNOTIFY,
                hwndTarget = hwnd
            },
            new RAWINPUTDEVICE
            {
                usUsagePage = Win32.HID_USAGE_PAGE_CONSUMER,
                usUsage = 0x01,
                dwFlags = Win32.RIDEV_INPUTSINK | Win32.RIDEV_DEVNOTIFY,
                hwndTarget = hwnd
            }
        };

        var ok = true;
        foreach (var device in devices)
            ok &= NativeMethods.RegisterRawInputDevices(new[] { device }, 1, size);
        return ok;
    }
}
