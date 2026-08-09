using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IServiceHeartbeatReader
{
    Task<LocalReadResult<ServiceHeartbeat>> ReadAsync(
        LocalServiceKind service,
        CancellationToken cancellationToken = default);
}
