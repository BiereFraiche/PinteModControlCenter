using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class CommunityPauseLogReaderTests
{
    private static readonly SessionManifest Session = new(1, "2.1.1", "session-pause", "zm_tomb", 1000);

    [TestMethod]
    public async Task ExistingHistoryIsNotReplayed_AndNewEventsAreSanitized()
    {
        using var root = new TemporaryServerRoot();
        const string xuid = "1111111111111111";
        var path = root.WriteCommunityPauseLog("[900] PAUSE_START | source=old | active_players=2\n");
        var before = TemporaryServerRoot.Fingerprint(path);
        using var reader = new CommunityPauseLogReader(root.Options, new FakeClock(DateTimeOffset.UtcNow));

        var initial = await reader.ReadAsync(Session);
        File.AppendAllText(path,
            $"[2000] PAUSE_VOTE_START | initiator=Alice | xuid={xuid} | required=4 | majority=3\n",
            new UTF8Encoding(false));
        var updated = await reader.ReadAsync(Session);

        Assert.AreEqual(0, initial.Events.Count);
        Assert.AreEqual(1, updated.Events.Count);
        Assert.AreEqual("Vote de pause lancé", updated.Events[0].Title);
        Assert.IsTrue(updated.Events[0].Details.Contains("Alice", StringComparison.Ordinal));
        Assert.IsFalse(updated.Events[0].Details.Contains(xuid, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(before.Length + Encoding.UTF8.GetByteCount($"[2000] PAUSE_VOTE_START | initiator=Alice | xuid={xuid} | required=4 | majority=3\n"), new FileInfo(path).Length);
    }

    [TestMethod]
    public async Task PartialLineWaitsForCompletion_AndUnknownEventIsIsolated()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteCommunityPauseLog(string.Empty);
        using var reader = new CommunityPauseLogReader(root.Options, new FakeClock(DateTimeOffset.UtcNow));
        await reader.ReadAsync(Session);
        File.AppendAllText(path, "[2000] PAUSE_START | source=community", new UTF8Encoding(false));

        var partial = await reader.ReadAsync(Session);
        File.AppendAllText(path, "\n[2100] PRIVATE_EVENT | secret=value\n", new UTF8Encoding(false));
        var complete = await reader.ReadAsync(Session);

        Assert.AreEqual(0, partial.Events.Count);
        Assert.AreEqual(1, complete.Events.Count);
        Assert.AreEqual(EventSeverity.Warning, complete.Events[0].Severity);
        Assert.AreEqual(1, complete.LinesIgnored);
    }

    [TestMethod]
    public async Task AtomicReplacementIsTailedWithoutReplayingReplacementHistory()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteCommunityPauseLog("[900] STATUS | active=false\n");
        using var reader = new CommunityPauseLogReader(root.Options, new FakeClock(DateTimeOffset.UtcNow));
        await reader.ReadAsync(Session);
        File.AppendAllText(path, "[2000] PAUSE_START | source=community\n", new UTF8Encoding(false));
        var first = await reader.ReadAsync(Session);
        File.WriteAllText(path, "[100] PAUSE_END | reason=ancienne_session_et_remplacement_plus_long\n", new UTF8Encoding(false));

        var replacement = await reader.ReadAsync(Session);
        File.AppendAllText(path, "[2500] PAUSE_END | reason=community_resume\n", new UTF8Encoding(false));
        var appended = await reader.ReadAsync(Session);

        Assert.AreEqual(1, first.Events.Count);
        Assert.AreEqual(1, replacement.Events.Count);
        Assert.AreEqual(2, appended.Events.Count);
        Assert.AreEqual("Partie reprise", appended.Events[0].Title);
    }

    [TestMethod]
    public async Task SessionChangeClearsEventsAndStartsAtCurrentEnd()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteCommunityPauseLog(string.Empty);
        using var reader = new CommunityPauseLogReader(root.Options, new FakeClock(DateTimeOffset.UtcNow));
        await reader.ReadAsync(Session);
        File.AppendAllText(path, "[2000] PAUSE_START | source=community\n", new UTF8Encoding(false));
        var first = await reader.ReadAsync(Session);

        var next = await reader.ReadAsync(Session with { SessionId = "session-pause-2", StartedGetTime = 0 });

        Assert.AreEqual(1, first.Events.Count);
        Assert.AreEqual(0, next.Events.Count);
    }
}
