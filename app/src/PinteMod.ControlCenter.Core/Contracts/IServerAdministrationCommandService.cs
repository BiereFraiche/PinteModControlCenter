using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IServerAdministrationCommandService
{
    Task<ServerAdministrationExecutionResult> ExecuteAsync(
        ServerAdministrationRequest request,
        RconEndpoint endpoint,
        CancellationToken cancellationToken = default);
}
