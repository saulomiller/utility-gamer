using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace UtilityGamer.WinUI.Helpers;

/// <summary>
/// Reads and writes the Windows mouse acceleration setting.
/// </summary>
public static class MouseHelper
{
    private const string MousePath = @"Control Panel\Mouse";

    private const uint SpiSetMouse = 0x0004;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        int[] pvParam,
        uint fWinIni);

    public static bool IsMouseAccelDisabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(MousePath);
            var speed = key?.GetValue("MouseSpeed")?.ToString() ?? "1";
            return string.Equals(speed, "0", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static void SetMouseAccelDisabled(bool disable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(MousePath, writable: true);
            if (key is null) return;

            if (disable)
            {
                key.SetValue("MouseSpeed", "0", RegistryValueKind.String);
                key.SetValue("MouseThreshold1", "0", RegistryValueKind.String);
                key.SetValue("MouseThreshold2", "0", RegistryValueKind.String);
            }
            else
            {
                key.SetValue("MouseSpeed", "1", RegistryValueKind.String);
                key.SetValue("MouseThreshold1", "6", RegistryValueKind.String);
                key.SetValue("MouseThreshold2", "10", RegistryValueKind.String);
            }
        }
        catch
        {
            // Best effort only.
        }

        var mouseParams = disable
            ? new[] { 0, 0, 0 }
            : new[] { 6, 10, 1 };

        _ = SystemParametersInfo(
            SpiSetMouse,
            0,
            mouseParams,
            SpifUpdateIniFile | SpifSendChange);
    }
}