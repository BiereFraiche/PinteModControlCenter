using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PinteMod.ControlCenter.Services;

internal enum GitHubUpdateState
{
    Unknown,
    Checking,
    NoCompatibleRelease,
    UpToDate,
    LocalNewer,
    UpdateAvailable,
    Unavailable
}

internal sealed record GitHubUpdateCheckResult(
    GitHubUpdateState State,
    string Message,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl)
{
    public bool UpdateAvailable => State == GitHubUpdateState.UpdateAvailable;
}

internal sealed partial class GitHubUpdateCheckService
{
    internal const string Repository = "BiereFraiche/PinteModControlCenter";
    private const string ReleasesEndpoint = "https://api.github.com/repos/BiereFraiche/PinteModControlCenter/releases?per_page=20";

    public async Task<GitHubUpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = GetCurrentVersion();
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PinteMod-ControlCenter/" + current);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.GetAsync(ReleasesEndpoint, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new GitHubUpdateCheckResult(
                    GitHubUpdateState.Unavailable,
                    $"GitHub indisponible ({(int)response.StatusCode}). Le Control Center reste utilisable.",
                    current,
                    null,
                    null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Unavailable(current);
            }

            ReleaseCandidate? latest = null;
            foreach (var release in document.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) continue;

                var tag = GetString(release, "tag_name");
                var name = GetString(release, "name");
                var url = GetString(release, "html_url");
                var assetNames = release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array
                    ? assets.EnumerateArray().Select(asset => GetString(asset, "name")).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
                    : [];

                var matchingAssets = assetNames.Where(ContainsControlCenter).ToArray();
                var controlCenterRelease = matchingAssets.Length > 0 || ContainsControlCenter(tag) || ContainsControlCenter(name);
                if (!controlCenterRelease) continue;

                // Prefer the version embedded in the Control Center asset.
                var versionText = matchingAssets
                    .Select(TryExtractVersion)
                    .FirstOrDefault(version => version is not null)
                    ?? new[] { tag, name }.Select(TryExtractVersion).FirstOrDefault(version => version is not null);
                if (versionText is null || !ReleaseVersion.TryParse(versionText, out var parsed)) continue;

                var candidate = new ReleaseCandidate(versionText, parsed, url);
                if (latest is null || candidate.Version.CompareTo(latest.Version) > 0)
                {
                    latest = candidate;
                }
            }

            if (latest is null)
            {
                return new GitHubUpdateCheckResult(
                    GitHubUpdateState.NoCompatibleRelease,
                    "GitHub : aucune version publique Control Center compatible n’est publiée pour le moment.",
                    current,
                    null,
                    null);
            }

            if (!ReleaseVersion.TryParse(current, out var currentVersion))
            {
                return new GitHubUpdateCheckResult(
                    GitHubUpdateState.Unknown,
                    $"GitHub : version publique {latest.VersionText} détectée, comparaison locale impossible.",
                    current,
                    latest.VersionText,
                    latest.ReleaseUrl);
            }

            var comparison = latest.Version.CompareTo(currentVersion);
            if (comparison > 0)
            {
                return new GitHubUpdateCheckResult(
                    GitHubUpdateState.UpdateAvailable,
                    $"Mise à jour publique disponible sur GitHub : {latest.VersionText}.",
                    current,
                    latest.VersionText,
                    latest.ReleaseUrl);
            }

            if (comparison == 0)
            {
                return new GitHubUpdateCheckResult(
                    GitHubUpdateState.UpToDate,
                    $"GitHub : vous utilisez la dernière version publique ({latest.VersionText}).",
                    current,
                    latest.VersionText,
                    latest.ReleaseUrl);
            }

            return new GitHubUpdateCheckResult(
                GitHubUpdateState.LocalNewer,
                $"GitHub : votre version locale ({current}) est plus récente que la version publique ({latest.VersionText}).",
                current,
                latest.VersionText,
                latest.ReleaseUrl);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(current);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidOperationException)
        {
            return Unavailable(current);
        }
    }


    internal static int? CompareVersions(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return null;
        if (!ReleaseVersion.TryParse(left, out var leftVersion) ||
            !ReleaseVersion.TryParse(right, out var rightVersion))
        {
            return null;
        }

        return leftVersion.CompareTo(rightVersion);
    }

    internal static string GetCurrentVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    }

    internal static string? TryExtractVersion(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // GitHub asset names append packaging qualifiers after the semantic version
        // (for example: -win-x64.zip). Strip only those known packaging suffixes
        // before parsing so they are never mistaken for a SemVer prerelease.
        var normalized = ArchiveSuffixRegex().Replace(input.Trim(), string.Empty);
        normalized = WindowsAssetSuffixRegex().Replace(normalized, string.Empty);

        var match = VersionRegex().Match(normalized);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool ContainsControlCenter(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("PinteMod-ControlCenter", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("Control Center", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("ControlCenter", StringComparison.OrdinalIgnoreCase));

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static GitHubUpdateCheckResult Unavailable(string current) => new(
        GitHubUpdateState.Unavailable,
        "GitHub : vérification impossible pour le moment. Cela ne bloque aucune fonction.",
        current,
        null,
        null);

    private sealed record ReleaseCandidate(string VersionText, ReleaseVersion Version, string? ReleaseUrl);

    internal sealed record ReleaseVersion(int Major, int Minor, int Patch, string? PreRelease) : IComparable<ReleaseVersion>
    {
        internal static bool TryParse(string value, out ReleaseVersion version)
        {
            version = new ReleaseVersion(0, 0, 0, null);
            var extracted = TryExtractVersion(value);
            if (extracted is null) return false;
            var dash = extracted.IndexOf('-');
            var core = dash >= 0 ? extracted[..dash] : extracted;
            var pre = dash >= 0 ? extracted[(dash + 1)..] : null;
            var pieces = core.Split('.');
            if (pieces.Length != 3 ||
                !int.TryParse(pieces[0], out var major) ||
                !int.TryParse(pieces[1], out var minor) ||
                !int.TryParse(pieces[2], out var patch))
            {
                return false;
            }

            version = new ReleaseVersion(major, minor, patch, string.IsNullOrWhiteSpace(pre) ? null : pre);
            return true;
        }

        public int CompareTo(ReleaseVersion? other)
        {
            if (other is null) return 1;
            var result = Major.CompareTo(other.Major);
            if (result != 0) return result;
            result = Minor.CompareTo(other.Minor);
            if (result != 0) return result;
            result = Patch.CompareTo(other.Patch);
            if (result != 0) return result;

            if (PreRelease is null && other.PreRelease is null) return 0;
            if (PreRelease is null) return 1;
            if (other.PreRelease is null) return -1;

            // PinteMod previews use a human-readable track label (for example
            // "preview-onboarding" or "preview-integration") followed by the
            // authoritative preview sequence (4a7, 4b1, ...). The descriptive
            // label must never decide update direction: 4B is newer than 4A even
            // though "integration" sorts before "onboarding" alphabetically.
            if (TryParsePinteModPreviewSequence(PreRelease, out var leftPreview) &&
                TryParsePinteModPreviewSequence(other.PreRelease, out var rightPreview))
            {
                result = leftPreview.Phase.CompareTo(rightPreview.Phase);
                if (result != 0) return result;
                result = leftPreview.Stage.CompareTo(rightPreview.Stage);
                if (result != 0) return result;
                result = leftPreview.Iteration.CompareTo(rightPreview.Iteration);
                if (result != 0) return result;
                return leftPreview.Fix.CompareTo(rightPreview.Fix);
            }

            var left = TokenRegex().Matches(PreRelease).Select(match => match.Value).ToArray();
            var right = TokenRegex().Matches(other.PreRelease).Select(match => match.Value).ToArray();
            for (var index = 0; index < Math.Max(left.Length, right.Length); index++)
            {
                if (index >= left.Length) return -1;
                if (index >= right.Length) return 1;
                var leftNumeric = int.TryParse(left[index], out var leftNumber);
                var rightNumeric = int.TryParse(right[index], out var rightNumber);
                if (leftNumeric && rightNumeric)
                {
                    result = leftNumber.CompareTo(rightNumber);
                }
                else
                {
                    result = string.Compare(left[index], right[index], StringComparison.OrdinalIgnoreCase);
                }
                if (result != 0) return result;
            }

            return 0;
        }

        private static bool TryParsePinteModPreviewSequence(string preRelease, out PreviewSequence sequence)
        {
            sequence = default;
            var match = PreviewSequenceRegex().Match(preRelease);
            if (!match.Success ||
                !int.TryParse(match.Groups["phase"].Value, out var phase) ||
                !int.TryParse(match.Groups["iteration"].Value, out var iteration))
            {
                return false;
            }

            var stageText = match.Groups["stage"].Value;
            if (stageText.Length != 1) return false;

            var fix = 0;
            if (match.Groups["fix"].Success &&
                !int.TryParse(match.Groups["fix"].Value, out fix))
            {
                return false;
            }

            sequence = new PreviewSequence(phase, char.ToUpperInvariant(stageText[0]), iteration, fix);
            return true;
        }

        private readonly record struct PreviewSequence(int Phase, char Stage, int Iteration, int Fix);
    }

    [GeneratedRegex(@"\.(?:zip|exe|msi|7z)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ArchiveSuffixRegex();

    [GeneratedRegex(@"-(?:win|windows)-(?:x64|x86|arm64)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WindowsAssetSuffixRegex();

    [GeneratedRegex(@"(?<!\d)(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"(?:^|[._-])(?<phase>\d+)(?<stage>[A-Za-z])(?<iteration>\d+)(?:[._-]fix(?<fix>\d+))?(?:$|[._-])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PreviewSequenceRegex();

    [GeneratedRegex(@"[A-Za-z]+|\d+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
