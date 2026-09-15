using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using MacroForge.Core.Macros;
using MacroForge.Core.Model;
using MacroForge.Core.Scope;

namespace MacroForge.App;

public sealed partial class MainViewModel
{
    private bool _sideView;
    public bool IsSideView { get => _sideView; set { _sideView = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFrontView)); } }
    public bool IsFrontView { get => !_sideView; set { if (value) IsSideView = false; } }
    public IEnumerable<MacroDefinition> AvailableMacros => Selected?.Snapshot.Bindings.Macros ?? Enumerable.Empty<MacroDefinition>();
    public IReadOnlyDictionary<string, string> ButtonLabels => Buttons.ToDictionary(b => b.Id, b =>
        Selected?.Snapshot.Bindings.Bindings.LastOrDefault(binding => BindingResolver.ButtonMatches(binding.TargetButton, b)) is { } binding
            ? binding.Enabled ? binding.Name : "巨集已停用" : "預設按鍵");
    private MacroDefinition? _chosenMacro;
    public MacroDefinition? ChosenMacro
    {
        get => _chosenMacro;
        set { _chosenMacro = value; if (value is not null) { BindingName = value.Name; LoadSteps(value.Actions); } OnPropertyChanged(); }
    }
    private bool _bindingEnabled = true;
    public bool BindingEnabled { get => _bindingEnabled; set { _bindingEnabled = value; OnPropertyChanged(); } }
    public bool IsKeyboardFunction => FunctionType == "鍵盤";
    public bool IsMouseFunction => FunctionType == "滑鼠";
    public bool IsCountTrigger => TriggerMode == "固定次數";
    private string _mouseAction = "左鍵";
    public string MouseAction { get => _mouseAction; set { _mouseAction = value; OnPropertyChanged(); } }
    public string IntervalValue
    {
        get => (IntervalParser.ParseToMilliseconds(IntervalText) / (IntervalUnit == "秒 (s)" ? 1000d : 1)).ToString("0.###", CultureInfo.InvariantCulture);
        set { IntervalText = value + (IntervalUnit == "秒 (s)" ? " s" : " ms"); }
    }
    public IReadOnlyList<string> IntervalUnits { get; } = new[] { "秒 (s)", "毫秒 (ms)" };
    public string IntervalUnit
    {
        get => IntervalText.Contains("ms", System.StringComparison.OrdinalIgnoreCase) ? "毫秒 (ms)" : "秒 (s)";
        set
        {
            if (value == IntervalUnit) return;
            var milliseconds = IntervalParser.ParseToMilliseconds(IntervalText);
            IntervalText = value == "秒 (s)" ? (milliseconds / 1000d).ToString(CultureInfo.InvariantCulture) + " s" : milliseconds + " ms";
        }
    }
    public string ActiveProfileName => Selected?.Snapshot.Bindings.Profiles.FirstOrDefault(p => p.Id == Selected.Snapshot.Bindings.ActiveProfileId)?.Name ?? "全域設定檔";
    public string DeviceConnection => Selected?.ConnectionLabel ?? "未連接";
    public string ConnectedSummary => $"{Mice.Count(m => m.IsConnected)} 個裝置已連接";
    public JobRow? SelectedJob => Jobs.FirstOrDefault(j => j.Id == _bindingId);
    public string ExecutionLabel => SelectedJob?.StateText ?? "尚未執行";
    public string ExecutionCount => string.IsNullOrEmpty(SelectedJob?.CountText) ? "已執行 0 次" : SelectedJob.CountText;
    public string ExecutionNext => string.IsNullOrEmpty(SelectedJob?.NextText) ? "等待觸發" : SelectedJob.NextText;
    public ICommand RemoveAppCommand => new RelayCommand(p => { if (p is string app) Applications.Remove(app); OnPropertyChanged(nameof(AppsLabel)); });
    public ICommand NewMacroCommand => new RelayCommand(_ => { ResetSteps(); BindingName = "新的巨集"; ChosenMacro = null; Page = "mouse"; });
    public ICommand TestSelectedCommand => new RelayCommand(_ =>
    {
        if (!SaveBinding()) return;
        var binding = Selected?.Snapshot.Bindings.Bindings.FirstOrDefault(b => b.Id == _bindingId);
        if (binding is not null && binding.Enabled)
            Service.Macros.Scheduler.Start(binding, BindingResolver.ToMacro(binding, Selected!.Snapshot.Bindings), "once", startupDelayMs: 3000);
        RefreshJobs();
    }, _ => HasSelectedButton);
    public ICommand StopSelectedCommand => new RelayCommand(_ => { Service.Macros.Scheduler.Stop(_bindingId); RefreshJobs(); });
    public ICommand PauseSelectedCommand => new RelayCommand(_ => PauseJobCommand.Execute(_bindingId));
    public ICommand ResumeSelectedCommand => new RelayCommand(_ => ResumeJobCommand.Execute(_bindingId));

    private void NotifyPresentation()
    {
        foreach (var name in new[] { nameof(ButtonLabels), nameof(AvailableMacros), nameof(SelectedJob), nameof(ExecutionLabel), nameof(ExecutionCount), nameof(ExecutionNext), nameof(ActiveProfileName), nameof(DeviceConnection), nameof(ConnectedSummary) }) OnPropertyChanged(name);
    }
}
