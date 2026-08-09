using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface ICommunityPauseLogReader
{
    Task<CommunityPauseLogSnapshot> ReadAsync(
        SessionManifest? session,
        CancellationToken cancellationToken = default);
}
