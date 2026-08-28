using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class AdaptiveUnavailableControlCenterDataProviderTests
{
    [TestMethod]
    public async Task NativeBoiiiRealProfile_NeverReturnsSimulationData()
    {
        using var directory = new TemporaryServerDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Root, "boiii", "custom_scripts"));
        var profile = new ServerInstallationAnalyzer().Analyze(directory.Root).IntegrationProfile;
        var provider = new AdaptiveUnavailableControlCenterDataProvider(directory.Root, profile);

        var snapshot = await provider.GetSnapshotAsync();

        Assert.AreEqual(ManagedServerIntegrationKind.BoiiiNative, profile.Kind);
        Assert.AreEqual(ControlCenterDataMode.HybridLocal, snapshot.DataContext.Mode);
        Assert.AreEqual(DataProvenance.Unavailable, snapshot.DataContext.SessionSource.Provenance);
        Assert.AreEqual(DataProvenance.Unavailable, snapshot.Server.MapProvenance);
        Assert.AreEqual(DataProvenance.Unavailable, snapshot.Server.SessionProvenance);
        Assert.IsFalse(snapshot.Server.RoundAvailable);
        Assert.IsFalse(snapshot.Server.PlayersConnectedAvailable);
        Assert.IsFalse(snapshot.Server.RankedStatusAvailable);
        Assert.IsTrue(snapshot.Server.ServerRunningAvailable);
        Assert.IsFalse(snapshot.Server.ServerRunning);
        Assert.AreEqual(0, snapshot.Players.Count);
        Assert.AreEqual(0, snapshot.Events.Count);
        Assert.AreEqual(0, snapshot.Records.Count);
        Assert.AreEqual(0, snapshot.Services.Count);
        Assert.AreEqual(0, snapshot.DataContext.SimulatedAreas.Count);
        Assert.AreEqual(DataProvenance.Unavailable, snapshot.LocalObservation.Logs.Source.Provenance);
        Assert.AreEqual(DataProvenance.Unavailable, snapshot.RankRecords.ProfilesSource.Provenance);
        Assert.AreEqual(DataProvenance.Unavailable, snapshot.EasterEggRecords.Source.Provenance);
    }

    [TestMethod]
    public async Task ThirdPartyRealProfile_RemainsEmptyAndFailClosed()
    {
        using var directory = new TemporaryServerDirectory();
        var customScripts = Path.Combine(directory.Root, "boiii", "custom_scripts");
        Directory.CreateDirectory(customScripts);
        await File.WriteAllTextAsync(Path.Combine(customScripts, "community.gsc"), "function init(){ addcommand(\"god\", ::x); }");
        var profile = new ServerInstallationAnalyzer().Analyze(directory.Root).IntegrationProfile;
        var provider = new AdaptiveUnavailableControlCenterDataProvider(directory.Root, profile);

        var snapshot = await provider.GetSnapshotAsync();

        Assert.AreEqual(ManagedServerIntegrationKind.ThirdPartyScripts, profile.Kind);
        Assert.AreEqual(IntegrationCommandTransport.None, profile.CommandTransport);
        Assert.AreEqual(0, snapshot.Players.Count);
        Assert.AreEqual(DataProvenance.Unavailable, snapshot.DataContext.SessionSource.Provenance);
        StringAssert.Contains(snapshot.DataContext.ModeLabel, "GSC TIERS");
    }

    private sealed class TemporaryServerDirectory : IDisposable
    {
        public TemporaryServerDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.AdaptiveUnavailableTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
