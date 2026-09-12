using System.Runtime.InteropServices;
using System.Text;
using MacroForge.Core.Native;

namespace MacroForge.Core.Devices;

internal static class DevicePropertyReader
{
    private static readonly HashSet<string> GenericLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "HID-compliant mouse",
        "HID-compliant device",
        "HID-compliant consumer control device",
        "USB Input Device",
        "USB Composite Device",
        "HID Keyboard Device",
        "USB Human Interface Device",
        "Bluetooth HID Device",
        "HID-compliant vendor-defined device",
        "Generic USB Hub"
    };

    public static bool IsGenericOsLabel(string? name) =>
        string.IsNullOrWhiteSpace(name) || GenericLabels.Contains(name.Trim());

    public static string? GetInterfaceString(string devicePath, DEVPROPKEY key)
    {
        var local = key;
        uint size = 0;
        NativeMethods.CM_Get_Device_Interface_PropertyW(devicePath, ref local, out _, null, ref size, 0);
        if (size == 0) return null;
        var buffer = new byte[size];
        var cr = NativeMethods.CM_Get_Device_Interface_PropertyW(devicePath, ref local, out var type, buffer, ref size, 0);
        if (cr != Win32.CR_SUCCESS) return null;
        if (type == Win32.DEVPROP_TYPE_STRING)
            return Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
        return null;
    }

    public static Guid? GetInterfaceGuid(string devicePath, DEVPROPKEY key)
    {
        var local = key;
        uint size = 0;
        NativeMethods.CM_Get_Device_Interface_PropertyW(devicePath, ref local, out _, null, ref size, 0);
        if (size == 0) return null;
        var buffer = new byte[Math.Max(size, 16u)];
        var cr = NativeMethods.CM_Get_Device_Interface_PropertyW(devicePath, ref local, out var type, buffer, ref size, 0);
        if (cr != Win32.CR_SUCCESS || type != Win32.DEVPROP_TYPE_GUID) return null;
        return new Guid(buffer.AsSpan(0, 16).ToArray());
    }

    public static string? GetDevNodeString(uint devInst, DEVPROPKEY key)
    {
        var local = key;
        uint size = 0;
        NativeMethods.CM_Get_DevNode_PropertyW(devInst, ref local, out _, null, ref size, 0);
        if (size == 0) return null;
        var buffer = new byte[size];
        var cr = NativeMethods.CM_Get_DevNode_PropertyW(devInst, ref local, out var type, buffer, ref size, 0);
        if (cr != Win32.CR_SUCCESS) return null;
        if (type == Win32.DEVPROP_TYPE_STRING)
            return Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
        return null;
    }

    public static uint? LocateDevNode(string instanceId)
    {
        if (NativeMethods.CM_Locate_DevNodeW(out var inst, instanceId, 0) == Win32.CR_SUCCESS)
            return inst;
        return null;
    }

    public static IEnumerable<uint> WalkParents(uint devInst, int max = 6)
    {
        var current = devInst;
        for (var i = 0; i < max; i++)
        {
            if (NativeMethods.CM_Get_Parent(out var parent, current, 0) != Win32.CR_SUCCESS)
                yield break;
            yield return parent;
            current = parent;
        }
    }

    public static string? GetDeviceId(uint devInst)
    {
        var buffer = new char[512];
        if (NativeMethods.CM_Get_Device_IDW(devInst, buffer, (uint)buffer.Length, 0) != Win32.CR_SUCCESS)
            return null;
        return new string(buffer).TrimEnd('\0');
    }

    public static string PickDisplayName(
        string? busReported,
        string? hidProduct,
        string? parentBusReported,
        string? friendly,
        string? deviceDesc,
        string? manufacturer,
        ushort vid,
        ushort pid)
    {
        foreach (var candidate in new[] { busReported, hidProduct, parentBusReported, friendly, deviceDesc })
        {
            if (!IsGenericOsLabel(candidate))
                return candidate!;
        }

        if (!IsGenericOsLabel(manufacturer) && !IsGenericOsLabel(hidProduct))
            return $"{manufacturer} {hidProduct}".Trim();

        if (vid != 0 || pid != 0)
            return $"Generic HID Mouse (VID_{vid:X4} PID_{pid:X4})";

        return "Generic HID Mouse";
    }
}
