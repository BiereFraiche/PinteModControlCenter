using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class BanServiceStatusReaderTests
{
    [TestMethod]
    public async Task FreshStatus_ExposesCountWithoutReplacingHeartbeatSemantics()
    {
        var now = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        using var root = new TemporaryServerRoot();
        root.WriteBanServiceStatus(now.AddSeconds(-2), 4);
        using var reader = new BanServiceStatusReader(root.Options, new FakeClock(now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(4, result.Value?.ActiveBans);
        Assert.AreEqual(DataFreshness.Fresh, result.Metadata.Freshness);
        Assert.IsTrue(result.Value?.Running);
    }

    [TestMethod]
    public async Task ExpiredStatus_DoesNotBecomeOffline()
    {
        var now = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        using var root = new TemporaryServerRoot();
        root.WriteBanServiceStatus(now.AddMinutes(-2));
        using var reader = new BanServiceStatusReader(root.Options, new FakeClock(now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(DataFreshness.Expired, result.Metadata.Freshness);
        Assert.IsNotNull(result.Value);
    }

    [TestMethod]
    public async Task ValidJsonWithUnexpectedRootShape_IsInvalidWithoutThrowing()
    {
        var now = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        using var root = new TemporaryServerRoot();
        root.WriteBlockA(BlockALocalFile.BanServiceStatus, "[]");
        using var reader = new BanServiceStatusReader(root.Options, new FakeClock(now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
    }
}
