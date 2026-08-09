using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Rcon;

internal static class RconDiagnosticResponseValidator
{
    private static readonly string[] HealthMarkers =
    [
        "[PinteMod Health]",
        "PASS=",
        "WARNING=",
        "ERROR="
    ];

    private static readonly string[] PauseMarkers =
    [
        "PINTEMOD COMMUNITY PAUSE",
        "EXPERIMENTAL v0.3",
        "Active:",
        "Successful pauses:"
    ];

    private static readonly string[] MapMarkers =
    [
        "PinteMod MAP INFO v0.11.0",
        "Map:",
        "Pack-a-Punch triggers:",
        "Profile power:",
        "Profile PaP:"
    ];

    private static readonly string[] PackAPunchMarkers =
    [
        "PinteMod PACK-A-PUNCH",
        "Map:",
        "Access profile:",
        "Pack-a-Punch triggers:",
        "Powered machines:"
    ];

    private static readonly string[] RoundMarkers =
    [
        "Current round:",
        "Living AI:"
    ];

    public static bool IsExpected(RconDiagnosticCommand command, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        return command switch
        {
            RconDiagnosticCommand.HealthFull => ContainsAll(response, HealthMarkers),
            RconDiagnosticCommand.PauseStatus => ContainsAll(response, PauseMarkers),
            RconDiagnosticCommand.MapInfo => ContainsAll(response, MapMarkers),
            RconDiagnosticCommand.PowerStatus =>
                response.Contains("[PinteMod]", StringComparison.OrdinalIgnoreCase) &&
                response.Contains("Profile:", StringComparison.OrdinalIgnoreCase) &&
                ContainsAny(
                    response,
                    "Global power flag is ON",
                    "Global power flag is OFF",
                    "Global power flag is unavailable",
                    "Power is not applicable on this map"),
            RconDiagnosticCommand.PackAPunchStatus => ContainsAll(response, PackAPunchMarkers),
            RconDiagnosticCommand.RoundStatus => ContainsAll(response, RoundMarkers),
            RconDiagnosticCommand.Players => ContainsAny(
                response,
                "Connected players:",
                "No connected player"),
            RconDiagnosticCommand.MapAudit => ContainsAll(
                response,
                ["PinteMod Map Audit", "Map", "Profile", "Power", "Pack-a-Punch", "Events", "Bosses"]),
            RconDiagnosticCommand.EventStatus => ContainsAll(
                response,
                ["PINTEMOD EVENTS", "Enabled:", "Map:", "Backend:"]),
            RconDiagnosticCommand.PowerUpCatalog => ContainsAll(
                response,
                ["PinteMod POWERUPS", "maxammo", "instakill", "doublepoints"]),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Commande de diagnostic non autorisée.")
        };
    }

    private static bool ContainsAll(string response, IEnumerable<string> markers) =>
        markers.All(marker => response.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string response, params string[] markers) =>
        markers.Any(marker => response.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
