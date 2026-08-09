namespace PinteMod.ControlCenter.Infrastructure.Local;

internal sealed class EasterEggRecordsPathPolicy(LocalPinteModOptions options)
{
    private static readonly string RootRelativePath =
        "easter_eggs_v2";

    private static readonly string ProfilesRelativePath =
        Path.Combine(RootRelativePath, "profiles.json");

    private static readonly string MapsRelativePath =
        Path.Combine(RootRelativePath, "maps");

    public string ResolveProfilesPath()
    {
        var candidate = Path.GetFullPath(Path.Combine(options.DataRoot, ProfilesRelativePath));
        EnsureUnderRoot(candidate);
        RejectExistingReparsePoints(candidate);
        return candidate;
    }

    public string ResolveMapsDirectory()
    {
        var candidate = Path.GetFullPath(Path.Combine(options.DataRoot, MapsRelativePath));
        EnsureUnderRoot(candidate);
        RejectExistingReparsePoints(candidate);
        return candidate;
    }

    public string ValidateProfilesPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var candidate = Path.GetFullPath(path);
        var authorizedPath = ResolveProfilesPath();
        if (!string.Equals(candidate, authorizedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Le profil Easter Egg demandé n’est pas la source active autorisée.");
        }

        EnsureUnderRoot(candidate);
        RejectExistingReparsePoints(candidate);
        return candidate;
    }

    public IReadOnlyList<string> EnumerateActiveMapJsonFiles()
    {
        var authorizedDirectory = ResolveMapsDirectory();
        if (!Directory.Exists(authorizedDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(authorizedDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(IsActiveJsonFileName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ValidateMapFilePath)
            .ToArray();
    }

    public string ValidateMapFilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var authorizedDirectory = ResolveMapsDirectory();
        var candidate = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(candidate), authorizedDirectory, StringComparison.OrdinalIgnoreCase) ||
            !IsActiveJsonFileName(candidate))
        {
            throw new InvalidOperationException("Le fichier Easter Egg demandé n’est pas une source officielle active autorisée.");
        }

        EnsureUnderRoot(candidate);
        RejectExistingReparsePoints(candidate);
        return candidate;
    }

    public static string SourceLabel =>
        "boiii/scriptdata/pintemod/easter_eggs_v2/profiles.json + " +
        "boiii/scriptdata/pintemod/easter_eggs_v2/maps/*.json";

    private static bool IsActiveJsonFileName(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(path);
        return !stem.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
               !stem.EndsWith(".bak", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureUnderRoot(string candidate)
    {
        var requiredPrefix = options.ServerRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La source Easter Egg sort de ServerRoot.");
        }
    }

    private void RejectExistingReparsePoints(string targetPath)
    {
        var current = options.ServerRoot;
        var relative = Path.GetRelativePath(options.ServerRoot, targetPath);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Les liens et jonctions ne sont pas autorisés : {current}");
            }
        }
    }
}
