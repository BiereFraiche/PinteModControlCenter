using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace PinteMod.ControlCenter.Services;

internal static class ManagedControlCenterInstallationService
{
    internal static string GetHome() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PinteMod",
        "ControlCenter");

    internal static string GetExecutablePath() => Path.Combine(GetHome(), "PinteMod.ControlCenter.exe");
    internal static string GetPendingUpdatePath() => Path.Combine(GetHome(), "PinteMod.ControlCenter.pending.exe");

    internal static async Task<bool> InstallOrStageAsync(string sourceExe, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceExe) || !File.Exists(sourceExe)) return false;

        Directory.CreateDirectory(GetHome());
        RemoveLegacyShortcuts();
        var target = GetExecutablePath();
        var pending = GetPendingUpdatePath();
        var sourceFull = Path.GetFullPath(sourceExe);
        var targetFull = Path.GetFullPath(target);

        if (string.Equals(sourceFull, targetFull, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (await TryCopyWithRetriesAsync(sourceFull, targetFull, 5, cancellationToken).ConfigureAwait(false))
        {
            TryDelete(pending);
            return true;
        }

        // Internal fallback only. No desktop/start-menu shortcut is created.
        // If this managed copy is currently open, stage the verified executable;
        // a managed 4A2+ UI applies it on the next launch before showing WPF.
        if (!await TryCopyWithRetriesAsync(sourceFull, pending, 5, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return true;
    }

    internal static void RemoveLegacyShortcuts()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs");

        TryDelete(Path.Combine(desktop, "PinteMod Control Center.lnk"));
        TryDelete(Path.Combine(startMenu, "PinteMod Control Center.lnk"));
    }

    internal static bool ShouldApplyPendingOnStartup()
    {
        try
        {
            var current = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(current)) return false;
            var isManagedExecutable = string.Equals(
                Path.GetFullPath(current),
                Path.GetFullPath(GetExecutablePath()),
                StringComparison.OrdinalIgnoreCase);
            var pending = GetPendingUpdatePath();
            if (!isManagedExecutable || !File.Exists(pending)) return false;

            if (FilesMatchSha256(GetExecutablePath(), pending))
            {
                TryDelete(pending);
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    internal static Process? StartPendingUpdate(int currentPid)
    {
        var pending = GetPendingUpdatePath();
        if (!File.Exists(pending)) return null;
        return Process.Start(new ProcessStartInfo
        {
            FileName = pending,
            Arguments = $"--managed-ui-apply-update {currentPid}",
            WorkingDirectory = GetHome(),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    internal static async Task<bool> ApplyPendingAsync(int previousPid, CancellationToken cancellationToken = default)
    {
        var source = Environment.ProcessPath;
        var pending = GetPendingUpdatePath();
        var target = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(source) ||
            !string.Equals(Path.GetFullPath(source), Path.GetFullPath(pending), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await WaitForProcessExitAsync(previousPid, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(GetHome());
        if (!await TryCopyWithRetriesAsync(source, target, 10, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        TryDelete(pending);
        RemoveLegacyShortcuts();
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            WorkingDirectory = GetHome(),
            UseShellExecute = true
        });
        return true;
    }

    private static async Task<bool> TryCopyWithRetriesAsync(
        string source,
        string destination,
        int attempts,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.Copy(source, destination, overwrite: true);
                return true;
            }
            catch (IOException) when (attempt < attempts)
            {
                await Task.Delay(200 * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < attempts)
            {
                await Task.Delay(200 * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    private static async Task WaitForProcessExitAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var previous = Process.GetProcessById(processId);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            await previous.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
        }
    }

    private static bool FilesMatchSha256(string first, string second)
    {
        try
        {
            if (!File.Exists(first) || !File.Exists(second)) return false;
            using var sha = SHA256.Create();
            using var a = File.OpenRead(first);
            var firstHash = sha.ComputeHash(a);
            using var b = File.OpenRead(second);
            var secondHash = sha.ComputeHash(b);
            return CryptographicOperations.FixedTimeEquals(firstHash, secondHash);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
