using System.Globalization;
using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class BanServiceStatusReader : IBanServiceStatusReader, IDisposable
{
    private readonly ReadOnlyBlockAJsonFileReader _reader = new();
    private readonly BlockALocalPathPolicy _paths;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BanServiceStatusSnapshot? _cached;

    public BanServiceStatusReader(LocalPinteModOptions options, IClock clock)
    {
        _paths = new BlockALocalPathPolicy(options);
        _clock = clock;
    }

    public async Task<LocalReadResult<BanServiceStatusSnapshot>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var source = _paths.GetSourceLabel(BlockALocalFile.BanServiceStatus);
            var result = await _reader.ReadAsync(_paths.ResolveFixed(BlockALocalFile.BanServiceStatus), Parse, cancellationToken);
            if (result.Status == LocalReadStatus.Success && result.Value is not null)
            {
                _cached = result.Value;
                var age = Age(result.Value.UpdatedAtUtc);
                return new(result.Value, new(
                    LocalReadStatus.Success,
                    HeartbeatFreshnessPolicy.Evaluate(age),
                    age,
                    DataProvenance.LocalFile,
                    source,
                    "État complémentaire du Ban Service lu avec succès."), result.Value.UpdatedAtUtc);
            }

            if (_cached is not null)
            {
                return new(_cached, new(
                    result.Status,
                    DataFreshness.Stale,
                    Age(_cached.UpdatedAtUtc),
                    DataProvenance.MemoryCache,
                    source,
                    "Dernière donnée valide — lecture actuelle indisponible."), _cached.UpdatedAtUtc);
            }

            return new(null, new(result.Status, DataFreshness.Unknown, null, DataProvenance.LocalFile, source, result.Message), result.LastWriteTimeUtc);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private TimeSpan Age(DateTimeOffset timestamp)
    {
        var age = _clock.UtcNow - timestamp;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static BanServiceStatusSnapshot Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Structure JSON invalide : racine.");
        }

        var schema = RequiredInt(root, "schema_version");
        if (schema != 1)
        {
            throw new LocalJsonValidationException(LocalReadStatus.UnsupportedSchema, $"Schéma Ban Service non pris en charge : {schema}.");
        }

        if (!root.TryGetProperty("running", out var running) || running.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Champ requis invalide : running.");
        }

        var activeBans = RequiredInt(root, "active_bans");
        if (activeBans < 0)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "active_bans doit être positif ou nul.");
        }

        return new BanServiceStatusSnapshot(
            schema,
            RequiredText(root, "version", 32),
            running.GetBoolean(),
            RequiredDate(root, "updated_utc"),
            activeBans,
            RequiredText(root, "privacy", 64));
    }

    private static int RequiredInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {name}.");

    private static string RequiredText(JsonElement root, string name, int maximum) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString()) && value.GetString()!.Length <= maximum
            ? value.GetString()!.Trim()
            : throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {name}.");

    private static DateTimeOffset RequiredDate(JsonElement root, string name)
    {
        var text = RequiredText(root, name, 64);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? value
            : throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Date UTC invalide : {name}.");
    }
}
