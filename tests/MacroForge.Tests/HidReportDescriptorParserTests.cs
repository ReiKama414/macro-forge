using MacroForge.Core.Hid;

namespace MacroForge.Tests;

public class HidReportDescriptorParserTests
{
    [Fact]
    public void Parses_five_button_mouse()
    {
        var parsed = HidReportDescriptorParser.Parse(Descriptor(buttonCount: 5, wheel: true));
        Assert.Equal(5, parsed.ButtonCount);
        Assert.True(parsed.HasWheel);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, parsed.ButtonUsages);
    }

    [Fact]
    public void Parses_eight_button_mouse()
    {
        var parsed = HidReportDescriptorParser.Parse(Descriptor(buttonCount: 8));
        Assert.Equal(8, parsed.ButtonCount);
    }

    [Fact]
    public void Parses_twelve_button_mouse()
    {
        var parsed = HidReportDescriptorParser.Parse(Descriptor(buttonCount: 12));
        Assert.Equal(12, parsed.ButtonCount);
        Assert.Contains(12, parsed.ButtonUsages);
    }

    [Fact]
    public void Unknown_hid_without_buttons_does_not_invent_them()
    {
        byte[] vendorOnly =
        {
            0x06, 0x00, 0xFF, // Usage Page vendor
            0x09, 0x01,
            0xA1, 0x01,
            0x15, 0x00,
            0x26, 0xFF, 0x00,
            0x75, 0x08,
            0x95, 0x08,
            0x81, 0x02,
            0xC0
        };
        var parsed = HidReportDescriptorParser.Parse(vendorOnly);
        Assert.Equal(0, parsed.ButtonCount);
    }

    private static byte[] Descriptor(int buttonCount, bool wheel = false)
    {
        var list = new List<byte>
        {
            0x05, 0x01, 0x09, 0x02, 0xA1, 0x01,
            0x09, 0x01, 0xA1, 0x00,
            0x05, 0x09, 0x19, 0x01, 0x29, (byte)buttonCount,
            0x15, 0x00, 0x25, 0x01,
            0x95, (byte)buttonCount, 0x75, 0x01, 0x81, 0x02,
            0x05, 0x01, 0x09, 0x30, 0x09, 0x31
        };
        if (wheel)
            list.AddRange(new byte[] { 0x09, 0x38 });
        list.AddRange(new byte[] { 0x15, 0x81, 0x25, 0x7F, 0x75, 0x08, 0x95, (byte)(wheel ? 3 : 2), 0x81, 0x06, 0xC0, 0xC0 });
        return list.ToArray();
    }
}
