using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace UtilityGamer.WinUI.Helpers;

/// <summary>
/// Reads and writes Windows accessibility keyboard shortcut settings.
/// Uses the official Sticky Keys system API so the current session is updated
/// immediately, while preserving other flags already configured by Windows.
/// Bit 4 (mask 0x04) = SKF_HOTKEYACTIVE - controls the Shift x5 shortcut.
/// </summary>
public static class AccessibilityHelper
{
    private const string StickyKeysPath = @"Control Panel\Accessibility\StickyKeys";
    private const string ToggleKeysPath = @"Control Panel\Accessibility\ToggleKeys";
    private const string FilterKeysPath = @"Control Panel\Accessibility\Keyboard Response";

    private const int HotkeyActiveBit = 0x04;
    private const int DefaultFlags = 510;

    private const uint SpiGetStickyKeys = 0x003A;
    private const uint SpiSetStickyKeys = 0x003B;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct StickyKeysInfo
    {
        public uint cbSize;
        public uint dwFlags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        ref StickyKeysInfo pvParam,
        uint fWinIni);

    /// <summary>
    /// Returns true if the Sticky Keys shortcut (Shift x5) is currently disabled.
    /// </summary>
    public static bool IsStickyKeysDisabled()
        => (GetStickyKeysFlags() & HotkeyActiveBit) == 0;

    /// <summary>
    /// Disables or restores the Sticky Keys shortcut.
    /// Also mirrors the hotkey bit to Toggle Keys and Filter Keys registry flags.
    /// </summary>
    public static void SetStickyKeysDisabled(bool disable)
    {
        var currentFlags = GetStickyKeysFlags();
        var newFlags = disable
            ? currentFlags & ~HotkeyActiveBit
            : currentFlags | HotkeyActiveBit;

        ApplyStickyKeysFlags(newFlags);
        ApplyRegistryBit(ToggleKeysPath, HotkeyActiveBit, clear: disable);
        ApplyRegistryBit(FilterKeysPath, HotkeyActiveBit, clear: disable);
    }

    private static int GetStickyKeysFlags()
    {
        var stickyKeys = new StickyKeysInfo
        {
            cbSize = (uint)Marshal.SizeOf<StickyKeysInfo>()
        };

        try
        {
            if (SystemParametersInfo(SpiGetStickyKeys, stickyKeys.cbSize, ref stickyKeys, 0))
                return unchecked((int)stickyKeys.dwFlags);
        }
        catch
        {
            // Fall back to the persisted registry value below.
        }

        return ReadRegistryFlags(StickyKeysPath);
    }

    private static void ApplyStickyKeysFlags(int flags)
    {
        var stickyKeys = new StickyKeysInfo
        {
            cbSize = (uint)Marshal.SizeOf<StickyKeysInfo>(),
            dwFlags = unchecked((uint)flags)
        };

        try
        {
            _ = SystemParametersInfo(
                SpiSetStickyKeys,
                stickyKeys.cbSize,
                ref stickyKeys,
                SpifUpdateIniFile | SpifSendChange);
        }
        catch
        {
            // Best effort only.
        }

        WriteRegistryFlags(StickyKeysPath, flags);
    }

    private static int ReadRegistryFlags(string subKey)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            var raw = key?.GetValue("Flags")?.ToString() ?? DefaultFlags.ToString();
            return int.TryParse(raw, out var parsed) ? parsed : DefaultFlags;
        }
        catch
        {
            return DefaultFlags;
        }
    }

    private static void WriteRegistryFlags(string subKey, int flags)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(subKey, writable: true);
            key?.SetValue("Flags", flags.ToString(), RegistryValueKind.String);
        }
        catch
        {
            // Best effort only.
        }
    }

    private static void ApplyRegistryBit(string subKey, int bitMask, bool clear)
    {
        var current = ReadRegistryFlags(subKey);
        var newFlags = clear
            ? current & ~bitMask
            : current | bitMask;

        WriteRegistryFlags(subKey, newFlags);
    }
}