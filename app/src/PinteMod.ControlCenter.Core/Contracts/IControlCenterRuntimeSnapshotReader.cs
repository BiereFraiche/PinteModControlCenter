using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IControlCenterRuntimeSnapshotReader
{
    Task<LocalReadResult<ControlCenterRuntimeSnapshot>> ReadAsync(
        string? activeSessionId,
        string? activeMapCode,
        CancellationToken cancellationToken = default);
}
