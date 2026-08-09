using System.Globalization;
using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class ServiceHeartbeatReader : IServiceHeartbeatReader, IDisposable
{
    private readonly ReadOnlyJsonFileReader _fileReader;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<LocalServiceKind, CachedHeartbeat> _lastValid = [];
    private readonly Dictionary<LocalServiceKind, int> _consecutiveFailures = [];

    public ServiceHeartbeatReader(LocalPinteModOptions options, IClock clock)
    {
        _fileReader = new ReadOnlyJsonFileReader(options);
        _clock = clock;
    }

    public async Task<LocalReadResult<ServiceHeartbeat>> ReadAsync(
        LocalServiceKind service,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var file = GetFile(service);
            var result = await _fileReader.ReadAsync(file, root => Parse(root, service), cancellationToken);
            if (result.Status == LocalReadStatus.Success && result.Value is not null)
            {
                var age = CalculateHeartbeatAge(result.Value.UpdatedAtUtc);
                _lastValid[service] = new CachedHeartbeat(result.Value, result.LastWriteTimeUtc);
                _consecutiveFailures[service] = 0;
                return new LocalReadResult<ServiceHeartbeat>(
                    result.Value,
                    new LocalSourceMetadata(
                        LocalReadStatus.Success,
                        HeartbeatFreshnessPolicy.Evaluate(age),
                        age,
                        DataProvenance.LocalFile,
                        LocalPinteModOptions.GetSourceLabel(file),
                        "Heartbeat local lu avec succès."),
                    result.Value.UpdatedAtUtc);
            }

            var failures = _consecutiveFailures.GetValueOrDefault(service) + 1;
            _consecutiveFailures[service] = failures;
            var durable = IsPotentialReadError(result.Status) && failures >= 3;

            if (_lastValid.TryGetValue(service, out var cached))
            {
                var age = CalculateHeartbeatAge(cached.Value.UpdatedAtUtc);
                var freshness = HeartbeatFreshnessPolicy.Evaluate(age);
                var message = freshness == DataFreshness.Expired
                    ? "Dernière donnée valide — périmée."
                    : "Dernière donnée valide — lecture actuelle indisponible.";
                return new LocalReadResult<ServiceHeartbeat>(
                    cached.Value,
                    new LocalSourceMetadata(
                        result.Status,
                        freshness,
                        age,
                        DataProvenance.MemoryCache,
                        LocalPinteModOptions.GetSourceLabel(file),
                        message,
                        failures,
                        durable),
                    cached.Value.UpdatedAtUtc);
            }

            return new LocalReadResult<ServiceHeartbeat>(
                null,
                new LocalSourceMetadata(
                    result.Status,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    LocalPinteModOptions.GetSourceLabel(file),
                    result.Message,
                    failures,
                    durable),
                result.LastWriteTimeUtc);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private TimeSpan CalculateHeartbeatAge(DateTimeOffset updatedAtUtc)
    {
        var age = _clock.UtcNow - updatedAtUtc;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private ServiceHeartbeat Parse(JsonElement root, LocalServiceKind expectedService)
    {
        var schemaVersion = GetRequiredInt64(root, "schema_version");
        if (schemaVersion != 1)
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.UnsupportedSchema,
                $"Schéma heartbeat non pris en charge : {schemaVersion}.");
        }

        var tool = GetRequiredString(root, "tool");
        var expectedTool = GetExpectedTool(expectedService);
        if (!string.Equals(tool, expectedTool, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.Invalid,
                $"Heartbeat inattendu : {tool}; attendu : {expectedTool}.");
        }

        var updatedText = GetRequiredString(root, "updated_utc");
        if (!DateTimeOffset.TryParse(
                updatedText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var updatedAtUtc))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "updated_utc n’est pas une date UTC valide.");
        }

        if (updatedAtUtc - _clock.UtcNow > HeartbeatFreshnessPolicy.FutureTimestampTolerance)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "updated_utc est anormalement situé dans le futur.");
        }

        var rawState = GetRequiredString(root, "state");
        var version = GetRequiredString(root, "version");
        var sequence = GetRequiredInt64(root, "sequence");
        if (sequence < 0)
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, "sequence doit être positive ou nulle.");
        }

        return new ServiceHeartbeat(
            (int)schemaVersion,
            expectedService,
            tool,
            version,
            rawState,
            MapDeclaredState(rawState),
            sequence,
            updatedAtUtc,
            GetOptionalString(root, "last_error_code"));
    }

    public static ServiceDeclaredState MapDeclaredState(string rawState) =>
        rawState.Trim().ToLowerInvariant() switch
        {
            "running" => ServiceDeclaredState.Running,
            "monitoring" => ServiceDeclaredState.Monitoring,
            "connected" => ServiceDeclaredState.Connected,
            "active" => ServiceDeclaredState.Active,
            "paused" => ServiceDeclaredState.Paused,
            "configured" => ServiceDeclaredState.Configured,
            "stopped" => ServiceDeclaredState.Stopped,
            "error" or "failed" or "faulted" => ServiceDeclaredState.Error,
            _ => ServiceDeclaredState.Unknown
        };

    private static bool IsPotentialReadError(LocalReadStatus status) =>
        status is LocalReadStatus.Empty or
            LocalReadStatus.Invalid or
            LocalReadStatus.UnsupportedSchema or
            LocalReadStatus.AccessDenied or
            LocalReadStatus.IoError;

    private static LocalPinteModFile GetFile(LocalServiceKind service) =>
        service switch
        {
            LocalServiceKind.Supervisor => LocalPinteModFile.SupervisorHeartbeat,
            LocalServiceKind.BanService => LocalPinteModFile.BanServiceHeartbeat,
            LocalServiceKind.GeoIpBridge => LocalPinteModFile.GeoIpBridgeHeartbeat,
            LocalServiceKind.LiveConsole => LocalPinteModFile.LiveConsoleHeartbeat,
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Service local non autorisé.")
        };

    private static string GetExpectedTool(LocalServiceKind service) =>
        service switch
        {
            LocalServiceKind.Supervisor => "supervisor",
            LocalServiceKind.BanService => "ban_service",
            LocalServiceKind.GeoIpBridge => "geoip_bridge",
            LocalServiceKind.LiveConsole => "live_console",
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Service local non autorisé.")
        };

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

    private static string? GetOptionalString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static long GetRequiredInt64(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || !property.TryGetInt64(out var value))
        {
            throw new LocalJsonValidationException(LocalReadStatus.Invalid, $"Champ requis invalide : {propertyName}.");
        }

        return value;
    }

    private sealed record CachedHeartbeat(ServiceHeartbeat Value, DateTimeOffset? FileTimestampUtc);
}
