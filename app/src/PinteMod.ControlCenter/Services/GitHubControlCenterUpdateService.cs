using System.Net.Http;
using System.Security.Cryptography;
using System.IO;

namespace PinteMod.ControlCenter.Services;

internal sealed record GitHubControlCenterUpdateResult(bool RestartScheduled, string Message);

internal sealed class GitHubControlCenterUpdateService
{
    private const long MaximumDownloadBytes = 512L * 1024 * 1024;

    public async Task<GitHubControlCenterUpdateResult> DownloadAndStageAsync(
        GitHubUpdateCheckResult release,
        int currentPid,
        CancellationToken cancellationToken = default)
    {
        if (!release.UpdateAvailable ||
            string.IsNullOrWhiteSpace(release.DownloadUrl) ||
            string.IsNullOrWhiteSpace(release.Sha256) ||
            !IsSha256(release.Sha256) ||
            release.DownloadSize is not > 0 or > MaximumDownloadBytes ||
            !IsOfficialReleaseDownload(release.DownloadUrl))
        {
            return new(false, "La mise à jour GitHub ne fournit pas un EXE officiel vérifiable.");
        }

        var updateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PinteMod",
            "ControlCenter",
            "downloads");
        var temporary = Path.Combine(updateDirectory, Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            Directory.CreateDirectory(updateDirectory);
            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PinteMod-ControlCenter-Updater/1.0");
            using var response = await client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength is not { } contentLength ||
                contentLength != release.DownloadSize ||
                contentLength > MaximumDownloadBytes)
            {
                return new(false, "Le téléchargement GitHub est incomplet ou inattendu.");
            }

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var info = new FileInfo(temporary);
            if (info.Length != release.DownloadSize || !HashMatches(temporary, release.Sha256))
            {
                return new(false, "La signature SHA-256 de la mise à jour ne correspond pas à GitHub.");
            }

            if (!await PreferredControlCenterPathService.StageCurrentExecutableUpdateAsync(
                    temporary,
                    currentPid,
                    cancellationToken).ConfigureAwait(false))
            {
                return new(false, "La mise à jour a été téléchargée, mais son remplacement n’a pas pu être préparé.");
            }

            return new(true, "Mise à jour vérifiée. Le Control Center va redémarrer avec la nouvelle version.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, "Mise à jour annulée.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new(false, "Téléchargement impossible pour le moment. Votre version actuelle reste inchangée.");
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static bool IsOfficialReleaseDownload(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.Contains("/BiereFraiche/PinteModControlCenter/releases/download/", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.EndsWith("/PinteMod.ControlCenter.exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool HashMatches(string path, string expected)
    {
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(actual),
            Convert.FromHexString(expected));
    }
}
