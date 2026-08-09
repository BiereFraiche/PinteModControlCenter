using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class LocalDataSourceProbeTests
{
    [TestMethod]
    public async Task LocalRoot_WithFiveValidSources_IsReadyAndReadOnly()
    {
        using var root = new TemporaryServerRoot();
        var paths = new List<string> { root.WriteSession() };
        paths.AddRange(Enum.GetValues<LocalServiceKind>()
            .Select(service => root.WriteHeartbeat(service, DateTimeOffset.UtcNow)));
        var before = paths.ToDictionary(path => path, TemporaryServerRoot.Fingerprint);

        var result = await new LocalDataSourceProbe().ProbeAsync(
            new LocalDataSourceProbeRequest(OperatorDataLocation.Local, root.Root));

        Assert.IsTrue(result.RootAccepted);
        Assert.AreEqual(5, result.ReadableSourceCount);
        Assert.AreEqual(5, result.TotalSourceCount);
        foreach (var path in paths)
        {
            Assert.AreEqual(before[path], TemporaryServerRoot.Fingerprint(path));
        }
    }

    [TestMethod]
    public async Task AccessibleRoot_WithoutSources_IsReportedWithoutInventingData()
    {
        using var root = new TemporaryServerRoot();

        var result = await new LocalDataSourceProbe().ProbeAsync(
            new LocalDataSourceProbeRequest(OperatorDataLocation.Local, root.Root));

        Assert.IsTrue(result.RootAccepted);
        Assert.AreEqual(0, result.ReadableSourceCount);
        Assert.AreEqual(5, result.TotalSourceCount);
        StringAssert.Contains(result.Message, "aucune source");
    }

    [TestMethod]
    public async Task LanMode_RejectsNonUncPathBeforeReading()
    {
        using var root = new TemporaryServerRoot();

        var result = await new LocalDataSourceProbe().ProbeAsync(
            new LocalDataSourceProbeRequest(OperatorDataLocation.Lan, root.Root));

        Assert.IsFalse(result.RootAccepted);
        StringAssert.Contains(result.Message, "UNC");
    }

    [TestMethod]
    public async Task LocalMode_RejectsUncPathBeforeReading()
    {
        var result = await new LocalDataSourceProbe().ProbeAsync(
            new LocalDataSourceProbeRequest(OperatorDataLocation.Local, "\\\\serveur\\partage\\UnrankedServer"));

        Assert.IsFalse(result.RootAccepted);
        StringAssert.Contains(result.Message, "mode Local");
    }
}
