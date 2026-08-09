using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class EasterEggRecordReader : IEasterEggRecordReader, IDisposable
{
    private const int SupportedProfileSchemaVersion = 3;
    private const int SupportedMapSchemaVersion = 2;
    private const int MaximumProfileFileSizeBytes = 256 * 1024;
    private const int MaximumMapFileSizeBytes = 1024 * 1024;
    private const int MaximumMapFiles = 100;

    private static readonly HashSet<string> KnownProfileStatuses = new(StringComparer.Ordinal)
    {
        "NO_MAIN_QUEST",
        "DIAGNOSTIC",
        "VALIDATED",
        "OFFICIAL"
    };

    private readonly EasterEggRecordsPathPolicy _pathPolicy;
    private readonly ReadOnlyEasterEggJsonFileReader _fileReader = new();
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private EasterEggRecordCatalog? _lastValid;
    private DateTimeOffset? _lastValidSourceTimestampUtc;
    private int _consecutiveFailures;

    public EasterEggRecordReader(LocalPinteModOptions options, IClock clock)
    {
        _pathPolicy = new EasterEggRecordsPathPolicy(options);
        _clock = clock;
    }

    public async Task<LocalReadResult<EasterEggRecordCatalog>> ReadAsync(CancellationToken cancellationToken = default)
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
                return new LocalReadResult<EasterEggRecordCatalog>(
                    result.Value,
                    SuccessMetadata(result.Value, result.LastWriteTimeUtc),
                    result.LastWriteTimeUtc);
            }

            _consecutiveFailures++;
            var durable = IsPotentialReadError(result.Status) && _consecutiveFailures >= 3;
            if (_lastValid is not null)
            {
                return new LocalReadResult<EasterEggRecordCatalog>(
                    _lastValid,
                    new LocalSourceMetadata(
                        result.Status,
                        DataFreshness.Stale,
                        CalculateAge(_lastValidSourceTimestampUtc),
                        DataProvenance.MemoryCache,
                        EasterEggRecordsPathPolicy.SourceLabel,
                        "Dernière donnée valide — lecture actuelle indisponible.",
                        _consecutiveFailures,
                        durable),
                    _lastValidSourceTimestampUtc);
            }

            return new LocalReadResult<EasterEggRecordCatalog>(
                null,
                new LocalSourceMetadata(
                    result.Status,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    EasterEggRecordsPathPolicy.SourceLabel,
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

    private async Task<LocalJsonFileResult<EasterEggRecordCatalog>> ReadCatalogAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var profilesPath = _pathPolicy.ResolveProfilesPath();
            var profiles = await _fileReader.ReadAsync(
                profilesPath,
                _pathPolicy.ValidateProfilesPath,
                MaximumProfileFileSizeBytes,
                ParseProfiles,
                cancellationToken);
            if (profiles.Status != LocalReadStatus.Success || profiles.Value is null)
            {
                return Failure(profiles.Status, profiles.Message, profiles.LastWriteTimeUtc);
            }

            var latest = profiles.LastWriteTimeUtc;
            var mapsDirectory = _pathPolicy.ResolveMapsDirectory();
            var mapsDirectoryPresent = Directory.Exists(mapsDirectory);
            if (!mapsDirectoryPresent)
            {
                return SuccessEmpty(profiles.Value, false, latest);
            }

            var allPaths = await Task.Run(_pathPolicy.EnumerateActiveMapJsonFiles, cancellationToken);
            if (allPaths.Count == 0)
            {
                return SuccessEmpty(profiles.Value, true, latest);
            }

            var selectedPaths = allPaths.Take(MaximumMapFiles).ToArray();
            var skippedFiles = allPaths.Count - selectedPaths.Length;
            var skippedSlots = 0;
            var validDocuments = 0;
            var attemptedOfficialDocuments = 0;
            var unsupportedSchemas = 0;
            var records = new List<EasterEggRecord>();

            foreach (var path in selectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var expectedMapCode = Path.GetFileNameWithoutExtension(path);
                if (!IsSafeMapCode(expectedMapCode) || !profiles.Value.OfficialMaps.Contains(expectedMapCode))
                {
                    skippedFiles++;
                    continue;
                }

                attemptedOfficialDocuments++;
                var file = await _fileReader.ReadAsync(
                    path,
                    _pathPolicy.ValidateMapFilePath,
                    MaximumMapFileSizeBytes,
                    root => ParseMap(root, expectedMapCode),
                    cancellationToken);
                latest = Latest(latest, file.LastWriteTimeUtc);
                if (file.Status == LocalReadStatus.Success && file.Value is not null)
                {
                    validDocuments++;
                    records.AddRange(file.Value.Records);
                    skippedSlots += file.Value.SlotsSkipped;
                }
                else
                {
                    skippedFiles++;
                    if (file.Status == LocalReadStatus.UnsupportedSchema)
                    {
                        unsupportedSchemas++;
                    }
                }
            }

            if (attemptedOfficialDocuments > 0 && validDocuments == 0)
            {
                var status = unsupportedSchemas > 0
                    ? LocalReadStatus.UnsupportedSchema
                    : LocalReadStatus.Invalid;
                return Failure(status, "Aucun fichier Easter Egg officiel valide n’a pu être lu.", latest);
            }

            var catalog = new EasterEggRecordCatalog(
                records
                    .OrderBy(record => record.MapName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(record => record.PlayerCount)
                    .ThenBy(record => record.Position)
                    .ToArray(),
                profiles.Value.OfficialMaps.Count,
                allPaths.Count,
                skippedFiles,
                skippedSlots,
                true);
            return new LocalJsonFileResult<EasterEggRecordCatalog>(
                catalog,
                LocalReadStatus.Success,
                latest,
                $"{catalog.Records.Count} Easter Egg Record(s) officiel(s) lu(s).");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(LocalReadStatus.AccessDenied, $"Accès refusé : {exception.Message}");
        }
        catch (IOException exception)
        {
            return Failure(LocalReadStatus.IoError, $"Lecture impossible : {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return Failure(LocalReadStatus.AccessDenied, $"Source locale refusée : {exception.Message}");
        }
    }

    private static LocalJsonFileResult<EasterEggRecordCatalog> SuccessEmpty(
        EasterEggProfileState profiles,
        bool mapsDirectoryPresent,
        DateTimeOffset? timestampUtc)
    {
        var catalog = new EasterEggRecordCatalog(
            [],
            profiles.OfficialMaps.Count,
            0,
            0,
            0,
            mapsDirectoryPresent);
        return new LocalJsonFileResult<EasterEggRecordCatalog>(
            catalog,
            LocalReadStatus.Success,
            timestampUtc,
            "Aucun Easter Egg Record officiel enregistré.");
    }

    private LocalSourceMetadata SuccessMetadata(EasterEggRecordCatalog catalog, DateTimeOffset? timestampUtc) =>
        new(
            LocalReadStatus.Success,
            DataFreshness.Fresh,
            CalculateAge(timestampUtc),
            DataProvenance.LocalFile,
            EasterEggRecordsPathPolicy.SourceLabel,
            catalog.MapFilesSkipped == 0 && catalog.RecordSlotsSkipped == 0
                ? catalog.Records.Count == 0
                    ? "Profil officiel lu · aucun Easter Egg Record officiel enregistré."
                    : $"{catalog.Records.Count} Easter Egg Record(s) officiel(s) lu(s)."
                : $"{catalog.Records.Count} record(s) lu(s) · {catalog.MapFilesSkipped} fichier(s) et {catalog.RecordSlotsSkipped} entrée(s) ignoré(s).");

    private TimeSpan? CalculateAge(DateTimeOffset? timestampUtc)
    {
        if (timestampUtc is null)
        {
            return null;
        }

        var age = _clock.UtcNow - timestampUtc.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static EasterEggProfileState ParseProfiles(JsonElement root)
    {
        EnsureObject(root);
        var schemaVersion = GetRequiredNonNegativeInt32(root, "schema_version");
        if (schemaVersion != SupportedProfileSchemaVersion)
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.UnsupportedSchema,
                $"Schéma des profils Easter Egg non pris en charge : {schemaVersion}.");
        }

        EnsureExpectedString(root, "identity_kind", "BOIII_XUID");
        EnsureExpectedString(root, "official_mode", "per_map_validated_only");

        var officialMaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var profileCount = 0;
        foreach (var property in root.EnumerateObject())
        {
            if (!property.Name.StartsWith("status_", StringComparison.Ordinal))
            {
                continue;
            }

            var mapCode = property.Name["status_".Length..];
            if (!IsSafeMapCode(mapCode) || property.Value.ValueKind != JsonValueKind.String)
            {
                throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Profil de carte Easter Egg invalide.");
            }

            var status = property.Value.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(status) || !KnownProfileStatuses.Contains(status))
            {
                throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Statut Easter Egg invalide pour {mapCode}.");
            }

            profileCount++;
            if (string.Equals(status, "OFFICIAL", StringComparison.Ordinal))
            {
                officialMaps.Add(mapCode);
            }
        }

        if (profileCount == 0)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Aucun profil de carte Easter Egg déclaré.");
        }

        return new EasterEggProfileState(officialMaps, profileCount);
    }

    private static ParsedMapRecords ParseMap(JsonElement root, string expectedMapCode)
    {
        EnsureObject(root);
        var schemaVersion = GetRequiredNonNegativeInt32(root, "schema_version");
        if (schemaVersion != SupportedMapSchemaVersion)
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.UnsupportedSchema,
                $"Schéma des records Easter Egg non pris en charge : {schemaVersion}.");
        }

        EnsureExpectedString(root, "identity_kind", "BOIII_XUID");
        EnsureExpectedString(root, "mode", "official");
        var mapCode = GetRequiredString(root, "map", 64);
        if (!IsSafeMapCode(mapCode) ||
            !string.Equals(mapCode, expectedMapCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Code carte Easter Egg invalide ou incohérent avec le nom de fichier.");
        }

        var mapName = GetOptionalString(root, "display", 128) ?? OfficialMapNameResolver.Resolve(mapCode);
        var records = new List<EasterEggRecord>(20);
        var skippedSlots = 0;
        for (var playerCount = 1; playerCount <= 4; playerCount++)
        {
            for (var position = 1; position <= 5; position++)
            {
                var result = TryParseRecord(root, mapCode, mapName, playerCount, position, out var record);
                if (result == SlotParseResult.Valid)
                {
                    records.Add(record!);
                }
                else if (result == SlotParseResult.Invalid)
                {
                    skippedSlots++;
                }
            }
        }

        return new ParsedMapRecords(records, skippedSlots);
    }

    private static SlotParseResult TryParseRecord(
        JsonElement root,
        string mapCode,
        string mapName,
        int playerCount,
        int position,
        out EasterEggRecord? record)
    {
        record = null;
        var secondsName = FieldName("seconds", playerCount, position);
        if (!root.TryGetProperty(secondsName, out var secondsProperty))
        {
            return SlotParseResult.Empty;
        }

        if (!secondsProperty.TryGetInt32(out var seconds) || seconds < 0)
        {
            return SlotParseResult.Invalid;
        }

        if (seconds == 0)
        {
            return SlotParseResult.Empty;
        }

        if (!TryGetSafeString(root, FieldName("holders", playerCount, position), 512, out var holders) ||
            !TryGetSafeString(root, FieldName("holder_xuids", playerCount, position), 128, out var rawXuids) ||
            !TryGetSafeString(root, FieldName("run_id", playerCount, position), 256, out var runId) ||
            !TryGetSafeString(root, FieldName("source", playerCount, position), 256, out var source) ||
            !TryGetNonNegativeInt32(root, FieldName("round", playerCount, position), out var round))
        {
            return SlotParseResult.Invalid;
        }

        var xuids = rawXuids
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(xuid => xuid.ToLowerInvariant())
            .ToArray();
        if (xuids.Length is < 1 ||
            xuids.Length > playerCount ||
            xuids.Any(xuid => !XuidValidator.IsValid(xuid)) ||
            xuids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != xuids.Length)
        {
            return SlotParseResult.Invalid;
        }

        record = new EasterEggRecord(
            mapCode,
            mapName,
            playerCount,
            position,
            round,
            TimeSpan.FromSeconds(seconds),
            holders,
            xuids,
            runId,
            source);
        return SlotParseResult.Valid;
    }

    private static string FieldName(string prefix, int playerCount, int position) =>
        $"{prefix}_{playerCount}p_{position}";

    private static bool TryGetNonNegativeInt32(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out value) &&
               value >= 0;
    }

    private static bool TryGetSafeString(
        JsonElement root,
        string propertyName,
        int maximumLength,
        out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var parsed = property.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(parsed) || parsed.Length > maximumLength || parsed.Any(char.IsControl))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static void EnsureExpectedString(JsonElement root, string propertyName, string expected)
    {
        var actual = GetRequiredString(root, propertyName, 64);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Valeur inattendue pour {propertyName}.");
        }
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

    private static bool IsSafeMapCode(string mapCode) =>
        mapCode.Length is > 0 and <= 64 &&
        mapCode.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static DateTimeOffset? Latest(DateTimeOffset? current, DateTimeOffset? candidate) =>
        current is null || candidate > current ? candidate : current;

    private static bool IsPotentialReadError(LocalReadStatus status) =>
        status is LocalReadStatus.Empty or
            LocalReadStatus.Invalid or
            LocalReadStatus.UnsupportedSchema or
            LocalReadStatus.AccessDenied or
            LocalReadStatus.IoError;

    private static LocalJsonFileResult<EasterEggRecordCatalog> Failure(
        LocalReadStatus status,
        string message,
        DateTimeOffset? timestampUtc = null) =>
        new(null, status, timestampUtc, message);

    private sealed record EasterEggProfileState(IReadOnlySet<string> OfficialMaps, int ProfileCount);

    private sealed record ParsedMapRecords(IReadOnlyList<EasterEggRecord> Records, int SlotsSkipped);

    private enum SlotParseResult
    {
        Empty,
        Valid,
        Invalid
    }
}
