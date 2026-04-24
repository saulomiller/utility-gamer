using System.Runtime.InteropServices;

namespace UtilityGamer.WinUI.Helpers;

/// <summary>
/// Blocks the left and right Windows keys while a monitored game profile is active.
/// </summary>
public static class WindowsKeyBlocker
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private static readonly object Sync = new();
    private static readonly LowLevelKeyboardProc HookProc = KeyboardHookProc;
    private static nint _hookHandle;
    private static bool _blocked;

    public static bool IsBlocked
    {
        get
        {
            lock (Sync)
                return _blocked && _hookHandle != 0;
        }
    }

    public static void SetBlocked(bool blocked)
    {
        lock (Sync)
        {
            if (blocked == _blocked && (_hookHandle != 0 || !blocked))
                return;

            if (blocked)
            {
                _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, HookProc, GetModuleHandle(null), 0);
                _blocked = _hookHandle != 0;
            }
            else
            {
                if (_hookHandle != 0)
                {
                    UnhookWindowsHookEx(_hookHandle);
                    _hookHandle = 0;
                }

                _blocked = false;
            }
        }
    }

    private static nint KeyboardHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && IsBlocked && IsWindowsKeyMessage(wParam, lParam))
            return 1;

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsWindowsKeyMessage(nint wParam, nint lParam)
    {
        var message = wParam.ToInt32();
        if (message is not (WM_KEYDOWN or WM_KEYUP or WM_SYSKEYDOWN or WM_SYSKEYUP))
            return false;

        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        return data.VkCode is VK_LWIN or VK_RWIN;
    }

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public int VkCode;
        public int ScanCode;
        public int Flags;
        public int Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        nint hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);
}
