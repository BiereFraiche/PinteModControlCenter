using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface ISessionManifestReader
{
    Task<LocalReadResult<SessionManifest>> ReadAsync(CancellationToken cancellationToken = default);
}
