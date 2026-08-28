using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Services;

internal sealed class RemoteAgentConfigurationStore
{
    private const int MaximumBytes = 128 * 1024;
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("PinteMod.ControlCenter.RemoteAgentHost.v1");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal static string GetAgentHome() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PinteMod",
        "ControlCenter",
        "remote-agent");

    internal static string GetConfigurationPath() => Path.Combine(GetAgentHome(), "agent-config.json");
    internal static string GetExecutablePath() => Path.Combine(GetAgentHome(), "PinteMod.ControlCenter.exe");
    internal static string GetPendingUpdatePath() => Path.Combine(GetAgentHome(), "PinteMod.ControlCenter.pending.exe");
    internal static string GetLogPath() => Path.Combine(GetAgentHome(), "agent.log");
    internal static string GetStopRequestPath() => Path.Combine(GetAgentHome(), "stop.request");
    internal static string GetUpdateInProgressPath() => Path.Combine(GetAgentHome(), "update-in-progress.marker");

    public async Task<RemoteAgentConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = GetConfigurationPath();
        try
        {
            if (!File.Exists(path)) return new RemoteAgentConfiguration(RemoteAgentProtocol.SchemaVersion, []);
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > MaximumBytes) return new RemoteAgentConfiguration(RemoteAgentProtocol.SchemaVersion, []);
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<RemoteAgentConfiguration>(stream, JsonOptions, cancellationToken)
                       .ConfigureAwait(false)
                   ?? new RemoteAgentConfiguration(RemoteAgentProtocol.SchemaVersion, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new RemoteAgentConfiguration(RemoteAgentProtocol.SchemaVersion, []);
        }
    }

    public async Task SaveAsync(RemoteAgentConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Directory.CreateDirectory(GetAgentHome());
        var path = GetConfigurationPath();
        var temp = path + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temp,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static string ProtectSecret(byte[] secret)
    {
        if (secret is null || secret.Length != 32) throw new ArgumentException("Secret Agent invalide.", nameof(secret));
        var protectedBytes = ProtectedData.Protect(secret, OptionalEntropy, DataProtectionScope.CurrentUser);
        try
        {
            return Convert.ToBase64String(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public static byte[]? UnprotectSecret(string? protectedSecretBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedSecretBase64)) return null;
        byte[]? protectedBytes = null;
        try
        {
            protectedBytes = Convert.FromBase64String(protectedSecretBase64);
            if (protectedBytes.Length > 4096) return null;
            var plain = ProtectedData.Unprotect(protectedBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
            if (plain.Length != 32)
            {
                CryptographicOperations.ZeroMemory(plain);
                return null;
            }
            return plain;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return null;
        }
        finally
        {
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }
}
