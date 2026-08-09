using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class HybridLocalSnapshotMonitorTests
{
    [TestMethod]
    public async Task Monitor_IsSequentialAndStopsOnCancellation()
    {
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var store = new SlowSnapshotStore(snapshot);
        var monitor = new HybridLocalSnapshotMonitor(store, TimeSpan.FromMilliseconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var callbacks = 0;

        try
        {
            await monitor.RunAsync((_, _) =>
            {
                callbacks++;
                if (callbacks == 3)
                {
                    cancellation.Cancel();
                }

                return Task.CompletedTask;
            }, cancellation.Token);
            Assert.Fail("Une annulation était attendue.");
        }
        catch (OperationCanceledException)
        {
        }

        Assert.AreEqual(3, callbacks);
        Assert.AreEqual(1, store.MaximumConcurrency);
    }

    [TestMethod]
    public async Task Monitor_RefreshRunsOutsideCallingSynchronizationContext()
    {
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var store = new SlowSnapshotStore(snapshot);
        var monitor = new HybridLocalSnapshotMonitor(store, TimeSpan.FromMilliseconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var callingContext = new SynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(callingContext);
        try
        {
            var run = monitor.RunAsync((_, _) =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            }, cancellation.Token);
            try
            {
                await run.ConfigureAwait(false);
                Assert.Fail("Une annulation était attendue.");
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.IsNull(store.ObservedSynchronizationContext);
        Assert.AreNotSame(callingContext, store.ObservedSynchronizationContext);
    }

    private sealed class SlowSnapshotStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        private int _concurrency;
        public int MaximumConcurrency { get; private set; }
        public SynchronizationContext? ObservedSynchronizationContext { get; private set; }
        public DashboardSnapshot? Current { get; private set; } = snapshot;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current!);

        public async Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
        {
            ObservedSynchronizationContext = SynchronizationContext.Current;
            var current = Interlocked.Increment(ref _concurrency);
            MaximumConcurrency = Math.Max(MaximumConcurrency, current);
            try
            {
                await Task.Delay(10, cancellationToken);
                return Current!;
            }
            finally
            {
                Interlocked.Decrement(ref _concurrency);
            }
        }
    }
}
