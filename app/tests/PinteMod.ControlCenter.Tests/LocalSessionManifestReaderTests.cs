using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class LocalSessionManifestReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ValidManifest_IsReadFromTheActiveJsonOnly()
    {
        using var root = new TemporaryServerRoot();
        root.WriteSession("zm_castle", "local-session", "2.1.1");
        using var reader = new SessionManifestReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNotNull(result.Value);
        Assert.AreEqual("zm_castle", result.Value.MapCode);
        Assert.AreEqual("local-session", result.Value.SessionId);
        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataProvenance.LocalFile, result.Metadata.Provenance);
        Assert.AreEqual(DataFreshness.Fresh, result.Metadata.Freshness);
    }

    [TestMethod]
    public async Task MissingActiveManifest_DoesNotUseTmpOrBak()
    {
        using var root = new TemporaryServerRoot();
        var active = root.Options.ResolvePath(LocalPinteModFile.CurrentSession);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        File.WriteAllText(active + ".tmp", "{\"map\":\"zm_tomb\"}");
        File.WriteAllText(active + ".bak", "{\"map\":\"zm_tomb\"}");
        using var reader = new SessionManifestReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Missing, result.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Unknown, result.Metadata.Freshness);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("{\"schema_version\":1")]
    [DataRow("{\"schema_version\":1,\"module_version\":\"2.1.1\"}")]
    public async Task EmptyTruncatedOrPartialManifest_IsRejected(string contents)
    {
        using var root = new TemporaryServerRoot();
        root.Write(LocalPinteModFile.CurrentSession, contents);
        using var reader = new SessionManifestReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.IsTrue(result.Metadata.ReadStatus is LocalReadStatus.Empty or LocalReadStatus.Invalid);
    }

    [TestMethod]
    public async Task UnsupportedSchema_IsReportedSeparately()
    {
        using var root = new TemporaryServerRoot();
        root.WriteSession(schemaVersion: 99);
        using var reader = new SessionManifestReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.UnsupportedSchema, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task FailedRefresh_ReturnsStaleMemoryValueWithoutClaimingItIsFresh()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteSession("zm_tomb", "cached-session");
        using var reader = new SessionManifestReader(root.Options, new FakeClock(Now));
        await reader.ReadAsync();
        File.WriteAllText(path, "{");

        var result = await reader.ReadAsync();

        Assert.AreEqual("cached-session", result.Value?.SessionId);
        Assert.AreEqual(DataProvenance.MemoryCache, result.Metadata.Provenance);
        Assert.AreEqual(DataFreshness.Stale, result.Metadata.Freshness);
        StringAssert.Contains(result.Metadata.Message, "Dernière donnée valide");
    }

    [TestMethod]
    public async Task Cancellation_IsPropagatedCleanly()
    {
        using var root = new TemporaryServerRoot();
        root.WriteSession();
        using var reader = new SessionManifestReader(root.Options, new FakeClock(Now));
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
}
