using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed partial class StructuredLogReader : IStructuredLogReader, IDisposable
{
    private const int MaximumBytesPerFile = 2 * 1024 * 1024;
    private const int MaximumLineLength = 4096;
    private const int MaximumCachedEvents = 500;
    private readonly BlockALocalPathPolicy _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, FileCursor> _cursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TrackedPlayer> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LiveEvent> _events = [];
    private string? _sessionId;
    private long _sessionStartedGetTime;
    private long _latestGetTime;
    private int? _round;
    private RankedStatus _rankedStatus = RankedStatus.Unknown;
    private bool _rankedStatusAvailable;
    private int _ignored;
    private int _malformed;

    public StructuredLogReader(LocalPinteModOptions options) => _paths = new BlockALocalPathPolicy(options);

    public Task<StructuredLogSnapshot> ReadAsync(
        SessionManifest? session,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadWorkerAsync(session, cancellationToken), cancellationToken);

    public void Dispose() => _gate.Dispose();

    private async Task<StructuredLogSnapshot> ReadWorkerAsync(SessionManifest? session, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (session is null)
            {
                Reset(null, 0);
                return StructuredLogSnapshot.Empty("—", LocalSourceMetadata.Unavailable("Session active indisponible."));
            }

            if (!string.Equals(_sessionId, session.SessionId, StringComparison.Ordinal))
            {
                Reset(session.SessionId, session.StartedGetTime);
            }

            var scanned = 0;
            var sourceTimes = new List<DateTimeOffset>();
            var availableSources = new List<(string FileName, string Path, FileInfo Info)>();
            foreach (var fileName in BlockALocalPathPolicy.GetAllowedSessionLogNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = _paths.ResolveSessionLogPath(session.SessionId, fileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                var info = new FileInfo(path);
                info.Refresh();
                availableSources.Add((fileName, path, info));
            }

            if (await ContainsReplacedSourceAsync(availableSources, cancellationToken))
            {
                Reset(session.SessionId, session.StartedGetTime);
            }

            foreach (var sourceFile in availableSources)
            {
                scanned++;
                sourceTimes.Add(new DateTimeOffset(DateTime.SpecifyKind(sourceFile.Info.LastWriteTimeUtc, DateTimeKind.Utc)));
                await ReadIncrementAsync(sourceFile.Path, sourceFile.FileName, cancellationToken);
            }

            var source = scanned == 0
                ? new LocalSourceMetadata(
                    LocalReadStatus.Missing,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    $"logs/sessions/{SafeSessionLabel(session.SessionId)}",
                    "Aucun log autorisé disponible pour la session active.")
                : new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    sourceTimes.Count == 0 ? null : NonNegativeAge(sourceTimes.Max()),
                    DataProvenance.LocalFile,
                    $"logs/sessions/{SafeSessionLabel(session.SessionId)}",
                    $"Session active lue : {scanned} source(s), {_ignored} ligne(s) ignorée(s), {_malformed} malformée(s).");

            TimeSpan? duration = _latestGetTime >= session.StartedGetTime
                ? TimeSpan.FromMilliseconds(_latestGetTime - session.StartedGetTime)
                : null;
            var playerModels = _players.Values
                .OrderBy(item => item.ClientNumber)
                .Select(item => item.ToModel(_latestGetTime))
                .ToArray();

            return new StructuredLogSnapshot(
                session.SessionId,
                _events.OrderByDescending(item => item.SessionElapsed).Take(MaximumCachedEvents).ToArray(),
                playerModels,
                _round,
                duration,
                _rankedStatus,
                _rankedStatusAvailable,
                source,
                scanned,
                _ignored,
                _malformed,
                _events.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var readStatus = exception switch
            {
                UnauthorizedAccessException => LocalReadStatus.AccessDenied,
                InvalidOperationException => LocalReadStatus.Invalid,
                _ => LocalReadStatus.IoError
            };
            return new StructuredLogSnapshot(
                session?.SessionId ?? "—",
                _events.ToArray(),
                _players.Values.Select(item => item.ToModel(_latestGetTime)).ToArray(),
                _round,
                null,
                _rankedStatus,
                _rankedStatusAvailable,
                new LocalSourceMetadata(
                    readStatus,
                    DataFreshness.Stale,
                    null,
                    _events.Count > 0 ? DataProvenance.MemoryCache : DataProvenance.LocalFile,
                    "Logs structurés de la session active",
                    _events.Count > 0
                        ? "Dernière donnée valide — lecture actuelle indisponible."
                        : "Lecture des logs impossible."),
                0,
                _ignored,
                _malformed,
                _events.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ReadIncrementAsync(string path, string fileName, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        info.Refresh();
        if (info.Length == 0)
        {
            return;
        }

        if (!_cursors.TryGetValue(path, out var cursor))
        {
            cursor = new FileCursor();
            _cursors[path] = cursor;
        }

        if (info.Length < cursor.Position || info.LastWriteTimeUtc < cursor.LastWriteTimeUtc)
        {
            cursor.Position = 0;
            cursor.Generation++;
        }

        if (cursor.Position >= info.Length)
        {
            cursor.LastWriteTimeUtc = info.LastWriteTimeUtc;
            cursor.CreationTimeUtc = info.CreationTimeUtc;
            cursor.PrefixHash = await ComputePrefixHashAsync(path, info.Length, cancellationToken);
            return;
        }

        var available = Math.Min(info.Length - cursor.Position, MaximumBytesPerFile);
        if (cursor.Position == 0 && info.Length > MaximumBytesPerFile)
        {
            cursor.Position = info.Length - MaximumBytesPerFile;
            available = MaximumBytesPerFile;
            _ignored++;
        }

        var buffer = new byte[(int)available];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        stream.Position = cursor.Position;
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
        if (bytesRead == 0)
        {
            return;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        var lastNewLine = text.LastIndexOf('\n');
        if (lastNewLine < 0)
        {
            if (text.Length > MaximumLineLength)
            {
                cursor.Position += bytesRead;
                _malformed++;
            }

            return;
        }

        var completeText = text[..(lastNewLine + 1)];
        foreach (var rawLine in completeText.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith("===", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Length > MaximumLineLength)
            {
                _malformed++;
                continue;
            }

            ProcessLine(fileName, line);
        }

        cursor.Position += Encoding.UTF8.GetByteCount(completeText);
        cursor.LastWriteTimeUtc = info.LastWriteTimeUtc;
        cursor.CreationTimeUtc = info.CreationTimeUtc;
        cursor.PrefixHash = await ComputePrefixHashAsync(path, info.Length, cancellationToken);
    }

    private async Task<bool> ContainsReplacedSourceAsync(
        IReadOnlyList<(string FileName, string Path, FileInfo Info)> sources,
        CancellationToken cancellationToken)
    {
        foreach (var source in sources)
        {
            if (!_cursors.TryGetValue(source.Path, out var cursor) || cursor.Position == 0)
            {
                continue;
            }

            if (source.Info.Length < cursor.Position ||
                source.Info.LastWriteTimeUtc < cursor.LastWriteTimeUtc ||
                source.Info.CreationTimeUtc != cursor.CreationTimeUtc)
            {
                return true;
            }

            var currentPrefix = await ComputePrefixHashAsync(source.Path, source.Info.Length, cancellationToken);
            if (cursor.PrefixHash is null || !CryptographicOperations.FixedTimeEquals(cursor.PrefixHash, currentPrefix))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<byte[]> ComputePrefixHashAsync(
        string path,
        long fileLength,
        CancellationToken cancellationToken)
    {
        var length = (int)Math.Min(256, Math.Max(0, fileLength));
        if (length == 0)
        {
            return SHA256.HashData([]);
        }

        var buffer = new byte[length];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var read = await stream.ReadAsync(buffer.AsMemory(0, length), cancellationToken);
        return SHA256.HashData(buffer.AsSpan(0, read));
    }

    private void ProcessLine(string fileName, string line)
    {
        if (string.Equals(fileName, "connections.log", StringComparison.OrdinalIgnoreCase))
        {
            ProcessConnectionLine(line);
            return;
        }

        var match = ManagedEventLine().Match(line);
        if (!match.Success || !long.TryParse(match.Groups["time"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var time))
        {
            _malformed++;
            return;
        }

        var eventName = match.Groups["event"].Value;
        if (!AllowedEvents.Contains(eventName))
        {
            _ignored++;
            return;
        }

        var fields = ParseFields(match.Groups["fields"].Value);
        TrackLatest(time, fields);
        UpdateDerivedState(eventName, time, fields);
        AddEvent(fileName, eventName, time, fields);
    }

    private void ProcessConnectionLine(string line)
    {
        var match = ConnectionLine().Match(line);
        if (!match.Success ||
            !long.TryParse(match.Groups["time"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var time) ||
            !int.TryParse(match.Groups["round"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var round) ||
            !int.TryParse(match.Groups["client"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var client))
        {
            _malformed++;
            return;
        }

        var eventName = match.Groups["event"].Value;
        var xuid = match.Groups["xuid"].Value.Trim();
        if (!XuidValidator.IsValid(xuid))
        {
            _malformed++;
            return;
        }

        _latestGetTime = Math.Max(_latestGetTime, time);
        _round = round;
        var displayName = LogPrivacyFilter.SafePlayerName(match.Groups["name"].Value);
        if (eventName is "JOIN" or "ACTIVE")
        {
            _players[xuid] = new TrackedPlayer(xuid, client, displayName, time);
        }
        else if (eventName == "LEAVE")
        {
            _players.Remove(xuid);
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["player"] = displayName,
            ["client"] = client.ToString(CultureInfo.InvariantCulture),
            ["round"] = round.ToString(CultureInfo.InvariantCulture),
            ["xuid"] = xuid
        };
        AddEvent("connections.log", eventName, time, fields);
    }

    private void UpdateDerivedState(string eventName, long time, IReadOnlyDictionary<string, string> fields)
    {
        if (eventName == "MATCH_UNRANKED")
        {
            _rankedStatus = RankedStatus.Unranked;
            _rankedStatusAvailable = true;
        }

        if (fields.TryGetValue("round", out var roundText) &&
            int.TryParse(roundText, NumberStyles.None, CultureInfo.InvariantCulture, out var round) && round >= 0)
        {
            _round = round;
        }

        var xuid = GetXuid(fields);
        if (xuid is null || !_players.TryGetValue(xuid, out var player))
        {
            return;
        }

        if (fields.TryGetValue("display", out var display) || fields.TryGetValue("player", out display))
        {
            player.DisplayName = LogPrivacyFilter.SafePlayerName(display);
        }

        if (eventName is "IDENTITY_ATTACHED" or "PERSISTENT_ROLE_CHANGED" && fields.TryGetValue("role", out var role))
        {
            player.Role = SafeCode(role, 24);
        }

        if (eventName is "LANGUAGE_ASSIGNED" or "LANGUAGE_CHANGED" or "PLAYER_LANGUAGE_ATTACHED" && fields.TryGetValue("language", out var language))
        {
            player.Language = SafeCode(language, 12);
        }

        if (eventName == "COUNTRY_ANNOUNCED" && fields.TryGetValue("country", out var country))
        {
            player.CountryCode = SafeCode(country, 3).ToUpperInvariant();
        }

        if (eventName == "MODERATION_STATE")
        {
            player.IsMuted = IsTrue(fields, "muted");
            player.IsBanned = IsTrue(fields, "banned");
        }

        player.LastObservedGetTime = Math.Max(player.LastObservedGetTime, time);
    }

    private void TrackLatest(long time, IReadOnlyDictionary<string, string> fields)
    {
        _latestGetTime = Math.Max(_latestGetTime, time);
        if (fields.TryGetValue("gettime", out var getTimeText) &&
            long.TryParse(getTimeText, NumberStyles.None, CultureInfo.InvariantCulture, out var getTime))
        {
            _latestGetTime = Math.Max(_latestGetTime, getTime);
        }
    }

    private void AddEvent(string fileName, string eventName, long time, IReadOnlyDictionary<string, string> fields)
    {
        var category = CategoryFor(fileName, eventName);
        var title = TitleFor(eventName);
        var details = BuildDetails(fields);
        _events.Add(new LiveEvent(
            DateTimeOffset.UnixEpoch,
            category,
            title,
            details,
            SeverityFor(eventName))
        {
            SessionElapsed = TimeSpan.FromMilliseconds(Math.Max(0, time - _sessionStartedGetTime)),
            Provenance = DataProvenance.LocalFile,
            SourceLabel = fileName
        });

        if (_events.Count > MaximumCachedEvents)
        {
            _events.RemoveRange(0, _events.Count - MaximumCachedEvents);
        }
    }

    private static Dictionary<string, string> ParseFields(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(64))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0 || separator == part.Length - 1)
            {
                continue;
            }

            var key = part[..separator].Trim();
            if (SafeFieldName().IsMatch(key))
            {
                result[key] = part[(separator + 1)..].Trim();
            }
        }

        return result;
    }

    private static string BuildDetails(IReadOnlyDictionary<string, string> fields)
    {
        var values = PublicFieldNames
            .Where(fields.ContainsKey)
            .Select(key => $"{PublicFieldLabels.GetValueOrDefault(key, key)} : {LogPrivacyFilter.SanitizeDisplayText(fields[key], 64)}")
            .Where(value => !value.EndsWith(": ", StringComparison.Ordinal))
            .Take(5)
            .ToArray();
        return values.Length == 0 ? "Événement structuré local" : string.Join(" · ", values);
    }

    private static string? GetXuid(IReadOnlyDictionary<string, string> fields)
    {
        foreach (var key in new[] { "xuid", "player_xuid", "target_xuid" })
        {
            if (fields.TryGetValue(key, out var value) && XuidValidator.IsValid(value.Trim()))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string SafeCode(string value, int maximumLength)
    {
        var result = new string(value.Trim().Where(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-').Take(maximumLength).ToArray());
        return string.IsNullOrWhiteSpace(result) ? "unknown" : result.ToLowerInvariant();
    }

    private static bool IsTrue(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.Trim().ToLowerInvariant() is "true" or "1" or "yes" or "active";

    private static string CategoryFor(string fileName, string eventName) => fileName switch
    {
        "connections.log" => "JOUEURS",
        "ranks.log" => "RANKS",
        "easter_eggs.log" => "EASTER EGG",
        "moderation.log" => "MODÉRATION",
        "localization.log" => "LANGUE",
        "storage.log" or "validation.log" => "ALERTES",
        _ when eventName.Contains("REJECTED", StringComparison.Ordinal) => "ALERTES",
        _ => "SYSTÈME"
    };

    private static string TitleFor(string eventName) => eventName switch
    {
        "JOIN" => "Joueur connecté",
        "LEAVE" => "Joueur déconnecté",
        "ACTIVE" => "Présence joueur confirmée",
        "MATCH_UNRANKED" => "Session déclarée Unranked",
        "MATCH_CLOCK_STARTED" => "Chronomètre de partie démarré",
        "MAP_RECORD_TOP5" => "Record de manche enregistré",
        "NATIVE_COMPLETION_DETECTED" or "TOMB_COMPLETION_DETECTED" => "Easter Egg détecté",
        "IDENTITY_ATTACHED" => "Identité joueur associée",
        "PERSISTENT_ROLE_CHANGED" => "Rôle joueur mis à jour",
        "LANGUAGE_ASSIGNED" or "LANGUAGE_CHANGED" or "PLAYER_LANGUAGE_ATTACHED" => "Langue joueur mise à jour",
        "COUNTRY_ANNOUNCED" => "Pays joueur détecté",
        "MODERATION_STATE" => "État de modération mis à jour",
        "CORRUPT_QUARANTINED" => "Fichier corrompu isolé",
        "JSON_RESTORED_FROM_BACKUP" => "Donnée restaurée par PinteMod",
        "V211_SUITE" or "GROUPED_SUITE" or "GROUPED_TEST_SUITE_PASS" => "Validation PinteMod",
        _ => eventName.Replace('_', ' ')
    };

    private static EventSeverity SeverityFor(string eventName)
    {
        if (eventName.Contains("CORRUPT", StringComparison.Ordinal) || eventName.Contains("ERROR", StringComparison.Ordinal))
        {
            return EventSeverity.Danger;
        }

        if (eventName.Contains("REJECTED", StringComparison.Ordinal) ||
            eventName.Contains("BLOCKED", StringComparison.Ordinal) ||
            eventName.Contains("TIMEOUT", StringComparison.Ordinal) ||
            eventName.Contains("UNRANKED", StringComparison.Ordinal))
        {
            return EventSeverity.Warning;
        }

        return eventName is "JOIN" or "MAP_RECORD_TOP5" or "NATIVE_COMPLETION_DETECTED" or "TOMB_COMPLETION_DETECTED"
            ? EventSeverity.Success
            : EventSeverity.Information;
    }

    private void Reset(string? sessionId, long startedGetTime)
    {
        _sessionId = sessionId;
        _sessionStartedGetTime = startedGetTime;
        _cursors.Clear();
        _players.Clear();
        _events.Clear();
        _latestGetTime = 0;
        _round = null;
        _rankedStatus = RankedStatus.Unknown;
        _rankedStatusAvailable = false;
        _ignored = 0;
        _malformed = 0;
    }

    private static string SafeSessionLabel(string sessionId) =>
        sessionId.Length <= 24 ? sessionId : sessionId[..24] + "…";

    private static TimeSpan NonNegativeAge(DateTimeOffset timestamp)
    {
        var age = DateTimeOffset.UtcNow - timestamp;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private sealed class FileCursor
    {
        public long Position { get; set; }
        public int Generation { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public DateTime CreationTimeUtc { get; set; }
        public byte[]? PrefixHash { get; set; }
    }

    private sealed class TrackedPlayer(string xuid, int clientNumber, string displayName, long joinedGetTime)
    {
        public string Xuid { get; } = xuid;
        public int ClientNumber { get; } = clientNumber;
        public string DisplayName { get; set; } = displayName;
        public string Role { get; set; } = "unknown";
        public string Language { get; set; } = "unknown";
        public string CountryCode { get; set; } = "--";
        public long JoinedGetTime { get; } = joinedGetTime;
        public long LastObservedGetTime { get; set; } = joinedGetTime;
        public bool IsMuted { get; set; }
        public bool IsBanned { get; set; }

        public PlayerState ToModel(long latestGetTime) => new(
            ClientNumber,
            Xuid,
            DisplayName,
            Role,
            Language,
            CountryCode,
            PlayerLifeState.Unknown,
            0,
            TimeSpan.FromMilliseconds(Math.Max(0, latestGetTime - JoinedGetTime)),
            IsMuted,
            IsBanned)
        {
            LifeStateAvailable = false,
            PointsAvailable = false,
            PresenceAvailable = latestGetTime >= JoinedGetTime,
            Provenance = DataProvenance.LocalFile
        };
    }

    private static readonly HashSet<string> AllowedEvents = new(StringComparer.Ordinal)
    {
        "EE_DIAG", "GROUPED_SUITE", "LATE_JOIN_ACTIVE_CONFIRMED", "LATE_JOIN_ATTEMPT", "LATE_JOIN_REJECTED",
        "LATE_JOIN_SUCCESS", "NORMAL_DEATH_AFTER_LATE_JOIN", "PUBLIC_TIP_CHAT", "RANKS", "SPECTATOR_SPAWN_PROMPT",
        "SPECTATOR_WAITING", "WELCOME_SENT_HUD", "ACTIVITY_FLUSH", "AUDIT_COMPLETE", "MAP_RECORD_TOP5",
        "MATCH_CLOCK_STARTED", "MATCH_RECORD_ID", "MATCH_UNRANKED", "MODULE_LOADED", "OFFLINE_PERSONAL_ROUND_UPDATED",
        "PLAYER_ATTACHED", "PLAYER_DISCONNECTED", "PLAYER_STARTED", "RECORD_ELIGIBLE", "ROUND_SKIPPED_UNRANKED",
        "SESSION_STARTED", "TEAM_RECORD_CHAT", "UNRANKED_PERSONAL_ROLLBACK", "ACTIVE_HOLDERS_TEST_ACCEPTED",
        "ACTIVE_HOLDERS_TEST_BLOCKED", "GROUPED_TEST_SUITE_PASS", "NATIVE_CANDIDATE_STORED", "NATIVE_COMPLETION_DETECTED",
        "OFFICIAL_RECORD_CANDIDATE_BLOCKED", "PROFILE_ARMED", "PROFILE_OFFICIAL_ENABLED", "PROFILE_VALIDATED",
        "TEST_CANDIDATE_DUPLICATE_IGNORED", "TEST_CANDIDATE_STORED", "TEST_RECORD_TOP5", "TEST_RECORDS_CLEARED",
        "TOMB_COMPLETION_DETECTED", "TOMB_FINAL_STEP_STARTED", "TOMB_SIGNAL_COMPARISON", "IDENTITY_ATTACHED",
        "PERSISTENT_ROLE_CHANGED", "MODERATION_STATE", "COUNTRY_ANNOUNCED", "GEOIP_REQUESTED", "GEOIP_TIMEOUT",
        "LANGUAGE_ASSIGNED", "LANGUAGE_CHANGED", "PLAYER_LANGUAGE_ATTACHED", "CORRUPT_QUARANTINED",
        "JSON_RESTORED_FROM_BACKUP", "V211_SUITE"
    };

    private static readonly string[] PublicFieldNames =
    [
        "player", "display", "map", "round", "players", "client", "role", "language", "country", "status", "result",
        "category", "type", "version", "official_mode", "records", "profiles"
    ];

    private static readonly IReadOnlyDictionary<string, string> PublicFieldLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["player"] = "Joueur", ["display"] = "Affichage", ["map"] = "Carte", ["round"] = "Manche", ["players"] = "Joueurs",
        ["client"] = "Client", ["role"] = "Rôle", ["language"] = "Langue", ["country"] = "Pays",
        ["status"] = "État", ["result"] = "Résultat", ["category"] = "Catégorie", ["type"] = "Type",
        ["version"] = "Version", ["official_mode"] = "Mode", ["records"] = "Records", ["profiles"] = "Profils"
    };

    [GeneratedRegex(@"^\[(?<time>\d+)(?:\s+ms)?\]\s*(?<event>[A-Z][A-Z0-9_]+)\s*(?:\|\s*(?<fields>.*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex ManagedEventLine();

    [GeneratedRegex(@"^\[(?<time>\d+)\s+ms\]\[round\s+(?<round>\d+)\]\[(?<event>JOIN|LEAVE|ACTIVE)\]\s*(?<name>[^|]{1,96})\s*\|\s*xuid=(?<xuid>[^|\s]+)\s*\|\s*client=(?<client>\d+)(?:\s*\|.*)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionLine();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeFieldName();
}
