using System.Text;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Security;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class DpapiRconSecretStoreTests
{
    [TestMethod]
    public async Task SavedSecret_IsDpapiProtectedAndCanBeReadByCurrentUser()
    {
        using var secretFile = new TemporarySecretFile();
        var store = new DpapiRconSecretStore(secretFile.Path);
        const string secret = "TestRcon-42";

        await store.SaveAsync(secret);
        var encrypted = await File.ReadAllBytesAsync(secretFile.Path);
        var restored = await store.ReadAsync();

        Assert.IsTrue(await store.HasSecretAsync());
        Assert.AreEqual(secret, restored);
        Assert.IsFalse(Encoding.UTF8.GetString(encrypted).Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(File.Exists(secretFile.Path + ".tmp"));
    }

    [TestMethod]
    public async Task SecretWithWhitespace_IsRejectedAndNeverWritten()
    {
        using var secretFile = new TemporarySecretFile();
        var store = new DpapiRconSecretStore(secretFile.Path);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.SaveAsync("secret invalide"));

        Assert.IsFalse(File.Exists(secretFile.Path));
    }

    [TestMethod]
    public async Task CorruptedPayload_ReturnsNoSecret()
    {
        using var secretFile = new TemporarySecretFile();
        Directory.CreateDirectory(Path.GetDirectoryName(secretFile.Path)!);
        await File.WriteAllTextAsync(secretFile.Path, "not-dpapi");

        var restored = await new DpapiRconSecretStore(secretFile.Path).ReadAsync();

        Assert.IsNull(restored);
    }

    private sealed class TemporarySecretFile : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PinteMod.ControlCenter.SecretTests",
            Guid.NewGuid().ToString("N"));

        public string Path => System.IO.Path.Combine(_root, "rcon.secret.dpapi");

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
