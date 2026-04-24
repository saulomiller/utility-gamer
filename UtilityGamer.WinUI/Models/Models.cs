using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace UtilityGamer.WinUI.Models;

public sealed class AppSettings
{
    public List<GameProfile> Games { get; set; } = new();

    public int GameWidth  { get; set; } = 1440;
    public int GameHeight { get; set; } = 1080;
    public int GameHz     { get; set; } = 144;

    public int DefaultWidth  { get; set; } = 1920;
    public int DefaultHeight { get; set; } = 1080;
    public int DefaultHz     { get; set; } = 144;
    public bool DefaultResolutionSwitchEnabled { get; set; } = false;

    public bool   GlobalTimerEnabled   { get; set; } = true;
    public bool   GameBarDisabled      { get; set; } = true;
    public bool   RemoveXboxOverlay    { get; set; }
    public bool   StickyKeysDisabled   { get; set; }
    public bool   MouseAccelDisabled   { get; set; }
    public bool   StartWithWindows     { get; set; }
    public bool   StartMinimizedToTray { get; set; }
    public string Language             { get; set; } = "pt-BR";
    public bool   DarkTheme            { get; set; } = true;
    public bool   NotifyOnResolutionChange { get; set; } = true;
    public bool   NotifyOnPowerPlanChange  { get; set; } = true;

    // Window placement.
    public int WindowWidth  { get; set; } = 960;
    public int WindowHeight { get; set; } = 720;
    public int WindowLeft   { get; set; } = -1;
    public int WindowTop    { get; set; } = -1;

    // UI state.
    public int SelectedTabIndex { get; set; } = 0;
    public string SelectedGameName { get; set; } = string.Empty;

    [JsonIgnore]
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UtilityGamer", "settings.json");
}

public sealed class GameProfile : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private string _name        = string.Empty;
    private string _exePath     = string.Empty;
    private string _processName = string.Empty;

    public string Name
    {
        get => _name;
        set => Set(ref _name, SanitizeText(value));
    }

    public string ExePath
    {
        get => _exePath;
        set => Set(ref _exePath, SanitizePath(value));
    }

    public string ProcessName
    {
        get => _processName;
        set => Set(ref _processName, SanitizeProcessName(value));
    }

    private int _gameWidth;
    private int _gameHeight;
    private int _gameHz;

    public int GameWidth  { get => _gameWidth;  set => Set(ref _gameWidth, value); }
    public int GameHeight { get => _gameHeight; set => Set(ref _gameHeight, value); }
    public int GameHz     { get => _gameHz;     set => Set(ref _gameHz, value); }

    private int _defaultWidth;
    private int _defaultHeight;
    private int _defaultHz;
    private bool _resolutionSwitchEnabled = true;

    public int DefaultWidth  { get => _defaultWidth;  set => Set(ref _defaultWidth, value); }
    public int DefaultHeight { get => _defaultHeight; set => Set(ref _defaultHeight, value); }
    public int DefaultHz     { get => _defaultHz;     set => Set(ref _defaultHz, value); }

    /// <summary>
    /// Enables desktop resolution switching for this game profile.
    /// </summary>
    public bool ResolutionSwitchEnabled
    {
        get => _resolutionSwitchEnabled;
        set => Set(ref _resolutionSwitchEnabled, value);
    }

    private string? _monitorDeviceName;

    /// <summary>
    /// Device name returned by EnumDisplayDevices (for example "\\.\DISPLAY1").
    /// Null means the primary monitor.
    /// </summary>
    public string? MonitorDeviceName
    {
        get => _monitorDeviceName;
        set => Set(ref _monitorDeviceName, SanitizeOptionalText(value));
    }

    private Guid? _powerPlanGuid;

    /// <summary>
    /// Power plan GUID to activate when the game starts.
    /// Null means no power plan change.
    /// </summary>
    public Guid? PowerPlanGuid
    {
        get => _powerPlanGuid;
        set => Set(ref _powerPlanGuid, value);
    }



    private string? _powerPlanDisplayName;

    /// <summary>
    /// Display name assigned by MainPage after loading available plans.
    /// This value is not serialized.
    /// </summary>
    [JsonIgnore]
    public string? PowerPlanDisplayName
    {
        get => _powerPlanDisplayName;
        set => Set(ref _powerPlanDisplayName, SanitizeOptionalText(value));
    }

    [JsonIgnore]
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(Name) ? ProcessName : $"{Name} ({ProcessName})";

    public override string ToString() => DisplayLabel;

    private static string SanitizeText(string? value)
        => value?.Trim() ?? string.Empty;

    private static string? SanitizeOptionalText(string? value)
    {
        var sanitized = value?.Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private static string SanitizePath(string? value)
    {
        var sanitized = SanitizeText(value);
        if (string.IsNullOrWhiteSpace(sanitized))
            return string.Empty;

        try
        {
            return Path.GetFullPath(sanitized);
        }
        catch
        {
            return sanitized;
        }
    }

    private static string SanitizeProcessName(string? value)
    {
        var sanitized = SanitizeText(value);
        if (string.IsNullOrWhiteSpace(sanitized))
            return string.Empty;

        var fileName = Path.GetFileName(sanitized);
        return Path.GetFileNameWithoutExtension(fileName);
    }
}


