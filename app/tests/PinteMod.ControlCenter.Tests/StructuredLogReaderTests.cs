using System.Text;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class StructuredLogReaderTests
{
    private const string Session = "session-local-001";
    private const string Xuid = "1111111111111111";

    [TestMethod]
    public async Task JoinAndExplicitUnranked_ProduceInferredSnapshotWithoutFullXuidInEvents()
    {
        using var root = new TemporaryServerRoot();
        root.WriteSessionLog(Session, "connections.log",
            $"[2000 ms][round 4][JOIN] Alice | xuid={Xuid} | client=0 | players=1\n");
        root.WriteSessionLog(Session, "ranks.log",
            "[2500] MATCH_UNRANKED | round=4 | reason=private value\n");
        using var reader = new StructuredLogReader(root.Options);

        var result = await reader.ReadAsync(new SessionManifest(1, "2.1.1", Session, "zm_tomb", 1000));

        Assert.AreEqual(1, result.Players.Count);
        Assert.AreEqual(4, result.Round);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), result.SessionDuration);
        Assert.AreEqual(RankedStatus.Unranked, result.RankedStatus);
        Assert.IsTrue(result.RankedStatusAvailable);
        Assert.IsFalse(result.Events.Any(item => item.Details.Contains(Xuid, StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.Events.All(item => item.SessionElapsed is not null));
    }

    [TestMethod]
    public async Task PartialFinalLine_IsIgnoredUntilCompleted()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteSessionLog(Session, "connections.log",
            $"[2000 ms][round 4][JOIN] Alice | xuid={Xuid} | client=0 | players=1");
        using var reader = new StructuredLogReader(root.Options);
        var manifest = new SessionManifest(1, "2.1.1", Session, "zm_tomb", 1000);

        var partial = await reader.ReadAsync(manifest);
        File.AppendAllText(path, Environment.NewLine, new UTF8Encoding(false));
        var completed = await reader.ReadAsync(manifest);

        Assert.AreEqual(0, partial.Players.Count);
        Assert.AreEqual(1, completed.Players.Count);
    }

    [TestMethod]
    public async Task MalformedLine_IsolatedAndSessionChangeClearsPreviousPlayers()
    {
        using var root = new TemporaryServerRoot();
        root.WriteSessionLog(Session, "connections.log",
            $"invalid line\n[2000 ms][round 4][JOIN] Alice | xuid={Xuid} | client=0 | players=1\n");
        const string nextSession = "session-local-002";
        root.WriteSessionLog(nextSession, "ranks.log", "[100] MODULE_LOADED | version=2.1.1\n");
        using var reader = new StructuredLogReader(root.Options);

        var first = await reader.ReadAsync(new SessionManifest(1, "2.1.1", Session, "zm_tomb", 0));
        var second = await reader.ReadAsync(new SessionManifest(1, "2.1.1", nextSession, "zm_tomb", 0));

        Assert.AreEqual(1, first.Players.Count);
        Assert.AreEqual(1, first.MalformedLines);
        Assert.AreEqual(0, second.Players.Count);
        Assert.IsFalse(second.Events.Any(item => item.Title == "Joueur connecté"));
    }

    [TestMethod]
    public async Task UnknownEvent_IsCountedAndDoesNotHideValidEvent()
    {
        using var root = new TemporaryServerRoot();
        root.WriteSessionLog(Session, "ranks.log",
            "[100] UNKNOWN_PRIVATE_EVENT | data=secret\n[200] MATCH_CLOCK_STARTED | round=1\n");
        using var reader = new StructuredLogReader(root.Options);

        var result = await reader.ReadAsync(new SessionManifest(1, "2.1.1", Session, "zm_tomb", 0));

        Assert.AreEqual(1, result.LinesIgnored);
        Assert.AreEqual(1, result.Events.Count);
        Assert.AreEqual("Chronomètre de partie démarré", result.Events[0].Title);
    }

    [TestMethod]
    public async Task UnsafeSessionIdentifier_IsRejectedAsInvalidWithoutEscapingServerRoot()
    {
        using var root = new TemporaryServerRoot();
        using var reader = new StructuredLogReader(root.Options);

        var result = await reader.ReadAsync(
            new SessionManifest(1, "2.1.1", "../outside", "zm_tomb", 0));

        Assert.AreEqual(LocalReadStatus.Invalid, result.Source.ReadStatus);
        Assert.AreEqual(0, result.FilesScanned);
        Assert.AreEqual(0, result.Events.Count);
        Assert.AreEqual(0, result.Players.Count);
    }

    [TestMethod]
    public async Task AtomicReplacementWithEqualOrLargerFile_ResetsSessionCacheAndReadsNewContent()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteSessionLog(Session, "connections.log",
            $"[2000 ms][round 4][JOIN] Alice | xuid={Xuid} | client=0 | players=1\n");
        using var reader = new StructuredLogReader(root.Options);
        var manifest = new SessionManifest(1, "2.1.1", Session, "zm_tomb", 1000);
        var first = await reader.ReadAsync(manifest);
        const string replacementXuid = "2222222222222222";
        File.WriteAllText(path,
            $"[3000 ms][round 5][JOIN] Bob | xuid={replacementXuid} | client=1 | players=1\n" +
            $"[3100 ms][round 5][ACTIVE] Bob | xuid={replacementXuid} | client=1 | players=1\n",
            new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(1));

        var second = await reader.ReadAsync(manifest);

        Assert.AreEqual("Alice", first.Players.Single().DisplayName);
        Assert.AreEqual(1, second.Players.Count);
        Assert.AreEqual("Bob", second.Players.Single().DisplayName);
        Assert.AreEqual(5, second.Round);
        Assert.IsFalse(second.Players.Any(player => player.DisplayName == "Alice"));
    }
}
