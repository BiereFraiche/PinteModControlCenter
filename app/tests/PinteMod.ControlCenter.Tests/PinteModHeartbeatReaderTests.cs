using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PinteModHeartbeatReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ValidHeartbeat_UsesVerifiedFileTimeAndAcceptsEmptyUtc()
    {
        using var root = new TemporaryServerRoot();
        root.WritePinteModHeartbeat(Now.AddSeconds(-2));
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001");

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Fresh, result.Metadata.Freshness);
        Assert.AreEqual(DataProvenance.LocalFile, result.Metadata.Provenance);
        Assert.AreEqual(ServiceDeclaredState.Running, result.Value?.DeclaredState);
        Assert.IsNull(result.Value?.DeclaredUpdatedAtUtc);
        Assert.AreEqual(TimeSpan.FromSeconds(2), result.Metadata.Age);
    }

    [DataTestMethod]
    [DataRow(2, DataFreshness.Fresh)]
    [DataRow(20, DataFreshness.Stale)]
    [DataRow(46, DataFreshness.Expired)]
    public async Task FileMtime_DeterminesFreshness(int ageSeconds, DataFreshness expected)
    {
        using var root = new TemporaryServerRoot();
        root.WritePinteModHeartbeat(Now.AddSeconds(-ageSeconds));
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001");

        Assert.AreEqual(expected, result.Metadata.Freshness);
    }

    [DataTestMethod]
    [DataRow("running", ServiceHealth.Healthy)]
    [DataRow("stopped", ServiceHealth.Offline)]
    [DataRow("error", ServiceHealth.Error)]
    public async Task FreshDeclaredState_MapsToExpectedHealth(string state, ServiceHealth expected)
    {
        using var root = new TemporaryServerRoot();
        root.WritePinteModHeartbeat(Now.AddSeconds(-2), state: state);
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001");

        Assert.AreEqual(expected, PinteModRuntimeOverlayDataProvider.SynthesizeHealth(result));
    }

    [TestMethod]
    public async Task ExpiredRunningHeartbeat_IsUnknownAndNeverOffline()
    {
        using var root = new TemporaryServerRoot();
        root.WritePinteModHeartbeat(Now.AddSeconds(-46));
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001");

        Assert.AreEqual(ServiceHealth.Unknown, PinteModRuntimeOverlayDataProvider.SynthesizeHealth(result));
        Assert.AreNotEqual(ServiceHealth.Offline, PinteModRuntimeOverlayDataProvider.SynthesizeHealth(result));
    }

    [TestMethod]
    public async Task MissingActiveFile_DoesNotUseTmpOrBak()
    {
        using var root = new TemporaryServerRoot();
        var active = root.Options.ResolvePath(LocalPinteModFile.PinteModHeartbeat);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        await File.WriteAllTextAsync(active + ".tmp", "{}");
        await File.WriteAllTextAsync(active + ".bak", "{}");
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001");

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Missing, result.Metadata.ReadStatus);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("{")]
    [DataRow("[]")]
    public async Task EmptyTruncatedOrUnexpectedRoot_IsRejected(string contents)
    {
        using var root = new TemporaryServerRoot();
        root.Write(LocalPinteModFile.PinteModHeartbeat, contents);
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001");

        Assert.IsNull(result.Value);
        Assert.IsTrue(result.Metadata.ReadStatus is LocalReadStatus.Empty or LocalReadStatus.Invalid);
    }

    [DataTestMethod]
    [DataRow("\"schema_version\": 1", "\"schema_version\": 2", LocalReadStatus.UnsupportedSchema)]
    [DataRow("\"declared_state\": \"running\"", "\"declared_state\": \"paused\"", LocalReadStatus.Invalid)]
    [DataRow("\"sequence\": 7", "\"sequence\": -1", LocalReadStatus.Invalid)]
    [DataRow("\"generated_gettime\": 123456", "\"generated_gettime\": -1", LocalReadStatus.Invalid)]
    [DataRow("session_gettime_and_file_mtime", "utc", LocalReadStatus.Invalid)]
    public async Task InvalidContractField_IsRejected(string oldValue, string newValue, LocalReadStatus expected)
    {
        using var root = new TemporaryServerRoot();
        var path = root.WritePinteModHeartbeat(Now.AddSeconds(-2));
        var json = (await File.ReadAllTextAsync(path)).Replace(oldValue, newValue, StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, json);
        File.SetLastWriteTimeUtc(path, Now.AddSeconds(-2).UtcDateTime);
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001");

        Assert.IsNull(result.Value);
        Assert.AreEqual(expected, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task WrongSession_IsRejectedAndOldCacheCannotCrossSession()
    {
        using var root = new TemporaryServerRoot();
        root.WritePinteModHeartbeat(Now.AddSeconds(-2), sessionId: "session-a");
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));
        var first = await reader.ReadAsync("session-a");

        var second = await reader.ReadAsync("session-b");

        Assert.IsNotNull(first.Value);
        Assert.IsNull(second.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, second.Metadata.ReadStatus);
        Assert.AreNotEqual(DataProvenance.MemoryCache, second.Metadata.Provenance);
    }

    [TestMethod]
    public async Task ReadFailure_ReturnsSameSessionCacheWithoutClaimingFreshRead()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WritePinteModHeartbeat(Now.AddSeconds(-2));
        using var reader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));
        await reader.ReadAsync("session-local-001");
        await File.WriteAllTextAsync(path, "{");

        var result = await reader.ReadAsync("session-local-001");

        Assert.IsNotNull(result.Value);
        Assert.AreEqual(DataProvenance.MemoryCache, result.Metadata.Provenance);
        Assert.AreNotEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task OversizedOrFutureDatedHeartbeat_IsRejected()
    {
        using var root = new TemporaryServerRoot();
        root.Write(LocalPinteModFile.PinteModHeartbeat, new string('x', PinteModHeartbeatReader.MaximumFileSizeBytes + 1));
        using var oversizedReader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));
        var oversized = await oversizedReader.ReadAsync("session-local-001");
        Assert.AreEqual(LocalReadStatus.Invalid, oversized.Metadata.ReadStatus);

        root.WritePinteModHeartbeat(Now.AddSeconds(6));
        using var futureReader = new PinteModHeartbeatReader(root.Options, new FakeClock(Now));
        var future = await futureReader.ReadAsync("session-local-001");
        Assert.AreEqual(LocalReadStatus.Invalid, future.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task FileModifiedDuringRead_IsRejectedAfterAllRetries()
    {
        using var root = new TemporaryServerRoot();
        root.WritePinteModHeartbeat(Now.AddSeconds(-2));
        using var reader = new PinteModHeartbeatReader(
            root.Options,
            new FakeClock(Now),
            path => File.AppendAllText(path, " "));

        var result = await reader.ReadAsync("session-local-001");

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
    }
}
