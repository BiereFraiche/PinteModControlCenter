using System.Text;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Security;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class DpapiRconSecretStoreTests
{
    [TestMethod]
    public void SavedSecret_IsDpapiProtectedAndCanBeReadByCurrentUser()
    {
        using var secretFile = new TemporarySecretFile();
        var store = new DpapiRconSecretStore(secretFile.Path, new ReversibleTestProtector());
        const string secret = "TestRcon-42";

        store.SaveAsync(secret).GetAwaiter().GetResult();
        var encrypted = File.ReadAllBytes(secretFile.Path);
        var restored = store.ReadAsync().GetAwaiter().GetResult();

        Assert.IsTrue(store.HasSecretAsync().GetAwaiter().GetResult());
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

    private sealed class ReversibleTestProtector : ICurrentUserDataProtector
    {
        public byte[] Protect(byte[] plainBytes) => plainBytes.Select(value => (byte)(value ^ 0xA5)).ToArray();

        public byte[] Unprotect(byte[] protectedBytes) => protectedBytes.Select(value => (byte)(value ^ 0xA5)).ToArray();
    }
}
