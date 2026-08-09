using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class BlockAControlCenterDataProviderTests
{
    [TestMethod]
    public async Task BlockA_LeavesValidatedRecordsAndServicesUntouched_AndReplacesRuntimeSimulation()
    {
        const string xuid = "1111111111111111";
        var baseline = new SimulatedControlCenterDataProvider();
        var baselineSnapshot = await baseline.GetSnapshotAsync();
        var source = new LocalSourceMetadata(LocalReadStatus.Success, DataFreshness.Fresh, TimeSpan.Zero,
            DataProvenance.LocalFile, "fixture", "OK");
        var session = new SessionManifest(1, "2.1.1", "session", "zm_tomb", 1000);
        var player = new PlayerState(0, xuid, "Alice", "unknown", "unknown", "--", PlayerLifeState.Unknown,
            0, TimeSpan.FromSeconds(4), false, false)
        {
            LifeStateAvailable = false,
            PointsAvailable = false,
            Provenance = DataProvenance.LocalFile
        };
        var logs = new StructuredLogSnapshot("session", [], [player], 5, TimeSpan.FromSeconds(5),
            RankedStatus.Unranked, true, source, 2, 0, 0, 0);
        var metadata = new LocalPlayerMetadataSnapshot([new(xuid, "Alice", "moderator", "fr", null)], 2, 0);
        var provider = new BlockAControlCenterDataProvider(
            baseline,
            new SessionStub(new(session, source, DateTimeOffset.UtcNow)),
            new InstallationStub(new(null, source, null)),
            new BanStub(new(null, source, null)),
            new MetadataStub(new(metadata, source, DateTimeOffset.UtcNow)),
            new LogStub(logs));

        var result = await provider.GetSnapshotAsync();

        CollectionAssert.AreEqual(baselineSnapshot.Records.ToArray(), result.Records.ToArray());
        CollectionAssert.AreEqual(baselineSnapshot.Services.ToArray(), result.Services.ToArray());
        Assert.AreEqual(5, result.Server.Round);
        Assert.AreEqual(RankedStatus.Unranked, result.Server.RankedStatus);
        Assert.IsFalse(result.Server.ServerRunningAvailable);
        Assert.IsFalse(result.Server.MaxPlayersAvailable);
        Assert.AreEqual("moderator", result.Players.Single().Role);
        Assert.AreEqual("fr", result.Players.Single().Language);
        Assert.IsTrue(result.Server.RuntimeValuesInferred);
    }

    [TestMethod]
    public async Task PauseObservationOlderThanCurrentSession_IsNotExposedAsCurrent()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new LocalSourceMetadata(LocalReadStatus.Success, DataFreshness.Fresh, TimeSpan.Zero,
            DataProvenance.LocalFile, "fixture", "OK");
        var session = new SessionManifest(1, "2.1.1", "session", "zm_tomb", 1000);
        var pause = new CommunityPauseStatusSnapshot(
            "0.3", 900, true, 120, 1, 2, 1, "Aucun", null, null, null, true, true, true);
        var logs = StructuredLogSnapshot.Empty("session", source);
        var provider = new BlockAControlCenterDataProvider(
            new SimulatedControlCenterDataProvider(),
            new SessionStub(new(session, source, now)),
            new InstallationStub(new(null, source, null)),
            new BanStub(new(null, source, null)),
            new MetadataStub(new(null, source, null)),
            new LogStub(logs),
            new PauseStatusStub(new(pause, source, now.AddSeconds(-1))),
            new PauseLogStub(CommunityPauseLogSnapshot.Empty(source)));

        var result = await provider.GetSnapshotAsync();

        Assert.IsNull(result.LocalObservation.CommunityPause.Value);
        Assert.AreEqual(DataFreshness.Unknown, result.LocalObservation.CommunityPause.Metadata.Freshness);
    }

    private sealed class SessionStub(LocalReadResult<SessionManifest> value) : ISessionManifestReader
    {
        public Task<LocalReadResult<SessionManifest>> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class InstallationStub(LocalReadResult<InstallationVerificationReport> value) : IInstallationVerificationReader
    {
        public Task<LocalReadResult<InstallationVerificationReport>> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class BanStub(LocalReadResult<BanServiceStatusSnapshot> value) : IBanServiceStatusReader
    {
        public Task<LocalReadResult<BanServiceStatusSnapshot>> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class MetadataStub(LocalReadResult<LocalPlayerMetadataSnapshot> value) : ILocalPlayerMetadataReader
    {
        public Task<LocalReadResult<LocalPlayerMetadataSnapshot>> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class LogStub(StructuredLogSnapshot value) : IStructuredLogReader
    {
        public Task<StructuredLogSnapshot> ReadAsync(SessionManifest? session, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class PauseStatusStub(LocalReadResult<CommunityPauseStatusSnapshot> value) : ICommunityPauseStatusReader
    {
        public Task<LocalReadResult<CommunityPauseStatusSnapshot>> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class PauseLogStub(CommunityPauseLogSnapshot value) : ICommunityPauseLogReader
    {
        public Task<CommunityPauseLogSnapshot> ReadAsync(SessionManifest? session, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }
}
