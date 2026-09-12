using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MacroForge.Core.Devices;

public readonly record struct DeviceFingerprint(
    string DeviceId,
    ushort Vid,
    ushort Pid,
    string? ContainerId,
    string? InstanceId,
    string? DevicePath,
    ushort UsagePage,
    ushort Usage)
{
    public static DeviceFingerprint Create(
        ushort vid,
        ushort pid,
        string? containerId,
        string? instanceId,
        string? devicePath,
        ushort usagePage,
        ushort usage)
    {
        var uniqueSource = !string.IsNullOrWhiteSpace(containerId)
            ? containerId
            : !string.IsNullOrWhiteSpace(instanceId)
                ? NormalizeInstance(instanceId)
                : devicePath ?? "unknown";

        var token = ShortHash(uniqueSource);
        var deviceId = $"mouse_{vid:x4}_{pid:x4}_{token}";
        return new DeviceFingerprint(deviceId, vid, pid, containerId, instanceId, devicePath, usagePage, usage);
    }

    public static string NormalizeInstance(string instanceId)
    {
        return Regex.Replace(instanceId, @"[#\\?{}]", "_");
    }

    public static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant()));
        return Convert.ToHexString(bytes)[..10].ToLowerInvariant();
    }

    public static bool TryParseVidPid(string? hardwareOrPath, out ushort vid, out ushort pid)
    {
        vid = 0;
        pid = 0;
        if (string.IsNullOrWhiteSpace(hardwareOrPath))
            return false;

        var text = hardwareOrPath.ToUpperInvariant();
        var vidMatch = Regex.Match(text, @"VID[_]?([0-9A-F]{4})");
        var pidMatch = Regex.Match(text, @"PID[_]?([0-9A-F]{4})");
        if (!vidMatch.Success || !pidMatch.Success)
            return false;

        vid = Convert.ToUInt16(vidMatch.Groups[1].Value, 16);
        pid = Convert.ToUInt16(pidMatch.Groups[1].Value, 16);
        return true;
    }
}
