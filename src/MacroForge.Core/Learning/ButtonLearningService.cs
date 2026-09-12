using MacroForge.Core.Devices;
using MacroForge.Core.Input;
using MacroForge.Core.Model;

namespace MacroForge.Core.Learning;

public enum LearningMode
{
    Off,
    SingleButton,
    ScanAll
}

public sealed class LearningResult
{
    public bool Added { get; init; }
    public bool Duplicate { get; init; }
    public MouseButtonDefinition? Button { get; init; }
    public string Message { get; init; } = "";
}

public sealed class ButtonLearningService
{
    public LearningMode Mode { get; private set; } = LearningMode.Off;
    public string? TargetDeviceId { get; private set; }

    public event Action<LearningResult>? ButtonObserved;

    public void StartSingle(string deviceId)
    {
        Mode = LearningMode.SingleButton;
        TargetDeviceId = deviceId;
    }

    public void StartScanAll(string deviceId)
    {
        Mode = LearningMode.ScanAll;
        TargetDeviceId = deviceId;
    }

    public void Stop()
    {
        Mode = LearningMode.Off;
        TargetDeviceId = null;
    }

    public LearningResult Observe(
        DeviceModel model,
        RawInputEvent input,
        PhysicalMouseDevice? attributedDevice)
    {
        if (Mode == LearningMode.Off)
            return new LearningResult { Message = "學習模式未啟動。" };

        if (attributedDevice is not null && attributedDevice.Fingerprint.DeviceId != model.DeviceId)
        {
            if (input.Source == InputSource.Mouse)
                return new LearningResult { Message = "此輸入來自其他滑鼠。" };
        }

        if (TargetDeviceId != model.DeviceId)
            return new LearningResult { Message = "目前不是這隻滑鼠的學習對象。" };

        if (!input.IsDown)
            return new LearningResult { Message = "忽略按鍵放開。" };

        if (IsMovementOnly(input))
            return new LearningResult { Message = "忽略游標移動。" };

        var button = CreateButton(model, input);
        var existing = model.Buttons.FirstOrDefault(b => b.Signature == button.Signature);
        if (existing is not null)
        {
            var dup = new LearningResult
            {
                Duplicate = true,
                Button = existing,
                Message = $"已存在：{existing.DisplayName}"
            };
            ButtonObserved?.Invoke(dup);
            if (Mode == LearningMode.SingleButton)
                Stop();
            return dup;
        }

        model.Buttons.Add(button);
        model.UpdatedAt = DateTimeOffset.UtcNow;
        var added = new LearningResult
        {
            Added = true,
            Button = button,
            Message = $"已加入 {button.DisplayName}（來源：{SourceLabel(button.Source)}）"
        };
        ButtonObserved?.Invoke(added);
        if (Mode == LearningMode.SingleButton)
            Stop();
        return added;
    }

    public static MouseButtonDefinition SeedStandardButton(int hidUsage)
    {
        return new MouseButtonDefinition
        {
            Id = $"btn_{hidUsage}",
            DisplayName = StandardName(hidUsage),
            Source = InputSource.Mouse,
            RawButtonIndex = hidUsage,
            HidUsageId = hidUsage,
            IsUnknown = false,
            InputSummary = hidUsage <= 5 ? StandardName(hidUsage) : $"HID Button {hidUsage}"
        };
    }

    public static List<MouseButtonDefinition> SeedFromCapabilities(DeviceCapabilities caps)
    {
        var usages = caps.HidButtonUsages.Count > 0
            ? caps.HidButtonUsages.ToList()
            : Enumerable.Range(1, Math.Max(0, caps.ButtonCount)).ToList();

        return usages
            .Where(u => u > 0)
            .Distinct()
            .OrderBy(u => u)
            .Select(SeedStandardButton)
            .ToList();
    }

    private static MouseButtonDefinition CreateButton(DeviceModel model, RawInputEvent input)
    {
        if (input.Source == InputSource.Mouse && input.RawButtonIndex is int idx)
        {
            if (idx is 1001 or 1002 or 1003 or 1004)
            {
                return new MouseButtonDefinition
                {
                    Id = NextId(model, "wheel"),
                    DisplayName = idx switch
                    {
                        1001 => "滾輪上",
                        1002 => "滾輪下",
                        1003 => "水平滾輪右",
                        _ => "水平滾輪左"
                    },
                    Source = InputSource.Mouse,
                    RawButtonIndex = idx,
                    InputSummary = $"Wheel {input.WheelDelta}"
                };
            }

            return new MouseButtonDefinition
            {
                Id = $"btn_{idx}",
                DisplayName = StandardName(idx),
                Source = InputSource.Mouse,
                RawButtonIndex = idx,
                HidUsageId = input.HidUsageId ?? idx,
                DeviceHandle = input.DeviceHandle.ToString(),
                HidReportHex = input.HidReport is null ? null : Convert.ToHexString(input.HidReport),
                InputSummary = $"HID Button {idx}"
            };
        }

        if (input.Source == InputSource.Keyboard && input.VirtualKey is int vk)
        {
            return new MouseButtonDefinition
            {
                Id = NextId(model, "key"),
                DisplayName = NextUnknownName(model),
                IsUnknown = true,
                Source = InputSource.Keyboard,
                VirtualKey = vk,
                ScanCode = input.ScanCode,
                DeviceHandle = input.DeviceHandle.ToString(),
                InputSummary = VirtualKeyNames.GetName(vk)
            };
        }

        if (input.Source is InputSource.ConsumerHid || input.ConsumerUsage is not null)
        {
            var consumer = input.ConsumerUsage ?? 0;
            return new MouseButtonDefinition
            {
                Id = NextId(model, "con"),
                DisplayName = NextUnknownName(model),
                IsUnknown = true,
                Source = InputSource.ConsumerHid,
                ConsumerUsage = consumer,
                HidUsageId = consumer,
                DeviceHandle = input.DeviceHandle.ToString(),
                HidReportHex = input.HidReport is null ? null : Convert.ToHexString(input.HidReport),
                InputSummary = $"Consumer 0x{consumer:X}"
            };
        }

        return new MouseButtonDefinition
        {
            Id = NextId(model, "unk"),
            DisplayName = NextUnknownName(model),
            IsUnknown = true,
            Source = input.Source,
            HidReportHex = input.HidReport is null ? null : Convert.ToHexString(input.HidReport),
            DeviceHandle = input.DeviceHandle.ToString(),
            InputSummary = "未知 HID Report"
        };
    }

    private static bool IsMovementOnly(RawInputEvent input) =>
        input.Source == InputSource.Mouse &&
        input.RawButtonIndex is null &&
        input.HidUsageId is null &&
        input.WheelDelta is null;

    private static string NextId(DeviceModel model, string prefix)
    {
        var n = 1;
        string id;
        do
        {
            id = $"{prefix}_{n++}";
        } while (model.Buttons.Any(b => b.Id == id));
        return id;
    }

    private static string NextUnknownName(DeviceModel model)
    {
        var n = model.Buttons.Count(b => b.IsUnknown) + 1;
        return $"未知按鍵 {n}";
    }

    public static string StandardName(int usage) => usage switch
    {
        1 => "左鍵",
        2 => "右鍵",
        3 => "中鍵",
        _ => $"Button {usage}"
    };

    public static string SourceLabel(InputSource source) => source switch
    {
        InputSource.Mouse => "Mouse",
        InputSource.Keyboard => "Keyboard",
        InputSource.ConsumerHid => "Consumer HID",
        _ => "Unknown HID"
    };
}
