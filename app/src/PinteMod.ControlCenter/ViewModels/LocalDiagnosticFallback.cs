using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.ViewModels;

internal sealed record LocalDiagnosticFallbackResult(
    string Status,
    string Message,
    ServiceHealth Health);

internal static class LocalDiagnosticFallback
{
    private const string RuntimePrefix =
        "Commande RCON envoyée · BOIII n’a pas transporté la sortie console · état local autoritaire affiché.";

    public static bool TryCreate(
        RconDiagnosticCommand command,
        DashboardSnapshot snapshot,
        out LocalDiagnosticFallbackResult result,
        bool rconDeliveryUnconfirmed = false)
    {
        if (command == RconDiagnosticCommand.PauseStatus && TryPause(snapshot, rconDeliveryUnconfirmed, out result))
        {
            return true;
        }

        if (command == RconDiagnosticCommand.HealthFull && TryHealth(snapshot, rconDeliveryUnconfirmed, out result))
        {
            return true;
        }

        if (command is RconDiagnosticCommand.MapAudit or
            RconDiagnosticCommand.EventStatus or
            RconDiagnosticCommand.PowerUpCatalog)
        {
            result = new(
                "SORTIE CONSOLE NON TRANSPORTÉE",
                "Commande exécutée, sortie console non transportée · aucun contrat local autoritaire ne permet d’afficher ces détails.",
                ServiceHealth.Warning);
            return true;
        }

        if (!TryGetAuthoritativeRuntime(snapshot, out var runtime))
        {
            result = default!;
            return false;
        }

        var details = command switch
        {
            RconDiagnosticCommand.MapInfo =>
                $"Carte : {OfficialMapNameResolver.Resolve(runtime.MapCode)} ({runtime.MapCode}) · session locale cohérente.",
            RconDiagnosticCommand.PowerStatus =>
                $"Courant : {PowerState(runtime.PowerState)}.",
            RconDiagnosticCommand.PackAPunchStatus =>
                $"Pack-a-Punch de la carte : {PackAPunchState(runtime.PackAPunchState)}.",
            RconDiagnosticCommand.RoundStatus =>
                runtime.Round is { } round ? $"Manche observée : {round}." : null,
            RconDiagnosticCommand.Players => FormatPlayers(runtime),
            _ => null
        };
        if (details is null)
        {
            result = default!;
            return false;
        }

        result = new(
            "ÉTAT LOCAL AUTORITAIRE",
            $"{Prefix(rconDeliveryUnconfirmed)}{Environment.NewLine}{details}",
            ServiceHealth.Healthy);
        return true;
    }

    private static bool TryGetAuthoritativeRuntime(
        DashboardSnapshot snapshot,
        out ControlCenterRuntimeSnapshot runtime)
    {
        var observation = snapshot.LocalObservation.RuntimeSnapshot;
        if (snapshot.DataContext.Mode != ControlCenterDataMode.HybridLocal ||
            observation.Value is not { } value ||
            observation.Metadata.ReadStatus != LocalReadStatus.Success ||
            observation.Metadata.Freshness != DataFreshness.Fresh ||
            observation.Metadata.Provenance != DataProvenance.LocalFile ||
            snapshot.Server.SessionProvenance != DataProvenance.LocalFile ||
            !string.Equals(value.SessionId, snapshot.Server.SessionId, StringComparison.Ordinal) ||
            !string.Equals(value.MapCode, snapshot.Server.MapCode, StringComparison.OrdinalIgnoreCase))
        {
            runtime = null!;
            return false;
        }

        runtime = value;
        return true;
    }

    private static bool TryPause(
        DashboardSnapshot snapshot,
        bool rconDeliveryUnconfirmed,
        out LocalDiagnosticFallbackResult result)
    {
        var observation = snapshot.LocalObservation.CommunityPause;
        if (observation.Value is not { } pause ||
            observation.Metadata.ReadStatus != LocalReadStatus.Success ||
            observation.Metadata.Freshness != DataFreshness.Fresh ||
            observation.Metadata.Provenance != DataProvenance.LocalFile)
        {
            result = default!;
            return false;
        }

        result = new(
            "STATUT LOCAL COMMUNITY PAUSE",
            $"{Prefix(rconDeliveryUnconfirmed)} · feedback local spécialisé affiché.{Environment.NewLine}" +
            (pause.Active ? "Partie en pause." : "Partie non pausée."),
            ServiceHealth.Healthy);
        return true;
    }

    private static bool TryHealth(
        DashboardSnapshot snapshot,
        bool rconDeliveryUnconfirmed,
        out LocalDiagnosticFallbackResult result)
    {
        var services = snapshot.Services
            .Where(service => service.Source.ReadStatus == LocalReadStatus.Success &&
                              service.Source.Freshness == DataFreshness.Fresh &&
                              service.Source.Provenance == DataProvenance.LocalFile)
            .Select(service => $"{service.Name} : {Health(service.Health)}")
            .ToArray();
        if (services.Length == 0)
        {
            result = default!;
            return false;
        }

        result = new(
            rconDeliveryUnconfirmed ? "RÉSUMÉ LOCAL · RCON À VÉRIFIER" : "RÉSUMÉ LOCAL · PAS LE HEALTH FULL",
            (rconDeliveryUnconfirmed
                ? "Aucune réponse RCON reçue, mais les données PinteMod locales sont fraîches. Vérifiez le port RCON avant toute action d’administration. "
                : "Commande RCON envoyée · BOIII n’a pas transporté les 51 contrôles de la console. ") +
            $"Résumé des services locaux observables uniquement : {string.Join(" · ", services)}.",
            ServiceHealth.Warning);
        return true;
    }

    private static string Prefix(bool rconDeliveryUnconfirmed) => rconDeliveryUnconfirmed
        ? "Tentative RCON émise, sans réponse BOIII ; état local autoritaire affiché."
        : RuntimePrefix;

    private static string FormatPlayers(ControlCenterRuntimeSnapshot runtime)
    {
        var maximum = runtime.MaximumPlayers is { } max ? $" / {max}" : string.Empty;
        var players = runtime.Players.Count == 0
            ? "Aucun joueur détaillé dans le snapshot."
            : string.Join(Environment.NewLine, runtime.Players.Select(player =>
                $"• {LogPrivacyFilter.SafePlayerName(player.DisplayName)} · client {player.ClientNumber} · {LifeState(player.LifeState)}"));
        return $"Joueurs connectés : {runtime.ConnectedPlayers}{maximum}.{Environment.NewLine}{players}";
    }

    private static string PowerState(RuntimePowerState state) => state switch
    {
        RuntimePowerState.On => "ACTIF",
        RuntimePowerState.Off => "INACTIF",
        RuntimePowerState.NotApplicable => "NON APPLICABLE",
        _ => "INCONNU"
    };

    private static string PackAPunchState(RuntimePackAPunchState state) => state switch
    {
        RuntimePackAPunchState.Available => "DISPONIBLE",
        RuntimePackAPunchState.Unavailable => "INDISPONIBLE",
        RuntimePackAPunchState.NotApplicable => "NON APPLICABLE",
        _ => "INCONNU"
    };

    private static string LifeState(PlayerLifeState state) => state switch
    {
        PlayerLifeState.Alive => "en vie",
        PlayerLifeState.Downed => "à terre",
        PlayerLifeState.Dead => "mort",
        PlayerLifeState.Spectator => "spectateur",
        _ => "état inconnu"
    };

    private static string Health(ServiceHealth health) => health switch
    {
        ServiceHealth.Healthy => "SAIN",
        ServiceHealth.Warning => "AVERTISSEMENT",
        ServiceHealth.Offline => "HORS LIGNE",
        ServiceHealth.Error => "ERREUR",
        _ => "INCONNU"
    };
}
