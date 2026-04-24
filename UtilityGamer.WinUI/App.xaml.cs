using System.Runtime.InteropServices;

namespace UtilityGamer.WinUI;

public partial class App : Application
{
    private const uint WM_SIZE = 0x0005;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_SETICON = 0x0080;
    private const uint WM_TRAYICON = 0x8001;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint NIN_SELECT = 0x0400;
    private const uint NIN_KEYSELECT = 0x0401;

    private const int SIZE_MINIMIZED = 1;
    private const int SW_HIDE = 0;
    private const int SW_RESTORE = 9;
    private const int GWL_WNDPROC = -4;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const uint LR_DEFAULTSIZE = 0x0040;
    private const uint MF_STRING = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint MF_GRAYED = 0x0001;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TRAY_CMD_OPEN = 1001;
    private const uint TRAY_CMD_START = 1002;
    private const uint TRAY_CMD_STOP = 1003;
    private const uint TRAY_CMD_EXIT = 1004;
    private const uint LOWORD_MASK = 0xFFFF;

    public static Window MainWindow { get; private set; } = null!;

    private static nint _windowHandle;
    private static nint _smallIconHandle;
    private static nint _largeIconHandle;
    private static nint _trayIconHandle;
    private static nint _oldWndProc;
    private static WndProcDelegate? _wndProc;
    private static NotifyIconData _notifyIconData;

    private static bool _trayIconVisible;
    private static bool _trayMonitoringActive;
    private static bool _minimizeToTrayEnabled;
    private static bool _hideToTrayOnLaunch;
    private static bool _isExitRequested;
    private static string _uiLanguage = "pt-BR";

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ToastHelper.EnsureRegistered();

        var settings = TryReadSettingsStatic();
        _uiLanguage = NormalizeUiLanguageCode(settings.Language);
        _minimizeToTrayEnabled = settings.StartMinimizedToTray;
        _hideToTrayOnLaunch = _minimizeToTrayEnabled &&
            args.Arguments.Contains("--tray", StringComparison.OrdinalIgnoreCase);

        MainWindow = new Window
        {
            Title = "Utility Gamer",
            Content = new Views.MainPage()
        };

        ApplyWindowPlacement(settings);
        MainWindow.Activate();
        _windowHandle = WindowNative.GetWindowHandle(MainWindow);

        ApplyTitleBarTheme(MainWindow);
        ApplyWindowAndTaskbarIcons();
        EnsureTrayIconRegistered();
        InstallWindowHook();

        MainWindow.Closed += OnMainWindowClosed;

        if (_hideToTrayOnLaunch)
            HideToTray();
    }

    public static void SetMinimizeToTrayEnabled(bool enabled)
    {
        _minimizeToTrayEnabled = enabled;
        if (!enabled)
            RestoreFromTray();
    }

    public static void SetUiLanguage(string languageCode)
    {
        _uiLanguage = NormalizeUiLanguageCode(languageCode);
    }

    public static void SetTitleBarTheme()
    {
        if (MainWindow is not null)
            ApplyTitleBarTheme(MainWindow);
    }

    public static void SetTrayMonitoringState(bool active)
    {
        _trayMonitoringActive = active;
    }

    private static void ApplyTitleBarTheme(Window window)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported()) return;

        var titleBar = window.AppWindow.TitleBar;

        var bg = Color.FromArgb(255, 0x11, 0x1C, 0x27);
        var bgHover = Color.FromArgb(255, 0x16, 0x20, 0x30);
        var bgPress = Color.FromArgb(255, 0x0D, 0x17, 0x20);
        var fg = Color.FromArgb(255, 0xD4, 0xE4, 0xF0);
        var fgMuted = Color.FromArgb(255, 0x7A, 0x92, 0xA8);

        titleBar.BackgroundColor = bg;
        titleBar.InactiveBackgroundColor = bg;
        titleBar.ButtonBackgroundColor = bg;
        titleBar.ButtonInactiveBackgroundColor = bg;
        titleBar.ButtonHoverBackgroundColor = bgHover;
        titleBar.ButtonPressedBackgroundColor = bgPress;

        titleBar.ForegroundColor = fg;
        titleBar.InactiveForegroundColor = fgMuted;
        titleBar.ButtonForegroundColor = fg;
        titleBar.ButtonInactiveForegroundColor = fgMuted;
        titleBar.ButtonHoverForegroundColor = fg;
        titleBar.ButtonPressedForegroundColor = fg;
    }

    private static void ApplyWindowAndTaskbarIcons()
    {
        var appPng = ResolveAssetPath("icons8-engrenagem-100.png");
        var bigPng = ResolveAssetPath("icons8-engrenagem-96.png");
        var smallPng = ResolveAssetPath("icons8-engrenagem-48.png");
        var trayPng = ResolveAssetPath("icons8-engrenagem-24.png");

        var appIco = EnsureIco(appPng, "app-100.ico");
        var bigIco = EnsureIco(bigPng, "window-big-96.ico");
        var smallIco = EnsureIco(smallPng, "window-small-48.ico");
        var trayIco = EnsureIco(trayPng, "tray-24.ico");

        if (!string.IsNullOrWhiteSpace(appIco))
            MainWindow.AppWindow.SetIcon(appIco);

        if (!string.IsNullOrWhiteSpace(bigIco))
            _largeIconHandle = LoadImage(0, bigIco, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);

        if (!string.IsNullOrWhiteSpace(smallIco))
            _smallIconHandle = LoadImage(0, smallIco, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);

        if (!string.IsNullOrWhiteSpace(trayIco))
            _trayIconHandle = LoadImage(0, trayIco, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);

        if (_largeIconHandle != 0)
            SendMessage(_windowHandle, WM_SETICON, 1, _largeIconHandle);
        if (_smallIconHandle != 0)
            SendMessage(_windowHandle, WM_SETICON, 0, _smallIconHandle);
    }

    private static void EnsureTrayIconRegistered()
    {
        if (_trayIconVisible || _windowHandle == 0 || _trayIconHandle == 0) return;

        _notifyIconData = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NotifyIconFlags.Message | NotifyIconFlags.Icon | NotifyIconFlags.Tip,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _trayIconHandle,
            szTip = "Utility Gamer"
        };

        if (Shell_NotifyIcon(NotifyCommand.Add, ref _notifyIconData))
        {
            _notifyIconData.uTimeoutOrVersion = 4;
            Shell_NotifyIcon(NotifyCommand.SetVersion, ref _notifyIconData);
            _trayIconVisible = true;
        }
    }

    private static void RemoveTrayIcon()
    {
        if (!_trayIconVisible) return;
        Shell_NotifyIcon(NotifyCommand.Delete, ref _notifyIconData);
        _trayIconVisible = false;
    }

    private static void HideToTray()
    {
        if (_windowHandle == 0) return;
        ShowWindow(_windowHandle, SW_HIDE);
    }

    private static void RestoreFromTray()
    {
        if (_windowHandle == 0) return;
        ShowWindow(_windowHandle, SW_RESTORE);
        MainWindow.Activate();
        SetForegroundWindow(_windowHandle);
    }

    private static Views.MainPage? GetMainPage()
    {
        if (MainWindow?.Content is Views.MainPage page)
            return page;

        return MainWindow?.Content is Microsoft.UI.Xaml.Controls.Frame { Content: Views.MainPage framedPage }
            ? framedPage
            : null;
    }

    private static void ShowTrayMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == 0) return;

        try
        {
            AppendMenu(menu, MF_STRING, TRAY_CMD_OPEN, Translate("Open"));
            AppendMenu(menu, MF_SEPARATOR, 0, string.Empty);

            if (_trayMonitoringActive)
            {
                AppendMenu(menu, MF_STRING | MF_GRAYED, TRAY_CMD_START, Translate("Start monitoring"));
                AppendMenu(menu, MF_STRING, TRAY_CMD_STOP, Translate("Stop monitoring"));
            }
            else
            {
                AppendMenu(menu, MF_STRING, TRAY_CMD_START, Translate("Start monitoring"));
                AppendMenu(menu, MF_STRING | MF_GRAYED, TRAY_CMD_STOP, Translate("Stop monitoring"));
            }

            AppendMenu(menu, MF_SEPARATOR, 0, string.Empty);
            AppendMenu(menu, MF_STRING, TRAY_CMD_EXIT, Translate("Exit"));

            GetCursorPos(out var point);
            SetForegroundWindow(_windowHandle);

            var command = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON,
                point.X, point.Y, 0, _windowHandle, 0);

            if (command == TRAY_CMD_OPEN)
            {
                RestoreFromTray();
            }
            else if (command == TRAY_CMD_START)
            {
                GetMainPage()?.StartWatcherFromTray();
            }
            else if (command == TRAY_CMD_STOP)
            {
                GetMainPage()?.StopWatcherFromTray();
            }
            else if (command == TRAY_CMD_EXIT)
            {
                _isExitRequested = true;
                RemoveTrayIcon();
                MainWindow.Close();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static string Translate(string englishText)
    {
        if (_uiLanguage.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return englishText switch
            {
                "Open" => "Abrir",
                "Start monitoring" => "Iniciar monitoramento",
                "Stop monitoring" => "Parar monitoramento",
                "Exit" => "Sair",
                _ => englishText
            };
        }

        return englishText;
    }

    private static string NormalizeUiLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return "pt-BR";

        if (languageCode.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";

        if (languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase) ||
            languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US";
        }

        return "pt-BR";
    }

    private static void InstallWindowHook()
    {
        if (_windowHandle == 0 || _oldWndProc != 0) return;

        _wndProc = WindowProc;
        _oldWndProc = SetWindowLongPtr(_windowHandle, GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private static nint WindowProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_SIZE:
                if (_minimizeToTrayEnabled && wParam == SIZE_MINIMIZED)
                {
                    HideToTray();
                    return 0;
                }
                break;

            case WM_CLOSE:
                if (_minimizeToTrayEnabled && !_isExitRequested)
                {
                    HideToTray();
                    return 0;
                }
                break;

            case WM_TRAYICON:
                var clickMessage = unchecked((uint)lParam.ToInt64()) & LOWORD_MASK;
                if (clickMessage == WM_LBUTTONUP ||
                    clickMessage == WM_LBUTTONDBLCLK ||
                    clickMessage == NIN_SELECT ||
                    clickMessage == NIN_KEYSELECT)
                {
                    RestoreFromTray();
                    return 0;
                }

                if (clickMessage == WM_RBUTTONUP || clickMessage == WM_CONTEXTMENU)
                {
                    ShowTrayMenu();
                    return 0;
                }
                break;

            case WM_DESTROY:
                RemoveTrayIcon();
                break;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private static AppSettings TryReadSettingsStatic()
    {
        try
        {
            var path = AppSettings.DefaultPath;
            if (!File.Exists(path)) return new AppSettings();
            var json = File.ReadAllText(path, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.Games ??= new();
            settings.Language = NormalizeUiLanguageCode(settings.Language);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static void ApplyWindowPlacement(AppSettings settings)
    {
        var appWindow = MainWindow?.AppWindow;
        if (appWindow is null) return;

        var width = Math.Clamp(settings.WindowWidth, 600, 3840);
        var height = Math.Clamp(settings.WindowHeight, 480, 2160);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

        if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
        {
            var displayArea = DisplayArea.GetFromPoint(
                new Windows.Graphics.PointInt32(settings.WindowLeft, settings.WindowTop),
                DisplayAreaFallback.Nearest);

            var work = displayArea.WorkArea;
            var maxLeft = Math.Max(work.X, work.X + work.Width - width);
            var maxTop = Math.Max(work.Y, work.Y + work.Height - height);
            var safeLeft = Math.Clamp(settings.WindowLeft, work.X, maxLeft);
            var safeTop = Math.Clamp(settings.WindowTop, work.Y, maxTop);
            appWindow.Move(new Windows.Graphics.PointInt32(safeLeft, safeTop));
            return;
        }

        var primaryArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var primaryWork = primaryArea.WorkArea;
        var centerX = primaryWork.X + (primaryWork.Width - width) / 2;
        var centerY = primaryWork.Y + (primaryWork.Height - height) / 2;
        appWindow.Move(new Windows.Graphics.PointInt32(
            Math.Max(primaryWork.X, centerX),
            Math.Max(primaryWork.Y, centerY)));
    }

    private static string? ResolveAssetPath(string fileName)
    {
        foreach (var root in EnumerateAncestors(AppContext.BaseDirectory)
                     .Concat(EnumerateAncestors(Environment.CurrentDirectory)))
        {
            var candidate = Path.Combine(root, fileName);
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(root, "UtilityGamer.WinUI", "Assets", fileName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateAncestors(string start)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (seen.Add(dir.FullName))
                yield return dir.FullName;
            dir = dir.Parent;
        }
    }

    private static string? EnsureIco(string? pngPath, string icoFileName)
    {
        if (string.IsNullOrWhiteSpace(pngPath) || !File.Exists(pngPath)) return null;

        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UtilityGamer",
            "icons-cache");
        Directory.CreateDirectory(cacheDir);

        var icoPath = Path.Combine(cacheDir, icoFileName);
        if (!File.Exists(icoPath) ||
            File.GetLastWriteTimeUtc(icoPath) < File.GetLastWriteTimeUtc(pngPath))
        {
            WriteSinglePngIco(pngPath, icoPath);
        }

        return icoPath;
    }

    private static void WriteSinglePngIco(string pngPath, string icoPath)
    {
        var pngData = File.ReadAllBytes(pngPath);
        if (pngData.Length < 24)
            throw new InvalidDataException($"Invalid PNG file: {pngPath}");

        var width = ReadBigEndianInt32(pngData, 16);
        var height = ReadBigEndianInt32(pngData, 20);

        using var stream = File.Create(icoPath);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // icon type
        writer.Write((ushort)1); // image count

        writer.Write((byte)(width >= 256 ? 0 : width));
        writer.Write((byte)(height >= 256 ? 0 : height));
        writer.Write((byte)0); // color count
        writer.Write((byte)0); // reserved
        writer.Write((ushort)1); // planes
        writer.Write((ushort)32); // bit count
        writer.Write(pngData.Length);
        writer.Write(6 + 16); // image data offset
        writer.Write(pngData);
    }

    private static int ReadBigEndianInt32(byte[] data, int offset)
        => (data[offset] << 24)
         | (data[offset + 1] << 16)
         | (data[offset + 2] << 8)
         | data[offset + 3];

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        CleanupNativeResources();
    }

    private static void CleanupNativeResources()
    {
        ToastHelper.Unregister();
        RemoveTrayIcon();

        if (_windowHandle != 0 && _oldWndProc != 0)
        {
            SetWindowLongPtr(_windowHandle, GWL_WNDPROC, _oldWndProc);
            _oldWndProc = 0;
        }

        _wndProc = null;
        _windowHandle = 0;

        if (_trayIconHandle != 0)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = 0;
        }
        if (_smallIconHandle != 0)
        {
            DestroyIcon(_smallIconHandle);
            _smallIconHandle = 0;
        }
        if (_largeIconHandle != 0)
        {
            DestroyIcon(_largeIconHandle);
            _largeIconHandle = 0;
        }
    }

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [Flags]
    private enum NotifyIconFlags : uint
    {
        Message = 0x00000001,
        Icon = 0x00000002,
        Tip = 0x00000004
    }

    private enum NotifyCommand : uint
    {
        Add = 0x00000000,
        Delete = 0x00000002,
        SetVersion = 0x00000004
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public NotifyIconFlags uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(NotifyCommand message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(
        nint hInst,
        string name,
        uint type,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(nint menu, uint flags, uint itemId, string newItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenu(
        nint menu,
        uint flags,
        int x,
        int y,
        int reserved,
        nint hWnd,
        nint rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW", SetLastError = true)]
    private static extern nint CallWindowProc(nint prevWndProc, nint hWnd, uint msg, nint wParam, nint lParam);

    private static nint SetWindowLongPtr(nint hWnd, int index, nint value)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, index, value)
            : SetWindowLong32(hWnd, index, value.ToInt32());

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint hWnd, int index, nint value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern nint SetWindowLong32(nint hWnd, int index, int value);
}








