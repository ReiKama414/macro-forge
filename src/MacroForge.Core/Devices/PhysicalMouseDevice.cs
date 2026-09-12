using MacroForge.Core.Hid;
using MacroForge.Core.Model;

namespace MacroForge.Core.Devices;

public sealed class HidInterfaceInfo
{
    public IntPtr Handle { get; init; }
    public uint RawType { get; init; }
    public string DevicePath { get; init; } = "";
    public ushort UsagePage { get; init; }
    public ushort Usage { get; init; }
    public string Role { get; init; } = "Unknown";
}

public sealed class PhysicalMouseDevice
{
    public DeviceFingerprint Fingerprint { get; init; }
    public string Manufacturer { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string FriendlyName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? InstanceId { get; init; }
    public string? ContainerId { get; init; }
    public string? DevicePath { get; init; }
    public IntPtr PrimaryHandle { get; init; }
    public DeviceCapabilities Capabilities { get; init; } = new();
    public IReadOnlyList<HidInterfaceInfo> Interfaces { get; init; } = Array.Empty<HidInterfaceInfo>();
    public ConnectionState Connection { get; set; } = ConnectionState.Connected;
    public bool IsUnknownHid { get; init; }

    public bool OwnsHandle(IntPtr hDevice) =>
        PrimaryHandle == hDevice || Interfaces.Any(i => i.Handle == hDevice);
}
