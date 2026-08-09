using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RankRecordsOverlayDataProviderTests
{
    [TestMethod]
    public async Task Overlay_ReplacesOnlyRanksAndRoundRecords_AndKeepsEasterEggSimulation()
    {
        var baseline = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy) with
        {
            DataContext = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy).DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE HYBRIDE LOCAL",
                ServerRoot = "C:\\test"
            }
        };
        var profile = new RankProfile("aaaaaaaaaaaaaaaa", "LocalPlayer", 7, TimeSpan.FromHours(12), 61);
        var roundRecord = new RoundRecord(
            "zm_castle",
            "Der Eisendrache",
            2,
            1,
            70,
            TimeSpan.FromHours(3),
            "LocalPlayer + Partner",
            ["aaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbb"],
            "match-local");
        var provider = new RankRecordsOverlayDataProvider(
            new StubProvider(baseline),
            new StubRankReader(Success(new RankProfileCatalog([profile], 1, 0), "players")),
            new StubRoundReader(Success(new RoundRecordCatalog([roundRecord], 1, 0, 0), "maps")));

        var result = await provider.GetSnapshotAsync();

        Assert.AreEqual(baseline.Server, result.Server);
        CollectionAssert.AreEqual(baseline.Services.ToArray(), result.Services.ToArray());
        CollectionAssert.AreEqual(baseline.Players.ToArray(), result.Players.ToArray());
        CollectionAssert.AreEqual(baseline.Events.ToArray(), result.Events.ToArray());
        Assert.AreEqual(1, result.RankRecords.Profiles.Count);
        Assert.AreEqual("LocalPlayer", result.RankRecords.Profiles.Single().DisplayName);
        Assert.AreEqual(1, result.Records.Count(record => !record.IsEasterEgg));
        Assert.AreEqual(1, result.Records.Count(record => record.IsEasterEgg));
        Assert.AreEqual(DataProvenance.LocalFile, result.Records.Single(record => !record.IsEasterEgg).Provenance);
        Assert.IsFalse(result.DataContext.SimulatedAreas.Contains("Records"));
        Assert.IsTrue(result.DataContext.SimulatedAreas.Contains("Easter Egg Records"));
    }

    [TestMethod]
    public async Task MissingLocalCatalogs_DoNotMasqueradeSimulatedRoundRecordsAsLocal()
    {
        var baseline = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var missingProfiles = Missing<RankProfileCatalog>("players");
        var missingRecords = Missing<RoundRecordCatalog>("maps");
        var provider = new RankRecordsOverlayDataProvider(
            new StubProvider(baseline),
            new StubRankReader(missingProfiles),
            new StubRoundReader(missingRecords));

        var result = await provider.GetSnapshotAsync();

        Assert.AreEqual(0, result.RankRecords.Profiles.Count);
        Assert.AreEqual(0, result.Records.Count(record => !record.IsEasterEgg));
        Assert.IsTrue(result.Records.All(record => record.IsEasterEgg));
        Assert.AreEqual(LocalReadStatus.Missing, result.RankRecords.ProfilesSource.ReadStatus);
        Assert.AreEqual(LocalReadStatus.Missing, result.RankRecords.RoundRecordsSource.ReadStatus);
    }

    private static LocalReadResult<T> Success<T>(T value, string source)
        where T : class =>
        new(
            value,
            new LocalSourceMetadata(
                LocalReadStatus.Success,
                DataFreshness.Fresh,
                TimeSpan.FromMinutes(1),
                DataProvenance.LocalFile,
                source,
                "Lecture réussie."),
            DateTimeOffset.UtcNow.AddMinutes(-1));

    private static LocalReadResult<T> Missing<T>(string source)
        where T : class =>
        new(
            null,
            new LocalSourceMetadata(
                LocalReadStatus.Missing,
                DataFreshness.Unknown,
                null,
                DataProvenance.LocalFile,
                source,
                "Source absente."),
            null);

    private sealed class StubProvider(DashboardSnapshot snapshot) : IControlCenterDataProvider
    {
        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class StubRankReader(LocalReadResult<RankProfileCatalog> result) : IRankProfileReader
    {
        public Task<LocalReadResult<RankProfileCatalog>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class StubRoundReader(LocalReadResult<RoundRecordCatalog> result) : IRoundRecordReader
    {
        public Task<LocalReadResult<RoundRecordCatalog>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
