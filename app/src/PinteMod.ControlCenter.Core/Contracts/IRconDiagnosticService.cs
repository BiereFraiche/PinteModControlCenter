using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IRconDiagnosticService
{
    Task<RconExecutionResult> ExecuteAsync(
        RconDiagnosticCommand command,
        RconEndpoint endpoint,
        CancellationToken cancellationToken = default);
}
