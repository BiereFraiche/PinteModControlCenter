using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class HybridControlCenterDataProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task HybridSnapshot_OverlaysSessionAndThreeBackgroundServices()
    {
        using var root = new TemporaryServerRoot();
        var simulatedProvider = new SimulatedControlCenterDataProvider();
        var simulated = await simulatedProvider.GetSnapshotAsync();
        var session = SuccessSession("zm_castle", "real-session");
        var heartbeats = Enum.GetValues<LocalServiceKind>()
            .ToDictionary(kind => kind, kind => SuccessHeartbeat(kind, "running"));
        var provider = new HybridControlCenterDataProvider(
            simulatedProvider,
            new StubSessionReader(session),
            new StubHeartbeatReader(heartbeats),
            root.Options);

        var result = await provider.GetSnapshotAsync();

        Assert.AreEqual(ControlCenterDataMode.HybridLocal, result.DataContext.Mode);
        Assert.AreEqual("zm_castle", result.Server.MapCode);
        Assert.AreEqual("Der Eisendrache", result.Server.MapName);
        Assert.AreEqual("real-session", result.Server.SessionId);
        Assert.AreEqual(DataProvenance.LocalFile, result.Server.MapProvenance);
        Assert.AreEqual(simulated.Server.Round, result.Server.Round);
        Assert.AreEqual(simulated.Server.SessionDuration, result.Server.SessionDuration);
        Assert.AreEqual(simulated.Server.RankedStatus, result.Server.RankedStatus);
        Assert.AreEqual(simulated.Server.ServerRunning, result.Server.ServerRunning);
        CollectionAssert.AreEqual(simulated.Players.ToArray(), result.Players.ToArray());
        CollectionAssert.AreEqual(simulated.Events.ToArray(), result.Events.ToArray());
        CollectionAssert.AreEqual(simulated.Records.ToArray(), result.Records.ToArray());
        Assert.AreEqual(4, result.Services.Count);
        Assert.AreEqual(ServiceHealth.Unknown, result.Services.Single(service => service.Name == "PinteMod").Health);
        Assert.AreEqual("État inconnu — aucun heartbeat dédié", result.Services.Single(service => service.Name == "PinteMod").Description);
    }

    [TestMethod]
    public async Task MissingSession_DoesNotReplaceSimulatedMap()
    {
        using var root = new TemporaryServerRoot();
        var simulatedProvider = new SimulatedControlCenterDataProvider();
        var missing = new LocalReadResult<SessionManifest>(
            null,
            new LocalSourceMetadata(
                LocalReadStatus.Missing,
                DataFreshness.Unknown,
                null,
                DataProvenance.LocalFile,
                "current_session.json",
                "Fichier absent."),
            null);
        var heartbeats = Enum.GetValues<LocalServiceKind>()
            .ToDictionary(kind => kind, kind => SuccessHeartbeat(kind, "running"));
        var provider = new HybridControlCenterDataProvider(
            simulatedProvider,
            new StubSessionReader(missing),
            new StubHeartbeatReader(heartbeats),
            root.Options);

        var result = await provider.GetSnapshotAsync();

        Assert.AreEqual("zm_tomb", result.Server.MapCode);
        Assert.AreEqual(DataProvenance.Simulation, result.Server.MapProvenance);
        Assert.AreEqual(LocalReadStatus.Missing, result.DataContext.SessionSource.ReadStatus);
    }

    [TestMethod]
    public async Task ExpiredHeartbeat_RemainsExpiredAndUnknownInHybridSnapshot()
    {
        using var root = new TemporaryServerRoot();
        var expired = SuccessHeartbeat(LocalServiceKind.Supervisor, "running") with
        {
            Metadata = SuccessHeartbeat(LocalServiceKind.Supervisor, "running").Metadata with
            {
                Freshness = DataFreshness.Expired,
                Age = TimeSpan.FromSeconds(46)
            }
        };
        var heartbeats = Enum.GetValues<LocalServiceKind>()
            .ToDictionary(kind => kind, kind => kind == LocalServiceKind.Supervisor ? expired : SuccessHeartbeat(kind, "running"));
        var provider = new HybridControlCenterDataProvider(
            new SimulatedControlCenterDataProvider(),
            new StubSessionReader(SuccessSession("zm_tomb", "session")),
            new StubHeartbeatReader(heartbeats),
            root.Options);

        var result = await provider.GetSnapshotAsync();
        var supervisor = result.Services.Single(service => service.Name == "Supervisor");

        Assert.AreEqual(DataFreshness.Expired, supervisor.Source.Freshness);
        Assert.AreEqual(ServiceHealth.Unknown, supervisor.Health);
        Assert.AreNotEqual(ServiceHealth.Offline, supervisor.Health);
    }

    private static LocalReadResult<SessionManifest> SuccessSession(string map, string sessionId) =>
        new(
            new SessionManifest(1, "2.1.1", sessionId, map, 123),
            new LocalSourceMetadata(
                LocalReadStatus.Success,
                DataFreshness.Fresh,
                TimeSpan.FromMinutes(2),
                DataProvenance.LocalFile,
                "current_session.json",
                "OK"),
            Now.AddMinutes(-2));

    private static LocalReadResult<ServiceHeartbeat> SuccessHeartbeat(LocalServiceKind kind, string state)
    {
        var heartbeat = new ServiceHeartbeat(
            1,
            kind,
            TemporaryServerRoot.ToolFor(kind),
            "2.1.1",
            state,
            ServiceHeartbeatReader.MapDeclaredState(state),
            1,
            Now.AddSeconds(-2),
            null);
        return new LocalReadResult<ServiceHeartbeat>(
            heartbeat,
            new LocalSourceMetadata(
                LocalReadStatus.Success,
                DataFreshness.Fresh,
                TimeSpan.FromSeconds(2),
                DataProvenance.LocalFile,
                TemporaryServerRoot.ToolFor(kind) + ".json",
                "OK"),
            heartbeat.UpdatedAtUtc);
    }

    private sealed class StubSessionReader(LocalReadResult<SessionManifest> result) : ISessionManifestReader
    {
        public Task<LocalReadResult<SessionManifest>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class StubHeartbeatReader(
        IReadOnlyDictionary<LocalServiceKind, LocalReadResult<ServiceHeartbeat>> results) : IServiceHeartbeatReader
    {
        public Task<LocalReadResult<ServiceHeartbeat>> ReadAsync(
            LocalServiceKind service,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(results[service]);
    }
}
