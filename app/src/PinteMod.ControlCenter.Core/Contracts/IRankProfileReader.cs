using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IRankProfileReader
{
    Task<LocalReadResult<RankProfileCatalog>> ReadAsync(CancellationToken cancellationToken = default);
}
