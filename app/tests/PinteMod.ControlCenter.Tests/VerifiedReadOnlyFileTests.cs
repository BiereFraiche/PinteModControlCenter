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
}
