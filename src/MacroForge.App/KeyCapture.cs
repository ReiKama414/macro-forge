using System.Windows.Input;
using MacroForge.Core.Learning;

namespace MacroForge.App;

public static class KeyCapture
{
    public static bool TryCapture(KeyEventArgs e, out string canonical, out string display)
    {
        canonical = "";
        display = "";
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.None or Key.DeadCharProcessed or Key.ImeProcessed)
            return false;
        if (IsModifier(key))
            return false;

        var parts = new List<string>();
        if (Keyboard.IsKeyDown(Key.LeftCtrl)) parts.Add("LCtrl");
        else if (Keyboard.IsKeyDown(Key.RightCtrl)) parts.Add("RCtrl");
        if (Keyboard.IsKeyDown(Key.LeftShift)) parts.Add("LShift");
        else if (Keyboard.IsKeyDown(Key.RightShift)) parts.Add("RShift");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
        {
            if (Keyboard.IsKeyDown(Key.LeftAlt)) parts.Add("LAlt");
            else parts.Add("RAlt");
        }

        var token = Canonical(key);
        if (string.IsNullOrEmpty(token))
            return false;
        parts.Add(token);
        canonical = string.Join("+", parts);
        display = VirtualKeyNames.DisplayCombo(canonical);
        return true;
    }

    public static string Canonical(Key key) => key switch
    {
        Key.D0 => "D0",
        Key.D1 => "D1",
        Key.D2 => "D2",
        Key.D3 => "D3",
        Key.D4 => "D4",
        Key.D5 => "D5",
        Key.D6 => "D6",
        Key.D7 => "D7",
        Key.D8 => "D8",
        Key.D9 => "D9",
        Key.NumPad0 => "NumPad0",
        Key.NumPad1 => "NumPad1",
        Key.NumPad2 => "NumPad2",
        Key.NumPad3 => "NumPad3",
        Key.NumPad4 => "NumPad4",
        Key.NumPad5 => "NumPad5",
        Key.NumPad6 => "NumPad6",
        Key.NumPad7 => "NumPad7",
        Key.NumPad8 => "NumPad8",
        Key.NumPad9 => "NumPad9",
        Key.Multiply => "NumPadMultiply",
        Key.Add => "NumPadAdd",
        Key.Subtract => "NumPadSubtract",
        Key.Decimal => "NumPadDecimal",
        Key.Divide => "NumPadDivide",
        Key.LeftCtrl => "LCtrl",
        Key.RightCtrl => "RCtrl",
        Key.LeftShift => "LShift",
        Key.RightShift => "RShift",
        Key.LeftAlt => "LAlt",
        Key.RightAlt => "RAlt",
        Key.Space => "Space",
        Key.Return => "Enter",
        Key.Escape => "Escape",
        Key.Tab => "Tab",
        Key.Back => "Backspace",
        _ => CanonicalFromVirtual(key)
    };

    private static string CanonicalFromVirtual(Key key)
    {
        var vk = KeyInterop.VirtualKeyFromKey(key);
        return vk != 0 ? VirtualKeyNames.CanonicalFromVk(vk) : key.ToString();
    }

    private static bool IsModifier(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System;
}
