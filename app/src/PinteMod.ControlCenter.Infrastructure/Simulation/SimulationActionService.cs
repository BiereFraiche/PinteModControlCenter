using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Security;
using PinteMod.ControlCenter.Core.Simulation;

namespace PinteMod.ControlCenter.Infrastructure.Simulation;

public sealed class SimulationActionService : ISimulationActionService
{
    private static readonly HashSet<SimulationAction> PlayerActions =
    [
        SimulationAction.RevivePlayer,
        SimulationAction.RespawnPlayer,
        SimulationAction.GrantPoints,
        SimulationAction.RefillAmmo,
        SimulationAction.GiveWeapon,
        SimulationAction.GivePerk,
        SimulationAction.GiveAllPerks,
        SimulationAction.GivePowerUpPlayer,
        SimulationAction.TeleportPlayer,
        SimulationAction.ToggleGodmode,
        SimulationAction.MutePlayer,
        SimulationAction.UnmutePlayer,
        SimulationAction.KickPlayer,
        SimulationAction.BanPlayer,
        SimulationAction.ChangeRole,
        SimulationAction.RemoveRole,
        SimulationAction.ViewHistory
    ];

    private static readonly IReadOnlyDictionary<SimulationAction, string> Labels =
        new Dictionary<SimulationAction, string>
        {
            [SimulationAction.RevivePlayer] = "Réanimation",
            [SimulationAction.RespawnPlayer] = "Respawn",
            [SimulationAction.GrantPoints] = "Attribution de points",
            [SimulationAction.RefillAmmo] = "Recharge des munitions",
            [SimulationAction.GiveWeapon] = "Attribution d'arme",
            [SimulationAction.GivePerk] = "Attribution d'atout",
            [SimulationAction.GiveAllPerks] = "Attribution de tous les atouts",
            [SimulationAction.GivePowerUpPlayer] = "Apparition d’un power-up au viseur du joueur",
            [SimulationAction.TeleportPlayer] = "Téléportation",
            [SimulationAction.ToggleGodmode] = "Godmode",
            [SimulationAction.MutePlayer] = "Mute",
            [SimulationAction.UnmutePlayer] = "Unmute",
            [SimulationAction.KickPlayer] = "Kick",
            [SimulationAction.BanPlayer] = "Ban",
            [SimulationAction.ChangeRole] = "Changement de rôle",
            [SimulationAction.RemoveRole] = "Retrait du rôle",
            [SimulationAction.ViewHistory] = "Consultation de l'historique",
            [SimulationAction.ChangeMap] = "Changement de carte",
            [SimulationAction.RestartMap] = "Redémarrage de carte",
            [SimulationAction.SetRound] = "Définition de la manche",
            [SimulationAction.TogglePower] = "Activation du courant",
            [SimulationAction.EnablePackAPunch] = "Activation du Pack-a-Punch",
            [SimulationAction.PlayMusic] = "Lecture de musique",
            [SimulationAction.TriggerEvent] = "Déclenchement d'événement",
            [SimulationAction.SpawnBoss] = "Apparition d'un boss",
            [SimulationAction.SpawnPowerUp] = "Apparition d'un power-up",
            [SimulationAction.RunDiagnostics] = "Diagnostics"
        };

    public Task<SimulationResult> SimulateAsync(
        SimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Labels.TryGetValue(request.Action, out var actionLabel))
        {
            return Task.FromResult(Rejected(request, "Action absente de la liste blanche."));
        }

        if (PlayerActions.Contains(request.Action) && !XuidValidator.IsValid(request.TargetXuid))
        {
            return Task.FromResult(Rejected(request, "XUID cible absent ou invalide."));
        }

        if (request.OptionKey is { Length: > 64 } || request.OptionKey?.Any(char.IsControl) == true)
        {
            return Task.FromResult(Rejected(request, "Option simulée invalide."));
        }

        var target = request.TargetXuid is null
            ? string.Empty
            : $" · cible {XuidValidator.Abbreviate(request.TargetXuid)}";
        var option = string.IsNullOrWhiteSpace(request.OptionKey)
            ? string.Empty
            : $" · option {request.OptionKey}";

        var result = new SimulationResult(
            SimulationStatus.Simulated,
            $"Simulation uniquement — {actionLabel}{target}{option}. Aucune commande envoyée.",
            request.Action,
            request.TargetXuid,
            request.OptionKey,
            false,
            DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }

    private static SimulationResult Rejected(SimulationRequest request, string message) =>
        new(
            SimulationStatus.Rejected,
            $"Simulation refusée — {message} Aucune commande envoyée.",
            request.Action,
            request.TargetXuid,
            request.OptionKey,
            false,
            DateTimeOffset.UtcNow);
}
