using MacroForge.Core.Macros;
using MacroForge.Core.Model;

namespace MacroForge.Core.Scope;

public sealed class LiveForegroundSource : IForegroundSource
{
    public ForegroundApp? Current => ForegroundProcess.GetForegroundProcess();
}
