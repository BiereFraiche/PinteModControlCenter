using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class CommunityPauseStatusReaderTests
{
    [TestMethod]
    public async Task ValidFeedback_IsParsedReadOnly_WithoutRawIdentifiers()
    {
        using var root = new TemporaryServerRoot();
        var now = new DateTimeOffset(2026, 8, 9, 3, 0, 0, TimeSpan.Zero);
        var path = root.WriteCommunityPauseFeedback(Feedback(active: true, activeVote: "pause | YES=1 | NO=0 | majority=2"));
        File.SetLastWriteTimeUtc(path, now.UtcDateTime.AddSeconds(-10));
        var before = TemporaryServerRoot.Fingerprint(path);
        using var reader = new CommunityPauseStatusReader(root.Options, new FakeClock(now));

        var result = await reader.ReadAsync();
        var after = TemporaryServerRoot.Fingerprint(path);

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Fresh, result.Metadata.Freshness);
        Assert.IsNotNull(result.Value);
        Assert.IsTrue(result.Value.Active);
        Assert.AreEqual(142, result.Value.AutomaticResumeSeconds);
        Assert.AreEqual("Pause", result.Value.ActiveVote);
        Assert.AreEqual(1, result.Value.VoteYes);
        Assert.AreEqual(2, result.Value.VoteMajority);
        Assert.AreEqual(before, after);
        Assert.IsFalse(result.Metadata.Message.Contains(root.Root, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task OldFeedback_IsExpired_AndMustNotBePresentedAsCurrent()
    {
        using var root = new TemporaryServerRoot();
        var now = new DateTimeOffset(2026, 8, 9, 3, 0, 0, TimeSpan.Zero);
        var path = root.WriteCommunityPauseFeedback(Feedback(active: false));
        File.SetLastWriteTimeUtc(path, now.UtcDateTime.AddSeconds(-46));
        using var reader = new CommunityPauseStatusReader(root.Options, new FakeClock(now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Expired, result.Metadata.Freshness);
        Assert.IsNotNull(result.Value);
    }

    [DataTestMethod]
    [DataRow("0", false)]
    [DataRow("1", true)]
    public async Task NumericActiveValue_FromRealServer_IsParsed(string activeToken, bool expectedActive)
    {
        using var root = new TemporaryServerRoot();
        var now = new DateTimeOffset(2026, 8, 9, 3, 0, 0, TimeSpan.Zero);
        var path = root.WriteCommunityPauseFeedback(Feedback(expectedActive, activeToken: activeToken));
        File.SetLastWriteTimeUtc(path, now.UtcDateTime);
        using var reader = new CommunityPauseStatusReader(root.Options, new FakeClock(now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Fresh, result.Metadata.Freshness);
        Assert.AreEqual(expectedActive, result.Value?.Active);
    }

    [TestMethod]
    public async Task InvalidRewrite_PreservesLastValidValueAsStaleMemoryCache()
    {
        using var root = new TemporaryServerRoot();
        var now = new DateTimeOffset(2026, 8, 9, 3, 0, 0, TimeSpan.Zero);
        var path = root.WriteCommunityPauseFeedback(Feedback(active: true));
        File.SetLastWriteTimeUtc(path, now.UtcDateTime);
        using var reader = new CommunityPauseStatusReader(root.Options, new FakeClock(now));
        var valid = await reader.ReadAsync();
        await File.WriteAllTextAsync(path, "PINTEMOD_REMOTE_FEEDBACK_V1\ncommand=ezzpausestatus\n", new UTF8Encoding(false));

        var invalid = await reader.ReadAsync();

        Assert.IsNotNull(valid.Value);
        Assert.IsNotNull(invalid.Value);
        Assert.IsTrue(invalid.Value.Active);
        Assert.AreEqual(LocalReadStatus.Invalid, invalid.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Stale, invalid.Metadata.Freshness);
        Assert.AreEqual(DataProvenance.MemoryCache, invalid.Metadata.Provenance);
    }

    [TestMethod]
    public async Task MissingActiveFile_DoesNotUseTmpOrBak()
    {
        using var root = new TemporaryServerRoot();
        var active = root.BlockAPaths.ResolveFixed(BlockALocalFile.CommunityPauseFeedback);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        await File.WriteAllTextAsync(active + ".tmp", Feedback(active: true), new UTF8Encoding(false));
        await File.WriteAllTextAsync(active + ".bak", Feedback(active: true), new UTF8Encoding(false));
        using var reader = new CommunityPauseStatusReader(root.Options, new FakeClock(DateTimeOffset.UtcNow));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Missing, result.Metadata.ReadStatus);
        Assert.IsNull(result.Value);
    }

    private static string Feedback(bool active, string activeVote = "none", string? activeToken = null) => $$"""
        PINTEMOD_REMOTE_FEEDBACK_V1
        command=ezzpausestatus
        generated_gettime=120000
        ---
        PinteMod Community Pause - EXPERIMENTAL v0.3
        Active: {{activeToken ?? active.ToString().ToLowerInvariant()}}
        Automatic resume in: {{(active ? 142 : 0)}}s
        Successful pauses: 1/2
        Pause proposals used: 1
        Pause vote: 20s | majority
        Resume vote: 15s | majority
        Public reminder: first=300s | every=720s
        Active vote: {{activeVote}}
        Temporary God Mode: {{(active ? "ON" : "OFF")}}
        Spectator spawn guard: {{(active ? "ON" : "OFF")}}
        New AI spawning: {{(active ? "blocked" : "normal")}}
        Soft pause: map/EE script timers are NOT frozen
        END

        """;
}
