using System.Text.Json;
using MacroForge.Core.Model;

namespace MacroForge.Core.Storage;

public sealed class DeviceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string RootPath { get; }

    public DeviceStore(string? rootPath = null)
    {
        RootPath = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MacroForge",
            "devices");
        Directory.CreateDirectory(RootPath);
    }

    public string DeviceDirectory(string deviceId) => Path.Combine(RootPath, Sanitize(deviceId));

    public bool Exists(string deviceId) => File.Exists(Path.Combine(DeviceDirectory(deviceId), "device.json"));

    public IReadOnlyList<string> ListDeviceIds()
    {
        if (!Directory.Exists(RootPath))
            return Array.Empty<string>();
        return Directory.GetDirectories(RootPath)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();
    }

    public DeviceSnapshot Load(string deviceId)
    {
        var dir = DeviceDirectory(deviceId);
        return new DeviceSnapshot
        {
            Device = Read<DeviceModel>(Path.Combine(dir, "device.json")) ?? new DeviceModel { DeviceId = deviceId },
            Layout = Read<LayoutModel>(Path.Combine(dir, "layout.json")) ?? new LayoutModel(),
            Bindings = Read<BindingsModel>(Path.Combine(dir, "bindings.json")) ?? new BindingsModel()
        };
    }

    public void Save(DeviceSnapshot snapshot)
    {
        var dir = DeviceDirectory(snapshot.Device.DeviceId);
        Directory.CreateDirectory(dir);
        snapshot.Device.UpdatedAt = DateTimeOffset.UtcNow;
        Write(Path.Combine(dir, "device.json"), snapshot.Device);
        Write(Path.Combine(dir, "layout.json"), snapshot.Layout);
        Write(Path.Combine(dir, "bindings.json"), snapshot.Bindings);
    }

    public DeviceSnapshot Create(DeviceModel model, LayoutModel layout)
    {
        var snapshot = new DeviceSnapshot
        {
            Device = model,
            Layout = layout,
            Bindings = new BindingsModel()
        };
        Save(snapshot);
        return snapshot;
    }

    private static T? Read<T>(string path)
    {
        if (!File.Exists(path))
            return default;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static void Write<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static string Sanitize(string deviceId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(deviceId.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
