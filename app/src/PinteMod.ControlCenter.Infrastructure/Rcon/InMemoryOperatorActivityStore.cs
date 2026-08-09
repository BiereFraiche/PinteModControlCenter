using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Infrastructure.Rcon;

public sealed class InMemoryOperatorActivityStore : IOperatorActivityStore
{
    private const int MaximumEvents = 100;
    private readonly object _sync = new();
    private readonly List<LiveEvent> _events = [];

    public IReadOnlyList<LiveEvent> GetSnapshot()
    {
        lock (_sync)
        {
            return _events.OrderByDescending(item => item.OccurredAt).ToArray();
        }
    }

    public void RecordRconResult(RconExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var title = result.Command switch
        {
            RconDiagnosticCommand.HealthFull => "Diagnostic Health RCON",
            RconDiagnosticCommand.PauseStatus => "Diagnostic Pause RCON",
            RconDiagnosticCommand.MapInfo => "Diagnostic Carte RCON",
            RconDiagnosticCommand.PowerStatus => "Diagnostic Courant RCON",
            RconDiagnosticCommand.PackAPunchStatus => "Diagnostic Pack-a-Punch RCON",
            RconDiagnosticCommand.RoundStatus => "Diagnostic Manche RCON",
            RconDiagnosticCommand.Players => "Diagnostic Joueurs RCON",
            _ => "Diagnostic RCON"
        };
        var details = LogPrivacyFilter.SanitizeDisplayText(
            $"{result.Status} · Commande envoyée : {(result.CommandSent ? "Oui" : "Non")} · {result.DisplayResponse}",
            500);
        var severity = result.Status switch
        {
            RconExecutionStatus.Success => EventSeverity.Success,
            RconExecutionStatus.Timeout or RconExecutionStatus.EmptyResponse or RconExecutionStatus.UnexpectedResponse => EventSeverity.Warning,
            _ => EventSeverity.Danger
        };
        var item = new LiveEvent(
            result.CompletedAtUtc,
            "RCON",
            title,
            details,
            severity)
        {
            Provenance = DataProvenance.Unavailable,
            SourceLabel = "RCON opérateur"
        };

        lock (_sync)
        {
            _events.Add(item);
            if (_events.Count > MaximumEvents)
            {
                _events.RemoveRange(0, _events.Count - MaximumEvents);
            }
        }
    }

    public void RecordPauseResult(CommunityPauseExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var action = result.Action == CommunityPauseAction.Pause ? "Pause" : "Reprise";
        var details = LogPrivacyFilter.SanitizeDisplayText(
            $"{result.Status} · Commande envoyée : {(result.CommandSent ? "Oui" : "Non")} · " +
            $"Vérification demandée : {(result.StatusRefreshRequested ? "Oui" : "Non")} · {result.DisplayMessage}",
            500);
        var severity = result.Status switch
        {
            CommunityPauseExecutionStatus.SentAwaitingObservation => EventSeverity.Warning,
            CommunityPauseExecutionStatus.DeliveryUnknown => EventSeverity.Warning,
            _ => EventSeverity.Danger
        };
        var item = new LiveEvent(
            result.CompletedAtUtc,
            "RCON",
            $"Commande Community Pause · {action}",
            details,
            severity)
        {
            Provenance = DataProvenance.Unavailable,
            SourceLabel = "RCON opérateur"
        };

        lock (_sync)
        {
            _events.Add(item);
            if (_events.Count > MaximumEvents)
            {
                _events.RemoveRange(0, _events.Count - MaximumEvents);
            }
        }
    }

    public void RecordServerAdministrationResult(ServerAdministrationExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var action = result.Request.Action switch
        {
            ServerAdministrationAction.NextRound => "Terminer la manche",
            ServerAdministrationAction.SetRound => $"Définir la manche {result.Request.TargetRound}",
            ServerAdministrationAction.EnablePower => "Activer le courant",
            ServerAdministrationAction.EnablePackAPunch => "Activer Pack-a-Punch",
            ServerAdministrationAction.PlayMapMusic => "Lancer la musique de carte",
            ServerAdministrationAction.StopMapMusic => "Arrêter la musique de carte",
            ServerAdministrationAction.UnlockStandardPassages => "Déverrouiller les passages standard",
            ServerAdministrationAction.KeepLastZombie => "Garder un zombie",
            ServerAdministrationAction.KillAllZombies => "Éliminer les zombies",
            ServerAdministrationAction.MakePowerUpsPermanent => "Rendre les power-ups permanents",
            ServerAdministrationAction.RestorePowerUpTimeout => "Restaurer le délai des power-ups",
            _ => "Action serveur"
        };
        var details = LogPrivacyFilter.SanitizeDisplayText(
            $"{result.Status} · Commande envoyée : {(result.CommandSent ? "Oui" : "Non")} · {result.DisplayMessage}",
            750);
        var severity = result.CommandSent ? EventSeverity.Warning : EventSeverity.Danger;
        var item = new LiveEvent(
            result.CompletedAtUtc,
            "RCON",
            $"Administration serveur · {action}",
            details,
            severity)
        {
            Provenance = DataProvenance.Unavailable,
            SourceLabel = "RCON opérateur"
        };

        lock (_sync)
        {
            _events.Add(item);
            if (_events.Count > MaximumEvents)
            {
                _events.RemoveRange(0, _events.Count - MaximumEvents);
            }
        }
    }

    public void RecordPlayerAdministrationResult(PlayerAdministrationExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var action = result.Request.Action switch
        {
            PlayerAdministrationAction.Revive => "Revive",
            PlayerAdministrationAction.Respawn => "Respawn",
            PlayerAdministrationAction.GrantPoints => "Points",
            PlayerAdministrationAction.RefillAmmo => "Munitions",
            PlayerAdministrationAction.ToggleGodMode => "Godmode",
            PlayerAdministrationAction.GiveWeapon => "Arme",
            PlayerAdministrationAction.GivePerk => "Atout",
            PlayerAdministrationAction.GiveAllPerks => "Tous les atouts",
            PlayerAdministrationAction.TeleportToOwnAim => "Téléportation au viseur",
            PlayerAdministrationAction.Mute => "Mute",
            PlayerAdministrationAction.Unmute => "Unmute",
            PlayerAdministrationAction.Kick => "Kick",
            PlayerAdministrationAction.Ban => "Ban",
            PlayerAdministrationAction.SetRole => "Définir le rôle",
            PlayerAdministrationAction.RemoveRole => "Retirer le rôle",
            _ => "Action joueur"
        };
        var details = LogPrivacyFilter.SanitizeDisplayText(
            $"{result.Status} · Cible XUID neutralisée · Commande envoyée : {(result.CommandSent ? "Oui" : "Non")} · {result.DisplayMessage}",
            750);
        var item = new LiveEvent(
            result.CompletedAtUtc,
            "RCON",
            $"Administration joueur · {action}",
            details,
            result.CommandSent ? EventSeverity.Warning : EventSeverity.Danger)
        {
            Provenance = DataProvenance.Unavailable,
            SourceLabel = "RCON opérateur"
        };

        lock (_sync)
        {
            _events.Add(item);
            if (_events.Count > MaximumEvents)
            {
                _events.RemoveRange(0, _events.Count - MaximumEvents);
            }
        }
    }
}
