using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface ICommunityPauseCommandService
{
    Task<CommunityPauseExecutionResult> ExecuteAsync(
        CommunityPauseAction action,
        RconEndpoint endpoint,
        CancellationToken cancellationToken = default);
}
