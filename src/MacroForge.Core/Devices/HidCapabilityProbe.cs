using System.Runtime.InteropServices;
using System.Text;
using MacroForge.Core.Hid;
using MacroForge.Core.Model;
using MacroForge.Core.Native;
using Microsoft.Win32.SafeHandles;

namespace MacroForge.Core.Devices;

internal static class HidCapabilityProbe
{
    public static DeviceCapabilities Probe(string devicePath, RID_DEVICE_INFO rawInfo)
    {
        ushort usagePage = 0;
        ushort usage = 0;
        var buttonCount = 0;
        var hasWheel = false;
        var hasHWheel = rawInfo.dwType == Win32.RIM_TYPEMOUSE && rawInfo.mouse.fHasHorizontalWheel != 0;
        var hidButtons = new SortedSet<int>();
        var collections = new List<HidCollectionInfo>();
        byte[]? descriptor = null;
        var reportLen = 0;

        if (rawInfo.dwType == Win32.RIM_TYPEMOUSE)
        {
            buttonCount = (int)rawInfo.mouse.dwNumberOfButtons;
            usagePage = Win32.HID_USAGE_PAGE_GENERIC;
            usage = Win32.HID_USAGE_GENERIC_MOUSE;
        }
        else if (rawInfo.dwType == Win32.RIM_TYPEHID)
        {
            usagePage = rawInfo.hid.usUsagePage;
            usage = rawInfo.hid.usUsage;
        }

        SafeFileHandle? handle = null;
        try
        {
            handle = NativeMethods.CreateFile(
                devicePath,
                Win32.GENERIC_READ,
                Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle is { IsInvalid: false })
            {
                if (NativeMethods.HidD_GetPreparsedData(handle, out var preparsed) && preparsed != IntPtr.Zero)
                {
                    try
                    {
                        if (NativeMethods.HidP_GetCaps(preparsed, out var caps) == 0)
                        {
                            usagePage = caps.UsagePage;
                            usage = caps.Usage;
                            reportLen = caps.InputReportByteLength;
                            collections.Add(new HidCollectionInfo
                            {
                                UsagePage = caps.UsagePage,
                                Usage = caps.Usage,
                                Kind = DescribeCollection(caps.UsagePage, caps.Usage)
                            });

                            if (caps.NumberInputButtonCaps > 0)
                            {
                                var buttonCaps = new HIDP_BUTTON_CAPS[caps.NumberInputButtonCaps];
                                var len = caps.NumberInputButtonCaps;
                                if (NativeMethods.HidP_GetButtonCaps(Win32.HidP_Input, buttonCaps, ref len, preparsed) == 0)
                                {
                                    foreach (var cap in buttonCaps.Take(len))
                                    {
                                        if (cap.UsagePage != Win32.HID_USAGE_PAGE_BUTTON)
                                            continue;
                                        if (cap.IsRange != 0)
                                        {
                                            for (var u = cap.UsageMinOrUsage; u <= cap.UsageMaxOrReserved1; u++)
                                                hidButtons.Add(u);
                                        }
                                        else
                                        {
                                            hidButtons.Add(cap.UsageMinOrUsage);
                                        }
                                    }
                                }
                            }

                            if (caps.NumberInputValueCaps > 0)
                            {
                                var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
                                var vlen = caps.NumberInputValueCaps;
                                if (NativeMethods.HidP_GetValueCaps(Win32.HidP_Input, valueCaps, ref vlen, preparsed) == 0)
                                {
                                    foreach (var cap in valueCaps.Take(vlen))
                                    {
                                        var valueUsage = cap.IsRange != 0 ? cap.UsageMinOrUsage : cap.UsageMinOrUsage;
                                        if (cap.UsagePage == Win32.HID_USAGE_PAGE_GENERIC && valueUsage == 0x38)
                                            hasWheel = true;
                                        if (cap.UsagePage == Win32.HID_USAGE_PAGE_GENERIC && valueUsage is 0x48)
                                            hasHWheel = true;
                                        if (cap.UsagePage == Win32.HID_USAGE_PAGE_CONSUMER && valueUsage == 0x0238)
                                            hasHWheel = true;
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        NativeMethods.HidD_FreePreparsedData(preparsed);
                    }
                }
            }
        }
        catch
        {
            // Some Bluetooth / virtual HID interfaces refuse CreateFile. Capabilities then come from Raw Input only.
        }
        finally
        {
            handle?.Dispose();
        }

        if (hidButtons.Count > 0)
            buttonCount = Math.Max(buttonCount, hidButtons.Count);

        return new DeviceCapabilities
        {
            ButtonCount = buttonCount,
            HasWheel = hasWheel,
            HasHorizontalWheel = hasHWheel,
            UsagePage = usagePage,
            Usage = usage,
            InputReportByteLength = reportLen,
            HidButtonUsages = hidButtons.ToList(),
            Collections = collections,
            ReportDescriptor = descriptor,
            ReportDescriptorHex = descriptor is null ? null : Convert.ToHexString(descriptor)
        };
    }

    public static (string? Manufacturer, string? Product) ReadHidStrings(string devicePath)
    {
        try
        {
            using var handle = NativeMethods.CreateFile(
                devicePath,
                0,
                Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32.OPEN_EXISTING,
                0,
                IntPtr.Zero);
            if (handle.IsInvalid)
                return (null, null);

            var mfg = new char[256];
            var prod = new char[256];
            string? manufacturer = null;
            string? product = null;
            if (NativeMethods.HidD_GetManufacturerString(handle, mfg, mfg.Length * 2))
                manufacturer = new string(mfg).TrimEnd('\0').Trim();
            if (NativeMethods.HidD_GetProductString(handle, prod, prod.Length * 2))
                product = new string(prod).TrimEnd('\0').Trim();
            return (NullIfEmpty(manufacturer), NullIfEmpty(product));
        }
        catch
        {
            return (null, null);
        }
    }

    public static (ushort Vid, ushort Pid)? ReadAttributes(string devicePath)
    {
        try
        {
            using var handle = NativeMethods.CreateFile(
                devicePath,
                0,
                Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32.OPEN_EXISTING,
                0,
                IntPtr.Zero);
            if (handle.IsInvalid)
                return null;
            var attr = new HIDD_ATTRIBUTES { Size = (uint)Marshal.SizeOf<HIDD_ATTRIBUTES>() };
            if (!NativeMethods.HidD_GetAttributes(handle, ref attr))
                return null;
            return (attr.VendorID, attr.ProductID);
        }
        catch
        {
            return null;
        }
    }

    public static string DescribeCollection(ushort page, ushort usage)
    {
        if (page == Win32.HID_USAGE_PAGE_GENERIC && usage == Win32.HID_USAGE_GENERIC_MOUSE)
            return "Mouse";
        if (page == Win32.HID_USAGE_PAGE_GENERIC && usage == Win32.HID_USAGE_GENERIC_POINTER)
            return "Pointer";
        if (page == Win32.HID_USAGE_PAGE_GENERIC && usage == Win32.HID_USAGE_GENERIC_KEYBOARD)
            return "Keyboard";
        if (page == Win32.HID_USAGE_PAGE_CONSUMER)
            return "Consumer";
        return $"UsagePage 0x{page:X4} Usage 0x{usage:X4}";
    }

    public static string RoleFromUsage(ushort page, ushort usage, uint rawType)
    {
        if (rawType == Win32.RIM_TYPEMOUSE || (page == 1 && usage == 2))
            return "Mouse";
        if (rawType == Win32.RIM_TYPEKEYBOARD || (page == 1 && usage == 6))
            return "Keyboard";
        if (page == Win32.HID_USAGE_PAGE_CONSUMER)
            return "ConsumerHid";
        return "Hid";
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
