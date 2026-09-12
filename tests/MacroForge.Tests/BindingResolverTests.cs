using MacroForge.Core.Macros;
using MacroForge.Core.Model;

namespace MacroForge.Tests;

public class BindingResolverTests
{
    [Fact]
    public void Application_binding_wins_over_global_for_same_button()
    {
        var button = new MouseButtonDefinition { Id = "btn_5", RawButtonIndex = 5 };
        var model = new BindingsModel
        {
            Bindings =
            {
                new ButtonBinding
                {
                    Id = "g",
                    Button = "mouse_5",
                    Scope = new ScopeDefinition { Type = "global" },
                    Action = new BindingAction { Type = "keyboard", Key = "Back" }
                },
                new ButtonBinding
                {
                    Id = "game",
                    Button = "mouse_5",
                    Scope = new ScopeDefinition { Type = "applications", Applications = { "game.exe" } },
                    Action = new BindingAction { Type = "macro", MacroId = "skill" }
                },
                new ButtonBinding
                {
                    Id = "chrome",
                    Button = "mouse_5",
                    Scope = new ScopeDefinition { Type = "applications", Applications = { "chrome.exe" } },
                    Action = new BindingAction { Type = "keyboard", Keys = { "Ctrl", "W" } }
                }
            }
        };

        var inGame = BindingResolver.Resolve(model, button, new ForegroundApp { ProcessName = "game.exe" });
        var inChrome = BindingResolver.Resolve(model, button, new ForegroundApp { ProcessName = "chrome.exe" });
        var desktop = BindingResolver.Resolve(model, button, new ForegroundApp { ProcessName = "Explorer.exe" });

        Assert.Equal("game", inGame?.Id);
        Assert.Equal("chrome", inChrome?.Id);
        Assert.Equal("g", desktop?.Id);
    }

    [Fact]
    public void Application_profile_outranks_global_profile()
    {
        var app = new ForegroundApp { ProcessName = "Code.exe" };
        var profile = BindingResolver.ResolveProfile(new[]
        {
            new ProfileDefinition { Id = "work", Name = "工作", Scope = new ScopeDefinition { Type = "applications", Applications = { "Code.exe" } } },
            new ProfileDefinition { Id = "all", Name = "全域", Scope = new ScopeDefinition { Type = "global" } }
        }, app);
        Assert.Equal("work", profile?.Id);
    }
}
