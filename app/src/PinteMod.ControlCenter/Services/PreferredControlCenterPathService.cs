using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace PinteMod.ControlCenter.Services;

internal static class PreferredControlCenterPathService
{
    private static readonly string StateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PinteMod",
        "ControlCenter");

    private static readonly string PreferredPathFile = Path.Combine(StateDirectory, "preferred-ui-path.txt");
    private static readonly string PreferredPendingUpdate = Path.Combine(StateDirectory, "preferred-ui.pending.exe");

    internal static string? GetPreferredExecutablePath()
    {
        try
        {
            if (!File.Exists(PreferredPathFile)) return null;
            var value = File.ReadAllText(PreferredPathFile).Trim();
            return IsEligibleUserExecutable(value) ? Path.GetFullPath(value) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal static bool RegisterCurrentExecutable()
    {
        var current = Environment.ProcessPath;
        return RegisterExecutable(current);
    }

    internal static bool RegisterExecutable(string? path)
    {
        if (!IsEligibleUserExecutable(path)) return false;
        try
        {
            Directory.CreateDirectory(StateDirectory);
            var full = Path.GetFullPath(path!);
            var temp = PreferredPathFile + ".tmp." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, full);
            File.Move(temp, PreferredPathFile, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static string? DiscoverRunningUserInterface()
    {
        try
        {
            var candidates = Process.GetProcessesByName("PinteMod.ControlCenter")
                .Where(process => process.Id != Environment.ProcessId)
                .OrderByDescending(process =>
                {
                    try { return process.StartTime; }
                    catch { return DateTime.MinValue; }
                })
                .ToArray();

            foreach (var process in candidates)
            {
                using (process)
                {
                    try
                    {
                        if (process.MainWindowHandle == IntPtr.Zero) continue;
                        var path = process.MainModule?.FileName;
                        if (!IsEligibleUserExecutable(path)) continue;
                        RegisterExecutable(path);
                        return Path.GetFullPath(path!);
                    }
                    catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                    {
                    }
                }
            }
        }
        catch (InvalidOperationException)
        {
        }

        return null;
    }

    internal static async Task<bool> SynchronizePreferredExecutableAsync(
        string sourceExe,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceExe) || !File.Exists(sourceExe)) return false;
        var target = GetPreferredExecutablePath() ?? DiscoverRunningUserInterface();
        if (string.IsNullOrWhiteSpace(target)) return false;

        try
        {
            var sourceFull = Path.GetFullPath(sourceExe);
            var targetFull = Path.GetFullPath(target);
            if (string.Equals(sourceFull, targetFull, StringComparison.OrdinalIgnoreCase)) return true;
            if (FilesMatchSha256(sourceFull, targetFull)) return true;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Copy(sourceFull, targetFull, overwrite: true);
                    return true;
                }
                catch (IOException) when (attempt < 3)
                {
                    await Task.Delay(250 * attempt, cancellationToken).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException) when (attempt < 3)
                {
                    await Task.Delay(250 * attempt, cancellationToken).ConfigureAwait(false);
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
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }

        return false;
    }

    internal static async Task<bool> StageCurrentExecutableUpdateAsync(
        string verifiedSourceExe,
        int currentPid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(verifiedSourceExe) || !File.Exists(verifiedSourceExe)) return false;
        var current = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(current) || !File.Exists(current)) return false;

        try
        {
            if (string.Equals(
                    Path.GetFullPath(current),
                    Path.GetFullPath(ManagedControlCenterInstallationService.GetExecutablePath()),
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!await ManagedControlCenterInstallationService.InstallOrStageAsync(verifiedSourceExe, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
                return ManagedControlCenterInstallationService.StartPendingUpdate(currentPid) is not null;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }

        if (!IsEligibleUserExecutable(current)) return false;
        if (!RegisterExecutable(current)) return false;

        try
        {
            Directory.CreateDirectory(StateDirectory);
            var temp = PreferredPendingUpdate + ".tmp." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N");
            try
            {
                await using (var source = new FileStream(verifiedSourceExe, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var destination = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                File.Move(temp, PreferredPendingUpdate, overwrite: true);
            }
            finally
            {
                TryDelete(temp);
            }

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = PreferredPendingUpdate,
                Arguments = $"--preferred-ui-apply-update {currentPid}",
                WorkingDirectory = StateDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            return process is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    internal static async Task<bool> ApplyPendingCurrentExecutableUpdateAsync(
        int previousPid,
        CancellationToken cancellationToken = default)
    {
        var source = Environment.ProcessPath;
        var target = GetPreferredExecutablePath();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) return false;

        try
        {
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(PreferredPendingUpdate), StringComparison.OrdinalIgnoreCase)) return false;
            if (!IsEligibleUserExecutable(target)) return false;

            await WaitForProcessExitAsync(previousPid, cancellationToken).ConfigureAwait(false);
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Copy(source, target, overwrite: true);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = target,
                        WorkingDirectory = Path.GetDirectoryName(target) ?? StateDirectory,
                        UseShellExecute = true
                    });
                    return true;
                }
                catch (IOException) when (attempt < 10)
                {
                    await Task.Delay(200 * attempt, cancellationToken).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException) when (attempt < 10)
                {
                    await Task.Delay(200 * attempt, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
        }
        return false;
    }

    private static async Task WaitForProcessExitAsync(int processId, CancellationToken cancellationToken)
    {
        if (processId <= 0) return;
        try
        {
            using var process = Process.GetProcessById(processId);
            while (!process.HasExited)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                process.Refresh();
            }
        }
        catch (ArgumentException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static bool FilesMatchSha256(string first, string second)
    {
        try
        {
            if (!File.Exists(first) || !File.Exists(second)) return false;
            if (new FileInfo(first).Length != new FileInfo(second).Length) return false;
            using var sha = SHA256.Create();
            using var firstStream = File.OpenRead(first);
            var firstHash = sha.ComputeHash(firstStream);
            using var secondStream = File.OpenRead(second);
            var secondHash = sha.ComputeHash(secondStream);
            return CryptographicOperations.FixedTimeEquals(firstHash, secondHash);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool IsEligibleUserExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal)) return false;
            if (!string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (!File.Exists(path)) return false;

            var full = Path.GetFullPath(path);
            var agentHome = Path.GetFullPath(RemoteAgentConfigurationStore.GetAgentHome())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var managedHome = Path.GetFullPath(ManagedControlCenterInstallationService.GetHome())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            return !full.StartsWith(agentHome, StringComparison.OrdinalIgnoreCase) &&
                   !full.StartsWith(managedHome, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
