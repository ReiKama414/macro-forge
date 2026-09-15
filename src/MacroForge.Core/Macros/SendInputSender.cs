using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroForge.Core.Learning;
using MacroForge.Core.Logging;
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
        Emit(new[] { Mouse(down) });
        Emit(new[] { Mouse(up) });
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
            Emit(inputs.ToArray());
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
            Emit(inputs.ToArray());
    }

    private static void Emit(INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == inputs.Length)
            return;
        var error = Marshal.GetLastWin32Error();
        AppLogService.Instance.Error("輸入", $"SendInput 失敗：無法注入輸入 (錯誤碼：{error})");
        Debug.WriteLine($"SendInput failed: sent={sent}/{inputs.Length} err={error}");
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
