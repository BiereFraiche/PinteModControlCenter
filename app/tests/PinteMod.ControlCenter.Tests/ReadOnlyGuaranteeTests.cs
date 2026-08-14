using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ReadOnlyGuaranteeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ServerRoot_MustBeExplicitAbsoluteExistingAndNotVolumeRoot()
    {
        Assert.ThrowsException<ArgumentException>(() => new LocalPinteModOptions("relative-root"));
        Assert.ThrowsException<DirectoryNotFoundException>(() =>
            new LocalPinteModOptions(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        Assert.ThrowsException<ArgumentException>(() => new LocalPinteModOptions(Path.GetPathRoot(Path.GetTempPath())!));
    }

    [TestMethod]
    public void EveryAuthorizedPath_IsConfinedUnderServerRootAndIsNeverTmpOrBak()
    {
        using var root = new TemporaryServerRoot();
        var prefix = root.Root + Path.DirectorySeparatorChar;

        foreach (var file in Enum.GetValues<LocalPinteModFile>())
        {
            var path = root.Options.ResolvePath(file);
            Assert.IsTrue(path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
        }
    }

    [TestMethod]
    public void EveryBlockAPath_IsConfinedWhitelistedAndRejectsTemporaryOrTraversalInputs()
    {
        using var root = new TemporaryServerRoot();
        var prefix = root.Root + Path.DirectorySeparatorChar;

        foreach (var file in Enum.GetValues<BlockALocalFile>())
        {
            var path = root.BlockAPaths.ResolveFixed(file);
            Assert.IsTrue(path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
        }

        Assert.ThrowsException<InvalidOperationException>(() =>
            root.BlockAPaths.ResolveSessionLogPath("../outside", "connections.log"));
        Assert.ThrowsException<InvalidOperationException>(() =>
            root.BlockAPaths.ResolveSessionLogPath("session-safe", "server.log"));
        Assert.ThrowsException<InvalidOperationException>(() =>
            root.BlockAPaths.ResolveSessionLogPath("session-safe", "connections.log.tmp"));
        Assert.ThrowsException<InvalidOperationException>(() =>
            root.BlockAPaths.ResolveLocalizationFilePath(manual: true, "../1111111111111111.json"));
        Assert.ThrowsException<InvalidOperationException>(() =>
            root.BlockAPaths.ResolveLocalizationFilePath(manual: false, "1111111111111111.json.bak"));
    }

    [TestMethod]
    public async Task ReadingAllAuthorizedSessionHeartbeatAndRuntimeFiles_DoesNotChangeSizeDateOrHash()
    {
        using var root = new TemporaryServerRoot();
        var paths = new List<string> { root.WriteSession() };
        paths.AddRange(Enum.GetValues<LocalServiceKind>().Select(kind => root.WriteHeartbeat(kind, Now)));
        paths.Add(root.WritePinteModHeartbeat(Now.AddSeconds(-2)));
        paths.Add(root.WriteRuntimeSnapshot(Now.AddSeconds(-2)));
        var before = paths.ToDictionary(path => path, TemporaryServerRoot.Fingerprint);
        var clock = new FakeClock(Now);
        using var sessionReader = new SessionManifestReader(root.Options, clock);
        using var heartbeatReader = new ServiceHeartbeatReader(root.Options, clock);
        using var pinteModHeartbeatReader = new PinteModHeartbeatReader(root.Options, clock);
        using var runtimeReader = new ControlCenterRuntimeSnapshotReader(root.Options, clock);

        await sessionReader.ReadAsync();
        foreach (var service in Enum.GetValues<LocalServiceKind>())
        {
            await heartbeatReader.ReadAsync(service);
        }
        await pinteModHeartbeatReader.ReadAsync("session-local-001");
        await runtimeReader.ReadAsync("session-local-001", "zm_tomb");

        foreach (var path in paths)
        {
            Assert.AreEqual(before[path], TemporaryServerRoot.Fingerprint(path), $"Le fichier a changé : {path}");
        }
    }

    [TestMethod]
    public async Task ReadingControlCenterContracts_DoesNotChangeSizeDateOrHash()
    {
        using var root = new TemporaryServerRoot();
        var paths = new[]
        {
            root.Write(LocalPinteModFile.ControlCenterCapabilities, ContractCapabilities()),
            root.Write(LocalPinteModFile.ControlCenterActionFeedback, ContractFeedback()),
            root.Write(LocalPinteModFile.ControlCenterMapTransition, ContractTransition()),
            root.Write(LocalPinteModFile.ControlCenterServerIdentity, ContractIdentity())
        };
        foreach (var path in paths)
        {
            File.SetLastWriteTimeUtc(path, Now.AddSeconds(-2).UtcDateTime);
        }

        var before = paths.ToDictionary(path => path, TemporaryServerRoot.Fingerprint);
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));

        _ = await reader.ReadAsync("session-local-001", "zm_tomb");

        foreach (var path in paths)
        {
            Assert.AreEqual(before[path], TemporaryServerRoot.Fingerprint(path), $"Le fichier a changé : {path}");
        }
    }

    [TestMethod]
    public async Task ReadingRanksAndRoundRecords_DoesNotChangeSizeDateOrHash()
    {
        using var root = new TemporaryServerRoot();
        var paths = new[]
        {
            root.WriteRankProfile(
                "1111111111111111",
                RankProfileReaderTests.ProfileJson("1111111111111111", "ReadOnly", 2, 600, 20)),
            root.WriteMapRecords("zm_tomb", RoundRecordReaderTests.MapJson())
        };
        var before = paths.ToDictionary(path => path, TemporaryServerRoot.Fingerprint);
        var clock = new FakeClock(Now);
        using var rankReader = new RankProfileReader(root.Options, clock);
        using var recordReader = new RoundRecordReader(root.Options, clock);

        await rankReader.ReadAsync();
        await recordReader.ReadAsync();

        foreach (var path in paths)
        {
            Assert.AreEqual(before[path], TemporaryServerRoot.Fingerprint(path), $"Le fichier a changé : {path}");
        }
    }

    [TestMethod]
    public async Task ReadingOfficialEasterEggRecords_DoesNotChangeSizeDateOrHash()
    {
        using var root = new TemporaryServerRoot();
        var paths = new[]
        {
            root.WriteEasterEggProfiles(EasterEggRecordReaderTests.ProfilesJson()),
            root.WriteEasterEggMapRecords("zm_tomb", EasterEggRecordReaderTests.MapJson())
        };
        var before = paths.ToDictionary(path => path, TemporaryServerRoot.Fingerprint);
        using var reader = new EasterEggRecordReader(root.Options, new FakeClock(Now));

        await reader.ReadAsync();

        foreach (var path in paths)
        {
            Assert.AreEqual(before[path], TemporaryServerRoot.Fingerprint(path), $"Le fichier a changé : {path}");
        }
    }

    [TestMethod]
    public async Task ReadingAllBlockASources_DoesNotChangeSizeDateOrHash()
    {
        using var root = new TemporaryServerRoot();
        const string session = "session-local-001";
        var paths = new[]
        {
            root.WriteInstallationVerification(Now.AddHours(-1)),
            root.WriteBanServiceStatus(Now.AddSeconds(-2)),
            root.WriteRoles(),
            root.WriteLanguage("1111111111111111", "fr", manual: true),
            root.WriteSessionLog(session, "connections.log",
                "[1000 ms][round 1][JOIN] Safe | xuid=1111111111111111 | client=0 | players=1\n"),
            root.WriteSessionLog(session, "ranks.log", "[1200] MATCH_CLOCK_STARTED | round=1\n"),
            root.WriteCommunityPauseFeedback(PauseFeedback()),
            root.WriteCommunityPauseLog("[1300] STATUS | active=false | remaining=0\n")
        };
        var before = paths.ToDictionary(path => path, TemporaryServerRoot.Fingerprint);
        var clock = new FakeClock(Now);
        using var installation = new InstallationVerificationReader(root.Options, clock);
        using var ban = new BanServiceStatusReader(root.Options, clock);
        using var metadata = new LocalPlayerMetadataReader(root.Options, clock);
        using var logs = new StructuredLogReader(root.Options);
        using var pauseStatus = new CommunityPauseStatusReader(root.Options, clock);
        using var pauseLog = new CommunityPauseLogReader(root.Options, clock);

        await installation.ReadAsync();
        await ban.ReadAsync();
        await metadata.ReadAsync();
        await logs.ReadAsync(new SessionManifest(1, "2.1.1", session, "zm_tomb", 0));
        await pauseStatus.ReadAsync();
        await pauseLog.ReadAsync(new SessionManifest(1, "2.1.1", session, "zm_tomb", 0));

        foreach (var path in paths)
        {
            Assert.AreEqual(before[path], TemporaryServerRoot.Fingerprint(path), $"Le fichier a changé : {path}");
        }
    }

    private static string PauseFeedback() => """
        PINTEMOD_REMOTE_FEEDBACK_V1
        command=ezzpausestatus
        generated_gettime=120000
        ---
        PinteMod Community Pause - EXPERIMENTAL v0.3
        Active: false
        Automatic resume in: 0s
        Successful pauses: 0/2
        Pause proposals used: 0
        Pause vote: 20s | majority
        Resume vote: 15s | majority
        Public reminder: first=300s | every=720s
        Active vote: none
        Temporary God Mode: OFF
        Spectator spawn guard: OFF
        New AI spawning: normal
        Soft pause: map/EE script timers are NOT frozen
        END

        """;

    private static string ContractCapabilities() => """
        {"schema_version":1,"module_version":"2.1.1","contract_module_version":"0.1.3","command_contract_version":1,"session_id":"session-local-001","sequence":1,"generated_gettime":1,"updated_at_utc":"","time_authority":"session_gettime_and_file_mtime","map_code":"zm_tomb","map_source":"runtime","map_installation_authority":"unknown","map_count":0,"rotation_state":"unknown","rotation_entry_count":0,"change_map":false,"restart_map":true,"event_count":0,"boss_count":0,"power_up_count":0,"diagnostic_count":0,"transition_state":"idle","set_hostname":true,"set_join_password":true,"clear_join_password":true,"join_password_transport":"loopback_rcon_ephemeral","map_profile":"OFFICIAL","power_support":"SUPPORTED","pack_a_punch_support":"SUPPORTED","event_support":"NONE","boss_support":"NONE","music_support":"SUPPORTED","dog_round_support":"NONE","active_pintemod_bosses":0,"max_pintemod_bosses":2}
        """;

    private static string ContractFeedback() => """
        {"schema_version":1,"session_id":"session-local-001","sequence":1,"generated_gettime":1,"updated_at_utc":"","time_authority":"session_gettime_and_file_mtime","request_id":"request_0001","action":"restart_map","status":"accepted","result_code":"accepted"}
        """;

    private static string ContractTransition() => """
        {"schema_version":1,"request_id":"request_0001","action":"restart_map","requested_map":"zm_tomb","originating_session_id":"session-local-001","status":"transitioning","result_code":"transition_started","generated_gettime":1,"updated_at_utc":"","time_authority":"session_gettime_and_file_mtime"}
        """;

    private static string ContractIdentity() => """
        {"schema_version":1,"session_id":"session-local-001","sequence":1,"generated_gettime":1,"updated_at_utc":"","time_authority":"session_gettime_and_file_mtime","public_hostname":"PinteMod Test","public_hostname_state":"observed","join_password_enabled":false,"revision":1}
        """;
}
