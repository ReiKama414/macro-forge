using MacroForge.Core.Model;

namespace MacroForge.Core.Macros;

public sealed class MacroEngine
{
    public MacroScheduler Scheduler { get; }
    public bool Enabled { get; set; } = true;

    public MacroEngine(IInputSender? sender = null, IForegroundSource? foreground = null)
    {
        Scheduler = new MacroScheduler(sender, foreground);
    }

    public void HandleButton(
        MouseButtonDefinition button,
        bool isDown,
        BindingsModel bindings,
        ForegroundApp? app)
    {
        if (!Enabled)
            return;

        var resolved = BindingResolver.Resolve(bindings, button, app);
        if (resolved is null)
            return;

        if (!isDown)
        {
            Scheduler.OnButtonUp(resolved.Id);
            return;
        }

        var macro = BindingResolver.ToMacro(resolved, bindings);
        if (macro.Actions.Count == 0)
            return;
        Scheduler.OnButtonDown(resolved, macro);
    }
}
