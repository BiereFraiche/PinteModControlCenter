using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class LiveConsoleViewModelTests
{
    [TestMethod]
    public async Task Pause_FreezesVisibleEventsUntilExplicitResume()
    {
        var first = Event("Premier", 1);
        var second = Event("Second", 2);
        var store = new MutableStore(Snapshot([first]));
        var viewModel = new LogsViewModel(store);
        await viewModel.InitializeAsync();

        viewModel.ToggleDisplayPauseCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ToggleDisplayPauseCommand);
        store.Current = Snapshot([second, first]);
        await viewModel.InitializeAsync();

        Assert.IsTrue(viewModel.IsDisplayPaused);
        Assert.AreEqual(1, viewModel.Events.Count);
        Assert.AreEqual(1, viewModel.PendingEventCount);

        viewModel.ToggleDisplayPauseCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ToggleDisplayPauseCommand);

        Assert.IsFalse(viewModel.IsDisplayPaused);
        Assert.AreEqual(2, viewModel.Events.Count);
        Assert.AreEqual(0, viewModel.PendingEventCount);
    }

    [TestMethod]
    public void AutoScroll_IsEnabledByDefaultAndCanBeDisabled()
    {
        var viewModel = new LogsViewModel(new MutableStore(Snapshot([])));

        Assert.IsTrue(viewModel.AutoScrollEnabled);
        viewModel.AutoScrollEnabled = false;
        Assert.IsFalse(viewModel.AutoScrollEnabled);
    }

    [TestMethod]
    public async Task PauseEvents_HaveDedicatedFilterAndExplicitLocalSource()
    {
        var pauseEvent = Event("Partie mise en pause", 2, "PAUSE");
        var systemEvent = Event("Système", 1);
        var viewModel = new LogsViewModel(new MutableStore(Snapshot([pauseEvent, systemEvent])));

        await viewModel.InitializeAsync();
        viewModel.SelectFilterCommand.Execute(viewModel.Filters.Single(item => item.Key == "PAUSE"));

        Assert.AreEqual(1, viewModel.Events.Count);
        Assert.AreEqual("Partie mise en pause", viewModel.Events[0].Title);
        Assert.IsTrue(viewModel.SourceLabel.Contains("pause.log", StringComparison.Ordinal));
        Assert.IsTrue(viewModel.SourceSummary.Contains("pause Réussie", StringComparison.Ordinal));
    }

    private static DashboardSnapshot Snapshot(IReadOnlyList<LiveEvent> events)
    {
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        return snapshot with
        {
            Events = events,
            DataContext = snapshot.DataContext with { Mode = ControlCenterDataMode.HybridLocal },
            LocalObservation = snapshot.LocalObservation with
            {
                CommunityPauseLogSource = new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    TimeSpan.Zero,
                    DataProvenance.LocalFile,
                    "logs/pause.log",
                    "OK"),
                Logs = new StructuredLogSnapshot(
                    "session",
                    events,
                    [],
                    1,
                    TimeSpan.FromMinutes(1),
                    RankedStatus.Unknown,
                    false,
                    new LocalSourceMetadata(
                        LocalReadStatus.Success,
                        DataFreshness.Fresh,
                        TimeSpan.Zero,
                        DataProvenance.LocalFile,
                        "logs/sessions/<active>",
                        "OK"),
                    1,
                    0,
                    0,
                    events.Count)
            }
        };
    }

    private static LiveEvent Event(string title, int seconds, string category = "SYSTÈME") => new(
        DateTimeOffset.UnixEpoch,
        category,
        title,
        "Détail",
        EventSeverity.Information)
    {
        SessionElapsed = TimeSpan.FromSeconds(seconds),
        Provenance = DataProvenance.LocalFile
    };

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (command.IsExecuting && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(command.IsExecuting);
    }

    private sealed class MutableStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current { get; set; } = snapshot;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);
    }
}
