using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ManagedServerProfileStoreTests
{
    [TestMethod]
    public async Task SaveAndLoad_PreservesRelativeLauncherOnly()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonManagedServerProfileStore(directory.Path);
        var expected = new ManagedServerProfileConfiguration(1, "Launch_PinteMod_Server.bat");

        await store.SaveAsync(expected);
        var result = await store.LoadAsync();

        Assert.AreEqual(expected, result);
        Assert.IsFalse(File.Exists(directory.Path + ".tmp"));
    }

    [TestMethod]
    public async Task Save_RejectsTraversalAndAbsoluteLauncher()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonManagedServerProfileStore(directory.Path);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            store.SaveAsync(new ManagedServerProfileConfiguration(1, "..\\outside.bat")));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            store.SaveAsync(new ManagedServerProfileConfiguration(1, "C:\\outside.bat")));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PinteMod.ControlCenter.ManagedProfileTests",
            Guid.NewGuid().ToString("N"));

        public string Path => System.IO.Path.Combine(_root, "managed-server.json");

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
