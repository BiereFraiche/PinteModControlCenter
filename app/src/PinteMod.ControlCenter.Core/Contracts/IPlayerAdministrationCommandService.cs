using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IPlayerAdministrationCommandService
{
    Task<PlayerAdministrationExecutionResult> ExecuteAsync(
        PlayerAdministrationRequest request,
        RconEndpoint endpoint,
        CancellationToken cancellationToken = default);
}
