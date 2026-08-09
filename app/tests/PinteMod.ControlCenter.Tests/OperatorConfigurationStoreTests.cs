using System.Text;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class OperatorConfigurationStoreTests
{
    [TestMethod]
    public async Task MissingConfiguration_ReturnsSafeSimulationDefault()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var store = new JsonOperatorConfigurationStore(directory.ConfigurationPath);

        var result = await store.LoadAsync();

        Assert.AreEqual(OperatorConfiguration.Default, result);
        Assert.IsFalse(result.ActivateDataSourceOnStartup);
    }

    [TestMethod]
    public async Task SaveAndLoad_PersistsOnlyNonSensitiveOperatorValues()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var store = new JsonOperatorConfigurationStore(directory.ConfigurationPath);
        var expected = new OperatorConfiguration(
            1,
            OperatorDataLocation.Lan,
            "\\\\serveur\\partage\\UnrankedServer",
            true,
            "192.168.1.20",
            27017);

        await store.SaveAsync(expected);
        var result = await store.LoadAsync();
        var raw = await File.ReadAllTextAsync(directory.ConfigurationPath, Encoding.UTF8);

        Assert.AreEqual(expected, result);
        Assert.IsFalse(raw.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(raw.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(File.Exists(directory.ConfigurationPath + ".tmp"));
    }

    [TestMethod]
    public async Task InvalidConfiguration_ReturnsDefaultWithoutThrowing()
    {
        using var directory = new TemporaryConfigurationDirectory();
        Directory.CreateDirectory(Path.GetDirectoryName(directory.ConfigurationPath)!);
        await File.WriteAllTextAsync(directory.ConfigurationPath, "{");

        var result = await new JsonOperatorConfigurationStore(directory.ConfigurationPath).LoadAsync();

        Assert.AreEqual(OperatorConfiguration.Default, result);
    }

    [TestMethod]
    public async Task PublicRconTarget_CannotBeSaved()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var store = new JsonOperatorConfigurationStore(directory.ConfigurationPath);
        var configuration = OperatorConfiguration.Default with { RconAddress = "8.8.8.8" };

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.SaveAsync(configuration));
        Assert.IsFalse(File.Exists(directory.ConfigurationPath));
    }

    [TestMethod]
    public async Task PublicRconTargetAlreadyOnDisk_IsResetToLoopback()
    {
        using var directory = new TemporaryConfigurationDirectory();
        Directory.CreateDirectory(Path.GetDirectoryName(directory.ConfigurationPath)!);
        await File.WriteAllTextAsync(
            directory.ConfigurationPath,
            """
            {
              "schema_version": 1,
              "data_location": 0,
              "server_root": "",
              "activate_data_source_on_startup": false,
              "rcon_address": "8.8.8.8",
              "rcon_port": 27018
            }
            """);

        var result = await new JsonOperatorConfigurationStore(directory.ConfigurationPath).LoadAsync();

        Assert.AreEqual("127.0.0.1", result.RconAddress);
        Assert.AreEqual(OperatorConfiguration.Default.RconPort, result.RconPort);
    }

    private sealed class TemporaryConfigurationDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "PinteMod.ControlCenter.OperatorTests",
            Guid.NewGuid().ToString("N"));

        public string ConfigurationPath => Path.Combine(_root, "operator-settings.json");

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
