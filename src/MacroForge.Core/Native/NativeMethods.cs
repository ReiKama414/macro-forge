using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MacroForge.Core.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct DEVPROPKEY
{
    public Guid fmtid;
    public uint pid;

    public DEVPROPKEY(Guid fmtid, uint pid)
    {
        this.fmtid = fmtid;
        this.pid = pid;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTDEVICE
{
    public ushort usUsagePage;
    public ushort usUsage;
    public uint dwFlags;
    public IntPtr hwndTarget;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTDEVICELIST
{
    public IntPtr hDevice;
    public uint dwType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTHEADER
{
    public uint dwType;
    public uint dwSize;
    public IntPtr hDevice;
    public IntPtr wParam;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_MOUSE
{
    public uint dwId;
    public uint dwNumberOfButtons;
    public uint dwSampleRate;
    public int fHasHorizontalWheel;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_KEYBOARD
{
    public uint dwType;
    public uint dwSubType;
    public uint dwKeyboardMode;
    public uint dwNumberOfFunctionKeys;
    public uint dwNumberOfIndicators;
    public uint dwNumberOfKeysTotal;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_HID
{
    public uint dwVendorId;
    public uint dwProductId;
    public uint dwVersionNumber;
    public ushort usUsagePage;
    public ushort usUsage;
}

[StructLayout(LayoutKind.Explicit)]
internal struct RID_DEVICE_INFO
{
    [FieldOffset(0)] public uint cbSize;
    [FieldOffset(4)] public uint dwType;
    [FieldOffset(8)] public RID_DEVICE_INFO_MOUSE mouse;
    [FieldOffset(8)] public RID_DEVICE_INFO_KEYBOARD keyboard;
    [FieldOffset(8)] public RID_DEVICE_INFO_HID hid;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HIDD_ATTRIBUTES
{
    public uint Size;
    public ushort VendorID;
    public ushort ProductID;
    public ushort VersionNumber;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HIDP_CAPS
{
    public ushort Usage;
    public ushort UsagePage;
    public ushort InputReportByteLength;
    public ushort OutputReportByteLength;
    public ushort FeatureReportByteLength;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
    public ushort[] Reserved;
    public ushort NumberLinkCollectionNodes;
    public ushort NumberInputButtonCaps;
    public ushort NumberInputValueCaps;
    public ushort NumberInputDataIndices;
    public ushort NumberOutputButtonCaps;
    public ushort NumberOutputValueCaps;
    public ushort NumberOutputDataIndices;
    public ushort NumberFeatureButtonCaps;
    public ushort NumberFeatureValueCaps;
    public ushort NumberFeatureDataIndices;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HIDP_BUTTON_CAPS
{
    public ushort UsagePage;
    public byte ReportID;
    public byte IsAlias;
    public ushort BitField;
    public ushort LinkCollection;
    public ushort LinkUsage;
    public ushort LinkUsagePage;
    public byte IsRange;
    public byte IsStringRange;
    public byte IsDesignatorRange;
    public byte IsAbsolute;
    public uint ReportCountReserved;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
    public uint[] Reserved;
    public ushort UsageMinOrUsage;
    public ushort UsageMaxOrReserved1;
    public ushort StringMinOrIndex;
    public ushort StringMaxOrReserved2;
    public ushort DesignatorMinOrIndex;
    public ushort DesignatorMaxOrReserved3;
    public ushort DataIndexMinOrIndex;
    public ushort DataIndexMaxOrReserved4;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HIDP_VALUE_CAPS
{
    public ushort UsagePage;
    public byte ReportID;
    public byte IsAlias;
    public ushort BitField;
    public ushort LinkCollection;
    public ushort LinkUsage;
    public ushort LinkUsagePage;
    public byte IsRange;
    public byte IsStringRange;
    public byte IsDesignatorRange;
    public byte IsAbsolute;
    public byte HasNull;
    public byte ReservedByte;
    public ushort BitSize;
    public ushort ReportCount;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public ushort Reserved4;
    public ushort Reserved5;
    public uint UnitsExp;
    public uint Units;
    public int LogicalMin;
    public int LogicalMax;
    public int PhysicalMin;
    public int PhysicalMax;
    public ushort UsageMinOrUsage;
    public ushort UsageMaxOrReserved1;
    public ushort StringMinOrIndex;
    public ushort StringMaxOrReserved2;
    public ushort DesignatorMinOrIndex;
    public ushort DesignatorMaxOrReserved3;
    public ushort DataIndexMinOrIndex;
    public ushort DataIndexMaxOrReserved4;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public int type;
    public INPUTUNION u;
}

[StructLayout(LayoutKind.Explicit)]
internal struct INPUTUNION
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

internal sealed class SafeHidHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeHidHandle() : base(true) { }

    public SafeHidHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.HidD_FreePreparsedData(handle);
        return true;
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetRawInputDeviceList(
        [Out] RAWINPUTDEVICELIST[]? pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterRawInputDevices(
        RAWINPUTDEVICE[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

    [DllImport("hid.dll", CharSet = CharSet.Unicode)]
    public static extern bool HidD_GetManufacturerString(SafeFileHandle hidDeviceObject, [Out] char[] buffer, int bufferLength);

    [DllImport("hid.dll", CharSet = CharSet.Unicode)]
    public static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, [Out] char[] buffer, int bufferLength);

    [DllImport("hid.dll")]
    public static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    public static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    [DllImport("hid.dll")]
    public static extern int HidP_GetButtonCaps(int reportType, [Out] HIDP_BUTTON_CAPS[] buttonCaps, ref ushort buttonCapsLength, IntPtr preparsedData);

    [DllImport("hid.dll")]
    public static extern int HidP_GetValueCaps(int reportType, [Out] HIDP_VALUE_CAPS[] valueCaps, ref ushort valueCapsLength, IntPtr preparsedData);

    [DllImport("hid.dll")]
    public static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_PropertyW(
        string pszDeviceInterface,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_IDW(uint dnDevInst, [Out] char[] buffer, uint bufferLen, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_DevNode_PropertyW(
        uint dnDevInst,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint ulFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, System.Text.StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
}
