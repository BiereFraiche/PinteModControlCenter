using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class InstallationVerificationReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ValidReport_ExposesOnlyWhitelistedFields()
    {
        using var root = new TemporaryServerRoot();
        root.WriteInstallationVerification(Now.AddHours(-2));
        using var reader = new InstallationVerificationReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Fresh, result.Metadata.Freshness);
        Assert.AreEqual(1, result.Value?.PassCount);
        Assert.AreEqual(1, result.Value?.Checks.Count);
        Assert.IsNull(typeof(InstallationVerificationReport).GetProperty("Root"));
        Assert.IsNull(typeof(InstallationVerificationCheck).GetProperty("Details"));
        Assert.IsFalse(result.Value!.Checks[0].Recommendation.Contains("C:\\private", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ReportOlderThanTwentyFourHours_IsValidButStale()
    {
        using var root = new TemporaryServerRoot();
        root.WriteInstallationVerification(Now.AddHours(-25));
        using var reader = new InstallationVerificationReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNotNull(result.Value);
        Assert.AreEqual(DataFreshness.Stale, result.Metadata.Freshness);
        StringAssert.Contains(result.Metadata.Message, "ancien");
    }

    [TestMethod]
    public async Task MissingReport_IsNeutralAndDoesNotInventResult()
    {
        using var root = new TemporaryServerRoot();
        using var reader = new InstallationVerificationReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Missing, result.Metadata.ReadStatus);
        StringAssert.Contains(result.Metadata.Message, "non exécutée");
    }

    [TestMethod]
    public async Task ValidJsonWithUnexpectedRootShape_IsInvalidWithoutThrowing()
    {
        using var root = new TemporaryServerRoot();
        root.WriteBlockA(BlockALocalFile.InstallationVerification, "[]");
        using var reader = new InstallationVerificationReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task UnexpectedResultEntryShape_IsInvalidWithoutStoppingReader()
    {
        using var root = new TemporaryServerRoot();
        root.WriteBlockA(BlockALocalFile.InstallationVerification, $$"""
            {
              "schema_version": 1,
              "tool": "Verify_PinteMod_Installation",
              "version": "2.1.1",
              "checked_utc": "{{Now:O}}",
              "pass": 1,
              "warning": 0,
              "error": 0,
              "results": { "value": [42], "Count": 1 }
            }
            """);
        using var reader = new InstallationVerificationReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
    }
}
