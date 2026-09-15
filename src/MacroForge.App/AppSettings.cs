using System.IO;
using System.Text.Json;

namespace MacroForge.App;

internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Accent { get; set; } = "綠";
    /// <summary>When true, minimize hides to tray instead of taskbar.</summary>
    public bool MinimizeToTray { get; set; }
    /// <summary>When false, show the first-time tray confirm dialog on minimize.</summary>
    public bool MinimizeToTrayPromptSeen { get; set; }

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacroForge",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json);
                if (loaded is not null)
                    return loaded;
            }

            // Migrate legacy accent.txt if present.
            var accentPath = Path.Combine(Path.GetDirectoryName(FilePath)!, "accent.txt");
            if (File.Exists(accentPath))
            {
                var settings = new AppSettings { Accent = File.ReadAllText(accentPath).Trim() };
                settings.Save();
                return settings;
            }
        }
        catch
        {
            // fall through
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // Settings persistence is optional.
        }
    }
}
