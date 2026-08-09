using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface ICommunityPauseStatusReader
{
    Task<LocalReadResult<CommunityPauseStatusSnapshot>> ReadAsync(
        CancellationToken cancellationToken = default);
}
