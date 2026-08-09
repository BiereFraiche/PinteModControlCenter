using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IControlCenterSnapshotStore
{
    DashboardSnapshot? Current { get; }

    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}
