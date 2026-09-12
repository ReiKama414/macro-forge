using MacroForge.Core.Model;
using MacroForge.Core.Scope;

namespace MacroForge.Tests;

public class ScopeAndIntervalTests
{
    [Theory]
    [InlineData("500 ms", 500)]
    [InlineData("1 s", 1000)]
    [InlineData("2.5 s", 2500)]
    [InlineData("10 s", 10000)]
    public void Parses_interval_to_milliseconds(string text, int ms)
    {
        Assert.Equal(ms, IntervalParser.ParseToMilliseconds(text));
    }

    [Fact]
    public void Global_scope_always_matches()
    {
        var scope = new ScopeDefinition { Type = "global" };
        Assert.True(ScopeMatcher.Matches(scope, new ForegroundApp { ProcessName = "Discord.exe" }));
    }

    [Fact]
    public void Application_scope_matches_exe_name()
    {
        var scope = new ScopeDefinition
        {
            Type = "applications",
            Applications = { "game.exe", "game2.exe" }
        };
        Assert.True(ScopeMatcher.Matches(scope, new ForegroundApp
        {
            ProcessName = "game.exe",
            ExecutablePath = @"C:\Games\game.exe"
        }));
        Assert.False(ScopeMatcher.Matches(scope, new ForegroundApp { ProcessName = "Discord.exe" }));
    }

    [Fact]
    public void Path_match_requires_full_path()
    {
        var scope = new ScopeDefinition
        {
            Type = "applications",
            Match = "path",
            Applications = { @"C:\Games\Test\game.exe" }
        };
        Assert.True(ScopeMatcher.Matches(scope, new ForegroundApp
        {
            ProcessName = "game.exe",
            ExecutablePath = @"C:\Games\Test\game.exe"
        }));
        Assert.False(ScopeMatcher.Matches(scope, new ForegroundApp
        {
            ProcessName = "game.exe",
            ExecutablePath = @"C:\Other\game.exe"
        }));
    }
}
