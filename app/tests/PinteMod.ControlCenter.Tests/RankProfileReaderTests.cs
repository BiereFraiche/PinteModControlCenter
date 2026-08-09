using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RankProfileReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ValidProfiles_AreReadSortedAndWhitelisted()
    {
        using var root = new TemporaryServerRoot();
        root.WriteRankProfile("1111111111111111", ProfileJson("1111111111111111", "Alpha", 3, 3600, 28));
        root.WriteRankProfile("2222222222222222", ProfileJson("2222222222222222", "Bravo", 5, 7200, 41));
        using var reader = new RankProfileReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataProvenance.LocalFile, result.Metadata.Provenance);
        Assert.AreEqual(2, result.Value?.Profiles.Count);
        Assert.AreEqual("Bravo", result.Value?.Profiles[0].DisplayName);
        Assert.AreEqual("2222222222222222", result.Value?.Profiles[0].Xuid);
        Assert.AreEqual(TimeSpan.FromHours(2), result.Value?.Profiles[0].TotalPlayTime);
        Assert.AreEqual(0, result.Value?.FilesSkipped);
    }

    [TestMethod]
    public async Task InvalidFilenameTmpBakAndLegacyRanks_AreNeverActiveProfiles()
    {
        using var root = new TemporaryServerRoot();
        root.WriteRankProfile("1111111111111111", ProfileJson("1111111111111111", "Actif", 1, 60, 10));
        root.WriteRankProfile("2222222222222222", ProfileJson("2222222222222222", "Tmp", 1, 60, 10), ".json.tmp");
        root.WriteRankProfile("3333333333333333", ProfileJson("3333333333333333", "Bak", 1, 60, 10), ".json.bak");
        root.WriteRankProfile("pseudo-invalide", ProfileJson("4444444444444444", "Invalide", 1, 60, 10));
        var legacy = Path.Combine(root.Root, "boiii", "scriptdata", "pintemod", "ranks");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "5555555555555555.json"), ProfileJson("5555555555555555", "Legacy", 1, 60, 10));
        using var reader = new RankProfileReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(1, result.Value?.Profiles.Count);
        Assert.AreEqual("Actif", result.Value?.Profiles.Single().DisplayName);
        Assert.AreEqual(1, result.Value?.FilesSkipped);
    }

    [TestMethod]
    public async Task XuidMismatchOrUnsupportedSchema_IsRejected()
    {
        using var mismatchRoot = new TemporaryServerRoot();
        mismatchRoot.WriteRankProfile("1111111111111111", ProfileJson("2222222222222222", "Mismatch", 1, 60, 10));
        using var mismatchReader = new RankProfileReader(mismatchRoot.Options, new FakeClock(Now));

        var mismatch = await mismatchReader.ReadAsync();

        Assert.IsNull(mismatch.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, mismatch.Metadata.ReadStatus);

        using var schemaRoot = new TemporaryServerRoot();
        schemaRoot.WriteRankProfile("1111111111111111", ProfileJson("1111111111111111", "Schema", 1, 60, 10, 99));
        using var schemaReader = new RankProfileReader(schemaRoot.Options, new FakeClock(Now));
        var schema = await schemaReader.ReadAsync();

        Assert.IsNull(schema.Value);
        Assert.AreEqual(LocalReadStatus.UnsupportedSchema, schema.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task FailedRefresh_ReturnsStaleMemoryProfiles()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteRankProfile("1111111111111111", ProfileJson("1111111111111111", "Cache", 1, 60, 10));
        using var reader = new RankProfileReader(root.Options, new FakeClock(Now));
        await reader.ReadAsync();
        File.WriteAllText(path, "{");

        var result = await reader.ReadAsync();

        Assert.AreEqual("Cache", result.Value?.Profiles.Single().DisplayName);
        Assert.AreEqual(DataProvenance.MemoryCache, result.Metadata.Provenance);
        Assert.AreEqual(DataFreshness.Stale, result.Metadata.Freshness);
        StringAssert.Contains(result.Metadata.Message, "Dernière donnée valide");
    }

    [TestMethod]
    public async Task MissingDirectoryAndCancellation_AreHandled()
    {
        using var root = new TemporaryServerRoot();
        using var reader = new RankProfileReader(root.Options, new FakeClock(Now));
        var missing = await reader.ReadAsync();
        Assert.IsNull(missing.Value);
        Assert.AreEqual(LocalReadStatus.Missing, missing.Metadata.ReadStatus);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await reader.ReadAsync(cancellation.Token);
            Assert.Fail("Une annulation devait être propagée.");
        }
        catch (OperationCanceledException)
        {
            // TaskCanceledException est une forme valide d’annulation coopérative.
        }
    }

    internal static string ProfileJson(
        string xuid,
        string name,
        int sessions,
        int totalSeconds,
        int bestRound,
        int schemaVersion = 2) => $$"""
        {
          "schema_version": {{schemaVersion}},
          "identity_kind": "boiii_xuid",
          "key": "internal-value-not-exposed",
          "xuid": "{{xuid}}",
          "name": "Old {{name}}",
          "last_name": "{{name}}",
          "sessions": {{sessions}},
          "total_seconds": {{totalSeconds}},
          "best_overall_round": {{bestRound}}
        }
        """;
}
