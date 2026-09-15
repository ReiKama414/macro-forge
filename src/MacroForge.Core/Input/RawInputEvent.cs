using MacroForge.Core.Model;

namespace MacroForge.Core.Input;

public sealed class RawInputEvent
{
    public IntPtr DeviceHandle { get; init; }
    public InputSource Source { get; init; }
    public bool IsDown { get; init; }
    public int? RawButtonIndex { get; init; }
    public int? HidUsageId { get; init; }
    public int? VirtualKey { get; init; }
    public int? ScanCode { get; init; }
    public int? ConsumerUsage { get; init; }
    public int? WheelDelta { get; init; }
    public byte[]? HidReport { get; init; }
    public string? DevicePathHint { get; init; }

    public string Signature =>
        $"{Source}|raw:{RawButtonIndex}|hid:{HidUsageId}|vk:{VirtualKey}|scan:{ScanCode}|con:{ConsumerUsage}|report:{ReportKey}";

    public string ReportKey => HidReport is { Length: > 0 }
        ? Convert.ToHexString(HidReport)
        : "";
}
