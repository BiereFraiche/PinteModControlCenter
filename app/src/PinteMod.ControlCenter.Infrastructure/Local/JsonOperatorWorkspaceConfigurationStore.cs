using System.Text.Json;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed partial class JsonOperatorWorkspaceConfigurationStore : IOperatorWorkspaceConfigurationStore
{
    private const int MaximumConfigurationBytes = 16 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly string _configurationPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonOperatorWorkspaceConfigurationStore(string? configurationPath = null)
    {
        _configurationPath = configurationPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PinteMod",
            "ControlCenter",
            "server-workspace.json");
    }

    public async Task<OperatorWorkspaceConfiguration> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_configurationPath))
            {
                return OperatorWorkspaceConfiguration.Default;
            }

            var info = new FileInfo(_configurationPath);
            if (info.Length is <= 0 or > MaximumConfigurationBytes)
            {
                return OperatorWorkspaceConfiguration.Default;
            }

            await using var stream = new FileStream(
                _configurationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var configuration = await JsonSerializer.DeserializeAsync<OperatorWorkspaceConfiguration>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            return NormalizeOrDefault(configuration);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return OperatorWorkspaceConfiguration.Default;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        OperatorWorkspaceConfiguration configuration,
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

    public static bool IsValidProfileId(string? profileId) =>
        !string.IsNullOrWhiteSpace(profileId) && ProfileIdPattern().IsMatch(profileId);

    private static OperatorWorkspaceConfiguration NormalizeOrDefault(
        OperatorWorkspaceConfiguration? configuration)
    {
        try
        {
            return configuration is null
                ? OperatorWorkspaceConfiguration.Default
                : NormalizeForSave(configuration);
        }
        catch (ArgumentException)
        {
            return OperatorWorkspaceConfiguration.Default;
        }
    }

    private static OperatorWorkspaceConfiguration NormalizeForSave(
        OperatorWorkspaceConfiguration configuration)
    {
        if (configuration.SchemaVersion != OperatorWorkspaceConfiguration.CurrentSchemaVersion ||
            configuration.ProfileIds is null)
        {
            throw new ArgumentException("La configuration multi-serveurs est invalide.", nameof(configuration));
        }

        var profileIds = configuration.ProfileIds
            .Where(IsValidProfileId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (profileIds.Length == 0 ||
            profileIds.Length > OperatorWorkspaceConfiguration.MaximumProfileCount ||
            profileIds.Length != configuration.ProfileIds.Count)
        {
            throw new ArgumentException("La liste des profils serveurs est invalide.", nameof(configuration));
        }

        var activeProfileId = profileIds.Contains(configuration.ActiveProfileId, StringComparer.Ordinal)
            ? configuration.ActiveProfileId
            : profileIds[0];
        return new OperatorWorkspaceConfiguration(
            OperatorWorkspaceConfiguration.CurrentSchemaVersion,
            profileIds,
            activeProfileId);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileIdPattern();
}
