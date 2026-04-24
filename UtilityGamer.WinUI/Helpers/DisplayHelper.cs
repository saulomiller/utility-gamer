using System.Runtime.InteropServices;

namespace UtilityGamer.WinUI.Helpers;

/// <summary>
/// Enumerates connected displays and changes their resolution/refresh rate
/// using the Win32 ChangeDisplaySettingsEx API.
/// </summary>
public static class DisplayHelper
{
    public sealed record MonitorInfo(
        string DeviceName,
        string FriendlyName,
        int CurrentWidth,
        int CurrentHeight,
        int CurrentHz,
        bool IsPrimary);

    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var result = new List<MonitorInfo>();
        var device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };

        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            if ((device.StateFlags & DisplayDeviceStateFlags.AttachedToDesktop) == 0)
            {
                device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                continue;
            }

            if ((device.StateFlags & DisplayDeviceStateFlags.MirroringDriver) != 0)
            {
                device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                continue;
            }

            var devName = device.DeviceName;
            var isPrimary = (device.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0;

            var monitor = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            var friendlyName = EnumDisplayDevices(devName, 0, ref monitor, 0)
                ? monitor.DeviceString
                : devName;

            var mode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            int w = 0;
            int h = 0;
            int hz = 0;
            if (EnumDisplaySettingsEx(devName, ENUM_CURRENT_SETTINGS, ref mode, 0))
            {
                w = (int)mode.dmPelsWidth;
                h = (int)mode.dmPelsHeight;
                hz = (int)mode.dmDisplayFrequency;
            }

            var idx = devName.LastIndexOf('\\');
            var shortDev = idx >= 0 ? devName[(idx + 1)..] : devName;

            result.Add(new MonitorInfo(
                DeviceName: devName,
                FriendlyName: $"{friendlyName} ({shortDev})",
                CurrentWidth: w,
                CurrentHeight: h,
                CurrentHz: hz,
                IsPrimary: isPrimary));

            device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        }

        return result;
    }

    public static string? SetResolution(string deviceName, int width, int height, int hz)
    {
        var mode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };

        if (!EnumDisplaySettingsEx(deviceName, ENUM_CURRENT_SETTINGS, ref mode, 0))
            return $"Could not read current settings for {deviceName}.";

        mode.dmPelsWidth = (uint)width;
        mode.dmPelsHeight = (uint)height;
        mode.dmDisplayFrequency = (uint)hz;
        mode.dmFields = DmFields.PelsWidth | DmFields.PelsHeight | DmFields.DisplayFrequency;

        var result = ChangeDisplaySettingsEx(
            deviceName,
            ref mode,
            IntPtr.Zero,
            ChangeDisplaySettingsFlags.UpdateRegistry | ChangeDisplaySettingsFlags.NoReset,
            IntPtr.Zero);

        if (result != DispChange.Successful)
            return $"ChangeDisplaySettingsEx failed: {result}";

        result = ChangeDisplaySettingsEx(
            null,
            IntPtr.Zero,
            IntPtr.Zero,
            ChangeDisplaySettingsFlags.None,
            IntPtr.Zero);

        return result == DispChange.Successful
            ? null
            : $"Failed to commit display changes: {result}";
    }

    public static (int Width, int Height, int Hz)? GetCurrentResolution(string deviceName)
    {
        var mode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
        if (!EnumDisplaySettingsEx(deviceName, ENUM_CURRENT_SETTINGS, ref mode, 0))
            return null;

        return ((int)mode.dmPelsWidth, (int)mode.dmPelsHeight, (int)mode.dmDisplayFrequency);
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettingsEx(
        string lpszDeviceName,
        int iModeNum,
        ref DEVMODE lpDevMode,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern DispChange ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        ref DEVMODE lpDevMode,
        IntPtr hwnd,
        ChangeDisplaySettingsFlags dwflags,
        IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern DispChange ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        IntPtr lpDevMode,
        IntPtr hwnd,
        ChangeDisplaySettingsFlags dwflags,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DISPLAY_DEVICE
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public DisplayDeviceStateFlags StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public DmFields dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [Flags]
    private enum DmFields : uint
    {
        PelsWidth = 0x00080000,
        PelsHeight = 0x00100000,
        DisplayFrequency = 0x00400000
    }

    [Flags]
    private enum DisplayDeviceStateFlags : uint
    {
        AttachedToDesktop = 0x00000001,
        PrimaryDevice = 0x00000004,
        MirroringDriver = 0x00000008
    }

    [Flags]
    private enum ChangeDisplaySettingsFlags : uint
    {
        None = 0x00000000,
        UpdateRegistry = 0x00000001,
        NoReset = 0x10000000
    }

    private enum DispChange : int
    {
        Successful = 0,
        Restart = 1,
        Failed = -1,
        BadMode = -2,
        NotUpdated = -3,
        BadFlags = -4,
        BadParam = -5,
        BadDualView = -6
    }
}
