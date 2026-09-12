using MacroForge.Core.Model;

namespace MacroForge.Core.Macros;

public static class MacroScriptParser
{
    public static List<MacroAction> Parse(string? script)
    {
        var actions = new List<MacroAction>();
        if (string.IsNullOrWhiteSpace(script))
            return actions;

        foreach (var raw in script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (TryClick(line, out var button))
            {
                actions.Add(new MacroAction { Type = "mouse", MouseButton = button });
                continue;
            }

            if (TryDelay(line, out var delay))
            {
                actions.Add(new MacroAction { Type = "delay", DelayMs = delay });
                continue;
            }

            var keys = line.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            actions.Add(new MacroAction { Type = "key", Keys = keys.ToList() });
        }

        return actions;
    }

    public static string Format(IEnumerable<MacroAction> actions)
    {
        var lines = new List<string>();
        foreach (var action in actions)
        {
            switch (action.Type.ToLowerInvariant())
            {
                case "delay":
                    lines.Add(action.DelayMs >= 1000 && action.DelayMs % 1000 == 0
                        ? $"delay {action.DelayMs / 1000}s"
                        : $"delay {action.DelayMs}ms");
                    break;
                case "mouse":
                    lines.Add($"click {action.MouseButton ?? "left"}");
                    break;
                default:
                    lines.Add(string.Join(" + ", action.Keys));
                    break;
            }
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryDelay(string line, out int ms)
    {
        ms = 0;
        var lower = line.ToLowerInvariant().Trim();
        if (lower.StartsWith("delay") || lower.StartsWith("wait") || lower.StartsWith("間隔"))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var token = parts.Length >= 2 ? string.Join("", parts.Skip(1)) : parts[0];
            ms = Scope.IntervalParser.ParseToMilliseconds(token, 0);
            return ms > 0;
        }

        if ((lower.EndsWith("ms") || lower.EndsWith("毫秒") || lower.EndsWith("s") || lower.EndsWith("秒"))
            && lower.Any(char.IsDigit)
            && !lower.Contains('+'))
        {
            ms = Scope.IntervalParser.ParseToMilliseconds(line, 0);
            return ms > 0;
        }

        return false;
    }

    private static bool TryClick(string line, out string button)
    {
        button = "left";
        var lower = line.ToLowerInvariant().Replace(" ", "");
        if (lower is "click" or "clickleft" or "leftclick" or "mouseleft" or "lclick")
        {
            button = "left";
            return true;
        }
        if (lower is "clickright" or "rightclick" or "mouseright")
        {
            button = "right";
            return true;
        }
        if (lower is "clickmiddle" or "middleclick")
        {
            button = "middle";
            return true;
        }
        if (lower.StartsWith("click"))
        {
            button = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "left";
            return true;
        }
        return false;
    }
}
