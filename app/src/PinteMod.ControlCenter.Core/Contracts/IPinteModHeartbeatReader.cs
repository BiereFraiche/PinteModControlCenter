using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IPinteModHeartbeatReader
{
    Task<LocalReadResult<PinteModHeartbeatSnapshot>> ReadAsync(
        string? activeSessionId,
        CancellationToken cancellationToken = default);
}
