using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class VerifiedReadOnlyFileTests
{
    [TestMethod]
    public async Task RegularFile_IsReadThroughVerifiedHandle()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteSession();

        await using var stream = VerifiedReadOnlyFile.Open(
            path,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var contents = await reader.ReadToEndAsync();

        StringAssert.Contains(contents, "session-local-001");
    }

    [TestMethod]
    public async Task Metadata_ComesFromVerifiedHandleEvenWhenPathIsReplaced()
    {
        using var root = new TemporaryServerRoot();
        var path = root.Write(
            LocalPinteModFile.PinteModHeartbeat,
            "{\"value\":\"original\"}");
        var originalLastWrite = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, originalLastWrite);
        var replacementPath = path + ".replaced";
        var replaced = false;
        var reader = new ReadOnlyJsonFileReader(
            root.Options,
            openedPath =>
            {
                if (replaced)
                {
                    return;
                }

                replaced = true;
                File.Move(openedPath, replacementPath);
                File.WriteAllText(openedPath, "{\"value\":\"replacement\"}");
                File.SetLastWriteTimeUtc(openedPath, originalLastWrite.AddHours(1));
            });

        var result = await reader.ReadAsync(
            LocalPinteModFile.PinteModHeartbeat,
            element => new TestJsonValue(element.GetProperty("value").GetString()!),
            1024);

        Assert.AreEqual(LocalReadStatus.Success, result.Status);
        Assert.AreEqual("original", result.Value?.Value);
        Assert.AreEqual(new DateTimeOffset(originalLastWrite), result.LastWriteTimeUtc);
    }

    [TestMethod]
    public async Task FileGrowingAfterVerifiedSizeCheck_IsCappedAndRejected()
    {
        using var root = new TemporaryServerRoot();
        const int maximumBytes = 128;
        var path = root.Write(
            LocalPinteModFile.PinteModHeartbeat,
            "{\"value\":\"initial\"}");
        var reader = new ReadOnlyJsonFileReader(
            root.Options,
            afterMetadataBeforeRead: openedPath =>
                File.AppendAllText(openedPath, new string('x', maximumBytes * 4)));

        var result = await reader.ReadAsync(
            LocalPinteModFile.PinteModHeartbeat,
            element => new TestJsonValue(element.GetProperty("value").GetString()!),
            maximumBytes);

        Assert.AreEqual(LocalReadStatus.Invalid, result.Status);
        Assert.AreEqual("Fichier anormalement volumineux.", result.Message);
    }

    [TestMethod]
    public async Task BoundedCopy_ReadsAtMostMaximumBytesFromLongerSource()
    {
        await using var source = new MemoryStream(new byte[4096]);
        await using var destination = new MemoryStream();

        await ReadOnlyJsonFileReader.CopyAtMostAsync(source, destination, 257);

        Assert.AreEqual(257, destination.Length);
        Assert.AreEqual(257, source.Position);
    }

    [TestMethod]
    public void OpenedTargetMismatch_IsRefusedBeforeRead()
    {
        var expected = Path.Combine(Path.GetTempPath(), "allowed", "data.json");
        var opened = Path.Combine(Path.GetTempPath(), "outside", "data.json");

        Assert.ThrowsException<LocalFileAccessRefusedException>(
            () => VerifiedReadOnlyFile.EnsureOpenedPathMatches(expected, opened));
    }

    [TestMethod]
    public void EquivalentExtendedUncPath_RemainsSupported()
    {
        VerifiedReadOnlyFile.EnsureOpenedPathMatches(
            @"\\server\share\PinteModData\logs\current_session.json",
            @"\\?\UNC\server\share\PinteModData\logs\current_session.json");
    }

    [TestMethod]
    public async Task RankPathPolicyFailure_ReturnsControlledRefusalWithoutPath()
    {
        using var root = new TemporaryServerRoot();
        var policy = new RankRecordsPathPolicy(root.Options);
        var reader = new ReadOnlyRankJsonFileReader(policy);
        var outsidePath = Path.Combine(Path.GetTempPath(), "private-server", "profile.json");

        var result = await reader.ReadAsync(
            RankRecordsDirectory.Players,
            outsidePath,
            1024,
            _ => new object());

        Assert.AreEqual(LocalReadStatus.AccessDenied, result.Status);
        Assert.AreEqual("Source locale refusée.", result.Message);
        Assert.IsFalse(result.Message.Contains(outsidePath, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record TestJsonValue(string Value);
}
