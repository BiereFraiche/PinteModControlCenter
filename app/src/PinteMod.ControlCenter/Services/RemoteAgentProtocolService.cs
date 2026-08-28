using System.IO;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Services;

internal static class RemoteAgentProtocolService
{
    internal const int MaximumJsonBytes = 32 * 1024;
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal static string GetAgentRoot(string serverRoot) =>
        Path.Combine(serverRoot, RemoteAgentProtocol.QueueFolderName, RemoteAgentProtocol.AgentFolderName);

    internal static string GetPairingPath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.PairingFileName);

    internal static string GetStatusPath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.StatusFileName);

    internal static string GetRequestsPath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.RequestsFolderName);

    internal static string GetResponsesPath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.ResponsesFolderName);

    internal static string GetUpdatesPath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.UpdatesFolderName);

    internal static string GetUpdateManifestPath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.UpdateManifestFileName);

    internal static string GetAvailablePackageManifestPath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.AvailablePackageManifestFileName);

    internal static string GetProfileCatalogPath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.ProfileCatalogFileName);

    internal static string GetServerRuntimePath(string serverRoot) =>
        Path.Combine(GetAgentRoot(serverRoot), RemoteAgentProtocol.ServerRuntimeFileName);

    internal static void EnsureQueueDirectories(string serverRoot)
    {
        var root = GetAgentRoot(serverRoot);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(GetRequestsPath(serverRoot));
        Directory.CreateDirectory(GetResponsesPath(serverRoot));
        Directory.CreateDirectory(GetUpdatesPath(serverRoot));
        var ignore = Path.Combine(root, ".gitignore");
        if (!File.Exists(ignore))
        {
            File.WriteAllText(ignore, "*" + Environment.NewLine + "!.gitignore" + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    internal static string SignRequest(RemoteLaunchRequest request, byte[] secret) =>
        ComputeHmac(secret, CanonicalRequest(request));

    internal static string SignResponse(RemoteLaunchResponse response, byte[] secret) =>
        ComputeHmac(secret, CanonicalResponse(response));

    internal static string SignStatus(RemoteAgentStatusEnvelope status, byte[] secret) =>
        ComputeHmac(secret, CanonicalStatus(status));

    internal static string SignUpdate(RemoteAgentUpdateEnvelope update, byte[] secret) =>
        ComputeHmac(secret, CanonicalUpdate(update));

    internal static string SignAvailablePackage(RemoteAgentAvailablePackageEnvelope package, byte[] secret) =>
        ComputeHmac(secret, CanonicalAvailablePackage(package));

    internal static string SignProfileCatalog(RemoteAgentProfileCatalogEnvelope catalog, byte[] secret) =>
        ComputeHmac(secret, CanonicalProfileCatalog(catalog));

    internal static string SignServerRuntime(RemoteAgentServerRuntimeEnvelope runtime, byte[] secret) =>
        ComputeHmac(secret, CanonicalServerRuntime(runtime));

    internal static bool VerifyRequest(RemoteLaunchRequest request, byte[] secret) =>
        VerifyHmac(request.Signature, ComputeHmac(secret, CanonicalRequest(request)));

    internal static bool VerifyResponse(RemoteLaunchResponse response, byte[] secret) =>
        VerifyHmac(response.Signature, ComputeHmac(secret, CanonicalResponse(response)));

    internal static bool VerifyStatus(RemoteAgentStatusEnvelope status, byte[] secret) =>
        VerifyHmac(status.Signature, ComputeHmac(secret, CanonicalStatus(status)));

    internal static bool VerifyUpdate(RemoteAgentUpdateEnvelope update, byte[] secret) =>
        VerifyHmac(update.Signature, ComputeHmac(secret, CanonicalUpdate(update)));

    internal static bool VerifyAvailablePackage(RemoteAgentAvailablePackageEnvelope package, byte[] secret) =>
        VerifyHmac(package.Signature, ComputeHmac(secret, CanonicalAvailablePackage(package)));

    internal static bool VerifyProfileCatalog(RemoteAgentProfileCatalogEnvelope catalog, byte[] secret) =>
        VerifyHmac(catalog.Signature, ComputeHmac(secret, CanonicalProfileCatalog(catalog)));

    internal static bool VerifyServerRuntime(RemoteAgentServerRuntimeEnvelope runtime, byte[] secret) =>
        VerifyHmac(runtime.Signature, ComputeHmac(secret, CanonicalServerRuntime(runtime)));

    internal static async Task WriteJsonAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "." + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(
                             temp,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    internal static async Task<T?> ReadJsonBoundedAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path)) return default;
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > MaximumJsonBytes) return default;
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return default;
        }
    }

    internal static string GetAgentVersion()
    {
        var assembly = typeof(RemoteAgentProtocolService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }

    private static string CanonicalRequest(RemoteLaunchRequest request) => string.Join("\n",
        request.SchemaVersion.ToString(CultureInfo.InvariantCulture),
        request.RequestId,
        request.AgentId,
        request.Action,
        request.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        request.ExpiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        request.Nonce);

    private static string CanonicalResponse(RemoteLaunchResponse response) => string.Join("\n",
        response.SchemaVersion.ToString(CultureInfo.InvariantCulture),
        response.RequestId,
        response.AgentId,
        response.Status,
        response.ResultCode,
        response.Message,
        response.CompletedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        response.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private static string CanonicalStatus(RemoteAgentStatusEnvelope status) => string.Join("\n",
        status.SchemaVersion.ToString(CultureInfo.InvariantCulture),
        status.AgentId,
        status.DisplayName,
        status.MachineName,
        status.State,
        status.UpdatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        status.AgentVersion);

    private static string CanonicalUpdate(RemoteAgentUpdateEnvelope update) => string.Join("\n",
        update.SchemaVersion.ToString(CultureInfo.InvariantCulture),
        update.AgentId,
        update.TargetVersion,
        update.PackageFileName,
        update.Sha256,
        update.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        update.ExpiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string CanonicalAvailablePackage(RemoteAgentAvailablePackageEnvelope package) => string.Join("\n",
        package.SchemaVersion.ToString(CultureInfo.InvariantCulture),
        package.AgentId,
        package.Version,
        package.PackageFileName,
        package.Sha256,
        package.UpdatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));


    private static string CanonicalProfileCatalog(RemoteAgentProfileCatalogEnvelope catalog)
    {
        var lines = new List<string>
        {
            catalog.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            catalog.AuthorityAgentId,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(catalog.MachineName ?? string.Empty)),
            catalog.UpdatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            catalog.Profiles.Count.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var profile in catalog.Profiles)
        {
            lines.Add(string.Join("\t",
                profile.AgentId,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(profile.DisplayName ?? string.Empty)),
                Convert.ToBase64String(Encoding.UTF8.GetBytes(profile.RootFolderName ?? string.Empty)),
                Convert.ToBase64String(Encoding.UTF8.GetBytes(profile.LauncherRelativePath ?? string.Empty)),
                profile.ServerPort.ToString(CultureInfo.InvariantCulture),
                profile.PinteModDetected ? "1" : "0"));
        }
        return string.Join("\n", lines);
    }

    private static string CanonicalServerRuntime(RemoteAgentServerRuntimeEnvelope runtime) => string.Join("\n",
        runtime.SchemaVersion.ToString(CultureInfo.InvariantCulture),
        runtime.AgentId,
        runtime.UpdatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        runtime.ServerRunning ? "1" : "0");

    private static string ComputeHmac(byte[] secret, string canonical)
    {
        using var hmac = new HMACSHA256(secret);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool VerifyHmac(string? supplied, string expected)
    {
        if (string.IsNullOrWhiteSpace(supplied) || supplied.Length != expected.Length) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(supplied),
                Convert.FromHexString(expected));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
