using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class EasterEggRecordsOverlayDataProviderTests
{
    [TestMethod]
    public async Task Overlay_ReplacesOnlySimulatedEasterEggRecords()
    {
        var baseline = HybridBaseline();
        var local = new EasterEggRecord(
            "zm_tomb",
            "Origins",
            4,
            1,
            22,
            TimeSpan.FromHours(2),
            "Local Alpha + Local Bravo",
            ["1111111111111111", "2222222222222222"],
            "run-local",
            "native_trigger_active_holders_2of4");
        var catalog = new EasterEggRecordCatalog([local], 1, 1, 0, 0, true);
        var provider = new EasterEggRecordsOverlayDataProvider(
            new StubProvider(baseline),
            new StubReader(Success(catalog)));

        var result = await provider.GetSnapshotAsync();

        Assert.AreEqual(
            baseline.Records.Count(record => !record.IsEasterEgg),
            result.Records.Count(record => !record.IsEasterEgg));
        Assert.AreEqual(1, result.Records.Count(record => record.IsEasterEgg));
        var easterEgg = result.Records.Single(record => record.IsEasterEgg);
        Assert.AreEqual("Local Alpha + Local Bravo", easterEgg.Holder);
        Assert.AreEqual(DataProvenance.LocalFile, easterEgg.Provenance);
        Assert.AreEqual(1, result.EasterEggRecords.Records.Count);
        Assert.IsFalse(result.DataContext.SimulatedAreas.Contains("Easter Egg Records"));
    }

    [TestMethod]
    public async Task ValidEmptyLocalCatalog_RemovesSimulationWithoutInventingARecord()
    {
        var baseline = HybridBaseline();
        var catalog = new EasterEggRecordCatalog([], 1, 0, 0, 0, false);
        var provider = new EasterEggRecordsOverlayDataProvider(
            new StubProvider(baseline),
            new StubReader(Success(catalog)));

        var result = await provider.GetSnapshotAsync();

        Assert.AreEqual(0, result.Records.Count(record => record.IsEasterEgg));
        Assert.AreEqual(0, result.EasterEggRecords.Records.Count);
        Assert.AreEqual(LocalReadStatus.Success, result.EasterEggRecords.Source.ReadStatus);
        Assert.IsFalse(result.DataContext.SimulatedAreas.Contains("Easter Egg Records"));
    }

    private static DashboardSnapshot HybridBaseline()
    {
        var simulated = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        return simulated with
        {
            DataContext = simulated.DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE HYBRIDE LOCAL",
                ServerRoot = "C:\\test"
            }
        };
    }

    private static LocalReadResult<EasterEggRecordCatalog> Success(EasterEggRecordCatalog catalog) =>
        new(
            catalog,
            new LocalSourceMetadata(
                LocalReadStatus.Success,
                DataFreshness.Fresh,
                TimeSpan.FromMinutes(1),
                DataProvenance.LocalFile,
                "easter_eggs_v2",
                "Lecture réussie."),
            DateTimeOffset.UtcNow.AddMinutes(-1));

    private sealed class StubProvider(DashboardSnapshot snapshot) : IControlCenterDataProvider
    {
        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class StubReader(LocalReadResult<EasterEggRecordCatalog> result) : IEasterEggRecordReader
    {
        public Task<LocalReadResult<EasterEggRecordCatalog>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
