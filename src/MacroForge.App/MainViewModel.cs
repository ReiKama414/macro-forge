using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MacroForge.Core;
using MacroForge.Core.Devices;
using MacroForge.Core.Learning;
using MacroForge.Core.Logging;
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

/// <summary>
/// Mutable so the automation list can be updated in place. Rebuilding the whole
/// collection on a timer destroys the buttons mid-click and swallows the click.
/// </summary>
public sealed class JobRow : INotifyPropertyChanged
{
    public string Id { get; init; } = "";

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _buttonName = "";
    public string ButtonName { get => _buttonName; set => Set(ref _buttonName, value); }

    private string _mouseName = "";
    public string MouseName { get => _mouseName; set => Set(ref _mouseName, value); }

    private string _stateText = "";
    public string StateText { get => _stateText; set => Set(ref _stateText, value); }

    private string _scopeLabel = "";
    public string ScopeLabel { get => _scopeLabel; set => Set(ref _scopeLabel, value); }

    private string _intervalText = "";
    public string IntervalText { get => _intervalText; set => Set(ref _intervalText, value); }

    private string _countText = "";
    public string CountText { get => _countText; set => Set(ref _countText, value); }

    private string _nextText = "";
    public string NextText { get => _nextText; set => Set(ref _nextText, value); }

    private bool _isLive;
    public bool IsLive
    {
        get => _isLive;
        set { if (Set(ref _isLive, value)) OnPropertyChanged(nameof(IsIdle)); }
    }
    public bool IsIdle => !IsLive;

    private bool _canPause;
    public bool CanPause { get => _canPause; set => Set(ref _canPause, value); }

    private bool _canResume;
    public bool CanResume { get => _canResume; set => Set(ref _canResume, value); }

    private bool _canStart;
    public bool CanStart { get => _canStart; set => Set(ref _canStart, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
            OnPropertyChanged(nameof(KeyCap));
        }
    }

    public string KeyLabel => VirtualKeyNames.DisplayCombo(Value);
    public string KeyCap => VirtualKeyNames.CapCombo(Value);

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

public sealed partial class MainViewModel : INotifyPropertyChanged
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
    public IReadOnlyList<string> AccentOptions { get; } = new[] { "綠", "青", "藍", "紫", "橘" };

    private readonly DispatcherTimer _jobTimer;
    private volatile bool _jobsDirty = true;
    private volatile bool _devicesDirty;
    private bool _shuttingDown;
    private readonly bool _persistTheme;

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
            SelectedButton = Buttons.FirstOrDefault(b => b.Id == _selectedButton?.Id)
                ?? Buttons.FirstOrDefault(b => b.RawButtonIndex == 4) ?? Buttons.FirstOrDefault();
            NotifyPresentation();
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
            NotifyPresentation();
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
            NotifyPageFlags();
        }
    }
    public bool IsMousePage => Page == "mouse";
    public bool IsAutoPage => Page == "auto";
    public bool HasJobs => Jobs.Count > 0;
    public bool HasNoJobs => Jobs.Count == 0;
    public bool HasSelectedButton => SelectedButton is not null;

    private string _status = "正在掃描滑鼠…";
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

    private bool _armed;
    /// <summary>
    /// Master switch, always off at startup so nothing runs until the user says so.
    /// </summary>
    public bool Armed
    {
        get => _armed;
        set
        {
            if (_armed == value) return;
            _armed = value;
            Service.SetTriggersEnabled(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ArmLabel));
            OnPropertyChanged(nameof(ArmHint));
            _jobsDirty = true;
            AppLogService.Instance.Action("系統",
                value ? "已開啟巨集總開關：滑鼠按鍵可觸發巨集" : "已關閉巨集總開關：滑鼠按鍵不會觸發巨集");
        }
    }
    public string ArmLabel => Armed ? "已啟用" : "未啟用";
    public string ArmHint => Armed
        ? "滑鼠按鍵會觸發巨集"
        : "滑鼠按鍵不會觸發巨集";

    private AppSettings _settings = new();
    private string _selectedAccent = "綠";
    public string SelectedAccent
    {
        get => _selectedAccent;
        set
        {
            if (_selectedAccent == value) return;
            _selectedAccent = value;
            ApplyAccent(value);
            OnPropertyChanged();
        }
    }

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray == value) return;
            _settings.MinimizeToTray = value;
            _settings.MinimizeToTrayPromptSeen = true;
            if (_persistTheme)
                _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool MinimizeToTrayPromptSeen => _settings.MinimizeToTrayPromptSeen;

    public void RememberMinimizeToTrayChoice(bool minimizeToTray, bool dontAskAgain)
    {
        _settings.MinimizeToTray = minimizeToTray;
        if (dontAskAgain)
            _settings.MinimizeToTrayPromptSeen = true;
        if (_persistTheme)
            _settings.Save();
        OnPropertyChanged(nameof(MinimizeToTray));
        OnPropertyChanged(nameof(MinimizeToTrayPromptSeen));
    }

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
    public string FunctionType { get => _functionType; set { _functionType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsMacroFunction)); OnPropertyChanged(nameof(IsKeyboardFunction)); OnPropertyChanged(nameof(IsMouseFunction)); } }
    public bool IsMacroFunction => FunctionType == "巨集";

    private string _triggerMode = "切換循環";
    public string TriggerMode { get => _triggerMode; set { _triggerMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCountTrigger)); } }

    private string _intervalText = "1 s";
    public string IntervalText { get => _intervalText; set { _intervalText = value; OnPropertyChanged(); OnPropertyChanged(nameof(IntervalValue)); OnPropertyChanged(nameof(IntervalUnit)); } }

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
                return "尚無動作，請用下方按鈕新增。";
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
    public string RecordBanner => "請按下實際那一顆鍵（主鍵盤與數字鍵盤會分開記錄）· Esc 取消";

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
    public ICommand DeleteJobCommand { get; }
    public ICommand EditJobCommand { get; }
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

    public MainViewModel(UniversalMouseService service, bool persistTheme = true)
    {
        Service = service;
        _persistTheme = persistTheme;
        LoadAccent();
        InitLoggingCommands();
        AttachLogService();
        _jobTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _jobTimer.Tick += (_, _) => OnTick();

        // Never use blocking Dispatcher.Invoke here: these fire from macro worker
        // threads and would starve the UI thread (frozen window, lost clicks).
        service.DevicesChanged += () => Post(() => _devicesDirty = true);
        service.MacrosChanged += () => _jobsDirty = true;
        service.ConnectionChanged += ev => Post(() =>
        {
            Toast = ev.Message;
            Status = ev.Message;
            _devicesDirty = true;
            AppLogService.Instance.Info("裝置", ev.Message);
        });
        service.NewDeviceFound += prompt => Post(() =>
        {
            PendingNew = prompt;
            OnPropertyChanged(nameof(PendingNew));
            OnPropertyChanged(nameof(HasPendingNew));
            Toast = $"發現新的滑鼠：{prompt.Physical.DisplayName}";
            AppLogService.Instance.Info("裝置",
                $"發現新的滑鼠：{prompt.Physical.DisplayName}（VID:{prompt.Physical.Fingerprint.Vid:X4} PID:{prompt.Physical.Fingerprint.Pid:X4}）");
        });
        service.LearningUpdated += result => Post(() =>
        {
            Status = result.Message;
            OnPropertyChanged(nameof(IsLearning));
            OnPropertyChanged(nameof(LearningTitle));
            _devicesDirty = true;
            AppLogService.Instance.Action("按鍵", result.Message);
        });
        service.Status += text => Post(() => Status = text);

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
        RedetectCommand = new RelayCommand(_ =>
        {
            service.Refresh(announceHotPlug: true);
            Status = $"已重新掃描滑鼠 · {Mice.Count(m => m.IsConnected)} 個已連接";
        });
        DismissToastCommand = new RelayCommand(_ => Toast = "");
        SaveBindingCommand = new RelayCommand(_ => SaveBinding());
        PickAppCommand = new RelayCommand(_ => PickApp());
        UseForegroundAppCommand = new RelayCommand(async _ =>
        {
            var app = await CaptureTargetAsync();
            if (app is null) return;
            Status = $"已選擇：{app.ExeFileName}";
            AddApp(MatchPath && !string.IsNullOrWhiteSpace(app.ExecutablePath)
                ? app.ExecutablePath
                : app.ExeFileName);
        });
        ClearAppsCommand = new RelayCommand(_ =>
        {
            Applications.Clear();
            OnPropertyChanged(nameof(AppsLabel));
        });
        EmergencyStopCommand = new RelayCommand(_ =>
        {
            _captureCancellation?.Cancel();
            Service.Macros.Scheduler.EmergencyStop();
            Status = "緊急停止：全部巨集已取消";
            AppLogService.Instance.Warning("自動化", "緊急停止：全部巨集已取消 (F12)");
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
        DeleteJobCommand = new RelayCommand(p =>
        {
            if (p is not string id) return;
            Service.RemoveBinding(id);
            Status = "已刪除自動化";
            _jobsDirty = true;
        });
        EditJobCommand = new RelayCommand(p =>
        {
            if (p is string id)
                EditJob(id);
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
        Status = $"已掃描 {Mice.Count(m => m.IsConnected)} 隻滑鼠 · 目前未啟用 · F12 全部停止";
        AppLogService.Instance.Info("系統", "應用程式啟動完成 (MacroForge v1.0.0)");
        AppLogService.Instance.Version("更新", "目前版本：v1.0.0");
        foreach (var mouse in Mice.Where(m => m.IsConnected))
        {
            AppLogService.Instance.Info("裝置",
                $"偵測到滑鼠：{mouse.DisplayName} (VID:{mouse.Snapshot.Device.Vid} PID:{mouse.Snapshot.Device.Pid})");
            AppLogService.Instance.Info("裝置",
                $"已載入滑鼠設定檔：{mouse.DisplayName}（{mouse.ButtonCount} 個按鍵）");
        }
    }

    public void Shutdown()
    {
        _captureCancellation?.Cancel();
        _shuttingDown = true;
        _jobTimer.Stop();
        Service.StopRuntime();
        AppLogService.Instance.Info("系統", "應用程式關閉");
    }

    private void Post(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;
        dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }

    private void OnTick()
    {
        if (_shuttingDown)
            return;
        if (_devicesDirty)
        {
            _devicesDirty = false;
            _jobsDirty = false;
            Reload();
            return;
        }
        var hasLive = Jobs.Any(j => j.IsLive);
        if (!_jobsDirty && !hasLive)
            return;
        _jobsDirty = false;
        RefreshJobs();
    }

    private void LoadAccent()
    {
        _settings = AppSettings.Load();
        _selectedAccent = string.IsNullOrWhiteSpace(_settings.Accent) ? "綠" : _settings.Accent.Trim();
        ApplyAccent(_selectedAccent, save: false);
        OnPropertyChanged(nameof(MinimizeToTray));
        OnPropertyChanged(nameof(MinimizeToTrayPromptSeen));
    }

    private void ApplyAccent(string name, bool save = true)
    {
        if (Application.Current?.Resources is { } resources)
            ThemePalette.Apply(resources, name);

        if (!save || !_persistTheme) return;
        _settings.Accent = name;
        _settings.Save();
    }

    public void Reload()
    {
        var selectedId = Selected?.DeviceId ?? Service.Selected?.DeviceId;
        var buttonId = SelectedButton?.Id;
        Mice.Clear();
        foreach (var mouse in Service.Mice)
            Mice.Add(mouse);
        Selected = Mice.FirstOrDefault(m => m.DeviceId == selectedId) ?? Service.Selected ?? Mice.FirstOrDefault();
        // Selected may be the same instance, which short-circuits its setter, so
        // refresh the button list explicitly (learning adds buttons in place).
        RefreshButtons();
        SelectedButton = Buttons.FirstOrDefault(b => b.Id == buttonId) ?? Buttons.FirstOrDefault(b => b.RawButtonIndex == 4) ?? Buttons.FirstOrDefault();
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
        BindingEnabled = true;
        BindingName = SelectedButton.DisplayName;
        var binding = Selected.Snapshot.Bindings.Bindings
            .LastOrDefault(b => BindingResolver.ButtonMatches(b.TargetButton, SelectedButton));
        if (binding is null)
        {
            _bindingId = "";
            _chosenMacro = null;
            OnPropertyChanged(nameof(ChosenMacro));
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
        BindingEnabled = binding.Enabled;
        MouseAction = binding.Action.MouseButton switch { "right" => "右鍵", "middle" => "中鍵", _ => "左鍵" };
        BindingName = string.IsNullOrWhiteSpace(binding.Name) ? SelectedButton.DisplayName : binding.Name;
        FunctionType = binding.Action.Type switch { "keyboard" => "鍵盤", "mouse" => "滑鼠", _ => "巨集" };
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
        _chosenMacro = Selected.Snapshot.Bindings.Macros.FirstOrDefault(m => m.Id == binding.Action.MacroId);
        OnPropertyChanged(nameof(ChosenMacro));
        LoadSteps(macro.Actions);
        OnPropertyChanged(nameof(AppsLabel));
    }

    private bool SaveBinding()
    {
        if (Selected is null || SelectedButton is null) return false;
        if (FunctionType == "預設")
        {
            foreach (var existing in Selected.Snapshot.Bindings.Bindings.Where(b => BindingResolver.ButtonMatches(b.TargetButton, SelectedButton)).ToList())
                Service.RemoveBinding(existing.Id);
            _bindingId = "";
            RefreshJobs();
            Status = "已恢復原本按鍵行為";
            AppLogService.Instance.Action("按鍵", $"已恢復預設按鍵：{SelectedButton.DisplayName}");
            return false;
        }
        if (FunctionType == "停用")
        {
            foreach (var existing in Selected.Snapshot.Bindings.Bindings.Where(b => BindingResolver.ButtonMatches(b.TargetButton, SelectedButton)).ToList())
            {
                existing.Enabled = false;
                Service.Macros.Scheduler.Stop(existing.Id);
                Service.UpsertBinding(existing);
            }
            BindingEnabled = false;
            RefreshJobs();
            Status = "已停用此按鍵的巨集；原生滑鼠輸入仍然有效";
            AppLogService.Instance.Action("按鍵", $"已停用巨集：{SelectedButton.DisplayName}");
            return false;
        }
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
            : FunctionType == "滑鼠" ? new List<MacroAction> { new() { Type = "mouse", MouseButton = MouseAction switch { "右鍵" => "right", "中鍵" => "middle", _ => "left" } } } : StepsToActions();
        if (actions.Count == 0 || (FunctionType == "鍵盤" && keys.Length == 0))
        {
            Status = "尚無動作，請先新增一個動作再儲存。";
            return false;
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
                Type = FunctionType == "鍵盤" ? "keyboard" : FunctionType == "滑鼠" ? "mouse" : "macro",
                MouseButton = FunctionType == "滑鼠" ? actions[0].MouseButton : null,
                Keys = keys.ToList(),
                MacroId = macro.Id
            },
            Macro = macro,
            Enabled = BindingEnabled
        };
        _bindingId = binding.Id;
        var macros = Selected.Snapshot.Bindings.Macros;
        macros.RemoveAll(m => m.Id == macro.Id);
        macros.Add(macro);
        _chosenMacro = macro;
        OnPropertyChanged(nameof(ChosenMacro));
        Service.UpsertBinding(binding);
        RefreshJobs();
        Status = $"已儲存「{binding.Name}」· 設定已套用";
        AppLogService.Instance.Action("按鍵",
            $"{(binding.Enabled ? "啟用" : "停用")}按鍵映射：{SelectedButton.DisplayName} -> {binding.Name}");
        return true;
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

    private CancellationTokenSource? _captureCancellation;

    private async Task<ForegroundApp?> CaptureTargetAsync()
    {
        _captureCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _captureCancellation = cancellation;
        try
        {
            return await ForegroundProcess.CaptureAfterSwitchAsync(message => Status = message, cancellation.Token);
        }
        catch (OperationCanceledException) { return null; }
        finally { if (ReferenceEquals(_captureCancellation, cancellation)) _captureCancellation = null; }
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
        var value = MatchPath ? exe : Path.GetFileName(exe);
        if (!Applications.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase)))
            Applications.Add(value);
        OnPropertyChanged(nameof(AppsLabel));
    }

    private void StartAllBindings()
    {
        if (Selected is null) return;
        foreach (var binding in Selected.Snapshot.Bindings.Bindings.Where(b => b.Enabled))
        {
            var macro = BindingResolver.ToMacro(binding, Selected.Snapshot.Bindings);
            if (macro.Actions.Count == 0) continue;
            Service.Macros.Scheduler.Start(binding, macro, ManualTrigger(binding.Trigger), startupDelayMs: 3000);
        }
        Page = "auto";
        RefreshJobs();
        Status = "3 秒後開始，請切換到目標程式；F12 可取消。";
    }

    /// <summary>
    /// Pressing Start in the dashboard has no key to hold, so hold/toggle become
    /// a repeating run while once/count keep their own semantics.
    /// </summary>
    private static string ManualTrigger(string? trigger) => (trigger ?? "").ToLowerInvariant() switch
    {
        "once" or "單次執行" => "once",
        "count" or "固定次數" => "count",
        _ => "interval"
    };

    private async void AddProfileFromForeground()
    {
        var app = await CaptureTargetAsync();
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
            Service.Macros.Scheduler.Start(binding, macro, ManualTrigger(binding.Trigger), startupDelayMs: 3000);
            Page = "auto";
            Status = $"{binding.Name}：3 秒後開始，請切換到目標程式；F12 可取消。";
            RefreshJobs();
            return;
        }
        Status = "找不到這個自動化項目。";
    }

    private void EditJob(string id)
    {
        foreach (var mouse in Service.Mice)
        {
            var binding = mouse.Snapshot.Bindings.Bindings.FirstOrDefault(b => b.Id == id);
            if (binding is null)
                continue;
            Selected = Mice.FirstOrDefault(m => m.DeviceId == mouse.DeviceId) ?? mouse;
            SelectedButton = Buttons.FirstOrDefault(b => BindingResolver.ButtonMatches(binding.TargetButton, b))
                ?? Buttons.FirstOrDefault();
            Page = "mouse";
            Status = $"正在編輯「{binding.Name}」";
            return;
        }
        Status = "找不到這個自動化項目。";
    }

    private void RefreshJobs()
    {
        var live = Service.Macros.Scheduler.Snapshots.ToDictionary(s => s.Id, s => s);
        var order = new List<string>();

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

                var row = Jobs.FirstOrDefault(j => j.Id == binding.Id);
                if (row is null)
                {
                    row = new JobRow { Id = binding.Id };
                    Jobs.Add(row);
                }

                row.Name = string.IsNullOrWhiteSpace(binding.Name) ? (button?.DisplayName ?? binding.TargetButton) : binding.Name;
                row.ButtonName = button?.DisplayName ?? binding.TargetButton;
                row.MouseName = mouse.DisplayName;
                row.StateText = running is null ? "未開始" : running.State switch
                {
                    "paused" => "已暫停",
                    "waiting" => "等待中",
                    "running" => "執行中",
                    _ => "未開始"
                };
                row.ScopeLabel = ScopeMatcher.Label(binding.Scope);
                row.IntervalText = $"每 {IntervalParser.Format(binding.IntervalMs <= 0 ? 1000 : binding.IntervalMs)}";
                row.CountText = running is null ? "" : $"已執行 {running.ExecutionCount} 次";
                row.NextText = running?.State switch
                {
                    "running" => $"下一次 {next:0.0} 秒",
                    "waiting" => "回到指定程式後自動繼續",
                    "paused" => "已手動暫停",
                    _ => ""
                };
                row.IsLive = running is not null && running.State is "running" or "paused" or "waiting";
                row.CanPause = running?.State is "running" or "waiting";
                row.CanResume = running?.State == "paused";
                row.CanStart = macro.Actions.Count > 0 && running is null;
                order.Add(binding.Id);
            }
        }

        for (var i = Jobs.Count - 1; i >= 0; i--)
        {
            if (!order.Contains(Jobs[i].Id))
                Jobs.RemoveAt(i);
        }

        NotifyPresentation();
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
