using System.Text.Json;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class JsonMapCatalogService : IMapCatalogService
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumCatalogBytes = 128 * 1024;
    private const int MaximumRotationLineLength = 16 * 1024;
    private const int MaximumCustomMaps = 256;
    private const int MaximumDisplayNameLength = 80;
    private static readonly Regex RotationPattern = new(
        "^\\s*set\\s+sv_maprotation\\s+\"(?<rotation>[^\"]+)\"\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly string _catalogPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, PersistedMapEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public JsonMapCatalogService(string? catalogPath = null)
    {
        _catalogPath = catalogPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PinteMod",
            "ControlCenter",
            "map-catalog.json");
    }

    public async Task<MapCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return BuildSnapshot(_entries);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MapCatalogOperationResult> ImportRotationLineAsync(
        string rotationLine,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseRotationLine(rotationLine, out var mapCodes))
        {
            return Failure(
                "LIGNE REFUSÉE",
                "Collez uniquement une ligne active « set sv_maprotation » contenant des codes de carte valides.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var updated = Clone(_entries);
            foreach (var entry in updated.Values)
            {
                entry.IsInServerRotation = false;
            }

            foreach (var code in mapCodes)
            {
                if (OfficialMapCatalog.Contains(code))
                {
                    updated.TryAdd(code, new PersistedMapEntry
                    {
                        Code = code,
                        DisplayName = OfficialMapCatalog.ResolveName(code)
                    });
                }
                else if (!updated.ContainsKey(code) && updated.Count >= MaximumCustomMaps)
                {
                    return Failure("CATALOGUE PLEIN", "Le catalogue local a atteint sa limite de sécurité.");
                }
                else
                {
                    updated.TryAdd(code, new PersistedMapEntry { Code = code, DisplayName = code });
                }

                updated[code].IsInServerRotation = true;
            }

            RemoveUnusedEntries(updated);
            if (!await TrySaveAsync(updated, cancellationToken).ConfigureAwait(false))
            {
                return Failure("ERREUR LOCALE", "La rotation n’a pas pu être enregistrée dans le catalogue du Control Center.");
            }

            _entries = updated;
            return new MapCatalogOperationResult(
                true,
                "ROTATION IMPORTÉE",
                $"{mapCodes.Count} carte(s) validée(s) · aucune configuration serveur modifiée.",
                mapCodes.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MapCatalogOperationResult> AddManualMapAsync(
        string mapCode,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        if (!MapCodeValidator.TryNormalize(mapCode, out var code) ||
            !TryNormalizeDisplayName(displayName, out var name))
        {
            return Failure(
                "CARTE REFUSÉE",
                "Le code accepte uniquement a-z, 0-9 et _, et le nom doit contenir 1 à 80 caractères lisibles.");
        }

        if (OfficialMapCatalog.Contains(code))
        {
            return Failure("DÉJÀ OFFICIELLE", "Cette carte existe déjà dans le catalogue officiel.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var updated = Clone(_entries);
            if (!updated.TryGetValue(code, out var entry))
            {
                if (updated.Count >= MaximumCustomMaps)
                {
                    return Failure("CATALOGUE PLEIN", "Le catalogue local a atteint sa limite de sécurité.");
                }

                entry = new PersistedMapEntry { Code = code };
                updated.Add(code, entry);
            }

            entry.DisplayName = name;
            entry.IsManual = true;
            if (!await TrySaveAsync(updated, cancellationToken).ConfigureAwait(false))
            {
                return Failure("ERREUR LOCALE", "La carte custom n’a pas pu être enregistrée localement.");
            }

            _entries = updated;
            return new MapCatalogOperationResult(
                true,
                "CARTE AJOUTÉE",
                "La carte custom est disponible dans le Control Center. Aucun fichier serveur n’a été modifié.",
                1);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MapCatalogOperationResult> RemoveManualMapAsync(
        string mapCode,
        CancellationToken cancellationToken = default)
    {
        if (!MapCodeValidator.TryNormalize(mapCode, out var code))
        {
            return Failure("CARTE REFUSÉE", "Le code de carte est invalide.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            if (!_entries.TryGetValue(code, out var current) || !current.IsManual)
            {
                return Failure("NON SUPPRIMABLE", "Seules les cartes ajoutées manuellement peuvent être retirées ici.");
            }

            var updated = Clone(_entries);
            updated[code].IsManual = false;
            RemoveUnusedEntries(updated);
            if (!await TrySaveAsync(updated, cancellationToken).ConfigureAwait(false))
            {
                return Failure("ERREUR LOCALE", "La carte n’a pas pu être retirée du catalogue local.");
            }

            _entries = updated;
            return new MapCatalogOperationResult(
                true,
                "CARTE RETIRÉE",
                "L’entrée manuelle a été retirée du Control Center uniquement.",
                1);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MapCatalogOperationResult> ObserveMapAsync(
        string mapCode,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        if (!MapCodeValidator.TryNormalize(mapCode, out var code))
        {
            return Failure("CARTE IGNORÉE", "Le code observé ne respecte pas le format autorisé.");
        }

        if (OfficialMapCatalog.Contains(code))
        {
            return new MapCatalogOperationResult(true, "CARTE OFFICIELLE", "Carte déjà connue.");
        }

        var name = TryNormalizeDisplayName(displayName, out var normalizedName) ? normalizedName : code;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            if (_entries.TryGetValue(code, out var existing) && existing.IsObserved)
            {
                return new MapCatalogOperationResult(true, "CARTE CONNUE", "Carte custom déjà observée.");
            }

            var updated = Clone(_entries);
            if (!updated.TryGetValue(code, out var entry))
            {
                if (updated.Count >= MaximumCustomMaps)
                {
                    return Failure("CATALOGUE PLEIN", "La carte observée n’a pas été mémorisée.");
                }

                entry = new PersistedMapEntry { Code = code, DisplayName = name };
                updated.Add(code, entry);
            }

            entry.IsObserved = true;
            if (string.Equals(entry.DisplayName, entry.Code, StringComparison.OrdinalIgnoreCase))
            {
                entry.DisplayName = name;
            }

            if (!await TrySaveAsync(updated, cancellationToken).ConfigureAwait(false))
            {
                return Failure("ERREUR LOCALE", "La carte observée n’a pas pu être mémorisée.");
            }

            _entries = updated;
            return new MapCatalogOperationResult(true, "CARTE OBSERVÉE", "Carte custom ajoutée depuis le snapshot local.", 1);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static bool TryParseRotationLine(string? value, out IReadOnlyList<string> mapCodes)
    {
        mapCodes = [];
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumRotationLineLength ||
            value.Contains('\r') || value.Contains('\n'))
        {
            return false;
        }

        var match = RotationPattern.Match(value);
        if (!match.Success)
        {
            return false;
        }

        var tokens = match.Groups["rotation"].Value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var maps = new List<string>();
        for (var index = 0; index < tokens.Length;)
        {
            var directive = tokens[index++];
            if (directive.Equals("gametype", StringComparison.OrdinalIgnoreCase))
            {
                if (index >= tokens.Length || !MapCodeValidator.TryNormalize(tokens[index++], out _))
                {
                    return false;
                }
            }
            else if (directive.Equals("map", StringComparison.OrdinalIgnoreCase))
            {
                if (index >= tokens.Length || !MapCodeValidator.TryNormalize(tokens[index++], out var code))
                {
                    return false;
                }

                if (!maps.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    maps.Add(code);
                }
            }
            else
            {
                return false;
            }
        }

        if (maps.Count is < 1 or > 128)
        {
            return false;
        }

        mapCodes = maps;
        return true;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        _entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
        _loaded = true;
    }

    private async Task<Dictionary<string, PersistedMapEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_catalogPath))
            {
                return new(StringComparer.OrdinalIgnoreCase);
            }

            var info = new FileInfo(_catalogPath);
            if (info.Length is <= 0 or > MaximumCatalogBytes)
            {
                return new(StringComparer.OrdinalIgnoreCase);
            }

            await using var stream = new FileStream(
                _catalogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync<PersistedMapCatalog>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (document?.SchemaVersion != CurrentSchemaVersion || document.Entries is null)
            {
                return new(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, PersistedMapEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in document.Entries.Take(MaximumCustomMaps))
            {
                if (!MapCodeValidator.TryNormalize(entry.Code, out var code) ||
                    !TryNormalizeDisplayName(entry.DisplayName, out var name))
                {
                    continue;
                }

                result[code] = new PersistedMapEntry
                {
                    Code = code,
                    DisplayName = OfficialMapCatalog.Contains(code) ? OfficialMapCatalog.ResolveName(code) : name,
                    IsInServerRotation = entry.IsInServerRotation,
                    IsManual = entry.IsManual && !OfficialMapCatalog.Contains(code),
                    IsObserved = entry.IsObserved && !OfficialMapCatalog.Contains(code)
                };
            }

            RemoveUnusedEntries(result);
            return result;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<bool> TrySaveAsync(
        Dictionary<string, PersistedMapEntry> entries,
        CancellationToken cancellationToken)
    {
        var temporaryPath = _catalogPath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_catalogPath)!);
            var document = new PersistedMapCatalog
            {
                SchemaVersion = CurrentSchemaVersion,
                Entries = entries.Values
                    .OrderBy(entry => entry.Code, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _catalogPath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Le fichier temporaire n'est jamais une source active et sera remplacé au prochain essai.
            }
        }
    }

    private static MapCatalogSnapshot BuildSnapshot(Dictionary<string, PersistedMapEntry> persisted)
    {
        var merged = OfficialMapCatalog.Entries.ToDictionary(
            entry => entry.Code,
            entry => entry,
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in persisted.Values)
        {
            if (merged.TryGetValue(item.Code, out var official))
            {
                merged[item.Code] = official with { IsInServerRotation = item.IsInServerRotation };
            }
            else
            {
                merged[item.Code] = new MapCatalogEntry(
                    item.Code,
                    item.DisplayName,
                    false,
                    item.IsInServerRotation,
                    item.IsManual,
                    item.IsObserved);
            }
        }

        var ordered = OfficialMapCatalog.Entries
            .Select(entry => merged[entry.Code])
            .Concat(merged.Values
                .Where(entry => !entry.IsOfficial)
                .OrderByDescending(entry => entry.IsInServerRotation)
                .ThenBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            .ToArray();
        return new MapCatalogSnapshot(ordered);
    }

    private static Dictionary<string, PersistedMapEntry> Clone(Dictionary<string, PersistedMapEntry> source) =>
        source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.OrdinalIgnoreCase);

    private static void RemoveUnusedEntries(Dictionary<string, PersistedMapEntry> entries)
    {
        foreach (var code in entries
                     .Where(pair => !pair.Value.IsInServerRotation && !pair.Value.IsManual && !pair.Value.IsObserved)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            entries.Remove(code);
        }
    }

    private static bool TryNormalizeDisplayName(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is >= 1 and <= MaximumDisplayNameLength &&
               normalized.All(character => !char.IsControl(character));
    }

    private static MapCatalogOperationResult Failure(string status, string message) =>
        new(false, status, message);

    private sealed class PersistedMapCatalog
    {
        public int SchemaVersion { get; set; }

        public List<PersistedMapEntry>? Entries { get; set; }
    }

    private sealed class PersistedMapEntry
    {
        public string Code { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsInServerRotation { get; set; }

        public bool IsManual { get; set; }

        public bool IsObserved { get; set; }

        public PersistedMapEntry Clone() => new()
        {
            Code = Code,
            DisplayName = DisplayName,
            IsInServerRotation = IsInServerRotation,
            IsManual = IsManual,
            IsObserved = IsObserved
        };
    }
}
