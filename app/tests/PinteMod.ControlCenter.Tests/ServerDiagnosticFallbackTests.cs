using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.State;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ServerDiagnosticFallbackTests
{
    private const string Xuid = "1234567890abcdef";
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27018, TimeSpan.FromSeconds(3));

    [DataTestMethod]
    [DataRow(RconDiagnosticCommand.MapInfo, "Origins (zm_tomb)")]
    [DataRow(RconDiagnosticCommand.PowerStatus, "Courant : ACTIF")]
    [DataRow(RconDiagnosticCommand.PackAPunchStatus, "Pack-a-Punch de la carte : DISPONIBLE")]
    [DataRow(RconDiagnosticCommand.RoundStatus, "Manche observée : 17")]
    [DataRow(RconDiagnosticCommand.Players, "Joueurs connectés : 1 / 18")]
    public async Task EmptyRconResponse_WithFreshMatchingRuntime_DisplaysAuthoritativeLocalFallback(
        RconDiagnosticCommand command,
        string expected)
    {
        var snapshot = Snapshot(DataFreshness.Fresh);
        var viewModel = CreateViewModel(snapshot);
        await viewModel.InitializeAsync();

        var selectedCommand = SelectCommand(viewModel, command);
        selectedCommand.Execute(null);
        await WaitForCommandAsync(selectedCommand);

        Assert.AreEqual("ÉTAT LOCAL AUTORITAIRE", viewModel.ServerDiagnosticStatus);
        Assert.AreEqual(ServiceHealth.Healthy, viewModel.ServerDiagnosticHealth);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.ServerDiagnosticCommandSent);
        StringAssert.Contains(viewModel.ServerDiagnosticMessage, "sortie console");
        StringAssert.Contains(viewModel.ServerDiagnosticMessage, expected);
        Assert.IsFalse(viewModel.ServerDiagnosticMessage.Contains(Xuid, StringComparison.OrdinalIgnoreCase));
    }

    [DataTestMethod]
    [DataRow(DataFreshness.Stale, "session")]
    [DataRow(DataFreshness.Expired, "session")]
    [DataRow(DataFreshness.Fresh, "autre-session")]
    public async Task EmptyRconResponse_WithoutFreshMatchingRuntime_DoesNotInventResult(
        DataFreshness freshness,
        string runtimeSession)
    {
        var snapshot = Snapshot(freshness, runtimeSession);
        var viewModel = CreateViewModel(snapshot);
        await viewModel.InitializeAsync();

        viewModel.MapDiagnosticCommand.Execute(null);
        await WaitForCommandAsync(viewModel.MapDiagnosticCommand);

        Assert.AreEqual("ENVOYÉ · SANS TEXTE", viewModel.ServerDiagnosticStatus);
        Assert.AreEqual("Réponse vide.", viewModel.ServerDiagnosticMessage);
        Assert.AreEqual(ServiceHealth.Warning, viewModel.ServerDiagnosticHealth);
    }

    [DataTestMethod]
    [DataRow(RconDiagnosticCommand.MapAudit)]
    [DataRow(RconDiagnosticCommand.EventStatus)]
    [DataRow(RconDiagnosticCommand.PowerUpCatalog)]
    public async Task DiagnosticWithoutLocalContract_ExplainsTransportLimit(RconDiagnosticCommand command)
    {
        var viewModel = CreateViewModel(Snapshot(DataFreshness.Fresh));
        await viewModel.InitializeAsync();

        var selectedCommand = SelectCommand(viewModel, command);
        selectedCommand.Execute(null);
        await WaitForCommandAsync(selectedCommand);

        Assert.AreEqual("SORTIE CONSOLE NON TRANSPORTÉE", viewModel.ServerDiagnosticStatus);
        StringAssert.Contains(viewModel.ServerDiagnosticMessage, "aucun contrat local autoritaire");
        Assert.AreEqual(ServiceHealth.Warning, viewModel.ServerDiagnosticHealth);
    }

    [TestMethod]
    public async Task EmptyHealth_DoesNotInventPass51_AndShowsOnlyLocalServiceSummary()
    {
        var viewModel = CreateViewModel(Snapshot(DataFreshness.Fresh));
        await viewModel.InitializeAsync();

        viewModel.HealthDiagnosticCommand.Execute(null);
        await WaitForCommandAsync(viewModel.HealthDiagnosticCommand);

        Assert.AreEqual("RÉSUMÉ LOCAL · PAS LE HEALTH FULL", viewModel.ServerDiagnosticStatus);
        StringAssert.Contains(viewModel.ServerDiagnosticMessage, "PinteMod : SAIN");
        Assert.IsFalse(viewModel.ServerDiagnosticMessage.Contains("PASS=51", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(viewModel.ServerDiagnosticMessage.Contains("51 contrôles réussis", StringComparison.OrdinalIgnoreCase));
    }

    private static ServerViewModel CreateViewModel(DashboardSnapshot snapshot) => new(
        new SnapshotStore(snapshot),
        new SimulationActionService(),
        rconEndpointFactory: () => Endpoint,
        rconDiagnosticService: new EmptyDiagnosticService());

    private static AsyncRelayCommand SelectCommand(ServerViewModel viewModel, RconDiagnosticCommand command) => command switch
    {
        RconDiagnosticCommand.HealthFull => viewModel.HealthDiagnosticCommand,
        RconDiagnosticCommand.MapInfo => viewModel.MapDiagnosticCommand,
        RconDiagnosticCommand.PowerStatus => viewModel.PowerDiagnosticCommand,
        RconDiagnosticCommand.PackAPunchStatus => viewModel.PackAPunchDiagnosticCommand,
        RconDiagnosticCommand.RoundStatus => viewModel.RoundDiagnosticCommand,
        RconDiagnosticCommand.Players => viewModel.PlayersDiagnosticCommand,
        RconDiagnosticCommand.MapAudit => viewModel.MapAuditDiagnosticCommand,
        RconDiagnosticCommand.EventStatus => viewModel.EventStatusDiagnosticCommand,
        RconDiagnosticCommand.PowerUpCatalog => viewModel.PowerUpCatalogDiagnosticCommand,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
    };

    private static DashboardSnapshot Snapshot(DataFreshness freshness, string runtimeSession = "session")
    {
        var baseline = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var source = new LocalSourceMetadata(
            LocalReadStatus.Success,
            freshness,
            TimeSpan.FromSeconds(1),
            DataProvenance.LocalFile,
            "source locale",
            "Lecture locale réussie.");
        var player = new RuntimePlayerSnapshot(
            Xuid, $"Alice {Xuid}", 0, "connected", PlayerLifeState.Alive, RuntimeGodModeState.Off,
            1000, 100, 100, "ray_gun", RuntimeWeaponPackAPunchState.Base, 20, 100, [], false, []);
        var runtime = new ControlCenterRuntimeSnapshot(
            1, "2.1.1", runtimeSession, 1, 1000, null, "session_gettime_and_file_mtime", "zm_tomb",
            17, 0, TimeSpan.FromMinutes(2), RankedStatus.Ranked, RuntimePowerState.On,
            RuntimePackAPunchState.Available, 1, 18, 1, 0, false, [player]);
        var server = baseline.Server with
        {
            SessionId = "session",
            MapCode = "zm_tomb",
            MapName = "Origins",
            SessionProvenance = DataProvenance.LocalFile,
            MapProvenance = DataProvenance.LocalFile
        };
        var service = new ServiceStatus("PinteMod", "running", ServiceHealth.Healthy, DateTimeOffset.UtcNow)
        {
            DeclaredState = ServiceDeclaredState.Running,
            Source = source
        };
        return baseline with
        {
            Server = server,
            Services = [service],
            DataContext = new SnapshotDataContext(
                ControlCenterDataMode.HybridLocal, "MODE HYBRIDE LOCAL", null, source, []),
            LocalObservation = baseline.LocalObservation with
            {
                RuntimeSnapshot = new LocalReadResult<ControlCenterRuntimeSnapshot>(runtime, source, DateTimeOffset.UtcNow)
            }
        };
    }

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        for (var attempt = 0; attempt < 200 && command.IsExecuting; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(command.IsExecuting);
    }

    private sealed class SnapshotStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current { get; private set; } = snapshot;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);
    }

    private sealed class EmptyDiagnosticService : IRconDiagnosticService
    {
        public Task<RconExecutionResult> ExecuteAsync(
            RconDiagnosticCommand command,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RconExecutionResult(
                command, RconExecutionStatus.EmptyResponse, "Réponse vide.", true, DateTimeOffset.UtcNow));
    }
}
