using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface ILocalPlayerMetadataReader
{
    Task<LocalReadResult<LocalPlayerMetadataSnapshot>> ReadAsync(CancellationToken cancellationToken = default);
}
