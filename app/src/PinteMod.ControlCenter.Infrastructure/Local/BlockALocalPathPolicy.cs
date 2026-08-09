namespace PinteMod.ControlCenter.Infrastructure.Local;

public enum BlockALocalFile
{
    InstallationVerification,
    BanServiceStatus,
    Roles,
    CommunityPauseFeedback,
    CommunityPauseLog
}

public sealed class BlockALocalPathPolicy
{
    private static readonly IReadOnlyDictionary<BlockALocalFile, string> FixedPaths =
        new Dictionary<BlockALocalFile, string>
        {
            [BlockALocalFile.InstallationVerification] = Path.Combine("diagnostics", "installation_verification.json"),
            [BlockALocalFile.BanServiceStatus] = Path.Combine("bans", "service_status.json"),
            [BlockALocalFile.Roles] = Path.Combine("identity", "roles.json"),
            [BlockALocalFile.CommunityPauseFeedback] = Path.Combine("remote", "feedback.latest.txt"),
            [BlockALocalFile.CommunityPauseLog] = Path.Combine("logs", "pause.log")
        };

    private static readonly HashSet<string> AllowedSessionLogs = new(StringComparer.OrdinalIgnoreCase)
    {
        "connections.log", "community.log", "ranks.log", "easter_eggs.log", "identity.log",
        "moderation.log", "localization.log", "storage.log", "validation.log"
    };

    private readonly string _serverRoot;
    private readonly string _dataRoot;

    public BlockALocalPathPolicy(LocalPinteModOptions options)
    {
        _serverRoot = options.ServerRoot;
        _dataRoot = options.DataRoot;
    }

    public string ResolveFixed(BlockALocalFile file) =>
        ResolveRelativePath(FixedPaths.TryGetValue(file, out var path)
            ? path
            : throw new ArgumentOutOfRangeException(nameof(file), file, "Source Bloc A non autorisée."));

    public string GetSourceLabel(BlockALocalFile file) =>
        FixedPaths.TryGetValue(file, out var path)
            ? "boiii/scriptdata/pintemod/" + path.Replace(Path.DirectorySeparatorChar, '/')
            : throw new ArgumentOutOfRangeException(nameof(file), file, "Source Bloc A non autorisée.");

    public string ResolveSessionLogPath(string sessionId, string fileName)
    {
        if (!IsSafeIdentifier(sessionId, 96))
        {
            throw new InvalidOperationException("Identifiant de session non autorisé.");
        }

        if (!AllowedSessionLogs.Contains(fileName))
        {
            throw new InvalidOperationException("Famille de log non autorisée.");
        }

        return ResolveRelativePath(Path.Combine("logs", "sessions", sessionId, fileName));
    }

    public string ResolveLocalizationDirectory(bool manual) =>
        ResolveRelativePath(Path.Combine("localization", manual ? "manual" : "auto"));

    public string ResolveLocalizationFilePath(bool manual, string xuidFileName)
    {
        var xuid = Path.GetFileNameWithoutExtension(xuidFileName);
        if (!xuidFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            xuid.Length != 16 || !xuid.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("Fichier de langue non autorisé.");
        }

        return ResolveRelativePath(Path.Combine(
            "localization", manual ? "manual" : "auto", xuidFileName));
    }

    public static IReadOnlyCollection<string> GetAllowedSessionLogNames() => AllowedSessionLogs;

    private string ResolveRelativePath(string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath) ||
            relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is ".." or "."))
        {
            throw new InvalidOperationException("Le chemin local autorisé est invalide.");
        }

        var candidate = Path.GetFullPath(Path.Combine(_dataRoot, relativePath));
        var requiredPrefix = _serverRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La source locale sort de ServerRoot.");
        }

        if (candidate.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            candidate.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Les fichiers temporaires et de sauvegarde ne sont pas des sources actives.");
        }

        RejectExistingReparsePoints(candidate);
        return candidate;
    }

    private void RejectExistingReparsePoints(string targetPath)
    {
        var current = _serverRoot;
        foreach (var segment in Path.GetRelativePath(_serverRoot, targetPath)
                     .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Les liens et jonctions ne sont pas autorisés dans ServerRoot : {current}");
            }
        }
    }

    private static bool IsSafeIdentifier(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}
