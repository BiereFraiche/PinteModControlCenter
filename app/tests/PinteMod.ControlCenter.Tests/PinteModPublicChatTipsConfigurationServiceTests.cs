using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PinteModPublicChatTipsConfigurationServiceTests
{
    [TestMethod]
    public async Task SaveAsync_WritesOnlyDedicatedManagedBlockAndReloadsMessages()
    {
        using var directory = new TemporaryServerDirectory();
        await directory.CreateFromCurrentPayloadAsync();
        var service = new PinteModPublicChatTipsConfigurationService();
        var expected = new PublicChatTipsConfiguration(
            true,
            90,
            180,
            180,
            ["Un joueur troll ? Lancez un votekick depuis .menu.", "Merci de respecter les autres joueurs."]);

        var save = await service.SaveAsync(directory.Root, expected);
        var load = await service.LoadAsync(directory.Root);

        Assert.IsTrue(save.Success, save.Message);
        Assert.IsTrue(load.Supported, load.Message);
        Assert.AreEqual(90, load.Configuration.FirstDelaySeconds);
        Assert.AreEqual(180, load.Configuration.MinimumDelaySeconds);
        CollectionAssert.AreEqual(expected.Messages.ToArray(), load.Configuration.Messages.ToArray());
        var config = await File.ReadAllTextAsync(directory.ConfigPath);
        StringAssert.Contains(config, "// BEGIN PINTEMOD CONTROL CENTER PUBLIC TIPS");
        StringAssert.Contains(config, "// END PINTEMOD CONTROL CENTER PUBLIC TIPS");
        StringAssert.Contains(config, "level.ezz_admin_version = \"2.1.1\";");
        Assert.IsTrue(
            config.IndexOf("// BEGIN PINTEMOD CONTROL CENTER PUBLIC TIPS", StringComparison.Ordinal) <
            config.IndexOf("level.pintemod_vote_duration", StringComparison.Ordinal));
        Assert.IsTrue(config.TrimEnd().EndsWith("}", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SaveAsync_UnknownCommunityModule_RefusesWithoutChangingConfiguration()
    {
        using var directory = new TemporaryServerDirectory();
        await directory.CreateFromCurrentPayloadAsync();
        await File.WriteAllTextAsync(directory.CommunityPath, "operator-owned community module");
        var before = await File.ReadAllTextAsync(directory.ConfigPath);

        var result = await new PinteModPublicChatTipsConfigurationService().SaveAsync(
            directory.Root,
            PublicChatTipsConfiguration.Default);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(before, await File.ReadAllTextAsync(directory.ConfigPath));
    }

    [TestMethod]
    public async Task SaveAsync_InvalidSchedule_LeavesConfigurationUntouched()
    {
        using var directory = new TemporaryServerDirectory();
        await directory.CreateFromCurrentPayloadAsync();
        var before = await File.ReadAllTextAsync(directory.ConfigPath);

        var result = await new PinteModPublicChatTipsConfigurationService().SaveAsync(
            directory.Root,
            PublicChatTipsConfiguration.Default with { MinimumDelaySeconds = 59 });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(before, await File.ReadAllTextAsync(directory.ConfigPath));
    }

    private sealed class TemporaryServerDirectory : IDisposable
    {
        public TemporaryServerDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.PublicTips", Guid.NewGuid().ToString("N"));
            ConfigPath = Path.Combine(Root, "boiii", "custom_scripts", "ezz_admin_config.gsc");
            CommunityPath = Path.Combine(Root, "boiii", "custom_scripts", "ezz_admin_community.gsc");
        }

        public string Root { get; }

        public string ConfigPath { get; }

        public string CommunityPath { get; }

        public async Task CreateFromCurrentPayloadAsync()
        {
            var payload = new EmbeddedServerPayloadService();
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            await File.WriteAllBytesAsync(ConfigPath, await payload.ReadPinteModPayloadFileAsync("boiii/custom_scripts/ezz_admin_config.gsc"));
            await File.WriteAllBytesAsync(CommunityPath, await payload.ReadPinteModPayloadFileAsync("boiii/custom_scripts/ezz_admin_community.gsc"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
