using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class LocalPlayerMetadataReaderTests
{
    private const string Xuid = "1111111111111111";

    [TestMethod]
    public async Task ManualLanguage_OverridesAutomaticLanguage_AndRoleUsesXuid()
    {
        using var root = new TemporaryServerRoot();
        root.WriteRoles(Xuid, "moderator");
        root.WriteLanguage(Xuid, "en", manual: false);
        root.WriteLanguage(Xuid, "fr", manual: true);
        using var reader = new LocalPlayerMetadataReader(root.Options, new FakeClock(DateTimeOffset.UtcNow));

        var result = await reader.ReadAsync();
        var player = result.Value!.Players.Single();

        Assert.AreEqual(Xuid, player.Xuid);
        Assert.AreEqual("moderator", player.Role);
        Assert.AreEqual("fr", player.Language);
        Assert.AreEqual(LocalReadStatus.Success, result.Metadata.ReadStatus);
    }

    [TestMethod]
    public async Task InvalidLanguageFile_IsIsolatedFromValidRole()
    {
        using var root = new TemporaryServerRoot();
        root.WriteRoles(Xuid, "helper");
        root.WriteLanguage(Xuid, "fr!", manual: true);
        using var reader = new LocalPlayerMetadataReader(root.Options, new FakeClock(DateTimeOffset.UtcNow));

        var result = await reader.ReadAsync();

        Assert.AreEqual(1, result.Value?.Players.Count);
        Assert.IsNull(result.Value!.Players[0].Language);
        Assert.AreEqual(1, result.Value.FilesSkipped);
    }

    [TestMethod]
    public async Task InvalidRolesWithoutValidLanguage_ReturnsLastValidValueAsStaleCache()
    {
        var now = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        using var root = new TemporaryServerRoot();
        root.WriteRoles(Xuid, "helper");
        using var reader = new LocalPlayerMetadataReader(root.Options, new FakeClock(now));
        var first = await reader.ReadAsync();
        root.WriteBlockA(BlockALocalFile.Roles, "[]");

        var second = await reader.ReadAsync();

        Assert.AreEqual(1, first.Value?.Players.Count);
        Assert.AreEqual(1, second.Value?.Players.Count);
        Assert.AreEqual(Xuid, second.Value?.Players.Single().Xuid);
        Assert.AreEqual(LocalReadStatus.Invalid, second.Metadata.ReadStatus);
        Assert.AreEqual(DataFreshness.Stale, second.Metadata.Freshness);
        Assert.AreEqual(DataProvenance.MemoryCache, second.Metadata.Provenance);
    }

    [TestMethod]
    public async Task ValidJsonWithUnexpectedRootShape_IsInvalidWithoutThrowing()
    {
        using var root = new TemporaryServerRoot();
        root.WriteBlockA(BlockALocalFile.Roles, "[]");
        using var reader = new LocalPlayerMetadataReader(root.Options, new FakeClock(DateTimeOffset.UtcNow));

        var result = await reader.ReadAsync();

        Assert.IsNull(result.Value);
        Assert.AreEqual(LocalReadStatus.Invalid, result.Metadata.ReadStatus);
    }
}
