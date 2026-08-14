using System.IO;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ControlCenterContractReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ValidContracts_AreParsedFromFourBoundedReadOnlySources()
    {
        using var root = new TemporaryServerRoot();
        var paths = WriteContracts(root, Now.AddSeconds(-2));
        var before = paths.ToDictionary(path => path, TemporaryServerRoot.Fingerprint);
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Success, result.Capabilities.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Fresh, result.Capabilities.Metadata.Freshness);
        Assert.IsTrue(result.Capabilities.Value!.RestartMap);
        Assert.AreEqual("zm_tomb", result.Capabilities.Value.SupportedMaps.Single().Code);
        Assert.AreEqual("margwa", result.Capabilities.Value.BossAliases.Single());
        Assert.AreEqual(ControlCenterFeedbackStatus.Accepted, result.ActionFeedback.Value!.Status);
        Assert.AreEqual(ControlCenterTransitionStatus.Transitioning, result.MapTransition.Value!.Status);
        Assert.AreEqual(7L, result.ServerIdentity.Value!.Revision);
        Assert.IsTrue(result.ServerIdentity.Value.JoinPasswordEnabled);
        foreach (var path in paths)
        {
            Assert.AreEqual(before[path], TemporaryServerRoot.Fingerprint(path));
        }
    }

    [TestMethod]
    public async Task CapabilitiesV014_IsAcceptedAndItsVersionIsPreserved()
    {
        using var root = new TemporaryServerRoot();
        WriteContracts(root, Now.AddSeconds(-2));
        var capabilityPath = root.Options.ResolvePath(LocalPinteModFile.ControlCenterCapabilities);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(capabilityPath))!.AsObject();
        json["contract_module_version"] = "0.1.4";
        await File.WriteAllTextAsync(capabilityPath, json.ToJsonString());
        File.SetLastWriteTimeUtc(capabilityPath, Now.AddSeconds(-1).UtcDateTime);
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Success, result.Capabilities.Metadata.ReadStatus);
        Assert.AreEqual("0.1.4", result.Capabilities.Value!.ContractModuleVersion);
        Assert.IsTrue(result.Capabilities.Value.SetHostname);
        Assert.IsTrue(result.Capabilities.Value.SetJoinPassword);
    }

    [TestMethod]
    public async Task UnknownCapabilitiesVersion_IsRejectedWithoutAffectingIdentity()
    {
        using var root = new TemporaryServerRoot();
        WriteContracts(root, Now.AddSeconds(-2));
        var capabilityPath = root.Options.ResolvePath(LocalPinteModFile.ControlCenterCapabilities);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(capabilityPath))!.AsObject();
        json["contract_module_version"] = "0.1.5";
        await File.WriteAllTextAsync(capabilityPath, json.ToJsonString());
        File.SetLastWriteTimeUtc(capabilityPath, Now.AddSeconds(-1).UtcDateTime);
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Invalid, result.Capabilities.Metadata.ReadStatus);
        Assert.AreEqual(LocalReadStatus.Success, result.ServerIdentity.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task SupportedMapNeverBecomesInstalledAndChangeMapTrueIsRejected()
    {
        using var root = new TemporaryServerRoot();
        WriteContracts(root, Now.AddSeconds(-2));
        var capabilityPath = root.Options.ResolvePath(LocalPinteModFile.ControlCenterCapabilities);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(capabilityPath))!.AsObject();
        json["change_map"] = true;
        await File.WriteAllTextAsync(capabilityPath, json.ToJsonString());
        File.SetLastWriteTimeUtc(capabilityPath, Now.AddSeconds(-1).UtcDateTime);
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Invalid, result.Capabilities.Metadata.ReadStatus);
        Assert.IsNull(result.Capabilities.Value);
    }

    [TestMethod]
    public async Task UnexpectedDynamicEntryOrRootShape_IsRejectedWithoutInterruptingOtherSources()
    {
        using var root = new TemporaryServerRoot();
        WriteContracts(root, Now.AddSeconds(-2));
        var capabilityPath = root.Options.ResolvePath(LocalPinteModFile.ControlCenterCapabilities);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(capabilityPath))!.AsObject();
        json["boss_2_alias"] = "panzer";
        await File.WriteAllTextAsync(capabilityPath, json.ToJsonString());
        await File.WriteAllTextAsync(
            root.Options.ResolvePath(LocalPinteModFile.ControlCenterServerIdentity),
            "[]");
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Invalid, result.Capabilities.Metadata.ReadStatus);
        Assert.AreEqual(LocalReadStatus.Invalid, result.ServerIdentity.Metadata.ReadStatus);
        Assert.AreEqual(LocalReadStatus.Success, result.ActionFeedback.Metadata.ReadStatus);
        Assert.AreEqual(LocalReadStatus.Success, result.MapTransition.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task RuntimeNativeNumbersAndBooleans_AreAcceptedWhileQuotedScalarsAreRejected()
    {
        using var root = new TemporaryServerRoot();
        WriteContracts(root, Now.AddSeconds(-2));
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));

        var native = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Success, native.Capabilities.Metadata.ReadStatus);
        Assert.AreEqual(LocalReadStatus.Success, native.ServerIdentity.Metadata.ReadStatus);
        var identityPath = root.Options.ResolvePath(LocalPinteModFile.ControlCenterServerIdentity);
        var quoted = JsonNode.Parse(await File.ReadAllTextAsync(identityPath))!.AsObject();
        quoted["join_password_enabled"] = "true";
        await File.WriteAllTextAsync(identityPath, quoted.ToJsonString());
        File.SetLastWriteTimeUtc(identityPath, Now.AddSeconds(-1).UtcDateTime);

        var invalid = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Invalid, invalid.ServerIdentity.Metadata.ReadStatus);
        Assert.AreEqual(DataProvenance.MemoryCache, invalid.ServerIdentity.Metadata.Provenance);
    }

    [TestMethod]
    public async Task SessionOrMapMismatch_IsRejectedAndCannotReusePreviousSessionCache()
    {
        using var root = new TemporaryServerRoot();
        WriteContracts(root, Now.AddSeconds(-2));
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));
        Assert.IsNotNull((await reader.ReadAsync("session-local-001", "zm_tomb")).Capabilities.Value);

        var result = await reader.ReadAsync("session-local-002", "zm_castle");

        Assert.IsNull(result.Capabilities.Value);
        Assert.IsNull(result.ServerIdentity.Value);
        Assert.AreEqual(DataProvenance.LocalFile, result.Capabilities.Metadata.Provenance);
    }

    [TestMethod]
    public async Task InvalidCurrentRead_ReturnsMemoryCacheButNeverAsFreshLocal()
    {
        using var root = new TemporaryServerRoot();
        WriteContracts(root, Now.AddSeconds(-2));
        var clock = new FakeClock(Now);
        using var reader = new ControlCenterContractReader(root.Options, clock);
        _ = await reader.ReadAsync("session-local-001", "zm_tomb");
        await File.WriteAllTextAsync(
            root.Options.ResolvePath(LocalPinteModFile.ControlCenterCapabilities),
            "{");
        clock.UtcNow = Now.AddSeconds(20);

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.IsNotNull(result.Capabilities.Value);
        Assert.AreEqual(DataProvenance.MemoryCache, result.Capabilities.Metadata.Provenance);
        Assert.AreEqual(DataFreshness.Stale, result.Capabilities.Metadata.Freshness);
        Assert.AreEqual(LocalReadStatus.Invalid, result.Capabilities.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task LateTransitionRemainsTransitioningEvenWhenSourceIsExpired()
    {
        using var root = new TemporaryServerRoot();
        WriteContracts(root, Now.AddSeconds(-60));
        using var reader = new ControlCenterContractReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(ControlCenterTransitionStatus.Transitioning, result.MapTransition.Value!.Status);
        Assert.AreEqual(DataFreshness.Expired, result.MapTransition.Metadata.Freshness);
    }

    [TestMethod]
    public async Task TmpAndBakAreNeverPromotedAndOversizedCapabilitiesAreRejected()
    {
        using var root = new TemporaryServerRoot();
        var active = root.Options.ResolvePath(LocalPinteModFile.ControlCenterCapabilities);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        await File.WriteAllTextAsync(active + ".tmp", CapabilitiesJson());
        await File.WriteAllTextAsync(active + ".bak", CapabilitiesJson());
        using var missingReader = new ControlCenterContractReader(root.Options, new FakeClock(Now));
        var missing = await missingReader.ReadAsync("session-local-001", "zm_tomb");
        Assert.AreEqual(LocalReadStatus.Missing, missing.Capabilities.Metadata.ReadStatus);

        await File.WriteAllTextAsync(
            active,
            new string('x', ControlCenterContractReader.CapabilitiesMaximumFileSizeBytes + 1));
        using var oversizedReader = new ControlCenterContractReader(root.Options, new FakeClock(Now));
        var oversized = await oversizedReader.ReadAsync("session-local-001", "zm_tomb");
        Assert.AreEqual(LocalReadStatus.Invalid, oversized.Capabilities.Metadata.ReadStatus);
    }

    private static string[] WriteContracts(TemporaryServerRoot root, DateTimeOffset timestamp)
    {
        var paths = new[]
        {
            root.Write(LocalPinteModFile.ControlCenterCapabilities, CapabilitiesJson()),
            root.Write(LocalPinteModFile.ControlCenterActionFeedback, FeedbackJson()),
            root.Write(LocalPinteModFile.ControlCenterMapTransition, TransitionJson()),
            root.Write(LocalPinteModFile.ControlCenterServerIdentity, IdentityJson())
        };
        foreach (var path in paths)
        {
            File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
        }

        return paths;
    }

    private static string CapabilitiesJson() => """
        {
          "schema_version":1,"module_version":"2.1.1","contract_module_version":"0.1.3",
          "command_contract_version":1,"session_id":"session-local-001","sequence":7,
          "generated_gettime":25000,"updated_at_utc":"","time_authority":"session_gettime_and_file_mtime",
          "map_code":"zm_tomb","map_source":"runtime","map_installation_authority":"unknown",
          "map_count":1,"map_1_code":"zm_tomb","map_1_display_name":"Origins","map_1_availability":"supported",
          "rotation_state":"unknown","rotation_entry_count":0,"change_map":false,"restart_map":true,
          "event_count":0,"boss_count":1,"boss_1_alias":"margwa",
          "power_up_count":1,"power_up_1_alias":"max_ammo","diagnostic_count":3,
          "diagnostic_1_alias":"map_audit","diagnostic_2_alias":"event_status","diagnostic_3_alias":"power_ups",
          "transition_state":"idle","set_hostname":true,"set_join_password":true,
          "clear_join_password":true,"join_password_transport":"loopback_rcon_ephemeral",
          "map_profile":"OFFICIAL","power_support":"SUPPORTED","pack_a_punch_support":"SUPPORTED",
          "event_support":"SUPPORTED_MARGWA","boss_support":"MARGWA","music_support":"SUPPORTED",
          "dog_round_support":"NOT_DECLARED","active_pintemod_bosses":0,"max_pintemod_bosses":2
        }
        """;

    private static string FeedbackJson() => """
        {
          "schema_version":1,"session_id":"session-local-001","sequence":12,
          "generated_gettime":31000,"updated_at_utc":"","time_authority":"session_gettime_and_file_mtime",
          "request_id":"request_0001","action":"restart_map","status":"accepted","result_code":"accepted"
        }
        """;

    private static string TransitionJson() => """
        {
          "schema_version":1,"request_id":"request_0001","action":"restart_map",
          "requested_map":"zm_tomb","originating_session_id":"session-local-001",
          "status":"transitioning","result_code":"transition_started","generated_gettime":33000,
          "updated_at_utc":"","time_authority":"session_gettime_and_file_mtime"
        }
        """;

    private static string IdentityJson() => """
        {
          "schema_version":1,"session_id":"session-local-001","sequence":3,"generated_gettime":8000,
          "updated_at_utc":"","time_authority":"session_gettime_and_file_mtime",
          "public_hostname":"PinteMod Test","public_hostname_state":"observed",
          "join_password_enabled":true,"revision":7
        }
        """;
}
