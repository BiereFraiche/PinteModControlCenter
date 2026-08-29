using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ManagedServerRemoteAgentTests
{
    [TestMethod]
    public async Task ManagedProfile_PersistsRemoteAgentIdWithoutChangingLauncher()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.ManagedRemoteTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "managed-server.json");
        try
        {
            var store = new JsonManagedServerProfileStore(path);
            await store.SaveAsync(new ManagedServerProfileConfiguration(
                ManagedServerProfileConfiguration.CurrentSchemaVersion,
                "Server.bat")
            {
                RemoteAgentId = "agent-0123456789abcdef"
            });

            var loaded = await store.LoadAsync();
            Assert.AreEqual("Server.bat", loaded.LauncherRelativePath);
            Assert.AreEqual("agent-0123456789abcdef", loaded.RemoteAgentId);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ManagedProfile_LaunchIsDisabledWhenServerIsAlreadyRunning()
    {
        var configuration = OperatorConfiguration.Default with
        {
            ServerRoot = @"C:\Server3",
            RconPort = 27021
        };
        var profile = new ServerManagerProfileViewModel(
            "server3",
            configuration,
            new ManagedServerProfileConfiguration(ManagedServerProfileConfiguration.CurrentSchemaVersion, "Server.bat"));
        profile.ApplyAnalysis(new ManagedServerAnalysis(
            true, true, true, true, true, false, false, 35, ["Server.bat"],
            ManagedServerIntegrationKind.PinteMod, "PinteMod"));

        Assert.IsTrue(profile.CanLaunchSelected);
        profile.ApplyServerRunning(true);
        Assert.IsTrue(profile.ServerRunning);
        Assert.IsFalse(profile.CanLaunchSelected);
    }

    [TestMethod]
    public void ManagedProfile_ExistingPinteMod_OffersOnlyTheSafeFirstPartyRepair()
    {
        var profile = new ServerManagerProfileViewModel(
            "server3",
            OperatorConfiguration.Default with { ServerRoot = @"C:\Server3" },
            ManagedServerProfileConfiguration.Default);
        profile.ApplyAnalysis(new ManagedServerAnalysis(
            true, true, true, true, true, false, false, 35, ["Server.bat"],
            ManagedServerIntegrationKind.PinteMod, "PinteMod"));

        Assert.IsTrue(profile.CanRepairPinteModSafely);
        Assert.IsTrue(profile.CanInstallPinteMod);
        Assert.AreEqual("VÉRIFIER / RÉPARER PINTE MOD", profile.PinteModInstallActionLabel);
    }
    [TestMethod]
    public void ManagedProfile_RemotePairing_IsVisuallyExplicitWhenOnline()
    {
        var configuration = OperatorConfiguration.Default with
        {
            ServerRoot = @"\\server-pc\Server3",
            RconPort = 27021
        };
        var profile = new ServerManagerProfileViewModel(
            "server3",
            configuration,
            new ManagedServerProfileConfiguration(ManagedServerProfileConfiguration.CurrentSchemaVersion, "Server.bat"));
        profile.ApplyAnalysis(new ManagedServerAnalysis(
            true, true, true, true, true, false, true, 35, ["Server.bat"],
            ManagedServerIntegrationKind.PinteMod, "PinteMod"));

        profile.ApplyRemoteAgentProbe(new RemoteAgentProbeResult(true, true, true, "Agent ONLINE")
        {
            MachineName = "SERVER-PC",
            AgentVersion = "2.4.0-preview-onboarding.4a7"
        });

        Assert.IsTrue(profile.RemoteConnectionLinked);
        Assert.AreEqual("PC SERVEUR RELIÉ", profile.RemoteConnectionTitle);
        StringAssert.Contains(profile.RemoteConnectionSummary, "SERVER-PC");
        StringAssert.Contains(profile.RemoteConnectionSummary, "appairé");
        Assert.IsFalse(profile.CanPairRemoteAgent);
    }

    [TestMethod]
    public void ManagedProfile_RemotePairing_ShowsPairedOfflineWithoutAskingToPairAgain()
    {
        var configuration = OperatorConfiguration.Default with
        {
            ServerRoot = @"\\server-pc\Server3",
            RconPort = 27021
        };
        var profile = new ServerManagerProfileViewModel(
            "server3",
            configuration,
            new ManagedServerProfileConfiguration(ManagedServerProfileConfiguration.CurrentSchemaVersion, "Server.bat"));
        profile.ApplyAnalysis(new ManagedServerAnalysis(
            true, true, true, true, true, false, true, 35, ["Server.bat"],
            ManagedServerIntegrationKind.PinteMod, "PinteMod"));

        profile.ApplyRemoteAgentProbe(new RemoteAgentProbeResult(true, true, false, "Agent OFFLINE")
        {
            MachineName = "SERVER-PC",
            AgentVersion = "2.4.0-preview-onboarding.4a7"
        });

        Assert.IsTrue(profile.RemoteConnectionPairedOffline);
        StringAssert.Contains(profile.RemoteConnectionTitle, "APPARIÉ");
        Assert.IsFalse(profile.CanPairRemoteAgent);
    }

}
