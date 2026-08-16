using System.IO;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ControlCenterRuntimeSnapshotReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ValidSnapshot_ParsesAuthoritativeServerPlayerInventoryAndEmptyUtc()
    {
        using var root = new TemporaryServerRoot();
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2));
        using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Fresh, result.Metadata.Freshness);
        Assert.AreEqual(12, result.Value?.Round);
        Assert.AreEqual(TimeSpan.FromMilliseconds(122456), result.Value?.SessionElapsed);
        Assert.AreEqual(RankedStatus.Ranked, result.Value?.RankedStatus);
        Assert.AreEqual(RuntimePackAPunchState.Available, result.Value?.PackAPunchState);
        var player = result.Value!.Players.Single();
        Assert.AreEqual("0000000000000001", player.Xuid);
        Assert.AreEqual(PlayerLifeState.Alive, player.LifeState);
        Assert.AreEqual(12500, player.Points);
        Assert.AreEqual("ray_gun", player.EquippedWeapon);
        Assert.AreEqual(RuntimeWeaponPackAPunchState.Upgraded, player.Weapons.Single().PackAPunchState);
        CollectionAssert.AreEqual(new[] { "jug", "speed" }, player.Perks.ToArray());
        Assert.IsNull(result.Value.DeclaredUpdatedAtUtc);
    }

    [DataTestMethod]
    [DataRow(2, DataFreshness.Fresh)]
    [DataRow(20, DataFreshness.Stale)]
    [DataRow(46, DataFreshness.Expired)]
    public async Task FileMtime_DeterminesFreshness(int ageSeconds, DataFreshness expected)
    {
        using var root = new TemporaryServerRoot();
        root.WriteRuntimeSnapshot(Now.AddSeconds(-ageSeconds));
        using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(expected, result.Metadata.Freshness);
    }

    [TestMethod]
    public async Task OptionalRuntimeValuesMayBeAbsentWithoutInventingData()
    {
        using var root = new TemporaryServerRoot();
        var json = ParseFixture();
        foreach (var name in new[]
                 {
                     "round", "session_started_gettime", "session_elapsed_ms", "max_players",
                     "player_1_points", "player_1_health", "player_1_max_health",
                     "player_1_equipped_ammo_clip", "player_1_equipped_ammo_reserve"
                 })
        {
            json.Remove(name);
        }

        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), json.ToJsonString());
        using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
        Assert.IsNull(result.Value?.Round);
        Assert.IsNull(result.Value?.SessionElapsed);
        Assert.IsNull(result.Value?.MaximumPlayers);
        Assert.IsNull(result.Value?.Players.Single().Points);
    }

    [DataTestMethod]
    [DataRow("schema_version", "2", LocalReadStatus.UnsupportedSchema)]
    [DataRow("sequence", "-1", LocalReadStatus.Invalid)]
    [DataRow("generated_gettime", "-1", LocalReadStatus.Invalid)]
    [DataRow("map_code", "../zone", LocalReadStatus.Invalid)]
    [DataRow("round", "999", LocalReadStatus.Invalid)]
    [DataRow("ranked_status", "maybe", LocalReadStatus.Invalid)]
    [DataRow("power_state", "enabled", LocalReadStatus.Invalid)]
    [DataRow("pack_a_punch_state", "ready", LocalReadStatus.Invalid)]
    [DataRow("player_1_xuid", "invalid", LocalReadStatus.Invalid)]
    [DataRow("player_1_life_state", "respawning", LocalReadStatus.Invalid)]
    [DataRow("player_1_perk_1", "unknown_perk", LocalReadStatus.Invalid)]
    public async Task InvalidContractValue_IsRejected(string property, string replacement, LocalReadStatus expected)
    {
        using var root = new TemporaryServerRoot();
        var json = ParseFixture();
        json[property] = property is "schema_version" or "sequence" or "generated_gettime" or "round"
            ? int.Parse(replacement, System.Globalization.CultureInfo.InvariantCulture)
            : replacement;
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), json.ToJsonString());
        using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.IsNull(result.Value);
        Assert.AreEqual(expected, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task ExcessPlayersWeaponsOrDisplayName_AreRejected()
    {
        using var root = new TemporaryServerRoot();
        var players = ParseFixture();
        players["connected_players"] = 5;
        players["observable_players"] = 5;
        players["players_truncated"] = 1;
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), players.ToJsonString());
        using var reader1 = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        Assert.AreEqual(LocalReadStatus.Invalid,
            (await reader1.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus);

        var weapons = ParseFixture();
        weapons["player_1_weapon_count"] = 9;
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), weapons.ToJsonString());
        using var reader2 = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        Assert.AreEqual(LocalReadStatus.Invalid,
            (await reader2.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus);

        var display = ParseFixture();
        display["player_1_display_name"] = new string('A', 65);
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), display.ToJsonString());
        using var reader3 = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        Assert.AreEqual(LocalReadStatus.Invalid,
            (await reader3.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task DuplicateXuidOrClientNumber_IsRejected()
    {
        using var root = new TemporaryServerRoot();
        var duplicateXuid = WithSecondPlayer(duplicateXuid: true, duplicateClient: false);
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), duplicateXuid.ToJsonString());
        using var reader1 = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        Assert.AreEqual(LocalReadStatus.Invalid,
            (await reader1.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus);

        var duplicateClient = WithSecondPlayer(duplicateXuid: false, duplicateClient: true);
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), duplicateClient.ToJsonString());
        using var reader2 = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        Assert.AreEqual(LocalReadStatus.Invalid,
            (await reader2.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task SessionOrMapMismatch_InvalidatesCacheAcrossSessionChange()
    {
        using var root = new TemporaryServerRoot();
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), sessionId: "session-a", mapCode: "zm_tomb");
        using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        var first = await reader.ReadAsync("session-a", "zm_tomb");

        var second = await reader.ReadAsync("session-b", "zm_castle");

        Assert.IsNotNull(first.Value);
        Assert.IsNull(second.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, second.Metadata.ReadStatus);
        Assert.AreNotEqual(DataProvenance.MemoryCache, second.Metadata.Provenance);
    }

    [TestMethod]
    public async Task FailedRefresh_ReturnsSameSessionCacheButNeverFreshFileProvenance()
    {
        using var root = new TemporaryServerRoot();
        var path = root.WriteRuntimeSnapshot(Now.AddSeconds(-2));
        using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        await reader.ReadAsync("session-local-001", "zm_tomb");
        await File.WriteAllTextAsync(path, "{");

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.IsNotNull(result.Value);
        Assert.AreEqual(DataProvenance.MemoryCache, result.Metadata.Provenance);
        Assert.AreNotEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task MissingActiveFile_DoesNotUseTmpOrBak()
    {
        using var root = new TemporaryServerRoot();
        var active = root.Options.ResolvePath(LocalPinteModFile.ControlCenterRuntimeSnapshot);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        await File.WriteAllTextAsync(active + ".tmp", TemporaryServerRoot.RuntimeSnapshotJson());
        await File.WriteAllTextAsync(active + ".bak", TemporaryServerRoot.RuntimeSnapshotJson());
        using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Missing, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task EmptyTruncatedOversizedFutureOrModifiedFile_IsRejected()
    {
        using var root = new TemporaryServerRoot();
        foreach (var contents in new[] { string.Empty, "{", "[]" })
        {
            root.WriteRuntimeSnapshot(Now.AddSeconds(-2), contents);
            using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
            Assert.IsTrue((await reader.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus
                is LocalReadStatus.Empty or LocalReadStatus.Invalid);
        }

        root.Write(LocalPinteModFile.ControlCenterRuntimeSnapshot,
            new string('x', ControlCenterRuntimeSnapshotReader.MaximumFileSizeBytes + 1));
        using var oversized = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        Assert.AreEqual(LocalReadStatus.Invalid,
            (await oversized.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus);

        root.WriteRuntimeSnapshot(Now.AddSeconds(6));
        using var future = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));
        Assert.AreEqual(LocalReadStatus.Invalid,
            (await future.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus);

        root.WriteRuntimeSnapshot(Now.AddSeconds(-2));
        using var modified = new ControlCenterRuntimeSnapshotReader(
            root.Options,
            new FakeClock(Now),
            path => File.AppendAllText(path, " "));
        Assert.AreEqual(LocalReadStatus.Invalid,
            (await modified.ReadAsync("session-local-001", "zm_tomb")).Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task DisplayNameIsNeutralizedBeforeItCanReachPresentation()
    {
        using var root = new TemporaryServerRoot();
        var json = ParseFixture();
        json["player_1_display_name"] = "Joueur 192.168.1.20 C:\\Users\\private";
        root.WriteRuntimeSnapshot(Now.AddSeconds(-2), json.ToJsonString());
        using var reader = new ControlCenterRuntimeSnapshotReader(root.Options, new FakeClock(Now));

        var result = await reader.ReadAsync("session-local-001", "zm_tomb");
        var display = result.Value!.Players.Single().DisplayName;

        Assert.IsFalse(display.Contains("192.168.1.20", StringComparison.Ordinal));
        Assert.IsFalse(display.Contains("C:\\Users", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject ParseFixture() =>
        JsonNode.Parse(TemporaryServerRoot.RuntimeSnapshotJson())!.AsObject();

    private static JsonObject WithSecondPlayer(bool duplicateXuid, bool duplicateClient)
    {
        var json = ParseFixture();
        json["connected_players"] = 2;
        json["observable_players"] = 2;
        var firstProperties = json
            .Where(property => property.Key.StartsWith("player_1_", StringComparison.Ordinal))
            .ToArray();
        foreach (var property in firstProperties)
        {
            json[property.Key.Replace("player_1_", "player_2_", StringComparison.Ordinal)] = property.Value?.DeepClone();
        }

        json["player_2_xuid"] = duplicateXuid ? "0000000000000001" : "0000000000000002";
        json["player_2_client_number"] = duplicateClient ? 0 : 1;
        return json;
    }
}
