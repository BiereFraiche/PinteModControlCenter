using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RoundRecordReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ValidMapSchema_ParsesTopEntriesAndXuids()
    {
        using var root = new TemporaryServerRoot();
        root.WriteMapRecords("zm_tomb", MapJson());
        using var reader = new RoundRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(2, result.Value?.Records.Count);
        var solo = result.Value!.Records.Single(record => record.PlayerCount == 1);
        Assert.AreEqual(1, solo.Position);
        Assert.AreEqual(55, solo.Round);
        Assert.AreEqual("1111111111111111", solo.HolderXuids.Single());
        var duo = result.Value.Records.Single(record => record.PlayerCount == 2);
        CollectionAssert.AreEqual(
            new[] { "1111111111111111", "2222222222222222" },
            duo.HolderXuids.ToArray());
    }

    [TestMethod]
    public async Task InvalidActiveSlot_IsSkippedWithoutDiscardingTheMap()
    {
        using var root = new TemporaryServerRoot();
        var json = MapJson().Replace(
            "1111111111111111+2222222222222222",
            "not-a-xuid+2222222222222222",
            StringComparison.Ordinal);
        root.WriteMapRecords("zm_tomb", json);
        using var reader = new RoundRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(1, result.Value?.Records.Count);
        Assert.AreEqual(1, result.Value?.SlotsSkipped);
    }

    [TestMethod]
    public async Task OverlongTextInOneSlot_DoesNotDiscardOtherValidSlotsFromTheMap()
    {
        using var root = new TemporaryServerRoot();
        var overlongHolder = new string('X', 513);
        var json = MapJson().Replace(
            "Alpha + Bravo",
            overlongHolder,
            StringComparison.Ordinal);
        root.WriteMapRecords("zm_tomb", json);
        using var reader = new RoundRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(1, result.Value?.Records.Count);
        Assert.AreEqual(1, result.Value?.Records.Single().PlayerCount);
        Assert.AreEqual(1, result.Value?.SlotsSkipped);
        Assert.AreEqual(0, result.Value?.FilesSkipped);
    }

    [TestMethod]
    public async Task TmpBakSubdirectoryAndFilenameMismatch_AreIgnoredOrRejected()
    {
        using var root = new TemporaryServerRoot();
        root.WriteMapRecords("zm_tomb", MapJson());
        root.WriteMapRecords("zm_castle", MapJson("zm_castle", "Der Eisendrache"), ".json.tmp");
        root.WriteMapRecords("zm_zod", MapJson("zm_zod", "Shadows of Evil"), ".json.bak");
        root.WriteMapRecords("mismatch", MapJson());
        var nested = Path.Combine(root.Root, "boiii", "scriptdata", "pintemod", "ranks_v2", "maps", "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "zm_castle.json"), MapJson("zm_castle", "Der Eisendrache"));
        using var reader = new RoundRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(2, result.Value?.Records.Count);
        Assert.AreEqual(1, result.Value?.FilesSkipped);
        Assert.IsTrue(result.Value?.Records.All(record => record.MapCode == "zm_tomb"));
    }

    [TestMethod]
    public async Task UnsupportedSchema_IsReportedAndCacheBecomesStaleAfterFailure()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteMapRecords("zm_tomb", MapJson());
        using var reader = new RoundRecordReader(root.Options, new FakeClock(Now));
        await reader.ReadAsync();
        File.WriteAllText(path, MapJson(schemaVersion: 99));

        var cached = await reader.ReadAsync();

        Assert.AreEqual(2, cached.Value?.Records.Count);
        Assert.AreEqual(LocalReadStatus.UnsupportedSchema, cached.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Stale, cached.Metadata.Freshness);
        Assert.AreEqual(DataProvenance.MemoryCache, cached.Metadata.Provenance);

        using var freshRoot = new TemporaryServerRoot();
        freshRoot.WriteMapRecords("zm_tomb", MapJson(schemaVersion: 99));
        using var freshReader = new RoundRecordReader(freshRoot.Options, new FakeClock(Now));
        var unsupported = await freshReader.ReadAsync();
        Assert.IsNull(unsupported.Value);
        Assert.AreEqual(LocalReadStatus.UnsupportedSchema, unsupported.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task ValidEmptyMapDocument_IsARealEmptyCatalog()
    {
        using var root = new TemporaryServerRoot();
        var json = """
            {
              "schema_version": 4,
              "identity_kind": "boiii_xuid",
              "map": "zm_tomb",
              "display": "Origins",
              "round_1p_1": 0
            }
            """;
        root.WriteMapRecords("zm_tomb", json);
        using var reader = new RoundRecordReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNotNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(0, result.Value.Records.Count);
    }

    internal static string MapJson(
        string mapCode = "zm_tomb",
        string display = "Origins",
        int schemaVersion = 4) => $$"""
        {
          "schema_version": {{schemaVersion}},
          "identity_kind": "boiii_xuid",
          "map": "{{mapCode}}",
          "display": "{{display}}",
          "next_run_id": 9,
          "round_1p_1": 55,
          "seconds_1p_1": 5400,
          "holders_1p_1": "Alpha",
          "holder_xuids_1p_1": "1111111111111111",
          "match_id_1p_1": "match-solo",
          "round_2p_1": 44,
          "seconds_2p_1": 4200,
          "holders_2p_1": "Alpha + Bravo",
          "holder_xuids_2p_1": "1111111111111111+2222222222222222",
          "match_id_2p_1": "match-duo",
          "round_3p_1": 0,
          "round_4p_1": 0
        }
        """;
}
