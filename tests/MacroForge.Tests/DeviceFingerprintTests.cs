using MacroForge.Core.Devices;

namespace MacroForge.Tests;

public class DeviceFingerprintTests
{
    [Fact]
    public void Uses_vid_pid_and_stable_unique_token()
    {
        var a = DeviceFingerprint.Create(0x046D, 0xC08B, "{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}", "HID\\VID_046D&PID_C08B\\1", @"\\?\HID#VID_046D&PID_C08B", 1, 2);
        var b = DeviceFingerprint.Create(0x046D, 0xC08B, "{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}", "HID\\VID_046D&PID_C08B\\1", @"\\?\HID#VID_046D&PID_C08B", 1, 2);
        Assert.Equal(a.DeviceId, b.DeviceId);
        Assert.StartsWith("mouse_046d_c08b_", a.DeviceId);
    }

    [Fact]
    public void Different_containers_are_different_devices()
    {
        var a = DeviceFingerprint.Create(0x1532, 0x00C1, "{11111111-1111-1111-1111-111111111111}", null, "path-a", 1, 2);
        var b = DeviceFingerprint.Create(0x1532, 0x00C1, "{22222222-2222-2222-2222-222222222222}", null, "path-b", 1, 2);
        Assert.NotEqual(a.DeviceId, b.DeviceId);
    }

    [Fact]
    public void Parses_vid_pid_from_any_path_shape()
    {
        Assert.True(DeviceFingerprint.TryParseVidPid(@"\\?\HID#VID_046D&PID_C08B#7&abc#{guid}", out var vid, out var pid));
        Assert.Equal(0x046D, vid);
        Assert.Equal(0xC08B, pid);
    }
}
