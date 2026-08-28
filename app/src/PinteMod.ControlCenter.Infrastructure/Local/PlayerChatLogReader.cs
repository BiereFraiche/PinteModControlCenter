using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed partial class PlayerChatLogReader : IPlayerChatLogReader, IDisposable
{
    private const int MaximumBytesPerRead = 2 * 1024 * 1024;
    private const int MaximumLineLength = 2048;
    private readonly BlockALocalPathPolicy _paths;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, FileCursor> _cursors = new(StringComparer.OrdinalIgnoreCase);

    public PlayerChatLogReader(LocalPinteModOptions options, IClock clock)
    {
        _paths = new BlockALocalPathPolicy(options);
        _clock = clock;
    }

    public async Task<PlayerChatReadResult> ReadAsync(
        string? sessionId,
        string? mapCode,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(mapCode))
            {
                return PlayerChatReadResult.Empty(
                    LocalSourceMetadata.Unavailable("Session active indisponible pour le chat joueurs."));
            }

            if (!MapCodeValidator.TryNormalize(mapCode, out var normalizedMapCode))
            {
                return PlayerChatReadResult.Empty(new LocalSourceMetadata(
                    LocalReadStatus.Invalid,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    "Chat joueurs",
                    "Code carte invalide pour la source chat."));
            }

            var path = _paths.ResolveSessionChatLogPath(sessionId);
            var sourceLabel = BlockALocalPathPolicy.GetSessionChatLogSourceLabel(sessionId);
            if (!File.Exists(path))
            {
                return PlayerChatReadResult.Empty(new LocalSourceMetadata(
                    LocalReadStatus.Missing,
                    DataFreshness.Unknown,
                    null,
                    DataProvenance.LocalFile,
                    sourceLabel,
                    "Aucun journal chat joueur disponible pour la session active."));
            }

            if (!_cursors.TryGetValue(path, out var cursor))
            {
                cursor = new FileCursor();
                _cursors[path] = cursor;
            }

            await using var stream = VerifiedReadOnlyFile.Open(
                path,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var metadata = VerifiedReadOnlyFile.GetMetadata(stream);
            if (metadata.Length <= 0)
            {
                cursor.Position = 0;
                cursor.LastWriteTimeUtc = metadata.LastWriteTimeUtc;
                return PlayerChatReadResult.Empty(new LocalSourceMetadata(
                    LocalReadStatus.Empty,
                    DataFreshness.Fresh,
                    NonNegativeAge(metadata.LastWriteTimeUtc),
                    DataProvenance.LocalFile,
                    sourceLabel,
                    "Le journal chat joueur est vide."));
            }

            if (metadata.Length < cursor.Position || metadata.LastWriteTimeUtc < cursor.LastWriteTimeUtc)
            {
                cursor.Position = 0;
            }

            if (cursor.Position >= metadata.Length)
            {
                cursor.LastWriteTimeUtc = metadata.LastWriteTimeUtc;
                return PlayerChatReadResult.Empty(new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    NonNegativeAge(metadata.LastWriteTimeUtc),
                    DataProvenance.LocalFile,
                    sourceLabel,
                    "Journal chat joueur à jour."));
            }

            var ignored = 0;
            var malformed = 0;
            var startPosition = cursor.Position;
            var available = metadata.Length - startPosition;
            var truncatedStart = available > MaximumBytesPerRead;
            if (truncatedStart)
            {
                startPosition = metadata.Length - MaximumBytesPerRead;
                available = MaximumBytesPerRead;
                ignored++;
            }

            stream.Position = startPosition;
            var buffer = new byte[(int)available];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                cursor.LastWriteTimeUtc = metadata.LastWriteTimeUtc;
                return PlayerChatReadResult.Empty(new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    NonNegativeAge(metadata.LastWriteTimeUtc),
                    DataProvenance.LocalFile,
                    sourceLabel,
                    "Journal chat joueur à jour."));
            }

            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var firstLineOffset = 0;
            if (truncatedStart)
            {
                var firstNewLine = text.IndexOf('\n');
                if (firstNewLine < 0)
                {
                    cursor.Position = metadata.Length;
                    cursor.LastWriteTimeUtc = metadata.LastWriteTimeUtc;
                    return new PlayerChatReadResult(
                        [],
                        new LocalSourceMetadata(
                            LocalReadStatus.Success,
                            DataFreshness.Fresh,
                            NonNegativeAge(metadata.LastWriteTimeUtc),
                            DataProvenance.LocalFile,
                            sourceLabel,
                            "Journal chat joueur lu avec troncature de sécurité."),
                        ignored + 1,
                        malformed);
                }

                firstLineOffset = firstNewLine + 1;
            }

            var lastNewLine = text.LastIndexOf('\n');
            if (lastNewLine < firstLineOffset)
            {
                return PlayerChatReadResult.Empty(new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    NonNegativeAge(metadata.LastWriteTimeUtc),
                    DataProvenance.LocalFile,
                    sourceLabel,
                    "Dernière ligne chat encore incomplète."));
            }

            var completeText = text[firstLineOffset..(lastNewLine + 1)];
            var parsed = new List<ParsedChatLine>();
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
                    malformed++;
                    continue;
                }

                if (TryParseLine(line, out var parsedLine))
                {
                    parsed.Add(parsedLine);
                }
                else
                {
                    ignored++;
                }
            }

            var consumedBytes = Encoding.UTF8.GetByteCount(text[..(lastNewLine + 1)]);
            cursor.Position = startPosition + consumedBytes;
            cursor.LastWriteTimeUtc = metadata.LastWriteTimeUtc;

            var mapLabel = LogPrivacyFilter.SanitizeChatText(
                OfficialMapNameResolver.Resolve(normalizedMapCode),
                80);
            var messages = MaterializeMessages(
                parsed,
                sessionId,
                normalizedMapCode,
                mapLabel,
                metadata.LastWriteTimeUtc);

            return new PlayerChatReadResult(
                messages,
                new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    NonNegativeAge(metadata.LastWriteTimeUtc),
                    DataProvenance.LocalFile,
                    sourceLabel,
                    messages.Count == 0
                        ? "Aucun nouveau message joueur."
                        : $"{messages.Count} nouveau(x) message(s) joueur lu(s)."),
                ignored,
                malformed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var status = exception switch
            {
                LocalFileAccessRefusedException => LocalReadStatus.AccessDenied,
                UnauthorizedAccessException => LocalReadStatus.AccessDenied,
                InvalidOperationException => LocalReadStatus.Invalid,
                _ => LocalReadStatus.IoError
            };
            return PlayerChatReadResult.Empty(new LocalSourceMetadata(
                status,
                DataFreshness.Stale,
                null,
                DataProvenance.LocalFile,
                "Chat joueurs",
                "Lecture du journal chat joueur impossible."));
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    internal static bool TryParseLine(string line, out ParsedChatLine parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = ChatLinePattern().Match(line);
        if (!match.Success ||
            !long.TryParse(match.Groups["gettime"].Value, out var getTime) ||
            getTime < 0)
        {
            return false;
        }

        var displayName = LogPrivacyFilter.SafeChatPlayerName(match.Groups["name"].Value);
        var message = LogPrivacyFilter.SanitizeChatText(match.Groups["message"].Value, 500);
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        parsed = new ParsedChatLine(getTime, displayName, message);
        return true;
    }

    private static IReadOnlyList<PlayerChatMessage> MaterializeMessages(
        IReadOnlyList<ParsedChatLine> parsed,
        string sessionId,
        string mapCode,
        string mapLabel,
        DateTime fileLastWriteTimeUtc)
    {
        if (parsed.Count == 0)
        {
            return [];
        }

        var latestGetTime = parsed.Max(item => item.GetTime);
        var anchor = new DateTimeOffset(DateTime.SpecifyKind(fileLastWriteTimeUtc, DateTimeKind.Utc));
        return parsed.Select(item =>
        {
            var occurredAtUtc = anchor - TimeSpan.FromMilliseconds(latestGetTime - item.GetTime);
            var eventId = CreateEventId(sessionId, item.GetTime, item.DisplayName, item.Message);
            return new PlayerChatMessage(
                eventId,
                occurredAtUtc,
                item.DisplayName,
                item.Message,
                mapCode,
                mapLabel);
        }).ToArray();
    }

    private static string CreateEventId(
        string sessionId,
        long getTime,
        string displayName,
        string message)
    {
        var payload = Encoding.UTF8.GetBytes($"{sessionId}\n{getTime}\n{displayName}\n{message}");
        return Convert.ToHexString(SHA256.HashData(payload))[..32].ToLowerInvariant();
    }

    private TimeSpan NonNegativeAge(DateTime timestampUtc)
    {
        var age = _clock.UtcNow - new DateTimeOffset(DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc));
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    [GeneratedRegex(
        @"^(?:\[(?:\d{2}:){2}\d{2}\]\[CHAT\]\s*)?\[(?<gettime>\d{1,12})\s+ms\]\[chat\]\s+(?<name>.+?)\s+\[(?:[0-9a-fA-F]{16}|<hidden>|<unavailable>)\]:\s?(?<message>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChatLinePattern();

    internal readonly record struct ParsedChatLine(long GetTime, string DisplayName, string Message);

    private sealed class FileCursor
    {
        public long Position { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }
    }
}
