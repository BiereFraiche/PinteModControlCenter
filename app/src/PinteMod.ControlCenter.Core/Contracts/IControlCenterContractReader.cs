using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IControlCenterContractReader
{
    Task<ControlCenterContractSnapshot> ReadAsync(
        string? activeSessionId,
        string? activeMapCode,
        CancellationToken cancellationToken = default);
}
