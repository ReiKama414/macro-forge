using MacroForge.Core.Model;

namespace MacroForge.Core.Layout;

public static class LayoutFactory
{
    public static LayoutModel CreateDefault(IReadOnlyList<MouseButtonDefinition> buttons)
    {
        var layout = new LayoutModel();
        var extras = buttons.Where(b => !IsPrimary(b)).ToList();
        var extraIndex = 0;

        foreach (var button in buttons)
        {
            var extraSlot = IsPrimary(button) ? 0 : extraIndex;
            var (x, y) = DefaultPosition(button, extraSlot, extras.Count);
            if (!IsPrimary(button))
                extraIndex++;
            layout.Buttons.Add(new LayoutNode
            {
                ButtonId = button.Id,
                X = x,
                Y = y
            });
        }

        return layout;
    }

    public static void EnsureNodes(LayoutModel layout, IReadOnlyList<MouseButtonDefinition> buttons)
    {
        foreach (var button in buttons)
        {
            if (layout.Buttons.Any(n => n.ButtonId == button.Id))
                continue;
            var extras = buttons.Count(b => !IsPrimary(b));
            var extraIndex = layout.Buttons.Count(n =>
            {
                var match = buttons.FirstOrDefault(b => b.Id == n.ButtonId);
                return match is not null && !IsPrimary(match);
            });
            var (x, y) = DefaultPosition(button, extraIndex, Math.Max(extras, extraIndex + 1));
            layout.Buttons.Add(new LayoutNode { ButtonId = button.Id, X = x, Y = y });
        }

        layout.Buttons.RemoveAll(n => buttons.All(b => b.Id != n.ButtonId));
    }

    public static (double X, double Y) DefaultPosition(MouseButtonDefinition button, int extraIndex, int extraCount)
    {
        if (button.RawButtonIndex == 1) return (0.22, 0.38);
        if (button.RawButtonIndex == 2) return (0.78, 0.38);
        if (button.RawButtonIndex == 3) return (0.50, 0.24);
        if (button.RawButtonIndex == 4) return (0.08, 0.52);
        if (button.RawButtonIndex == 5) return (0.08, 0.36);

        if (extraCount <= 0)
            extraCount = 1;
        var t = extraCount == 1 ? 0.5 : extraIndex / (double)(extraCount - 1);
        var angle = Math.PI * 0.15 + t * Math.PI * 0.70;
        var x = 0.50 + Math.Cos(angle) * 0.46;
        var y = 0.55 + Math.Sin(angle) * 0.34;
        return (Clamp(x), Clamp(y));
    }

    public static bool IsPrimary(MouseButtonDefinition button) =>
        button.RawButtonIndex is 1 or 2 or 3;

    private static double Clamp(double v) => Math.Clamp(v, 0.04, 0.96);
}
