using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Simulation;

public sealed class CachedControlCenterSnapshotStore(IControlCenterDataProvider dataProvider)
    : IControlCenterSnapshotStore, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DashboardSnapshot? Current { get; private set; }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (Current is not null)
        {
            return Current;
        }

        return await RefreshAsync(cancellationToken);
    }

    public async Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Current = await dataProvider.GetSnapshotAsync(cancellationToken);
            return Current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
