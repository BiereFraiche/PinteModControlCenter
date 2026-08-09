using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class LocalPlayerModerationHistoryReader(
    LocalPinteModOptions options,
    IClock clock) : IPlayerModerationHistoryReader
{
    private const int MaximumHistoryBytes = 64 * 1024;

    public async Task<LocalReadResult<PlayerModerationHistory>> ReadAsync(
        string xuid,
        CancellationToken cancellationToken = default)
    {
        if (!XuidValidator.IsValid(xuid))
        {
            return Failure(LocalReadStatus.Invalid, "Identité XUID invalide.");
        }

        string path;
        try
        {
            path = ResolvePath(xuid);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Failure(LocalReadStatus.AccessDenied, "Chemin d’historique refusé.");
        }

        try
        {
            if (!File.Exists(path))
            {
                return Failure(LocalReadStatus.Missing, "Aucun historique de modération local pour ce joueur.");
            }

            var info = new FileInfo(path);
            if (info.Length == 0)
            {
                return Failure(LocalReadStatus.Empty, "Le fichier d’historique est vide.", info.LastWriteTimeUtc);
            }

            if (info.Length > MaximumHistoryBytes)
            {
                return Failure(LocalReadStatus.Invalid, "Le fichier d’historique dépasse la limite autorisée.", info.LastWriteTimeUtc);
            }

            await using var stream = VerifiedReadOnlyFile.Open(
                path,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var document = await JsonDocument.ParseAsync(
                stream,
                new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow },
                cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure(LocalReadStatus.Invalid, "La racine de l’historique doit être un objet JSON.", info.LastWriteTimeUtc);
            }

            var root = document.RootElement;
            if (!TryReadInt(root, "schema_version", out var schemaVersion) || schemaVersion != 1)
            {
                return Failure(LocalReadStatus.UnsupportedSchema, "Schéma d’historique non pris en charge.", info.LastWriteTimeUtc);
            }

            if (!TryReadString(root, "identity_kind", out var identityKind) ||
                !string.Equals(identityKind, "BOIII_XUID", StringComparison.Ordinal) ||
                !TryReadString(root, "xuid", out var storedXuid) ||
                !string.Equals(storedXuid, xuid, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(LocalReadStatus.Invalid, "L’identité de l’historique ne correspond pas au joueur sélectionné.", info.LastWriteTimeUtc);
            }

            if (!TryReadNonNegativeInt(root, "kicks", out var kicks) ||
                !TryReadNonNegativeInt(root, "mutes", out var mutes) ||
                !TryReadNonNegativeInt(root, "temporary_bans", out var temporaryBans) ||
                !TryReadNonNegativeInt(root, "permanent_bans", out var permanentBans) ||
                !TryReadNonNegativeInt(root, "unbans", out var unbans))
            {
                return Failure(LocalReadStatus.Invalid, "Les compteurs de l’historique sont invalides.", info.LastWriteTimeUtc);
            }

            TryReadString(root, "last_action", out var lastAction);
            TryReadString(root, "last_reason", out var lastReason);
            var value = new PlayerModerationHistory(
                kicks,
                mutes,
                temporaryBans,
                permanentBans,
                unbans,
                LogPrivacyFilter.SanitizeDisplayText(NormalizeDisplay(lastAction, "Aucune"), 80),
                LogPrivacyFilter.SanitizeDisplayText(NormalizeDisplay(lastReason, "Aucune raison"), 180));
            var timestamp = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            return new LocalReadResult<PlayerModerationHistory>(
                value,
                new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    Age(timestamp),
                    DataProvenance.LocalFile,
                    "moderation/history/<XUID neutralisé>.json",
                    "Historique local lu avec succès."),
                timestamp);
        }
        catch (JsonException)
        {
            return Failure(LocalReadStatus.Invalid, "Historique JSON incomplet ou invalide.", LastWrite(path));
        }
        catch (LocalFileAccessRefusedException)
        {
            return Failure(LocalReadStatus.AccessDenied, "Source d’historique local refusée.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(LocalReadStatus.AccessDenied, "Accès refusé à l’historique local.", LastWrite(path));
        }
        catch (IOException)
        {
            return Failure(LocalReadStatus.IoError, "L’historique local est momentanément illisible.", LastWrite(path));
        }
    }

    private string ResolvePath(string xuid)
    {
        var historyRoot = Path.GetFullPath(Path.Combine(options.DataRoot, "moderation", "history"));
        var requiredDataPrefix = Path.TrimEndingDirectorySeparator(options.DataRoot) + Path.DirectorySeparatorChar;
        if (!historyRoot.StartsWith(requiredDataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Historique hors racine.");
        }

        var path = Path.GetFullPath(Path.Combine(historyRoot, xuid.ToLowerInvariant() + ".json"));
        var requiredHistoryPrefix = Path.TrimEndingDirectorySeparator(historyRoot) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(requiredHistoryPrefix, StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Historique hors dossier autorisé.");
        }

        RejectExistingReparsePoints(path);
        return path;
    }

    private void RejectExistingReparsePoints(string targetPath)
    {
        var current = options.ServerRoot;
        foreach (var segment in Path.GetRelativePath(options.ServerRoot, targetPath)
                     .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Lien ou jonction refusé.");
            }
        }
    }

    private static bool TryReadNonNegativeInt(JsonElement root, string name, out int value) =>
        TryReadInt(root, name, out value) && value >= 0;

    private static bool TryReadInt(JsonElement root, string name, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(property.GetString(), out value),
            _ => false
        };
    }

    private static bool TryReadString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static string NormalizeDisplay(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return new string(value.Trim().Where(character => !char.IsControl(character)).ToArray());
    }

    private TimeSpan Age(DateTimeOffset timestamp)
    {
        var age = clock.UtcNow - timestamp;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private LocalReadResult<PlayerModerationHistory> Failure(
        LocalReadStatus status,
        string message,
        DateTime? lastWriteUtc = null)
    {
        DateTimeOffset? timestamp = lastWriteUtc is { } value
            ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
            : null;
        return new LocalReadResult<PlayerModerationHistory>(
            null,
            new LocalSourceMetadata(
                status,
                DataFreshness.Unknown,
                timestamp is null ? null : Age(timestamp.Value),
                DataProvenance.Unavailable,
                "moderation/history/<XUID neutralisé>.json",
                message),
            timestamp);
    }

    private static DateTime? LastWrite(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
