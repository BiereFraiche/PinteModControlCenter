using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed partial class CommunityPauseLogReader : ICommunityPauseLogReader, IDisposable
{
    private const int MaximumBytesPerRead = 256 * 1024;
    private const int MaximumLineLength = 4096;
    private const int MaximumCachedEvents = 200;
    private readonly BlockALocalPathPolicy _paths;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<LiveEvent> _events = [];
    private string? _sessionId;
    private long _sessionStartedGetTime;
    private FileCursor? _cursor;
    private int _ignored;
    private int _malformed;

    public CommunityPauseLogReader(LocalPinteModOptions options, IClock clock)
    {
        _paths = new BlockALocalPathPolicy(options);
        _clock = clock;
    }

    public Task<CommunityPauseLogSnapshot> ReadAsync(
        SessionManifest? session,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadWorkerAsync(session, cancellationToken), cancellationToken);

    public void Dispose() => _gate.Dispose();

    private async Task<CommunityPauseLogSnapshot> ReadWorkerAsync(
        SessionManifest? session,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (session is null)
            {
                Reset(null, 0);
                return CommunityPauseLogSnapshot.Empty(
                    LocalSourceMetadata.Unavailable("Session active indisponible."));
            }

            if (!string.Equals(_sessionId, session.SessionId, StringComparison.Ordinal))
            {
                Reset(session.SessionId, session.StartedGetTime);
            }

            var path = _paths.ResolveFixed(BlockALocalFile.CommunityPauseLog);
            var source = _paths.GetSourceLabel(BlockALocalFile.CommunityPauseLog);
            if (!File.Exists(path))
            {
                return new(_events.ToArray(), new(
                    LocalReadStatus.Missing,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    source,
                    "Journal Community Pause absent."), _ignored, _malformed);
            }

            var info = new FileInfo(path);
            info.Refresh();
            var timestamp = new DateTimeOffset(DateTime.SpecifyKind(info.LastWriteTimeUtc, DateTimeKind.Utc));

            if (_cursor is null)
            {
                _cursor = await FileCursor.AtEndAsync(path, info, cancellationToken).ConfigureAwait(false);
            }
            else if (await IsReplacementAsync(path, info, _cursor, cancellationToken).ConfigureAwait(false))
            {
                _cursor = await FileCursor.AtEndAsync(path, info, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ReadIncrementAsync(path, info, _cursor, cancellationToken).ConfigureAwait(false);
            }

            var age = NonNegativeAge(timestamp);
            return new(_events.OrderByDescending(item => item.SessionElapsed).ToArray(), new(
                LocalReadStatus.Success,
                DataFreshness.Fresh,
                age,
                DataProvenance.LocalFile,
                source,
                $"Journal Community Pause suivi en lecture seule : {_ignored} ligne(s) ignorée(s), {_malformed} malformée(s)."),
                _ignored,
                _malformed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var status = exception switch
            {
                UnauthorizedAccessException => LocalReadStatus.AccessDenied,
                InvalidOperationException => LocalReadStatus.Invalid,
                _ => LocalReadStatus.IoError
            };
            return new(_events.ToArray(), new(
                status,
                _events.Count == 0 ? DataFreshness.Unknown : DataFreshness.Stale,
                null,
                _events.Count == 0 ? DataProvenance.LocalFile : DataProvenance.MemoryCache,
                _paths.GetSourceLabel(BlockALocalFile.CommunityPauseLog),
                _events.Count == 0
                    ? "Lecture du journal Community Pause impossible."
                    : "Derniers événements valides — lecture actuelle indisponible."),
                _ignored,
                _malformed);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ReadIncrementAsync(
        string path,
        FileInfo info,
        FileCursor cursor,
        CancellationToken cancellationToken)
    {
        if (cursor.Position >= info.Length)
        {
            cursor.UpdateIdentity(info, await ComputePrefixHashAsync(path, cursor.PrefixLength, cancellationToken).ConfigureAwait(false));
            return;
        }

        var available = Math.Min(info.Length - cursor.Position, MaximumBytesPerRead);
        if (info.Length - cursor.Position > MaximumBytesPerRead)
        {
            cursor.Position = info.Length - MaximumBytesPerRead;
            available = MaximumBytesPerRead;
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
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
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
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Length > MaximumLineLength)
            {
                _malformed++;
                continue;
            }

            ProcessLine(line);
        }

        cursor.Position += Encoding.UTF8.GetByteCount(completeText);
        cursor.PrefixLength = (int)Math.Min(256, info.Length);
        cursor.UpdateIdentity(
            info,
            await ComputePrefixHashAsync(path, cursor.PrefixLength, cancellationToken).ConfigureAwait(false));
    }

    private async Task<bool> IsReplacementAsync(
        string path,
        FileInfo info,
        FileCursor cursor,
        CancellationToken cancellationToken)
    {
        if (info.Length < cursor.Position ||
            info.LastWriteTimeUtc < cursor.LastWriteTimeUtc ||
            info.CreationTimeUtc != cursor.CreationTimeUtc)
        {
            return true;
        }

        if (cursor.PrefixHash is null || cursor.PrefixLength == 0)
        {
            return false;
        }

        var current = await ComputePrefixHashAsync(path, cursor.PrefixLength, cancellationToken).ConfigureAwait(false);
        return !CryptographicOperations.FixedTimeEquals(cursor.PrefixHash, current);
    }

    private void ProcessLine(string line)
    {
        var match = ManagedEventLine().Match(line);
        if (!match.Success ||
            !long.TryParse(match.Groups["time"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var time))
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
        var details = PublicFieldNames
            .Where(fields.ContainsKey)
            .Select(key => $"{PublicFieldLabels.GetValueOrDefault(key, key)} : {LogPrivacyFilter.SanitizeDisplayText(fields[key], 64)}")
            .Take(5)
            .ToArray();

        _events.Add(new(
            DateTimeOffset.UnixEpoch,
            "PAUSE",
            TitleFor(eventName),
            details.Length == 0 ? "Événement Community Pause" : string.Join(" · ", details),
            SeverityFor(eventName))
        {
            SessionElapsed = TimeSpan.FromMilliseconds(Math.Max(0, time - _sessionStartedGetTime)),
            Provenance = DataProvenance.LocalFile,
            SourceLabel = "pause.log"
        });

        if (_events.Count > MaximumCachedEvents)
        {
            _events.RemoveRange(0, _events.Count - MaximumCachedEvents);
        }
    }

    private static Dictionary<string, string> ParseFields(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(32))
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

    private static string TitleFor(string eventName) => eventName switch
    {
        "PAUSE_START" => "Partie mise en pause",
        "PAUSE_END" => "Partie reprise",
        "PAUSE_COUNT" => "Compteur de pauses mis à jour",
        "PAUSE_VOTE_START" => "Vote de pause lancé",
        "RESUME_VOTE_START" => "Vote de reprise lancé",
        "VOTE_RESULT" => "Résultat du vote de pause",
        "STATUS" => "Statut Community Pause actualisé",
        _ => "Événement Community Pause"
    };

    private static EventSeverity SeverityFor(string eventName) => eventName switch
    {
        "PAUSE_START" or "PAUSE_VOTE_START" => EventSeverity.Warning,
        "PAUSE_END" => EventSeverity.Success,
        _ => EventSeverity.Information
    };

    private void Reset(string? sessionId, long startedGetTime)
    {
        _sessionId = sessionId;
        _sessionStartedGetTime = startedGetTime;
        _cursor = null;
        _events.Clear();
        _ignored = 0;
        _malformed = 0;
    }

    private TimeSpan NonNegativeAge(DateTimeOffset timestamp)
    {
        var age = _clock.UtcNow - timestamp;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static async Task<byte[]> ComputePrefixHashAsync(
        string path,
        int length,
        CancellationToken cancellationToken)
    {
        if (length <= 0)
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
        var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        return SHA256.HashData(buffer.AsSpan(0, read));
    }

    private sealed class FileCursor
    {
        public long Position { get; set; }
        public int PrefixLength { get; set; }
        public DateTime LastWriteTimeUtc { get; private set; }
        public DateTime CreationTimeUtc { get; private set; }
        public byte[]? PrefixHash { get; private set; }

        public static async Task<FileCursor> AtEndAsync(
            string path,
            FileInfo info,
            CancellationToken cancellationToken)
        {
            var prefixLength = (int)Math.Min(256, info.Length);
            var cursor = new FileCursor
            {
                Position = info.Length,
                PrefixLength = prefixLength
            };
            cursor.UpdateIdentity(
                info,
                await ComputePrefixHashAsync(path, prefixLength, cancellationToken).ConfigureAwait(false));
            return cursor;
        }

        public void UpdateIdentity(FileInfo info, byte[] prefixHash)
        {
            LastWriteTimeUtc = info.LastWriteTimeUtc;
            CreationTimeUtc = info.CreationTimeUtc;
            PrefixHash = prefixHash;
        }
    }

    private static readonly HashSet<string> AllowedEvents = new(StringComparer.Ordinal)
    {
        "PAUSE_START", "PAUSE_END", "PAUSE_COUNT", "PAUSE_VOTE_START",
        "RESUME_VOTE_START", "VOTE_RESULT", "STATUS"
    };

    private static readonly string[] PublicFieldNames =
    [
        "source", "reason", "active_players", "successful_before", "successful",
        "active", "remaining", "proposals", "yes", "no", "majority", "required", "initiator"
    ];

    private static readonly IReadOnlyDictionary<string, string> PublicFieldLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "Source", ["reason"] = "Raison", ["active_players"] = "Joueurs actifs",
            ["successful_before"] = "Pauses avant", ["successful"] = "Pauses", ["active"] = "Active",
            ["remaining"] = "Restant", ["proposals"] = "Propositions", ["yes"] = "Oui", ["no"] = "Non",
            ["majority"] = "Majorité", ["required"] = "Votants", ["initiator"] = "Initiateur"
        };

    [GeneratedRegex(@"^\[(?<time>\d+)\]\s*(?<event>[A-Z][A-Z0-9_]+)\s*(?:\|\s*(?<fields>.*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex ManagedEventLine();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeFieldName();
}
