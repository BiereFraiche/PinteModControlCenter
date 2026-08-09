using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.Services;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ClipboardExportViewModelTests
{
    [TestMethod]
    public async Task Settings_CopiesOnlyTheNeutralizedDiagnosticResponse()
    {
        var clipboard = new CapturingClipboard();
        var service = new FixedDiagnosticService("Joueur <XUID neutralisé> · adresse <IP neutralisée>");
        var viewModel = new SettingsViewModel(
            rconDiagnosticService: service,
            clipboardService: clipboard);

        viewModel.TestRconPlayersCommand.Execute(null);
        await WaitForCommandAsync(viewModel.TestRconPlayersCommand);
        viewModel.CopyRconResponseCommand.Execute(null);
        await WaitForCommandAsync(viewModel.CopyRconResponseCommand);

        Assert.AreEqual(viewModel.RconResponse, clipboard.Text);
        Assert.AreEqual("Réponse neutralisée copiée.", viewModel.RconCopyStatus);
    }

    [TestMethod]
    public async Task Server_CopiesOnlyTheNeutralizedDiagnosticResponse()
    {
        var clipboard = new CapturingClipboard();
        var service = new FixedDiagnosticService("Audit carte · chemin <CHEMIN neutralisé>");
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var viewModel = new ServerViewModel(
            new FixedSnapshotStore(snapshot),
            new SimulationActionService(),
            rconDiagnosticService: service,
            rconEndpointFactory: () => new RconEndpoint("127.0.0.1", 27018, TimeSpan.FromSeconds(3)),
            clipboardService: clipboard);

        viewModel.MapAuditDiagnosticCommand.Execute(null);
        await WaitForCommandAsync(viewModel.MapAuditDiagnosticCommand);
        viewModel.CopyServerDiagnosticCommand.Execute(null);
        await WaitForCommandAsync(viewModel.CopyServerDiagnosticCommand);

        Assert.AreEqual(viewModel.ServerDiagnosticMessage, clipboard.Text);
        Assert.AreEqual("Réponse neutralisée copiée.", viewModel.ServerDiagnosticCopyStatus);
    }

    [TestMethod]
    public async Task Logs_CopiesOnlyCurrentlyVisibleEvents()
    {
        var clipboard = new CapturingClipboard();
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy) with
        {
            Events =
            [
                new LiveEvent(
                    DateTimeOffset.UtcNow,
                    "SYSTÈME",
                    "Événement neutralisé",
                    "Joueur <XUID neutralisé> · fichier <CHEMIN neutralisé>",
                    EventSeverity.Information)
            ]
        };
        var viewModel = new LogsViewModel(new FixedSnapshotStore(snapshot), clipboardService: clipboard);
        await viewModel.InitializeAsync();

        viewModel.CopyVisibleEventsCommand.Execute(null);
        await WaitForCommandAsync(viewModel.CopyVisibleEventsCommand);

        Assert.IsNotNull(clipboard.Text);
        StringAssert.Contains(clipboard.Text, "Événement neutralisé");
        StringAssert.Contains(clipboard.Text, "<XUID neutralisé>");
        Assert.AreEqual("1 événement(s) neutralisé(s) copié(s).", viewModel.ClipboardStatus);
    }

    [TestMethod]
    public async Task ClipboardFailure_IsReportedWithoutCrashing()
    {
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var viewModel = new LogsViewModel(
            new FixedSnapshotStore(snapshot),
            clipboardService: new CapturingClipboard { AcceptCopy = false });
        await viewModel.InitializeAsync();

        viewModel.CopyVisibleEventsCommand.Execute(null);
        await WaitForCommandAsync(viewModel.CopyVisibleEventsCommand);

        StringAssert.Contains(viewModel.ClipboardStatus, "Copie impossible");
    }

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (command.IsExecuting && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(command.IsExecuting);
    }

    private sealed class CapturingClipboard : ITextClipboardService
    {
        public bool AcceptCopy { get; init; } = true;

        public string? Text { get; private set; }

        public bool TrySetText(string text)
        {
            if (!AcceptCopy)
            {
                return false;
            }

            Text = text;
            return true;
        }
    }

    private sealed class FixedDiagnosticService(string response) : IRconDiagnosticService
    {
        public Task<RconExecutionResult> ExecuteAsync(
            RconDiagnosticCommand command,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RconExecutionResult(
                command,
                RconExecutionStatus.Success,
                response,
                true,
                DateTimeOffset.UtcNow));
    }

    private sealed class FixedSnapshotStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current => snapshot;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
