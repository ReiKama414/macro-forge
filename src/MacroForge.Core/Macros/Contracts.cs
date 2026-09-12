using MacroForge.Core.Model;

namespace MacroForge.Core.Macros;

public interface IInputSender
{
    void SendKeys(IReadOnlyList<string> keys);
    void SendMouseClick(string button);
    void SendText(string text);
}

public interface IForegroundSource
{
    ForegroundApp? Current { get; }
}
