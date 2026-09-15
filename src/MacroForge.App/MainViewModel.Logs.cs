using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MacroForge.Core.Logging;
using Microsoft.Win32;

namespace MacroForge.App;

public sealed partial class MainViewModel
{
    private readonly ObservableCollection<AppLogEntry> _visibleLogs = new();
    private readonly ObservableCollection<LogPageItem> _logPageNumbers = new();

    public ObservableCollection<AppLogEntry> VisibleLogs => _visibleLogs;
    public ObservableCollection<LogPageItem> LogPageNumbers => _logPageNumbers;

    public bool IsLogPage => Page == "log";
    public bool ShowDeviceWorkspace => !IsLogPage;

    private string _logFilter = "all";
    public string LogFilter
    {
        get => _logFilter;
        set
        {
            if (_logFilter == value) return;
            _logFilter = value;
            _logPageIndex = 1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LogClearLabel));
            RefreshLogView(preservePage: true);
        }
    }

    private string _logSearch = "";
    public string LogSearch
    {
        get => _logSearch;
        set
        {
            if (_logSearch == value) return;
            _logSearch = value;
            _logPageIndex = 1;
            OnPropertyChanged();
            RefreshLogView(preservePage: true);
        }
    }

    private string _logLevelOption = "全部等級";
    public string LogLevelOption
    {
        get => _logLevelOption;
        set
        {
            if (_logLevelOption == value) return;
            _logLevelOption = value;
            _logPageIndex = 1;
            OnPropertyChanged();
            RefreshLogView(preservePage: true);
        }
    }

    private string _logTimeOption = "全部時間";
    public string LogTimeOption
    {
        get => _logTimeOption;
        set
        {
            if (_logTimeOption == value) return;
            _logTimeOption = value;
            _logPageIndex = 1;
            OnPropertyChanged();
            RefreshLogView(preservePage: true);
        }
    }

    public IReadOnlyList<string> LogLevelOptions { get; } =
        new[] { "全部等級", "資訊", "操作", "警告", "錯誤", "版本" };

    public IReadOnlyList<string> LogTimeOptions { get; } =
        new[] { "全部時間", "近 1 小時", "近 24 小時", "近 7 天" };

    private int _logPageIndex = 1;
    public int LogPageIndex
    {
        get => _logPageIndex;
        set
        {
            var next = Math.Max(1, value);
            if (_logPageIndex == next) return;
            _logPageIndex = next;
            OnPropertyChanged();
            RefreshLogView(preservePage: true);
        }
    }

    public int LogPageSize { get; } = 20;
    public int LogTotalCount { get; private set; }
    public int LogPageCount { get; private set; } = 1;
    public string LogPageLabel => $"第 {LogPageIndex} / {LogPageCount} 頁";
    public string LogTotalText =>
        $"共 {LogTotalCount} 筆（每種類型最多 {AppLogService.MaxEntriesPerLevel} 筆，超出自動循環）";

    public int LogCountAll => AppLogService.Instance.Count;
    public int LogCountAction => AppLogService.Instance.CountByLevel(LogLevel.Action);
    public int LogCountError => AppLogService.Instance.CountByLevel(LogLevel.Error);
    public int LogCountWarning => AppLogService.Instance.CountByLevel(LogLevel.Warning);
    public int LogCountVersion => AppLogService.Instance.CountByLevel(LogLevel.Version);
    public int LogCountInfo => AppLogService.Instance.CountByLevel(LogLevel.Info);

    public string LogClearLabel => ResolveLogLevelFilter() is { } level
        ? $"清除{LevelName(level)}"
        : "清除全部";

    public string LogCountSummary =>
        $"全部 {LogCountAll}  ·  操作 {LogCountAction}  ·  錯誤 {LogCountError}  ·  警告 {LogCountWarning}  ·  版本 {LogCountVersion}  ·  系統 {LogCountInfo}";

    public ICommand ShowLogCommand { get; private set; } = null!;
    public ICommand SetLogFilterCommand { get; private set; } = null!;
    public ICommand RefreshLogsCommand { get; private set; } = null!;
    public ICommand ExportLogsCommand { get; private set; } = null!;
    public ICommand ClearLogsCommand { get; private set; } = null!;
    public ICommand LogPrevPageCommand { get; private set; } = null!;
    public ICommand LogNextPageCommand { get; private set; } = null!;
    public ICommand LogGoPageCommand { get; private set; } = null!;

    private void InitLoggingCommands()
    {
        ShowLogCommand = new RelayCommand(_ => OpenLogPage());
        SetLogFilterCommand = new RelayCommand(p =>
        {
            if (p is string filter)
                LogFilter = filter;
        });
        RefreshLogsCommand = new RelayCommand(_ => RefreshLogView());
        ExportLogsCommand = new RelayCommand(_ => ExportLogs());
        ClearLogsCommand = new RelayCommand(_ => ClearLogs());
        LogPrevPageCommand = new RelayCommand(_ =>
        {
            if (LogPageIndex > 1)
                LogPageIndex--;
        });
        LogNextPageCommand = new RelayCommand(_ =>
        {
            if (LogPageIndex < LogPageCount)
                LogPageIndex++;
        });
        LogGoPageCommand = new RelayCommand(p =>
        {
            if (p is LogPageItem item)
                LogPageIndex = item.Number;
            else if (p is int page)
                LogPageIndex = page;
            else if (p is string text && int.TryParse(text, out var n))
                LogPageIndex = n;
        });
    }

    public void OpenLogPage()
    {
        Page = "log";
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            RefreshLogView();
            return;
        }
        dispatcher.BeginInvoke(() => RefreshLogView(), DispatcherPriority.Loaded);
    }

    private void AttachLogService()
    {
        AppLogService.Instance.Changed += () => Post(() =>
        {
            NotifyLogCounts();
            if (IsLogPage)
                RefreshLogView(preservePage: true);
        });
    }

    private bool _logViewBusy;

    public void RefreshLogView(bool preservePage = false)
    {
        if (_logViewBusy)
            return;
        _logViewBusy = true;
        try
        {
            var level = ResolveLogLevelFilter();
            TimeSpan? since = _logTimeOption switch
            {
                "近 1 小時" => TimeSpan.FromHours(1),
                "近 24 小時" => TimeSpan.FromHours(24),
                "近 7 天" => TimeSpan.FromDays(7),
                _ => null
            };

            var filtered = AppLogService.Instance.Query(level, _logSearch, since);
            LogTotalCount = filtered.Count;
            LogPageCount = Math.Max(1, (int)Math.Ceiling(LogTotalCount / (double)LogPageSize));
            if (!preservePage)
                _logPageIndex = 1;
            else if (_logPageIndex > LogPageCount)
                _logPageIndex = LogPageCount;

            var pageItems = filtered
                .Skip((_logPageIndex - 1) * LogPageSize)
                .Take(LogPageSize)
                .ToList();

            _visibleLogs.Clear();
            foreach (var item in pageItems)
                _visibleLogs.Add(item);

            OnPropertyChanged(nameof(LogPageIndex));
            OnPropertyChanged(nameof(LogPageCount));
            OnPropertyChanged(nameof(LogPageLabel));
            OnPropertyChanged(nameof(LogTotalCount));
            OnPropertyChanged(nameof(LogTotalText));
            OnPropertyChanged(nameof(LogClearLabel));
            NotifyLogCounts();
        }
        catch
        {
            // Keep the log page visible even if a refresh fails.
        }
        finally
        {
            _logViewBusy = false;
        }
    }

    private LogLevel? ResolveLogLevelFilter()
    {
        if (_logFilter is not "all")
        {
            return _logFilter switch
            {
                "action" => LogLevel.Action,
                "error" => LogLevel.Error,
                "warning" => LogLevel.Warning,
                "version" => LogLevel.Version,
                "system" => LogLevel.Info,
                _ => null
            };
        }

        return _logLevelOption switch
        {
            "資訊" => LogLevel.Info,
            "操作" => LogLevel.Action,
            "警告" => LogLevel.Warning,
            "錯誤" => LogLevel.Error,
            "版本" => LogLevel.Version,
            _ => null
        };
    }

    private void ClearLogs()
    {
        if (Application.Current?.MainWindow is not Window owner)
            return;

        var level = ResolveLogLevelFilter();
        var scope = level is null ? "全部日誌" : $"{LevelName(level.Value)}日誌";
        var answer = CyberDialog.Confirm(
            owner,
            "清除日誌",
            $"確定要清除{scope}嗎？此動作無法復原。\n\n（未手動清除時，各類型超過 {AppLogService.MaxEntriesPerLevel} 筆會自動循環覆蓋最舊紀錄。）",
            yesLabel: "清除",
            noLabel: "取消",
            height: 300);

        if (answer != true)
            return;

        var removed = AppLogService.Instance.Clear(level);
        Status = $"已清除 {removed} 筆日誌";
        _logPageIndex = 1;
        RefreshLogView();
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Action => "操作",
        LogLevel.Warning => "警告",
        LogLevel.Error => "錯誤",
        LogLevel.Version => "版本",
        _ => "系統"
    };

    private void ExportLogs()
    {
        var level = ResolveLogLevelFilter();
        TimeSpan? since = _logTimeOption switch
        {
            "近 1 小時" => TimeSpan.FromHours(1),
            "近 24 小時" => TimeSpan.FromHours(24),
            "近 7 天" => TimeSpan.FromDays(7),
            _ => null
        };
        var filtered = AppLogService.Instance.Query(level, _logSearch, since);
        var picker = new SaveFileDialog
        {
            FileName = $"MacroForge-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            Filter = "文字檔 (*.txt)|*.txt|所有檔案 (*.*)|*.*"
        };
        if (picker.ShowDialog() != true)
            return;
        try
        {
            File.WriteAllText(picker.FileName, AppLogService.Instance.ExportText(filtered));
            Status = $"已匯出 {filtered.Count} 筆日誌";
            AppLogService.Instance.Action("系統", $"已匯出日誌：{Path.GetFileName(picker.FileName)}（{filtered.Count} 筆）");
        }
        catch (Exception error)
        {
            AppLogService.Instance.Error("系統", $"匯出日誌失敗：{error.Message}");
            Status = "匯出日誌失敗";
        }
    }

    private void NotifyLogCounts()
    {
        foreach (var name in new[]
                 {
                     nameof(LogCountAll), nameof(LogCountAction), nameof(LogCountError),
                     nameof(LogCountWarning), nameof(LogCountVersion), nameof(LogCountInfo),
                     nameof(LogClearLabel), nameof(LogCountSummary)
                 })
            OnPropertyChanged(name);
    }

    private void NotifyPageFlags()
    {
        OnPropertyChanged(nameof(IsMousePage));
        OnPropertyChanged(nameof(IsAutoPage));
        OnPropertyChanged(nameof(IsLogPage));
        OnPropertyChanged(nameof(ShowDeviceWorkspace));
    }
}
