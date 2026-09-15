using System.Text.Json;

namespace MacroForge.Core.Logging;

public enum LogLevel
{
    Info,
    Action,
    Warning,
    Error,
    Version
}

public sealed class AppLogEntry
{
    public string Id { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Category { get; init; } = "系統";
    public string Message { get; init; } = "";

    public string LevelLabel => Level switch
    {
        LogLevel.Action => "操作",
        LogLevel.Warning => "警告",
        LogLevel.Error => "錯誤",
        LogLevel.Version => "版本",
        _ => "資訊"
    };

    public string TimeText => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>Thread-safe in-memory + AppData JSONL log store for multiple event kinds.</summary>
public sealed class AppLogService
{
    public static AppLogService Instance { get; } = new();

    /// <summary>Each log level keeps up to this many entries (auto FIFO).</summary>
    public const int MaxEntriesPerLevel = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private readonly List<AppLogEntry> _entries = new();
    private readonly string _path;
    private int _persistPending;

    public event Action? Changed;

    public AppLogService(string? rootPath = null)
    {
        var root = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MacroForge");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "app-log.jsonl");
        Load();
    }

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public int CountByLevel(LogLevel level)
    {
        lock (_gate)
            return _entries.Count(e => e.Level == level);
    }

    public IReadOnlyList<AppLogEntry> Snapshot()
    {
        lock (_gate)
            return _entries.ToList();
    }

    public void Info(string category, string message) => Write(LogLevel.Info, category, message);
    public void Action(string category, string message) => Write(LogLevel.Action, category, message);
    public void Warning(string category, string message) => Write(LogLevel.Warning, category, message);
    public void Error(string category, string message) => Write(LogLevel.Error, category, message);
    public void Version(string category, string message) => Write(LogLevel.Version, category, message);

    public void Write(LogLevel level, string category, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var entry = new AppLogEntry
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Category = string.IsNullOrWhiteSpace(category) ? "系統" : category.Trim(),
            Message = message.Trim()
        };

        lock (_gate)
        {
            _entries.Insert(0, entry);
            TrimLevelLocked(level);
        }

        SchedulePersist();
        Changed?.Invoke();
    }

    /// <summary>Clear all entries, or only one level when specified.</summary>
    public int Clear(LogLevel? level = null)
    {
        int removed;
        lock (_gate)
        {
            if (level is null)
            {
                removed = _entries.Count;
                _entries.Clear();
            }
            else
            {
                removed = _entries.RemoveAll(e => e.Level == level.Value);
            }
        }

        SchedulePersist();
        Changed?.Invoke();
        return removed;
    }

    public IReadOnlyList<AppLogEntry> Query(
        LogLevel? level = null,
        string? search = null,
        TimeSpan? since = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        IEnumerable<AppLogEntry> q;
        lock (_gate)
            q = _entries.ToList();

        if (level is not null)
            q = q.Where(e => e.Level == level);
        if (since is not null)
        {
            var cutoff = DateTimeOffset.Now - since.Value;
            q = q.Where(e => e.Timestamp >= cutoff);
        }
        if (from is not null)
            q = q.Where(e => e.Timestamp >= from.Value);
        if (to is not null)
            q = q.Where(e => e.Timestamp <= to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(e =>
                e.Message.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.LevelLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return q.ToList();
    }

    public string ExportText(IEnumerable<AppLogEntry> entries) =>
        string.Join(Environment.NewLine, entries.Select(e =>
            $"{e.TimeText}\t{e.LevelLabel}\t{e.Category}\t{e.Message.Replace("\r", " ").Replace("\n", " | ")}"));

    private void TrimLevelLocked(LogLevel level)
    {
        var count = 0;
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Level != level)
                continue;
            count++;
            if (count <= MaxEntriesPerLevel)
                continue;
            _entries.RemoveAt(i);
            i--;
        }
    }

    private void SchedulePersist()
    {
        // Coalesce bursts so macro loops don't thrash disk.
        if (Interlocked.Increment(ref _persistPending) > 1)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    await Task.Delay(250);
                    var pending = Interlocked.Exchange(ref _persistPending, 0);
                    if (pending == 0)
                        break;

                    List<AppLogEntry> snapshot;
                    lock (_gate)
                        snapshot = _entries.ToList();

                    await using var stream = new FileStream(
                        _path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
                    await using var writer = new StreamWriter(stream);
                    // Persist oldest→newest so reload preserves chronological file order.
                    foreach (var item in snapshot.AsEnumerable().Reverse())
                        await writer.WriteLineAsync(JsonSerializer.Serialize(item, JsonOptions));
                }
            }
            catch
            {
                Interlocked.Exchange(ref _persistPending, 0);
            }
        });
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            var loaded = new List<AppLogEntry>();
            foreach (var line in File.ReadLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var entry = JsonSerializer.Deserialize<AppLogEntry>(line, JsonOptions);
                if (entry is not null && !string.IsNullOrWhiteSpace(entry.Message))
                    loaded.Add(entry);
            }
            // Newest first in memory
            loaded.Reverse();
            lock (_gate)
            {
                _entries.Clear();
                _entries.AddRange(loaded);
                foreach (LogLevel level in Enum.GetValues<LogLevel>())
                    TrimLevelLocked(level);
            }
        }
        catch
        {
            // Corrupt log file should not block startup.
        }
    }
}
