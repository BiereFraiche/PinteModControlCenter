using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed partial class CommunityPauseStatusReader : ICommunityPauseStatusReader, IDisposable
{
    private const int MaximumFileSizeBytes = 16 * 1024;
    private const int MaximumAttempts = 3;
    private readonly BlockALocalPathPolicy _paths;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CommunityPauseStatusSnapshot? _cached;
    private DateTimeOffset? _cachedTimestampUtc;

    public CommunityPauseStatusReader(LocalPinteModOptions options, IClock clock)
    {
        _paths = new BlockALocalPathPolicy(options);
        _clock = clock;
    }

    public Task<LocalReadResult<CommunityPauseStatusSnapshot>> ReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadWorkerAsync(cancellationToken), cancellationToken);

    public void Dispose() => _gate.Dispose();

    private async Task<LocalReadResult<CommunityPauseStatusSnapshot>> ReadWorkerAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = _paths.ResolveFixed(BlockALocalFile.CommunityPauseFeedback);
            var source = _paths.GetSourceLabel(BlockALocalFile.CommunityPauseFeedback);
            var failure = LocalReadStatus.IoError;
            var failureMessage = "Lecture du statut Community Pause impossible.";
            DateTimeOffset? failureTimestamp = null;

            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!File.Exists(path))
                    {
                        return Unavailable(LocalReadStatus.Missing, source, "Aucun statut Community Pause disponible.");
                    }

                    var before = new FileInfo(path);
                    before.Refresh();
                    failureTimestamp = UtcTimestamp(before.LastWriteTimeUtc);
                    if (before.Length == 0)
                    {
                        failure = LocalReadStatus.Empty;
                        failureMessage = "Fichier de statut vide.";
                    }
                    else if (before.Length > MaximumFileSizeBytes)
                    {
                        return FromFailure(LocalReadStatus.Invalid, source, "Fichier de statut anormalement volumineux.", failureTimestamp);
                    }
                    else
                    {
                        await using var stream = VerifiedReadOnlyFile.Open(
                            path,
                            FileOptions.Asynchronous | FileOptions.SequentialScan);
                        using var memory = new MemoryStream((int)before.Length);
                        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                        var after = new FileInfo(path);
                        after.Refresh();
                        failureTimestamp = UtcTimestamp(after.LastWriteTimeUtc);

                        if (before.Length != after.Length ||
                            before.LastWriteTimeUtc != after.LastWriteTimeUtc ||
                            memory.Length > MaximumFileSizeBytes)
                        {
                            failure = LocalReadStatus.Invalid;
                            failureMessage = "Fichier modifié pendant la lecture.";
                        }
                        else
                        {
                            var status = Parse(Encoding.UTF8.GetString(memory.ToArray()));
                            _cached = status;
                            _cachedTimestampUtc = failureTimestamp;
                            var age = Age(failureTimestamp);
                            return new(status, new(
                                LocalReadStatus.Success,
                                HeartbeatFreshnessPolicy.Evaluate(age ?? TimeSpan.MaxValue),
                                age,
                                DataProvenance.LocalFile,
                                source,
                                "Statut Community Pause lu avec succès."), failureTimestamp);
                        }
                    }
                }
                catch (PauseStatusValidationException exception)
                {
                    failure = LocalReadStatus.Invalid;
                    failureMessage = exception.PublicMessage;
                }
                catch (LocalFileAccessRefusedException)
                {
                    return FromFailure(LocalReadStatus.AccessDenied, source, "Source Community Pause refusée.", failureTimestamp);
                }
                catch (UnauthorizedAccessException)
                {
                    return FromFailure(LocalReadStatus.AccessDenied, source, "Accès au statut Community Pause refusé.", failureTimestamp);
                }
                catch (IOException)
                {
                    failure = LocalReadStatus.IoError;
                    failureMessage = "Lecture du statut Community Pause impossible.";
                }

                if (attempt < MaximumAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(35), cancellationToken).ConfigureAwait(false);
                }
            }

            return FromFailure(failure, source, failureMessage, failureTimestamp);
        }
        finally
        {
            _gate.Release();
        }
    }

    private LocalReadResult<CommunityPauseStatusSnapshot> FromFailure(
        LocalReadStatus status,
        string source,
        string message,
        DateTimeOffset? sourceTimestampUtc)
    {
        if (_cached is null)
        {
            return Unavailable(status, source, message, sourceTimestampUtc);
        }

        return new(_cached, new(
            status,
            DataFreshness.Stale,
            Age(_cachedTimestampUtc),
            DataProvenance.MemoryCache,
            source,
            "Dernière donnée valide — lecture actuelle indisponible."), _cachedTimestampUtc);
    }

    private static LocalReadResult<CommunityPauseStatusSnapshot> Unavailable(
        LocalReadStatus status,
        string source,
        string message,
        DateTimeOffset? sourceTimestampUtc = null) =>
        new(null, new(status, DataFreshness.Unknown, null, DataProvenance.LocalFile, source, message), sourceTimestampUtc);

    private TimeSpan? Age(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
        {
            return null;
        }

        var age = _clock.UtcNow - timestamp.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static DateTimeOffset UtcTimestamp(DateTime timestamp) =>
        new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

    private static CommunityPauseStatusSnapshot Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 15 || lines[0] != "PINTEMOD_REMOTE_FEEDBACK_V1" ||
            lines[1] != "command=ezzpausestatus" || lines[3] != "---" ||
            !lines.Any(line => line == "END"))
        {
            throw new PauseStatusValidationException("Format Community Pause incomplet ou non reconnu.");
        }

        var moduleMatch = ModuleLine().Match(lines[4]);
        if (!moduleMatch.Success)
        {
            throw new PauseStatusValidationException("Version Community Pause absente ou invalide.");
        }

        var generated = ParseLongValue(lines[2], "generated_gettime=", 0, long.MaxValue);
        var active = ParseBooleanValue(RequiredLine(lines, "Active: "), "Active: ");
        var remaining = ParseIntWithSuffix(RequiredLine(lines, "Automatic resume in: "), "Automatic resume in: ", "s", 0, 180);
        var successful = ParseRatio(RequiredLine(lines, "Successful pauses: "), "Successful pauses: ");
        var proposals = ParseIntValue(RequiredLine(lines, "Pause proposals used: "), "Pause proposals used: ", 0, 10000);
        var activeVote = ParseActiveVote(RequiredLine(lines, "Active vote: "));
        var temporaryGodMode = ParseOnOff(RequiredLine(lines, "Temporary God Mode: "), "Temporary God Mode: ");
        var spectatorSpawnGuard = ParseOnOff(RequiredLine(lines, "Spectator spawn guard: "), "Spectator spawn guard: ");
        var newAiSpawningBlocked = ParseAiState(RequiredLine(lines, "New AI spawning: "));

        return new(
            moduleMatch.Groups["version"].Value,
            generated,
            active,
            remaining,
            successful.Current,
            successful.Maximum,
            proposals,
            activeVote.Kind,
            activeVote.Yes,
            activeVote.No,
            activeVote.Majority,
            temporaryGodMode,
            spectatorSpawnGuard,
            newAiSpawningBlocked);
    }

    private static string RequiredLine(IEnumerable<string> lines, string prefix) =>
        lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))
        ?? throw new PauseStatusValidationException($"Champ Community Pause manquant : {prefix.Trim()}");

    private static bool ParseBooleanValue(string line, string prefix)
    {
        var value = line[prefix.Length..];
        return value switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new PauseStatusValidationException($"Valeur booléenne invalide : {prefix.Trim()}")
        };
    }

    private static bool ParseOnOff(string line, string prefix)
    {
        var value = line[prefix.Length..];
        return value switch
        {
            "ON" => true,
            "OFF" => false,
            _ => throw new PauseStatusValidationException($"État invalide : {prefix.Trim()}")
        };
    }

    private static bool ParseAiState(string line)
    {
        const string prefix = "New AI spawning: ";
        return line[prefix.Length..] switch
        {
            "blocked" => true,
            "normal" => false,
            _ => throw new PauseStatusValidationException("État de création IA invalide.")
        };
    }

    private static int ParseIntWithSuffix(string line, string prefix, string suffix, int minimum, int maximum)
    {
        var value = line[prefix.Length..];
        if (!value.EndsWith(suffix, StringComparison.Ordinal))
        {
            throw new PauseStatusValidationException($"Valeur invalide : {prefix.Trim()}");
        }

        return ParseInt(value[..^suffix.Length], minimum, maximum, prefix);
    }

    private static int ParseIntValue(string line, string prefix, int minimum, int maximum) =>
        ParseInt(line[prefix.Length..], minimum, maximum, prefix);

    private static long ParseLongValue(string line, string prefix, long minimum, long maximum)
    {
        if (!line.StartsWith(prefix, StringComparison.Ordinal) ||
            !long.TryParse(line[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value < minimum || value > maximum)
        {
            throw new PauseStatusValidationException($"Valeur invalide : {prefix.Trim()}");
        }

        return value;
    }

    private static int ParseInt(string value, int minimum, int maximum, string field)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ||
            result < minimum || result > maximum)
        {
            throw new PauseStatusValidationException($"Valeur invalide : {field.Trim()}");
        }

        return result;
    }

    private static (int Current, int Maximum) ParseRatio(string line, string prefix)
    {
        var parts = line[prefix.Length..].Split('/');
        if (parts.Length != 2)
        {
            throw new PauseStatusValidationException("Compteur de pauses invalide.");
        }

        var current = ParseInt(parts[0], 0, 100, prefix);
        var maximum = ParseInt(parts[1], 1, 100, prefix);
        if (current > maximum)
        {
            throw new PauseStatusValidationException("Compteur de pauses incohérent.");
        }

        return (current, maximum);
    }

    private static (string Kind, int? Yes, int? No, int? Majority) ParseActiveVote(string line)
    {
        const string prefix = "Active vote: ";
        var value = line[prefix.Length..];
        if (value == "none")
        {
            return ("Aucun", null, null, null);
        }

        var match = VoteLine().Match(value);
        if (!match.Success)
        {
            throw new PauseStatusValidationException("Vote Community Pause invalide.");
        }

        var kind = match.Groups["kind"].Value == "pause" ? "Pause" : "Reprise";
        return (
            kind,
            ParseInt(match.Groups["yes"].Value, 0, 100, "YES"),
            ParseInt(match.Groups["no"].Value, 0, 100, "NO"),
            ParseInt(match.Groups["majority"].Value, 1, 100, "majority"));
    }

    [GeneratedRegex(@"^PinteMod Community Pause - EXPERIMENTAL v(?<version>[0-9]+(?:\.[0-9]+){1,3})$", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleLine();

    [GeneratedRegex(@"^(?<kind>pause|resume) \| YES=(?<yes>\d+) \| NO=(?<no>\d+) \| majority=(?<majority>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex VoteLine();

    private sealed class PauseStatusValidationException(string message) : Exception(message)
    {
        public string PublicMessage { get; } = message;
    }
}
