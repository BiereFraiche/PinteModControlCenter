using System.Globalization;
using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class InstallationVerificationReader : IInstallationVerificationReader, IDisposable
{
    private static readonly TimeSpan RecentReportWindow = TimeSpan.FromHours(24);
    private readonly ReadOnlyBlockAJsonFileReader _reader = new();
    private readonly BlockALocalPathPolicy _paths;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InstallationVerificationReport? _cached;
    private DateTimeOffset? _cachedTimestamp;

    public InstallationVerificationReader(LocalPinteModOptions options, IClock clock)
    {
        _paths = new BlockALocalPathPolicy(options);
        _clock = clock;
    }

    public async Task<LocalReadResult<InstallationVerificationReport>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var source = _paths.GetSourceLabel(BlockALocalFile.InstallationVerification);
            var result = await _reader.ReadAsync(_paths.ResolveFixed(BlockALocalFile.InstallationVerification), Parse, cancellationToken);
            if (result.Status == LocalReadStatus.Success && result.Value is not null)
            {
                _cached = result.Value;
                _cachedTimestamp = result.LastWriteTimeUtc;
                var age = Age(result.Value.CheckedAtUtc);
                return new(result.Value, new(
                    LocalReadStatus.Success,
                    age <= RecentReportWindow ? DataFreshness.Fresh : DataFreshness.Stale,
                    age,
                    DataProvenance.LocalFile,
                    source,
                    age <= RecentReportWindow
                        ? "Rapport d’installation local lu avec succès."
                        : "Rapport d’installation valide — rapport ancien."), result.Value.CheckedAtUtc);
            }

            if (_cached is not null)
            {
                return new(_cached, new(
                    result.Status,
                    DataFreshness.Stale,
                    Age(_cached.CheckedAtUtc),
                    DataProvenance.MemoryCache,
                    source,
                    "Dernière donnée valide — lecture actuelle indisponible."), _cachedTimestamp);
            }

            return new(null, new(
                result.Status,
                DataFreshness.Unknown,
                null,
                DataProvenance.LocalFile,
                source,
                result.Status == LocalReadStatus.Missing
                    ? "Vérification non exécutée."
                    : result.Message), result.LastWriteTimeUtc);
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

    private static InstallationVerificationReport Parse(JsonElement root)
    {
        RequireObject(root, "racine");
        var schema = RequiredInt(root, "schema_version");
        if (schema != 1)
        {
            throw new LocalJsonValidationException(LocalReadStatus.UnsupportedSchema, $"Schéma de vérification non pris en charge : {schema}.");
        }

        var checkedAt = RequiredDate(root, "checked_utc");
        var checks = new List<InstallationVerificationCheck>();
        if (root.TryGetProperty("results", out var results) &&
            results.ValueKind == JsonValueKind.Object &&
            results.TryGetProperty("value", out var values) &&
            values.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in values.EnumerateArray().Take(256))
            {
                RequireObject(item, "entrée de contrôle");
                var status = RequiredText(item, "status", 24).ToUpperInvariant();
                if (status is not ("PASS" or "WARNING" or "ERROR"))
                {
                    throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Statut de contrôle d’installation invalide.");
                }

                checks.Add(new InstallationVerificationCheck(
                    LogPrivacyFilter.SanitizeDisplayText(RequiredText(item, "check", 160), 160),
                    status,
                    item.TryGetProperty("recommendation", out var recommendation) && recommendation.ValueKind == JsonValueKind.String
                        ? LogPrivacyFilter.SanitizeDisplayText(recommendation.GetString(), 240)
                        : string.Empty));
            }
        }

        var pass = RequiredInt(root, "pass");
        var warning = RequiredInt(root, "warning");
        var error = RequiredInt(root, "error");
        if (pass < 0 || warning < 0 || error < 0 || pass + warning + error != checks.Count)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "Compteurs de vérification incohérents.");
        }

        return new InstallationVerificationReport(
            schema,
            RequiredText(root, "tool", 80),
            RequiredText(root, "version", 32),
            checkedAt,
            pass,
            warning,
            error,
            checks);
    }

    private static int RequiredInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetInt32(out var parsed))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {name}.");
        }

        return parsed;
    }

    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Structure JSON invalide : {label}.");
        }
    }

    private static string RequiredText(JsonElement root, string name, int maximum)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > maximum)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {name}.");
        }

        return value.GetString()!.Trim();
    }

    private static DateTimeOffset RequiredDate(JsonElement root, string name)
    {
        var text = RequiredText(root, name, 64);
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Date UTC invalide : {name}.");
        }

        return value;
    }
}
