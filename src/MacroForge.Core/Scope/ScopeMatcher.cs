using MacroForge.Core.Model;

namespace MacroForge.Core.Scope;

public static class ScopeMatcher
{
    public static bool Matches(ScopeDefinition scope, ForegroundApp? app)
    {
        if (scope.IsGlobal)
            return true;
        if (app is null)
            return false;
        if (scope.Applications.Count == 0)
            return false;

        var pathMatch = string.Equals(scope.Match, "path", StringComparison.OrdinalIgnoreCase);
        foreach (var entry in scope.Applications)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;
            if (pathMatch)
            {
                if (!string.IsNullOrWhiteSpace(app.ExecutablePath) &&
                    string.Equals(app.ExecutablePath, entry, StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            var wanted = Path.GetFileName(entry);
            if (string.Equals(wanted, app.ExeFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(wanted, app.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(wanted, AppendExe(app.ProcessName), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static int Priority(ScopeDefinition scope) => scope.IsApplications ? 2 : 1;

    public static string Label(ScopeDefinition scope)
    {
        if (scope.IsGlobal || scope.Applications.Count == 0)
            return "全域";
        return string.Join(", ", scope.Applications.Select(Path.GetFileName));
    }

    private static string AppendExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
}
