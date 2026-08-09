using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IStructuredLogReader
{
    Task<StructuredLogSnapshot> ReadAsync(
        SessionManifest? session,
        CancellationToken cancellationToken = default);
}
