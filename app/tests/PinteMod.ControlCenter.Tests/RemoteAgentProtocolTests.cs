using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RemoteAgentProtocolTests
{
    [TestMethod]
    public void SignedLaunchRequest_TamperingIsRejected()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var now = DateTimeOffset.UtcNow;
        var request = new RemoteLaunchRequest(
            RemoteAgentProtocol.SchemaVersion,
            "0123456789abcdef0123456789abcdef",
            "agent-test",
            RemoteAgentProtocol.LaunchAction,
            now,
            now.AddSeconds(60),
            "00112233445566778899AABBCCDDEEFF",
            string.Empty);
        request = request with { Signature = RemoteAgentProtocolService.SignRequest(request, secret) };

        Assert.IsTrue(RemoteAgentProtocolService.VerifyRequest(request, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyRequest(request with { Action = "anything" }, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyRequest(request with { AgentId = "agent-other" }, secret));
    }

    [TestMethod]
    public void SignedStopRequest_IsClosedAndTamperProtected()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var now = DateTimeOffset.UtcNow;
        var request = new RemoteLaunchRequest(
            RemoteAgentProtocol.SchemaVersion,
            "fedcba9876543210fedcba9876543210",
            "agent-test",
            RemoteAgentProtocol.StopAction,
            now,
            now.AddSeconds(60),
            "FFEEDDCCBBAA99887766554433221100",
            string.Empty);
        request = request with { Signature = RemoteAgentProtocolService.SignRequest(request, secret) };

        Assert.IsTrue(RemoteAgentProtocolService.VerifyRequest(request, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyRequest(request with { Action = "stop-anything" }, secret));
    }

    [TestMethod]
    public void SignedAgentUpdate_TamperingIsRejected()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var now = DateTimeOffset.UtcNow;
        var update = new RemoteAgentUpdateEnvelope(
            RemoteAgentProtocol.SchemaVersion,
            "agent-test",
            "2.3.0-preview-manager.3n",
            "PinteMod.ControlCenter.0123456789ABCDEF.exe",
            new string('A', 64),
            now,
            now.AddMinutes(5),
            string.Empty);
        update = update with { Signature = RemoteAgentProtocolService.SignUpdate(update, secret) };

        Assert.IsTrue(RemoteAgentProtocolService.VerifyUpdate(update, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyUpdate(update with { TargetVersion = "tampered" }, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyUpdate(update with { Sha256 = new string('B', 64) }, secret));
    }

    [TestMethod]
    public void SignedAvailablePackage_TamperingIsRejected()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var available = new RemoteAgentAvailablePackageEnvelope(
            RemoteAgentProtocol.SchemaVersion,
            "agent-test",
            "2.4.0-preview-integration.4b1.fix4",
            "available.0123456789ABCDEF.exe",
            new string('A', 64),
            DateTimeOffset.UtcNow,
            string.Empty);
        available = available with { Signature = RemoteAgentProtocolService.SignAvailablePackage(available, secret) };

        Assert.IsTrue(RemoteAgentProtocolService.VerifyAvailablePackage(available, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyAvailablePackage(available with { Version = "tampered" }, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyAvailablePackage(available with { Sha256 = new string('B', 64) }, secret));
    }


    [TestMethod]
    public void SignedProfileCatalog_TamperingIsRejected()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var catalog = new RemoteAgentProfileCatalogEnvelope(
            RemoteAgentProtocol.SchemaVersion,
            "agent-source",
            "PC-SERVEUR",
            DateTimeOffset.UtcNow,
            [new RemoteAgentProfileCatalogEntry("agent-target", "Server4", "Server4", "Server.bat", 27020, false)],
            string.Empty);
        catalog = catalog with { Signature = RemoteAgentProtocolService.SignProfileCatalog(catalog, secret) };

        Assert.IsTrue(RemoteAgentProtocolService.VerifyProfileCatalog(catalog, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyProfileCatalog(
            catalog with { Profiles = [catalog.Profiles[0] with { RootFolderName = "Server5" }] }, secret));
    }

    [TestMethod]
    public void SignedServerRuntime_TamperingIsRejected()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var runtime = new RemoteAgentServerRuntimeEnvelope(
            RemoteAgentProtocol.SchemaVersion,
            "agent-test",
            DateTimeOffset.UtcNow,
            true,
            string.Empty);
        runtime = runtime with { Signature = RemoteAgentProtocolService.SignServerRuntime(runtime, secret) };

        Assert.IsTrue(RemoteAgentProtocolService.VerifyServerRuntime(runtime, secret));
        Assert.IsFalse(RemoteAgentProtocolService.VerifyServerRuntime(runtime with { ServerRunning = false }, secret));
    }

    [TestMethod]
    public void CatalogPathResolver_OnlyBuildsSiblingUncRoots()
    {
        CollectionAssert.AreEqual(
            new[] { @"\\PC\Servers\Server4", @"\\PC\Server4" },
            RemoteAgentCatalogPathResolver.BuildSiblingCandidates(@"\\PC\Servers\Server3", "Server4").ToArray());
        Assert.AreEqual(0, RemoteAgentCatalogPathResolver.BuildSiblingCandidates(@"C:\Servers\Server3", "Server4").Count);
        Assert.AreEqual(0, RemoteAgentCatalogPathResolver.BuildSiblingCandidates(@"\\PC\Servers\Server3", @"..\evil").Count);
    }

    [TestMethod]
    public void QueuePaths_RemainUnderExplicitServerRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.RemoteAgentTests", "Server3");
        var requestRoot = RemoteAgentProtocolService.GetRequestsPath(root);
        var updatesRoot = RemoteAgentProtocolService.GetUpdatesPath(root);
        StringAssert.StartsWith(Path.GetFullPath(requestRoot), Path.GetFullPath(root));
        StringAssert.StartsWith(Path.GetFullPath(updatesRoot), Path.GetFullPath(root));
        StringAssert.Contains(requestRoot, RemoteAgentProtocol.QueueFolderName);
    }
    [TestMethod]
    public void ExactPublishedAgentBuild_ClockSkewDoesNotInvalidateCryptographicProof()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        const string agentId = "agent-test";
        const string version = "2.4.0-preview-integration.4b1.fix11";
        var sha = new string('A', 64);

        // Deliberately use timestamps several minutes apart. Confirmation must
        // depend on HMAC + version + exact SHA-256, not on synchronized clocks.
        var status = new RemoteAgentStatusEnvelope(
            RemoteAgentProtocol.SchemaVersion, agentId, "Server4", "PC-SERVEUR", "online",
            DateTimeOffset.UtcNow.AddMinutes(-7), version, string.Empty);
        status = status with { Signature = RemoteAgentProtocolService.SignStatus(status, secret) };

        var available = new RemoteAgentAvailablePackageEnvelope(
            RemoteAgentProtocol.SchemaVersion, agentId, version,
            "available.AAAAAAAAAAAAAAAA.exe", sha, DateTimeOffset.UtcNow.AddMinutes(6), string.Empty);
        available = available with { Signature = RemoteAgentProtocolService.SignAvailablePackage(available, secret) };

        Assert.IsTrue(RemoteLaunchClientService.IsExactPublishedAgentBuild(
            status, available, agentId, version, sha, secret));
    }

    [TestMethod]
    public void ExactPublishedAgentBuild_WrongHashIsRejected()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        const string agentId = "agent-test";
        const string version = "2.4.0-preview-integration.4b1.fix11";
        var publishedSha = new string('A', 64);

        var status = new RemoteAgentStatusEnvelope(
            RemoteAgentProtocol.SchemaVersion, agentId, "Server4", "PC-SERVEUR", "online",
            DateTimeOffset.UtcNow, version, string.Empty);
        status = status with { Signature = RemoteAgentProtocolService.SignStatus(status, secret) };

        var available = new RemoteAgentAvailablePackageEnvelope(
            RemoteAgentProtocol.SchemaVersion, agentId, version,
            "available.AAAAAAAAAAAAAAAA.exe", publishedSha, DateTimeOffset.UtcNow, string.Empty);
        available = available with { Signature = RemoteAgentProtocolService.SignAvailablePackage(available, secret) };

        Assert.IsFalse(RemoteLaunchClientService.IsExactPublishedAgentBuild(
            status, available, agentId, version, new string('B', 64), secret));
    }

    [TestMethod]
    public void AuthenticatedHeartbeatAdvance_RequiresSameAgentValidSignatureAndChangedSequenceTime()
    {
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        const string agentId = "agent-test";
        var initial = new RemoteAgentStatusEnvelope(
            RemoteAgentProtocol.SchemaVersion, agentId, "Server4", "PC-SERVEUR", "online",
            DateTimeOffset.Parse("2026-08-21T10:00:00Z"), "2.4.0-preview-integration.4b1.fix9", string.Empty);
        initial = initial with { Signature = RemoteAgentProtocolService.SignStatus(initial, secret) };

        var advanced = initial with { UpdatedAtUtc = initial.UpdatedAtUtc.AddSeconds(5), Signature = string.Empty };
        advanced = advanced with { Signature = RemoteAgentProtocolService.SignStatus(advanced, secret) };
        Assert.IsTrue(RemoteLaunchClientService.IsAuthenticatedHeartbeatAdvance(initial, advanced, agentId, secret));

        Assert.IsFalse(RemoteLaunchClientService.IsAuthenticatedHeartbeatAdvance(initial, initial, agentId, secret));
        Assert.IsFalse(RemoteLaunchClientService.IsAuthenticatedHeartbeatAdvance(initial, advanced with { AgentId = "agent-other" }, agentId, secret));
    }

}
