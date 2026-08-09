using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IMapCatalogService
{
    Task<MapCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<MapCatalogOperationResult> ImportRotationLineAsync(
        string rotationLine,
        CancellationToken cancellationToken = default);

    Task<MapCatalogOperationResult> AddManualMapAsync(
        string mapCode,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<MapCatalogOperationResult> RemoveManualMapAsync(
        string mapCode,
        CancellationToken cancellationToken = default);

    Task<MapCatalogOperationResult> ObserveMapAsync(
        string mapCode,
        string displayName,
        CancellationToken cancellationToken = default);
}
