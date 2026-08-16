using System.Globalization;
using System.Net.Sockets;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Infrastructure.Rcon;

public sealed class PlayerAdministrationCommandService(
    IRconClient client,
    IRconSecretStore secretStore,
    IClock clock,
    IRconOperationGate? operationGate = null) : IPlayerAdministrationCommandService
{
    private static readonly HashSet<string> AllowedPerkAliases = new(StringComparer.Ordinal)
    {
        "jug", "quick", "speed", "doubletap", "staminup", "deadshot", "mule", "cherry", "widows"
    };

    private static readonly HashSet<string> AllowedPowerUpAliases = new(StringComparer.Ordinal)
    {
        "maxammo", "instakill", "doublepoints", "firesale", "carpenter",
        "nuke", "deathmachine", "freeperk", "shield"
    };

    private static readonly HashSet<string> AllowedBanDurations = new(StringComparer.Ordinal)
    {
        "30m", "2h", "7d", "4w", "perm"
    };

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
    {
        "helper", "moderator", "admin"
    };

    private readonly IRconOperationGate _operationGate = operationGate ?? RconOperationGate.Shared;

    public Task<PlayerAdministrationExecutionResult> ExecuteAsync(
        PlayerAdministrationRequest request,
        RconEndpoint endpoint,
        CancellationToken cancellationToken = default) =>
        _operationGate.ExecuteAsync(
            token => ExecuteCoreAsync(request, endpoint, token),
            cancellationToken);

    private async Task<PlayerAdministrationExecutionResult> ExecuteCoreAsync(
        PlayerAdministrationRequest request,
        RconEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryBuildCommand(request, out var command))
        {
            return Result(
                request,
                PlayerAdministrationExecutionStatus.InvalidRequest,
                "Action, cible XUID ou option refusée par la liste blanche.",
                false);
        }

        if (!RconEndpointValidator.IsAllowed(endpoint))
        {
            return Result(
                request,
                PlayerAdministrationExecutionStatus.InvalidConfiguration,
                "Adresse ou port RCON invalide.",
                false);
        }

        var password = await secretStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(password))
        {
            return Result(
                request,
                PlayerAdministrationExecutionStatus.SecretMissing,
                "Secret RCON local requis.",
                false);
        }

        var commandSent = false;
        try
        {
            // UDP ne permet pas de distinguer une erreur de réception d'un envoi déjà parti.
            commandSent = true;
            var response = await client.SendAsync(
                endpoint,
                password,
                command,
                cancellationToken).ConfigureAwait(false);
            var responseNotice = string.IsNullOrWhiteSpace(response)
                ? "BOIII n’a renvoyé aucun texte."
                : $"Réponse neutralisée : {LogPrivacyFilter.SanitizeDisplayText(response, 500)}";

            return Result(
                request,
                PlayerAdministrationExecutionStatus.SentAwaitingManualVerification,
                $"Commande transmise · {responseNotice} Vérifiez le résultat dans la partie ou la console avant toute autre mutation.",
                true);
        }
        catch (TimeoutException)
        {
            return Result(
                request,
                PlayerAdministrationExecutionStatus.DeliveryUnknown,
                "Résultat incertain · vérifiez la partie ou la console et ne répétez pas la commande.",
                true);
        }
        catch (Exception exception) when (exception is SocketException or IOException or ArgumentException)
        {
            return Result(
                request,
                PlayerAdministrationExecutionStatus.TransportError,
                commandSent
                    ? "Résultat incertain · le transport a échoué après le début possible de l’envoi. Vérifiez la partie ou la console avant toute autre mutation."
                    : "Échec du transport RCON · aucune commande n’a été préparée.",
                commandSent);
        }
    }

    private PlayerAdministrationExecutionResult Result(
        PlayerAdministrationRequest request,
        PlayerAdministrationExecutionStatus status,
        string message,
        bool commandSent) => new(
        request,
        status,
        LogPrivacyFilter.SanitizeDisplayText(message, 750),
        commandSent,
        clock.UtcNow);

    private static bool TryBuildCommand(PlayerAdministrationRequest request, out string command)
    {
        command = string.Empty;
        var xuid = request.TargetXuid?.Trim();
        if (!XuidValidator.IsValid(xuid))
        {
            return false;
        }

        var option = request.Option?.Trim().ToLowerInvariant();
        command = request.Action switch
        {
            PlayerAdministrationAction.Revive when request.PointsAmount is null && option is null =>
                $"ezzrevive {xuid}",
            PlayerAdministrationAction.Respawn when request.PointsAmount is null && option is null =>
                $"ezzspawn {xuid}",
            PlayerAdministrationAction.GrantPoints when request.PointsAmount is >= -999999 and <= 999999 and not 0 && option is null =>
                $"points {xuid} {request.PointsAmount.Value.ToString(CultureInfo.InvariantCulture)}",
            PlayerAdministrationAction.RefillAmmo when request.PointsAmount is null && option is null =>
                $"ammo {xuid}",
            PlayerAdministrationAction.ToggleGodMode when request.PointsAmount is null && option is null =>
                $"godmode {xuid}",
            PlayerAdministrationAction.GiveWeapon when request.PointsAmount is null && option is not null && PlayerWeaponCatalog.IsAllowedAlias(option) =>
                $"ezzweapon {xuid} {option}",
            PlayerAdministrationAction.PackAPunchCurrentWeapon when request.PointsAmount is null && option is null =>
                $"ezzpapweapon {xuid}",
            PlayerAdministrationAction.GivePerk when request.PointsAmount is null && option is not null && AllowedPerkAliases.Contains(option) =>
                $"ezzperk {xuid} {option}",
            PlayerAdministrationAction.RemovePerk when request.PointsAmount is null && option is not null && AllowedPerkAliases.Contains(option) =>
                $"ezzremoveperk {xuid} {option}",
            PlayerAdministrationAction.GiveAllPerks when request.PointsAmount is null && option is null =>
                $"ezzallperks {xuid}",
            PlayerAdministrationAction.GivePowerUp when request.PointsAmount is null && option is not null && AllowedPowerUpAliases.Contains(option) =>
                $"ezzpowerup {xuid} {option}",
            PlayerAdministrationAction.TeleportToOwnAim when request.PointsAmount is null && option is null =>
                $"ezztp {xuid}",
            PlayerAdministrationAction.Mute when request.PointsAmount is null && option is null =>
                $"ezzmute {xuid} control-center",
            PlayerAdministrationAction.Unmute when request.PointsAmount is null && option is null =>
                $"ezzunmute {xuid}",
            PlayerAdministrationAction.Kick when request.PointsAmount is null && option is null =>
                $"ezzkick {xuid} control-center",
            PlayerAdministrationAction.Ban when request.PointsAmount is null && option is not null && AllowedBanDurations.Contains(option) =>
                $"ezzban {xuid} {option} control-center",
            PlayerAdministrationAction.SetRole when request.PointsAmount is null && option is not null && AllowedRoles.Contains(option) =>
                $"ezzidsetrole {xuid} {option}",
            PlayerAdministrationAction.RemoveRole when request.PointsAmount is null && option is null =>
                $"ezzidremoverole {xuid}",
            _ => string.Empty
        };
        return command.Length > 0;
    }
}
