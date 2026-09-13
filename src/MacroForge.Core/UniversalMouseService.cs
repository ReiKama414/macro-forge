using MacroForge.Core.Devices;
using MacroForge.Core.Input;
using MacroForge.Core.Layout;
using MacroForge.Core.Learning;
using MacroForge.Core.Macros;
using MacroForge.Core.Model;
using MacroForge.Core.Native;
using MacroForge.Core.Scope;
using MacroForge.Core.Storage;

namespace MacroForge.Core;

public sealed class ManagedMouse
{
    public PhysicalMouseDevice Physical { get; set; }
    public DeviceSnapshot Snapshot { get; set; }
    public bool IsConnected { get; set; }
    public bool IsNew { get; set; }

    public string DeviceId => Snapshot.Device.DeviceId;
    public string DisplayName => string.IsNullOrWhiteSpace(Physical.DisplayName)
        ? Snapshot.Device.ProductName
        : Physical.DisplayName;
    public string ConnectionLabel => IsConnected ? "已連接" : "已中斷";
    public int ButtonCount => Snapshot.Device.Buttons.Count;

    public ManagedMouse(PhysicalMouseDevice physical, DeviceSnapshot snapshot, bool isNew)
    {
        Physical = physical;
        Snapshot = snapshot;
        IsConnected = physical.Connection == ConnectionState.Connected;
        IsNew = isNew;
    }
}

public sealed class DeviceConnectionEvent
{
    public required ManagedMouse Mouse { get; init; }
    public required bool Connected { get; init; }
    public required string Message { get; init; }
}

public sealed class NewDevicePrompt
{
    public required PhysicalMouseDevice Physical { get; init; }
    public required int DetectedStandardButtons { get; init; }
}

public sealed class UniversalMouseService
{
    private readonly DeviceStore _store;
    private readonly List<ManagedMouse> _mice = new();
    private long _lastHotPlugMs;

    public ButtonLearningService Learning { get; } = new();
    public MacroEngine Macros { get; } = new();

    public IReadOnlyList<ManagedMouse> Mice => _mice;
    public ManagedMouse? Selected { get; private set; }

    public event Action? DevicesChanged;
    public event Action<DeviceConnectionEvent>? ConnectionChanged;
    public event Action<NewDevicePrompt>? NewDeviceFound;
    public event Action<LearningResult>? LearningUpdated;
    public event Action<string>? Status;
    public event Action? MacrosChanged;
    public ForegroundApp? Foreground { get; private set; }

    private CancellationTokenSource? _runtimeCts;

    /// <summary>
    /// Master switch. While false, physical mouse buttons never trigger macros —
    /// the app stays inert until the user explicitly arms it.
    /// </summary>
    public bool TriggersEnabled { get; private set; }

    public UniversalMouseService(DeviceStore? store = null)
    {
        _store = store ?? new DeviceStore();
        Learning.ButtonObserved += result => LearningUpdated?.Invoke(result);
        Macros.Scheduler.Changed += () => MacrosChanged?.Invoke();
    }

    public void SetTriggersEnabled(bool enabled)
    {
        if (TriggersEnabled == enabled)
            return;
        TriggersEnabled = enabled;
        if (!enabled)
            Macros.Scheduler.StopAll();
        Status?.Invoke(enabled ? "已啟用：滑鼠按鍵可觸發巨集" : "已停用：滑鼠按鍵不會觸發巨集");
    }

    public void StartRuntime()
    {
        _runtimeCts?.Cancel();
        _runtimeCts = new CancellationTokenSource();
        _ = MonitorForegroundAsync(_runtimeCts.Token);
    }

    public void StopRuntime()
    {
        TriggersEnabled = false;
        _runtimeCts?.Cancel();
        _runtimeCts = null;
        Macros.Scheduler.EmergencyStop();
    }

    public void Refresh(bool announceHotPlug = false)
    {
        var scanned = MouseEnumerator.Scan();
        var scannedIds = new HashSet<string>(scanned.Select(s => s.Fingerprint.DeviceId));

        foreach (var physical in scanned)
        {
            var existing = _mice.FirstOrDefault(m => m.DeviceId == physical.Fingerprint.DeviceId);
            var isStored = _store.Exists(physical.Fingerprint.DeviceId);
            if (existing is null)
            {
                if (isStored)
                {
                    var loaded = _store.Load(physical.Fingerprint.DeviceId);
                    MergeLiveIdentity(loaded.Device, physical);
                    var managed = new ManagedMouse(physical, loaded, isNew: false) { IsConnected = true };
                    LayoutFactory.EnsureNodes(managed.Snapshot.Layout, managed.Snapshot.Device.Buttons);
                    _mice.Add(managed);
                    if (announceHotPlug)
                    {
                        ConnectionChanged?.Invoke(new DeviceConnectionEvent
                        {
                            Mouse = managed,
                            Connected = true,
                            Message = $"裝置已連接：{managed.DisplayName}"
                        });
                    }
                }
                else
                {
                    NewDeviceFound?.Invoke(new NewDevicePrompt
                    {
                        Physical = physical,
                        DetectedStandardButtons = Math.Max(physical.Capabilities.ButtonCount, physical.Capabilities.HidButtonUsages.Count)
                    });
                    var draft = BuildDraft(physical);
                    var managed = new ManagedMouse(physical, draft, isNew: true) { IsConnected = true };
                    _mice.Add(managed);
                    if (announceHotPlug)
                    {
                        ConnectionChanged?.Invoke(new DeviceConnectionEvent
                        {
                            Mouse = managed,
                            Connected = true,
                            Message = $"發現新的滑鼠：{managed.DisplayName}"
                        });
                    }
                }
            }
            else
            {
                var wasConnected = existing.IsConnected;
                existing.Physical = physical;
                existing.IsConnected = true;
                MergeLiveIdentity(existing.Snapshot.Device, physical);
                if (announceHotPlug && !wasConnected)
                {
                    ConnectionChanged?.Invoke(new DeviceConnectionEvent
                    {
                        Mouse = existing,
                        Connected = true,
                        Message = $"裝置已連接：{existing.DisplayName}"
                    });
                }
            }
        }

        foreach (var mouse in _mice)
        {
            if (scannedIds.Contains(mouse.DeviceId))
                continue;
            if (!mouse.IsConnected)
                continue;
            mouse.IsConnected = false;
            if (announceHotPlug)
            {
                ConnectionChanged?.Invoke(new DeviceConnectionEvent
                {
                    Mouse = mouse,
                    Connected = false,
                    Message = $"裝置已中斷：{mouse.DisplayName}"
                });
            }
        }

        Selected ??= _mice.FirstOrDefault(m => m.IsConnected) ?? _mice.FirstOrDefault();
        DevicesChanged?.Invoke();
    }

    public ManagedMouse CreateDeviceModel(PhysicalMouseDevice physical)
    {
        var existing = _mice.FirstOrDefault(m => m.DeviceId == physical.Fingerprint.DeviceId);
        var buttons = ButtonLearningService.SeedFromCapabilities(physical.Capabilities);
        var model = new DeviceModel
        {
            DeviceId = physical.Fingerprint.DeviceId,
            Manufacturer = physical.Manufacturer,
            ProductName = physical.ProductName,
            FriendlyName = physical.FriendlyName,
            Vid = physical.Fingerprint.Vid.ToString("x4"),
            Pid = physical.Fingerprint.Pid.ToString("x4"),
            InstanceId = physical.InstanceId,
            ContainerId = physical.ContainerId,
            DevicePath = physical.DevicePath,
            UsagePage = physical.Capabilities.UsagePage,
            Usage = physical.Capabilities.Usage,
            Buttons = buttons
        };
        var layout = LayoutFactory.CreateDefault(buttons);
        var snapshot = _store.Create(model, layout);
        if (existing is null)
        {
            existing = new ManagedMouse(physical, snapshot, isNew: false);
            _mice.Add(existing);
        }
        else
        {
            existing.Snapshot = snapshot;
            existing.IsNew = false;
            existing.Physical = physical;
        }

        Selected = existing;
        DevicesChanged?.Invoke();
        Status?.Invoke($"已建立裝置模型：{existing.DisplayName}");
        return existing;
    }

    public void Select(string deviceId)
    {
        Selected = _mice.FirstOrDefault(m => m.DeviceId == deviceId);
    }

    public void SaveSelected()
    {
        if (Selected is null) return;
        LayoutFactory.EnsureNodes(Selected.Snapshot.Layout, Selected.Snapshot.Device.Buttons);
        _store.Save(Selected.Snapshot);
    }

    public void HandleRawInput(IntPtr lParam)
    {
        var events = RawInputParser.Parse(lParam);
        foreach (var ev in events)
            HandleEvent(ev);
    }

    public void HandleEvent(RawInputEvent ev)
    {
        if (ev.Source == InputSource.Keyboard && ev.VirtualKey == Win32.VK_F12 && ev.IsDown)
        {
            Macros.Scheduler.EmergencyStop();
            Status?.Invoke("緊急停止：全部巨集已取消");
            return;
        }

        var attributed = AttributeDevice(ev);
        if (Learning.Mode != LearningMode.Off && Selected is not null)
        {
            var result = Learning.Observe(Selected.Snapshot.Device, ev, attributed);
            if (result.Added)
            {
                LayoutFactory.EnsureNodes(Selected.Snapshot.Layout, Selected.Snapshot.Device.Buttons);
                SaveSelected();
            }
            return;
        }

        if (!TriggersEnabled)
            return;

        if (attributed is null)
            return;
        var managed = _mice.FirstOrDefault(m => m.DeviceId == attributed.Fingerprint.DeviceId);
        if (managed is null)
            return;
        var button = managed.Snapshot.Device.Buttons.FirstOrDefault(b => Matches(b, ev));
        if (button is null)
            return;

        var app = ForegroundProcess.GetForegroundProcess();
        ApplyProfile(managed, app);
        Macros.HandleButton(button, ev.IsDown, managed.Snapshot.Bindings, app);
    }

    public void ApplyProfile(ManagedMouse mouse, ForegroundApp? app)
    {
        var profile = BindingResolver.ResolveProfile(mouse.Snapshot.Bindings.Profiles, app);
        var id = profile?.Id;
        if (mouse.Snapshot.Bindings.ActiveProfileId == id)
            return;
        mouse.Snapshot.Bindings.ActiveProfileId = id;
        if (profile is not null)
            Status?.Invoke($"已切換 Profile：{profile.Name}");
    }

    public PhysicalMouseDevice? AttributeDevice(RawInputEvent ev)
    {
        foreach (var mouse in _mice.Where(m => m.IsConnected))
        {
            if (mouse.Physical.OwnsHandle(ev.DeviceHandle))
                return mouse.Physical;
        }
        return null;
    }

    public void AddVirtualButton(string displayName)
    {
        if (Selected is null) return;
        var n = Selected.Snapshot.Device.Buttons.Count(b => b.IsUnknown) + 1;
        var button = new MouseButtonDefinition
        {
            Id = $"virt_{Guid.NewGuid():N}"[..12],
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"未知按鍵 {n}" : displayName,
            IsUnknown = true,
            Source = InputSource.UnknownHid,
            InputSummary = "虛擬按鍵，尚未綁定真實 Input"
        };
        Selected.Snapshot.Device.Buttons.Add(button);
        LayoutFactory.EnsureNodes(Selected.Snapshot.Layout, Selected.Snapshot.Device.Buttons);
        SaveSelected();
        DevicesChanged?.Invoke();
    }

    public void DeleteButton(string buttonId)
    {
        if (Selected is null) return;
        Selected.Snapshot.Device.Buttons.RemoveAll(b => b.Id == buttonId);
        Selected.Snapshot.Layout.Buttons.RemoveAll(b => b.ButtonId == buttonId);
        Selected.Snapshot.Bindings.Bindings.RemoveAll(b => string.Equals(b.TargetButton, buttonId, StringComparison.OrdinalIgnoreCase));
        SaveSelected();
        DevicesChanged?.Invoke();
    }

    public void RenameButton(string buttonId, string name)
    {
        if (Selected is null) return;
        var button = Selected.Snapshot.Device.Buttons.FirstOrDefault(b => b.Id == buttonId);
        if (button is null) return;
        button.DisplayName = name;
        button.PhysicalName = name;
        SaveSelected();
        DevicesChanged?.Invoke();
    }

    public void RestoreDefaultLayout()
    {
        if (Selected is null) return;
        Selected.Snapshot.Layout = LayoutFactory.CreateDefault(Selected.Snapshot.Device.Buttons);
        SaveSelected();
        DevicesChanged?.Invoke();
    }

    public void ResetDetectedButtons()
    {
        if (Selected is null) return;
        Selected.Snapshot.Device.Buttons = ButtonLearningService.SeedFromCapabilities(Selected.Physical.Capabilities);
        Selected.Snapshot.Layout = LayoutFactory.CreateDefault(Selected.Snapshot.Device.Buttons);
        SaveSelected();
        DevicesChanged?.Invoke();
    }

    public void UpsertBinding(ButtonBinding binding)
    {
        if (Selected is null) return;
        if (string.IsNullOrWhiteSpace(binding.Id))
            binding.Id = Guid.NewGuid().ToString("N")[..12];
        Selected.Snapshot.Bindings.Bindings.RemoveAll(b => b.Id == binding.Id);
        Selected.Snapshot.Bindings.Bindings.Add(binding);
        SaveSelected();
        DevicesChanged?.Invoke();
    }

    public void RemoveBinding(string bindingId)
    {
        if (Selected is null) return;
        Selected.Snapshot.Bindings.Bindings.RemoveAll(b => b.Id == bindingId);
        Macros.Scheduler.Stop(bindingId);
        SaveSelected();
        DevicesChanged?.Invoke();
    }

    public void UpsertProfile(ProfileDefinition profile)
    {
        if (Selected is null) return;
        if (string.IsNullOrWhiteSpace(profile.Id))
            profile.Id = Guid.NewGuid().ToString("N")[..8];
        Selected.Snapshot.Bindings.Profiles.RemoveAll(p => p.Id == profile.Id);
        Selected.Snapshot.Bindings.Profiles.Add(profile);
        SaveSelected();
        DevicesChanged?.Invoke();
    }

    public void BindMacro(string buttonId, MacroDefinition macro)
    {
        if (Selected is null) return;
        UpsertBinding(new ButtonBinding
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Button = buttonId,
            Name = macro.Name,
            Scope = new ScopeDefinition { Type = "global" },
            Action = new BindingAction { Type = "macro" },
            Macro = macro,
            Trigger = "once",
            IntervalMs = 1000
        });
    }

    private async Task MonitorForegroundAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Foreground = ForegroundProcess.GetForegroundProcess();
                foreach (var mouse in _mice)
                    ApplyProfile(mouse, Foreground);
            }
            catch
            {
                // ignore query failures
            }
            await Task.Delay(150, ct);
        }
    }

    public bool HandleWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_INPUT)
        {
            HandleRawInput(lParam);
            return true;
        }

        if (msg is Win32.WM_DEVICECHANGE or Win32.WM_INPUT_DEVICE_CHANGE)
        {
            var now = Environment.TickCount64;
            if (now - _lastHotPlugMs < 250)
                return true;
            _lastHotPlugMs = now;
            Refresh(announceHotPlug: true);
            return true;
        }

        return false;
    }

    public static string LimitationText =>
        "此按鍵目前沒有提供標準 Windows Input Event。可能由滑鼠驅動程式或裝置 Firmware 管理。軟體不會建立假的 Button Event。";

    private static DeviceSnapshot BuildDraft(PhysicalMouseDevice physical)
    {
        var buttons = ButtonLearningService.SeedFromCapabilities(physical.Capabilities);
        return new DeviceSnapshot
        {
            Device = new DeviceModel
            {
                DeviceId = physical.Fingerprint.DeviceId,
                Manufacturer = physical.Manufacturer,
                ProductName = physical.ProductName,
                FriendlyName = physical.FriendlyName,
                Vid = physical.Fingerprint.Vid.ToString("x4"),
                Pid = physical.Fingerprint.Pid.ToString("x4"),
                InstanceId = physical.InstanceId,
                ContainerId = physical.ContainerId,
                DevicePath = physical.DevicePath,
                UsagePage = physical.Capabilities.UsagePage,
                Usage = physical.Capabilities.Usage,
                Buttons = buttons
            },
            Layout = LayoutFactory.CreateDefault(buttons),
            Bindings = new BindingsModel()
        };
    }

    private static void MergeLiveIdentity(DeviceModel model, PhysicalMouseDevice physical)
    {
        model.Manufacturer = physical.Manufacturer;
        model.ProductName = physical.ProductName;
        model.FriendlyName = physical.FriendlyName;
        model.DevicePath = physical.DevicePath;
        model.InstanceId = physical.InstanceId;
        model.ContainerId = physical.ContainerId;
        model.UsagePage = physical.Capabilities.UsagePage;
        model.Usage = physical.Capabilities.Usage;
    }

    private static bool Matches(MouseButtonDefinition button, RawInputEvent ev)
    {
        if (button.Source != ev.Source)
            return false;
        if (button.RawButtonIndex is not null)
            return button.RawButtonIndex == ev.RawButtonIndex;
        if (button.VirtualKey is not null)
            return button.VirtualKey == ev.VirtualKey;
        if (button.ConsumerUsage is not null)
            return button.ConsumerUsage == ev.ConsumerUsage;
        return false;
    }
}
