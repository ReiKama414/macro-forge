using System.Collections.Concurrent;
using MacroForge.Core.Macros;
using MacroForge.Core.Model;

namespace MacroForge.Tests;

public class MacroSchedulerTests
{
    [Fact]
    public async Task Independent_macros_do_not_block_each_other()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground { Current = new ForegroundApp { ProcessName = "game.exe" } };
        using var scheduler = new MacroScheduler(sender, fg);

        Start(scheduler, "a", "F", 40, "game.exe");
        Start(scheduler, "b", "1", 40, "game.exe");
        Start(scheduler, "c", "2", 40);

        await Task.Delay(220);

        Assert.True(sender.Keys.Count(k => k == "F") >= 2);
        Assert.True(sender.Keys.Count(k => k == "1") >= 2);
        Assert.True(sender.Keys.Count(k => k == "2") >= 2);
        scheduler.EmergencyStop();
    }

    [Fact]
    public async Task App_scoped_macro_stops_sending_after_foreground_changes()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground { Current = new ForegroundApp { ProcessName = "game.exe" } };
        using var scheduler = new MacroScheduler(sender, fg);
        Start(scheduler, "skill", "F", 30, "game.exe");
        await Task.Delay(80);
        var before = sender.Keys.Count(k => k == "F");
        fg.Current = new ForegroundApp { ProcessName = "Discord.exe" };
        await Task.Delay(120);
        var after = sender.Keys.Count(k => k == "F");
        Assert.True(before >= 1);
        Assert.True(after - before <= 1);
        scheduler.EmergencyStop();
    }

    [Fact]
    public async Task Global_macro_continues_when_app_changes()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground { Current = new ForegroundApp { ProcessName = "game.exe" } };
        using var scheduler = new MacroScheduler(sender, fg);
        Start(scheduler, "space", "Space", 30);
        await Task.Delay(70);
        fg.Current = new ForegroundApp { ProcessName = "Discord.exe" };
        await Task.Delay(90);
        Assert.True(sender.Keys.Count(k => k == "Space") >= 3);
        scheduler.EmergencyStop();
    }

    [Fact]
    public void Macro_script_supports_multi_step_loop_body()
    {
        var actions = MacroScriptParser.Parse("1\ndelay 500ms\n2\ndelay 300ms\nclick left\ndelay 100ms\n3\ndelay 2s");
        Assert.Equal(8, actions.Count);
        Assert.Equal("1", actions[0].Keys[0]);
        Assert.Equal(500, actions[1].DelayMs);
        Assert.Equal("mouse", actions[4].Type);
        Assert.Equal(2000, actions[7].DelayMs);
    }

    [Fact]
    public async Task App_scoped_macro_auto_resumes_when_foreground_returns()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground { Current = new ForegroundApp { ProcessName = "game.exe" } };
        using var scheduler = new MacroScheduler(sender, fg);
        Start(scheduler, "skill", "F", 40, "game.exe");
        await Task.Delay(70);
        Assert.True(sender.Keys.Count(k => k == "F") >= 1);

        fg.Current = new ForegroundApp { ProcessName = "Discord.exe" };
        await Task.Delay(100);
        var mid = sender.Keys.Count(k => k == "F");
        Assert.Contains(scheduler.Snapshots, s => s.Id == "skill" && s.State == "waiting");

        fg.Current = new ForegroundApp { ProcessName = "game.exe" };
        await Task.Delay(150);
        Assert.True(sender.Keys.Count(k => k == "F") > mid);
        Assert.Contains(scheduler.Snapshots, s => s.Id == "skill" && s.State == "running");
        scheduler.EmergencyStop();
    }

    [Fact]
    public async Task Manual_pause_stays_paused_until_resume()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground { Current = new ForegroundApp { ProcessName = "game.exe" } };
        using var scheduler = new MacroScheduler(sender, fg);
        Start(scheduler, "space", "Space", 40);
        await Task.Delay(60);
        scheduler.Pause("space");
        var mid = sender.Keys.Count(k => k == "Space");
        await Task.Delay(120);
        Assert.Equal(mid, sender.Keys.Count(k => k == "Space"));
        Assert.Contains(scheduler.Snapshots, s => s.Id == "space" && s.State == "paused");

        scheduler.Resume("space");
        await Task.Delay(120);
        Assert.True(sender.Keys.Count(k => k == "Space") > mid);
        scheduler.EmergencyStop();
    }

    [Fact]
    public async Task Restart_same_id_does_not_orphan_new_job()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground { Current = new ForegroundApp { ProcessName = "game.exe" } };
        using var scheduler = new MacroScheduler(sender, fg);
        Start(scheduler, "skill", "F", 40, "game.exe");
        await Task.Delay(50);
        Start(scheduler, "skill", "F", 40, "game.exe");
        await Task.Delay(120);
        Assert.True(scheduler.IsRunning("skill"));
        Assert.Contains(scheduler.Snapshots, s => s.Id == "skill" && s.State is "running" or "waiting");
        var count = sender.Keys.Count(k => k == "F");
        Assert.True(count >= 1);
        scheduler.EmergencyStop();
        Assert.False(scheduler.IsRunning("skill"));
    }

    [Fact]
    public async Task Stop_interrupts_a_long_action_delay_immediately()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground();
        using var scheduler = new MacroScheduler(sender, fg);
        var binding = new ButtonBinding
        {
            Id = "long-delay",
            Name = "long-delay",
            Scope = new ScopeDefinition { Type = "global" },
            IntervalMs = 10
        };
        var macro = new MacroDefinition
        {
            Id = "long-delay",
            Name = "long-delay",
            Actions =
            {
                new MacroAction { Type = "delay", DelayMs = 5000 },
                new MacroAction { Type = "key", Keys = { "F" } }
            }
        };

        scheduler.Start(binding, macro, "once");
        await Task.Delay(80);
        scheduler.Stop(binding.Id);
        await Task.Delay(120);

        Assert.False(scheduler.IsRunning(binding.Id));
        Assert.Empty(sender.Keys);
    }

    [Fact]
    public async Task Manual_start_buffers_input_and_stop_cancels_pending_start()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground { Current = new ForegroundApp { ProcessName = "game.exe" } };
        using var scheduler = new MacroScheduler(sender, fg);
        var binding = new ButtonBinding { Id = "buffer", Scope = new ScopeDefinition { Type = "global" } };
        var macro = new MacroDefinition { Actions = { new MacroAction { Type = "key", Keys = { "F" } } } };
        scheduler.Start(binding, macro, "once", startupDelayMs: 250);
        await Task.Delay(80);
        Assert.Empty(sender.Keys);
        scheduler.EmergencyStop();
        await Task.Delay(280);
        Assert.Empty(sender.Keys);
        scheduler.Start(binding, macro, "once", startupDelayMs: 100);
        await Task.Delay(250);
        Assert.Single(sender.Keys);
    }

    [Fact]
    public async Task Global_macro_waits_while_MacroForge_is_foreground()
    {
        var sender = new FakeSender();
        var fg = new FakeForeground { Current = new ForegroundApp { ProcessName = "MacroForge.exe" } };
        using var scheduler = new MacroScheduler(sender, fg);
        Start(scheduler, "self-guard", "F", 20);
        await Task.Delay(120);
        Assert.Empty(sender.Keys);
        Assert.Contains(scheduler.Snapshots, s => s.State == "waiting");
        fg.Current = new ForegroundApp { ProcessName = "game.exe" };
        await Task.Delay(150);
        Assert.NotEmpty(sender.Keys);
    }

    private static void Start(MacroScheduler scheduler, string id, string key, int interval, string? app = null)
    {
        var binding = new ButtonBinding
        {
            Id = id,
            Button = id,
            Name = id,
            Trigger = "interval",
            IntervalMs = interval,
            Scope = app is null
                ? new ScopeDefinition { Type = "global" }
                : new ScopeDefinition { Type = "applications", Applications = { app }, OnLeave = "pause" },
            Action = new BindingAction { Type = "keyboard", Key = key },
            Macro = new MacroDefinition
            {
                Id = id,
                Name = id,
                Actions = { new MacroAction { Type = "key", Keys = { key } } }
            }
        };
        scheduler.Start(binding, binding.Macro!, "interval");
    }

    private sealed class FakeSender : IInputSender
    {
        public ConcurrentBag<string> Keys { get; } = new();
        public void SendKeys(IReadOnlyList<string> keys) => Keys.Add(string.Join("+", keys));
        public void SendMouseClick(string button) { }
        public void SendText(string text) { }
    }

    private sealed class FakeForeground : IForegroundSource
    {
        public ForegroundApp? Current { get; set; }
    }
}
