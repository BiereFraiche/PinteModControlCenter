using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class BoiiiRconBootstrapServiceTests
{
    [TestMethod]
    public async Task InitializeAsync_VirginServer_WritesDeclaredConfigOnlyOnce()
    {
        using var directory = new TemporaryServerDirectory();
        directory.Create("set ServerFilename=server_zm.cfg\r\n");

        var service = new BoiiiRconBootstrapService();
        var result = await service.InitializeAsync(directory.Root, "SafeRcon-2026");

        Assert.IsTrue(result.Success, result.Message);
        var config = await File.ReadAllTextAsync(directory.ConfigPath);
        StringAssert.Contains(config, "set rcon_password \"SafeRcon-2026\"");

        var second = await service.InitializeAsync(directory.Root, "OtherRcon-2026");
        Assert.IsFalse(second.Success);
        config = await File.ReadAllTextAsync(directory.ConfigPath);
        Assert.IsFalse(config.Contains("OtherRcon-2026", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task InitializeAsync_ExistingRcon_RefusesToOverwrite()
    {
        using var directory = new TemporaryServerDirectory();
        directory.Create("set ServerFilename=server_zm.cfg\r\n", "set rcon_password \"AlreadySet-2026\"\r\n");

        var result = await new BoiiiRconBootstrapService().InitializeAsync(directory.Root, "NewRcon-2026");

        Assert.IsFalse(result.Success);
        var config = await File.ReadAllTextAsync(directory.ConfigPath);
        StringAssert.Contains(config, "AlreadySet-2026");
        Assert.IsFalse(config.Contains("NewRcon-2026", StringComparison.Ordinal));
    }

    private sealed class TemporaryServerDirectory : IDisposable
    {
        public TemporaryServerDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.RconBootstrap", Guid.NewGuid().ToString("N"));
            ConfigPath = Path.Combine(Root, "zone", "server_zm.cfg");
        }

        public string Root { get; }

        public string ConfigPath { get; }

        public void Create(string launcher, string config = "set sv_hostname \"Test\"\r\n")
        {
            Directory.CreateDirectory(Path.Combine(Root, "boiii"));
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(Path.Combine(Root, "Server.bat"), launcher);
            File.WriteAllText(ConfigPath, config);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
