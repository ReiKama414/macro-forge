using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MacroForge.Core;
using MacroForge.Core.Devices;
using MacroForge.Core.Learning;
using MacroForge.Core.Macros;
using MacroForge.Core.Model;
using MacroForge.Core.Scope;

namespace MacroForge.App;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _can;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? can = null)
    {
        _execute = execute;
        _can = can;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}

public sealed class JobRow
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string ButtonName { get; init; } = "";
    public string MouseName { get; init; } = "";
    public string StateText { get; init; } = "";
    public string ScopeLabel { get; init; } = "";
    public string IntervalText { get; init; } = "";
    public string CountText { get; init; } = "";
    public string NextText { get; init; } = "";
    public bool IsLive { get; init; }
    public bool IsIdle => !IsLive;
    public bool IsPaused { get; init; }
    public bool CanPause { get; init; }
    public bool CanResume { get; init; }
    public bool CanStart { get; init; }
}

public sealed class MacroStep : INotifyPropertyChanged
{
    public static IReadOnlyList<string> KindOptions { get; } = new[] { "按鍵盤", "等待", "滑鼠點擊" };
    public static IReadOnlyList<string> ClickOptions { get; } = new[] { "左鍵", "右鍵", "中鍵" };

    private string _kind = "按鍵盤";
    public string Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsKey));
            OnPropertyChanged(nameof(IsWait));
            OnPropertyChanged(nameof(IsClick));
            OnPropertyChanged(nameof(Hint));
        }
    }

    private string _value = "";
    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KeyLabel));
        }
    }

    public string KeyLabel => VirtualKeyNames.DisplayCombo(Value);

    private string _clickButton = "左鍵";
    public string ClickButton { get => _clickButton; set { _clickButton = value; OnPropertyChanged(); } }

    public bool IsKey => Kind == "按鍵盤";
    public bool IsWait => Kind == "等待";
    public bool IsClick => Kind == "滑鼠點擊";
    public string Hint => Kind switch
    {
        "等待" => "要停幾毫秒，例如 500",
        "滑鼠點擊" => "點哪一顆滑鼠鍵",
        _ => "不要手打 1。請按「錄製按鍵」，再按下鍵盤上實際的那一顆。"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    public UniversalMouseService Service { get; }
    public ObservableCollection<ManagedMouse> Mice { get; } = new();
    public ObservableCollection<MouseButtonDefinition> Buttons { get; } = new();
    public ObservableCollection<JobRow> Jobs { get; } = new();
    public ObservableCollection<string> Applications { get; } = new();
    public ObservableCollection<MacroStep> MacroSteps { get; } = new();
    public IReadOnlyList<string> FunctionOptions { get; } = new[] { "巨集", "鍵盤" };
    public IReadOnlyList<string> TriggerOptions { get; } = new[] { "單次執行", "按住循環", "切換循環", "固定次數", "定時執行" };
    public IReadOnlyList<string> LeaveOptions { get; } = new[] { "暫停", "停止", "忽略" };

    private readonly DispatcherTimer _jobTimer;

    private ManagedMouse? _selected;
    public ManagedMouse? Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            if (value is not null)
                Service.Select(value.DeviceId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(DeviceTitle));
            OnPropertyChanged(nameof(DeviceSubtitle));
            OnPropertyChanged(nameof(CapabilityText));
            OnPropertyChanged(nameof(IdentityText));
            OnPropertyChanged(nameof(ProfileText));
            RefreshButtons();
            LoadBindingEditor();
        }
    }

    private MouseButtonDefinition? _selectedButton;
    public MouseButtonDefinition? SelectedButton
    {
        get => _selectedButton;
        set
        {
            _selectedButton = value;
            _renameText = value?.DisplayName ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(RenameText));
            OnPropertyChanged(nameof(HasSelectedButton));
            LoadBindingEditor();
        }
    }

    private string _page = "mouse";
    public string Page
    {
        get => _page;
        set
        {
            _page = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMousePage));
            OnPropertyChanged(nameof(IsAutoPage));
        }
    }
    public bool IsMousePage => Page == "mouse";
    public bool IsAutoPage => Page == "auto";
    public bool HasJobs => Jobs.Count > 0;
    public bool HasNoJobs => Jobs.Count == 0;
    public bool HasSelectedButton => SelectedButton is not null;

    private string _status = "正在掃描滑鼠…";
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

    private string _toast = "";
    public string Toast { get => _toast; set { _toast = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasToast)); } }
    public bool HasToast => !string.IsNullOrWhiteSpace(Toast);

    private bool _editLayout;
    public bool EditLayout { get => _editLayout; set { _editLayout = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditLabel)); } }
    public string EditLabel => EditLayout ? "完成編輯" : "編輯模型";

    public bool IsLearning => Service.Learning.Mode != LearningMode.Off;
    public string LearningTitle => Service.Learning.Mode switch
    {
        LearningMode.SingleButton => "請按下你想加入的滑鼠按鍵",
        LearningMode.ScanAll => "請繼續按其他按鍵",
        _ => ""
    };
    public string LearningHint => UniversalMouseService.LimitationText;
    public NewDevicePrompt? PendingNew { get; private set; }
    public bool HasPendingNew => PendingNew is not null;
    public bool AskScanAfterCreate { get; private set; }
    public bool HasSelection => Selected is not null;
    public string DeviceTitle => Selected?.DisplayName ?? "尚未選擇滑鼠";
    public string DeviceSubtitle => Selected is null ? "只顯示 HID Mouse" : $"{Selected.ButtonCount} 個按鍵 · {Selected.ConnectionLabel}";
    public string CapabilityText => Selected is null ? "" :
        $"VID {Selected.Snapshot.Device.Vid}  PID {Selected.Snapshot.Device.Pid}  ·  HID 按鍵 {Selected.Physical.Capabilities.ButtonCount}";
    public string IdentityText => Selected is null ? "" :
        $"{Selected.Snapshot.Device.DeviceId}\n{Selected.Snapshot.Device.DevicePath}";
    public string ProfileText
    {
        get
        {
            if (Selected is null) return "Profile：無";
            var id = Selected.Snapshot.Bindings.ActiveProfileId;
            var profile = Selected.Snapshot.Bindings.Profiles.FirstOrDefault(p => p.Id == id);
            var fg = Service.Foreground?.ExeFileName ?? "—";
            return $"前景 {fg}  ·  Profile {(profile?.Name ?? "全域")}";
        }
    }

    private string _renameText = "";
    public string RenameText { get => _renameText; set { _renameText = value; OnPropertyChanged(); } }

    private string _bindingId = "";
    private string _functionType = "巨集";
    public string FunctionType { get => _functionType; set { _functionType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsMacroFunction)); } }
    public bool IsMacroFunction => FunctionType == "巨集";

    private string _triggerMode = "切換循環";
    public string TriggerMode { get => _triggerMode; set { _triggerMode = value; OnPropertyChanged(); } }

    private string _intervalText = "1 s";
    public string IntervalText { get => _intervalText; set { _intervalText = value; OnPropertyChanged(); } }

    private string _repeatCountText = "1";
    public string RepeatCountText { get => _repeatCountText; set { _repeatCountText = value; OnPropertyChanged(); } }

    private bool _scopeGlobal = true;
    public bool ScopeGlobal { get => _scopeGlobal; set { _scopeGlobal = value; OnPropertyChanged(); OnPropertyChanged(nameof(ScopeApps)); } }
    public bool ScopeApps { get => !_scopeGlobal; set { ScopeGlobal = !value; } }

    private string _leavePolicy = "暫停";
    public string LeavePolicy { get => _leavePolicy; set { _leavePolicy = value; OnPropertyChanged(); } }

    private string _keyboardKeys = "";
    public string KeyboardKeys { get => _keyboardKeys; set { _keyboardKeys = value; OnPropertyChanged(); OnPropertyChanged(nameof(KeyboardKeysLabel)); } }

    public string MacroPreview
    {
        get
        {
            if (MacroSteps.Count == 0)
                return "還沒有動作。用下面的按鈕新增「按鍵盤／等待／滑鼠點擊」。";
            var i = 1;
            return string.Join("\n", MacroSteps.Select(s => s.Kind switch
            {
                "等待" => $"{i++}. 等待 {s.Value} 毫秒",
                "滑鼠點擊" => $"{i++}. 點滑鼠{s.ClickButton}",
                _ => $"{i++}. {s.KeyLabel}"
            }));
        }
    }

    private string _bindingName = "";
    public string BindingName { get => _bindingName; set { _bindingName = value; OnPropertyChanged(); } }

    private string _matchMode = "exe";
    public string MatchMode { get => _matchMode; set { _matchMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(MatchPath)); } }
    public bool MatchPath { get => MatchMode == "path"; set { MatchMode = value ? "path" : "exe"; } }

    public string AppsLabel => Applications.Count == 0 ? "尚未指定" : string.Join("、", Applications);

    private MacroStep? _recordingStep;
    public MacroStep? RecordingStep
    {
        get => _recordingStep;
        private set
        {
            _recordingStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRecordingKey));
        }
    }
    public bool IsRecordingKey => RecordingStep is not null || _recordingKeyboard;
    private bool _recordingKeyboard;
    public string RecordBanner => "請按下鍵盤上實際的那一顆。上方數字 1（主鍵盤）與右側數字鍵盤 1 會分開記錄。Esc 取消。";

    public string Limitation => UniversalMouseService.LimitationText;

    public ICommand ShowMouseCommand { get; }
    public ICommand ShowAutoCommand { get; }
    public ICommand LearnOneCommand { get; }
    public ICommand ScanAllCommand { get; }
    public ICommand StopLearnCommand { get; }
    public ICommand ToggleEditCommand { get; }
    public ICommand CreateModelCommand { get; }
    public ICommand StartScanAfterCreateCommand { get; }
    public ICommand SkipScanCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteButtonCommand { get; }
    public ICommand AddVirtualCommand { get; }
    public ICommand RestoreLayoutCommand { get; }
    public ICommand RedetectCommand { get; }
    public ICommand DismissToastCommand { get; }
    public ICommand SaveBindingCommand { get; }
    public ICommand PickAppCommand { get; }
    public ICommand UseForegroundAppCommand { get; }
    public ICommand ClearAppsCommand { get; }
    public ICommand EmergencyStopCommand { get; }
    public ICommand StopJobCommand { get; }
    public ICommand PauseJobCommand { get; }
    public ICommand ResumeJobCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand StartJobCommand { get; }
    public ICommand StopAllCommand { get; }
    public ICommand AddWorkProfileCommand { get; }
    public ICommand AddKeyStepCommand { get; }
    public ICommand AddWaitStepCommand { get; }
    public ICommand AddClickStepCommand { get; }
    public ICommand RemoveStepCommand { get; }
    public ICommand RecordKeyCommand { get; }
    public ICommand RecordKeyboardCommand { get; }

    public MainViewModel(UniversalMouseService service)
    {
        Service = service;
        _jobTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _jobTimer.Tick += (_, _) => RefreshJobs();

        service.DevicesChanged += () => Application.Current?.Dispatcher.Invoke(Reload);
        service.MacrosChanged += () => Application.Current?.Dispatcher.Invoke(RefreshJobs);
        service.ConnectionChanged += ev => Application.Current?.Dispatcher.Invoke(() =>
        {
            Toast = ev.Message;
            Status = ev.Message;
            Reload();
        });
        service.NewDeviceFound += prompt => Application.Current?.Dispatcher.Invoke(() =>
        {
            PendingNew = prompt;
            OnPropertyChanged(nameof(PendingNew));
            OnPropertyChanged(nameof(HasPendingNew));
            Toast = $"發現新的滑鼠：{prompt.Physical.DisplayName}";
        });
        service.LearningUpdated += result => Application.Current?.Dispatcher.Invoke(() =>
        {
            Status = result.Message;
            OnPropertyChanged(nameof(IsLearning));
            OnPropertyChanged(nameof(LearningTitle));
            Reload();
        });
        service.Status += text => Application.Current?.Dispatcher.Invoke(() => Status = text);

        ShowMouseCommand = new RelayCommand(_ => Page = "mouse");
        ShowAutoCommand = new RelayCommand(_ =>
        {
            Page = "auto";
            RefreshJobs();
        });
        LearnOneCommand = new RelayCommand(_ =>
        {
            if (Selected is null) return;
            service.Learning.StartSingle(Selected.DeviceId);
            Status = "請按下你想加入的滑鼠按鍵";
            NotifyLearning();
        });
        ScanAllCommand = new RelayCommand(_ => StartScanAll());
        StopLearnCommand = new RelayCommand(_ =>
        {
            service.Learning.Stop();
            Status = "已停止學習";
            NotifyLearning();
        });
        ToggleEditCommand = new RelayCommand(_ => EditLayout = !EditLayout);
        CreateModelCommand = new RelayCommand(_ => CreatePending());
        StartScanAfterCreateCommand = new RelayCommand(_ =>
        {
            AskScanAfterCreate = false;
            OnPropertyChanged(nameof(AskScanAfterCreate));
            StartScanAll();
        });
        SkipScanCommand = new RelayCommand(_ =>
        {
            AskScanAfterCreate = false;
            OnPropertyChanged(nameof(AskScanAfterCreate));
        });
        RenameCommand = new RelayCommand(_ => RenameSelected());
        DeleteButtonCommand = new RelayCommand(_ =>
        {
            if (SelectedButton is null) return;
            service.DeleteButton(SelectedButton.Id);
        });
        AddVirtualCommand = new RelayCommand(_ => service.AddVirtualButton("未知按鍵"));
        RestoreLayoutCommand = new RelayCommand(_ => service.RestoreDefaultLayout());
        RedetectCommand = new RelayCommand(_ => service.ResetDetectedButtons());
        DismissToastCommand = new RelayCommand(_ => Toast = "");
        SaveBindingCommand = new RelayCommand(_ => SaveBinding());
        PickAppCommand = new RelayCommand(_ => PickApp());
        UseForegroundAppCommand = new RelayCommand(_ =>
        {
            var app = ForegroundProcess.GetForegroundProcess();
            if (app is null) return;
            AddApp(app.ExeFileName);
        });
        ClearAppsCommand = new RelayCommand(_ =>
        {
            Applications.Clear();
            OnPropertyChanged(nameof(AppsLabel));
        });
        EmergencyStopCommand = new RelayCommand(_ =>
        {
            Service.Macros.Scheduler.EmergencyStop();
            Status = "緊急停止：全部巨集已取消";
        });
        StopJobCommand = new RelayCommand(p =>
        {
            if (p is string id)
                Service.Macros.Scheduler.Stop(id);
        });
        PauseJobCommand = new RelayCommand(p =>
        {
            if (p is string id)
                Service.Macros.Scheduler.Pause(id);
        });
        ResumeJobCommand = new RelayCommand(p =>
        {
            if (p is string id)
                Service.Macros.Scheduler.Resume(id);
        });
        StopAllCommand = new RelayCommand(_ => Service.Macros.Scheduler.StopAll());
        StartAllCommand = new RelayCommand(_ => StartAllBindings());
        StartJobCommand = new RelayCommand(p =>
        {
            if (p is string id)
                StartJob(id);
        });
        AddWorkProfileCommand = new RelayCommand(_ => AddProfileFromForeground());
        AddKeyStepCommand = new RelayCommand(_ =>
        {
            var step = new MacroStep { Kind = "按鍵盤", Value = "" };
            AddStep(step);
            BeginRecord(step);
        });
        AddWaitStepCommand = new RelayCommand(_ => AddStep(new MacroStep { Kind = "等待", Value = "500" }));
        AddClickStepCommand = new RelayCommand(_ => AddStep(new MacroStep { Kind = "滑鼠點擊", ClickButton = "左鍵" }));
        RemoveStepCommand = new RelayCommand(p =>
        {
            if (p is MacroStep step)
            {
                if (RecordingStep == step)
                    CancelRecord();
                MacroSteps.Remove(step);
                OnPropertyChanged(nameof(MacroPreview));
            }
        });
        RecordKeyCommand = new RelayCommand(p =>
        {
            if (p is MacroStep step)
                BeginRecord(step);
        });
        RecordKeyboardCommand = new RelayCommand(_ =>
        {
            RecordingStep = null;
            _recordingKeyboard = true;
            OnPropertyChanged(nameof(IsRecordingKey));
            Status = RecordBanner;
        });
    }

    public void Start()
    {
        Service.Refresh();
        Service.StartRuntime();
        _jobTimer.Start();
        Reload();
        Status = $"已掃描 {Mice.Count(m => m.IsConnected)} 隻滑鼠 · F12 緊急停止";
    }

    public void Reload()
    {
        var selectedId = Selected?.DeviceId ?? Service.Selected?.DeviceId;
        var buttonId = SelectedButton?.Id;
        Mice.Clear();
        foreach (var mouse in Service.Mice)
            Mice.Add(mouse);
        Selected = Mice.FirstOrDefault(m => m.DeviceId == selectedId) ?? Service.Selected ?? Mice.FirstOrDefault();
        if (buttonId is not null)
            SelectedButton = Buttons.FirstOrDefault(b => b.Id == buttonId);
        OnPropertyChanged(nameof(HasPendingNew));
        OnPropertyChanged(nameof(IsLearning));
        OnPropertyChanged(nameof(LearningTitle));
        OnPropertyChanged(nameof(ProfileText));
        RefreshJobs();
    }

    public void MoveButton(string buttonId, double x, double y)
    {
        if (Selected is null) return;
        var node = Selected.Snapshot.Layout.Buttons.FirstOrDefault(n => n.ButtonId == buttonId);
        if (node is null)
        {
            node = new LayoutNode { ButtonId = buttonId };
            Selected.Snapshot.Layout.Buttons.Add(node);
        }
        node.X = Math.Clamp(x, 0.02, 0.98);
        node.Y = Math.Clamp(y, 0.02, 0.98);
        Service.SaveSelected();
        OnPropertyChanged(nameof(Selected));
    }

    private void RefreshButtons()
    {
        Buttons.Clear();
        if (Selected is null) return;
        foreach (var button in Selected.Snapshot.Device.Buttons)
            Buttons.Add(button);
        OnPropertyChanged(nameof(DeviceTitle));
        OnPropertyChanged(nameof(DeviceSubtitle));
        OnPropertyChanged(nameof(CapabilityText));
        OnPropertyChanged(nameof(IdentityText));
        OnPropertyChanged(nameof(ProfileText));
    }

    private void LoadBindingEditor()
    {
        if (Selected is null || SelectedButton is null)
            return;
        BindingName = SelectedButton.DisplayName;
        var binding = Selected.Snapshot.Bindings.Bindings
            .LastOrDefault(b => BindingResolver.ButtonMatches(b.TargetButton, SelectedButton));
        if (binding is null)
        {
            _bindingId = "";
            FunctionType = "巨集";
            TriggerMode = "切換循環";
            IntervalText = "1 s";
            RepeatCountText = "1";
            ScopeGlobal = true;
            LeavePolicy = "暫停";
            KeyboardKeys = "";
            ResetSteps();
            Applications.Clear();
            OnPropertyChanged(nameof(AppsLabel));
            return;
        }

        _bindingId = binding.Id;
        BindingName = string.IsNullOrWhiteSpace(binding.Name) ? SelectedButton.DisplayName : binding.Name;
        FunctionType = string.Equals(binding.Action.Type, "keyboard", StringComparison.OrdinalIgnoreCase) ? "鍵盤" : "巨集";
        TriggerMode = binding.Trigger switch
        {
            "once" => "單次執行",
            "hold" => "按住循環",
            "count" => "固定次數",
            "interval" => "定時執行",
            _ => "切換循環"
        };
        IntervalText = IntervalParser.Format(binding.IntervalMs <= 0 ? 1000 : binding.IntervalMs);
        RepeatCountText = Math.Max(1, binding.RepeatCount).ToString();
        ScopeGlobal = binding.Scope.IsGlobal;
        LeavePolicy = binding.Scope.OnLeave switch
        {
            "stop" => "停止",
            "ignore" => "忽略",
            _ => "暫停"
        };
        MatchMode = binding.Scope.Match;
        Applications.Clear();
        foreach (var app in binding.Scope.Applications)
            Applications.Add(app);
        var macro = BindingResolver.ToMacro(binding, Selected.Snapshot.Bindings);
        KeyboardKeys = string.Join(" + ", binding.Action.Keys.Count > 0 ? binding.Action.Keys : (string.IsNullOrWhiteSpace(binding.Action.Key) ? Array.Empty<string>() : new[] { binding.Action.Key }));
        LoadSteps(macro.Actions);
        OnPropertyChanged(nameof(AppsLabel));
    }

    private void SaveBinding()
    {
        if (Selected is null || SelectedButton is null) return;
        var trigger = TriggerMode switch
        {
            "單次執行" => "once",
            "按住循環" => "hold",
            "固定次數" => "count",
            "定時執行" => "interval",
            _ => "toggle"
        };
        var leave = LeavePolicy switch
        {
            "停止" => "stop",
            "忽略" => "ignore",
            _ => "pause"
        };
        var keys = KeyboardKeys.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var actions = FunctionType == "鍵盤"
            ? new List<MacroAction> { new() { Type = "key", Keys = keys.ToList() } }
            : StepsToActions();
        if (actions.Count == 0)
        {
            Status = "還沒有動作。請先按「+ 按鍵盤」錄製，或加上等待，再儲存。";
            return;
        }

        var macro = new MacroDefinition
        {
            Id = string.IsNullOrWhiteSpace(_bindingId) ? Guid.NewGuid().ToString("N")[..10] : _bindingId,
            Name = string.IsNullOrWhiteSpace(BindingName) ? SelectedButton.DisplayName : BindingName,
            Actions = actions
        };

        var binding = new ButtonBinding
        {
            Id = string.IsNullOrWhiteSpace(_bindingId) ? Guid.NewGuid().ToString("N")[..12] : _bindingId,
            Button = SelectedButton.RawButtonIndex is int i ? $"mouse_{i}" : SelectedButton.Id,
            Name = macro.Name,
            Trigger = trigger,
            IntervalMs = IntervalParser.ParseToMilliseconds(IntervalText),
            RepeatCount = int.TryParse(RepeatCountText, out var n) ? Math.Max(1, n) : 1,
            Scope = new ScopeDefinition
            {
                Type = ScopeGlobal ? "global" : "applications",
                Applications = Applications.ToList(),
                Match = MatchMode,
                OnLeave = leave
            },
            Action = new BindingAction
            {
                Type = FunctionType == "鍵盤" ? "keyboard" : "macro",
                Keys = keys.ToList(),
                MacroId = macro.Id
            },
            Macro = macro
        };
        _bindingId = binding.Id;
        var macros = Selected.Snapshot.Bindings.Macros;
        macros.RemoveAll(m => m.Id == macro.Id);
        macros.Add(macro);
        Service.UpsertBinding(binding);
        RefreshJobs();
        Page = "auto";
        Status = $"已儲存「{binding.Name}」。在自動化頁面按「開始」。";
    }

    public void BeginRecord(MacroStep step)
    {
        _recordingKeyboard = false;
        RecordingStep = step;
        Status = RecordBanner;
    }

    public void CancelRecord()
    {
        RecordingStep = null;
        _recordingKeyboard = false;
        OnPropertyChanged(nameof(IsRecordingKey));
        Status = "已取消錄製";
    }

    public bool CompleteRecord(string canonical)
    {
        if (_recordingKeyboard)
        {
            KeyboardKeys = canonical;
            _recordingKeyboard = false;
            OnPropertyChanged(nameof(IsRecordingKey));
            OnPropertyChanged(nameof(KeyboardKeysLabel));
            Status = $"已記錄：{VirtualKeyNames.DisplayCombo(canonical)}";
            return true;
        }

        if (RecordingStep is null)
            return false;
        RecordingStep.Value = canonical;
        RecordingStep = null;
        OnPropertyChanged(nameof(MacroPreview));
        Status = $"已記錄：{VirtualKeyNames.DisplayCombo(canonical)}";
        return true;
    }

    public string KeyboardKeysLabel => VirtualKeyNames.DisplayCombo(KeyboardKeys);

    private void AddStep(MacroStep step)
    {
        step.PropertyChanged += (_, __) => OnPropertyChanged(nameof(MacroPreview));
        MacroSteps.Add(step);
        OnPropertyChanged(nameof(MacroPreview));
    }

    private void ResetSteps()
    {
        MacroSteps.Clear();
        OnPropertyChanged(nameof(MacroPreview));
    }

    private void LoadSteps(IEnumerable<MacroAction> actions)
    {
        MacroSteps.Clear();
        foreach (var action in actions)
        {
            MacroStep step;
            switch (action.Type.ToLowerInvariant())
            {
                case "delay":
                    step = new MacroStep { Kind = "等待", Value = action.DelayMs.ToString() };
                    break;
                case "mouse":
                    step = new MacroStep
                    {
                        Kind = "滑鼠點擊",
                        ClickButton = action.MouseButton?.ToLowerInvariant() switch
                        {
                            "right" => "右鍵",
                            "middle" => "中鍵",
                            _ => "左鍵"
                        }
                    };
                    break;
                default:
                    step = new MacroStep
                    {
                        Kind = "按鍵盤",
                        Value = string.Join("+", action.Keys)
                    };
                    break;
            }
            AddStep(step);
        }
    }

    private List<MacroAction> StepsToActions()
    {
        var actions = new List<MacroAction>();
        foreach (var step in MacroSteps)
        {
            if (step.IsWait)
            {
                var raw = step.Value ?? "";
                var ms = IntervalParser.ParseToMilliseconds(
                    raw.Contains("ms", StringComparison.OrdinalIgnoreCase) ||
                    raw.Contains('s', StringComparison.OrdinalIgnoreCase)
                        ? raw
                        : raw + "ms",
                    500);
                actions.Add(new MacroAction { Type = "delay", DelayMs = ms });
                continue;
            }
            if (step.IsClick)
            {
                var button = step.ClickButton switch
                {
                    "右鍵" => "right",
                    "中鍵" => "middle",
                    _ => "left"
                };
                actions.Add(new MacroAction { Type = "mouse", MouseButton = button });
                continue;
            }
            var keys = (step.Value ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (keys.Length == 0)
                continue;
            actions.Add(new MacroAction { Type = "key", Keys = keys.ToList() });
        }
        return actions;
    }

    private void PickApp()
    {
        var picker = new AppPickerWindow { Owner = Application.Current.MainWindow };
        if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedExe))
            AddApp(picker.SelectedExe);
    }

    private void AddApp(string exe)
    {
        ScopeGlobal = false;
        if (!Applications.Any(a => string.Equals(a, exe, StringComparison.OrdinalIgnoreCase)))
            Applications.Add(exe);
        OnPropertyChanged(nameof(AppsLabel));
    }

    private void StartAllBindings()
    {
        if (Selected is null) return;
        var app = ForegroundProcess.GetForegroundProcess();
        foreach (var binding in Selected.Snapshot.Bindings.Bindings.Where(b => b.Enabled))
        {
            if (!ScopeMatcher.Matches(binding.Scope, app) && !binding.Scope.IsGlobal)
                continue;
            var macro = BindingResolver.ToMacro(binding, Selected.Snapshot.Bindings);
            if (macro.Actions.Count == 0) continue;
            Service.Macros.Scheduler.Start(binding, macro, binding.Trigger is "once" ? "interval" : binding.Trigger);
        }
        Page = "auto";
        RefreshJobs();
        Status = "已嘗試開始所有已儲存的巨集";
    }

    private void AddProfileFromForeground()
    {
        var app = ForegroundProcess.GetForegroundProcess();
        if (app is null || Selected is null) return;
        Service.UpsertProfile(new ProfileDefinition
        {
            Name = app.ExeFileName,
            Scope = new ScopeDefinition
            {
                Type = "applications",
                Applications = { app.ExeFileName }
            }
        });
        Status = $"已建立 Profile：{app.ExeFileName}";
    }

    private void StartJob(string id)
    {
        foreach (var mouse in Service.Mice)
        {
            var binding = mouse.Snapshot.Bindings.Bindings.FirstOrDefault(b => b.Id == id);
            if (binding is null)
                continue;
            var macro = BindingResolver.ToMacro(binding, mouse.Snapshot.Bindings);
            if (macro.Actions.Count == 0)
            {
                Status = "這個項目還沒有動作，請回滑鼠頁補上按鍵。";
                return;
            }
            var trigger = binding.Trigger is "once" or "hold" or "單次執行" or "按住循環" ? "interval" : binding.Trigger;
            if (string.IsNullOrWhiteSpace(trigger) || trigger is "toggle" or "切換循環")
                trigger = "interval";
            Service.Macros.Scheduler.Start(binding, macro, trigger);
            Page = "auto";
            Status = $"已開始：{binding.Name}";
            RefreshJobs();
            return;
        }
        Status = "找不到這個自動化項目。";
    }

    private void RefreshJobs()
    {
        Jobs.Clear();
        var live = Service.Macros.Scheduler.Snapshots.ToDictionary(s => s.Id, s => s);
        foreach (var mouse in Service.Mice)
        {
            foreach (var binding in mouse.Snapshot.Bindings.Bindings)
            {
                live.TryGetValue(binding.Id, out var running);
                var macro = BindingResolver.ToMacro(binding, mouse.Snapshot.Bindings);
                var button = mouse.Snapshot.Device.Buttons.FirstOrDefault(b => BindingResolver.ButtonMatches(binding.TargetButton, b));
                var next = running?.NextRunAt is DateTimeOffset t
                    ? Math.Max(0, (t - DateTimeOffset.UtcNow).TotalSeconds)
                    : 0;
                Jobs.Add(new JobRow
                {
                    Id = binding.Id,
                    Name = string.IsNullOrWhiteSpace(binding.Name) ? (button?.DisplayName ?? binding.TargetButton) : binding.Name,
                    ButtonName = button?.DisplayName ?? binding.TargetButton,
                    MouseName = mouse.DisplayName,
                    StateText = running is null ? "未開始" : running.State switch
                    {
                        "paused" => "已暫停",
                        "waiting" => "等待中（回程式繼續）",
                        "running" => "執行中",
                        _ => "未開始"
                    },
                    ScopeLabel = ScopeMatcher.Label(binding.Scope),
                    IntervalText = $"每隔 {IntervalParser.Format(binding.IntervalMs <= 0 ? 1000 : binding.IntervalMs)}",
                    CountText = running is null ? "尚未執行" : $"已執行 {running.ExecutionCount} 次",
                    NextText = running?.State == "running" ? $"下一次 {next:0.0} 秒"
                        : running?.State == "waiting" ? "回到指定程式後會自動繼續"
                        : running?.State == "paused" ? "按「繼續」或「開始」"
                        : "按開始才會執行",
                    IsLive = running is not null && running.State is "running" or "paused" or "waiting",
                    IsPaused = running?.State == "paused",
                    CanPause = running?.State is "running" or "waiting",
                    CanResume = running?.State == "paused",
                    CanStart = macro.Actions.Count > 0
                        && (running is null || running.State is "paused" or "waiting")
                });
            }
        }
        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(HasNoJobs));
        OnPropertyChanged(nameof(ProfileText));
    }

    private void StartScanAll()
    {
        if (Selected is null) return;
        Service.Learning.StartScanAll(Selected.DeviceId);
        Status = "掃描全部滑鼠按鍵：請依序按下每個實體按鍵";
        NotifyLearning();
    }

    private void CreatePending()
    {
        var physical = PendingNew?.Physical ?? Selected?.Physical;
        if (physical is null) return;
        Service.CreateDeviceModel(physical);
        PendingNew = null;
        AskScanAfterCreate = true;
        OnPropertyChanged(nameof(PendingNew));
        OnPropertyChanged(nameof(HasPendingNew));
        OnPropertyChanged(nameof(AskScanAfterCreate));
        Status = "是否開始掃描額外按鍵？";
    }

    private void RenameSelected()
    {
        if (SelectedButton is null) return;
        var name = string.IsNullOrWhiteSpace(RenameText) ? SelectedButton.DisplayName : RenameText;
        Service.RenameButton(SelectedButton.Id, name);
        BindingName = name;
        Status = $"已重新命名為 {name}";
    }

    private void NotifyLearning()
    {
        OnPropertyChanged(nameof(IsLearning));
        OnPropertyChanged(nameof(LearningTitle));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
