using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UtilityGamer.WinUI.Helpers;

/// <summary>
/// Enumerates Windows power plans and activates them via powercfg.
/// </summary>
public static class PowerPlanHelper
{
    public sealed record PowerPlan(Guid Guid, string Name);

    private static readonly string PowerCfgPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "powercfg.exe");

    public static async Task<IReadOnlyList<PowerPlan>> GetPlansAsync()
    {
        var result = await RunPowerCfgAsync("/list");
        if (result.ExitCode != 0)
            return Array.Empty<PowerPlan>();

        return ParsePlans(result.Output);
    }

    public static async Task<Guid?> GetActivePlanAsync()
    {
        var result = await RunPowerCfgAsync("/getactivescheme");
        if (result.ExitCode != 0)
            return null;

        var match = Regex.Match(
            result.Output,
            @"([0-9a-fA-F\-]{36})",
            RegexOptions.IgnoreCase);

        return match.Success && Guid.TryParse(match.Groups[1].Value, out var guid)
            ? guid
            : null;
    }

    public static async Task<string?> SetActivePlanAsync(Guid planGuid)
    {
        var result = await RunPowerCfgAsync($"/setactive {planGuid:D}");
        if (result.ExitCode == 0)
            return null;

        return string.IsNullOrWhiteSpace(result.Output)
            ? "powercfg failed to activate the requested plan."
            : result.Output;
    }

    private static IReadOnlyList<PowerPlan> ParsePlans(string output)
    {
        var plans = new List<PowerPlan>();
        var matches = Regex.Matches(
            output,
            @"([0-9a-fA-F\-]{36})\s*\(([^)]+)\)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            if (Guid.TryParse(match.Groups[1].Value, out var guid))
                plans.Add(new PowerPlan(guid, match.Groups[2].Value.Trim()));
        }

        return plans;
    }

    private static async Task<CommandResult> RunPowerCfgAsync(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = PowerCfgPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (!process.Start())
            return new CommandResult(-1, "Failed to start powercfg.");

        var output = await ProcessOutputHelper.ReadCombinedOutputAsync(process);
        return new CommandResult(process.ExitCode, output);
    }

    private readonly record struct CommandResult(int ExitCode, string Output);
}

