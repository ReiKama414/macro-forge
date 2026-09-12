using System.Globalization;
using System.Text.RegularExpressions;

namespace MacroForge.Core.Scope;

public static class IntervalParser
{
    public static int ParseToMilliseconds(string? text, int fallbackMs = 1000)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallbackMs;
        var raw = text.Trim().ToLowerInvariant().Replace(" ", "");
        var match = Regex.Match(raw, @"^(?<n>\d+(\.\d+)?)(?<u>ms|s|sec|secs|秒|毫秒)?$");
        if (!match.Success)
            return fallbackMs;
        var value = double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups["u"].Value;
        if (unit is "ms" or "毫秒")
            return Math.Max(1, (int)Math.Round(value));
        if (unit is "s" or "sec" or "secs" or "秒" or "")
        {
            if (unit == "" && value >= 100)
                return Math.Max(1, (int)Math.Round(value));
            return Math.Max(1, (int)Math.Round(value * 1000));
        }
        return fallbackMs;
    }

    public static string Format(int milliseconds)
    {
        if (milliseconds % 1000 == 0)
            return $"{milliseconds / 1000} s";
        if (milliseconds >= 1000 && milliseconds % 100 == 0)
            return $"{(milliseconds / 1000d).ToString("0.#", CultureInfo.InvariantCulture)} s";
        return $"{milliseconds} ms";
    }
}
