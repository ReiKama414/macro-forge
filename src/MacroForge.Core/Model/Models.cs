namespace MacroForge.Core.Model;

public enum InputSource
{
    Mouse,
    Keyboard,
    ConsumerHid,
    UnknownHid
}

public enum ConnectionState
{
    Connected,
    Disconnected
}

public sealed class HidCollectionInfo
{
    public ushort UsagePage { get; init; }
    public ushort Usage { get; init; }
    public string Kind { get; init; } = "";
}

public sealed class DeviceCapabilities
{
    public int ButtonCount { get; init; }
    public bool HasWheel { get; init; }
    public bool HasHorizontalWheel { get; init; }
    public ushort UsagePage { get; init; }
    public ushort Usage { get; init; }
    public int InputReportByteLength { get; init; }
    public IReadOnlyList<int> HidButtonUsages { get; init; } = Array.Empty<int>();
    public IReadOnlyList<HidCollectionInfo> Collections { get; init; } = Array.Empty<HidCollectionInfo>();
    public byte[]? ReportDescriptor { get; init; }
    public string? ReportDescriptorHex { get; init; }
}

public sealed class MouseButtonDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? PhysicalName { get; set; }
    public bool IsUnknown { get; set; }
    public InputSource Source { get; set; }
    public int? RawButtonIndex { get; set; }
    public int? HidUsageId { get; set; }
    public int? VirtualKey { get; set; }
    public int? ScanCode { get; set; }
    public int? ConsumerUsage { get; set; }
    public string? HidReportHex { get; set; }
    public string? DeviceHandle { get; set; }
    public string InputSummary { get; set; } = "";

    public string Signature =>
        $"{Source}|raw:{RawButtonIndex}|hid:{HidUsageId}|vk:{VirtualKey}|scan:{ScanCode}|con:{ConsumerUsage}|report:{HidReportHex ?? ""}";
}

public sealed class DeviceModel
{
    public string DeviceId { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string Vid { get; set; } = "";
    public string Pid { get; set; } = "";
    public string? InstanceId { get; set; }
    public string? ContainerId { get; set; }
    public string? DevicePath { get; set; }
    public ushort UsagePage { get; set; }
    public ushort Usage { get; set; }
    public List<MouseButtonDefinition> Buttons { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LayoutNode
{
    public string ButtonId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class LayoutModel
{
    public List<LayoutNode> Buttons { get; set; } = new();
}

public sealed class ScopeDefinition
{
    public string Type { get; set; } = "global";
    public List<string> Applications { get; set; } = new();
    public string Match { get; set; } = "exe";
    public string OnLeave { get; set; } = "pause";

    public bool IsGlobal => string.Equals(Type, "global", StringComparison.OrdinalIgnoreCase);
    public bool IsApplications => string.Equals(Type, "applications", StringComparison.OrdinalIgnoreCase);
}

public sealed class BindingAction
{
    public string Type { get; set; } = "macro";
    public string? MacroId { get; set; }
    public string? Key { get; set; }
    public List<string> Keys { get; set; } = new();
    public string? MouseButton { get; set; }
}

public sealed class MacroAction
{
    public string Type { get; set; } = "key";
    public List<string> Keys { get; set; } = new();
    public int DelayMs { get; set; }
    public string? Text { get; set; }
    public string? MouseButton { get; set; }
}

public sealed class MacroDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<MacroAction> Actions { get; set; } = new();
}

public sealed class ButtonBinding
{
    public string Id { get; set; } = "";
    public string Button { get; set; } = "";
    public string? ButtonId { get; set; }
    public string? ProfileId { get; set; }
    public string Name { get; set; } = "";
    public ScopeDefinition Scope { get; set; } = new();
    public BindingAction Action { get; set; } = new();
    public MacroDefinition? Macro { get; set; }
    public string Trigger { get; set; } = "toggle";
    public int IntervalMs { get; set; } = 1000;
    public int RepeatCount { get; set; } = 1;
    public bool Enabled { get; set; } = true;

    public string TargetButton => !string.IsNullOrWhiteSpace(Button) ? Button : ButtonId ?? "";
}

public sealed class ProfileDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ScopeDefinition Scope { get; set; } = new();
}

public sealed class BindingsModel
{
    public List<ButtonBinding> Bindings { get; set; } = new();
    public List<MacroDefinition> Macros { get; set; } = new();
    public List<ProfileDefinition> Profiles { get; set; } = new();
    public string? ActiveProfileId { get; set; }
}

public sealed class DeviceSnapshot
{
    public DeviceModel Device { get; set; } = new();
    public LayoutModel Layout { get; set; } = new();
    public BindingsModel Bindings { get; set; } = new();
}

public sealed class ForegroundApp
{
    public string ProcessName { get; init; } = "";
    public string ExecutablePath { get; init; } = "";
    public uint ProcessId { get; init; }

    public string ExeFileName =>
        string.IsNullOrWhiteSpace(ExecutablePath)
            ? ProcessName
            : Path.GetFileName(ExecutablePath);
}

public sealed class RunningMacroSnapshot
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string State { get; init; } = "stopped";
    public string ScopeLabel { get; init; } = "全域";
    public int IntervalMs { get; init; }
    public int ExecutionCount { get; init; }
    public DateTimeOffset? NextRunAt { get; init; }
    public bool InScope { get; init; }
}
