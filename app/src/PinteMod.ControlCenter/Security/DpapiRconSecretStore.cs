using System.IO;
using System.Security.Cryptography;
using System.Text;
using PinteMod.ControlCenter.Core.Contracts;

namespace PinteMod.ControlCenter.Security;

public sealed class DpapiRconSecretStore : IRconSecretStore
{
    private const int MaximumEncryptedBytes = 16 * 1024;
    private readonly string _secretPath;
    private readonly ICurrentUserDataProtector _protector;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DpapiRconSecretStore(string? secretPath = null)
        : this(secretPath, new CurrentUserDpapiProtector())
    {
    }

    internal DpapiRconSecretStore(string? secretPath, ICurrentUserDataProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        _secretPath = secretPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PinteMod",
            "ControlCenter",
            "rcon.secret.dpapi");
        _protector = protector;
    }

    public Task<bool> HasSecretAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(File.Exists(_secretPath) && new FileInfo(_secretPath).Length is > 0 and <= MaximumEncryptedBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    public async Task SaveAsync(string secret, CancellationToken cancellationToken = default)
    {
        ValidateSecret(secret);
        var plainBytes = Encoding.UTF8.GetBytes(secret);
        byte[]? protectedBytes = null;
        var temporaryPath = _secretPath + ".tmp";

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            protectedBytes = _protector.Protect(plainBytes);
            Directory.CreateDirectory(Path.GetDirectoryName(_secretPath)!);
            await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _secretPath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }

    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        byte[]? protectedBytes = null;
        byte[]? plainBytes = null;
        try
        {
            if (!File.Exists(_secretPath))
            {
                return null;
            }

            var info = new FileInfo(_secretPath);
            if (info.Length is <= 0 or > MaximumEncryptedBytes)
            {
                return null;
            }

            protectedBytes = await File.ReadAllBytesAsync(_secretPath, cancellationToken).ConfigureAwait(false);
            plainBytes = _protector.Unprotect(protectedBytes);
            var secret = Encoding.UTF8.GetString(plainBytes);
            ValidateSecret(secret);
            return secret;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            return null;
        }
        finally
        {
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }

            _gate.Release();
        }
    }

    private static void ValidateSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 128 ||
            secret.Any(character => char.IsWhiteSpace(character) || character == '"'))
        {
            throw new ArgumentException("Le secret RCON ne doit contenir ni espace, ni guillemet, ni retour à la ligne.", nameof(secret));
        }
    }
}

internal interface ICurrentUserDataProtector
{
    byte[] Protect(byte[] plainBytes);

    byte[] Unprotect(byte[] protectedBytes);
}

internal sealed class CurrentUserDpapiProtector : ICurrentUserDataProtector
{
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("PinteMod.ControlCenter.RCON.v1");

    public byte[] Protect(byte[] plainBytes) =>
        ProtectedData.Protect(plainBytes, OptionalEntropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedBytes) =>
        ProtectedData.Unprotect(protectedBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
}
