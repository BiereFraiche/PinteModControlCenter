using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RecordsFilteringViewModelTests
{
    [TestMethod]
    public async Task Records_FiltersCanCombineTypeMapPlayerCountAndHolder()
    {
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy) with
        {
            Records =
            [
                new RecordEntry("zm_castle", "Der Eisendrache", 4, 40, TimeSpan.FromMinutes(70), "Alice + Bob", false) { Position = 1 },
                new RecordEntry("zm_castle", "Der Eisendrache", 4, 0, TimeSpan.FromMinutes(54), "Alice + Bob", true) { Position = 1 },
                new RecordEntry("zm_stalingrad", "Gorod Krovi", 2, 35, TimeSpan.FromMinutes(80), "Charlie + Dana", false) { Position = 2 },
                new RecordEntry("zm_stalingrad", "Gorod Krovi", 2, 0, TimeSpan.FromMinutes(65), "Charlie + Dana", true) { Position = 1 }
            ]
        };
        var viewModel = new RecordsViewModel(new SnapshotStore(snapshot));

        await viewModel.InitializeAsync();

        viewModel.SelectedRecordType = viewModel.RecordTypeOptions.Single(option => option.Key == "ee");
        viewModel.SelectedRecordMap = viewModel.RecordMapOptions.Single(option => option.Key == "zm_castle");
        viewModel.SelectedRecordPlayerCount = viewModel.RecordPlayerCountOptions.Single(option => option.Key == "4");
        viewModel.SelectedRecordHolder = viewModel.RecordHolderOptions.Single(option => option.Key == "Alice");

        Assert.AreEqual(1, viewModel.Records.Count);
        Assert.IsTrue(viewModel.Records[0].IsEasterEgg);
        Assert.AreEqual("Der Eisendrache", viewModel.Records[0].MapName);
        Assert.AreEqual(4, viewModel.Records[0].PlayerCount);
        CollectionAssert.Contains(viewModel.Records[0].HolderNames.ToArray(), "Alice");
        Assert.AreEqual("1 / 4 record(s) affiché(s)", viewModel.FilterSummary);
    }

    [TestMethod]
    public async Task Records_SortByDuration_OrdersFastestFirst()
    {
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy) with
        {
            Records =
            [
                new RecordEntry("zm_castle", "Der Eisendrache", 4, 40, TimeSpan.FromMinutes(70), "Alice", false) { Position = 1 },
                new RecordEntry("zm_tomb", "Origins", 4, 35, TimeSpan.FromMinutes(45), "Bob", false) { Position = 1 }
            ]
        };
        var viewModel = new RecordsViewModel(new SnapshotStore(snapshot));

        await viewModel.InitializeAsync();
        viewModel.SelectedRecordSort = viewModel.RecordSortOptions.Single(option => option.Key == "duration");

        Assert.AreEqual("Origins", viewModel.Records[0].MapName);
        Assert.AreEqual("Der Eisendrache", viewModel.Records[1].MapName);
    }


    [TestMethod]
    public async Task Records_DefaultDisplayLimitKeepsAllMatchesForFilteringButRendersOnlyFifty()
    {
        var baseSnapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var records = Enumerable.Range(1, 250)
            .Select(index => new RecordEntry(
                index % 2 == 0 ? "zm_castle" : "zm_tomb",
                index % 2 == 0 ? "Der Eisendrache" : "Origins",
                4,
                10 + index,
                TimeSpan.FromSeconds(1000 + index),
                $"Player {index}",
                false)
            {
                Position = index
            })
            .ToArray();
        var viewModel = new RecordsViewModel(new SnapshotStore(baseSnapshot with { Records = records }));

        await viewModel.InitializeAsync();

        Assert.AreEqual(50, viewModel.Records.Count);
        Assert.AreEqual(250, viewModel.FilteredRecordCount);
        StringAssert.Contains(viewModel.FilterSummary, "50 affiché(s) · 250 correspondant(s)");

        viewModel.SelectedRecordPageSize = viewModel.RecordPageSizeOptions.Single(option => option.Key == "all");

        Assert.AreEqual(250, viewModel.Records.Count);
    }

    [TestMethod]
    public async Task Records_SelectedValueKeysDriveFiltersWithoutObjectIdentityDependency()
    {
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy) with
        {
            Records =
            [
                new RecordEntry("zm_castle", "Der Eisendrache", 4, 40, TimeSpan.FromMinutes(70), "Alice", false) { Position = 1 },
                new RecordEntry("zm_tomb", "Origins", 2, 30, TimeSpan.FromMinutes(55), "Bob", false) { Position = 1 }
            ]
        };
        var viewModel = new RecordsViewModel(new SnapshotStore(snapshot));

        await viewModel.InitializeAsync();
        viewModel.SelectedRecordMapKey = "zm_castle";

        Assert.AreEqual("zm_castle", viewModel.SelectedRecordMap.Key);
        Assert.AreEqual(1, viewModel.Records.Count);
        Assert.AreEqual("Der Eisendrache", viewModel.Records[0].MapName);
    }

    private sealed class SnapshotStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current { get; private set; } = snapshot;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);
    }
}
