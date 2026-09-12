using System.Runtime.InteropServices;
using MacroForge.Core.Model;
using MacroForge.Core.Native;

namespace MacroForge.Core.Input;

public static class RawInputParser
{
    public static IReadOnlyList<RawInputEvent> Parse(IntPtr lParam)
    {
        uint size = 0;
        NativeMethods.GetRawInputData(lParam, Win32.RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (size == 0)
            return Array.Empty<RawInputEvent>();

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var read = NativeMethods.GetRawInputData(lParam, Win32.RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            if (read == uint.MaxValue)
                return Array.Empty<RawInputEvent>();
            return ParseBuffer(buffer, (int)size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static IReadOnlyList<RawInputEvent> ParseBuffer(IntPtr buffer, int size)
    {
        var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
        if (header.dwType == Win32.RIM_TYPEMOUSE)
            return ParseMouse(header, buffer);
        if (header.dwType == Win32.RIM_TYPEKEYBOARD)
            return ParseKeyboard(header, buffer);
        if (header.dwType == Win32.RIM_TYPEHID)
            return ParseHid(header, buffer, size);
        return Array.Empty<RawInputEvent>();
    }

    private static IReadOnlyList<RawInputEvent> ParseMouse(RAWINPUTHEADER header, IntPtr buffer)
    {
        var events = new List<RawInputEvent>();
        var offset = Marshal.SizeOf<RAWINPUTHEADER>();
        var flags = (ushort)Marshal.ReadInt16(buffer, offset + 4);
        var buttonData = (ushort)Marshal.ReadInt16(buffer, offset + 6);
        var rawButtons = (uint)Marshal.ReadInt32(buffer, offset + 8);

        AddMouseButton(events, header.hDevice, flags, Win32.RI_MOUSE_LEFT_BUTTON_DOWN, Win32.RI_MOUSE_LEFT_BUTTON_UP, 1);
        AddMouseButton(events, header.hDevice, flags, Win32.RI_MOUSE_RIGHT_BUTTON_DOWN, Win32.RI_MOUSE_RIGHT_BUTTON_UP, 2);
        AddMouseButton(events, header.hDevice, flags, Win32.RI_MOUSE_MIDDLE_BUTTON_DOWN, Win32.RI_MOUSE_MIDDLE_BUTTON_UP, 3);
        AddMouseButton(events, header.hDevice, flags, Win32.RI_MOUSE_BUTTON_4_DOWN, Win32.RI_MOUSE_BUTTON_4_UP, 4);
        AddMouseButton(events, header.hDevice, flags, Win32.RI_MOUSE_BUTTON_5_DOWN, Win32.RI_MOUSE_BUTTON_5_UP, 5);

        if ((flags & Win32.RI_MOUSE_WHEEL) != 0)
        {
            var delta = (short)buttonData;
            events.Add(new RawInputEvent
            {
                DeviceHandle = header.hDevice,
                Source = InputSource.Mouse,
                IsDown = true,
                RawButtonIndex = delta > 0 ? 1001 : 1002,
                WheelDelta = delta
            });
        }

        if ((flags & Win32.RI_MOUSE_HWHEEL) != 0)
        {
            var delta = (short)buttonData;
            events.Add(new RawInputEvent
            {
                DeviceHandle = header.hDevice,
                Source = InputSource.Mouse,
                IsDown = true,
                RawButtonIndex = delta > 0 ? 1003 : 1004,
                WheelDelta = delta
            });
        }

        if (rawButtons != 0)
        {
            for (var bit = 0; bit < 32; bit++)
            {
                if ((rawButtons & (1u << bit)) == 0)
                    continue;
                var index = bit + 1;
                if (index <= 5)
                    continue;
                events.Add(new RawInputEvent
                {
                    DeviceHandle = header.hDevice,
                    Source = InputSource.Mouse,
                    IsDown = true,
                    RawButtonIndex = index,
                    HidUsageId = index
                });
            }
        }

        return events;
    }

    private static void AddMouseButton(
        List<RawInputEvent> events,
        IntPtr handle,
        ushort flags,
        ushort downFlag,
        ushort upFlag,
        int index)
    {
        if ((flags & downFlag) != 0)
        {
            events.Add(new RawInputEvent
            {
                DeviceHandle = handle,
                Source = InputSource.Mouse,
                IsDown = true,
                RawButtonIndex = index,
                HidUsageId = index
            });
        }
        else if ((flags & upFlag) != 0)
        {
            events.Add(new RawInputEvent
            {
                DeviceHandle = handle,
                Source = InputSource.Mouse,
                IsDown = false,
                RawButtonIndex = index,
                HidUsageId = index
            });
        }
    }

    private static IReadOnlyList<RawInputEvent> ParseKeyboard(RAWINPUTHEADER header, IntPtr buffer)
    {
        var offset = Marshal.SizeOf<RAWINPUTHEADER>();
        var makeCode = (ushort)Marshal.ReadInt16(buffer, offset);
        var flags = (ushort)Marshal.ReadInt16(buffer, offset + 2);
        var vkey = (ushort)Marshal.ReadInt16(buffer, offset + 6);
        var breakFlag = (flags & 0x01) != 0;
        if (vkey is 0 or 0xFF)
            return Array.Empty<RawInputEvent>();

        return new[]
        {
            new RawInputEvent
            {
                DeviceHandle = header.hDevice,
                Source = InputSource.Keyboard,
                IsDown = !breakFlag,
                VirtualKey = vkey,
                ScanCode = makeCode
            }
        };
    }

    private static IReadOnlyList<RawInputEvent> ParseHid(RAWINPUTHEADER header, IntPtr buffer, int size)
    {
        var offset = Marshal.SizeOf<RAWINPUTHEADER>();
        if (size < offset + 8)
            return Array.Empty<RawInputEvent>();
        var dwSizeHid = Marshal.ReadInt32(buffer, offset);
        var dwCount = Marshal.ReadInt32(buffer, offset + 4);
        if (dwSizeHid <= 0 || dwCount <= 0)
            return Array.Empty<RawInputEvent>();

        var dataOffset = offset + 8;
        var total = Math.Min(dwSizeHid * dwCount, size - dataOffset);
        var report = new byte[total];
        Marshal.Copy(buffer + dataOffset, report, 0, total);

        return new[]
        {
            new RawInputEvent
            {
                DeviceHandle = header.hDevice,
                Source = InputSource.UnknownHid,
                IsDown = true,
                HidReport = report
            }
        };
    }
}
