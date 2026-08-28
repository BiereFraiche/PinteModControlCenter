using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Configuration;

public sealed record ApplicationStartupOptions(
    ControlCenterDataMode DataMode,
    string? ServerRoot,
    SimulationScenario Scenario)
{
    public static ApplicationStartupOptions Resolve(
        IReadOnlyList<string> arguments,
        OperatorConfiguration savedConfiguration,
        bool forceSavedDataSource = false)
    {
        ArgumentNullException.ThrowIfNull(savedConfiguration);
        var parsed = Parse(arguments);
        var hasExplicitDataSelection = arguments.Any(argument =>
            argument.StartsWith("--data-mode=", StringComparison.OrdinalIgnoreCase) ||
            argument.StartsWith("--server-root=", StringComparison.OrdinalIgnoreCase));
        return !hasExplicitDataSelection &&
               (forceSavedDataSource || savedConfiguration.ActivateDataSourceOnStartup) &&
               !string.IsNullOrWhiteSpace(savedConfiguration.ServerRoot)
            ? parsed with
            {
                DataMode = ControlCenterDataMode.HybridLocal,
                ServerRoot = savedConfiguration.ServerRoot
            }
            : parsed;
    }

    public static ApplicationStartupOptions Parse(IReadOnlyList<string> arguments)
    {
        var dataModeValue = GetSingleArgument(arguments, "--data-mode=");
        var serverRoot = GetSingleArgument(arguments, "--server-root=");
        var dataMode = dataModeValue?.ToLowerInvariant() switch
        {
            null or "simulation" => ControlCenterDataMode.Simulation,
            "hybrid-local" => ControlCenterDataMode.HybridLocal,
            _ => throw new ArgumentException($"Mode de données inconnu : {dataModeValue}.")
        };

        if (dataMode == ControlCenterDataMode.HybridLocal && string.IsNullOrWhiteSpace(serverRoot))
        {
            throw new ArgumentException(
                "Le mode hybride local exige --server-root=<chemin absolu>.");
        }

        if (dataMode == ControlCenterDataMode.Simulation && serverRoot is not null)
        {
            throw new ArgumentException(
                "--server-root est accepté uniquement avec --data-mode=hybrid-local.");
        }

        return new ApplicationStartupOptions(dataMode, serverRoot, ParseScenario(arguments));
    }

    private static string? GetSingleArgument(IReadOnlyList<string> arguments, string prefix)
    {
        var matches = arguments
            .Where(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length > 1)
        {
            throw new ArgumentException($"L’argument {prefix.TrimEnd('=')} ne peut être fourni qu’une fois.");
        }

        return matches.Length == 0 ? null : matches[0][prefix.Length..];
    }

    private static SimulationScenario ParseScenario(IReadOnlyList<string> arguments)
    {
        var value = arguments
            .FirstOrDefault(argument => argument.StartsWith("--scenario=", StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1];

        return value?.ToLowerInvariant() switch
        {
            "warning" => SimulationScenario.Warning,
            "offline" => SimulationScenario.Offline,
            "stopped" => SimulationScenario.ServerStopped,
            "empty" => SimulationScenario.Empty,
            _ => SimulationScenario.Healthy
        };
    }
}
