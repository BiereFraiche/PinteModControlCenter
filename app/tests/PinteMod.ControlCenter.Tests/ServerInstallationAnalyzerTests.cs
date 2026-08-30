using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ServerInstallationAnalyzerTests
{
    [TestMethod]
    public void Analyze_ThirdPartyScripts_AreDetectedWithoutInventingCompatibility()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        File.WriteAllText(Path.Combine(directory.CustomScripts, "third_party.gsc"), "// third party");

        var result = new ServerInstallationAnalyzer().Analyze(directory.Root);

        Assert.IsTrue(result.RootExists);
        Assert.IsTrue(result.BoiiiRootDetected);
        Assert.IsFalse(result.PinteModDetected);
        Assert.AreEqual(ManagedServerIntegrationKind.ThirdPartyScripts, result.IntegrationKind);
        Assert.IsTrue(result.ThirdPartyGscDetected);
        Assert.AreEqual(1, result.ThirdPartyGscCount);
        CollectionAssert.Contains(result.ThirdPartyGscNames.ToArray(), "third_party.gsc");
        Assert.AreEqual(1, result.GscFileCount);
    }


    [TestMethod]
    public void Analyze_EmptyBoiii_RemainsNative()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();

        var result = new ServerInstallationAnalyzer().Analyze(directory.Root);

        Assert.AreEqual(ManagedServerIntegrationKind.BoiiiNative, result.IntegrationKind);
        Assert.IsFalse(result.ThirdPartyGscDetected);
        Assert.AreEqual(0, result.GscFileCount);
    }

    [TestMethod]
    public void Analyze_ServerBat_DetectsDeclaredGamePort()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        File.WriteAllText(
            Path.Combine(directory.Root, "Server.bat"),
            "@echo off\r\nset GamePort=27021\r\nstart boiii.exe +set net_port \"%GamePort%\"\r\n");

        var result = new ServerInstallationAnalyzer().Analyze(directory.Root);

        Assert.AreEqual(27021, result.DetectedServerPort);
        Assert.AreEqual("Server.bat", result.DetectedServerPortLauncher);
    }

    [TestMethod]
    public void Analyze_KnownNamesWithUnknownContent_RemainThirdPartyAndFailClosed()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        File.WriteAllText(Path.Combine(directory.CustomScripts, "ezz_admin_01_main.gsc"), "// main");
        File.WriteAllText(Path.Combine(directory.CustomScripts, "ezz_admin_storage.gsc"), "// storage");
        File.WriteAllText(Path.Combine(directory.CustomScripts, "ezz_admin_control_center_contracts.gsc"), "// bridge");
        File.WriteAllText(Path.Combine(directory.Root, "Server.bat"), "@echo off\r\n");

        var result = new ServerInstallationAnalyzer().Analyze(directory.Root);

        Assert.IsFalse(result.PinteModDetected);
        Assert.IsFalse(result.ControlCenterBridgeDetected);
        Assert.AreEqual(ManagedServerIntegrationKind.ThirdPartyScripts, result.IntegrationKind);
        Assert.AreEqual(IntegrationCommandTransport.None, result.IntegrationProfile.CommandTransport);
        Assert.IsFalse(result.IntegrationProfile.Supports(IntegrationCapabilityKey.ServerCommands));
        CollectionAssert.Contains(result.LauncherCandidates.ToArray(), "Server.bat");
    }

    [TestMethod]
    public async Task Analyze_EmbeddedPinteModAndBridge_AreTrustedByReviewedHashes()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var payload = new EmbeddedServerPayloadService();
        var install = await payload.InstallPinteModStableAsync(directory.Root);
        Assert.IsTrue(install.Success, install.Message);
        var bridge = await payload.InstallOrUpdateBridgeAsync(directory.Root, ["zm_tomb"]);
        Assert.IsTrue(bridge.Success, bridge.Message);
        File.WriteAllText(Path.Combine(directory.Root, "Server.bat"), "@echo off\r\n");

        var result = new ServerInstallationAnalyzer().Analyze(directory.Root);

        Assert.IsTrue(result.PinteModDetected);
        Assert.IsTrue(result.ControlCenterBridgeDetected);
        Assert.AreEqual(ManagedServerIntegrationKind.PinteMod, result.IntegrationKind);
        Assert.AreEqual(IntegrationCommandTransport.PinteModClosedRconV1, result.IntegrationProfile.CommandTransport);
        Assert.IsTrue(result.IntegrationProfile.Supports(IntegrationCapabilityKey.ServerCommands));
    }


    [TestMethod]
    public void Analyze_ThirdPartyAudit_ObservesButNeverEnablesCommands()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        File.WriteAllText(
            Path.Combine(directory.CustomScripts, "community_admin.gsc"),
            "autoexec function init() { addcommand(\"god\", ::cmd_god); }\nfunction x(){ players = level.players; writefile(\"custom/state.json\", \"{}\"); }");

        var result = new ServerInstallationAnalyzer().Analyze(directory.Root);
        var profile = result.IntegrationProfile;

        Assert.AreEqual(ManagedServerIntegrationKind.ThirdPartyScripts, profile.Kind);
        Assert.AreEqual(IntegrationCommandTransport.None, profile.CommandTransport);
        Assert.AreEqual(IntegrationCapabilityAvailability.Observed, profile.Get(IntegrationCapabilityKey.ServerCommands));
        Assert.AreEqual(IntegrationCapabilityAvailability.Observed, profile.Get(IntegrationCapabilityKey.Players));
        CollectionAssert.Contains(profile.ThirdPartyAudit.DeclaredCommands.ToArray(), "god");
        CollectionAssert.Contains(profile.ThirdPartyAudit.ObservedFamilies.ToArray(), "Godmode");
    }

    [TestMethod]
    public async Task Analyze_PinteMod_EnablesOnlyClosedPinteModCommandTransport()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var install = await new EmbeddedServerPayloadService().InstallPinteModStableAsync(directory.Root);
        Assert.IsTrue(install.Success, install.Message);

        var profile = new ServerInstallationAnalyzer().Analyze(directory.Root).IntegrationProfile;

        Assert.AreEqual(IntegrationCommandTransport.PinteModClosedRconV1, profile.CommandTransport);
        Assert.IsTrue(profile.Supports(IntegrationCapabilityKey.Players));
        Assert.IsTrue(profile.Supports(IntegrationCapabilityKey.ServerCommands));
        Assert.IsTrue(profile.SupportsPinteModClosedCommands);
    }

    [TestMethod]
    public void Analyze_BoiiiWithoutLauncher_DisablesLifecycleCapability()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();

        var result = new ServerInstallationAnalyzer().Analyze(directory.Root);

        Assert.IsFalse(result.CanLaunchLocally);
        Assert.AreEqual(
            IntegrationCapabilityAvailability.Unavailable,
            result.IntegrationProfile.Get(IntegrationCapabilityKey.ServerLifecycle));
    }

    [TestMethod]
    public void Analyze_UnknownRoot_FailsClosed()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var result = new ServerInstallationAnalyzer().Analyze(missing);

        Assert.IsFalse(result.RootExists);
        Assert.IsFalse(result.BoiiiRootDetected);
        Assert.AreEqual(ManagedServerIntegrationKind.Unknown, result.IntegrationKind);
    }

    private sealed class TemporaryServerDirectory : IDisposable
    {
        public TemporaryServerDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.ManagerTests", Guid.NewGuid().ToString("N"));
        }

        public string Root { get; }
        public string CustomScripts => Path.Combine(Root, "boiii", "custom_scripts");

        public void CreateBoiii() => Directory.CreateDirectory(CustomScripts);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
