using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IActiveBanReader
{
    Task<LocalReadResult<ActiveBanSnapshot>> ReadAsync(
        CancellationToken cancellationToken = default);
}
