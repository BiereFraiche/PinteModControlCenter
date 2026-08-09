namespace PinteMod.ControlCenter.Infrastructure.Local;

internal enum RankRecordsDirectory
{
    Players,
    Maps
}

internal sealed class RankRecordsPathPolicy(LocalPinteModOptions options)
{
    private static readonly IReadOnlyDictionary<RankRecordsDirectory, string> RelativeDirectories =
        new Dictionary<RankRecordsDirectory, string>
        {
            [RankRecordsDirectory.Players] = Path.Combine("ranks_v2", "players"),
            [RankRecordsDirectory.Maps] = Path.Combine("ranks_v2", "maps")
        };

    public string ResolveDirectory(RankRecordsDirectory directory)
    {
        if (!RelativeDirectories.TryGetValue(directory, out var relativePath))
        {
            throw new ArgumentOutOfRangeException(nameof(directory), directory, "Dossier Ranks non autorisé.");
        }

        var candidate = Path.GetFullPath(Path.Combine(options.DataRoot, relativePath));
        EnsureUnderRoot(candidate);
        RejectExistingReparsePoints(candidate);
        return candidate;
    }

    public IReadOnlyList<string> EnumerateActiveJsonFiles(RankRecordsDirectory directory)
    {
        var authorizedDirectory = ResolveDirectory(directory);
        if (!Directory.Exists(authorizedDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(authorizedDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(IsActiveJsonFileName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => ValidateFilePath(directory, path))
            .ToArray();
    }

    public string ValidateFilePath(RankRecordsDirectory directory, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var authorizedDirectory = ResolveDirectory(directory);
        var candidate = Path.GetFullPath(path);

        if (!string.Equals(Path.GetDirectoryName(candidate), authorizedDirectory, StringComparison.OrdinalIgnoreCase) ||
            !IsActiveJsonFileName(candidate))
        {
            throw new InvalidOperationException("Le fichier Ranks demandé n’est pas une source active autorisée.");
        }

        EnsureUnderRoot(candidate);
        RejectExistingReparsePoints(candidate);
        return candidate;
    }

    public static string GetSourceLabel(RankRecordsDirectory directory) =>
        RelativeDirectories.TryGetValue(directory, out var relativePath)
            ? "boiii/scriptdata/pintemod/" + relativePath.Replace(Path.DirectorySeparatorChar, '/') + "/*.json"
            : throw new ArgumentOutOfRangeException(nameof(directory), directory, "Dossier Ranks non autorisé.");

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
            throw new InvalidOperationException("La source Ranks sort de ServerRoot.");
        }
    }

    private void RejectExistingReparsePoints(string targetPath)
    {
        var current = options.ServerRoot;
        var relative = Path.GetRelativePath(options.ServerRoot, targetPath);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) || File.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException($"Les liens et jonctions ne sont pas autorisés : {current}");
                }
            }
        }
    }
}
