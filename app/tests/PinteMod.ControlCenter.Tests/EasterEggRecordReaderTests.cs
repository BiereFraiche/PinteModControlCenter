using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class EasterEggRecordReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ValidOfficialSchemas_ParseTopEntriesAndActiveHolders()
    {
        using var root = new TemporaryServerRoot();
        root.WriteEasterEggProfiles(ProfilesJson());
        root.WriteEasterEggMapRecords("zm_tomb", MapJson());
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataProvenance.LocalFile, result.Metadata.Provenance);
        Assert.AreEqual(2, result.Value?.Records.Count);
        Assert.AreEqual(1, result.Value?.OfficialProfileCount);
        var fixedFourPlayerRecord = result.Value!.Records.Single(record => record.PlayerCount == 4);
        Assert.AreEqual(2, fixedFourPlayerRecord.HolderXuids.Count);
        Assert.AreEqual(2, fixedFourPlayerRecord.Position);
        Assert.AreEqual(TimeSpan.FromSeconds(7200), fixedFourPlayerRecord.Duration);
    }

    [TestMethod]
    public async Task MissingMapsDirectory_IsAValidEmptyOfficialCatalog()
    {
        using var root = new TemporaryServerRoot();
        root.WriteEasterEggProfiles(ProfilesJson());
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(0, result.Value.Records.Count);
        Assert.IsFalse(result.Value.MapsDirectoryPresent);
        StringAssert.Contains(result.Metadata.Message, "aucun Easter Egg Record officiel");
    }

    [TestMethod]
    public async Task EmptyMapsDirectory_IsAValidEmptyOfficialCatalog()
    {
        using var root = new TemporaryServerRoot();
        root.WriteEasterEggProfiles(ProfilesJson());
        Directory.CreateDirectory(Path.Combine(
            root.Root,
            "boiii",
            "scriptdata",
            "pintemod",
            "easter_eggs_v2",
            "maps"));
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(0, result.Value?.Records.Count);
        Assert.IsTrue(result.Value?.MapsDirectoryPresent);
    }

    [TestMethod]
    public async Task CandidateTestBackupLegacyAndNonOfficialSources_AreNeverPromoted()
    {
        using var root = new TemporaryServerRoot();
        root.WriteEasterEggProfiles(ProfilesJson());
        root.WriteEasterEggMapRecords("zm_tomb", MapJson(), "candidates\\maps");
        root.WriteEasterEggMapRecords("zm_tomb", MapJson(), "test\\maps");
        root.WriteEasterEggMapRecords("zm_tomb", MapJson(), "maps", ".json.tmp");
        root.WriteEasterEggMapRecords("zm_tomb", MapJson(), "maps", ".json.bak");
        root.WriteEasterEggMapRecords("zm_castle", MapJson("zm_castle", "Der Eisendrache"));
        var legacyDirectory = Path.Combine(root.Root, "boiii", "scriptdata", "pintemod", "easter_eggs", "maps");
        Directory.CreateDirectory(legacyDirectory);
        File.WriteAllText(Path.Combine(legacyDirectory, "zm_tomb.json"), MapJson());
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(0, result.Value?.Records.Count);
        Assert.AreEqual(1, result.Value?.MapFilesScanned);
        Assert.AreEqual(1, result.Value?.MapFilesSkipped);
    }

    [TestMethod]
    public async Task InvalidSlot_DoesNotDiscardValidNeighbor()
    {
        using var root = new TemporaryServerRoot();
        root.WriteEasterEggProfiles(ProfilesJson());
        var json = MapJson().Replace(
            "1111111111111111+2222222222222222",
            "invalid+2222222222222222",
            StringComparison.Ordinal);
        root.WriteEasterEggMapRecords("zm_tomb", json);
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(1, result.Value?.Records.Count);
        Assert.AreEqual(1, result.Value?.RecordSlotsSkipped);
        Assert.AreEqual(1, result.Value?.Records.Single().PlayerCount);
    }

    [TestMethod]
    public async Task DuplicateXuidInOneSlot_IsRejectedLocally()
    {
        using var root = new TemporaryServerRoot();
        root.WriteEasterEggProfiles(ProfilesJson());
        var json = MapJson().Replace(
            "1111111111111111+2222222222222222",
            "1111111111111111+1111111111111111",
            StringComparison.Ordinal);
        root.WriteEasterEggMapRecords("zm_tomb", json);
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(1, result.Value?.Records.Count);
        Assert.AreEqual(1, result.Value?.RecordSlotsSkipped);
    }

    [TestMethod]
    public async Task UnsupportedProfileSchema_UsesStaleMemoryCacheAfterSuccess()
    {
        using var root = new TemporaryServerRoot();
        var profilesPath = root.WriteEasterEggProfiles(ProfilesJson());
        root.WriteEasterEggMapRecords("zm_tomb", MapJson());
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));
        await reader.ReadAsync();
        File.WriteAllText(profilesPath, ProfilesJson(schemaVersion: 99));

        var cached = await reader.ReadAsync();

        Assert.AreEqual(2, cached.Value?.Records.Count);
        Assert.AreEqual(LocalReadStatus.UnsupportedSchema, cached.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Stale, cached.Metadata.Freshness);
        Assert.AreEqual(DataProvenance.MemoryCache, cached.Metadata.Provenance);
    }

    [TestMethod]
    public async Task MissingTruncatedAndUnsupportedProfile_AreReportedPrecisely()
    {
        using var missingRoot = new TemporaryServerRoot();
        using var missingReader = new EasterEggRecordReader(missingRoot.Options, new FakeClock(Now));
        var missing = await missingReader.ReadAsync();
        Assert.AreEqual(LocalReadStatus.Missing, missing.Metadata.ReadStatus);

        using var truncatedRoot = new TemporaryServerRoot();
        truncatedRoot.WriteEasterEggProfiles("{\"schema_version\":3");
        using var truncatedReader = new EasterEggRecordReader(truncatedRoot.Options, new FakeClock(Now));
        var truncated = await truncatedReader.ReadAsync();
        Assert.AreEqual(LocalReadStatus.Invalid, truncated.Metadata.ReadStatus);

        using var unsupportedRoot = new TemporaryServerRoot();
        unsupportedRoot.WriteEasterEggProfiles(ProfilesJson(schemaVersion: 99));
        using var unsupportedReader = new EasterEggRecordReader(unsupportedRoot.Options, new FakeClock(Now));
        var unsupported = await unsupportedReader.ReadAsync();
        Assert.AreEqual(LocalReadStatus.UnsupportedSchema, unsupported.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task UnsupportedOfficialMapSchema_IsReportedWithoutCache()
    {
        using var root = new TemporaryServerRoot();
        root.WriteEasterEggProfiles(ProfilesJson());
        root.WriteEasterEggMapRecords("zm_tomb", MapJson(schemaVersion: 99));
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.UnsupportedSchema, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task FilenameMapMismatch_IsRejected()
    {
        using var root = new TemporaryServerRoot();
        root.WriteEasterEggProfiles(ProfilesJson());
        root.WriteEasterEggMapRecords("zm_tomb", MapJson("zm_castle", "Der Eisendrache"));
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
    }

    internal static string ProfilesJson(
        string tombStatus = "OFFICIAL",
        int schemaVersion = 3) => $$"""
        {
          "schema_version": {{schemaVersion}},
          "storage_generation": 2,
          "identity_kind": "BOIII_XUID",
          "official_mode": "per_map_validated_only",
          "official_writes": 1,
          "status_zm_tomb": "{{tombStatus}}",
          "status_zm_castle": "DIAGNOSTIC"
        }
        """;

    internal static string MapJson(
        string mapCode = "zm_tomb",
        string display = "Origins",
        int schemaVersion = 2) => $$"""
        {
          "schema_version": {{schemaVersion}},
          "storage_generation": 2,
          "identity_kind": "BOIII_XUID",
          "mode": "official",
          "map": "{{mapCode}}",
          "display": "{{display}}",
          "next_run_id": 8,
          "seconds_1p_1": 3300,
          "holders_1p_1": "Alpha",
          "holder_xuids_1p_1": "1111111111111111",
          "run_id_1p_1": "run-solo",
          "round_1p_1": 18,
          "source_1p_1": "native_trigger_active_holders_1of1",
          "seconds_4p_2": 7200,
          "holders_4p_2": "Alpha + Bravo",
          "holder_xuids_4p_2": "1111111111111111+2222222222222222",
          "run_id_4p_2": "run-four-player",
          "round_4p_2": 25,
          "source_4p_2": "native_trigger_active_holders_2of4"
        }
        """;
}
