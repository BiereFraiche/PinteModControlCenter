using System.IO;

namespace PinteMod.ControlCenter.Services;

internal static class RemoteAgentCatalogPathResolver
{
    internal static IReadOnlyList<string> BuildSiblingCandidates(string sourceUncRoot, string rootFolderName)
    {
        if (string.IsNullOrWhiteSpace(sourceUncRoot) || !sourceUncRoot.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return [];
        }

        var leaf = (rootFolderName ?? string.Empty).Trim();
        if (!IsSafeLeaf(leaf)) return [];

        var trimmed = sourceUncRoot.Trim().TrimEnd('\\', '/');
        if (!trimmed.StartsWith(@"\\", StringComparison.Ordinal)) return [];
        var parts = trimmed[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return [];

        var candidates = new List<string>();
        if (parts.Length >= 3)
        {
            var parent = @"\\" + string.Join("\\", parts.Take(parts.Length - 1));
            candidates.Add(parent + "\\" + leaf);
        }

        var shareCandidate = @"\\" + parts[0] + "\\" + leaf;
        if (!candidates.Contains(shareCandidate, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(shareCandidate);
        }

        return candidates;
    }

    internal static bool IsSafeLeaf(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 80 &&
        value is not "." and not ".." &&
        value.IndexOfAny(['\\', '/', ':']) < 0 &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        value.All(character => !char.IsControl(character));
}
