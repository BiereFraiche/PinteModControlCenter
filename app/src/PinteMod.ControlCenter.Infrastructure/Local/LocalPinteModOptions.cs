namespace PinteMod.ControlCenter.Infrastructure.Local;

public enum LocalPinteModFile
{
    CurrentSession,
    PinteModHeartbeat,
    ControlCenterRuntimeSnapshot,
    SupervisorHeartbeat,
    BanServiceHeartbeat,
    GeoIpBridgeHeartbeat,
    LiveConsoleHeartbeat
}

public enum LocalPinteModRootLayout
{
    ServerRoot,
    PinteModDataRoot
}

public sealed class LocalPinteModOptions
{
    private static readonly IReadOnlyDictionary<LocalPinteModFile, string> RelativePaths =
        new Dictionary<LocalPinteModFile, string>
        {
            [LocalPinteModFile.CurrentSession] = Path.Combine("logs", "current_session.json"),
            [LocalPinteModFile.PinteModHeartbeat] = Path.Combine("health", "pintemod.json"),
            [LocalPinteModFile.ControlCenterRuntimeSnapshot] = Path.Combine("runtime", "control_center_snapshot.json"),
            [LocalPinteModFile.SupervisorHeartbeat] = Path.Combine("health", "supervisor.json"),
            [LocalPinteModFile.BanServiceHeartbeat] = Path.Combine("health", "ban_service.json"),
            [LocalPinteModFile.GeoIpBridgeHeartbeat] = Path.Combine("health", "geoip_bridge.json"),
            [LocalPinteModFile.LiveConsoleHeartbeat] = Path.Combine("health", "live_console.json")
        };

    public LocalPinteModOptions(
        string serverRoot,
        LocalPinteModRootLayout rootLayout = LocalPinteModRootLayout.ServerRoot)
    {
        if (string.IsNullOrWhiteSpace(serverRoot))
        {
            throw new ArgumentException("ServerRoot est obligatoire en mode hybride local.", nameof(serverRoot));
        }

        if (!Path.IsPathFullyQualified(serverRoot))
        {
            throw new ArgumentException("ServerRoot doit être un chemin absolu.", nameof(serverRoot));
        }

        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(serverRoot));
        if (!IsSupportedRootShape(normalized, rootLayout))
        {
            throw new ArgumentException("La racine d’un volume ne peut pas être utilisée comme ServerRoot.", nameof(serverRoot));
        }

        if (!Directory.Exists(normalized))
        {
            throw new DirectoryNotFoundException($"ServerRoot n’existe pas : {normalized}");
        }

        RejectReparsePoint(normalized);
        ServerRoot = normalized;
        RootLayout = rootLayout;
        DataRoot = rootLayout == LocalPinteModRootLayout.PinteModDataRoot
            ? normalized
            : Path.Combine(normalized, "boiii", "scriptdata", "pintemod");
    }

    public string ServerRoot { get; }

    public string DataRoot { get; }

    public LocalPinteModRootLayout RootLayout { get; }

    internal static bool IsSupportedRootShape(
        string normalizedRoot,
        LocalPinteModRootLayout rootLayout)
    {
        var volumeRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(normalizedRoot) ?? string.Empty);
        if (!string.Equals(normalizedRoot, volumeRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Une racine de partage UNC (\\serveur\PinteModData) est précisément la racine
        // de données autorisée en mode LAN. Les racines de volume locales restent refusées.
        if (rootLayout != LocalPinteModRootLayout.PinteModDataRoot ||
            !normalizedRoot.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return false;
        }

        return normalizedRoot[2..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Length >= 2;
    }

    public string ResolvePath(LocalPinteModFile file)
    {
        if (!RelativePaths.TryGetValue(file, out var relativePath))
        {
            throw new ArgumentOutOfRangeException(nameof(file), file, "Source locale non autorisée.");
        }

        if (Path.IsPathFullyQualified(relativePath) ||
            relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is ".." or "."))
        {
            throw new InvalidOperationException("Le chemin local autorisé est invalide.");
        }

        var candidate = Path.GetFullPath(Path.Combine(DataRoot, relativePath));
        var requiredPrefix = ServerRoot + Path.DirectorySeparatorChar;
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

    public static string GetSourceLabel(LocalPinteModFile file) =>
        RelativePaths.TryGetValue(file, out var relativePath)
            ? "boiii/scriptdata/pintemod/" + relativePath.Replace(Path.DirectorySeparatorChar, '/')
            : throw new ArgumentOutOfRangeException(nameof(file), file, "Source locale non autorisée.");

    private void RejectExistingReparsePoints(string targetPath)
    {
        var current = ServerRoot;
        var relative = Path.GetRelativePath(ServerRoot, targetPath);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) || File.Exists(current))
            {
                RejectReparsePoint(current);
            }
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException($"Les liens et jonctions ne sont pas autorisés dans ServerRoot : {path}");
        }
    }
}
