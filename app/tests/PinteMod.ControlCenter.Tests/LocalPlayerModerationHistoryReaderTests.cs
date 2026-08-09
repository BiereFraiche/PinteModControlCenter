using System.Security.Cryptography;
using System.Text;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class LocalPlayerModerationHistoryReaderTests
{
    private const string Xuid = "1234567890abcdef";

    [TestMethod]
    public async Task ValidHistory_ExposesOnlyCountersAndNeutralizedDetails()
    {
        using var root = new TemporaryServerRoot();
        var path = WriteHistory(root, Xuid, $$"""
            {
              "schema_version": "1",
              "identity_kind": "BOIII_XUID",
              "xuid": "{{Xuid}}",
              "kicks": "2",
              "mutes": 3,
              "temporary_bans": "1",
              "permanent_bans": "0",
              "unbans": "1",
              "last_action": "kick",
              "last_reason": "Cible {{Xuid}} depuis 192.168.1.20"
            }
            """);
        var before = Fingerprint(path);
        var reader = new LocalPlayerModerationHistoryReader(
            root.Options,
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await reader.ReadAsync(Xuid);
        var after = Fingerprint(path);

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataProvenance.LocalFile, result.Metadata.Provenance);
        Assert.AreEqual(2, result.Value?.Kicks);
        Assert.AreEqual(3, result.Value?.Mutes);
        Assert.AreEqual(1, result.Value?.TemporaryBans);
        Assert.IsFalse(result.Value!.LastReason.Contains(Xuid, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Value.LastReason.Contains("192.168.1.20", StringComparison.Ordinal));
        Assert.AreEqual(before, after);
    }

    [TestMethod]
    public async Task MissingOrTemporaryHistory_NeverBecomesAnActiveSource()
    {
        using var root = new TemporaryServerRoot();
        var activePath = HistoryPath(root, Xuid);
        Directory.CreateDirectory(Path.GetDirectoryName(activePath)!);
        await File.WriteAllTextAsync(activePath + ".tmp", ValidJson(Xuid));
        await File.WriteAllTextAsync(activePath + ".bak", ValidJson(Xuid));
        var reader = new LocalPlayerModerationHistoryReader(
            root.Options,
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await reader.ReadAsync(Xuid);

        Assert.AreEqual(LocalReadStatus.Missing, result.Metadata.ReadStatus);
        Assert.IsNull(result.Value);
    }

    [DataTestMethod]
    [DataRow("[]", LocalReadStatus.Invalid)]
    [DataRow("{", LocalReadStatus.Invalid)]
    [DataRow("{\"schema_version\":99}", LocalReadStatus.UnsupportedSchema)]
    public async Task UnexpectedJsonShape_IsIsolated(string json, LocalReadStatus expectedStatus)
    {
        using var root = new TemporaryServerRoot();
        WriteHistory(root, Xuid, json);
        var reader = new LocalPlayerModerationHistoryReader(
            root.Options,
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await reader.ReadAsync(Xuid);

        Assert.AreEqual(expectedStatus, result.Metadata.ReadStatus);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public async Task MismatchedIdentityOrInvalidCounter_IsRejected()
    {
        using var root = new TemporaryServerRoot();
        WriteHistory(root, Xuid, ValidJson("fedcba0987654321", kicks: "-1"));
        var reader = new LocalPlayerModerationHistoryReader(
            root.Options,
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await reader.ReadAsync(Xuid);

        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
        Assert.IsNull(result.Value);
        Assert.IsFalse(result.Metadata.Message.Contains("fedcba0987654321", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task InvalidXuid_IsRejectedBeforeAnyPathAccess()
    {
        using var root = new TemporaryServerRoot();
        var result = await new LocalPlayerModerationHistoryReader(
                root.Options,
                new FakeClock(DateTimeOffset.UtcNow))
            .ReadAsync("../../server_zm.cfg");

        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
        Assert.IsNull(result.Value);
    }

    private static string WriteHistory(TemporaryServerRoot root, string xuid, string json)
    {
        var path = HistoryPath(root, xuid);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }

    private static string HistoryPath(TemporaryServerRoot root, string xuid) => Path.Combine(
        root.Options.DataRoot,
        "moderation",
        "history",
        xuid + ".json");

    private static string ValidJson(string xuid, string kicks = "0") => $$"""
        {
          "schema_version": "1",
          "identity_kind": "BOIII_XUID",
          "xuid": "{{xuid}}",
          "kicks": "{{kicks}}",
          "mutes": "0",
          "temporary_bans": "0",
          "permanent_bans": "0",
          "unbans": "0",
          "last_action": "none",
          "last_reason": ""
        }
        """;

    private static string Fingerprint(string path)
    {
        var info = new FileInfo(path);
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return $"{info.Length}|{info.LastWriteTimeUtc.Ticks}|{Convert.ToHexString(sha.ComputeHash(stream))}";
    }
}
