using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class ControlCenterRuntimeSnapshotReader : IControlCenterRuntimeSnapshotReader, IDisposable
{
    internal const int MaximumFileSizeBytes = 32768;
    private const int MaximumPlayers = 4;
    private const int MaximumWeaponsPerPlayer = 8;
    private const int MaximumPerksPerPlayer = 9;
    private const long MaximumSessionElapsedMilliseconds = 366L * 24 * 60 * 60 * 1000;
    private static readonly HashSet<string> KnownPerks = new(StringComparer.Ordinal)
    {
        "jug", "quick", "speed", "doubletap", "staminup", "deadshot", "mule", "cherry", "widows"
    };

    private readonly ReadOnlyJsonFileReader _fileReader;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CachedValue? _cached;
    private string? _activeSessionId;
    private int _consecutiveFailures;

    public ControlCenterRuntimeSnapshotReader(LocalPinteModOptions options, IClock clock)
        : this(options, clock, null)
    {
    }

    internal ControlCenterRuntimeSnapshotReader(
        LocalPinteModOptions options,
        IClock clock,
        Action<string>? afterReadBeforeVerification)
    {
        _fileReader = new ReadOnlyJsonFileReader(options, afterReadBeforeVerification);
        _clock = clock;
    }

    public async Task<LocalReadResult<ControlCenterRuntimeSnapshot>> ReadAsync(
        string? activeSessionId,
        string? activeMapCode,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!IsSafeSessionId(activeSessionId) || !MapCodeValidator.TryNormalize(activeMapCode, out var normalizedMap))
            {
                ResetForSession(null);
                return Unavailable("Aucune session locale active vérifiée.");
            }

            ResetForSession(activeSessionId);
            var result = await _fileReader.ReadAsync(
                LocalPinteModFile.ControlCenterRuntimeSnapshot,
                Parse,
                MaximumFileSizeBytes,
                cancellationToken);

            if (result.Status == LocalReadStatus.Success && result.Value is not null)
            {
                if (!string.Equals(result.Value.SessionId, activeSessionId, StringComparison.Ordinal) ||
                    !string.Equals(result.Value.MapCode, normalizedMap, StringComparison.Ordinal))
                {
                    _cached = null;
                    return InvalidSession(result.LastWriteTimeUtc);
                }

                if (!TryFileAge(result.LastWriteTimeUtc, out var age))
                {
                    return Failure(new LocalJsonFileResult<ControlCenterRuntimeSnapshot>(
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
                        "Snapshot runtime PinteMod lu avec succès."),
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

    private LocalReadResult<ControlCenterRuntimeSnapshot> Failure(
        LocalJsonFileResult<ControlCenterRuntimeSnapshot> result)
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

    private static ControlCenterRuntimeSnapshot Parse(JsonElement root)
    {
        RuntimeJsonContract.RequireObject(root);
        var schema = RuntimeJsonContract.RequiredInt32(root, "schema_version", 1, int.MaxValue);
        if (schema != 1)
        {
            throw new LocalJsonValidationException(
                LocalReadStatus.UnsupportedSchema,
                "Schéma snapshot runtime non pris en charge.");
        }

        var timeAuthority = RuntimeJsonContract.RequiredString(root, "time_authority", 64);
        if (!string.Equals(timeAuthority, RuntimeJsonContract.TimeAuthority, StringComparison.Ordinal))
        {
            throw RuntimeJsonContract.Invalid("Autorité temporelle runtime inconnue.");
        }

        var map = RuntimeJsonContract.RequiredString(root, "map_code", MapCodeValidator.MaximumLength);
        if (!MapCodeValidator.TryNormalize(map, out var normalizedMap))
        {
            throw RuntimeJsonContract.Invalid("Code de carte runtime invalide.");
        }

        var connectedPlayers = RuntimeJsonContract.RequiredInt32(root, "connected_players", 0, 64);
        var maximumPlayers = RuntimeJsonContract.OptionalInt32(root, "max_players", 1, 64);
        if (maximumPlayers is not null && connectedPlayers > maximumPlayers)
        {
            throw RuntimeJsonContract.Invalid("Nombre de joueurs runtime incohérent.");
        }

        var observablePlayers = RuntimeJsonContract.RequiredInt32(root, "observable_players", 0, MaximumPlayers);
        var identityUnavailablePlayers = RuntimeJsonContract.RequiredInt32(root, "identity_unavailable_players", 0, 64);
        var playersTruncated = RuntimeJsonContract.RequiredFlag(root, "players_truncated");
        if (observablePlayers + identityUnavailablePlayers > connectedPlayers ||
            (!playersTruncated && observablePlayers + identityUnavailablePlayers != connectedPlayers))
        {
            throw RuntimeJsonContract.Invalid("Comptage des joueurs runtime incohérent.");
        }

        var players = new List<RuntimePlayerSnapshot>(observablePlayers);
        var xuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clients = new HashSet<int>();
        for (var index = 1; index <= observablePlayers; index++)
        {
            var player = ParsePlayer(root, index);
            if (!xuids.Add(player.Xuid))
            {
                throw RuntimeJsonContract.Invalid("BOIII_XUID dupliqué dans le snapshot runtime.");
            }

            if (!clients.Add(player.ClientNumber))
            {
                throw RuntimeJsonContract.Invalid("Numéro client dupliqué dans le snapshot runtime.");
            }

            players.Add(player);
        }

        var elapsedMilliseconds = RuntimeJsonContract.OptionalInt64(
            root,
            "session_elapsed_ms",
            0,
            MaximumSessionElapsedMilliseconds);
        return new(
            schema,
            RuntimeJsonContract.RequiredString(root, "module_version", 32, RuntimeJsonContract.IsSafeIdentifier),
            RuntimeJsonContract.RequiredString(root, "session_id", 128, IsSafeSessionId),
            RuntimeJsonContract.RequiredInt64(root, "sequence", 0, long.MaxValue),
            RuntimeJsonContract.RequiredInt64(root, "generated_gettime", 0, int.MaxValue),
            RuntimeJsonContract.OptionalUtc(root, "updated_at_utc"),
            timeAuthority,
            normalizedMap,
            RuntimeJsonContract.OptionalInt32(root, "round", 0, 255),
            RuntimeJsonContract.OptionalInt64(root, "session_started_gettime", 0, int.MaxValue),
            elapsedMilliseconds is null ? null : TimeSpan.FromMilliseconds(elapsedMilliseconds.Value),
            ParseRankedStatus(RuntimeJsonContract.RequiredString(root, "ranked_status", 16)),
            ParsePowerState(RuntimeJsonContract.RequiredString(root, "power_state", 24)),
            ParsePackAPunchState(RuntimeJsonContract.RequiredString(root, "pack_a_punch_state", 24)),
            connectedPlayers,
            maximumPlayers,
            observablePlayers,
            identityUnavailablePlayers,
            playersTruncated,
            players);
    }

    private static RuntimePlayerSnapshot ParsePlayer(JsonElement root, int index)
    {
        var prefix = $"player_{index}_";
        var xuid = RuntimeJsonContract.RequiredString(root, prefix + "xuid", XuidValidator.ExpectedLength);
        if (!XuidValidator.IsValid(xuid))
        {
            throw RuntimeJsonContract.Invalid("BOIII_XUID runtime invalide.");
        }

        var rawDisplayName = RuntimeJsonContract.RequiredString(root, prefix + "display_name", 64);
        var presence = RuntimeJsonContract.RequiredString(root, prefix + "presence", 16);
        if (!string.Equals(presence, "connected", StringComparison.Ordinal))
        {
            throw RuntimeJsonContract.Invalid("Présence joueur runtime inconnue.");
        }

        var weaponCount = RuntimeJsonContract.RequiredInt32(root, prefix + "weapon_count", 0, MaximumWeaponsPerPlayer);
        var weapons = new List<RuntimeWeaponSnapshot>(weaponCount);
        for (var weaponIndex = 1; weaponIndex <= weaponCount; weaponIndex++)
        {
            var weaponPrefix = prefix + $"weapon_{weaponIndex}_";
            weapons.Add(new RuntimeWeaponSnapshot(
                RequiredWeaponId(root, weaponPrefix + "id"),
                ParseWeaponPackAPunchState(RuntimeJsonContract.RequiredString(root, weaponPrefix + "pap_state", 24)),
                RuntimeJsonContract.OptionalInt32(root, weaponPrefix + "ammo_clip", 0, 1_000_000),
                RuntimeJsonContract.OptionalInt32(root, weaponPrefix + "ammo_reserve", 0, 1_000_000)));
        }

        var perkCount = RuntimeJsonContract.RequiredInt32(root, prefix + "perk_count", 0, MaximumPerksPerPlayer);
        var perks = new List<string>(perkCount);
        for (var perkIndex = 1; perkIndex <= perkCount; perkIndex++)
        {
            var perk = RuntimeJsonContract.RequiredString(root, prefix + $"perk_{perkIndex}", 24);
            if (!KnownPerks.Contains(perk) || perks.Contains(perk, StringComparer.Ordinal))
            {
                throw RuntimeJsonContract.Invalid("Alias d’atout runtime inconnu ou dupliqué.");
            }

            perks.Add(perk);
        }

        return new(
            xuid,
            LogPrivacyFilter.SafePlayerName(rawDisplayName),
            RuntimeJsonContract.RequiredInt32(root, prefix + "client_number", 0, 63),
            presence,
            ParseLifeState(RuntimeJsonContract.RequiredString(root, prefix + "life_state", 16)),
            ParseGodModeState(RuntimeJsonContract.RequiredString(root, prefix + "godmode_state", 16)),
            RuntimeJsonContract.OptionalInt32(root, prefix + "points", 0, int.MaxValue),
            RuntimeJsonContract.OptionalInt32(root, prefix + "health", 0, 1_000_000),
            RuntimeJsonContract.OptionalInt32(root, prefix + "max_health", 0, 1_000_000),
            RequiredWeaponId(root, prefix + "equipped_weapon"),
            ParseWeaponPackAPunchState(RuntimeJsonContract.RequiredString(root, prefix + "equipped_weapon_pap_state", 24)),
            RuntimeJsonContract.OptionalInt32(root, prefix + "equipped_ammo_clip", 0, 1_000_000),
            RuntimeJsonContract.OptionalInt32(root, prefix + "equipped_ammo_reserve", 0, 1_000_000),
            weapons,
            RuntimeJsonContract.RequiredFlag(root, prefix + "weapons_truncated"),
            perks);
    }

    private static string RequiredWeaponId(JsonElement root, string name) =>
        RuntimeJsonContract.RequiredString(
            root,
            name,
            128,
            value => value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'));

    private static RankedStatus ParseRankedStatus(string value) => value switch
    {
        "ranked" => RankedStatus.Ranked,
        "unranked" => RankedStatus.Unranked,
        "unknown" => RankedStatus.Unknown,
        _ => throw RuntimeJsonContract.Invalid("Statut Ranked runtime inconnu.")
    };

    private static RuntimePowerState ParsePowerState(string value) => value switch
    {
        "on" => RuntimePowerState.On,
        "off" => RuntimePowerState.Off,
        "unknown" => RuntimePowerState.Unknown,
        "not_applicable" => RuntimePowerState.NotApplicable,
        _ => throw RuntimeJsonContract.Invalid("État du courant runtime inconnu.")
    };

    private static RuntimePackAPunchState ParsePackAPunchState(string value) => value switch
    {
        "available" => RuntimePackAPunchState.Available,
        "unavailable" => RuntimePackAPunchState.Unavailable,
        "unknown" => RuntimePackAPunchState.Unknown,
        "not_applicable" => RuntimePackAPunchState.NotApplicable,
        _ => throw RuntimeJsonContract.Invalid("État Pack-a-Punch runtime inconnu.")
    };

    private static RuntimeWeaponPackAPunchState ParseWeaponPackAPunchState(string value) => value switch
    {
        "base" => RuntimeWeaponPackAPunchState.Base,
        "upgraded" => RuntimeWeaponPackAPunchState.Upgraded,
        "unknown" => RuntimeWeaponPackAPunchState.Unknown,
        "not_applicable" => RuntimeWeaponPackAPunchState.NotApplicable,
        _ => throw RuntimeJsonContract.Invalid("État Pack-a-Punch d’arme inconnu.")
    };

    private static PlayerLifeState ParseLifeState(string value) => value switch
    {
        "alive" => PlayerLifeState.Alive,
        "down" => PlayerLifeState.Downed,
        "dead" => PlayerLifeState.Dead,
        "spectator" => PlayerLifeState.Spectator,
        "unknown" => PlayerLifeState.Unknown,
        _ => throw RuntimeJsonContract.Invalid("État de vie joueur inconnu.")
    };

    private static RuntimeGodModeState ParseGodModeState(string value) => value switch
    {
        "on" => RuntimeGodModeState.On,
        "off" => RuntimeGodModeState.Off,
        "unknown" => RuntimeGodModeState.Unknown,
        _ => throw RuntimeJsonContract.Invalid("État Godmode runtime inconnu.")
    };

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

    private LocalReadResult<ControlCenterRuntimeSnapshot> InvalidSession(DateTimeOffset? timestamp) =>
        new(
            null,
            Metadata(
                LocalReadStatus.Invalid,
                DataFreshness.Unknown,
                null,
                DataProvenance.LocalFile,
                "Snapshot runtime lié à une autre session ou carte."),
            timestamp);

    private LocalReadResult<ControlCenterRuntimeSnapshot> Unavailable(string message) =>
        new(null, LocalSourceMetadata.Unavailable(message) with
        {
            SourceLabel = LocalPinteModOptions.GetSourceLabel(LocalPinteModFile.ControlCenterRuntimeSnapshot)
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
            LocalPinteModOptions.GetSourceLabel(LocalPinteModFile.ControlCenterRuntimeSnapshot),
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

    private sealed record CachedValue(ControlCenterRuntimeSnapshot Value, DateTimeOffset FileTimestampUtc);
}
