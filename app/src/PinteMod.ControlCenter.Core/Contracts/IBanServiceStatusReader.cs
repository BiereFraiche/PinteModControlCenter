using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IBanServiceStatusReader
{
    Task<LocalReadResult<BanServiceStatusSnapshot>> ReadAsync(CancellationToken cancellationToken = default);
}
