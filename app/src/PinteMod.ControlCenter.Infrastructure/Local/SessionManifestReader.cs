using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class SessionManifestReader : ISessionManifestReader, IDisposable
{
    private readonly ReadOnlyJsonFileReader _fileReader;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SessionManifest? _lastValid;
    private DateTimeOffset? _lastValidSourceTimestampUtc;
    private int _consecutiveFailures;

    public SessionManifestReader(LocalPinteModOptions options, IClock clock)
    {
        _fileReader = new ReadOnlyJsonFileReader(options);
        _clock = clock;
    }

    public async Task<LocalReadResult<SessionManifest>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var result = await _fileReader.ReadAsync(
                LocalPinteModFile.CurrentSession,
                Parse,
                cancellationToken);

            if (result.Status == LocalReadStatus.Success && result.Value is not null)
            {
                _lastValid = result.Value;
                _lastValidSourceTimestampUtc = result.LastWriteTimeUtc;
                _consecutiveFailures = 0;
                return new LocalReadResult<SessionManifest>(
                    result.Value,
                    CreateSuccessMetadata(result.LastWriteTimeUtc),
                    result.LastWriteTimeUtc);
            }

            _consecutiveFailures++;
            var durable = IsPotentialReadError(result.Status) && _consecutiveFailures >= 3;
            if (_lastValid is not null)
            {
                return new LocalReadResult<SessionManifest>(
                    _lastValid,
                    new LocalSourceMetadata(
                        result.Status,
                        DataFreshness.Stale,
                        CalculateAge(_lastValidSourceTimestampUtc),
                        DataProvenance.MemoryCache,
                        LocalPinteModOptions.GetSourceLabel(LocalPinteModFile.CurrentSession),
                        "Dernière donnée valide — lecture actuelle indisponible.",
                        _consecutiveFailures,
                        durable),
                    _lastValidSourceTimestampUtc);
            }

            return new LocalReadResult<SessionManifest>(
                null,
                new LocalSourceMetadata(
                    result.Status,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    LocalPinteModOptions.GetSourceLabel(LocalPinteModFile.CurrentSession),
                    result.Message,
                    _consecutiveFailures,
                    durable),
                result.LastWriteTimeUtc);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private LocalSourceMetadata CreateSuccessMetadata(DateTimeOffset? sourceTimestampUtc) =>
        new(
            LocalReadStatus.Success,
            DataFreshness.Fresh,
            CalculateAge(sourceTimestampUtc),
            DataProvenance.LocalFile,
            LocalPinteModOptions.GetSourceLabel(LocalPinteModFile.CurrentSession),
            "Manifeste de session local lu avec succès.");

    private TimeSpan? CalculateAge(DateTimeOffset? timestampUtc)
    {
        if (timestampUtc is null)
        {
            return null;
        }

        var age = _clock.UtcNow - timestampUtc.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static bool IsPotentialReadError(LocalReadStatus status) =>
        status is LocalReadStatus.Empty or
            LocalReadStatus.Invalid or
            LocalReadStatus.UnsupportedSchema or
            LocalReadStatus.AccessDenied or
            LocalReadStatus.IoError;

    private static SessionManifest Parse(JsonElement root)
    {
        var schemaVersion = GetRequiredInt32(root, "schema_version");
        if (schemaVersion != 1)
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.UnsupportedSchema,
                $"Schéma current_session non pris en charge : {schemaVersion}.");
        }

        return new SessionManifest(
            schemaVersion,
            GetRequiredString(root, "module_version"),
            GetRequiredString(root, "session_id"),
            GetRequiredString(root, "map"),
            GetRequiredInt64(root, "started_gettime"));
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {propertyName}.");
        }

        return property.GetString()!.Trim();
    }

    private static int GetRequiredInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || !property.TryGetInt32(out var value))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {propertyName}.");
        }

        return value;
    }

    private static long GetRequiredInt64(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || !property.TryGetInt64(out var value))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {propertyName}.");
        }

        return value;
    }
}
