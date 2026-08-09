using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class RankProfileReader : IRankProfileReader, IDisposable
{
    private const int SupportedSchemaVersion = 2;
    private const int MaximumProfileFiles = 1000;
    private const int MaximumProfileFileSizeBytes = 64 * 1024;

    private readonly RankRecordsPathPolicy _pathPolicy;
    private readonly ReadOnlyRankJsonFileReader _fileReader;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RankProfileCatalog? _lastValid;
    private DateTimeOffset? _lastValidSourceTimestampUtc;
    private int _consecutiveFailures;

    public RankProfileReader(LocalPinteModOptions options, IClock clock)
    {
        _pathPolicy = new RankRecordsPathPolicy(options);
        _fileReader = new ReadOnlyRankJsonFileReader(_pathPolicy);
        _clock = clock;
    }

    public async Task<LocalReadResult<RankProfileCatalog>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var result = await ReadCatalogAsync(cancellationToken);
            if (result.Status == LocalReadStatus.Success && result.Value is not null)
            {
                _lastValid = result.Value;
                _lastValidSourceTimestampUtc = result.LastWriteTimeUtc;
                _consecutiveFailures = 0;
                return new LocalReadResult<RankProfileCatalog>(
                    result.Value,
                    SuccessMetadata(result.Value, result.LastWriteTimeUtc),
                    result.LastWriteTimeUtc);
            }

            _consecutiveFailures++;
            var durable = IsPotentialReadError(result.Status) && _consecutiveFailures >= 3;
            if (_lastValid is not null)
            {
                return new LocalReadResult<RankProfileCatalog>(
                    _lastValid,
                    new LocalSourceMetadata(
                        result.Status,
                        DataFreshness.Stale,
                        CalculateAge(_lastValidSourceTimestampUtc),
                        DataProvenance.MemoryCache,
                        RankRecordsPathPolicy.GetSourceLabel(RankRecordsDirectory.Players),
                        "Dernière donnée valide — lecture actuelle indisponible.",
                        _consecutiveFailures,
                        durable),
                    _lastValidSourceTimestampUtc);
            }

            return new LocalReadResult<RankProfileCatalog>(
                null,
                new LocalSourceMetadata(
                    result.Status,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    RankRecordsPathPolicy.GetSourceLabel(RankRecordsDirectory.Players),
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

    private async Task<LocalJsonFileResult<RankProfileCatalog>> ReadCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = _pathPolicy.ResolveDirectory(RankRecordsDirectory.Players);
            if (!Directory.Exists(directory))
            {
                return Failure(LocalReadStatus.Missing, "Dossier de profils Ranks absent.");
            }

            var allPaths = await Task.Run(
                () => _pathPolicy.EnumerateActiveJsonFiles(RankRecordsDirectory.Players),
                cancellationToken);
            if (allPaths.Count == 0)
            {
                return Failure(LocalReadStatus.Empty, "Aucun profil JSON actif.");
            }

            var selectedPaths = allPaths.Take(MaximumProfileFiles).ToArray();
            var skipped = allPaths.Count - selectedPaths.Length;
            var profiles = new List<RankProfile>(selectedPaths.Length);
            DateTimeOffset? latest = null;
            var unsupportedSchemas = 0;

            foreach (var path in selectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var expectedXuid = Path.GetFileNameWithoutExtension(path);
                if (!XuidValidator.IsValid(expectedXuid))
                {
                    skipped++;
                    continue;
                }

                var file = await _fileReader.ReadAsync(
                    RankRecordsDirectory.Players,
                    path,
                    MaximumProfileFileSizeBytes,
                    root => Parse(root, expectedXuid),
                    cancellationToken);
                if (file.Status == LocalReadStatus.Success && file.Value is not null)
                {
                    profiles.Add(file.Value);
                    latest = Latest(latest, file.LastWriteTimeUtc);
                }
                else
                {
                    skipped++;
                    if (file.Status == LocalReadStatus.UnsupportedSchema)
                    {
                        unsupportedSchemas++;
                    }
                }
            }

            if (profiles.Count == 0)
            {
                var status = unsupportedSchemas > 0
                    ? LocalReadStatus.UnsupportedSchema
                    : LocalReadStatus.Invalid;
                return Failure(status, "Aucun profil Ranks valide n’a pu être lu.", latest);
            }

            var catalog = new RankProfileCatalog(
                profiles
                    .OrderByDescending(profile => profile.TotalPlayTime)
                    .ThenByDescending(profile => profile.BestOverallRound)
                    .ThenBy(profile => profile.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray(),
                allPaths.Count,
                skipped);
            return new LocalJsonFileResult<RankProfileCatalog>(
                catalog,
                LocalReadStatus.Success,
                latest,
                skipped == 0
                    ? $"{profiles.Count} profil(s) lu(s)."
                    : $"{profiles.Count} profil(s) lu(s), {skipped} fichier(s) ignoré(s).");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(LocalReadStatus.AccessDenied, "Accès aux profils Ranks refusé.");
        }
        catch (IOException)
        {
            return Failure(LocalReadStatus.IoError, "Lecture des profils Ranks impossible.");
        }
        catch (InvalidOperationException)
        {
            return Failure(LocalReadStatus.AccessDenied, "Source locale Ranks refusée.");
        }
    }

    private LocalSourceMetadata SuccessMetadata(RankProfileCatalog catalog, DateTimeOffset? timestampUtc) =>
        new(
            LocalReadStatus.Success,
            DataFreshness.Fresh,
            CalculateAge(timestampUtc),
            DataProvenance.LocalFile,
            RankRecordsPathPolicy.GetSourceLabel(RankRecordsDirectory.Players),
            catalog.FilesSkipped == 0
                ? $"{catalog.Profiles.Count} profil(s) local(aux) lu(s)."
                : $"{catalog.Profiles.Count} profil(s) lu(s) · {catalog.FilesSkipped} fichier(s) ignoré(s).");

    private TimeSpan? CalculateAge(DateTimeOffset? timestampUtc)
    {
        if (timestampUtc is null)
        {
            return null;
        }

        var age = _clock.UtcNow - timestampUtc.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static RankProfile Parse(JsonElement root, string expectedXuid)
    {
        EnsureObject(root);
        var schemaVersion = GetRequiredNonNegativeInt32(root, "schema_version");
        if (schemaVersion != SupportedSchemaVersion)
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.UnsupportedSchema,
                $"Schéma profil Ranks non pris en charge : {schemaVersion}.");
        }

        var xuid = GetRequiredString(root, "xuid", 32);
        if (!XuidValidator.IsValid(xuid) ||
            !string.Equals(xuid, expectedXuid, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "XUID profil invalide ou incohérent avec le nom de fichier.");
        }

        var displayName = GetOptionalString(root, "last_name", 64) ??
                          GetOptionalString(root, "name", 64);
        if (displayName is null)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Pseudo d’affichage absent.");
        }

        return new RankProfile(
            xuid.ToLowerInvariant(),
            displayName,
            GetRequiredNonNegativeInt32(root, "sessions"),
            TimeSpan.FromSeconds(GetRequiredNonNegativeInt32(root, "total_seconds")),
            GetRequiredNonNegativeInt32(root, "best_overall_round"));
    }

    private static void EnsureObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "La racine JSON doit être un objet.");
        }
    }

    private static int GetRequiredNonNegativeInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            !property.TryGetInt32(out var value) ||
            value < 0)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ numérique invalide : {propertyName}.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement root, string propertyName, int maximumLength) =>
        GetOptionalString(root, propertyName, maximumLength) ??
        throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ texte invalide : {propertyName}.");

    private static string? GetOptionalString(JsonElement root, string propertyName, int maximumLength)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > maximumLength || value.Any(char.IsControl))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ texte invalide : {propertyName}.");
        }

        return value;
    }

    private static DateTimeOffset? Latest(DateTimeOffset? current, DateTimeOffset? candidate) =>
        current is null || candidate > current ? candidate : current;

    private static bool IsPotentialReadError(LocalReadStatus status) =>
        status is LocalReadStatus.Empty or
            LocalReadStatus.Invalid or
            LocalReadStatus.UnsupportedSchema or
            LocalReadStatus.AccessDenied or
            LocalReadStatus.IoError;

    private static LocalJsonFileResult<RankProfileCatalog> Failure(
        LocalReadStatus status,
        string message,
        DateTimeOffset? timestampUtc = null) =>
        new(null, status, timestampUtc, message);
}
