namespace MacroForge.Core.Native;

internal static class Win32
{
    public const int RID_INPUT = 0x10000003;
    public const int RID_HEADER = 0x10000005;
    public const uint RIDI_PREPARSEDDATA = 0x20000005;
    public const uint RIDI_DEVICENAME = 0x20000007;
    public const uint RIDI_DEVICEINFO = 0x2000000B;

    public const uint RIM_TYPEMOUSE = 0;
    public const uint RIM_TYPEKEYBOARD = 1;
    public const uint RIM_TYPEHID = 2;

    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RIDEV_DEVNOTIFY = 0x00002000;
    public const uint RIDEV_EXINPUTSINK = 0x00001000;

    public const int WM_INPUT = 0x00FF;
    public const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
    public const int WM_DEVICECHANGE = 0x0219;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;

    public const int GIDC_ARRIVAL = 1;
    public const int GIDC_REMOVAL = 2;

    public const int DBT_DEVICEARRIVAL = 0x8000;
    public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
    public const int DBT_DEVNODES_CHANGED = 7;

    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;

    public const int HidP_Input = 0;
    public const ushort HID_USAGE_PAGE_GENERIC = 0x01;
    public const ushort HID_USAGE_PAGE_CONSUMER = 0x0C;
    public const ushort HID_USAGE_PAGE_BUTTON = 0x09;
    public const ushort HID_USAGE_GENERIC_MOUSE = 0x02;
    public const ushort HID_USAGE_GENERIC_POINTER = 0x01;
    public const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;

    public const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
    public const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
    public const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
    public const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
    public const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
    public const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
    public const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
    public const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;
    public const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;
    public const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;
    public const ushort RI_MOUSE_WHEEL = 0x0400;
    public const ushort RI_MOUSE_HWHEEL = 0x0800;

    public const uint RIDEV_REMOVE = 0x00000001;

    public const int CR_SUCCESS = 0;
    public const uint DEVPROP_TYPE_STRING = 0x00000012;
    public const uint DEVPROP_TYPE_GUID = 0x0000000D;
    public const uint DEVPROP_TYPE_UINT32 = 0x00000007;

    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint DIGCF_DEVICEINTERFACE = 0x00000010;

    public static readonly Guid GUID_DEVINTERFACE_MOUSE = new("378DE44C-56EF-11D1-BC8C-00A0C91405DD");
    public static readonly Guid GUID_DEVINTERFACE_HID = new("4D1E55B2-F16F-11CF-88CB-001111000030");
    public static readonly Guid GUID_DEVINTERFACE_KEYBOARD = new("884B96C3-56EF-11D1-BC8C-00A0C91405DD");

    // DEVPKEY_Device_ContainerId
    public static DEVPROPKEY DEVPKEY_Device_ContainerId => new(
        new Guid(0x8c7ed206, 0x3f87, 0x41e2, 0x81, 0xe3, 0x4e, 0x55, 0x87, 0xb7, 0x3d, 0x83), 2);

    // DEVPKEY_Device_BusReportedDeviceDesc
    public static DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc => new(
        new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), 4);

    // DEVPKEY_Device_InstanceId
    public static DEVPROPKEY DEVPKEY_Device_InstanceId => new(
        new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), 256);

    // DEVPKEY_NAME
    public static DEVPROPKEY DEVPKEY_NAME => new(
        new Guid(0xb725f130, 0x47ef, 0x101a, 0xa5, 0xf1, 0x02, 0x60, 0x8c, 0x9e, 0xeb, 0xac), 10);

    // DEVPKEY_Device_Manufacturer
    public static DEVPROPKEY DEVPKEY_Device_Manufacturer => new(
        new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), 13);

    // DEVPKEY_Device_FriendlyName
    public static DEVPROPKEY DEVPKEY_Device_FriendlyName => new(
        new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), 14);

    // DEVPKEY_Device_DeviceDesc
    public static DEVPROPKEY DEVPKEY_Device_DeviceDesc => new(
        new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), 2);

    public const uint SPDRP_DEVICEDESC = 0x00000000;
    public const uint SPDRP_MFG = 0x0000000B;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    public const uint SPDRP_HARDWAREID = 0x00000001;

    public const int INPUT_KEYBOARD = 1;
    public const int INPUT_MOUSE = 0;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const uint KEYEVENTF_SCANCODE = 0x0008;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const int VK_F12 = 0x7B;
}
