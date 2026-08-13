using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class JsonOperatorConfigurationStore : IOperatorConfigurationStore
{
    private const int MaximumConfigurationBytes = 32 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly string _configurationPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonOperatorConfigurationStore(string? configurationPath = null)
    {
        _configurationPath = configurationPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PinteMod",
            "ControlCenter",
            "operator-settings.json");
    }

    public async Task<OperatorConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_configurationPath))
            {
                return OperatorConfiguration.Default;
            }

            var info = new FileInfo(_configurationPath);
            if (info.Length is <= 0 or > MaximumConfigurationBytes)
            {
                return OperatorConfiguration.Default;
            }

            await using var stream = new FileStream(
                _configurationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var configuration = await JsonSerializer.DeserializeAsync<OperatorConfiguration>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            return NormalizeOrDefault(configuration);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return OperatorConfiguration.Default;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        OperatorConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var normalized = NormalizeForSave(configuration);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = _configurationPath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configurationPath)!);
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    normalized,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _configurationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }

    private static OperatorConfiguration NormalizeOrDefault(OperatorConfiguration? configuration)
    {
        if (configuration is null ||
            configuration.SchemaVersion != OperatorConfiguration.CurrentSchemaVersion ||
            configuration.ServerRoot is null ||
            configuration.RconAddress is null ||
            configuration.ProfileDisplayName is null ||
            configuration.ServerRoot.Length > 2048 ||
            configuration.RconAddress.Length > 64 ||
            !OperatorConfiguration.IsValidProfileDisplayName(configuration.ProfileDisplayName) ||
            configuration.RconPort is < 1 or > 65535)
        {
            return OperatorConfiguration.Default;
        }

        var normalized = configuration with
        {
            ServerRoot = configuration.ServerRoot.Trim(),
            RconAddress = configuration.RconAddress.Trim(),
            ProfileDisplayName = NormalizeProfileDisplayName(configuration.ProfileDisplayName)
        };
        return RconEndpointValidator.IsAllowed(new RconEndpoint(
            normalized.RconAddress,
            normalized.RconPort,
            TimeSpan.FromSeconds(3)))
            ? normalized
            : normalized with
            {
                RconAddress = OperatorConfiguration.Default.RconAddress,
                RconPort = OperatorConfiguration.Default.RconPort
            };
    }

    private static OperatorConfiguration NormalizeForSave(OperatorConfiguration configuration)
    {
        if (configuration.ServerRoot.Length > 2048)
        {
            throw new ArgumentException("Le chemin de données est trop long.", nameof(configuration));
        }

        if (!OperatorConfiguration.IsValidProfileDisplayName(configuration.ProfileDisplayName))
        {
            throw new ArgumentException("Le nom du profil serveur est invalide.", nameof(configuration));
        }

        var address = configuration.RconAddress.Trim();
        if (!RconEndpointValidator.IsAllowed(new RconEndpoint(
                address,
                configuration.RconPort,
                TimeSpan.FromSeconds(3))))
        {
            throw new ArgumentException("La cible RCON est invalide.", nameof(configuration));
        }

        return configuration with
        {
            SchemaVersion = OperatorConfiguration.CurrentSchemaVersion,
            ServerRoot = configuration.ServerRoot.Trim(),
            RconAddress = address,
            ProfileDisplayName = NormalizeProfileDisplayName(configuration.ProfileDisplayName)
        };
    }

    private static string NormalizeProfileDisplayName(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? OperatorConfiguration.DefaultProfileDisplayName
            : displayName.Trim();
}
