using System.Text.Json;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class ControlCenterContractReader : IControlCenterContractReader, IDisposable
{
    internal const int CapabilitiesMaximumFileSizeBytes = 16 * 1024;
    internal const int FeedbackMaximumFileSizeBytes = 4 * 1024;
    internal const int TransitionMaximumFileSizeBytes = 4 * 1024;
    internal const int IdentityMaximumFileSizeBytes = 4 * 1024;
    private const long MaximumTwelveDigitValue = 999_999_999_999;
    private static readonly Regex DynamicCapabilityName = new(
        "^(?:map_[1-9][0-9]*_(?:code|display_name|availability)|boss_[1-9][0-9]*_alias|power_up_[1-9][0-9]*_alias|diagnostic_[1-9][0-9]*_alias)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> FeedbackResultCodes = new(StringComparer.Ordinal)
    {
        "accepted", "success", "invalid_request_id", "duplicate_request", "invalid_arguments",
        "map_not_allowed", "transition_in_progress", "transition_state_write_failed",
        "result_map_mismatch", "unsupported_on_map", "invalid_target_xuid",
        "target_not_connected", "events_disabled", "boss_limit_reached", "invalid_position",
        "spawn_failed", "invalid_hostname", "hostname_not_applied", "password_not_cleared"
        , "invalid_join_password", "password_not_applied", "hostname_persist_failed"
    };
    private static readonly HashSet<string> DiagnosticAliases = new(StringComparer.Ordinal)
    {
        "map_audit", "event_status", "power_ups"
    };

    private readonly ReadOnlyJsonFileReader _fileReader;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Cache<ControlCenterCapabilitiesSnapshot>? _capabilitiesCache;
    private Cache<ControlCenterActionFeedbackSnapshot>? _feedbackCache;
    private Cache<ControlCenterMapTransitionSnapshot>? _transitionCache;
    private Cache<ControlCenterServerIdentitySnapshot>? _identityCache;
    private string? _activeSessionId;

    public ControlCenterContractReader(LocalPinteModOptions options, IClock clock)
        : this(options, clock, null, null)
    {
    }

    internal ControlCenterContractReader(
        LocalPinteModOptions options,
        IClock clock,
        Action<string>? afterReadBeforeVerification,
        Action<string>? afterMetadataBeforeRead)
    {
        _fileReader = new ReadOnlyJsonFileReader(
            options,
            afterReadBeforeVerification,
            afterMetadataBeforeRead);
        _clock = clock;
    }

    public async Task<ControlCenterContractSnapshot> ReadAsync(
        string? activeSessionId,
        string? activeMapCode,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsSafeSessionId(activeSessionId) ||
                !MapCodeValidator.TryNormalize(activeMapCode, out var normalizedMap))
            {
                ResetForSession(null);
                return Unavailable("Aucune session locale active vérifiée.");
            }

            ResetForSession(activeSessionId);
            var capabilitiesTask = _fileReader.ReadAsync(
                LocalPinteModFile.ControlCenterCapabilities,
                ParseCapabilities,
                CapabilitiesMaximumFileSizeBytes,
                cancellationToken);
            var feedbackTask = _fileReader.ReadAsync(
                LocalPinteModFile.ControlCenterActionFeedback,
                ParseFeedback,
                FeedbackMaximumFileSizeBytes,
                cancellationToken);
            var transitionTask = _fileReader.ReadAsync(
                LocalPinteModFile.ControlCenterMapTransition,
                ParseTransition,
                TransitionMaximumFileSizeBytes,
                cancellationToken);
            var identityTask = _fileReader.ReadAsync(
                LocalPinteModFile.ControlCenterServerIdentity,
                ParseIdentity,
                IdentityMaximumFileSizeBytes,
                cancellationToken);
            await Task.WhenAll(capabilitiesTask, feedbackTask, transitionTask, identityTask)
                .ConfigureAwait(false);

            return new(
                Normalize(
                    await capabilitiesTask.ConfigureAwait(false),
                    LocalPinteModFile.ControlCenterCapabilities,
                    value => string.Equals(value.SessionId, activeSessionId, StringComparison.Ordinal) &&
                             string.Equals(value.MapCode, normalizedMap, StringComparison.Ordinal),
                    ref _capabilitiesCache,
                    "Capabilities Control Center"),
                Normalize(
                    await feedbackTask.ConfigureAwait(false),
                    LocalPinteModFile.ControlCenterActionFeedback,
                    value => string.Equals(value.SessionId, activeSessionId, StringComparison.Ordinal),
                    ref _feedbackCache,
                    "Feedback Control Center"),
                Normalize(
                    await transitionTask.ConfigureAwait(false),
                    LocalPinteModFile.ControlCenterMapTransition,
                    value => string.Equals(value.OriginatingSessionId, activeSessionId, StringComparison.Ordinal) ||
                             string.Equals(value.ResultingSessionId, activeSessionId, StringComparison.Ordinal),
                    ref _transitionCache,
                    "Transition Control Center"),
                Normalize(
                    await identityTask.ConfigureAwait(false),
                    LocalPinteModFile.ControlCenterServerIdentity,
                    value => string.Equals(value.SessionId, activeSessionId, StringComparison.Ordinal),
                    ref _identityCache,
                    "Identité serveur Control Center"));
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private LocalReadResult<T> Normalize<T>(
        LocalJsonFileResult<T> result,
        LocalPinteModFile file,
        Func<T, bool> belongsToActiveSession,
        ref Cache<T>? cache,
        string displayName)
        where T : class
    {
        if (result.Status == LocalReadStatus.Success && result.Value is not null)
        {
            if (!belongsToActiveSession(result.Value))
            {
                cache = null;
                return Result<T>(
                    file,
                    null,
                    LocalReadStatus.Invalid,
                    DataFreshness.Unknown,
                    DataProvenance.LocalFile,
                    result.LastWriteTimeUtc,
                    $"{displayName} lié à une autre session ou carte.");
            }

            if (!TryAge(result.LastWriteTimeUtc, out var age))
            {
                return Failure(
                    file,
                    new LocalJsonFileResult<T>(null, LocalReadStatus.Invalid, result.LastWriteTimeUtc,
                        "Horodatage du fichier invalide."),
                    ref cache,
                    displayName);
            }

            cache = new(result.Value, result.LastWriteTimeUtc!.Value);
            return Result(
                file,
                result.Value,
                LocalReadStatus.Success,
                HeartbeatFreshnessPolicy.Evaluate(age),
                DataProvenance.LocalFile,
                result.LastWriteTimeUtc,
                $"{displayName} lu avec succès.",
                age);
        }

        return Failure(file, result, ref cache, displayName);
    }

    private LocalReadResult<T> Failure<T>(
        LocalPinteModFile file,
        LocalJsonFileResult<T> failure,
        ref Cache<T>? cache,
        string displayName)
        where T : class
    {
        if (cache is null)
        {
            return Result<T>(
                file,
                null,
                failure.Status,
                DataFreshness.Unknown,
                DataProvenance.LocalFile,
                failure.LastWriteTimeUtc,
                failure.Message);
        }

        var age = Age(cache.FileTimestampUtc);
        var freshness = HeartbeatFreshnessPolicy.Evaluate(age);
        return Result(
            file,
            cache.Value,
            failure.Status,
            freshness,
            DataProvenance.MemoryCache,
            cache.FileTimestampUtc,
            freshness == DataFreshness.Expired
                ? $"Dernière donnée valide {displayName} — périmée."
                : $"Dernière donnée valide {displayName} — lecture actuelle indisponible.",
            age);
    }

    private static LocalReadResult<T> Result<T>(
        LocalPinteModFile file,
        T? value,
        LocalReadStatus status,
        DataFreshness freshness,
        DataProvenance provenance,
        DateTimeOffset? timestamp,
        string message,
        TimeSpan? age = null)
        where T : class =>
        new(value, new(
            status,
            freshness,
            age,
            provenance,
            LocalPinteModOptions.GetSourceLabel(file),
            message), timestamp);

    private static ControlCenterCapabilitiesSnapshot ParseCapabilities(JsonElement root)
    {
        RequireClosedObject(root, CapabilityPropertyNames, DynamicCapabilityName.IsMatch);
        RequireSchema(root);
        var contractModuleVersion = RequiredEnum(root, "contract_module_version", "0.1.3", "0.1.4");
        RequireIntegerExact(root, "command_contract_version", 1);
        RequireExact(root, "updated_at_utc", string.Empty);
        RequireExact(root, "time_authority", RuntimeJsonContract.TimeAuthority);
        RequireExact(root, "map_source", "runtime");
        RequireExact(root, "map_installation_authority", "unknown");
        RequireExact(root, "rotation_state", "unknown");
        RequireIntegerExact(root, "rotation_entry_count", 0);
        RequireBooleanExact(root, "change_map", false);
        RequireExact(root, "join_password_transport", "loopback_rcon_ephemeral");

        var mapCount = RequiredCount(root, "map_count", 64);
        var bossCount = RequiredCount(root, "boss_count", 64);
        var powerUpCount = RequiredCount(root, "power_up_count", 64);
        var diagnosticCount = RequiredCount(root, "diagnostic_count", 3);
        _ = RequiredCount(root, "event_count", 64);
        var maps = Enumerable.Range(1, mapCount)
            .Select(index => new SupportedMapCapability(
                RequiredMapCode(root, $"map_{index}_code"),
                RequiredDisplayString(root, $"map_{index}_display_name", 64)))
            .ToArray();
        for (var index = 1; index <= mapCount; index++)
        {
            RequireExact(root, $"map_{index}_availability", "supported");
        }

        EnsureUnique(maps.Select(map => map.Code), "Map supported dupliquée.");
        var bosses = ReadAliases(root, "boss", bossCount);
        var powerUps = ReadAliases(root, "power_up", powerUpCount);
        var diagnostics = ReadAliases(root, "diagnostic", diagnosticCount);
        if (diagnostics.Any(alias => !DiagnosticAliases.Contains(alias)))
        {
            throw RuntimeJsonContract.Invalid("Alias de diagnostic inconnu.");
        }
        EnsureDynamicCapabilityProperties(
            root,
            mapCount,
            bossCount,
            powerUpCount,
            diagnosticCount);

        return new(
            RequiredIdentifier(root, "module_version", 32),
            contractModuleVersion,
            RequiredSessionId(root, "session_id"),
            RequiredTwelveDigitNumber(root, "sequence"),
            RequiredTwelveDigitNumber(root, "generated_gettime"),
            RequiredMapCode(root, "map_code"),
            RequiredBoolean(root, "restart_map"),
            maps,
            bosses,
            powerUps,
            diagnostics,
            RequiredEnum(root, "transition_state", "idle", "accepted", "transitioning", "active", "failed"),
            RequiredBoolean(root, "set_hostname"),
            RequiredBoolean(root, "set_join_password"),
            RequiredBoolean(root, "clear_join_password"),
            RequiredDisplayString(root, "map_profile", 48),
            RequiredDisplayString(root, "power_support", 48),
            RequiredDisplayString(root, "pack_a_punch_support", 48),
            RequiredDisplayString(root, "event_support", 48),
            RequiredDisplayString(root, "boss_support", 48),
            RequiredDisplayString(root, "music_support", 48),
            RequiredDisplayString(root, "dog_round_support", 48),
            RequiredCount(root, "active_pintemod_bosses", 1000),
            RequiredCount(root, "max_pintemod_bosses", 1000));
    }

    private static ControlCenterActionFeedbackSnapshot ParseFeedback(JsonElement root)
    {
        RequireClosedObject(root, FeedbackPropertyNames);
        RequireSchema(root);
        RequireExact(root, "updated_at_utc", string.Empty);
        RequireExact(root, "time_authority", RuntimeJsonContract.TimeAuthority);
        var resultCode = RequiredIdentifier(root, "result_code", 64);
        if (!FeedbackResultCodes.Contains(resultCode))
        {
            throw RuntimeJsonContract.Invalid("Code de résultat Control Center inconnu.");
        }

        return new(
            RequiredSessionId(root, "session_id"),
            RequiredTwelveDigitNumber(root, "sequence"),
            RequiredTwelveDigitNumber(root, "generated_gettime"),
            RequiredRequestId(root, "request_id"),
            ParseAction(RequiredIdentifier(root, "action", 32)),
            ParseFeedbackStatus(RequiredIdentifier(root, "status", 16)),
            resultCode);
    }

    private static ControlCenterMapTransitionSnapshot ParseTransition(JsonElement root)
    {
        RequireClosedObject(root, TransitionPropertyNames);
        RequireSchema(root);
        RequireExact(root, "action", "restart_map");
        RequireExact(root, "updated_at_utc", string.Empty);
        RequireExact(root, "time_authority", RuntimeJsonContract.TimeAuthority);
        var status = ParseTransitionStatus(RequiredIdentifier(root, "status", 16));
        var resultCode = RequiredEnum(
            root,
            "result_code",
            "accepted",
            "transition_started",
            "success",
            "result_map_mismatch");
        var resultingSession = OptionalSessionId(root, "resulting_session_id");
        if (status is ControlCenterTransitionStatus.Active or ControlCenterTransitionStatus.Failed &&
            resultingSession is null)
        {
            throw RuntimeJsonContract.Invalid("Session résultante de transition absente.");
        }

        return new(
            RequiredRequestId(root, "request_id"),
            RequiredMapCode(root, "requested_map"),
            RequiredSessionId(root, "originating_session_id"),
            status,
            resultCode,
            RequiredTwelveDigitNumber(root, "generated_gettime"),
            resultingSession);
    }

    private static ControlCenterServerIdentitySnapshot ParseIdentity(JsonElement root)
    {
        RequireClosedObject(root, IdentityPropertyNames);
        RequireSchema(root);
        RequireExact(root, "updated_at_utc", string.Empty);
        RequireExact(root, "time_authority", RuntimeJsonContract.TimeAuthority);
        var hostname = RequiredStringAllowEmpty(root, "public_hostname", 96);
        if (hostname.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not (' ' or '.' or '_' or '[' or ']' or '(' or ')' or '-')))
        {
            throw RuntimeJsonContract.Invalid("Hostname public observé invalide.");
        }

        var state = RequiredEnum(root, "public_hostname_state", "observed", "neutralized", "empty") switch
        {
            "observed" => PublicHostnameState.Observed,
            "neutralized" => PublicHostnameState.Neutralized,
            _ => PublicHostnameState.Empty
        };
        if ((state == PublicHostnameState.Empty) != (hostname.Length == 0))
        {
            throw RuntimeJsonContract.Invalid("État du hostname public incohérent.");
        }

        return new(
            RequiredSessionId(root, "session_id"),
            RequiredTwelveDigitNumber(root, "sequence"),
            RequiredTwelveDigitNumber(root, "generated_gettime"),
            hostname,
            state,
            RequiredBoolean(root, "join_password_enabled"),
            RequiredInteger(root, "revision", 1, MaximumTwelveDigitValue));
    }

    private static void RequireSchema(JsonElement root) => RequireIntegerExact(root, "schema_version", 1);

    private static void RequireClosedObject(
        JsonElement root,
        IReadOnlySet<string> fixedNames,
        Func<string, bool>? dynamicName = null)
    {
        RuntimeJsonContract.RequireObject(root);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name) ||
                !fixedNames.Contains(property.Name) && dynamicName?.Invoke(property.Name) != true)
            {
                throw RuntimeJsonContract.Invalid("Propriété JSON Control Center inconnue ou dupliquée.");
            }
        }
    }

    private static void RequireExact(JsonElement root, string name, string expected)
    {
        if (!root.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            !string.Equals(property.GetString(), expected, StringComparison.Ordinal))
        {
            throw RuntimeJsonContract.Invalid($"Valeur contractuelle invalide : {name}.");
        }
    }

    private static void RequireIntegerExact(JsonElement root, string name, long expected)
    {
        if (RequiredInteger(root, name, expected, expected) != expected)
        {
            throw RuntimeJsonContract.Invalid($"Valeur contractuelle invalide : {name}.");
        }
    }

    private static void RequireBooleanExact(JsonElement root, string name, bool expected)
    {
        if (RequiredBoolean(root, name) != expected)
        {
            throw RuntimeJsonContract.Invalid($"Valeur contractuelle invalide : {name}.");
        }
    }

    private static string RequiredStringAllowEmpty(JsonElement root, string name, int maximumLength)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw RuntimeJsonContract.Invalid($"Champ requis invalide : {name}.");
        }

        var value = property.GetString() ?? string.Empty;
        if (value.Length > maximumLength)
        {
            throw RuntimeJsonContract.Invalid($"Champ requis invalide : {name}.");
        }

        return value;
    }

    private static string RequiredDisplayString(JsonElement root, string name, int maximumLength) =>
        RuntimeJsonContract.RequiredString(root, name, maximumLength, value =>
            value.All(character => !char.IsControl(character)));

    private static string RequiredIdentifier(JsonElement root, string name, int maximumLength) =>
        RuntimeJsonContract.RequiredString(root, name, maximumLength, RuntimeJsonContract.IsSafeIdentifier);

    private static string RequiredSessionId(JsonElement root, string name) =>
        RuntimeJsonContract.RequiredString(root, name, 96, IsSafeSessionId);

    private static string? OptionalSessionId(JsonElement root, string name) =>
        RuntimeJsonContract.OptionalString(root, name, 96, IsSafeSessionId);

    private static string RequiredRequestId(JsonElement root, string name)
    {
        var value = RuntimeJsonContract.RequiredString(root, name, 32, candidate =>
            candidate.Length >= 8 && candidate.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'));
        return value;
    }

    private static string RequiredMapCode(JsonElement root, string name) =>
        RuntimeJsonContract.RequiredString(root, name, 32, value =>
            value.StartsWith("zm_", StringComparison.Ordinal) &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'));

    private static long RequiredTwelveDigitNumber(JsonElement root, string name) =>
        RequiredInteger(root, name, 0, MaximumTwelveDigitValue);

    private static int RequiredCount(JsonElement root, string name, int maximum) =>
        checked((int)RequiredInteger(root, name, 0, maximum));

    private static long RequiredInteger(JsonElement root, string name, long minimum, long maximum)
    {
        if (!root.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt64(out var value) ||
            value < minimum ||
            value > maximum)
        {
            throw RuntimeJsonContract.Invalid($"Champ numérique natif invalide : {name}.");
        }

        return value;
    }

    private static bool RequiredBoolean(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw RuntimeJsonContract.Invalid($"Champ booléen natif invalide : {name}.");
        }

        return property.GetBoolean();
    }

    private static string RequiredEnum(JsonElement root, string name, params string[] allowed)
    {
        var value = RuntimeJsonContract.RequiredString(root, name, 64);
        if (!allowed.Contains(value, StringComparer.Ordinal))
        {
            throw RuntimeJsonContract.Invalid($"Valeur contractuelle inconnue : {name}.");
        }

        return value;
    }

    private static IReadOnlyList<string> ReadAliases(JsonElement root, string prefix, int count)
    {
        var aliases = Enumerable.Range(1, count)
            .Select(index => RuntimeJsonContract.RequiredString(
                root,
                $"{prefix}_{index}_alias",
                32,
                value => value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')))
            .ToArray();
        EnsureUnique(aliases, "Alias Control Center dupliqué.");
        return aliases;
    }

    private static void EnsureUnique(IEnumerable<string> values, string message)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (values.Any(value => !seen.Add(value)))
        {
            throw RuntimeJsonContract.Invalid(message);
        }
    }

    private static void EnsureDynamicCapabilityProperties(
        JsonElement root,
        int mapCount,
        int bossCount,
        int powerUpCount,
        int diagnosticCount)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index <= mapCount; index++)
        {
            expected.Add($"map_{index}_code");
            expected.Add($"map_{index}_display_name");
            expected.Add($"map_{index}_availability");
        }

        for (var index = 1; index <= bossCount; index++)
        {
            expected.Add($"boss_{index}_alias");
        }

        for (var index = 1; index <= powerUpCount; index++)
        {
            expected.Add($"power_up_{index}_alias");
        }

        for (var index = 1; index <= diagnosticCount; index++)
        {
            expected.Add($"diagnostic_{index}_alias");
        }

        var actual = root.EnumerateObject()
            .Select(property => property.Name)
            .Where(name => DynamicCapabilityName.IsMatch(name))
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            throw RuntimeJsonContract.Invalid("Compteurs et entrées dynamiques des capabilities incohérents.");
        }
    }

    private static ControlCenterContractAction ParseAction(string value) => value switch
    {
        "restart_map" => ControlCenterContractAction.RestartMap,
        "spawn_boss" => ControlCenterContractAction.SpawnBoss,
        "set_hostname" => ControlCenterContractAction.SetHostname,
        "set_join_password" => ControlCenterContractAction.SetJoinPassword,
        "clear_join_password" => ControlCenterContractAction.ClearJoinPassword,
        _ => throw RuntimeJsonContract.Invalid("Action Control Center inconnue.")
    };

    private static ControlCenterFeedbackStatus ParseFeedbackStatus(string value) => value switch
    {
        "accepted" => ControlCenterFeedbackStatus.Accepted,
        "applied" => ControlCenterFeedbackStatus.Applied,
        "rejected" => ControlCenterFeedbackStatus.Rejected,
        "failed" => ControlCenterFeedbackStatus.Failed,
        _ => throw RuntimeJsonContract.Invalid("Statut feedback Control Center inconnu.")
    };

    private static ControlCenterTransitionStatus ParseTransitionStatus(string value) => value switch
    {
        "accepted" => ControlCenterTransitionStatus.Accepted,
        "transitioning" => ControlCenterTransitionStatus.Transitioning,
        "active" => ControlCenterTransitionStatus.Active,
        "failed" => ControlCenterTransitionStatus.Failed,
        _ => throw RuntimeJsonContract.Invalid("Statut de transition Control Center inconnu.")
    };

    private void ResetForSession(string? sessionId)
    {
        if (string.Equals(_activeSessionId, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        _activeSessionId = sessionId;
        _capabilitiesCache = null;
        _feedbackCache = null;
        _transitionCache = null;
        _identityCache = null;
    }

    private ControlCenterContractSnapshot Unavailable(string message)
    {
        LocalReadResult<T> Missing<T>(LocalPinteModFile file) where T : class =>
            new(null, LocalSourceMetadata.Unavailable(message) with
            {
                SourceLabel = LocalPinteModOptions.GetSourceLabel(file)
            }, null);
        return new(
            Missing<ControlCenterCapabilitiesSnapshot>(LocalPinteModFile.ControlCenterCapabilities),
            Missing<ControlCenterActionFeedbackSnapshot>(LocalPinteModFile.ControlCenterActionFeedback),
            Missing<ControlCenterMapTransitionSnapshot>(LocalPinteModFile.ControlCenterMapTransition),
            Missing<ControlCenterServerIdentitySnapshot>(LocalPinteModFile.ControlCenterServerIdentity));
    }

    private bool TryAge(DateTimeOffset? timestamp, out TimeSpan age)
    {
        if (timestamp is null || timestamp.Value - _clock.UtcNow > HeartbeatFreshnessPolicy.FutureTimestampTolerance)
        {
            age = TimeSpan.Zero;
            return false;
        }

        age = Age(timestamp.Value);
        return true;
    }

    private TimeSpan Age(DateTimeOffset timestamp)
    {
        var age = _clock.UtcNow - timestamp;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static bool IsSafeSessionId(string? value) =>
        value is { Length: >= 1 and <= 96 } && RuntimeJsonContract.IsSafeIdentifier(value);

    private sealed record Cache<T>(T Value, DateTimeOffset FileTimestampUtc) where T : class;

    private static readonly HashSet<string> CapabilityPropertyNames = new(StringComparer.Ordinal)
    {
        "schema_version", "module_version", "contract_module_version", "command_contract_version",
        "session_id", "sequence", "generated_gettime", "updated_at_utc", "time_authority",
        "map_code", "map_source", "map_installation_authority", "map_count", "rotation_state",
        "rotation_entry_count", "change_map", "restart_map", "event_count", "boss_count",
        "power_up_count", "diagnostic_count", "transition_state", "set_hostname",
        "set_join_password", "clear_join_password", "join_password_transport", "map_profile",
        "power_support", "pack_a_punch_support", "event_support", "boss_support", "music_support",
        "dog_round_support", "active_pintemod_bosses", "max_pintemod_bosses"
    };

    private static readonly HashSet<string> FeedbackPropertyNames = new(StringComparer.Ordinal)
    {
        "schema_version", "session_id", "sequence", "generated_gettime", "updated_at_utc",
        "time_authority", "request_id", "action", "status", "result_code"
    };

    private static readonly HashSet<string> TransitionPropertyNames = new(StringComparer.Ordinal)
    {
        "schema_version", "request_id", "action", "requested_map", "originating_session_id",
        "status", "result_code", "generated_gettime", "updated_at_utc", "time_authority",
        "resulting_session_id"
    };

    private static readonly HashSet<string> IdentityPropertyNames = new(StringComparer.Ordinal)
    {
        "schema_version", "session_id", "sequence", "generated_gettime", "updated_at_utc",
        "time_authority", "public_hostname", "public_hostname_state", "join_password_enabled", "revision"
    };
}
