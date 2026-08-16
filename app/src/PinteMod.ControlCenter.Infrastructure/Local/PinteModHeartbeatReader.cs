using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class PinteModHeartbeatReader : IPinteModHeartbeatReader, IDisposable
{
    internal const int MaximumFileSizeBytes = 4096;
    private readonly ReadOnlyJsonFileReader _fileReader;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CachedValue? _cached;
    private string? _activeSessionId;
    private int _consecutiveFailures;

    public PinteModHeartbeatReader(LocalPinteModOptions options, IClock clock)
        : this(options, clock, null)
    {
    }

    internal PinteModHeartbeatReader(
        LocalPinteModOptions options,
        IClock clock,
        Action<string>? afterReadBeforeVerification)
    {
        _fileReader = new ReadOnlyJsonFileReader(options, afterReadBeforeVerification);
        _clock = clock;
    }

    public async Task<LocalReadResult<PinteModHeartbeatSnapshot>> ReadAsync(
        string? activeSessionId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!IsSafeSessionId(activeSessionId))
            {
                ResetForSession(null);
                return Unavailable("Aucune session locale active vérifiée.");
            }

            ResetForSession(activeSessionId);
            var result = await _fileReader.ReadAsync(
                LocalPinteModFile.PinteModHeartbeat,
                Parse,
                MaximumFileSizeBytes,
                cancellationToken);

            if (result.Status == LocalReadStatus.Success && result.Value is not null)
            {
                if (!string.Equals(result.Value.SessionId, activeSessionId, StringComparison.Ordinal))
                {
                    _cached = null;
                    return InvalidSession(result.LastWriteTimeUtc);
                }

                if (!TryFileAge(result.LastWriteTimeUtc, out var age))
                {
                    return Failure(
                        new LocalJsonFileResult<PinteModHeartbeatSnapshot>(
                            null,
                            LocalReadStatus.Invalid,
                            result.LastWriteTimeUtc,
                            "Horodatage du fichier invalide."));
                }

                _cached = new CachedValue(result.Value, result.LastWriteTimeUtc!.Value);
                _consecutiveFailures = 0;
                return new(
                    result.Value,
                    Metadata(
                        LocalReadStatus.Success,
                        HeartbeatFreshnessPolicy.Evaluate(age),
                        age,
                        DataProvenance.LocalFile,
                        "Heartbeat PinteMod local lu avec succès."),
                    result.LastWriteTimeUtc);
            }

            return Failure(result);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private LocalReadResult<PinteModHeartbeatSnapshot> Failure(
        LocalJsonFileResult<PinteModHeartbeatSnapshot> result)
    {
        _consecutiveFailures++;
        var durable = IsPotentialReadError(result.Status) && _consecutiveFailures >= 3;
        if (_cached is not null)
        {
            var age = CalculateAge(_cached.FileTimestampUtc);
            var freshness = HeartbeatFreshnessPolicy.Evaluate(age);
            return new(
                _cached.Value,
                Metadata(
                    result.Status,
                    freshness,
                    age,
                    DataProvenance.MemoryCache,
                    freshness == DataFreshness.Expired
                        ? "Dernière donnée valide — périmée."
                        : "Dernière donnée valide — lecture actuelle indisponible.",
                    _consecutiveFailures,
                    durable),
                _cached.FileTimestampUtc);
        }

        return new(
            null,
            Metadata(
                result.Status,
                DataFreshness.Unknown,
                null,
                DataProvenance.LocalFile,
                result.Message,
                _consecutiveFailures,
                durable),
            result.LastWriteTimeUtc);
    }

    private static PinteModHeartbeatSnapshot Parse(JsonElement root)
    {
        RuntimeJsonContract.RequireObject(root);
        var schema = RuntimeJsonContract.RequiredInt32(root, "schema_version", 1, int.MaxValue);
        if (schema != 1)
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.UnsupportedSchema,
                "Schéma heartbeat PinteMod non pris en charge.");
        }

        var rawState = RuntimeJsonContract.RequiredString(root, "declared_state", 16);
        var declaredState = rawState switch
        {
            "running" => ServiceDeclaredState.Running,
            "stopped" => ServiceDeclaredState.Stopped,
            "error" => ServiceDeclaredState.Error,
            _ => throw RuntimeJsonContract.Invalid("État déclaré PinteMod inconnu.")
        };
        var timeAuthority = RuntimeJsonContract.RequiredString(root, "time_authority", 64);
        if (!string.Equals(timeAuthority, RuntimeJsonContract.TimeAuthority, StringComparison.Ordinal))
        {
            throw RuntimeJsonContract.Invalid("Autorité temporelle PinteMod inconnue.");
        }

        return new(
            schema,
            RuntimeJsonContract.RequiredString(root, "module_version", 32, RuntimeJsonContract.IsSafeIdentifier),
            RuntimeJsonContract.RequiredString(root, "session_id", 128, IsSafeSessionId),
            declaredState,
            RuntimeJsonContract.OptionalString(root, "last_error_code", 64, RuntimeJsonContract.IsSafeIdentifier),
            RuntimeJsonContract.RequiredInt64(root, "sequence", 0, long.MaxValue),
            RuntimeJsonContract.RequiredInt64(root, "generated_gettime", 0, int.MaxValue),
            RuntimeJsonContract.OptionalUtc(root, "updated_at_utc"),
            timeAuthority);
    }

    private void ResetForSession(string? sessionId)
    {
        if (string.Equals(_activeSessionId, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        _activeSessionId = sessionId;
        _cached = null;
        _consecutiveFailures = 0;
    }

    private LocalReadResult<PinteModHeartbeatSnapshot> InvalidSession(DateTimeOffset? timestamp) =>
        new(
            null,
            Metadata(
                LocalReadStatus.Invalid,
                DataFreshness.Unknown,
                null,
                DataProvenance.LocalFile,
                "Heartbeat PinteMod lié à une autre session."),
            timestamp);

    private LocalReadResult<PinteModHeartbeatSnapshot> Unavailable(string message) =>
        new(null, LocalSourceMetadata.Unavailable(message) with
        {
            SourceLabel = LocalPinteModOptions.GetSourceLabel(LocalPinteModFile.PinteModHeartbeat)
        }, null);

    private LocalSourceMetadata Metadata(
        LocalReadStatus status,
        DataFreshness freshness,
        TimeSpan? age,
        DataProvenance provenance,
        string message,
        int failures = 0,
        bool durable = false) =>
        new(
            status,
            freshness,
            age,
            provenance,
            LocalPinteModOptions.GetSourceLabel(LocalPinteModFile.PinteModHeartbeat),
            message,
            failures,
            durable);

    private bool TryFileAge(DateTimeOffset? timestamp, out TimeSpan age)
    {
        if (timestamp is null || timestamp.Value - _clock.UtcNow > HeartbeatFreshnessPolicy.FutureTimestampTolerance)
        {
            age = TimeSpan.Zero;
            return false;
        }

        age = CalculateAge(timestamp.Value);
        return true;
    }

    private TimeSpan CalculateAge(DateTimeOffset timestamp)
    {
        var age = _clock.UtcNow - timestamp;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static bool IsSafeSessionId(string? value) =>
        value is { Length: >= 1 and <= 128 } && RuntimeJsonContract.IsSafeIdentifier(value);

    private static bool IsPotentialReadError(LocalReadStatus status) =>
        status is LocalReadStatus.Empty or LocalReadStatus.Invalid or LocalReadStatus.UnsupportedSchema or
            LocalReadStatus.AccessDenied or LocalReadStatus.IoError;

    private sealed record CachedValue(PinteModHeartbeatSnapshot Value, DateTimeOffset FileTimestampUtc);
}
