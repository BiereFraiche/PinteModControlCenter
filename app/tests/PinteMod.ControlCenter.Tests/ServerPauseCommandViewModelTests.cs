using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ServerPauseCommandViewModelTests
{
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27018, TimeSpan.FromSeconds(3));

    [TestMethod]
    public async Task FreshLiveStatus_EnablesOnlyActionMatchingCurrentState()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow);
        var viewModel = CreateViewModel(new SequencedSnapshotStore(snapshot, snapshot));

        await viewModel.InitializeAsync();

        Assert.IsTrue(viewModel.CanPauseServer);
        Assert.IsFalse(viewModel.CanResumeServer);
        Assert.IsTrue(viewModel.PauseServerCommand.CanExecute(null));
        Assert.IsFalse(viewModel.ResumeServerCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ConfirmedPause_IsSentOnceAndRequiresFreshObservedState()
    {
        var before = DateTimeOffset.UtcNow;
        var initial = Snapshot(active: false, before);
        var updated = Snapshot(active: true, before.AddSeconds(1));
        var store = new SequencedSnapshotStore(initial, initial, updated);
        var service = new CapturingPauseService();
        var confirmation = new StubConfirmationService(true);
        var viewModel = CreateViewModel(store, service, confirmation);
        await viewModel.InitializeAsync();

        viewModel.PauseServerCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PauseServerCommand);

        Assert.AreEqual(CommunityPauseAction.Pause, service.Action);
        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("CONFIRMÉ PAR LE STATUT LOCAL", viewModel.PauseCommandStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.PauseCommandSent);
        Assert.AreEqual(ServiceHealth.Healthy, viewModel.PauseCommandHealth);
        Assert.IsFalse(viewModel.CanPauseServer);
        Assert.IsTrue(viewModel.CanResumeServer);
    }

    [TestMethod]
    public async Task RefusedConfirmation_NeverCallsCommandService()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow);
        var service = new CapturingPauseService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(snapshot, snapshot),
            service,
            new StubConfirmationService(false));
        await viewModel.InitializeAsync();

        viewModel.PauseServerCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PauseServerCommand);

        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual("ANNULÉ", viewModel.PauseCommandStatus);
        Assert.AreEqual("Commande envoyée : Non", viewModel.PauseCommandSent);
    }

    [TestMethod]
    public async Task StatusChangedDuringConfirmation_ExpiresAuthorizationWithoutSending()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var initial = Snapshot(active: false, timestamp);
        var changed = Snapshot(active: true, timestamp.AddSeconds(1));
        var service = new CapturingPauseService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(initial, changed),
            service,
            new StubConfirmationService(true));
        await viewModel.InitializeAsync();

        viewModel.PauseServerCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PauseServerCommand);

        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual("AUTORISATION EXPIRÉE", viewModel.PauseCommandStatus);
        Assert.AreEqual("Commande envoyée : Non", viewModel.PauseCommandSent);
    }

    [TestMethod]
    public async Task DeliveryUnknown_BlocksMutationUntilNewerFreshStatusIsObserved()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var initial = Snapshot(active: false, timestamp);
        var updated = Snapshot(active: true, timestamp.AddSeconds(1));
        var service = new CapturingPauseService(CommunityPauseExecutionStatus.DeliveryUnknown);
        var diagnostic = new CapturingDiagnosticService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(initial, initial, updated),
            service,
            rconDiagnosticService: diagnostic);
        await viewModel.InitializeAsync();

        viewModel.PauseServerCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PauseServerCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.IsFalse(viewModel.CanPauseServer);
        Assert.IsFalse(viewModel.CanResumeServer);
        StringAssert.Contains(viewModel.RealPauseControlsNotice, "résultat précédent incertain");

        viewModel.RefreshPauseStatusCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RefreshPauseStatusCommand);

        Assert.AreEqual("STATUT ACTUALISÉ", viewModel.PauseCommandStatus);
        Assert.IsTrue(viewModel.CanResumeServer);
    }

    [TestMethod]
    public async Task TransportFailureAfterPossibleEmission_BlocksMutationOnOldSnapshot()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var initial = Snapshot(active: false, timestamp);
        var service = new CapturingPauseService(CommunityPauseExecutionStatus.TransportError);
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(initial, initial),
            service,
            new StubConfirmationService(true));
        await viewModel.InitializeAsync();

        viewModel.PauseServerCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PauseServerCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("RÉSULTAT INCERTAIN", viewModel.PauseCommandStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.PauseCommandSent);
        Assert.IsFalse(viewModel.CanPauseServer);
        Assert.IsFalse(viewModel.CanResumeServer);
        StringAssert.Contains(viewModel.RealPauseControlsNotice, "résultat précédent incertain");
    }

    [TestMethod]
    public async Task UnnormalizedTransportException_StillBlocksMutationOnOldSnapshot()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var initial = Snapshot(active: false, timestamp);
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(initial, initial),
            new ThrowingPauseService(),
            new StubConfirmationService(true));
        await viewModel.InitializeAsync();

        viewModel.PauseServerCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PauseServerCommand);

        Assert.AreEqual("RÉSULTAT INCERTAIN", viewModel.PauseCommandStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.PauseCommandSent);
        Assert.IsFalse(viewModel.CanPauseServer);
        Assert.IsFalse(viewModel.CanResumeServer);
    }

    [TestMethod]
    public async Task SentButNotObserved_KeepsMutationsBlockedOnOldSnapshot()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var initial = Snapshot(active: false, timestamp);
        var service = new CapturingPauseService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(initial, initial),
            service,
            new StubConfirmationService(true));
        await viewModel.InitializeAsync();

        viewModel.PauseServerCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PauseServerCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual("ENVOYÉ · NON CONFIRMÉ", viewModel.PauseCommandStatus);
        Assert.IsFalse(viewModel.CanPauseServer);
        Assert.IsFalse(viewModel.CanResumeServer);
        StringAssert.Contains(viewModel.RealPauseControlsNotice, "résultat précédent incertain");
    }

    [TestMethod]
    public async Task StalePauseSource_KeepsBothRealCommandsDisabled()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow, DataFreshness.Stale);
        var viewModel = CreateViewModel(new SequencedSnapshotStore(snapshot, snapshot));

        await viewModel.InitializeAsync();

        Assert.IsFalse(viewModel.CanPauseServer);
        Assert.IsFalse(viewModel.CanResumeServer);
        StringAssert.Contains(viewModel.RealPauseControlsNotice, "source serveur live");
    }

    [TestMethod]
    public async Task ManualStatusRefresh_UsesOnlyPauseStatusDiagnostic_AndEnablesResume()
    {
        var before = DateTimeOffset.UtcNow;
        var initial = Snapshot(active: true, before, DataFreshness.Expired);
        var refreshed = Snapshot(active: true, before.AddSeconds(1));
        var store = new SequencedSnapshotStore(initial, refreshed);
        var pauseService = new CapturingPauseService();
        var diagnostic = new CapturingDiagnosticService();
        var viewModel = CreateViewModel(store, pauseService, rconDiagnosticService: diagnostic);
        await viewModel.InitializeAsync();

        viewModel.RefreshPauseStatusCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RefreshPauseStatusCommand);

        Assert.AreEqual(1, diagnostic.CallCount);
        Assert.AreEqual(RconDiagnosticCommand.PauseStatus, diagnostic.Command);
        Assert.AreEqual(0, pauseService.CallCount);
        Assert.AreEqual("STATUT ACTUALISÉ", viewModel.PauseCommandStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.PauseCommandSent);
        Assert.IsTrue(viewModel.CanResumeServer);
    }

    [TestMethod]
    public async Task MapDiagnostic_UsesTypedReadOnlyCommandAndReportsResult()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow);
        var diagnostic = new CapturingDiagnosticService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(snapshot, snapshot),
            rconDiagnosticService: diagnostic);
        await viewModel.InitializeAsync();

        viewModel.MapDiagnosticCommand.Execute(null);
        await WaitForCommandAsync(viewModel.MapDiagnosticCommand);

        Assert.AreEqual(1, diagnostic.CallCount);
        Assert.AreEqual(RconDiagnosticCommand.MapInfo, diagnostic.Command);
        Assert.AreEqual("ENVOYÉ · SANS TEXTE", viewModel.ServerDiagnosticStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.ServerDiagnosticCommandSent);
        Assert.AreEqual(ServiceHealth.Warning, viewModel.ServerDiagnosticHealth);
    }

    [TestMethod]
    public async Task ConfirmedServerMutation_BlocksAllMutationsUntilConsoleAcknowledgement()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow);
        var service = new CapturingServerAdministrationService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(snapshot, snapshot),
            serverAdministrationService: service);
        await viewModel.InitializeAsync();

        viewModel.EnablePowerCommand.Execute(null);
        await WaitForCommandAsync(viewModel.EnablePowerCommand);

        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual(ServerAdministrationAction.EnablePower, service.Request?.Action);
        Assert.AreEqual("ENVOYÉ · À VÉRIFIER", viewModel.ServerAdministrationStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.ServerAdministrationCommandSent);
        Assert.IsFalse(viewModel.CanRunServerAdministration);
        Assert.IsFalse(viewModel.CanPauseServer);
        Assert.IsTrue(viewModel.AcknowledgeServerAdministrationCommand.CanExecute(null));

        viewModel.AcknowledgeServerAdministrationCommand.Execute(null);
        await WaitForCommandAsync(viewModel.AcknowledgeServerAdministrationCommand);

        Assert.IsTrue(viewModel.CanRunServerAdministration);
        Assert.IsTrue(viewModel.CanPauseServer);
        Assert.AreEqual("VÉRIFICATION MANUELLE CONFIRMÉE", viewModel.ServerAdministrationStatus);
    }

    [TestMethod]
    public async Task RefusedServerMutationConfirmation_NeverCallsService()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow);
        var service = new CapturingServerAdministrationService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(snapshot, snapshot),
            confirmation: new StubConfirmationService(false),
            serverAdministrationService: service);
        await viewModel.InitializeAsync();

        viewModel.NextRoundCommand.Execute(null);
        await WaitForCommandAsync(viewModel.NextRoundCommand);

        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual("ANNULÉ", viewModel.ServerAdministrationStatus);
        Assert.AreEqual("Commande envoyée : Non", viewModel.ServerAdministrationCommandSent);
    }

    [TestMethod]
    public async Task SetRound_UsesOnlySelectedPredeterminedTarget()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow);
        var service = new CapturingServerAdministrationService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(snapshot, snapshot),
            serverAdministrationService: service);
        await viewModel.InitializeAsync();
        viewModel.SelectedRound = 50;

        viewModel.SetRoundCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SetRoundCommand);

        Assert.AreEqual(ServerAdministrationAction.SetRound, service.Request?.Action);
        Assert.AreEqual(50, service.Request?.TargetRound);
    }

    [TestMethod]
    public async Task UnnormalizedServerMutationFailure_IsConservativelyLocked()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow);
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(snapshot, snapshot),
            serverAdministrationService: new ThrowingServerAdministrationService());
        await viewModel.InitializeAsync();

        viewModel.EnablePackAPunchCommand.Execute(null);
        await WaitForCommandAsync(viewModel.EnablePackAPunchCommand);

        Assert.AreEqual("RÉSULTAT INCERTAIN", viewModel.ServerAdministrationStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.ServerAdministrationCommandSent);
        Assert.IsFalse(viewModel.CanRunServerAdministration);
        Assert.IsFalse(viewModel.CanPauseServer);
    }

    [TestMethod]
    public async Task AdditionalServerAction_UsesTypedMusicRequest()
    {
        var snapshot = Snapshot(active: false, DateTimeOffset.UtcNow);
        var service = new CapturingServerAdministrationService();
        var viewModel = CreateViewModel(
            new SequencedSnapshotStore(snapshot, snapshot),
            serverAdministrationService: service);
        await viewModel.InitializeAsync();

        viewModel.PlayMapMusicCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PlayMapMusicCommand);

        Assert.AreEqual(ServerAdministrationAction.PlayMapMusic, service.Request?.Action);
        Assert.IsNull(service.Request?.TargetRound);
        Assert.IsFalse(viewModel.CanRunServerAdministration);
    }

    private static ServerViewModel CreateViewModel(
        IControlCenterSnapshotStore store,
        ICommunityPauseCommandService? service = null,
        IOperatorConfirmationService? confirmation = null,
        IRconDiagnosticService? rconDiagnosticService = null,
        IServerAdministrationCommandService? serverAdministrationService = null) => new(
            store,
            new SimulationActionService(),
            service ?? new CapturingPauseService(),
            confirmation ?? new StubConfirmationService(true),
            () => Endpoint,
            rconDiagnosticService: rconDiagnosticService,
            serverAdministrationCommandService: serverAdministrationService);

    private static DashboardSnapshot Snapshot(
        bool active,
        DateTimeOffset timestamp,
        DataFreshness freshness = DataFreshness.Fresh)
    {
        var pause = new CommunityPauseStatusSnapshot(
            "0.3", 1000, active, active ? 120 : 0, 0, 2, 0,
            "Aucun", null, null, null, active, active, active);
        var metadata = new LocalSourceMetadata(
            LocalReadStatus.Success,
            freshness,
            TimeSpan.FromSeconds(1),
            DataProvenance.LocalFile,
            "remote/feedback.latest.txt",
            "Statut lu.");
        return SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy) with
        {
            LocalObservation = BlockALocalSnapshot.Simulation with
            {
                CommunityPause = new LocalReadResult<CommunityPauseStatusSnapshot>(pause, metadata, timestamp)
            }
        };
    }

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (command.IsExecuting && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(command.IsExecuting);
    }

    private sealed class SequencedSnapshotStore : IControlCenterSnapshotStore
    {
        private readonly DashboardSnapshot[] _snapshots;
        private int _nextIndex = 1;

        public SequencedSnapshotStore(params DashboardSnapshot[] snapshots)
        {
            _snapshots = snapshots;
            Current = snapshots[0];
        }

        public DashboardSnapshot? Current { get; private set; }

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
        {
            Current = _snapshots[Math.Min(_nextIndex, _snapshots.Length - 1)];
            _nextIndex++;
            return Task.FromResult(Current);
        }
    }

    private sealed class CapturingPauseService(
        CommunityPauseExecutionStatus status = CommunityPauseExecutionStatus.SentAwaitingObservation) : ICommunityPauseCommandService
    {
        public CommunityPauseAction? Action { get; private set; }

        public int CallCount { get; private set; }

        public Task<CommunityPauseExecutionResult> ExecuteAsync(
            CommunityPauseAction action,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            Action = action;
            CallCount++;
            return Task.FromResult(new CommunityPauseExecutionResult(
                action,
                status,
                "Commande transmise.",
                true,
                status == CommunityPauseExecutionStatus.SentAwaitingObservation,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class StubConfirmationService(bool answer) : IOperatorConfirmationService
    {
        public Task<bool> ConfirmAsync(
            OperatorConfirmationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(answer);
    }

    private sealed class ThrowingPauseService : ICommunityPauseCommandService
    {
        public Task<CommunityPauseExecutionResult> ExecuteAsync(
            CommunityPauseAction action,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CommunityPauseExecutionResult>(
                new System.Net.Sockets.SocketException(
                    (int)System.Net.Sockets.SocketError.ConnectionReset));
    }

    private sealed class CapturingDiagnosticService : IRconDiagnosticService
    {
        public RconDiagnosticCommand? Command { get; private set; }

        public int CallCount { get; private set; }

        public Task<RconExecutionResult> ExecuteAsync(
            RconDiagnosticCommand command,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            CallCount++;
            return Task.FromResult(new RconExecutionResult(
                command,
                RconExecutionStatus.EmptyResponse,
                "Réponse vide.",
                true,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class CapturingServerAdministrationService : IServerAdministrationCommandService
    {
        public ServerAdministrationRequest? Request { get; private set; }

        public int CallCount { get; private set; }

        public Task<ServerAdministrationExecutionResult> ExecuteAsync(
            ServerAdministrationRequest request,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            CallCount++;
            return Task.FromResult(new ServerAdministrationExecutionResult(
                request,
                ServerAdministrationExecutionStatus.SentAwaitingManualVerification,
                "Commande transmise · vérifiez la console.",
                true,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class ThrowingServerAdministrationService : IServerAdministrationCommandService
    {
        public Task<ServerAdministrationExecutionResult> ExecuteAsync(
            ServerAdministrationRequest request,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ServerAdministrationExecutionResult>(
                new System.Net.Sockets.SocketException(
                    (int)System.Net.Sockets.SocketError.ConnectionReset));
    }
}
