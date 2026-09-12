namespace MacroForge.Core.Learning;

public static class VirtualKeyNames
{
    public static string GetName(int vk) => CanonicalFromVk(vk);

    public static string DisplayName(string token)
    {
        var key = token.Trim();
        if (key.Length == 0) return "";
        if (TryParse(key, out var vk))
            return DisplayFromVk(vk);
        return key;
    }

    public static string DisplayCombo(string? combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
            return "尚未指定按鍵";
        var parts = combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" + ", parts.Select(DisplayName));
    }

    public static string DisplayFromVk(int vk)
    {
        if (vk is >= 0x30 and <= 0x39)
            return $"上方數字 {vk - 0x30}（主鍵盤）";
        if (vk is >= 0x60 and <= 0x69)
            return $"右側數字鍵盤 {vk - 0x60}";
        if (vk is >= 0x70 and <= 0x87)
            return $"F{vk - 0x6F}";
        if (vk is >= 0x41 and <= 0x5A)
            return ((char)vk).ToString();

        return vk switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter（主鍵盤）",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0x13 => "Pause",
            0x14 => "CapsLock",
            0x1B => "Escape",
            0x20 => "空白鍵",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "方向左",
            0x26 => "方向上",
            0x27 => "方向右",
            0x28 => "方向下",
            0x2D => "Insert",
            0x2E => "Delete",
            0x5B => "左 Windows",
            0x5C => "右 Windows",
            0x5D => "Menu",
            0x6A => "右側數字鍵盤 *",
            0x6B => "右側數字鍵盤 +",
            0x6C => "右側數字鍵盤 Enter",
            0x6D => "右側數字鍵盤 -",
            0x6E => "右側數字鍵盤 .",
            0x6F => "右側數字鍵盤 /",
            0x90 => "NumLock",
            0xA0 => "左 Shift",
            0xA1 => "右 Shift",
            0xA2 => "左 Ctrl",
            0xA3 => "右 Ctrl",
            0xA4 => "左 Alt",
            0xA5 => "右 Alt",
            0xA6 => "Browser Back",
            _ => $"VK_0x{vk:X2}"
        };
    }

    public static string CanonicalFromVk(int vk)
    {
        if (vk is >= 0x30 and <= 0x39)
            return $"D{vk - 0x30}";
        if (vk is >= 0x60 and <= 0x69)
            return $"NumPad{vk - 0x60}";
        if (vk is >= 0x70 and <= 0x87)
            return $"F{vk - 0x6F}";
        if (vk is >= 0x41 and <= 0x5A)
            return ((char)vk).ToString();
        return vk switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Escape",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            0x6A => "NumPadMultiply",
            0x6B => "NumPadAdd",
            0x6D => "NumPadSubtract",
            0x6E => "NumPadDecimal",
            0x6F => "NumPadDivide",
            0xA0 => "LShift",
            0xA1 => "RShift",
            0xA2 => "LCtrl",
            0xA3 => "RCtrl",
            0xA4 => "LAlt",
            0xA5 => "RAlt",
            0xA6 => "Back",
            _ => $"VK_0x{vk:X2}"
        };
    }

    public static bool TryParse(string name, out int vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(name))
            return false;
        var key = name.Trim();

        if (key.Length == 1 && char.IsDigit(key[0]))
        {
            vk = key[0];
            return true;
        }

        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            vk = c;
            return c is >= 'A' and <= 'Z';
        }

        if (key.StartsWith("D", StringComparison.OrdinalIgnoreCase) &&
            key.Length == 2 && char.IsDigit(key[1]))
        {
            vk = (byte)'0' + (key[1] - '0');
            return true;
        }

        if (key.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) &&
            key.Length == 7 && char.IsDigit(key[6]))
        {
            vk = 0x60 + (key[6] - '0');
            return true;
        }

        if (key.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(key[1..], out var fn) && fn is >= 1 and <= 24)
        {
            vk = 0x6F + fn;
            return true;
        }

        vk = key.ToUpperInvariant() switch
        {
            "CTRL" or "LCTRL" or "CONTROL" => 0xA2,
            "RCTRL" => 0xA3,
            "ALT" or "LALT" => 0xA4,
            "RALT" => 0xA5,
            "SHIFT" or "LSHIFT" => 0xA0,
            "RSHIFT" => 0xA1,
            "ENTER" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "BACKSPACE" => 0x08,
            "BACK" => 0xA6,
            "NUMPADMULTIPLY" or "NUM*" => 0x6A,
            "NUMPADADD" or "NUM+" => 0x6B,
            "NUMPADSUBTRACT" or "NUM-" => 0x6D,
            "NUMPADDECIMAL" or "NUM." => 0x6E,
            "NUMPADDIVIDE" or "NUM/" => 0x6F,
            _ => 0
        };
        return vk != 0;
    }
}
