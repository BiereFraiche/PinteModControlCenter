using System.Net.Sockets;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Infrastructure.Rcon;

public sealed class CommunityPauseCommandService(
    IRconClient client,
    IRconSecretStore secretStore,
    IClock clock,
    IRconOperationGate? operationGate = null) : ICommunityPauseCommandService
{
    private readonly IRconOperationGate _operationGate = operationGate ?? RconOperationGate.Shared;

    public Task<CommunityPauseExecutionResult> ExecuteAsync(
        CommunityPauseAction action,
        RconEndpoint endpoint,
        CancellationToken cancellationToken = default) =>
        _operationGate.ExecuteAsync(
            token => ExecuteCoreAsync(action, endpoint, token),
            cancellationToken);

    private async Task<CommunityPauseExecutionResult> ExecuteCoreAsync(
        CommunityPauseAction action,
        RconEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        if (!IsValidEndpoint(endpoint))
        {
            return Result(action, CommunityPauseExecutionStatus.InvalidConfiguration,
                "Adresse ou port RCON invalide.", false, false);
        }

        var password = await secretStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(password))
        {
            return Result(action, CommunityPauseExecutionStatus.SecretMissing,
                "Secret RCON local requis.", false, false);
        }

        var commandSent = false;
        try
        {
            var mutationCommand = CommandText(action);

            // UDP does not provide an acknowledgement for the datagram itself.
            // Once the transport call begins, any later transport failure is
            // conservatively treated as a delivery that may have occurred.
            commandSent = true;
            _ = await client.SendAsync(
                endpoint,
                password,
                mutationCommand,
                cancellationToken).ConfigureAwait(false);

            // Explicit verification follows the requested mutation. It updates the
            // read-only feedback file used as the authoritative UI observation.
            _ = await client.SendAsync(
                endpoint,
                password,
                "ezzpausestatus",
                cancellationToken).ConfigureAwait(false);

            return Result(
                action,
                CommunityPauseExecutionStatus.SentAwaitingObservation,
                "Commande transmise · vérification de l’état local en cours.",
                true,
                true);
        }
        catch (TimeoutException)
        {
            return Result(
                action,
                CommunityPauseExecutionStatus.DeliveryUnknown,
                "Résultat incertain · ne recommencez pas immédiatement, vérifiez l’état local.",
                true,
                false);
        }
        catch (Exception exception) when (exception is SocketException or IOException or ArgumentException)
        {
            return Result(
                action,
                CommunityPauseExecutionStatus.TransportError,
                commandSent
                    ? "Résultat incertain · le transport a échoué après le début de l’envoi. Actualisez le statut avant toute nouvelle commande."
                    : "Échec du transport RCON · aucune commande n’a été préparée.",
                commandSent,
                false);
        }
    }

    private CommunityPauseExecutionResult Result(
        CommunityPauseAction action,
        CommunityPauseExecutionStatus status,
        string message,
        bool commandSent,
        bool statusRefreshRequested) => new(
            action,
            status,
            LogPrivacyFilter.SanitizeDisplayText(message, 500),
            commandSent,
            statusRefreshRequested,
            clock.UtcNow);

    private static string CommandText(CommunityPauseAction action) => action switch
    {
        CommunityPauseAction.Pause => "ezzpauseforce",
        CommunityPauseAction.Resume => "ezzresume",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Action Community Pause non autorisée.")
    };

    private static bool IsValidEndpoint(RconEndpoint endpoint) => RconEndpointValidator.IsAllowed(endpoint);
}
