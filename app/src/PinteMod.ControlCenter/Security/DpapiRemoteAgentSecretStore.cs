using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PinteMod.ControlCenter.Security;

public sealed class DpapiRemoteAgentSecretStore
{
    private const int MaximumEncryptedBytes = 16 * 1024;
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("PinteMod.ControlCenter.RemoteAgent.v1");
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DpapiRemoteAgentSecretStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public Task<bool> HasSecretAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(File.Exists(_path) && new FileInfo(_path).Length is > 0 and <= MaximumEncryptedBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    public async Task SaveAsync(byte[] secret, CancellationToken cancellationToken = default)
    {
        if (secret is null || secret.Length != 32)
        {
            throw new ArgumentException("Clé Agent distante invalide.", nameof(secret));
        }

        byte[]? protectedBytes = null;
        var temp = _path + ".tmp";
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            protectedBytes = ProtectedData.Protect(secret, OptionalEntropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllBytesAsync(temp, protectedBytes, cancellationToken).ConfigureAwait(false);
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
            if (File.Exists(temp)) File.Delete(temp);
            _gate.Release();
        }
    }

    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        byte[]? protectedBytes = null;
        try
        {
            if (!File.Exists(_path)) return null;
            var info = new FileInfo(_path);
            if (info.Length is <= 0 or > MaximumEncryptedBytes) return null;
            protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
            var plain = ProtectedData.Unprotect(protectedBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
            if (plain.Length != 32)
            {
                CryptographicOperations.ZeroMemory(plain);
                return null;
            }
            return plain;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return null;
        }
        finally
        {
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
            _gate.Release();
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
        return Task.CompletedTask;
    }
}
