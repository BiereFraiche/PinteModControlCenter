using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PinteModRuntimeOverlayDataProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly LocalSourceMetadata FreshSource = new(
        LocalReadStatus.Success,
        DataFreshness.Fresh,
        TimeSpan.FromSeconds(2),
        DataProvenance.LocalFile,
        "fixture",
        "OK");

    [TestMethod]
    public async Task FreshSessionMatchedSources_ReplaceInferredRuntimeAndPinteModSyntheticStatus()
    {
        var baseline = await HybridBaselineAsync();
        var provider = new PinteModRuntimeOverlayDataProvider(
            new FixedProvider(baseline),
            new HeartbeatStub(new(Heartbeat(ServiceDeclaredState.Running), FreshSource, Now.AddSeconds(-2))),
            new RuntimeStub(new(Runtime(), FreshSource, Now.AddSeconds(-2))));

        var result = await provider.GetSnapshotAsync();

        Assert.IsFalse(result.Server.RuntimeValuesInferred);
        Assert.AreEqual(31, result.Server.Round);
        Assert.AreEqual(3, result.Server.PlayersConnected);
        Assert.AreEqual(18, result.Server.MaxPlayers);
        Assert.AreEqual(RankedStatus.Unranked, result.Server.RankedStatus);
        Assert.AreEqual(TimeSpan.FromMinutes(42), result.Server.SessionDuration);
        Assert.IsTrue(result.Server.ServerRunning);
        Assert.IsTrue(result.Server.ServerRunningAvailable);
        Assert.AreEqual(RuntimePowerState.On, result.Server.PowerState);
        Assert.AreEqual(ServiceHealth.Healthy, result.Services.Single(service => service.Name == "PinteMod").Health);
        Assert.AreEqual("moderator", result.Players.Single().Role);
        Assert.AreEqual("fr", result.Players.Single().Language);
        Assert.IsNotNull(result.Players.Single().RuntimeDetails);
        Assert.AreEqual(DataProvenance.LocalFile, result.Players.Single().Provenance);
        Assert.IsFalse(result.Players.Single().ModerationStateAvailable);
        Assert.AreSame(result.LocalObservation.RuntimeSnapshot.Value, result.Server.RuntimeSource == FreshSource
            ? result.LocalObservation.RuntimeSnapshot.Value
            : null);
    }

    [TestMethod]
    public async Task MetadataIsMergedOnlyByXuidAndNeverByDisplayName()
    {
        var baseline = await HybridBaselineAsync();
        var wrongIdentity = baseline.Players.Single() with
        {
            Xuid = "0000000000000002",
            DisplayName = "Nom runtime identique",
            Role = "admin"
        };
        baseline = baseline with { Players = [wrongIdentity] };
        var provider = new PinteModRuntimeOverlayDataProvider(
            new FixedProvider(baseline),
            new HeartbeatStub(new(Heartbeat(ServiceDeclaredState.Running), FreshSource, Now)),
            new RuntimeStub(new(Runtime(displayName: "Nom runtime identique"), FreshSource, Now)));

        var result = await provider.GetSnapshotAsync();

        Assert.AreEqual("unknown", result.Players.Single().Role);
        Assert.AreNotEqual("admin", result.Players.Single().Role);
    }

    [TestMethod]
    public async Task StaleRuntime_DoesNotOverwriteLogInferenceOrPlayers()
    {
        var baseline = await HybridBaselineAsync();
        var stale = FreshSource with { Freshness = DataFreshness.Stale, Age = TimeSpan.FromSeconds(20) };
        var provider = new PinteModRuntimeOverlayDataProvider(
            new FixedProvider(baseline),
            new HeartbeatStub(new(Heartbeat(ServiceDeclaredState.Running), FreshSource, Now)),
            new RuntimeStub(new(Runtime(), stale, Now.AddSeconds(-20))));

        var result = await provider.GetSnapshotAsync();

        Assert.IsTrue(result.Server.RuntimeValuesInferred);
        Assert.AreEqual(baseline.Server.Round, result.Server.Round);
        CollectionAssert.AreEqual(baseline.Players.ToArray(), result.Players.ToArray());
        Assert.AreEqual(DataFreshness.Stale, result.LocalObservation.RuntimeSnapshot.Metadata.Freshness);
    }

    [TestMethod]
    public async Task ExpiredHeartbeatNeverClaimsOfflineButFreshStoppedDoes()
    {
        var baseline = await HybridBaselineAsync();
        var expired = FreshSource with { Freshness = DataFreshness.Expired, Age = TimeSpan.FromSeconds(46) };
        var expiredProvider = new PinteModRuntimeOverlayDataProvider(
            new FixedProvider(baseline),
            new HeartbeatStub(new(Heartbeat(ServiceDeclaredState.Running), expired, Now.AddSeconds(-46))),
            new RuntimeStub(new(null, LocalSourceMetadata.Unavailable("absent"), null)));
        var expiredResult = await expiredProvider.GetSnapshotAsync();
        Assert.IsFalse(expiredResult.Server.ServerRunningAvailable);
        Assert.AreEqual(ServiceHealth.Unknown, expiredResult.Services.Single(service => service.Name == "PinteMod").Health);
        Assert.AreEqual("Dernière donnée valide — périmée.", expiredResult.Services.Single(service => service.Name == "PinteMod").Description);

        var stoppedProvider = new PinteModRuntimeOverlayDataProvider(
            new FixedProvider(baseline),
            new HeartbeatStub(new(Heartbeat(ServiceDeclaredState.Stopped), FreshSource, Now)),
            new RuntimeStub(new(null, LocalSourceMetadata.Unavailable("absent"), null)));
        var stoppedResult = await stoppedProvider.GetSnapshotAsync();
        Assert.IsTrue(stoppedResult.Server.ServerRunningAvailable);
        Assert.IsFalse(stoppedResult.Server.ServerRunning);
        Assert.AreEqual(ServiceHealth.Offline, stoppedResult.Services.Single(service => service.Name == "PinteMod").Health);

        var errorProvider = new PinteModRuntimeOverlayDataProvider(
            new FixedProvider(baseline),
            new HeartbeatStub(new(Heartbeat(ServiceDeclaredState.Error), FreshSource, Now)),
            new RuntimeStub(new(null, LocalSourceMetadata.Unavailable("absent"), null)));
        var errorResult = await errorProvider.GetSnapshotAsync();
        Assert.IsTrue(errorResult.Server.ServerRunningAvailable);
        Assert.AreEqual(ServiceHealth.Error, errorResult.Server.ObservedServerHealth);
        Assert.AreEqual(ServiceHealth.Error, errorResult.Services.Single(service => service.Name == "PinteMod").Health);
    }

    [TestMethod]
    public async Task UnverifiedCurrentSession_PreventsBothRuntimeReadersFromBecomingAuthoritative()
    {
        var baseline = await HybridBaselineAsync();
        baseline = baseline with
        {
            DataContext = baseline.DataContext with
            {
                SessionSource = baseline.DataContext.SessionSource with { ReadStatus = LocalReadStatus.IoError }
            }
        };
        var heartbeat = new CapturingHeartbeatStub();
        var runtime = new CapturingRuntimeStub();
        var provider = new PinteModRuntimeOverlayDataProvider(new FixedProvider(baseline), heartbeat, runtime);

        await provider.GetSnapshotAsync();

        Assert.IsNull(heartbeat.SessionId);
        Assert.IsNull(runtime.SessionId);
        Assert.IsNull(runtime.MapCode);
    }

    private static async Task<DashboardSnapshot> HybridBaselineAsync()
    {
        var baseline = await new SimulatedControlCenterDataProvider().GetSnapshotAsync();
        var source = FreshSource with { SourceLabel = "current_session.json" };
        var matchingPlayer = baseline.Players[0] with
        {
            Xuid = "0000000000000001",
            DisplayName = "Ancien nom",
            Role = "moderator",
            Language = "fr",
            CountryCode = "FR",
            Provenance = DataProvenance.LocalFile
        };
        return baseline with
        {
            Server = baseline.Server with
            {
                SessionId = "session-local-001",
                MapCode = "zm_tomb",
                SessionProvenance = DataProvenance.LocalFile,
                MapProvenance = DataProvenance.LocalFile,
                RuntimeValuesInferred = true
            },
            Players = [matchingPlayer],
            DataContext = new(
                ControlCenterDataMode.HybridLocal,
                "MODE HYBRIDE LOCAL",
                null,
                source,
                ["Runtime"]),
            LocalObservation = BlockALocalSnapshot.Simulation,
            Services =
            [
                new ServiceStatus("PinteMod", "État inconnu — aucun heartbeat dédié", ServiceHealth.Unknown, DateTimeOffset.MinValue),
                .. baseline.Services.Where(service => service.Name != "PinteMod")
            ]
        };
    }

    private static PinteModHeartbeatSnapshot Heartbeat(ServiceDeclaredState state) =>
        new(1, "2.1.1", "session-local-001", state, null, 4, 1000, null,
            "session_gettime_and_file_mtime");

    private static ControlCenterRuntimeSnapshot Runtime(string displayName = "Joueur runtime")
    {
        var runtimePlayer = new RuntimePlayerSnapshot(
            "0000000000000001",
            displayName,
            0,
            "connected",
            PlayerLifeState.Alive,
            RuntimeGodModeState.Off,
            12000,
            100,
            100,
            "ray_gun",
            RuntimeWeaponPackAPunchState.Upgraded,
            20,
            160,
            [new("ray_gun", RuntimeWeaponPackAPunchState.Upgraded, 20, 160)],
            false,
            ["jug"]);
        return new(
            1, "2.1.1", "session-local-001", 9, 1000, null, "session_gettime_and_file_mtime",
            "zm_tomb", 31, 100, TimeSpan.FromMinutes(42), RankedStatus.Unranked,
            RuntimePowerState.On, RuntimePackAPunchState.Available, 3, 18, 1, 1, true, [runtimePlayer]);
    }

    private sealed class FixedProvider(DashboardSnapshot value) : IControlCenterDataProvider
    {
        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(value);
    }

    private sealed class HeartbeatStub(LocalReadResult<PinteModHeartbeatSnapshot> value) : IPinteModHeartbeatReader
    {
        public Task<LocalReadResult<PinteModHeartbeatSnapshot>> ReadAsync(string? activeSessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(value);
    }

    private sealed class RuntimeStub(LocalReadResult<ControlCenterRuntimeSnapshot> value) : IControlCenterRuntimeSnapshotReader
    {
        public Task<LocalReadResult<ControlCenterRuntimeSnapshot>> ReadAsync(string? activeSessionId, string? activeMapCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(value);
    }

    private sealed class CapturingHeartbeatStub : IPinteModHeartbeatReader
    {
        public string? SessionId { get; private set; }
        public Task<LocalReadResult<PinteModHeartbeatSnapshot>> ReadAsync(string? activeSessionId, CancellationToken cancellationToken = default)
        {
            SessionId = activeSessionId;
            return Task.FromResult(new LocalReadResult<PinteModHeartbeatSnapshot>(null, LocalSourceMetadata.Unavailable("absent"), null));
        }
    }

    private sealed class CapturingRuntimeStub : IControlCenterRuntimeSnapshotReader
    {
        public string? SessionId { get; private set; }
        public string? MapCode { get; private set; }
        public Task<LocalReadResult<ControlCenterRuntimeSnapshot>> ReadAsync(string? activeSessionId, string? activeMapCode, CancellationToken cancellationToken = default)
        {
            SessionId = activeSessionId;
            MapCode = activeMapCode;
            return Task.FromResult(new LocalReadResult<ControlCenterRuntimeSnapshot>(null, LocalSourceMetadata.Unavailable("absent"), null));
        }
    }
}
