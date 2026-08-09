using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.State;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PlayerAdministrationViewModelTests
{
    private const string Xuid = "1234567890abcdef";
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27018, TimeSpan.FromSeconds(3));

    [TestMethod]
    public async Task ConfirmedLocalPlayerAction_RevalidatesXuidAndLocksEveryPlayerPage()
    {
        var snapshot = HybridSnapshot(includePlayer: true);
        var store = new SnapshotStore(snapshot, snapshot);
        var service = new CapturingPlayerAdministrationService();
        var safety = new OperatorMutationSafetyState();
        var coordinator = new OperatorRconOperationCoordinator();
        var players = CreatePlayers(store, service, safety, coordinator);
        var dashboard = new DashboardViewModel(
            store,
            new SimulationActionService(),
            new PlayerSelectionState(),
            service,
            new ConfirmationService(true),
            () => Endpoint,
            rconOperations: coordinator,
            mutationSafety: safety);
        var server = new ServerViewModel(
            store,
            new SimulationActionService(),
            confirmationService: new ConfirmationService(true),
            rconEndpointFactory: () => Endpoint,
            rconOperations: coordinator,
            serverAdministrationCommandService: new CapturingServerAdministrationService(),
            mutationSafety: safety);
        await players.InitializeAsync();
        await dashboard.InitializeAsync();
        await server.InitializeAsync();

        players.PlayerActionCommand.Execute(SimulationAction.RefillAmmo);
        await WaitForCommandAsync(players.PlayerActionCommand);

        Assert.AreEqual(1, store.RefreshCount);
        Assert.AreEqual(PlayerAdministrationAction.RefillAmmo, service.Request?.Action);
        Assert.AreEqual(Xuid, service.Request?.TargetXuid);
        Assert.AreEqual("ENVOYÉ · À VÉRIFIER", players.PlayerAdministrationStatus);
        Assert.AreEqual("Commande envoyée : Oui", players.PlayerAdministrationCommandSent);
        Assert.IsFalse(players.PlayerActionCommand.CanExecute(SimulationAction.RefillAmmo));
        Assert.IsFalse(dashboard.PlayerActionCommand.CanExecute(SimulationAction.RefillAmmo));
        Assert.IsFalse(server.CanRunServerAdministration);

        players.AcknowledgePlayerAdministrationCommand.Execute(null);
        await WaitForCommandAsync(players.AcknowledgePlayerAdministrationCommand);

        Assert.IsTrue(players.PlayerActionCommand.CanExecute(SimulationAction.RefillAmmo));
        Assert.IsTrue(dashboard.PlayerActionCommand.CanExecute(SimulationAction.RefillAmmo));
        Assert.IsTrue(server.CanRunServerAdministration);

        server.EnablePowerCommand.Execute(null);
        await WaitForCommandAsync(server.EnablePowerCommand);

        Assert.IsFalse(players.PlayerActionCommand.CanExecute(SimulationAction.RefillAmmo));
        Assert.IsFalse(dashboard.PlayerActionCommand.CanExecute(SimulationAction.RefillAmmo));
    }

    [TestMethod]
    public async Task PlayerMissingAfterConfirmation_IsRejectedWithoutTransport()
    {
        var store = new SnapshotStore(HybridSnapshot(true), HybridSnapshot(false));
        var service = new CapturingPlayerAdministrationService();
        var viewModel = CreatePlayers(
            store,
            service,
            new OperatorMutationSafetyState(),
            new OperatorRconOperationCoordinator());
        await viewModel.InitializeAsync();

        viewModel.PlayerActionCommand.Execute(SimulationAction.RevivePlayer);
        await WaitForCommandAsync(viewModel.PlayerActionCommand);

        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual("AUTORISATION EXPIRÉE", viewModel.PlayerAdministrationStatus);
        Assert.AreEqual("Commande envoyée : Non", viewModel.PlayerAdministrationCommandSent);
    }

    [TestMethod]
    public async Task RefusedConfirmation_NeverRefreshesOrSends()
    {
        var snapshot = HybridSnapshot(true);
        var store = new SnapshotStore(snapshot, snapshot);
        var service = new CapturingPlayerAdministrationService();
        var viewModel = new PlayersViewModel(
            store,
            new SimulationActionService(),
            new PlayerSelectionState(),
            service,
            new ConfirmationService(false),
            () => Endpoint);
        await viewModel.InitializeAsync();

        viewModel.PlayerActionCommand.Execute(SimulationAction.ToggleGodmode);
        await WaitForCommandAsync(viewModel.PlayerActionCommand);

        Assert.AreEqual(0, store.RefreshCount);
        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual("ANNULÉ", viewModel.PlayerAdministrationStatus);
    }

    [TestMethod]
    public async Task HybridMode_DisablesUnsupportedActionsAndNeverFallsBackToSimulation()
    {
        var snapshot = HybridSnapshot(true);
        var viewModel = CreatePlayers(
            new SnapshotStore(snapshot, snapshot),
            new CapturingPlayerAdministrationService(),
            new OperatorMutationSafetyState(),
            new OperatorRconOperationCoordinator());
        await viewModel.InitializeAsync();

        Assert.IsTrue(viewModel.PlayerActionCommand.CanExecute(SimulationAction.TeleportPlayer));
        Assert.IsFalse(viewModel.PlayerActionCommand.CanExecute(SimulationAction.ViewHistory));
        Assert.IsTrue(viewModel.PlayerActionCommand.CanExecute(SimulationAction.GiveWeapon));
        Assert.IsTrue(viewModel.PlayerActionCommand.CanExecute(SimulationAction.GivePowerUpPlayer));
        Assert.IsTrue(viewModel.PlayerActionCommand.CanExecute(SimulationAction.BanPlayer));
        StringAssert.Contains(viewModel.PlayerActionsBadge, "XUID");
    }

    [TestMethod]
    public async Task PowerUpAction_UsesSelectedClosedAliasAndXuidTarget()
    {
        var snapshot = HybridSnapshot(true);
        var store = new SnapshotStore(snapshot, snapshot);
        var service = new CapturingPlayerAdministrationService();
        var viewModel = CreatePlayers(
            store,
            service,
            new OperatorMutationSafetyState(),
            new OperatorRconOperationCoordinator());
        await viewModel.InitializeAsync();
        viewModel.SelectedPowerUp = viewModel.PowerUpOptions.Single(option => option.Key == "nuke");

        viewModel.PlayerActionCommand.Execute(SimulationAction.GivePowerUpPlayer);
        await WaitForCommandAsync(viewModel.PlayerActionCommand);

        Assert.AreEqual(PlayerAdministrationAction.GivePowerUp, service.Request?.Action);
        Assert.AreEqual("nuke", service.Request?.Option);
        Assert.AreEqual(Xuid, service.Request?.TargetXuid);
    }

    [TestMethod]
    public async Task PublicViewModelStrings_ContainOnlyAbbreviatedXuid()
    {
        var snapshot = HybridSnapshot(true);
        var viewModel = CreatePlayers(
            new SnapshotStore(snapshot, snapshot),
            new CapturingPlayerAdministrationService(),
            new OperatorMutationSafetyState(),
            new OperatorRconOperationCoordinator());
        await viewModel.InitializeAsync();

        var publicStrings = viewModel.GetType()
            .GetProperties()
            .Where(property => property.PropertyType == typeof(string) && property.GetMethod?.IsPublic == true)
            .Select(property => property.GetValue(viewModel) as string)
            .Where(value => value is not null)
            .Cast<string>()
            .Concat(new[] { viewModel.SelectedPlayer!.ShortXuid });

        Assert.IsFalse(publicStrings.Any(value => value.Contains(Xuid, StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual("1234…cdef", viewModel.SelectedPlayer!.ShortXuid);
    }

    [TestMethod]
    public async Task LocalHistory_IsReadBySelectedXuidWithoutExposingIt()
    {
        var snapshot = HybridSnapshot(true);
        var reader = new CapturingHistoryReader();
        var viewModel = new PlayersViewModel(
            new SnapshotStore(snapshot, snapshot),
            new SimulationActionService(),
            new PlayerSelectionState(),
            playerHistoryReader: reader);
        await viewModel.InitializeAsync();

        Assert.IsTrue(viewModel.LoadPlayerHistoryCommand.CanExecute(null));
        viewModel.LoadPlayerHistoryCommand.Execute(null);
        await WaitForCommandAsync(viewModel.LoadPlayerHistoryCommand);

        Assert.AreEqual(Xuid, reader.RequestedXuid);
        Assert.AreEqual("HISTORIQUE LOCAL · READ-ONLY", viewModel.PlayerHistoryStatus);
        StringAssert.Contains(viewModel.PlayerHistoryCounters, "Kicks 2");
        Assert.IsFalse(viewModel.PlayerHistoryLastAction.Contains(Xuid, StringComparison.OrdinalIgnoreCase));
    }

    private static PlayersViewModel CreatePlayers(
        IControlCenterSnapshotStore store,
        IPlayerAdministrationCommandService service,
        OperatorMutationSafetyState safety,
        IOperatorRconOperationCoordinator coordinator) => new(
        store,
        new SimulationActionService(),
        new PlayerSelectionState(),
        service,
        new ConfirmationService(true),
        () => Endpoint,
        rconOperations: coordinator,
        mutationSafety: safety);

    private static DashboardSnapshot HybridSnapshot(bool includePlayer)
    {
        var baseline = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var source = new LocalSourceMetadata(
            LocalReadStatus.Success,
            DataFreshness.Fresh,
            TimeSpan.FromSeconds(1),
            DataProvenance.LocalFile,
            "source locale",
            "Lecture locale réussie.");
        var players = includePlayer
            ? new[]
            {
                new PlayerState(
                    0,
                    Xuid,
                    "Alice",
                    "user",
                    "fr",
                    "FR",
                    PlayerLifeState.Unknown,
                    0,
                    TimeSpan.FromMinutes(2),
                    false,
                    false)
                {
                    LifeStateAvailable = false,
                    PointsAvailable = false,
                    Provenance = DataProvenance.LocalFile
                }
            }
            : [];
        var logs = new StructuredLogSnapshot(
            "session",
            [],
            players,
            5,
            TimeSpan.FromMinutes(3),
            RankedStatus.Ranked,
            true,
            source,
            1,
            0,
            0,
            0);
        return baseline with
        {
            Players = players,
            DataContext = new SnapshotDataContext(
                ControlCenterDataMode.HybridLocal,
                "MODE HYBRIDE LOCAL",
                null,
                source,
                []),
            LocalObservation = baseline.LocalObservation with { Logs = logs }
        };
    }

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        for (var attempt = 0; attempt < 100 && command.IsExecuting; attempt++)
        {
            await Task.Delay(10);
        }
    }

    private static async Task WaitForCommandAsync<T>(AsyncRelayCommand<T> command)
    {
        for (var attempt = 0; attempt < 100 && command.IsExecuting; attempt++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class SnapshotStore(DashboardSnapshot current, DashboardSnapshot refreshed)
        : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current { get; private set; } = current;

        public int RefreshCount { get; private set; }

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            Current = refreshed;
            return Task.FromResult(refreshed);
        }
    }

    private sealed class ConfirmationService(bool answer) : IOperatorConfirmationService
    {
        public Task<bool> ConfirmAsync(
            OperatorConfirmationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(answer);
    }

    private sealed class CapturingPlayerAdministrationService : IPlayerAdministrationCommandService
    {
        public int CallCount { get; private set; }

        public PlayerAdministrationRequest? Request { get; private set; }

        public Task<PlayerAdministrationExecutionResult> ExecuteAsync(
            PlayerAdministrationRequest request,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(new PlayerAdministrationExecutionResult(
                request,
                PlayerAdministrationExecutionStatus.SentAwaitingManualVerification,
                "Commande transmise.",
                true,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class CapturingServerAdministrationService : IServerAdministrationCommandService
    {
        public Task<ServerAdministrationExecutionResult> ExecuteAsync(
            ServerAdministrationRequest request,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServerAdministrationExecutionResult(
                request,
                ServerAdministrationExecutionStatus.SentAwaitingManualVerification,
                "Commande transmise.",
                true,
                DateTimeOffset.UtcNow));
    }

    private sealed class CapturingHistoryReader : IPlayerModerationHistoryReader
    {
        public string? RequestedXuid { get; private set; }

        public Task<LocalReadResult<PlayerModerationHistory>> ReadAsync(
            string xuid,
            CancellationToken cancellationToken = default)
        {
            RequestedXuid = xuid;
            return Task.FromResult(new LocalReadResult<PlayerModerationHistory>(
                new PlayerModerationHistory(2, 1, 0, 0, 0, "kick", "Test local"),
                new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    TimeSpan.Zero,
                    DataProvenance.LocalFile,
                    "source neutralisée",
                    "Lecture réussie."),
                DateTimeOffset.UtcNow));
        }
    }
}
