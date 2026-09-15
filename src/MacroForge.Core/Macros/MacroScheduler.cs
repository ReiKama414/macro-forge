using System.Collections.Concurrent;
using MacroForge.Core.Logging;
using MacroForge.Core.Model;
using MacroForge.Core.Scope;

namespace MacroForge.Core.Macros;

public sealed class RunningMacro
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string BindingId { get; init; }
    public required ScopeDefinition Scope { get; init; }
    public required MacroDefinition Macro { get; init; }
    public required string Trigger { get; init; }
    public required int IntervalMs { get; init; }
    public required int RepeatCount { get; init; }
    public CancellationTokenSource Cts { get; } = new();
    public string State { get; set; } = "running";
    public int ExecutionCount { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NextRunAt { get; set; }
    public Task? Task { get; set; }
    public int StartupDelayMs { get; init; }
    /// <summary>True after Stop() announced this job, so RunAsync won't double-log.</summary>
    public bool StopAnnounced { get; set; }
}

public sealed class MacroScheduler : IDisposable
{
    private readonly ConcurrentDictionary<string, RunningMacro> _running = new();
    private readonly IInputSender _sender;
    private readonly IForegroundSource _foreground;
    private volatile bool _stopped;

    public event Action? Changed;

    public MacroScheduler(IInputSender? sender = null, IForegroundSource? foreground = null)
    {
        _sender = sender ?? new SendInputSender();
        _foreground = foreground ?? new LiveForegroundSource();
    }

    public IReadOnlyList<RunningMacroSnapshot> Snapshots =>
        _running.Values.Select(ToSnapshot).OrderBy(s => s.Name).ToList();

    public bool IsRunning(string bindingId) =>
        _running.TryGetValue(bindingId, out var job) && job.State is "running" or "paused" or "waiting";

    public bool IsPaused(string bindingId) =>
        _running.TryGetValue(bindingId, out var job) && job.State == "paused";

    public void OnButtonDown(ButtonBinding binding, MacroDefinition macro)
    {
        if (_stopped)
            return;
        var trigger = (binding.Trigger ?? "toggle").ToLowerInvariant();
        if (trigger is "toggle" or "interval" or "timed" or "定時" or "切換" or "切換循環")
        {
            if (IsRunning(binding.Id))
            {
                Stop(binding.Id);
                return;
            }
            Start(binding, macro, trigger is "interval" or "timed" or "定時" ? "interval" : "toggle");
            return;
        }

        if (trigger is "hold" or "按住" or "按住循環")
        {
            if (!IsRunning(binding.Id))
                Start(binding, macro, "hold");
            return;
        }

        if (trigger is "count" or "fixed" or "固定次數")
        {
            Start(binding, macro, "count");
            return;
        }

        Start(binding, macro, "once");
    }

    public void OnButtonUp(string bindingId)
    {
        if (_running.TryGetValue(bindingId, out var job) &&
            job.Trigger is "hold")
            Stop(bindingId);
    }

    public void Start(ButtonBinding binding, MacroDefinition macro, string trigger, int startupDelayMs = 0)
    {
        Stop(binding.Id, announce: false);
        var job = new RunningMacro
        {
            Id = binding.Id,
            StartupDelayMs = Math.Max(0, startupDelayMs),
            State = startupDelayMs > 0 ? "waiting" : "running",
            NextRunAt = startupDelayMs > 0 ? DateTimeOffset.UtcNow.AddMilliseconds(startupDelayMs) : null,
            Name = string.IsNullOrWhiteSpace(binding.Name) ? macro.Name : binding.Name,
            BindingId = binding.Id,
            Scope = binding.Scope,
            Macro = macro,
            Trigger = trigger,
            IntervalMs = Math.Max(1, binding.IntervalMs),
            RepeatCount = Math.Max(1, binding.RepeatCount)
        };
        _running[binding.Id] = job;
        job.Task = Task.Run(() => RunAsync(job));
        Changed?.Invoke();
        var triggerLabel = TriggerLabel(trigger);
        AppLogService.Instance.Action("自動化",
            $"開始執行巨集：{job.Name}（觸發方式：{triggerLabel}）");
    }

    public void Pause(string id)
    {
        if (_running.TryGetValue(id, out var job) && job.State is "running" or "waiting")
        {
            job.State = "paused";
            Changed?.Invoke();
            AppLogService.Instance.Action("自動化",
                $"已暫停巨集：{job.Name}（已執行 {job.ExecutionCount} 次，已運行 {FormatElapsed(job.StartedAt)}）");
        }
    }

    public void Resume(string id)
    {
        if (_running.TryGetValue(id, out var job) && job.State == "paused")
        {
            job.State = ScopeMatcher.Matches(job.Scope, _foreground.Current) ? "running" : "waiting";
            Changed?.Invoke();
            AppLogService.Instance.Action("自動化",
                $"已繼續巨集：{job.Name}（已執行 {job.ExecutionCount} 次，已運行 {FormatElapsed(job.StartedAt)}）");
        }
    }

    public void Stop(string id) => Stop(id, announce: true);

    public void Stop(string id, bool announce)
    {
        if (!_running.TryRemove(id, out var job))
            return;
        job.State = "stopped";
        job.StopAnnounced = true;
        job.Cts.Cancel();
        Changed?.Invoke();
        if (announce)
        {
            AppLogService.Instance.Action("自動化",
                $"已停止巨集：{job.Name}（共執行 {job.ExecutionCount} 次，持續 {FormatElapsed(job.StartedAt)}）");
        }
    }

    public void StopAll()
    {
        foreach (var id in _running.Keys.ToList())
            Stop(id);
    }

    public void EmergencyStop()
    {
        _stopped = true;
        StopAll();
        _stopped = false;
        Changed?.Invoke();
    }

    public void StartAll(IEnumerable<(ButtonBinding Binding, MacroDefinition Macro)> jobs)
    {
        foreach (var job in jobs)
            Start(job.Binding, job.Macro, NormalizeTrigger(job.Binding.Trigger));
    }

    public void Dispose() => EmergencyStop();

    private async Task RunAsync(RunningMacro job)
    {
        var ct = job.Cts.Token;
        try
        {
            if (job.StartupDelayMs > 0)
                await Task.Delay(job.StartupDelayMs, ct);
            job.NextRunAt = null;
            while (!ct.IsCancellationRequested)
            {
                if (!await WaitUntilReady(job, ct))
                    return;

                await ExecuteMacro(job, ct);
                if (ct.IsCancellationRequested || job.State == "stopped")
                    break;

                job.ExecutionCount++;
                job.NextRunAt = DateTimeOffset.UtcNow.AddMilliseconds(job.IntervalMs);
                Changed?.Invoke();
                if (job.Trigger is "once" or "count" || job.ExecutionCount == 1 || job.ExecutionCount % 10 == 0)
                {
                    AppLogService.Instance.Info("執行",
                        $"巨集「{job.Name}」已執行 {job.ExecutionCount} 次（已運行 {FormatElapsed(job.StartedAt)}）");
                }

                if (job.Trigger is "once")
                    break;
                if (job.Trigger is "count" && job.ExecutionCount >= job.RepeatCount)
                    break;

                try
                {
                    await WaitInterval(job, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        finally
        {
            // Only remove ourselves — a newer Start() may already own this id.
            if (_running.TryGetValue(job.Id, out var current) && ReferenceEquals(current, job))
                _running.TryRemove(job.Id, out _);

            if (!job.StopAnnounced)
            {
                job.StopAnnounced = true;
                AppLogService.Instance.Action("自動化",
                    $"巨集完成：{job.Name}（共執行 {job.ExecutionCount} 次，持續 {FormatElapsed(job.StartedAt)}）");
            }

            if (job.State != "stopped")
                job.State = "stopped";
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Manual pause waits for Resume. Out-of-scope with pause policy waits until
    /// the foreground app returns, then auto-continues (state = waiting → running).
    /// </summary>
    private async Task<bool> WaitUntilReady(RunningMacro job, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (job.State == "paused")
            {
                await Task.Delay(50, ct);
                continue;
            }

            var foreground = _foreground.Current;
            if (ForegroundProcess.IsOwnApplication(foreground))
            {
                if (job.State != "waiting") { job.State = "waiting"; Changed?.Invoke(); }
                await Task.Delay(50, ct);
                continue;
            }

            if (ScopeMatcher.Matches(job.Scope, foreground))
            {
                if (job.State != "running")
                {
                    job.State = "running";
                    Changed?.Invoke();
                }
                return true;
            }

            var policy = LeavePolicy(job);
            if (policy is "stop" or "停止")
            {
                Stop(job.Id);
                return false;
            }
            if (policy is "ignore" or "忽略")
            {
                if (job.State != "running")
                {
                    job.State = "running";
                    Changed?.Invoke();
                }
                return true;
            }

            if (job.State != "waiting")
            {
                job.State = "waiting";
                Changed?.Invoke();
            }
            await Task.Delay(50, ct);
        }
        return false;
    }

    private async Task WaitInterval(RunningMacro job, CancellationToken ct)
    {
        var remaining = job.IntervalMs;
        while (remaining > 0 && !ct.IsCancellationRequested)
        {
            if (job.State == "paused")
            {
                await Task.Delay(50, ct);
                continue;
            }

            if (!ScopeMatcher.Matches(job.Scope, _foreground.Current))
            {
                var policy = LeavePolicy(job);
                if (policy is "stop" or "停止")
                {
                    Stop(job.Id);
                    return;
                }
                if (policy is not ("ignore" or "忽略"))
                {
                    if (job.State != "waiting")
                    {
                        job.State = "waiting";
                        Changed?.Invoke();
                    }
                    await Task.Delay(50, ct);
                    continue;
                }
            }
            else if (job.State == "waiting")
            {
                job.State = "running";
                Changed?.Invoke();
            }

            job.NextRunAt = DateTimeOffset.UtcNow.AddMilliseconds(remaining);
            var slice = Math.Min(50, remaining);
            await Task.Delay(slice, ct);
            remaining -= slice;
        }
    }

    private async Task ExecuteMacro(RunningMacro job, CancellationToken ct)
    {
        foreach (var action in job.Macro.Actions)
        {
            ct.ThrowIfCancellationRequested();
            if (!await WaitUntilReady(job, ct))
                return;

            if (action.Type.Equals("delay", StringComparison.OrdinalIgnoreCase))
            {
                if (!await WaitActionDelay(job, Math.Max(0, action.DelayMs), ct))
                    return;
                continue;
            }

            if (!ScopeMatcher.Matches(job.Scope, _foreground.Current))
            {
                var policy = LeavePolicy(job);
                if (policy is "stop" or "停止")
                {
                    Stop(job.Id);
                    return;
                }
                if (policy is "ignore" or "忽略")
                    continue;
                if (!await WaitUntilReady(job, ct))
                    return;
            }

            if (action.Type.Equals("mouse", StringComparison.OrdinalIgnoreCase))
                _sender.SendMouseClick(action.MouseButton ?? "left");
            else if (action.Type.Equals("text", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(action.Text))
                _sender.SendText(action.Text);
            else
                _sender.SendKeys(action.Keys);
        }
    }

    private async Task<bool> WaitActionDelay(
        RunningMacro job,
        int delayMs,
        CancellationToken ct)
    {
        var remaining = delayMs;
        while (remaining > 0 && !ct.IsCancellationRequested)
        {
            if (!await WaitUntilReady(job, ct))
                return false;

            var slice = Math.Min(50, remaining);
            await Task.Delay(slice, ct);
            remaining -= slice;
        }
        return !ct.IsCancellationRequested;
    }

    private static string LeavePolicy(RunningMacro job) =>
        (job.Scope.OnLeave ?? "pause").ToLowerInvariant();

    private RunningMacroSnapshot ToSnapshot(RunningMacro job) => new()
    {
        Id = job.Id,
        Name = job.Name,
        State = job.State,
        ScopeLabel = ScopeMatcher.Label(job.Scope),
        IntervalMs = job.IntervalMs,
        ExecutionCount = job.ExecutionCount,
        NextRunAt = job.NextRunAt,
        InScope = ScopeMatcher.Matches(job.Scope, _foreground.Current)
    };

    private static string TriggerLabel(string trigger) => trigger switch
    {
        "hold" => "按住循環",
        "count" => "固定次數",
        "interval" => "定時執行",
        "once" => "單次執行",
        _ => "切換循環"
    };

    private static string FormatElapsed(DateTimeOffset startedAt)
    {
        var span = DateTimeOffset.UtcNow - startedAt;
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours} 小時 {span.Minutes} 分 {span.Seconds} 秒";
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes} 分 {span.Seconds} 秒";
        return $"{(int)Math.Max(0, span.TotalSeconds)} 秒";
    }

    private static string NormalizeTrigger(string? trigger) => (trigger ?? "toggle").ToLowerInvariant() switch
    {
        "hold" or "按住" or "按住循環" => "hold",
        "count" or "fixed" or "固定次數" => "count",
        "once" or "單次" or "單次執行" => "once",
        "interval" or "timed" or "定時" or "定時執行" => "interval",
        _ => "toggle"
    };
}
