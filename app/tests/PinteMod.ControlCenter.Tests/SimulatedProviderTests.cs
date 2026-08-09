using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class SimulatedProviderTests
{
    [TestMethod]
    public async Task Snapshot_IsInternallyConsistentAndSafe()
    {
        var provider = new SimulatedControlCenterDataProvider();

        var snapshot = await provider.GetSnapshotAsync();

        Assert.AreEqual(snapshot.Players.Count, snapshot.Server.PlayersConnected);
        Assert.AreEqual(RankedStatus.Ranked, snapshot.Server.RankedStatus);
        Assert.IsTrue(snapshot.Services.All(service => service.Health == ServiceHealth.Healthy));
        Assert.IsTrue(snapshot.Players.All(player => XuidValidator.IsValid(player.Xuid)));
        Assert.IsTrue(snapshot.Players.All(player => player.Xuid.StartsWith("000000000000000", StringComparison.Ordinal)));
        Assert.AreEqual(snapshot.Players.Count, snapshot.Players.Select(player => player.Xuid).Distinct().Count());
    }

    [TestMethod]
    public void DegradedScenarios_ExposeWarningOfflineErrorUnknownAndEmptyStates()
    {
        var warning = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Warning);
        var offline = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Offline);
        var stopped = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.ServerStopped);
        var empty = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Empty);

        Assert.IsTrue(warning.Services.Any(service => service.Health == ServiceHealth.Warning));
        Assert.IsTrue(offline.Services.Any(service => service.Health == ServiceHealth.Offline));
        Assert.IsTrue(offline.Services.Any(service => service.Health == ServiceHealth.Error));
        Assert.IsTrue(stopped.Services.Any(service => service.Health == ServiceHealth.Unknown));
        Assert.IsFalse(stopped.Server.ServerRunning);
        Assert.AreEqual(RankedStatus.Unranked, stopped.Server.RankedStatus);
        Assert.AreEqual(0, empty.Players.Count);
        Assert.AreEqual(0, empty.Records.Count);
        Assert.AreEqual(0, empty.Events.Count);
    }
}
