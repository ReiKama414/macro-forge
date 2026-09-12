using MacroForge.Core.Learning;
using MacroForge.Core.Model;
using MacroForge.Core.Scope;

namespace MacroForge.Core.Macros;

public static class BindingResolver
{
    public static bool ButtonMatches(string bindingButton, MouseButtonDefinition def)
    {
        if (string.Equals(bindingButton, def.Id, StringComparison.OrdinalIgnoreCase))
            return true;
        if (def.RawButtonIndex is int index)
        {
            if (string.Equals(bindingButton, $"btn_{index}", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(bindingButton, $"mouse_{index}", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static ProfileDefinition? ResolveProfile(IEnumerable<ProfileDefinition> profiles, ForegroundApp? app)
    {
        var list = profiles.ToList();
        var appProfile = list
            .Where(p => p.Scope.IsApplications && ScopeMatcher.Matches(p.Scope, app))
            .OrderByDescending(p => p.Scope.Applications.Count)
            .FirstOrDefault();
        if (appProfile is not null)
            return appProfile;
        return list.FirstOrDefault(p => p.Scope.IsGlobal);
    }

    public static ButtonBinding? Resolve(
        BindingsModel model,
        MouseButtonDefinition button,
        ForegroundApp? app)
    {
        var profile = ResolveProfile(model.Profiles, app);
        var candidates = model.Bindings
            .Where(b => b.Enabled)
            .Where(b => ButtonMatches(b.TargetButton, button))
            .Where(b => string.IsNullOrWhiteSpace(b.ProfileId) || (profile is not null && b.ProfileId == profile.Id))
            .Where(b => ScopeMatcher.Matches(b.Scope, app))
            .OrderByDescending(b => ScopeMatcher.Priority(b.Scope))
            .ThenByDescending(b => b.Scope.Applications.Count)
            .ToList();
        return candidates.FirstOrDefault();
    }

    public static MacroDefinition ToMacro(ButtonBinding binding, BindingsModel model)
    {
        if (binding.Macro is { Actions.Count: > 0 } inline)
        {
            if (string.IsNullOrWhiteSpace(inline.Name))
                inline.Name = binding.Name;
            if (string.IsNullOrWhiteSpace(inline.Id))
                inline.Id = binding.Id;
            return inline;
        }

        if (!string.IsNullOrWhiteSpace(binding.Action.MacroId))
        {
            var named = model.Macros.FirstOrDefault(m => m.Id == binding.Action.MacroId);
            if (named is not null)
                return named;
        }

        var keys = binding.Action.Keys.Count > 0
            ? binding.Action.Keys
            : string.IsNullOrWhiteSpace(binding.Action.Key) ? new List<string>() : new List<string> { binding.Action.Key };

        var actions = new List<MacroAction>();
        if (string.Equals(binding.Action.Type, "mouse", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(binding.Action.MouseButton))
        {
            actions.Add(new MacroAction { Type = "mouse", MouseButton = binding.Action.MouseButton ?? "left" });
        }
        else if (keys.Count > 0)
        {
            actions.Add(new MacroAction { Type = "key", Keys = keys });
        }

        return new MacroDefinition
        {
            Id = binding.Id,
            Name = string.IsNullOrWhiteSpace(binding.Name) ? binding.TargetButton : binding.Name,
            Actions = actions
        };
    }
}
