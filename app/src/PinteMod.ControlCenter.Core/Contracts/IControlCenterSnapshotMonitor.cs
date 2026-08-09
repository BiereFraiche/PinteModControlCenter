using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IControlCenterSnapshotMonitor
{
    TimeSpan Interval { get; }

    Task RunAsync(
        Func<DashboardSnapshot, CancellationToken, Task> snapshotUpdated,
        CancellationToken cancellationToken = default);
}
