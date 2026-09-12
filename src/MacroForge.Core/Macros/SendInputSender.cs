using System.Runtime.InteropServices;
using MacroForge.Core.Learning;
using MacroForge.Core.Native;

namespace MacroForge.Core.Macros;

public sealed class SendInputSender : IInputSender
{
    public void SendKeys(IReadOnlyList<string> keys)
    {
        Press(keys, down: true);
        Press(keys.Reverse().ToList(), down: false);
    }

    public void SendMouseClick(string button)
    {
        var (down, up) = button.ToLowerInvariant() switch
        {
            "right" => (Win32.MOUSEEVENTF_RIGHTDOWN, Win32.MOUSEEVENTF_RIGHTUP),
            "middle" => (Win32.MOUSEEVENTF_MIDDLEDOWN, Win32.MOUSEEVENTF_MIDDLEUP),
            _ => (Win32.MOUSEEVENTF_LEFTDOWN, Win32.MOUSEEVENTF_LEFTUP)
        };
        NativeMethods.SendInput(1, new[] { Mouse(down) }, Marshal.SizeOf<INPUT>());
        NativeMethods.SendInput(1, new[] { Mouse(up) }, Marshal.SizeOf<INPUT>());
    }

    public void SendText(string text)
    {
        var inputs = new List<INPUT>();
        foreach (var ch in text)
        {
            inputs.Add(Uni(ch, up: false));
            inputs.Add(Uni(ch, up: true));
        }
        if (inputs.Count > 0)
            NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    private static void Press(IReadOnlyList<string> keys, bool down)
    {
        var inputs = new List<INPUT>();
        foreach (var key in keys)
        {
            if (!VirtualKeyNames.TryParse(key, out var vk))
                continue;
            inputs.Add(new INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        dwFlags = down ? 0 : Win32.KEYEVENTF_KEYUP
                    }
                }
            });
        }
        if (inputs.Count > 0)
            NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    private static INPUT Mouse(uint flags) => new()
    {
        type = Win32.INPUT_MOUSE,
        u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flags } }
    };

    private static INPUT Uni(char ch, bool up) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        u = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wScan = ch,
                dwFlags = Win32.KEYEVENTF_UNICODE | (up ? Win32.KEYEVENTF_KEYUP : 0)
            }
        }
    };
}
