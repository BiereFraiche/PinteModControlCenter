using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.State;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PlayerWeaponActionsViewModelTests
{
    private const string Xuid = "1234567890abcdef";
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27018, TimeSpan.FromSeconds(3));

    [TestMethod]
    public async Task FreshMapRuntime_ExposesStandardAndOnlyCurrentMapSpecialWeapons()
    {
        var snapshot = Snapshot("zm_zod", RuntimeWeaponPackAPunchState.Base, "ray_gun");
        var viewModel = CreateViewModel(snapshot, new CapturingService());
        await viewModel.InitializeAsync();

        Assert.AreEqual(26, viewModel.WeaponOptions.Count);
        Assert.IsTrue(viewModel.WeaponOptions.Any(option => option.Key == "apothicon"));
        Assert.IsFalse(viewModel.WeaponOptions.Any(option => option.Key == "wunderwaffe"));
        Assert.IsFalse(viewModel.WeaponOptions.Any(option => option.Key == "rg"));
        StringAssert.Contains(viewModel.WeaponCatalogStatus, "zm_zod");
        StringAssert.Contains(viewModel.SourceSummary, "Runtime PinteMod local");
        Assert.AreEqual("1", viewModel.AlivePlayerCountDisplay);
    }

    [TestMethod]
    public async Task PackAPunchCurrentWeapon_UsesXuidAndLocksUntilManualAcknowledgement()
    {
        var snapshot = Snapshot("zm_tomb", RuntimeWeaponPackAPunchState.Base, "ray_gun");
        var service = new CapturingService();
        var viewModel = CreateViewModel(snapshot, service);
        await viewModel.InitializeAsync();

        Assert.IsTrue(viewModel.PlayerActionCommand.CanExecute(SimulationAction.PackAPunchCurrentWeapon));
        viewModel.PlayerActionCommand.Execute(SimulationAction.PackAPunchCurrentWeapon);
        await WaitForCommandAsync(viewModel.PlayerActionCommand);

        Assert.AreEqual(PlayerAdministrationAction.PackAPunchCurrentWeapon, service.Request?.Action);
        Assert.AreEqual(Xuid, service.Request?.TargetXuid);
        Assert.IsFalse(viewModel.PlayerActionCommand.CanExecute(SimulationAction.PackAPunchCurrentWeapon));

        viewModel.AcknowledgePlayerAdministrationCommand.Execute(null);
        await WaitForCommandAsync(viewModel.AcknowledgePlayerAdministrationCommand);
        Assert.IsTrue(viewModel.PlayerActionCommand.CanExecute(SimulationAction.PackAPunchCurrentWeapon));
    }

    [DataTestMethod]
    [DataRow(RuntimeWeaponPackAPunchState.Upgraded, "ray_gun")]
    [DataRow(RuntimeWeaponPackAPunchState.Base, "weapon_none")]
    public async Task PackAPunchButton_IsDisabledWhenRuntimeClearlyForbidsIt(
        RuntimeWeaponPackAPunchState state,
        string equippedWeapon)
    {
        var snapshot = Snapshot("zm_tomb", state, equippedWeapon);
        var viewModel = CreateViewModel(snapshot, new CapturingService());
        await viewModel.InitializeAsync();

        Assert.IsFalse(viewModel.PlayerActionCommand.CanExecute(SimulationAction.PackAPunchCurrentWeapon));
    }

    [TestMethod]
    public async Task RemovePerk_UsesSelectedClosedAlias()
    {
        var snapshot = Snapshot("zm_tomb", RuntimeWeaponPackAPunchState.Base, "ray_gun");
        var service = new CapturingService();
        var viewModel = CreateViewModel(snapshot, service);
        await viewModel.InitializeAsync();
        viewModel.SelectedPerk = viewModel.PerkOptions.Single(option => option.Key == "speed");

        viewModel.PlayerActionCommand.Execute(SimulationAction.RemovePerk);
        await WaitForCommandAsync(viewModel.PlayerActionCommand);

        Assert.AreEqual(PlayerAdministrationAction.RemovePerk, service.Request?.Action);
        Assert.AreEqual("speed", service.Request?.Option);
    }

    private static PlayersViewModel CreateViewModel(DashboardSnapshot snapshot, IPlayerAdministrationCommandService service) => new(
        new SnapshotStore(snapshot),
        new SimulationActionService(),
        new PlayerSelectionState(),
        service,
        new ConfirmationService(),
        () => Endpoint);

    private static DashboardSnapshot Snapshot(
        string mapCode,
        RuntimeWeaponPackAPunchState papState,
        string equippedWeapon)
    {
        var baseline = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var source = new LocalSourceMetadata(
            LocalReadStatus.Success, DataFreshness.Fresh, TimeSpan.FromSeconds(1), DataProvenance.LocalFile,
            "source locale", "Lecture réussie.");
        var runtimePlayer = new RuntimePlayerSnapshot(
            Xuid, "Alice", 0, "connected", PlayerLifeState.Alive, RuntimeGodModeState.Off,
            1000, 100, 100, equippedWeapon, papState, 20, 100, [], false, []);
        var player = new PlayerState(
            0, Xuid, "Alice", "user", "fr", "FR", PlayerLifeState.Alive, 1000,
            TimeSpan.FromMinutes(2), false, false)
        {
            Provenance = DataProvenance.LocalFile,
            RuntimeDetails = runtimePlayer,
            ModerationStateAvailable = false
        };
        var runtime = new ControlCenterRuntimeSnapshot(
            1, "2.1.1", "session", 1, 1000, null, "session_gettime_and_file_mtime", mapCode,
            5, 0, TimeSpan.FromMinutes(2), RankedStatus.Ranked, RuntimePowerState.Unknown,
            RuntimePackAPunchState.Unknown, 1, 18, 1, 0, false, [runtimePlayer]);
        return baseline with
        {
            Server = baseline.Server with
            {
                SessionId = "session",
                MapCode = mapCode,
                SessionProvenance = DataProvenance.LocalFile,
                MapProvenance = DataProvenance.LocalFile
            },
            Players = [player],
            DataContext = new SnapshotDataContext(
                ControlCenterDataMode.HybridLocal, "MODE HYBRIDE LOCAL", null, source, []),
            LocalObservation = baseline.LocalObservation with
            {
                RuntimeSnapshot = new LocalReadResult<ControlCenterRuntimeSnapshot>(runtime, source, DateTimeOffset.UtcNow)
            }
        };
    }

    private static async Task WaitForCommandAsync<T>(AsyncRelayCommand<T> command)
    {
        for (var attempt = 0; attempt < 200 && command.IsExecuting; attempt++) await Task.Delay(10);
        Assert.IsFalse(command.IsExecuting);
    }

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        for (var attempt = 0; attempt < 200 && command.IsExecuting; attempt++) await Task.Delay(10);
        Assert.IsFalse(command.IsExecuting);
    }

    private sealed class SnapshotStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current { get; private set; } = snapshot;
        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current!);
        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current!);
    }

    private sealed class ConfirmationService : IOperatorConfirmationService
    {
        public Task<bool> ConfirmAsync(OperatorConfirmationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class CapturingService : IPlayerAdministrationCommandService
    {
        public PlayerAdministrationRequest? Request { get; private set; }
        public Task<PlayerAdministrationExecutionResult> ExecuteAsync(
            PlayerAdministrationRequest request,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new PlayerAdministrationExecutionResult(
                request, PlayerAdministrationExecutionStatus.SentAwaitingManualVerification,
                "Commande transmise.", true, DateTimeOffset.UtcNow));
        }
    }
}
