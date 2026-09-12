using MacroForge.Core.Devices;
using MacroForge.Core.Input;
using MacroForge.Core.Learning;
using MacroForge.Core.Model;

namespace MacroForge.Tests;

public class ButtonLearningTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(12)]
    public void Seeds_exactly_the_reported_capability_count(int count)
    {
        var buttons = ButtonLearningService.SeedFromCapabilities(new DeviceCapabilities { ButtonCount = count });
        Assert.Equal(count, buttons.Count);
        Assert.Equal("左鍵", buttons[0].DisplayName);
        Assert.Equal($"Button {count}", buttons[^1].DisplayName);
    }

    [Fact]
    public void Unknown_hid_with_zero_buttons_does_not_fake_events()
    {
        var buttons = ButtonLearningService.SeedFromCapabilities(new DeviceCapabilities { ButtonCount = 0 });
        Assert.Empty(buttons);
    }

    [Fact]
    public void Scan_all_accumulates_and_ignores_duplicates()
    {
        var model = new DeviceModel { DeviceId = "mouse_0000_0001_abc" };
        var learning = new ButtonLearningService();
        learning.StartScanAll(model.DeviceId);

        var first = learning.Observe(model, Down(7), null);
        var again = learning.Observe(model, Down(7), null);
        var extra = learning.Observe(model, Down(8), null);

        Assert.True(first.Added);
        Assert.True(again.Duplicate);
        Assert.True(extra.Added);
        Assert.Equal(2, model.Buttons.Count);
        Assert.Equal(LearningMode.ScanAll, learning.Mode);
    }

    [Fact]
    public void Keyboard_remap_is_kept_as_unknown_button_with_source()
    {
        var model = new DeviceModel { DeviceId = "mouse_aaaa_bbbb_x" };
        var learning = new ButtonLearningService();
        learning.StartSingle(model.DeviceId);
        var result = learning.Observe(model, new RawInputEvent
        {
            Source = InputSource.Keyboard,
            IsDown = true,
            VirtualKey = 0x7C
        }, null);

        Assert.True(result.Added);
        Assert.Equal(InputSource.Keyboard, result.Button!.Source);
        Assert.StartsWith("未知按鍵", result.Button.DisplayName);
        Assert.Equal("F13", result.Button.InputSummary);
        Assert.Equal(LearningMode.Off, learning.Mode);
    }

    [Fact]
    public void Mouse_events_from_another_device_are_ignored()
    {
        var model = new DeviceModel { DeviceId = "mouse_a" };
        var other = new PhysicalMouseDevice
        {
            Fingerprint = DeviceFingerprint.Create(1, 2, "other", "other", "other", 1, 2)
        };
        var learning = new ButtonLearningService();
        learning.StartScanAll("mouse_a");
        var result = learning.Observe(model, Down(4), other);
        Assert.False(result.Added);
        Assert.Empty(model.Buttons);
    }

    private static RawInputEvent Down(int button) => new()
    {
        Source = InputSource.Mouse,
        IsDown = true,
        RawButtonIndex = button,
        HidUsageId = button
    };
}
