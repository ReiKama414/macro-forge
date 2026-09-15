using MacroForge.Core.Learning;

namespace MacroForge.Tests;

public class VirtualKeyNamesTests
{
    [Fact]
    public void Top_row_one_is_not_numpad_one()
    {
        Assert.True(VirtualKeyNames.TryParse("D1", out var top));
        Assert.True(VirtualKeyNames.TryParse("NumPad1", out var pad));
        Assert.Equal(0x31, top);
        Assert.Equal(0x61, pad);
        Assert.NotEqual(top, pad);
        Assert.Contains("主鍵盤", VirtualKeyNames.DisplayFromVk(top));
        Assert.Contains("數字鍵盤", VirtualKeyNames.DisplayFromVk(pad));
    }

    [Fact]
    public void Arrow_keys_use_glyphs()
    {
        Assert.Equal("←", VirtualKeyNames.CapGlyph("Left"));
        Assert.Equal("↑", VirtualKeyNames.CapGlyph("Up"));
        Assert.Equal("→", VirtualKeyNames.CapGlyph("Right"));
        Assert.Equal("↓", VirtualKeyNames.CapGlyph("Down"));
        Assert.Equal("← 左", VirtualKeyNames.DisplayName("Left"));
        Assert.True(VirtualKeyNames.TryParse("Left", out var left));
        Assert.Equal(0x25, left);
    }
}
