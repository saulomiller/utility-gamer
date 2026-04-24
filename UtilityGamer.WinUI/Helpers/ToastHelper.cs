using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace UtilityGamer.WinUI.Helpers;

/// <summary>
/// Sends Windows toast notifications for resolution and power plan changes.
/// </summary>
public static class ToastHelper
{
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered) return;
        try
        {
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch
        {
            _registered = false;
        }
    }

    public static void Unregister()
    {
        if (!_registered) return;

        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch
        {
            // Notifications are non-critical.
        }
        finally
        {
            _registered = false;
        }
    }

    public static void NotifyResolutionChanged(string processName, int width, int height, int hz)
    {
        if (!_registered) return;
        Send(
            $"Resolution changed for {processName}",
            $"{width}x{height} @ {hz}Hz applied",
            "Display");
    }

    public static void NotifyResolutionRestored(string processName, int width, int height, int hz)
    {
        if (!_registered) return;
        Send(
            "Resolution restored",
            $"Back to {width}x{height} @ {hz}Hz after {processName} exited",
            "Display");
    }

    public static void NotifyPowerPlanChanged(string processName, string planName)
    {
        if (!_registered) return;
        Send(
            $"Power plan changed for {processName}",
            planName,
            "Power");
    }

    public static void NotifyPowerPlanRestored(string previousPlanName)
    {
        if (!_registered) return;
        Send(
            "Power plan restored",
            previousPlanName,
            "Power");
    }

    private static void Send(string title, string body, string tag)
    {
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .SetTag(tag)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            // Notifications are non-critical.
        }
    }
}
