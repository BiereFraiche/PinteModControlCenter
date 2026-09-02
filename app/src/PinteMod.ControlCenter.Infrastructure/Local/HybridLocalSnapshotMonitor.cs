using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class HybridLocalSnapshotMonitor(
    IControlCenterSnapshotStore snapshotStore,
    TimeSpan? interval = null) : IControlCenterSnapshotMonitor
{
    public TimeSpan Interval { get; } = interval ?? TimeSpan.FromSeconds(2);

    public Task RunAsync(
        Func<DashboardSnapshot, CancellationToken, Task> snapshotUpdated,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshotUpdated);
        if (Interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("L’intervalle d’actualisation doit être positif.");
        }

        return Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var snapshot = await snapshotStore.RefreshAsync(cancellationToken).ConfigureAwait(false);
                    await snapshotUpdated(snapshot, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
                {
                    // PinteMod replaces its runtime files atomically. A file may
                    // briefly be locked or absent while a player joins/leaves;
                    // keep the last screen state and try again on the next tick.
                }
            }
        }, cancellationToken);
    }
}
