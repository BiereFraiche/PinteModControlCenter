using System.IO;
using System.Text.Json;
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

    [TestMethod]
    public async Task ReplaceAsync_ExistingRcon_ReplacesDirectiveWithoutReturningThePreviousSecret()
    {
        using var directory = new TemporaryServerDirectory();
        directory.Create("set ServerFilename=server_zm.cfg\r\n", "set rcon_password \"AlreadySet-2026\"\r\n");

        var result = await new BoiiiRconBootstrapService().ReplaceAsync(directory.Root, "NewRcon-2026");

        Assert.IsTrue(result.Success, result.Message);
        Assert.IsFalse(result.Message.Contains("AlreadySet-2026", StringComparison.Ordinal));
        var config = await File.ReadAllTextAsync(directory.ConfigPath);
        StringAssert.Contains(config, "set rcon_password \"NewRcon-2026\"");
        Assert.IsFalse(config.Contains("AlreadySet-2026", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReplaceAsync_PinteModServer_ReplacesPrivateDirectiveAndSynchronizesBridge()
    {
        using var directory = new TemporaryServerDirectory();
        directory.Create("set ServerFilename=server_zm.cfg\r\n");
        directory.AddPinteModFiles();
        var service = new BoiiiRconBootstrapService(async (path, secret, cancellationToken) =>
        {
            await File.WriteAllTextAsync(path, "protected-" + secret, cancellationToken);
            return true;
        });
        Assert.IsTrue((await service.InitializeAsync(directory.Root, "OldRcon-2026")).Success);

        var result = await service.ReplaceAsync(directory.Root, "NewRcon-2026");

        Assert.IsTrue(result.Success, result.Message);
        var privateConfig = await File.ReadAllTextAsync(Path.Combine(directory.Root, "zone", "pintemod_server_secrets.cfg"));
        StringAssert.Contains(privateConfig, "set rcon_password \"NewRcon-2026\"");
        Assert.IsFalse(privateConfig.Contains("OldRcon-2026", StringComparison.Ordinal));
        var bridgeSecret = await File.ReadAllTextAsync(Path.Combine(directory.Root, "boiii", "tools", "PinteMod_GeoIP_Bridge.secret.txt"));
        Assert.AreEqual("protected-NewRcon-2026", bridgeSecret);
    }

    [TestMethod]
    public async Task HasConfiguredRconAsync_ReturnsOnlyDirectivePresence()
    {
        using var directory = new TemporaryServerDirectory();
        directory.Create("set ServerFilename=server_zm.cfg\r\n", "set rcon_password \"AlreadySet-2026\"\r\n");

        var configured = await new BoiiiRconBootstrapService().HasConfiguredRconAsync(directory.Root);

        Assert.IsTrue(configured);
    }

    [TestMethod]
    public async Task InitializeAsync_PinteModServer_CreatesNativeLocalSetup()
    {
        using var directory = new TemporaryServerDirectory();
        directory.Create("set ServerFilename=server_zm.cfg\r\nset GamePort=27019\r\n");
        directory.AddPinteModFiles();

        var service = new BoiiiRconBootstrapService(async (path, _, cancellationToken) =>
        {
            await File.WriteAllTextAsync(path, "test-dpapi", cancellationToken);
            return true;
        });
        var result = await service.InitializeAsync(directory.Root, "SafeRcon-2026");

        Assert.IsTrue(result.Success, result.Message);
        var serverConfig = await File.ReadAllTextAsync(directory.ConfigPath);
        StringAssert.Contains(serverConfig, "exec \"pintemod_server_secrets.cfg\"");
        var privateConfig = Path.Combine(directory.Root, "zone", "pintemod_server_secrets.cfg");
        Assert.IsTrue(File.Exists(privateConfig));
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root, "boiii", "tools", "PinteMod_GeoIP_Bridge.secret.txt")));
        var bridgeJson = await File.ReadAllTextAsync(Path.Combine(directory.Root, "boiii", "tools", "PinteMod_GeoIP_Bridge.local.json"));
        using var bridge = JsonDocument.Parse(bridgeJson);
        Assert.AreEqual(27019, bridge.RootElement.GetProperty("server_port").GetInt32());
    }

    [TestMethod]
    public async Task InitializeAsync_PinteModServer_WhenWindowsCannotProtectSecret_LeavesNoSetupBehind()
    {
        using var directory = new TemporaryServerDirectory();
        directory.Create("set ServerFilename=server_zm.cfg\r\n");
        directory.AddPinteModFiles();
        var initialConfig = await File.ReadAllTextAsync(directory.ConfigPath);
        var service = new BoiiiRconBootstrapService((_, _, _) => Task.FromResult(false));

        var result = await service.InitializeAsync(directory.Root, "SafeRcon-2026");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(initialConfig, await File.ReadAllTextAsync(directory.ConfigPath));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "zone", "pintemod_server_secrets.cfg")));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "boiii", "tools", "PinteMod_GeoIP_Bridge.secret.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "boiii", "tools", "PinteMod_GeoIP_Bridge.local.json")));
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

        public void AddPinteModFiles()
        {
            var tools = Path.Combine(Root, "boiii", "tools");
            Directory.CreateDirectory(tools);
            File.WriteAllText(Path.Combine(tools, "PinteMod_Server_Launcher.ps1"), "# test");
            File.WriteAllText(
                Path.Combine(tools, "PinteMod_GeoIP_Bridge.example.json"),
                "{\"server_address\":\"127.0.0.1\",\"server_port\":27017}");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
