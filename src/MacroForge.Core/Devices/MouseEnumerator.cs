using System.Runtime.InteropServices;
using MacroForge.Core.Native;

namespace MacroForge.Core.Devices;

public static class MouseEnumerator
{
    public static IReadOnlyList<PhysicalMouseDevice> Scan()
    {
        var rawDevices = ListRawDevices();
        var interfaces = new List<RawInterface>();
        foreach (var item in rawDevices)
        {
            var path = GetDevicePath(item.hDevice);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var info = GetDeviceInfo(item.hDevice);
            var (vid, pid) = ResolveVidPid(path, info);
            var instanceId = DevicePropertyReader.GetInterfaceString(path, Win32.DEVPKEY_Device_InstanceId)
                             ?? ExtractInstanceFromPath(path);
            var container = DevicePropertyReader.GetInterfaceGuid(path, Win32.DEVPKEY_Device_ContainerId)?.ToString();
            var (mfg, product) = HidCapabilityProbe.ReadHidStrings(path);
            var bus = DevicePropertyReader.GetInterfaceString(path, Win32.DEVPKEY_Device_BusReportedDeviceDesc);
            var friendly = DevicePropertyReader.GetInterfaceString(path, Win32.DEVPKEY_Device_FriendlyName);
            var desc = DevicePropertyReader.GetInterfaceString(path, Win32.DEVPKEY_Device_DeviceDesc);
            var manufacturerProp = DevicePropertyReader.GetInterfaceString(path, Win32.DEVPKEY_Device_Manufacturer);

            string? parentBus = null;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                var node = DevicePropertyReader.LocateDevNode(instanceId);
                if (node is uint inst)
                {
                    foreach (var parent in DevicePropertyReader.WalkParents(inst))
                    {
                        parentBus = DevicePropertyReader.GetDevNodeString(parent, Win32.DEVPKEY_Device_BusReportedDeviceDesc);
                        if (!DevicePropertyReader.IsGenericOsLabel(parentBus))
                            break;
                        var parentName = DevicePropertyReader.GetDevNodeString(parent, Win32.DEVPKEY_NAME);
                        if (!DevicePropertyReader.IsGenericOsLabel(parentName))
                        {
                            parentBus = parentName;
                            break;
                        }
                    }
                }
            }

            var caps = HidCapabilityProbe.Probe(path, info);
            var usagePage = caps.UsagePage != 0
                ? caps.UsagePage
                : info.dwType == Win32.RIM_TYPEHID ? info.hid.usUsagePage : (ushort)1;
            var usage = caps.Usage != 0
                ? caps.Usage
                : info.dwType == Win32.RIM_TYPEHID ? info.hid.usUsage : (ushort)2;

            interfaces.Add(new RawInterface
            {
                Handle = item.hDevice,
                RawType = item.dwType,
                Path = path,
                Vid = vid,
                Pid = pid,
                InstanceId = instanceId,
                ContainerId = container,
                Manufacturer = FirstNonEmpty(mfg, manufacturerProp),
                Product = product,
                BusReported = bus,
                ParentBusReported = parentBus,
                Friendly = friendly,
                DeviceDesc = desc,
                Capabilities = caps,
                UsagePage = usagePage,
                Usage = usage,
                Role = HidCapabilityProbe.RoleFromUsage(usagePage, usage, item.dwType)
            });
        }

        return GroupPhysicalMice(interfaces);
    }

    private static List<PhysicalMouseDevice> GroupPhysicalMice(List<RawInterface> interfaces)
    {
        var mice = interfaces.Where(IsPointingDevice).ToList();
        var others = interfaces.Where(i => !IsPointingDevice(i)).ToList();
        var result = new List<PhysicalMouseDevice>();

        foreach (var mouse in mice)
        {
            var siblings = others.Where(o => SamePhysicalDevice(mouse, o)).ToList();
            var grouped = new List<RawInterface> { mouse };
            grouped.AddRange(siblings);

            var fingerprint = DeviceFingerprint.Create(
                mouse.Vid,
                mouse.Pid,
                mouse.ContainerId,
                mouse.InstanceId,
                mouse.Path,
                mouse.UsagePage,
                mouse.Usage);

            var display = DevicePropertyReader.PickDisplayName(
                mouse.BusReported,
                mouse.Product,
                mouse.ParentBusReported,
                mouse.Friendly,
                mouse.DeviceDesc,
                mouse.Manufacturer,
                mouse.Vid,
                mouse.Pid);

            var isUnknown = mouse.Vid == 0 && mouse.Pid == 0 && DevicePropertyReader.IsGenericOsLabel(display);

            result.Add(new PhysicalMouseDevice
            {
                Fingerprint = fingerprint,
                Manufacturer = mouse.Manufacturer ?? "",
                ProductName = mouse.Product ?? display,
                FriendlyName = mouse.Friendly ?? display,
                DisplayName = display,
                InstanceId = mouse.InstanceId,
                ContainerId = mouse.ContainerId,
                DevicePath = mouse.Path,
                PrimaryHandle = mouse.Handle,
                Capabilities = mouse.Capabilities,
                Connection = Model.ConnectionState.Connected,
                IsUnknownHid = isUnknown,
                Interfaces = grouped.Select(g => new HidInterfaceInfo
                {
                    Handle = g.Handle,
                    RawType = g.RawType,
                    DevicePath = g.Path,
                    UsagePage = g.UsagePage,
                    Usage = g.Usage,
                    Role = g.Role
                }).ToList()
            });
        }

        return result
            .GroupBy(d => d.Fingerprint.DeviceId)
            .Select(g => g.First())
            .OrderBy(d => d.DisplayName)
            .ToList();
    }

    private static bool IsPointingDevice(RawInterface item)
    {
        if (item.RawType == Win32.RIM_TYPEKEYBOARD)
            return false;
        if (item.UsagePage == Win32.HID_USAGE_PAGE_GENERIC && item.Usage == Win32.HID_USAGE_GENERIC_KEYBOARD)
            return false;
        if (item.Role == "Keyboard")
            return false;
        if (LooksLikeKeyboard(item))
            return false;
        if (item.RawType == Win32.RIM_TYPEMOUSE)
            return true;
        if (item.UsagePage == Win32.HID_USAGE_PAGE_GENERIC && item.Usage == Win32.HID_USAGE_GENERIC_MOUSE)
            return true;
        return item.Role == "Mouse";
    }

    private static bool LooksLikeKeyboard(RawInterface item)
    {
        var name = $"{item.Product} {item.Friendly} {item.DeviceDesc} {item.BusReported}";
        if (name.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Mouse", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool SamePhysicalDevice(RawInterface mouse, RawInterface other)
    {
        if (!string.IsNullOrWhiteSpace(mouse.ContainerId) &&
            string.Equals(mouse.ContainerId, other.ContainerId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (mouse.Vid != 0 && mouse.Pid != 0 && mouse.Vid == other.Vid && mouse.Pid == other.Pid)
        {
            if (!string.IsNullOrWhiteSpace(mouse.InstanceId) && !string.IsNullOrWhiteSpace(other.InstanceId))
            {
                var a = ParentToken(mouse.InstanceId);
                var b = ParentToken(other.InstanceId);
                if (a.Length > 0 && a == b)
                    return true;
            }
        }

        return false;
    }

    private static string ParentToken(string instanceId)
    {
        var parts = instanceId.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? string.Join("\\", parts.Take(2)) : instanceId;
    }

    private static List<RAWINPUTDEVICELIST> ListRawDevices()
    {
        uint count = 0;
        var size = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
        NativeMethods.GetRawInputDeviceList(null, ref count, size);
        if (count == 0)
            return new List<RAWINPUTDEVICELIST>();
        var list = new RAWINPUTDEVICELIST[count];
        var written = NativeMethods.GetRawInputDeviceList(list, ref count, size);
        if (written == uint.MaxValue)
            return new List<RAWINPUTDEVICELIST>();
        return list.Take((int)written).ToList();
    }

    private static string? GetDevicePath(IntPtr handle)
    {
        uint size = 0;
        NativeMethods.GetRawInputDeviceInfo(handle, Win32.RIDI_DEVICENAME, IntPtr.Zero, ref size);
        if (size == 0) return null;
        var ptr = Marshal.AllocHGlobal((int)size * 2);
        try
        {
            var result = NativeMethods.GetRawInputDeviceInfo(handle, Win32.RIDI_DEVICENAME, ptr, ref size);
            if (result == uint.MaxValue) return null;
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static RID_DEVICE_INFO GetDeviceInfo(IntPtr handle)
    {
        var info = new RID_DEVICE_INFO { cbSize = (uint)Marshal.SizeOf<RID_DEVICE_INFO>() };
        var size = info.cbSize;
        var ptr = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            var result = NativeMethods.GetRawInputDeviceInfo(handle, Win32.RIDI_DEVICEINFO, ptr, ref size);
            if (result == uint.MaxValue)
                return info;
            return Marshal.PtrToStructure<RID_DEVICE_INFO>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static (ushort Vid, ushort Pid) ResolveVidPid(string path, RID_DEVICE_INFO info)
    {
        if (info.dwType == Win32.RIM_TYPEHID && (info.hid.dwVendorId != 0 || info.hid.dwProductId != 0))
            return ((ushort)info.hid.dwVendorId, (ushort)info.hid.dwProductId);

        var hid = HidCapabilityProbe.ReadAttributes(path);
        if (hid is { } attr)
            return attr;

        if (DeviceFingerprint.TryParseVidPid(path, out var vid, out var pid))
            return (vid, pid);

        return (0, 0);
    }

    private static string ExtractInstanceFromPath(string path)
    {
        var trimmed = path.Replace(@"\\?\", "").Replace(@"\\.\", "");
        var guidIndex = trimmed.IndexOf('{');
        if (guidIndex > 0)
            trimmed = trimmed[..guidIndex].TrimEnd('#');
        return trimmed.Replace('#', '\\');
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private sealed class RawInterface
    {
        public IntPtr Handle { get; init; }
        public uint RawType { get; init; }
        public string Path { get; init; } = "";
        public ushort Vid { get; init; }
        public ushort Pid { get; init; }
        public string? InstanceId { get; init; }
        public string? ContainerId { get; init; }
        public string? Manufacturer { get; init; }
        public string? Product { get; init; }
        public string? BusReported { get; init; }
        public string? ParentBusReported { get; init; }
        public string? Friendly { get; init; }
        public string? DeviceDesc { get; init; }
        public Model.DeviceCapabilities Capabilities { get; init; } = new();
        public ushort UsagePage { get; init; }
        public ushort Usage { get; init; }
        public string Role { get; init; } = "";
    }
}
