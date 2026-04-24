namespace UtilityGamer.WinUI.Helpers;

/// <summary>
/// Watches game processes and applies/restores resolution and power plans.
/// </summary>
public sealed class ProcessWatcher : IDisposable
{
    internal sealed record Dependencies
    {
        public required Func<string, bool> IsProcessRunning { get; init; }
        public required Func<string?> GetPrimaryMonitorName { get; init; }
        public required Func<string, int, int, int, string?> SetResolution { get; init; }
        public required Func<Task<Guid?>> GetActivePowerPlanAsync { get; init; }
        public required Func<Guid, Task<string?>> SetActivePowerPlanAsync { get; init; }
        public required Func<Task<IReadOnlyList<PowerPlanHelper.PowerPlan>>> GetPowerPlansAsync { get; init; }

        public required Action<string, int, int, int> NotifyResolutionChanged { get; init; }
        public required Action<string, int, int, int> NotifyResolutionRestored { get; init; }
        public required Action<string, string> NotifyPowerPlanChanged { get; init; }
        public required Action<string> NotifyPowerPlanRestored { get; init; }
        public required Func<TimerCallback, object?, int, int, IDisposable> CreateTimer { get; init; }

        public static Dependencies CreateDefault() => new()
        {
            IsProcessRunning = IsProcessRunningCore,
            GetPrimaryMonitorName = GetPrimaryMonitorNameCore,
            SetResolution = DisplayHelper.SetResolution,
            GetActivePowerPlanAsync = PowerPlanHelper.GetActivePlanAsync,
            SetActivePowerPlanAsync = PowerPlanHelper.SetActivePlanAsync,
            GetPowerPlansAsync = PowerPlanHelper.GetPlansAsync,

            NotifyResolutionChanged = ToastHelper.NotifyResolutionChanged,
            NotifyResolutionRestored = ToastHelper.NotifyResolutionRestored,
            NotifyPowerPlanChanged = ToastHelper.NotifyPowerPlanChanged,
            NotifyPowerPlanRestored = ToastHelper.NotifyPowerPlanRestored,
            CreateTimer = static (callback, state, dueTime, period) =>
                new Timer(callback, state, dueTime, period)
        };
    }

    public event Action<string>? StatusChanged;

    private readonly List<GameProfile> _profiles = new();
    private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _previousPowerPlan = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _transitionsInProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly Dependencies _dependencies;

    private IDisposable? _timer;
    private bool _disposed;

    private const int PollIntervalMs = 2000;

    public bool IsRunning => _timer is not null;

    public bool NotifyResolution { get; set; } = true;
    public bool NotifyPowerPlan { get; set; } = true;

    public ProcessWatcher()
        : this(Dependencies.CreateDefault())
    {
    }

    internal ProcessWatcher(Dependencies dependencies)
    {
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
    }

    public void Start(IEnumerable<GameProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        Stop();

        lock (_sync)
        {
            _profiles.Clear();
            _profiles.AddRange(profiles.Where(p => !string.IsNullOrWhiteSpace(p.ProcessName)));
            _running.Clear();
            _previousPowerPlan.Clear();
            _transitionsInProgress.Clear();
            _timer = _dependencies.CreateTimer(OnTick, null, 0, PollIntervalMs);
        }

        StatusChanged?.Invoke($"Monitoring started for {_profiles.Count} game(s).");
    }

    public void Stop()
    {
        IDisposable? timer;
        bool wasRunning;

        lock (_sync)
        {
            wasRunning = _timer is not null || _running.Count > 0;
            timer = _timer;
            _timer = null;
            _running.Clear();
            _previousPowerPlan.Clear();
            _transitionsInProgress.Clear();
        }

        timer?.Dispose();

        if (wasRunning)
            StatusChanged?.Invoke("Monitoring stopped.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void OnTick(object? state) => _ = PollOnceAsync(waitForTransitions: false);

    internal Task PollNowAsync() => PollOnceAsync(waitForTransitions: true);

    private async Task PollOnceAsync(bool waitForTransitions)
    {
        List<GameProfile> snapshot;
        lock (_sync)
            snapshot = _profiles.ToList();

        if (snapshot.Count == 0)
            return;

        var transitionTasks = new List<Task>(snapshot.Count);
        foreach (var profile in snapshot)
        {
            var isNowRunning = _dependencies.IsProcessRunning(profile.ProcessName);
            bool wasRunning;
            lock (_sync)
                wasRunning = _running.Contains(profile.ProcessName);

            if (isNowRunning && !wasRunning)
                transitionTasks.Add(OnGameStartedAsync(profile));
            else if (!isNowRunning && wasRunning)
                transitionTasks.Add(OnGameExitedAsync(profile));
        }

        if (waitForTransitions && transitionTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(transitionTasks);
            }
            catch
            {
                // Each transition reports its own status error.
            }
        }
    }

    private bool TryBeginTransition(string processName)
    {
        lock (_sync)
            return _transitionsInProgress.Add(processName);
    }

    private void EndTransition(string processName)
    {
        lock (_sync)
            _transitionsInProgress.Remove(processName);
    }

    private async Task OnGameStartedAsync(GameProfile profile)
    {
        if (!TryBeginTransition(profile.ProcessName))
            return;

        try
        {
            bool started;
            lock (_sync)
                started = _running.Add(profile.ProcessName);

            if (!started)
                return;

            var monitorName = string.IsNullOrWhiteSpace(profile.MonitorDeviceName)
                ? _dependencies.GetPrimaryMonitorName()
                : profile.MonitorDeviceName;

            if (profile.ResolutionSwitchEnabled
                && monitorName is not null
                && profile.GameWidth > 0
                && profile.GameHeight > 0)
            {
                var error = _dependencies.SetResolution(
                    monitorName,
                    profile.GameWidth,
                    profile.GameHeight,
                    profile.GameHz);
                if (error is not null)
                {
                    StatusChanged?.Invoke($"Resolution error for {profile.ProcessName}: {error}");
                }
                else if (NotifyResolution)
                {
                    _dependencies.NotifyResolutionChanged(
                        profile.ProcessName,
                        profile.GameWidth,
                        profile.GameHeight,
                        profile.GameHz);
                }
            }

            if (profile.PowerPlanGuid.HasValue)
            {
                var current = await _dependencies.GetActivePowerPlanAsync();
                if (current.HasValue)
                {
                    lock (_sync)
                        _previousPowerPlan[profile.ProcessName] = current.Value;
                }

                var error = await _dependencies.SetActivePowerPlanAsync(profile.PowerPlanGuid.Value);
                if (error is not null)
                {
                    StatusChanged?.Invoke($"Power plan error for {profile.ProcessName}: {error}");
                }
                else if (NotifyPowerPlan)
                {
                    var plans = await _dependencies.GetPowerPlansAsync();
                    var name = plans.FirstOrDefault(p => p.Guid == profile.PowerPlanGuid.Value)?.Name
                        ?? profile.PowerPlanGuid.Value.ToString();
                    _dependencies.NotifyPowerPlanChanged(profile.ProcessName, name);
                }
            }


            StatusChanged?.Invoke($"{profile.ProcessName} started - profile applied.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Start error for {profile.ProcessName}: {ex.Message}");
        }
        finally
        {
            EndTransition(profile.ProcessName);
        }
    }

    private async Task OnGameExitedAsync(GameProfile profile)
    {
        if (!TryBeginTransition(profile.ProcessName))
            return;

        try
        {
            bool wasRunning;
            Guid previousPlan;
            bool hasPreviousPlan;

            lock (_sync)
            {
                wasRunning = _running.Remove(profile.ProcessName);
                hasPreviousPlan = _previousPowerPlan.TryGetValue(profile.ProcessName, out previousPlan);
                if (hasPreviousPlan)
                    _previousPowerPlan.Remove(profile.ProcessName);
            }

            if (!wasRunning)
                return;



            var monitorName = string.IsNullOrWhiteSpace(profile.MonitorDeviceName)
                ? _dependencies.GetPrimaryMonitorName()
                : profile.MonitorDeviceName;

            if (profile.ResolutionSwitchEnabled
                && monitorName is not null
                && profile.DefaultWidth > 0
                && profile.DefaultHeight > 0)
            {
                var error = _dependencies.SetResolution(
                    monitorName,
                    profile.DefaultWidth,
                    profile.DefaultHeight,
                    profile.DefaultHz);
                if (error is not null)
                {
                    StatusChanged?.Invoke($"Restore resolution error for {profile.ProcessName}: {error}");
                }
                else if (NotifyResolution)
                {
                    _dependencies.NotifyResolutionRestored(
                        profile.ProcessName,
                        profile.DefaultWidth,
                        profile.DefaultHeight,
                        profile.DefaultHz);
                }
            }

            if (hasPreviousPlan)
            {
                var error = await _dependencies.SetActivePowerPlanAsync(previousPlan);
                if (error is not null)
                {
                    StatusChanged?.Invoke($"Restore power plan error for {profile.ProcessName}: {error}");
                }
                else if (NotifyPowerPlan)
                {
                    var plans = await _dependencies.GetPowerPlansAsync();
                    var name = plans.FirstOrDefault(p => p.Guid == previousPlan)?.Name
                        ?? previousPlan.ToString();
                    _dependencies.NotifyPowerPlanRestored(name);
                }
            }

            StatusChanged?.Invoke($"{profile.ProcessName} exited - profile restored.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Exit error for {profile.ProcessName}: {ex.Message}");
        }
        finally
        {
            EndTransition(profile.ProcessName);
        }
    }

    private static bool IsProcessRunningCore(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch
        {
            return false;
        }
    }

    private static string? GetPrimaryMonitorNameCore()
        => DisplayHelper.GetMonitors().FirstOrDefault(m => m.IsPrimary)?.DeviceName;
}
