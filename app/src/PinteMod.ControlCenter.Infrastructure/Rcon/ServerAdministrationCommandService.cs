using System.Globalization;
using System.Net.Sockets;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Infrastructure.Rcon;

public sealed class ServerAdministrationCommandService(
    IRconClient client,
    IRconSecretStore secretStore,
    IClock clock,
    IRconOperationGate? operationGate = null) : IServerAdministrationCommandService
{
    private readonly IRconOperationGate _operationGate = operationGate ?? RconOperationGate.Shared;

    public Task<ServerAdministrationExecutionResult> ExecuteAsync(
        ServerAdministrationRequest request,
        RconEndpoint endpoint,
        CancellationToken cancellationToken = default) =>
        _operationGate.ExecuteAsync(
            token => ExecuteCoreAsync(request, endpoint, token),
            cancellationToken);

    private async Task<ServerAdministrationExecutionResult> ExecuteCoreAsync(
        ServerAdministrationRequest request,
        RconEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryBuildCommand(request, out var command))
        {
            return Result(
                request,
                ServerAdministrationExecutionStatus.InvalidRequest,
                "Action ou paramètre serveur refusé par la liste blanche.",
                false);
        }

        if (!RconEndpointValidator.IsAllowed(endpoint))
        {
            return Result(
                request,
                ServerAdministrationExecutionStatus.InvalidConfiguration,
                "Adresse ou port RCON invalide.",
                false);
        }

        var password = await secretStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(password))
        {
            return Result(
                request,
                ServerAdministrationExecutionStatus.SecretMissing,
                "Secret RCON local requis.",
                false);
        }

        var commandSent = false;
        try
        {
            // UDP ne fournit aucun accusé de livraison. Dès que l’appel commence,
            // la mutation est considérée comme potentiellement reçue par BOIII.
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
                ServerAdministrationExecutionStatus.SentAwaitingManualVerification,
                $"Commande transmise · {responseNotice} Vérifiez la console du serveur avant toute autre mutation.",
                true);
        }
        catch (TimeoutException)
        {
            return Result(
                request,
                ServerAdministrationExecutionStatus.DeliveryUnknown,
                "Résultat incertain · vérifiez la console du serveur et ne répétez pas la commande.",
                true);
        }
        catch (Exception exception) when (exception is SocketException or IOException or ArgumentException)
        {
            return Result(
                request,
                ServerAdministrationExecutionStatus.TransportError,
                commandSent
                    ? "Résultat incertain · le transport a échoué après le début de l’envoi. Vérifiez la console avant toute autre mutation."
                    : "Échec du transport RCON · aucune commande n’a été préparée.",
                commandSent);
        }
    }

    private ServerAdministrationExecutionResult Result(
        ServerAdministrationRequest request,
        ServerAdministrationExecutionStatus status,
        string message,
        bool commandSent) => new(
        request,
        status,
        LogPrivacyFilter.SanitizeDisplayText(message, 750),
        commandSent,
        clock.UtcNow);

    private static bool TryBuildCommand(ServerAdministrationRequest request, out string command)
    {
        var hasNoContractArguments = request.RequestId is null &&
                                     request.Option is null &&
                                     request.TargetXuid is null;
        command = request.Action switch
        {
            ServerAdministrationAction.NextRound when request.TargetRound is null && hasNoContractArguments => "ezznextround",
            ServerAdministrationAction.SetRound when request.TargetRound is >= 2 and <= 255 && hasNoContractArguments =>
                $"ezzsetround {request.TargetRound.Value.ToString(CultureInfo.InvariantCulture)}",
            ServerAdministrationAction.EnablePower when request.TargetRound is null && hasNoContractArguments => "ezzpower",
            ServerAdministrationAction.EnablePackAPunch when request.TargetRound is null && hasNoContractArguments => "ezzpap",
            ServerAdministrationAction.PlayMapMusic when request.TargetRound is null && hasNoContractArguments => "ezzmusicplayall",
            ServerAdministrationAction.StopMapMusic when request.TargetRound is null && hasNoContractArguments => "ezzmusicstopall",
            ServerAdministrationAction.UnlockStandardPassages when request.TargetRound is null && hasNoContractArguments => "ezzunlock",
            ServerAdministrationAction.KeepLastZombie when request.TargetRound is null && hasNoContractArguments => "ezzlastzombie",
            ServerAdministrationAction.KillAllZombies when request.TargetRound is null && hasNoContractArguments => "ezzkillzombies",
            ServerAdministrationAction.MakePowerUpsPermanent when request.TargetRound is null && hasNoContractArguments => "ezzfreezepowerups on",
            ServerAdministrationAction.RestorePowerUpTimeout when request.TargetRound is null && hasNoContractArguments => "ezzfreezepowerups off",
            ServerAdministrationAction.RestartMap when
                request.TargetRound is null &&
                request.Option is null &&
                request.TargetXuid is null &&
                ControlCenterCommandValidator.IsValidRequestId(request.RequestId) =>
                $"ezzccrestartmap {request.RequestId}",
            ServerAdministrationAction.SpawnBoss when
                request.TargetRound is null &&
                ControlCenterCommandValidator.IsValidRequestId(request.RequestId) &&
                ControlCenterCommandValidator.IsValidBossAlias(request.Option) &&
                XuidValidator.IsValid(request.TargetXuid) =>
                $"ezzccboss {request.RequestId} {request.Option} {request.TargetXuid}",
            ServerAdministrationAction.SetHostname when
                request.TargetRound is null &&
                request.TargetXuid is null &&
                ControlCenterCommandValidator.IsValidRequestId(request.RequestId) &&
                ControlCenterCommandValidator.IsValidHostname(request.Option) =>
                $"ezzccsethostname {request.RequestId} {request.Option}",
            ServerAdministrationAction.ClearJoinPassword when
                request.TargetRound is null &&
                request.Option is null &&
                request.TargetXuid is null &&
                ControlCenterCommandValidator.IsValidRequestId(request.RequestId) =>
                $"ezzccclearjoinpassword {request.RequestId}",
            _ => string.Empty
        };
        return command.Length > 0;
    }
}
