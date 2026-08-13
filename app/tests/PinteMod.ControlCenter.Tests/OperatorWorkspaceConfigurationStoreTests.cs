using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class OperatorWorkspaceConfigurationStoreTests
{
    [TestMethod]
    public async Task MissingWorkspace_ReturnsSinglePrimaryProfile()
    {
        using var directory = new TemporaryWorkspaceDirectory();
        var result = await new JsonOperatorWorkspaceConfigurationStore(directory.ConfigurationPath).LoadAsync();

        CollectionAssert.AreEqual(
            new[] { OperatorWorkspaceConfiguration.PrimaryProfileId },
            result.ProfileIds.ToArray());
        Assert.AreEqual(OperatorWorkspaceConfiguration.PrimaryProfileId, result.ActiveProfileId);
    }

    [TestMethod]
    public async Task SaveAndLoad_PreservesProfileOrderAndActiveProfile()
    {
        using var directory = new TemporaryWorkspaceDirectory();
        var store = new JsonOperatorWorkspaceConfigurationStore(directory.ConfigurationPath);
        var expected = new OperatorWorkspaceConfiguration(
            OperatorWorkspaceConfiguration.CurrentSchemaVersion,
            ["primary", "srv-second", "srv-third"],
            "srv-second");

        await store.SaveAsync(expected);
        var result = await store.LoadAsync();

        CollectionAssert.AreEqual(expected.ProfileIds.ToArray(), result.ProfileIds.ToArray());
        Assert.AreEqual(expected.ActiveProfileId, result.ActiveProfileId);
        Assert.IsFalse(File.Exists(directory.ConfigurationPath + ".tmp"));
    }

    [TestMethod]
    public async Task InvalidOrDuplicateProfileIds_AreRejectedBeforeWrite()
    {
        using var directory = new TemporaryWorkspaceDirectory();
        var store = new JsonOperatorWorkspaceConfigurationStore(directory.ConfigurationPath);
        var invalid = new OperatorWorkspaceConfiguration(
            1,
            ["primary", "../outside", "primary"],
            "primary");

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.SaveAsync(invalid));
        Assert.IsFalse(File.Exists(directory.ConfigurationPath));
    }

    [TestMethod]
    public void ProfileStoragePaths_AreIsolatedAndRejectTraversal()
    {
        var primary = OperatorProfileStoragePaths.GetConfigurationPath("primary");
        var second = OperatorProfileStoragePaths.GetConfigurationPath("srv-second");
        var secondSecret = OperatorProfileStoragePaths.GetRconSecretPath("srv-second");

        Assert.AreNotEqual(primary, second);
        Assert.AreEqual(Path.GetDirectoryName(second), Path.GetDirectoryName(secondSecret));
        StringAssert.Contains(second, Path.Combine("profiles", "srv-second"));
        Assert.ThrowsException<ArgumentException>(() =>
            OperatorProfileStoragePaths.GetConfigurationPath("../outside"));
    }

    private sealed class TemporaryWorkspaceDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "PinteMod.ControlCenter.WorkspaceTests",
            Guid.NewGuid().ToString("N"));

        public string ConfigurationPath => Path.Combine(_root, "server-workspace.json");

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
