using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PinteModDataRootLayoutTests
{
    [TestMethod]
    public void UncShareRoot_IsAcceptedOnlyForDirectPinteModDataLayout()
    {
        const string shareRoot = @"\\serveur-test\PinteModData";

        Assert.IsTrue(LocalPinteModOptions.IsSupportedRootShape(
            shareRoot,
            LocalPinteModRootLayout.PinteModDataRoot));
        Assert.IsFalse(LocalPinteModOptions.IsSupportedRootShape(
            shareRoot,
            LocalPinteModRootLayout.ServerRoot));
        Assert.IsFalse(LocalPinteModOptions.IsSupportedRootShape(
            Path.GetPathRoot(Path.GetTempPath())!,
            LocalPinteModRootLayout.PinteModDataRoot));
    }

    [TestMethod]
    public async Task ExplicitDataRoot_ReadsSessionHeartbeatsRanksAndPauseWithoutServerTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.DataRoot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new LocalPinteModOptions(root, LocalPinteModRootLayout.PinteModDataRoot);
            Assert.AreEqual(Path.GetFullPath(root), options.DataRoot);
            Assert.IsFalse(options.ResolvePath(LocalPinteModFile.CurrentSession)
                .Contains(Path.Combine("boiii", "scriptdata"), StringComparison.OrdinalIgnoreCase));

            Write(options.ResolvePath(LocalPinteModFile.CurrentSession), """
                {
                  "schema_version": 1,
                  "module_version": "2.1.1",
                  "session_id": "lan-session-001",
                  "map": "zm_tomb",
                  "started_gettime": 123456
                }
                """);
            foreach (var service in Enum.GetValues<LocalServiceKind>())
            {
                Write(options.ResolvePath(FileFor(service)), $$"""
                    {
                      "schema_version": 1,
                      "tool": "{{ToolFor(service)}}",
                      "version": "2.1.1",
                      "state": "running",
                      "sequence": 42,
                      "updated_utc": "{{DateTimeOffset.UtcNow:O}}",
                      "last_error_code": ""
                    }
                    """);
            }

            Write(Path.Combine(root, "ranks_v2", "players", "1111111111111111.json"),
                RankProfileReaderTests.ProfileJson("1111111111111111", "LAN", 1, 600, 20));
            Write(Path.Combine(root, "remote", "feedback.latest.txt"), """
                PINTEMOD_REMOTE_FEEDBACK_V1
                command=ezzpausestatus
                generated_gettime=1000
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
                """);

            var clock = new FakeClock(DateTimeOffset.UtcNow);
            using var sessionReader = new SessionManifestReader(options, clock);
            using var heartbeatReader = new ServiceHeartbeatReader(options, clock);
            using var rankReader = new RankProfileReader(options, clock);
            using var pauseReader = new CommunityPauseStatusReader(options, clock);

            var session = await sessionReader.ReadAsync();
            var supervisor = await heartbeatReader.ReadAsync(LocalServiceKind.Supervisor);
            var ranks = await rankReader.ReadAsync();
            var pause = await pauseReader.ReadAsync();

            Assert.AreEqual(LocalReadStatus.Success, session.Metadata.ReadStatus);
            Assert.AreEqual(LocalReadStatus.Success, supervisor.Metadata.ReadStatus);
            Assert.AreEqual(1, ranks.Value?.Profiles.Count);
            Assert.AreEqual(false, pause.Value?.Active);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Write(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }

    private static LocalPinteModFile FileFor(LocalServiceKind service) => service switch
    {
        LocalServiceKind.Supervisor => LocalPinteModFile.SupervisorHeartbeat,
        LocalServiceKind.BanService => LocalPinteModFile.BanServiceHeartbeat,
        LocalServiceKind.GeoIpBridge => LocalPinteModFile.GeoIpBridgeHeartbeat,
        LocalServiceKind.LiveConsole => LocalPinteModFile.LiveConsoleHeartbeat,
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
    };

    private static string ToolFor(LocalServiceKind service) => service switch
    {
        LocalServiceKind.Supervisor => "supervisor",
        LocalServiceKind.BanService => "ban_service",
        LocalServiceKind.GeoIpBridge => "geoip_bridge",
        LocalServiceKind.LiveConsole => "live_console",
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
    };
}
