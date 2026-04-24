using UtilityGamer.WinUI.Helpers;
using UtilityGamer.WinUI.Models;

namespace UtilityGamer.Tests;

[TestClass]
public sealed class ProcessWatcherTests
{
    [TestMethod]
    public async Task PollNowAsync_AppliesStartFlowOnlyOnce_WhenCalledConcurrently()
    {
        var state = new FakeWatcherState();
        var dependencies = BuildDependencies(state);
        using var watcher = new ProcessWatcher(dependencies);

        watcher.Start([CreateProfile(state)]);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => watcher.PollNowAsync()));

        Assert.AreEqual(1, state.SetResolutionCalls.Count);
        Assert.AreEqual(1, state.SetActivePowerPlanCalls.Count);
        Assert.AreEqual(state.TargetPlan, state.SetActivePowerPlanCalls.Single());
    }

    [TestMethod]
    public async Task PollNowAsync_RestoresProfileOnlyOnce_WhenCalledConcurrentlyDuringExit()
    {
        var state = new FakeWatcherState();
        var dependencies = BuildDependencies(state);
        using var watcher = new ProcessWatcher(dependencies);

        watcher.Start([CreateProfile(state)]);
        await watcher.PollNowAsync();

        Volatile.Write(ref state.IsProcessRunningFlag, 0);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => watcher.PollNowAsync()));

        Assert.AreEqual(2, state.SetResolutionCalls.Count);
        Assert.AreEqual((1920, 1080, 144), state.SetResolutionCalls[0]);
        Assert.AreEqual((1280, 720, 60), state.SetResolutionCalls[1]);

        Assert.AreEqual(2, state.SetActivePowerPlanCalls.Count);
        Assert.AreEqual(state.TargetPlan, state.SetActivePowerPlanCalls[0]);
        Assert.AreEqual(state.PreviousPlan, state.SetActivePowerPlanCalls[1]);
    }

    [TestMethod]
    public async Task PollNowAsync_SkipsResolutionChanges_WhenResolutionSwitchIsDisabled()
    {
        var state = new FakeWatcherState();
        var dependencies = BuildDependencies(state);
        using var watcher = new ProcessWatcher(dependencies);

        var profile = CreateProfile(state);
        profile.ResolutionSwitchEnabled = false;

        watcher.Start([profile]);
        await watcher.PollNowAsync();

        Assert.AreEqual(0, state.SetResolutionCalls.Count);
        Assert.AreEqual(1, state.SetActivePowerPlanCalls.Count);
        Assert.AreEqual(state.TargetPlan, state.SetActivePowerPlanCalls.Single());
    }

    [TestMethod]
    public async Task PollNowAsync_SkipsResolutionRestore_WhenResolutionSwitchIsDisabled()
    {
        var state = new FakeWatcherState();
        var dependencies = BuildDependencies(state);
        using var watcher = new ProcessWatcher(dependencies);

        var profile = CreateProfile(state);
        profile.ResolutionSwitchEnabled = false;

        watcher.Start([profile]);
        await watcher.PollNowAsync();

        Volatile.Write(ref state.IsProcessRunningFlag, 0);
        await watcher.PollNowAsync();

        Assert.AreEqual(0, state.SetResolutionCalls.Count);
        Assert.AreEqual(2, state.SetActivePowerPlanCalls.Count);
        Assert.AreEqual(state.TargetPlan, state.SetActivePowerPlanCalls[0]);
        Assert.AreEqual(state.PreviousPlan, state.SetActivePowerPlanCalls[1]);
    }

    [TestMethod]
    public void GameProfile_SanitizesWhitespaceAndExtensions()
    {
        var profile = new GameProfile
        {
            Name = "  Test Game  ",
            ExePath = "  C:\\Games\\TestGame\\testgame.exe  ",
            ProcessName = "  C:\\Games\\TestGame\\testgame.exe  ",
            MonitorDeviceName = "  \\\\.\\DISPLAY1  ",
            PowerPlanDisplayName = "  High Performance  "
        };

        Assert.AreEqual("Test Game", profile.Name);
        Assert.AreEqual(Path.GetFullPath("C:\\Games\\TestGame\\testgame.exe"), profile.ExePath);
        Assert.AreEqual("testgame", profile.ProcessName);
        Assert.AreEqual(@"\\.\DISPLAY1", profile.MonitorDeviceName);
        Assert.AreEqual("High Performance", profile.PowerPlanDisplayName);
    }

    [TestMethod]
    public void GameProfile_CollapsesBlankOptionalTextFields()
    {
        var profile = new GameProfile
        {
            Name = "   ",
            ProcessName = "  testgame.exe  ",
            MonitorDeviceName = "   ",
            PowerPlanDisplayName = "   "
        };

        Assert.AreEqual(string.Empty, profile.Name);
        Assert.AreEqual("testgame", profile.ProcessName);
        Assert.IsNull(profile.MonitorDeviceName);
        Assert.IsNull(profile.PowerPlanDisplayName);
        Assert.AreEqual("testgame", profile.DisplayLabel);
    }

    private static ProcessWatcher.Dependencies BuildDependencies(FakeWatcherState state) => new()
    {
        IsProcessRunning = _ => Volatile.Read(ref state.IsProcessRunningFlag) == 1,
        GetPrimaryMonitorName = () => @"\\.\DISPLAY1",
        SetResolution = (_, width, height, hz) =>
        {
            lock (state.Sync)
                state.SetResolutionCalls.Add((width, height, hz));
            return null;
        },
        GetActivePowerPlanAsync = () => Task.FromResult<Guid?>(state.PreviousPlan),
        SetActivePowerPlanAsync = guid =>
        {
            lock (state.Sync)
                state.SetActivePowerPlanCalls.Add(guid);
            return Task.FromResult<string?>(null);
        },
        GetPowerPlansAsync = () => Task.FromResult<IReadOnlyList<PowerPlanHelper.PowerPlan>>(
        [
            new PowerPlanHelper.PowerPlan(state.TargetPlan, "Target plan"),
            new PowerPlanHelper.PowerPlan(state.PreviousPlan, "Previous plan")
        ]),
        NotifyResolutionChanged = (_, _, _, _) => { },
        NotifyResolutionRestored = (_, _, _, _) => { },
        NotifyPowerPlanChanged = (_, _) => { },
        NotifyPowerPlanRestored = _ => { },
        CreateTimer = static (_, _, _, _) => new NoopTimer()
    };

    private static GameProfile CreateProfile(FakeWatcherState state) => new()
    {
        Name = "Test Game",
        ProcessName = "testgame",
        ExePath = @"C:\Games\TestGame\testgame.exe",
        MonitorDeviceName = @"\\.\DISPLAY1",
        GameWidth = 1920,
        GameHeight = 1080,
        GameHz = 144,
        DefaultWidth = 1280,
        DefaultHeight = 720,
        DefaultHz = 60,
        PowerPlanGuid = state.TargetPlan
    };

    private sealed class FakeWatcherState
    {
        public readonly object Sync = new();
        public readonly List<(int Width, int Height, int Hz)> SetResolutionCalls = [];
        public readonly List<Guid> SetActivePowerPlanCalls = [];
        public readonly Guid PreviousPlan = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public readonly Guid TargetPlan = Guid.Parse("22222222-2222-2222-2222-222222222222");

        public int IsProcessRunningFlag = 1;
    }

    private sealed class NoopTimer : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
