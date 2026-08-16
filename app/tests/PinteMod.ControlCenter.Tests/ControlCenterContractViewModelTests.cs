using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.State;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ControlCenterContractViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 0, 30, 0, TimeSpan.Zero);
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27018, TimeSpan.FromSeconds(3));
    private const string Xuid = "0000000000000001";

    [TestMethod]
    public async Task RestartMap_SessionChangeAfterConfirmationCancelsBeforeTransport()
    {
        var baseline = Snapshot();
        var changed = Snapshot(sessionId: "session-local-002", mapCode: "zm_castle");
        var store = new DynamicSnapshotStore(baseline, _ => changed);
        var service = new CapturingService();
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();

        viewModel.RestartMapCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RestartMapCommand);

        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual("AUTORISATION EXPIRÉE", viewModel.ServerAdministrationStatus);
        Assert.IsTrue(viewModel.ServerAdministrationCommandSent.EndsWith("Non", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SpawnBoss_PlayerDisappearingAfterConfirmationCancelsBeforeTransport()
    {
        var baseline = Snapshot();
        var withoutPlayer = Snapshot(players: []);
        var selection = new PlayerSelectionState();
        selection.Select(Xuid);
        var store = new DynamicSnapshotStore(baseline, _ => withoutPlayer);
        var service = new CapturingService();
        var viewModel = CreateViewModel(store, service, selection);
        await viewModel.InitializeAsync();
        Assert.IsTrue(viewModel.CanSpawnBoss);

        viewModel.SpawnBossCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SpawnBossCommand);

        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual("AUTORISATION EXPIRÉE", viewModel.ServerAdministrationStatus);
    }

    [TestMethod]
    public async Task SetHostname_RequiresCorrelatedAppliedFeedbackAndNewerIdentityRevision()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var refresh = 0;
        var store = new DynamicSnapshotStore(baseline, _ =>
        {
            refresh++;
            if (refresh == 1 || service?.Request is null)
            {
                return baseline;
            }

            return Snapshot(
                feedback: Feedback(service.Request, ControlCenterFeedbackStatus.Applied, "success"),
                identity: Identity("Nouveau Serveur", revision: 8, joinPasswordEnabled: true));
        });
        service = new CapturingService();
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();
        viewModel.RequestedHostname = "Nouveau Serveur";

        viewModel.SetHostnameCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SetHostnameCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual(ServerAdministrationAction.SetHostname, service.Request?.Action);
        Assert.AreEqual("APPLIQUÉ · CONFIRMÉ LOCALEMENT", viewModel.ServerAdministrationStatus);
        Assert.AreEqual(ServiceHealth.Healthy, viewModel.ServerAdministrationHealth);
        Assert.IsTrue(viewModel.CanRunServerAdministration);
    }

    [TestMethod]
    public async Task SlowRestartRemainsUnconfirmedAndBlockedButIsNeverReportedAsFailure()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var store = new DynamicSnapshotStore(baseline, _ => service?.Request is null
            ? baseline
            : Snapshot(
                feedback: Feedback(service.Request, ControlCenterFeedbackStatus.Accepted, "accepted"),
                transition: Transition(service.Request, ControlCenterTransitionStatus.Transitioning, "transition_started")));
        service = new CapturingService();
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();

        viewModel.RestartMapCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RestartMapCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("ENVOYÉ · NON CONFIRMÉ", viewModel.ServerAdministrationStatus);
        Assert.AreEqual(ServiceHealth.Warning, viewModel.ServerAdministrationHealth);
        Assert.IsFalse(viewModel.CanRunServerAdministration);
        Assert.IsFalse(viewModel.ServerAdministrationStatus.Contains("ÉCHEC", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RestartMap_IsConfirmedOnlyByActiveTransitionAndNewSession()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var refresh = 0;
        var store = new DynamicSnapshotStore(baseline, _ =>
        {
            refresh++;
            if (refresh == 1 || service?.Request is null)
            {
                return baseline;
            }

            return Snapshot(
                sessionId: "session-local-002",
                feedback: Feedback(
                    service.Request,
                    ControlCenterFeedbackStatus.Applied,
                    "success",
                    "session-local-002"),
                transition: Transition(
                    service.Request,
                    ControlCenterTransitionStatus.Active,
                    "success",
                    "session-local-002"));
        });
        service = new CapturingService();
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();

        viewModel.RestartMapCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RestartMapCommand);

        Assert.AreEqual("APPLIQUÉ · CONFIRMÉ LOCALEMENT", viewModel.ServerAdministrationStatus);
        Assert.AreEqual(ServiceHealth.Healthy, viewModel.ServerAdministrationHealth);
    }

    [TestMethod]
    public async Task RestartMap_DeliveryUnknown_IsConfirmedByActiveTransitionAndNewSession()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var refresh = 0;
        var store = new DynamicSnapshotStore(baseline, _ =>
        {
            refresh++;
            if (refresh == 1 || service?.Request is null)
            {
                return baseline;
            }

            return Snapshot(
                sessionId: "session-local-002",
                feedback: Feedback(
                    service.Request,
                    ControlCenterFeedbackStatus.Applied,
                    "success",
                    "session-local-002"),
                transition: Transition(
                    service.Request,
                    ControlCenterTransitionStatus.Active,
                    "success",
                    "session-local-002"));
        });
        service = new CapturingService(ServerAdministrationExecutionStatus.DeliveryUnknown);
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();

        viewModel.RestartMapCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RestartMapCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("APPLIQUÉ · CONFIRMÉ LOCALEMENT", viewModel.ServerAdministrationStatus);
        Assert.AreEqual(ServiceHealth.Healthy, viewModel.ServerAdministrationHealth);
    }

    [TestMethod]
    public async Task RestartMap_DeliveryUnknownWithoutLocalProof_RemainsUnconfirmedAndLocked()
    {
        var baseline = Snapshot();
        var service = new CapturingService(ServerAdministrationExecutionStatus.DeliveryUnknown);
        var viewModel = CreateViewModel(new DynamicSnapshotStore(baseline, _ => baseline), service);
        await viewModel.InitializeAsync();

        viewModel.RestartMapCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RestartMapCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("ENVOYÉ · NON CONFIRMÉ", viewModel.ServerAdministrationStatus);
        Assert.AreEqual(ServiceHealth.Warning, viewModel.ServerAdministrationHealth);
        Assert.IsFalse(viewModel.CanRunServerAdministration);
    }

    [TestMethod]
    public async Task SpawnBoss_TransportError_IsConfirmedByCorrelatedAppliedFeedback()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var store = new DynamicSnapshotStore(baseline, _ => service?.Request is null
            ? baseline
            : Snapshot(feedback: Feedback(service.Request, ControlCenterFeedbackStatus.Applied, "success")));
        service = new CapturingService(ServerAdministrationExecutionStatus.TransportError);
        var selection = new PlayerSelectionState();
        selection.Select(Xuid);
        var viewModel = CreateViewModel(store, service, selection);
        await viewModel.InitializeAsync();

        viewModel.SpawnBossCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SpawnBossCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("APPLIQUÉ · CONFIRMÉ LOCALEMENT", viewModel.ServerAdministrationStatus);
    }

    [TestMethod]
    public async Task SetHostname_TransportError_IsConfirmedByFeedbackAndNewerRevision()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var store = new DynamicSnapshotStore(baseline, _ => service?.Request is null
            ? baseline
            : Snapshot(
                feedback: Feedback(service.Request, ControlCenterFeedbackStatus.Applied, "success"),
                identity: Identity("Nouveau Serveur", revision: 8, joinPasswordEnabled: true)));
        service = new CapturingService(ServerAdministrationExecutionStatus.TransportError);
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();
        viewModel.RequestedHostname = "Nouveau Serveur";

        viewModel.SetHostnameCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SetHostnameCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("APPLIQUÉ · CONFIRMÉ LOCALEMENT", viewModel.ServerAdministrationStatus);
    }

    [TestMethod]
    public async Task ClearJoinPassword_DeliveryUnknown_IsConfirmedByFeedbackAndNewerRevision()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var store = new DynamicSnapshotStore(baseline, _ => service?.Request is null
            ? baseline
            : Snapshot(
                feedback: Feedback(service.Request, ControlCenterFeedbackStatus.Applied, "success"),
                identity: Identity("PinteMod Test", revision: 8, joinPasswordEnabled: false)));
        service = new CapturingService(ServerAdministrationExecutionStatus.DeliveryUnknown);
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();

        viewModel.ClearJoinPasswordCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ClearJoinPasswordCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("APPLIQUÉ · CONFIRMÉ LOCALEMENT", viewModel.ServerAdministrationStatus);
        Assert.AreEqual(ServiceHealth.Healthy, viewModel.ServerAdministrationHealth);
    }

    [TestMethod]
    public async Task DuplicateRequestFeedback_IsPresentedAsConfirmedRefusalWithoutRetry()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var store = new DynamicSnapshotStore(baseline, _ => service?.Request is null
            ? baseline
            : Snapshot(feedback: Feedback(
                service.Request,
                ControlCenterFeedbackStatus.Rejected,
                "duplicate_request")));
        service = new CapturingService();
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();
        viewModel.RequestedHostname = "Nouveau Serveur";

        viewModel.SetHostnameCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SetHostnameCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("REFUS CONFIRMÉ PAR PINTE MOD", viewModel.ServerAdministrationStatus);
        StringAssert.Contains(viewModel.ServerAdministrationMessage, "duplicate_request");
    }

    [TestMethod]
    public async Task AppliedHostnameFeedback_WithStableRevisionRemainsUnconfirmedAndBlocked()
    {
        var baseline = Snapshot();
        CapturingService? service = null;
        var store = new DynamicSnapshotStore(baseline, _ => service?.Request is null
            ? baseline
            : Snapshot(
                feedback: Feedback(service.Request, ControlCenterFeedbackStatus.Applied, "success"),
                identity: Identity("Nouveau Serveur", revision: 7, joinPasswordEnabled: true)));
        service = new CapturingService();
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();
        viewModel.RequestedHostname = "Nouveau Serveur";

        viewModel.SetHostnameCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SetHostnameCommand);

        Assert.AreEqual("ENVOYÉ · NON CONFIRMÉ", viewModel.ServerAdministrationStatus);
        Assert.AreEqual(ServiceHealth.Warning, viewModel.ServerAdministrationHealth);
        Assert.IsFalse(viewModel.CanRunServerAdministration);
    }

    [TestMethod]
    public async Task ChangeMapRemainsInformativeAndSetPasswordRequiresLoopback()
    {
        var viewModel = CreateViewModel(new DynamicSnapshotStore(Snapshot(), _ => Snapshot()), new CapturingService());
        await viewModel.InitializeAsync();

        StringAssert.Contains(viewModel.ChangeMapContractNotice, "supported ne signifie pas installed");
        StringAssert.Contains(viewModel.SetJoinPasswordNotice, "machine serveur");
        Assert.IsTrue(viewModel.CanSetJoinPassword);
        Assert.IsTrue(viewModel.CanRestartMap);
        Assert.IsFalse(viewModel.SimulateServerActionCommand is null);
    }

    [TestMethod]
    public async Task SetJoinPassword_UsesNoBindableSecretAndRequiresFreshIdentityRevision()
    {
        var baseline = Snapshot(identity: Identity("PinteMod Test", 7, false));
        CapturingService? service = null;
        var store = new DynamicSnapshotStore(baseline, _ => service?.Request is null
            ? baseline
            : Snapshot(
                feedback: Feedback(service.Request, ControlCenterFeedbackStatus.Applied, "success"),
                identity: Identity("PinteMod Test", 8, true)));
        service = new CapturingService();
        var viewModel = CreateViewModel(store, service);
        await viewModel.InitializeAsync();

        await viewModel.SetJoinPasswordAsync("Safe#2026");

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual(ServerAdministrationAction.SetJoinPassword, service.Request?.Action);
        Assert.IsNull(service.Request?.Option);
        Assert.AreEqual("APPLIQUÉ · CONFIRMÉ LOCALEMENT", viewModel.ServerAdministrationStatus);
        Assert.IsFalse(viewModel.ServerAdministrationMessage.Contains("Safe#2026", StringComparison.Ordinal));
    }

    private static ServerViewModel CreateViewModel(
        IControlCenterSnapshotStore store,
        CapturingService service,
        PlayerSelectionState? selection = null) => new(
        store,
        new SimulationActionService(),
        confirmationService: new AcceptConfirmationService(),
        rconEndpointFactory: () => Endpoint,
        serverAdministrationCommandService: service,
        selectionState: selection,
        contractObservationInterval: TimeSpan.Zero,
        contractObservationAttempts: 2);

    private static DashboardSnapshot Snapshot(
        string sessionId = "session-local-001",
        string mapCode = "zm_tomb",
        IReadOnlyList<PlayerState>? players = null,
        ControlCenterActionFeedbackSnapshot? feedback = null,
        ControlCenterMapTransitionSnapshot? transition = null,
        ControlCenterServerIdentitySnapshot? identity = null)
    {
        var baseline = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        players ??=
        [
            new PlayerState(0, Xuid, "Joueur fictif", "player", "fr", "FR", PlayerLifeState.Alive,
                500, TimeSpan.FromMinutes(1), false, false)
        ];
        var contracts = new ControlCenterContractSnapshot(
            Read(Capabilities(sessionId, mapCode)),
            feedback is null ? Missing<ControlCenterActionFeedbackSnapshot>() : Read(feedback),
            transition is null ? Missing<ControlCenterMapTransitionSnapshot>() : Read(transition),
            Read(identity ?? Identity("PinteMod Test", 7, true, sessionId)));
        return baseline with
        {
            Server = baseline.Server with
            {
                SessionId = sessionId,
                MapCode = mapCode,
                MapName = mapCode,
                SessionProvenance = DataProvenance.LocalFile,
                MapProvenance = DataProvenance.LocalFile
            },
            Players = players,
            LocalObservation = BlockALocalSnapshot.Simulation with
            {
                ControlCenterContracts = contracts
            }
        };
    }

    private static ControlCenterCapabilitiesSnapshot Capabilities(string sessionId, string mapCode) => new(
        "2.1.1", "0.1.3", sessionId, 7, 25000, mapCode, true,
        [new SupportedMapCapability(mapCode, mapCode)],
        ["margwa"], ["max_ammo"], ["map_audit", "event_status", "power_ups"],
        "idle", true, true, true, "OFFICIAL", "SUPPORTED", "SUPPORTED", "SUPPORTED_MARGWA",
        "MARGWA", "SUPPORTED", "NOT_DECLARED", 0, 2);

    private static ControlCenterServerIdentitySnapshot Identity(
        string hostname,
        long revision,
        bool joinPasswordEnabled,
        string sessionId = "session-local-001") => new(
        sessionId, 3, 8000, hostname, PublicHostnameState.Observed, joinPasswordEnabled, revision);

    private static ControlCenterActionFeedbackSnapshot Feedback(
        ServerAdministrationRequest request,
        ControlCenterFeedbackStatus status,
        string resultCode,
        string sessionId = "session-local-001") => new(
        sessionId, 13, 32000, request.RequestId!, request.Action switch
        {
            ServerAdministrationAction.RestartMap => ControlCenterContractAction.RestartMap,
            ServerAdministrationAction.SpawnBoss => ControlCenterContractAction.SpawnBoss,
            ServerAdministrationAction.SetHostname => ControlCenterContractAction.SetHostname,
            ServerAdministrationAction.SetJoinPassword => ControlCenterContractAction.SetJoinPassword,
            _ => ControlCenterContractAction.ClearJoinPassword
        }, status, resultCode);

    private static ControlCenterMapTransitionSnapshot Transition(
        ServerAdministrationRequest request,
        ControlCenterTransitionStatus status,
        string resultCode,
        string? resultingSessionId = null) => new(
        request.RequestId!, "zm_tomb", "session-local-001", status, resultCode, 33000, resultingSessionId);

    private static LocalReadResult<T> Read<T>(T value) where T : class => new(
        value,
        new LocalSourceMetadata(
            LocalReadStatus.Success,
            DataFreshness.Fresh,
            TimeSpan.FromSeconds(1),
            DataProvenance.LocalFile,
            "contrat-local.json",
            "Lecture réussie."),
        Now.AddSeconds(-1));

    private static LocalReadResult<T> Missing<T>() where T : class => new(
        null,
        new LocalSourceMetadata(
            LocalReadStatus.Missing,
            DataFreshness.Unknown,
            null,
            DataProvenance.LocalFile,
            "contrat-local.json",
            "Fichier absent."),
        null);

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (command.IsExecuting && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(command.IsExecuting);
    }

    private sealed class DynamicSnapshotStore(
        DashboardSnapshot initial,
        Func<int, DashboardSnapshot> refresh) : IControlCenterSnapshotStore
    {
        private int _count;
        public DashboardSnapshot? Current { get; private set; } = initial;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
        {
            Current = refresh(++_count);
            return Task.FromResult(Current);
        }
    }

    private sealed class CapturingService(
        ServerAdministrationExecutionStatus status = ServerAdministrationExecutionStatus.SentAwaitingManualVerification,
        bool commandSent = true) : IServerAdministrationCommandService
    {
        public int CallCount { get; private set; }
        public ServerAdministrationRequest? Request { get; private set; }

        public Task<ServerAdministrationExecutionResult> ExecuteAsync(
            ServerAdministrationRequest request,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(new ServerAdministrationExecutionResult(
                request,
                status,
                "Commande transmise.",
                commandSent,
                Now));
        }

        public Task<ServerAdministrationExecutionResult> SetJoinPasswordAsync(
            string requestId,
            string joinPassword,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(
                new ServerAdministrationRequest(ServerAdministrationAction.SetJoinPassword, RequestId: requestId),
                endpoint,
                cancellationToken);
    }

    private sealed class AcceptConfirmationService : IOperatorConfirmationService
    {
        public Task<bool> ConfirmAsync(
            OperatorConfirmationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
