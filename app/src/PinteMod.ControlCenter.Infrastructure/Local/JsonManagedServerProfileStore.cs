using System.Text.Json;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class JsonManagedServerProfileStore
{
    private const int MaximumConfigurationBytes = 16 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonManagedServerProfileStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public async Task<ManagedServerProfileConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return ManagedServerProfileConfiguration.Default;
            }

            var info = new FileInfo(_path);
            if (info.Length is <= 0 or > MaximumConfigurationBytes)
            {
                return ManagedServerProfileConfiguration.Default;
            }

            await using var stream = File.OpenRead(_path);
            var configuration = await JsonSerializer.DeserializeAsync<ManagedServerProfileConfiguration>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            return Normalize(configuration);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return ManagedServerProfileConfiguration.Default;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        ManagedServerProfileConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var normalized = NormalizeForSave(configuration);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temp = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await using (var stream = new FileStream(
                             temp,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }

            _gate.Release();
        }
    }

    private static ManagedServerProfileConfiguration Normalize(ManagedServerProfileConfiguration? value)
    {
        try
        {
            return value is null ? ManagedServerProfileConfiguration.Default : NormalizeForSave(value);
        }
        catch (ArgumentException)
        {
            return ManagedServerProfileConfiguration.Default;
        }
    }

    private static ManagedServerProfileConfiguration NormalizeForSave(ManagedServerProfileConfiguration value)
    {
        if (value.SchemaVersion != ManagedServerProfileConfiguration.CurrentSchemaVersion)
        {
            throw new ArgumentException("Configuration de lancement invalide.", nameof(value));
        }

        var launcher = value.LauncherRelativePath?.Trim() ?? string.Empty;
        if (launcher.Length > 260 || launcher.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || Path.IsPathRooted(launcher))
        {
            throw new ArgumentException("Chemin de lanceur invalide.", nameof(value));
        }

        if (launcher.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment == ".."))
        {
            throw new ArgumentException("Le lanceur doit rester sous la racine serveur.", nameof(value));
        }

        var remoteAgentId = value.RemoteAgentId?.Trim() ?? string.Empty;
        if (remoteAgentId.Length > 80 || remoteAgentId.Any(character =>
                !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new ArgumentException("Identifiant Agent distant invalide.", nameof(value));
        }

        return new ManagedServerProfileConfiguration(
            ManagedServerProfileConfiguration.CurrentSchemaVersion,
            launcher)
        {
            RemoteAgentId = remoteAgentId
        };
    }
}
