using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IPlayerModerationHistoryReader
{
    Task<LocalReadResult<PlayerModerationHistory>> ReadAsync(
        string xuid,
        CancellationToken cancellationToken = default);
}
