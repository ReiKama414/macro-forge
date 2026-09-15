using System.Windows;

namespace MacroForge.App;

public sealed class LogPageItem
{
    public int Number { get; init; }
    public bool IsCurrent { get; init; }
    public FontWeight Weight => IsCurrent ? FontWeights.SemiBold : FontWeights.Normal;
}
