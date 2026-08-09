using System.Security.Cryptography;
using System.Text;
using System.IO;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

internal sealed class TemporaryServerRoot : IDisposable
{
    public TemporaryServerRoot()
    {
        Root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Options = new LocalPinteModOptions(Root);
        BlockAPaths = new BlockALocalPathPolicy(Options);
    }

    public string Root { get; }

    public LocalPinteModOptions Options { get; }

    public BlockALocalPathPolicy BlockAPaths { get; }

    public string WriteSession(
        string map = "zm_tomb",
        string sessionId = "session-local-001",
        string moduleVersion = "2.1.1",
        int schemaVersion = 1)
    {
        var json = $$"""
            {
              "schema_version": {{schemaVersion}},
              "module_version": "{{moduleVersion}}",
              "session_id": "{{sessionId}}",
              "map": "{{map}}",
              "started_gettime": 123456
            }
            """;
        return Write(LocalPinteModFile.CurrentSession, json);
    }

    public string WriteHeartbeat(
        LocalServiceKind service,
        DateTimeOffset updatedAtUtc,
        string state = "running",
        long sequence = 7,
        int schemaVersion = 1)
    {
        var file = FileFor(service);
        var tool = ToolFor(service);
        var json = $$"""
            {
              "schema_version": {{schemaVersion}},
              "tool": "{{tool}}",
              "version": "2.1.1",
              "state": "{{state}}",
              "sequence": {{sequence}},
              "updated_utc": "{{updatedAtUtc:O}}",
              "last_error_code": ""
            }
            """;
        return Write(file, json);
    }

    public string Write(LocalPinteModFile file, string contents)
    {
        var path = Options.ResolvePath(file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public string WriteRankProfile(string xuid, string contents, string? suffix = null)
    {
        var directory = Path.Combine(Root, "boiii", "scriptdata", "pintemod", "ranks_v2", "players");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, xuid + (suffix ?? ".json"));
        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public string WriteMapRecords(string mapCode, string contents, string? suffix = null)
    {
        var directory = Path.Combine(Root, "boiii", "scriptdata", "pintemod", "ranks_v2", "maps");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, mapCode + (suffix ?? ".json"));
        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public string WriteEasterEggProfiles(string contents)
    {
        var directory = Path.Combine(Root, "boiii", "scriptdata", "pintemod", "easter_eggs_v2");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "profiles.json");
        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public string WriteEasterEggMapRecords(
        string mapCode,
        string contents,
        string relativeDirectory = "maps",
        string suffix = ".json")
    {
        var directory = Path.Combine(
            Root,
            "boiii",
            "scriptdata",
            "pintemod",
            "easter_eggs_v2",
            relativeDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, mapCode + suffix);
        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public string WriteInstallationVerification(DateTimeOffset checkedAtUtc, string? resultsJson = null)
    {
        resultsJson ??= """
            [
              { "status": "PASS", "check": "Structure locale", "details": "C:\\private\\server", "recommendation": "Aucune" }
            ]
            """;
        var json = $$"""
            {
              "schema_version": 1,
              "tool": "Verify_PinteMod_Installation",
              "version": "2.1.1",
              "checked_utc": "{{checkedAtUtc:O}}",
              "root": "C:\\private\\server",
              "pass": 1,
              "warning": 0,
              "error": 0,
              "results": { "value": {{resultsJson}}, "Count": 1 }
            }
            """;
        return WriteBlockA(BlockALocalFile.InstallationVerification, json);
    }

    public string WriteBanServiceStatus(DateTimeOffset updatedAtUtc, int activeBans = 2)
    {
        var json = $$"""
            {
              "schema_version": 1,
              "version": "2.1.1",
              "running": true,
              "updated_utc": "{{updatedAtUtc:O}}",
              "active_bans": {{activeBans}},
              "privacy": "strict"
            }
            """;
        return WriteBlockA(BlockALocalFile.BanServiceStatus, json);
    }

    public string WriteRoles(string xuid = "1111111111111111", string role = "moderator")
    {
        var json = $$"""
            {
              "schema_version": 1,
              "identity_kind": "BOIII_XUID",
              "display_1": "Joueur local",
              "xuid_1": "{{xuid}}",
              "role_1": "{{role}}",
              "count": 1,
              "updated_gettime": 1234,
              "updated_by": "1111111111111111"
            }
            """;
        return WriteBlockA(BlockALocalFile.Roles, json);
    }

    public string WriteLanguage(string xuid, string language, bool manual)
    {
        var directory = BlockAPaths.ResolveLocalizationDirectory(manual);
        Directory.CreateDirectory(directory);
        var path = BlockAPaths.ResolveLocalizationFilePath(manual, xuid + ".json");
        File.WriteAllText(path, $$"""{ "xuid": "{{xuid}}", "language": "{{language}}" }""", new UTF8Encoding(false));
        return path;
    }

    public string WriteSessionLog(string sessionId, string fileName, string contents)
    {
        var path = BlockAPaths.ResolveSessionLogPath(sessionId, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }

    public string WriteCommunityPauseFeedback(string contents) =>
        WriteBlockA(BlockALocalFile.CommunityPauseFeedback, contents);

    public string WriteCommunityPauseLog(string contents) =>
        WriteBlockA(BlockALocalFile.CommunityPauseLog, contents);

    public string WriteBlockA(BlockALocalFile file, string contents)
    {
        var path = BlockAPaths.ResolveFixed(file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }

    public static LocalPinteModFile FileFor(LocalServiceKind service) => service switch
    {
        LocalServiceKind.Supervisor => LocalPinteModFile.SupervisorHeartbeat,
        LocalServiceKind.BanService => LocalPinteModFile.BanServiceHeartbeat,
        LocalServiceKind.GeoIpBridge => LocalPinteModFile.GeoIpBridgeHeartbeat,
        LocalServiceKind.LiveConsole => LocalPinteModFile.LiveConsoleHeartbeat,
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
    };

    public static string ToolFor(LocalServiceKind service) => service switch
    {
        LocalServiceKind.Supervisor => "supervisor",
        LocalServiceKind.BanService => "ban_service",
        LocalServiceKind.GeoIpBridge => "geoip_bridge",
        LocalServiceKind.LiveConsole => "live_console",
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
    };

    public static FileFingerprint Fingerprint(string path)
    {
        var info = new FileInfo(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new FileFingerprint(
            info.Length,
            info.LastWriteTimeUtc,
            Convert.ToHexString(SHA256.HashData(stream)));
    }

    public void Dispose()
    {
        if (Directory.Exists(Root) &&
            Path.GetFullPath(Root).StartsWith(Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.Tests"), StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed record FileFingerprint(long Length, DateTime LastWriteTimeUtc, string Sha256);
