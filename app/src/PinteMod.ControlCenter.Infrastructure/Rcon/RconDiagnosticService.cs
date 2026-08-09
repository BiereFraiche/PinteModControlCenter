using System.Net.Sockets;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Infrastructure.Rcon;

public sealed class RconDiagnosticService(
    IRconClient client,
    IRconSecretStore secretStore,
    IClock clock,
    IRconOperationGate? operationGate = null) : IRconDiagnosticService
{
    private readonly IRconOperationGate _operationGate = operationGate ?? RconOperationGate.Shared;

    public Task<RconExecutionResult> ExecuteAsync(
        RconDiagnosticCommand command,
        RconEndpoint endpoint,
        CancellationToken cancellationToken = default) =>
        _operationGate.ExecuteAsync(
            token => ExecuteCoreAsync(command, endpoint, token),
            cancellationToken);

    private async Task<RconExecutionResult> ExecuteCoreAsync(
        RconDiagnosticCommand command,
        RconEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        if (!IsValidEndpoint(endpoint))
        {
            return Result(command, RconExecutionStatus.InvalidConfiguration, "Adresse ou port RCON invalide.", false);
        }

        var password = await secretStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(password))
        {
            return Result(command, RconExecutionStatus.SecretMissing, "Enregistrez d’abord le secret RCON local.", false);
        }

        try
        {
            var response = await client.SendAsync(
                endpoint,
                password,
                CommandText(command),
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(response))
            {
                return Result(
                    command,
                    RconExecutionStatus.EmptyResponse,
                    "BOIII a répondu sans texte · vérifiez le résultat dans la console du serveur.",
                    true);
            }

            var displayResponse = LogPrivacyFilter.SanitizeDisplayText(response, 4000);
            if (!RconDiagnosticResponseValidator.IsExpected(command, response))
            {
                return Result(
                    command,
                    RconExecutionStatus.UnexpectedResponse,
                    $"Réponse reçue mais non reconnue pour ce diagnostic · {displayResponse}",
                    true);
            }

            return Result(
                command,
                RconExecutionStatus.Success,
                displayResponse,
                true);
        }
        catch (TimeoutException)
        {
            return Result(command, RconExecutionStatus.Timeout, "Délai dépassé · aucune réponse RCON.", true);
        }
        catch (Exception exception) when (exception is SocketException or IOException or ArgumentException)
        {
            return Result(command, RconExecutionStatus.TransportError, "Échec du transport RCON.", false);
        }
    }

    private RconExecutionResult Result(
        RconDiagnosticCommand command,
        RconExecutionStatus status,
        string response,
        bool commandSent) => new(command, status, response, commandSent, clock.UtcNow);

    private static string CommandText(RconDiagnosticCommand command) => command switch
    {
        RconDiagnosticCommand.HealthFull => "ezzhealth full",
        RconDiagnosticCommand.PauseStatus => "ezzpausestatus",
        RconDiagnosticCommand.MapInfo => "ezzmap",
        RconDiagnosticCommand.PowerStatus => "ezzpowerstatus",
        RconDiagnosticCommand.PackAPunchStatus => "ezzpapstatus",
        RconDiagnosticCommand.RoundStatus => "ezzround",
        RconDiagnosticCommand.Players => "ezzplayers",
        RconDiagnosticCommand.MapAudit => "ezzmapaudit full",
        RconDiagnosticCommand.EventStatus => "ezzeventstatus",
        RconDiagnosticCommand.PowerUpCatalog => "ezzpowerups",
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Commande de diagnostic non autorisée.")
    };

    private static bool IsValidEndpoint(RconEndpoint endpoint) => RconEndpointValidator.IsAllowed(endpoint);
}
