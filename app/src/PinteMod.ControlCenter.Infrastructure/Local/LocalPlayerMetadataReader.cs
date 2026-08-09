using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class LocalPlayerMetadataReader : ILocalPlayerMetadataReader, IDisposable
{
    private const int MaximumProfiles = 2048;
    private readonly BlockALocalPathPolicy _paths;
    private readonly ReadOnlyBlockAJsonFileReader _reader = new();
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LocalPlayerMetadataSnapshot? _cached;
    private DateTimeOffset? _cachedTimestamp;

    public LocalPlayerMetadataReader(LocalPinteModOptions options, IClock clock)
    {
        _paths = new BlockALocalPathPolicy(options);
        _clock = clock;
    }

    public async Task<LocalReadResult<LocalPlayerMetadataSnapshot>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var players = new Dictionary<string, MutableMetadata>(StringComparer.OrdinalIgnoreCase);
            var scanned = 0;
            var skipped = 0;
            var timestamps = new List<DateTimeOffset>();

            var roles = await _reader.ReadAsync(_paths.ResolveFixed(BlockALocalFile.Roles), ParseRoles, cancellationToken);
            if (roles.Status == LocalReadStatus.Success && roles.Value is not null)
            {
                scanned++;
                if (roles.LastWriteTimeUtc is not null)
                {
                    timestamps.Add(roles.LastWriteTimeUtc.Value);
                }

                foreach (var item in roles.Value)
                {
                    players[item.Xuid] = new MutableMetadata(item.Xuid, item.DisplayName, item.Role, null, null);
                }
            }
            else if (roles.Status != LocalReadStatus.Missing)
            {
                skipped++;
            }

            await ReadLanguagesAsync(false, players, timestamps, value => value.AutomaticLanguage = value.Language, counters =>
            {
                scanned += counters.Scanned;
                skipped += counters.Skipped;
            }, cancellationToken);
            await ReadLanguagesAsync(true, players, timestamps, value => value.ManualLanguage = value.Language, counters =>
            {
                scanned += counters.Scanned;
                skipped += counters.Skipped;
            }, cancellationToken);

            if (scanned == 0 && roles.Status != LocalReadStatus.Success)
            {
                return CachedOrFailure(
                    roles.Status,
                    roles.Status == LocalReadStatus.Missing
                        ? "Aucune métadonnée joueur locale disponible."
                        : "Les métadonnées joueur locales sont invalides ou indisponibles.");
            }

            var snapshot = new LocalPlayerMetadataSnapshot(
                players.Values.Take(MaximumProfiles).Select(item => new LocalPlayerMetadata(
                    item.Xuid,
                    item.DisplayName,
                    item.Role,
                    item.ManualLanguage ?? item.AutomaticLanguage,
                    item.CountryCode)).ToArray(),
                scanned,
                skipped);
            _cached = snapshot;
            _cachedTimestamp = timestamps.Count == 0 ? _clock.UtcNow : timestamps.Max();
            var age = Age(_cachedTimestamp);
            return new(snapshot, new(
                LocalReadStatus.Success,
                DataFreshness.Fresh,
                age,
                DataProvenance.LocalFile,
                "identity/roles.json + localization/{manual,auto}/*.json",
                $"Métadonnées locales lues : {snapshot.Players.Count} profil(s), {skipped} fichier(s) ignoré(s)."), _cachedTimestamp);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task ReadLanguagesAsync(
        bool manual,
        Dictionary<string, MutableMetadata> players,
        List<DateTimeOffset> timestamps,
        Action<LanguageValue> applyLanguage,
        Action<ReadCounters> applyCounters,
        CancellationToken cancellationToken)
    {
        var scanned = 0;
        var skipped = 0;
        string directory;
        try
        {
            directory = _paths.ResolveLocalizationDirectory(manual);
        }
        catch (IOException)
        {
            applyCounters(new(scanned, skipped + 1));
            return;
        }

        if (!Directory.Exists(directory))
        {
            applyCounters(new(scanned, skipped));
            return;
        }

        foreach (var info in new DirectoryInfo(directory).EnumerateFiles("*.json").OrderBy(item => item.Name).Take(MaximumProfiles))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var path = _paths.ResolveLocalizationFilePath(manual, info.Name);
                var result = await _reader.ReadAsync(path, ParseLanguage, cancellationToken);
                if (result.Status != LocalReadStatus.Success || result.Value is null)
                {
                    skipped++;
                    continue;
                }

                if (!string.Equals(
                        Path.GetFileNameWithoutExtension(info.Name),
                        result.Value.Xuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                scanned++;
                if (result.LastWriteTimeUtc is not null)
                {
                    timestamps.Add(result.LastWriteTimeUtc.Value);
                }

                if (!players.TryGetValue(result.Value.Xuid, out var metadata))
                {
                    metadata = new MutableMetadata(result.Value.Xuid, null, null, null, null);
                    players[result.Value.Xuid] = metadata;
                }

                result.Value.Target = metadata;
                applyLanguage(result.Value);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                skipped++;
            }
        }

        applyCounters(new(scanned, skipped));
    }

    private LocalReadResult<LocalPlayerMetadataSnapshot> CachedOrFailure(LocalReadStatus status, string message)
    {
        if (_cached is not null)
        {
            return new(_cached, new(status, DataFreshness.Stale, Age(_cachedTimestamp), DataProvenance.MemoryCache,
                "Métadonnées joueur locales", "Dernière donnée valide — lecture actuelle indisponible."), _cachedTimestamp);
        }

        return new(null, new(status, DataFreshness.Unknown, null, DataProvenance.LocalFile, "Métadonnées joueur locales", message), null);
    }

    private TimeSpan? Age(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
        {
            return null;
        }

        var age = _clock.UtcNow - timestamp.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static IReadOnlyList<LocalPlayerMetadata> ParseRoles(JsonElement root)
    {
        RequireObject(root, "racine des rôles");
        var schema = RequiredInt(root, "schema_version");
        if (schema != 1)
        {
            throw new LocalJsonValidationException(LocalReadStatus.UnsupportedSchema, $"Schéma de rôles non pris en charge : {schema}.");
        }

        var count = RequiredInt(root, "count");
        if (count < 0 || count > MaximumProfiles)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Nombre de rôles invalide.");
        }

        var result = new List<LocalPlayerMetadata>(count);
        for (var index = 1; index <= count; index++)
        {
            var xuid = RequiredText(root, $"xuid_{index}", 32);
            if (!XuidValidator.IsValid(xuid))
            {
                throw new LocalJsonValidationException(LocalReadStatus.Invalid, "XUID de rôle invalide.");
            }

            result.Add(new(xuid, LogPrivacyFilter.SafePlayerName(RequiredText(root, $"display_{index}", 80)),
                RequiredText(root, $"role_{index}", 32).ToLowerInvariant(), null, null));
        }

        return result;
    }

    private static LanguageValue ParseLanguage(JsonElement root)
    {
        RequireObject(root, "racine de langue");
        var xuid = RequiredText(root, "xuid", 32);
        if (!XuidValidator.IsValid(xuid))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "XUID de langue invalide.");
        }

        var language = RequiredText(root, "language", 16).ToLowerInvariant();
        if (!language.All(character => char.IsAsciiLetter(character) || character is '-' or '_'))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Code langue invalide.");
        }

        return new LanguageValue(xuid, language);
    }

    private static int RequiredInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {name}.");

    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Structure JSON invalide : {label}.");
        }
    }

    private static string RequiredText(JsonElement root, string name, int maximum) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString()) && value.GetString()!.Length <= maximum
            ? value.GetString()!.Trim()
            : throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {name}.");

    private sealed class MutableMetadata(
        string xuid,
        string? displayName,
        string? role,
        string? automaticLanguage,
        string? countryCode)
    {
        public string Xuid { get; } = xuid;
        public string? DisplayName { get; } = displayName;
        public string? Role { get; } = role;
        public string? AutomaticLanguage { get; set; } = automaticLanguage;
        public string? ManualLanguage { get; set; }
        public string? CountryCode { get; } = countryCode;
    }

    private sealed class LanguageValue(string xuid, string language)
    {
        public string Xuid { get; } = xuid;
        public string Language { get; } = language;
        public MutableMetadata? Target { get; set; }
        public string? AutomaticLanguage { set => Target!.AutomaticLanguage = value; }
        public string? ManualLanguage { set => Target!.ManualLanguage = value; }
    }

    private sealed record ReadCounters(int Scanned, int Skipped);
}
