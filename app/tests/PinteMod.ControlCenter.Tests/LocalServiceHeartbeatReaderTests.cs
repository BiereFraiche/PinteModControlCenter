using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class LocalServiceHeartbeatReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

    [DataTestMethod]
    [DataRow(0, DataFreshness.Fresh)]
    [DataRow(15, DataFreshness.Fresh)]
    [DataRow(16, DataFreshness.Stale)]
    [DataRow(45, DataFreshness.Stale)]
    [DataRow(46, DataFreshness.Expired)]
    [DataRow(600, DataFreshness.Expired)]
    public void FreshnessPolicy_UsesValidatedBoundaries(int ageSeconds, DataFreshness expected)
    {
        Assert.AreEqual(expected, HeartbeatFreshnessPolicy.Evaluate(TimeSpan.FromSeconds(ageSeconds)));
    }

    [TestMethod]
    public async Task ValidHeartbeat_ExposesIndependentStateDimensions()
    {
        using var root = new TemporaryServerRoot();
        root.WriteHeartbeat(LocalServiceKind.Supervisor, Now.AddSeconds(-8), "monitoring");
        using var reader = new ServiceHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync(LocalServiceKind.Supervisor);

        Assert.AreEqual(ServiceDeclaredState.Monitoring, result.Value?.DeclaredState);
        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Fresh, result.Metadata.Freshness);
        Assert.AreEqual(TimeSpan.FromSeconds(8), result.Metadata.Age);
        Assert.AreEqual(DataProvenance.LocalFile, result.Metadata.Provenance);
        Assert.AreEqual(ServiceHealth.Healthy, HybridControlCenterDataProvider.SynthesizeHealth(result));
    }

    [TestMethod]
    public async Task ExpiredRunningHeartbeat_IsUnknownAndNeverAutomaticallyOffline()
    {
        using var root = new TemporaryServerRoot();
        root.WriteHeartbeat(LocalServiceKind.BanService, Now.AddSeconds(-46));
        using var reader = new ServiceHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync(LocalServiceKind.BanService);

        Assert.AreEqual(DataFreshness.Expired, result.Metadata.Freshness);
        Assert.AreEqual(ServiceDeclaredState.Running, result.Value?.DeclaredState);
        Assert.AreEqual(ServiceHealth.Unknown, HybridControlCenterDataProvider.SynthesizeHealth(result));
        Assert.AreNotEqual(ServiceHealth.Offline, HybridControlCenterDataProvider.SynthesizeHealth(result));
    }

    [DataTestMethod]
    [DataRow("stopped", ServiceHealth.Offline)]
    [DataRow("error", ServiceHealth.Error)]
    [DataRow("paused", ServiceHealth.Warning)]
    [DataRow("configured", ServiceHealth.Warning)]
    public async Task ExplicitDeclaredStates_ControlTheirSemanticHealth(string state, ServiceHealth expected)
    {
        using var root = new TemporaryServerRoot();
        root.WriteHeartbeat(LocalServiceKind.GeoIpBridge, Now.AddSeconds(-2), state);
        using var reader = new ServiceHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync(LocalServiceKind.GeoIpBridge);

        Assert.AreEqual(expected, HybridControlCenterDataProvider.SynthesizeHealth(result));
    }

    [TestMethod]
    public async Task MissingHeartbeat_IsUnknownRatherThanOfflineOrError()
    {
        using var root = new TemporaryServerRoot();
        using var reader = new ServiceHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync(LocalServiceKind.LiveConsole);

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Missing, result.Metadata.ReadStatus);
        Assert.AreEqual(ServiceHealth.Unknown, HybridControlCenterDataProvider.SynthesizeHealth(result));
    }

    [TestMethod]
    public async Task CachedExpiredValue_IsClearlyMarkedAndNotGreen()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteHeartbeat(LocalServiceKind.LiveConsole, Now.AddSeconds(-1));
        var clock = new FakeClock(Now);
        using var reader = new ServiceHeartbeatReader(root.Options, clock);
        await reader.ReadAsync(LocalServiceKind.LiveConsole);
        File.WriteAllText(path, "{");
        clock.UtcNow = Now.AddMinutes(1);

        var result = await reader.ReadAsync(LocalServiceKind.LiveConsole);

        Assert.AreEqual(DataProvenance.MemoryCache, result.Metadata.Provenance);
        Assert.AreEqual(DataFreshness.Expired, result.Metadata.Freshness);
        Assert.AreEqual("Dernière donnée valide — périmée.", result.Metadata.Message);
        Assert.AreEqual(ServiceHealth.Unknown, HybridControlCenterDataProvider.SynthesizeHealth(result));
    }

    [TestMethod]
    public async Task RepeatedInvalidReads_BecomeDurableErrorOnlyOnThirdRefresh()
    {
        using var root = new TemporaryServerRoot();
        root.Write(LocalPinteModFile.SupervisorHeartbeat, "{");
        using var reader = new ServiceHeartbeatReader(root.Options, new FakeClock(Now));

        var first = await reader.ReadAsync(LocalServiceKind.Supervisor);
        var second = await reader.ReadAsync(LocalServiceKind.Supervisor);
        var third = await reader.ReadAsync(LocalServiceKind.Supervisor);

        Assert.IsFalse(first.Metadata.IsDurableFailure);
        Assert.IsFalse(second.Metadata.IsDurableFailure);
        Assert.IsTrue(third.Metadata.IsDurableFailure);
        Assert.AreNotEqual(ServiceHealth.Error, HybridControlCenterDataProvider.SynthesizeHealth(first));
        Assert.AreEqual(ServiceHealth.Error, HybridControlCenterDataProvider.SynthesizeHealth(third));
    }

    [TestMethod]
    public async Task ExclusivelyLockedHeartbeat_IsReportedAsReadFailure()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteHeartbeat(LocalServiceKind.Supervisor, Now);
        using var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var reader = new ServiceHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync(LocalServiceKind.Supervisor);

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.IoError, result.Metadata.ReadStatus);
        Assert.IsFalse(result.Metadata.IsDurableFailure);
    }

    [TestMethod]
    public async Task FutureTimestampOutsideTolerance_IsInvalid()
    {
        using var root = new TemporaryServerRoot();
        root.WriteHeartbeat(LocalServiceKind.Supervisor, Now.AddSeconds(6));
        using var reader = new ServiceHeartbeatReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync(LocalServiceKind.Supervisor);

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
    }
}
