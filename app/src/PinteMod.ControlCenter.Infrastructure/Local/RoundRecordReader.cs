using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class RoundRecordReader : IRoundRecordReader, IDisposable
{
    private const int SupportedSchemaVersion = 4;
    private const int MaximumMapFiles = 100;
    private const int MaximumMapFileSizeBytes = 1024 * 1024;

    private readonly RankRecordsPathPolicy _pathPolicy;
    private readonly ReadOnlyRankJsonFileReader _fileReader;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RoundRecordCatalog? _lastValid;
    private DateTimeOffset? _lastValidSourceTimestampUtc;
    private int _consecutiveFailures;

    public RoundRecordReader(LocalPinteModOptions options, IClock clock)
    {
        _pathPolicy = new RankRecordsPathPolicy(options);
        _fileReader = new ReadOnlyRankJsonFileReader(_pathPolicy);
        _clock = clock;
    }

    public async Task<LocalReadResult<RoundRecordCatalog>> ReadAsync(CancellationToken cancellationToken = default)
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
                return new LocalReadResult<RoundRecordCatalog>(
                    result.Value,
                    SuccessMetadata(result.Value, result.LastWriteTimeUtc),
                    result.LastWriteTimeUtc);
            }

            _consecutiveFailures++;
            var durable = IsPotentialReadError(result.Status) && _consecutiveFailures >= 3;
            if (_lastValid is not null)
            {
                return new LocalReadResult<RoundRecordCatalog>(
                    _lastValid,
                    new LocalSourceMetadata(
                        result.Status,
                        DataFreshness.Stale,
                        CalculateAge(_lastValidSourceTimestampUtc),
                        DataProvenance.MemoryCache,
                        RankRecordsPathPolicy.GetSourceLabel(RankRecordsDirectory.Maps),
                        "Dernière donnée valide — lecture actuelle indisponible.",
                        _consecutiveFailures,
                        durable),
                    _lastValidSourceTimestampUtc);
            }

            return new LocalReadResult<RoundRecordCatalog>(
                null,
                new LocalSourceMetadata(
                    result.Status,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    RankRecordsPathPolicy.GetSourceLabel(RankRecordsDirectory.Maps),
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

    private async Task<LocalJsonFileResult<RoundRecordCatalog>> ReadCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = _pathPolicy.ResolveDirectory(RankRecordsDirectory.Maps);
            if (!Directory.Exists(directory))
            {
                return Failure(LocalReadStatus.Missing, "Dossier de records de manches absent.");
            }

            var allPaths = await Task.Run(
                () => _pathPolicy.EnumerateActiveJsonFiles(RankRecordsDirectory.Maps),
                cancellationToken);
            if (allPaths.Count == 0)
            {
                return Failure(LocalReadStatus.Empty, "Aucun record de carte JSON actif.");
            }

            var selectedPaths = allPaths.Take(MaximumMapFiles).ToArray();
            var skippedFiles = allPaths.Count - selectedPaths.Length;
            var skippedSlots = 0;
            var records = new List<RoundRecord>();
            DateTimeOffset? latest = null;
            var validDocuments = 0;
            var unsupportedSchemas = 0;

            foreach (var path in selectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var expectedMapCode = Path.GetFileNameWithoutExtension(path);
                if (!IsSafeMapCode(expectedMapCode))
                {
                    skippedFiles++;
                    continue;
                }

                var file = await _fileReader.ReadAsync(
                    RankRecordsDirectory.Maps,
                    path,
                    MaximumMapFileSizeBytes,
                    root => Parse(root, expectedMapCode),
                    cancellationToken);
                if (file.Status == LocalReadStatus.Success && file.Value is not null)
                {
                    validDocuments++;
                    records.AddRange(file.Value.Records);
                    skippedSlots += file.Value.SlotsSkipped;
                    latest = Latest(latest, file.LastWriteTimeUtc);
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

            if (validDocuments == 0)
            {
                var status = unsupportedSchemas > 0
                    ? LocalReadStatus.UnsupportedSchema
                    : LocalReadStatus.Invalid;
                return Failure(status, "Aucun fichier de records de manches valide n’a pu être lu.", latest);
            }

            var catalog = new RoundRecordCatalog(
                records
                    .OrderBy(record => record.MapName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(record => record.PlayerCount)
                    .ThenBy(record => record.Position)
                    .ToArray(),
                allPaths.Count,
                skippedFiles,
                skippedSlots);
            return new LocalJsonFileResult<RoundRecordCatalog>(
                catalog,
                LocalReadStatus.Success,
                latest,
                $"{catalog.Records.Count} record(s) lu(s) dans {validDocuments} fichier(s).");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(LocalReadStatus.AccessDenied, $"Accès refusé : {exception.Message}");
        }
        catch (IOException exception)
        {
            return Failure(LocalReadStatus.IoError, $"Lecture impossible : {exception.Message}");
        }
    }

    private LocalSourceMetadata SuccessMetadata(RoundRecordCatalog catalog, DateTimeOffset? timestampUtc) =>
        new(
            LocalReadStatus.Success,
            DataFreshness.Fresh,
            CalculateAge(timestampUtc),
            DataProvenance.LocalFile,
            RankRecordsPathPolicy.GetSourceLabel(RankRecordsDirectory.Maps),
            catalog.FilesSkipped == 0 && catalog.SlotsSkipped == 0
                ? $"{catalog.Records.Count} record(s) de manches local(aux) lu(s)."
                : $"{catalog.Records.Count} record(s) lu(s) · {catalog.FilesSkipped} fichier(s) et {catalog.SlotsSkipped} entrée(s) ignoré(s).");

    private TimeSpan? CalculateAge(DateTimeOffset? timestampUtc)
    {
        if (timestampUtc is null)
        {
            return null;
        }

        var age = _clock.UtcNow - timestampUtc.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static ParsedMapRecords Parse(JsonElement root, string expectedMapCode)
    {
        EnsureObject(root);
        var schemaVersion = GetRequiredNonNegativeInt32(root, "schema_version");
        if (schemaVersion != SupportedSchemaVersion)
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.UnsupportedSchema,
                $"Schéma records de manches non pris en charge : {schemaVersion}.");
        }

        var mapCode = GetRequiredString(root, "map", 64);
        if (!IsSafeMapCode(mapCode) ||
            !string.Equals(mapCode, expectedMapCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Code carte invalide ou incohérent avec le nom de fichier.");
        }

        var mapName = GetOptionalString(root, "display", 128) ?? OfficialMapNameResolver.Resolve(mapCode);
        var records = new List<RoundRecord>(20);
        var skippedSlots = 0;

        for (var playerCount = 1; playerCount <= 4; playerCount++)
        {
            for (var position = 1; position <= 5; position++)
            {
                var parseResult = TryParseRecord(
                    root,
                    mapCode,
                    mapName,
                    playerCount,
                    position,
                    out var record);
                if (parseResult == SlotParseResult.Valid)
                {
                    records.Add(record!);
                }
                else if (parseResult == SlotParseResult.Invalid)
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
        out RoundRecord? record)
    {
        record = null;
        var roundName = FieldName("round", playerCount, position);
        if (!root.TryGetProperty(roundName, out var roundProperty))
        {
            return SlotParseResult.Empty;
        }

        if (!roundProperty.TryGetInt32(out var round) || round < 0)
        {
            return SlotParseResult.Invalid;
        }

        if (round == 0)
        {
            return SlotParseResult.Empty;
        }

        if (!TryGetPositiveInt32(root, FieldName("seconds", playerCount, position), out var seconds) ||
            !TryGetSafeString(root, FieldName("holders", playerCount, position), 512, out var holders) ||
            !TryGetSafeString(root, FieldName("holder_xuids", playerCount, position), 128, out var rawXuids) ||
            !TryGetSafeString(root, FieldName("match_id", playerCount, position), 128, out var matchId))
        {
            return SlotParseResult.Invalid;
        }

        var xuids = rawXuids
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(xuid => xuid.ToLowerInvariant())
            .ToArray();
        if (xuids.Length != playerCount || xuids.Any(xuid => !XuidValidator.IsValid(xuid)))
        {
            return SlotParseResult.Invalid;
        }

        record = new RoundRecord(
            mapCode,
            mapName,
            playerCount,
            position,
            round,
            TimeSpan.FromSeconds(seconds),
            holders,
            xuids,
            matchId);
        return SlotParseResult.Valid;
    }

    private static string FieldName(string prefix, int playerCount, int position) =>
        $"{prefix}_{playerCount}p_{position}";

    private static bool TryGetPositiveInt32(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out value) &&
               value > 0;
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
        if (string.IsNullOrWhiteSpace(parsed) ||
            parsed.Length > maximumLength ||
            parsed.Any(char.IsControl))
        {
            return false;
        }

        value = parsed;
        return true;
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

    private static LocalJsonFileResult<RoundRecordCatalog> Failure(
        LocalReadStatus status,
        string message,
        DateTimeOffset? timestampUtc = null) =>
        new(null, status, timestampUtc, message);

    private sealed record ParsedMapRecords(IReadOnlyList<RoundRecord> Records, int SlotsSkipped);

    private enum SlotParseResult
    {
        Empty,
        Valid,
        Invalid
    }
}
