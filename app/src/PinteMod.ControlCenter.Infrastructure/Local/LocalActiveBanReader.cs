using System.Globalization;
using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class LocalActiveBanReader : IActiveBanReader, IDisposable
{
    private const int MaximumBans = 500;
    private readonly ReadOnlyBlockAJsonFileReader _reader = new();
    private readonly BlockALocalPathPolicy _paths;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ActiveBanSnapshot? _cached;

    public LocalActiveBanReader(LocalPinteModOptions options, IClock clock)
    {
        _paths = new BlockALocalPathPolicy(options);
        _clock = clock;
    }

    public async Task<LocalReadResult<ActiveBanSnapshot>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var source = _paths.GetSourceLabel(BlockALocalFile.ActiveBansDatabase);
            var result = await _reader.ReadAsync(
                _paths.ResolveFixed(BlockALocalFile.ActiveBansDatabase),
                Parse,
                cancellationToken).ConfigureAwait(false);
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
                    "Liste des bans actifs lue avec succès."), result.Value.UpdatedAtUtc);
            }

            if (_cached is not null)
            {
                return new(_cached, new(
                    result.Status,
                    DataFreshness.Stale,
                    Age(_cached.UpdatedAtUtc),
                    DataProvenance.MemoryCache,
                    source,
                    "Dernière liste valide — lecture actuelle indisponible."), _cached.UpdatedAtUtc);
            }

            return new(null, new(result.Status, DataFreshness.Unknown, null,
                DataProvenance.LocalFile, source, result.Message), result.LastWriteTimeUtc);
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

    private static ActiveBanSnapshot Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schema_version", out var schema) ||
            !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1 ||
            !root.TryGetProperty("updated_utc", out var updated) ||
            updated.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(updated.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var updatedAt) ||
            !root.TryGetProperty("bans", out var bans) || bans.ValueKind != JsonValueKind.Array ||
            bans.GetArrayLength() > MaximumBans)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Liste des bans invalide ou non prise en charge.");
        }

        var active = new List<ActiveBan>();
        foreach (var ban in bans.EnumerateArray())
        {
            if (ban.ValueKind != JsonValueKind.Object ||
                !TryBoolean(ban, "active", out var isActive) || !isActive)
            {
                continue;
            }

            if (!TryText(ban, "xuid", 64, out var xuid) || !XuidValidator.IsValid(xuid) ||
                !TryText(ban, "display", 80, out var display) ||
                !TryText(ban, "duration", 16, out var duration) ||
                !TryText(ban, "reason", 180, out var reason) ||
                !TryText(ban, "created_utc", 64, out var createdText) ||
                !DateTimeOffset.TryParse(createdText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var createdAt))
            {
                continue;
            }

            var expires = "Jamais";
            if (ban.TryGetProperty("expires_utc", out var expiry) && expiry.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(expiry.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expiresAt))
            {
                expires = expiresAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
            }

            active.Add(new ActiveBan(
                xuid,
                LogPrivacyFilter.SanitizeDisplayText(display, 80),
                duration,
                expires,
                LogPrivacyFilter.SanitizeDisplayText(reason, 180),
                createdAt));
        }

        return new ActiveBanSnapshot(active.OrderByDescending(item => item.CreatedAtUtc).ToArray(), updatedAt);
    }

    private static bool TryBoolean(JsonElement root, string name, out bool value)
    {
        value = false;
        if (!root.TryGetProperty(name, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryText(JsonElement root, string name, int maximum, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0 && value.Length <= maximum && value.All(character => !char.IsControl(character));
    }
}
