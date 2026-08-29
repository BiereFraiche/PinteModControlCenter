using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Services;

internal sealed class RemoteLaunchAgentHost
{
    private readonly RemoteAgentConfigurationStore _store = new();
    private readonly MultiServerOrchestratorService _orchestrator = new();
    private readonly ManagedServerStopService _stopService = new();
    private readonly LocalServerLaunchService _localLaunch = new();
    private readonly ManagedServerRuntimeProbe _runtimeProbe = new();
    private readonly HashSet<string> _processed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastStatusWrite = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastAvailablePackageWrite = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastCatalogWrite = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastRuntimeWrite = new(StringComparer.Ordinal);
    private string? _currentExecutableHash;
    private DateTimeOffset _lastPreferredUiSync = DateTimeOffset.MinValue;

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        using var mutex = new Mutex(initiallyOwned: true, "PinteMod.ControlCenter.RemoteAgent.CurrentUser", out var ownsMutex);
        if (!ownsMutex) return 0;

        var startedAtUtc = DateTimeOffset.UtcNow;
        Log("Agent starting");
        var managedAgentExecutable = RemoteAgentConfigurationStore.GetExecutablePath();
        if (File.Exists(managedAgentExecutable))
        {
            if (RemoteAgentRecoveryTaskService.EnsureInstalled(managedAgentExecutable, out var recoveryDiagnostic))
            {
                Log(recoveryDiagnostic);
            }
            else
            {
                Log("Recovery task unavailable: " + recoveryDiagnostic);
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var stopRequestPath = RemoteAgentConfigurationStore.GetStopRequestPath();
                if (ShouldHonorStopRequest(stopRequestPath, startedAtUtc))
                {
                    Log("Stop request detected");
                    break;
                }
                TryDeleteStaleStopRequest(stopRequestPath, startedAtUtc);

                if (DateTimeOffset.UtcNow - _lastPreferredUiSync >= TimeSpan.FromSeconds(5))
                {
                    PreferredControlCenterPathService.DiscoverRunningUserInterface();
                    var agentExecutable = RemoteAgentConfigurationStore.GetExecutablePath();
                    if (File.Exists(agentExecutable))
                    {
                        await PreferredControlCenterPathService
                            .SynchronizePreferredExecutableAsync(agentExecutable, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    ManagedControlCenterInstallationService.RemoveLegacyShortcuts();
                    _lastPreferredUiSync = DateTimeOffset.UtcNow;
                }

                var configuration = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
                if (configuration.SchemaVersion != RemoteAgentProtocol.SchemaVersion || configuration.Profiles.Count == 0)
                {
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var updateStarted = false;
                foreach (var profile in configuration.Profiles.Take(8))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await TryStartSelfUpdateAsync(profile, cancellationToken).ConfigureAwait(false))
                    {
                        updateStarted = true;
                        break;
                    }
                    await DeleteExpiredPairingAsync(profile, cancellationToken).ConfigureAwait(false);
                    await PublishStatusAsync(profile, cancellationToken).ConfigureAwait(false);
                    await PublishAvailablePackageAsync(profile, cancellationToken).ConfigureAwait(false);
                    await PublishProfileCatalogAsync(profile, configuration.Profiles, cancellationToken).ConfigureAwait(false);
                    await PublishServerRuntimeAsync(profile, cancellationToken).ConfigureAwait(false);
                    await ProcessProfileRequestsAsync(profile, configuration.Profiles, cancellationToken).ConfigureAwait(false);
                }

                if (updateStarted)
                {
                    Log("Self-update staged; Agent exiting for replacement");
                    break;
                }

                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                Log("Loop error: " + exception.GetType().Name + " " + exception.Message);
                try { await Task.Delay(1500, cancellationToken).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }

        Log("Agent stopped");
        return 0;
    }

    internal static bool ShouldHonorStopRequest(string path, DateTimeOffset agentStartedAtUtc)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var text = File.ReadAllText(path).Trim();
            return DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var requestedAtUtc) &&
                requestedAtUtc.ToUniversalTime() >= agentStartedAtUtc.ToUniversalTime();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A request that cannot be inspected must not stop a newly started
            // Agent; the next recovery tick can retry after the file is released.
            return false;
        }
    }

    private static void TryDeleteStaleStopRequest(string path, DateTimeOffset agentStartedAtUtc)
    {
        try
        {
            if (!File.Exists(path) || ShouldHonorStopRequest(path, agentStartedAtUtc)) return;
            File.Delete(path);
            Log("Stale stop request removed");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static async Task<bool> TryStartSelfUpdateAsync(
        RemoteAgentProfileRegistration profile,
        CancellationToken cancellationToken)
    {
        var manifestPath = RemoteAgentProtocolService.GetUpdateManifestPath(profile.ServerRoot);
        var update = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentUpdateEnvelope>(manifestPath, cancellationToken)
            .ConfigureAwait(false);
        if (update is null) return false;

        var now = DateTimeOffset.UtcNow;
        if (update.SchemaVersion != RemoteAgentProtocol.SchemaVersion ||
            !string.Equals(update.AgentId, profile.AgentId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(update.TargetVersion) || update.TargetVersion.Length > 80 ||
            string.IsNullOrWhiteSpace(update.PackageFileName) || update.PackageFileName.Length > 120 ||
            !string.Equals(Path.GetFileName(update.PackageFileName), update.PackageFileName, StringComparison.Ordinal) ||
            !update.PackageFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            update.Sha256.Length != 64 || !update.Sha256.All(Uri.IsHexDigit) ||
            update.CreatedAtUtc > now.AddMinutes(2) || update.ExpiresAtUtc < now ||
            update.ExpiresAtUtc - update.CreatedAtUtc > TimeSpan.FromMinutes(10))
        {
            TryDelete(manifestPath);
            return false;
        }

        var secret = RemoteAgentConfigurationStore.UnprotectSecret(profile.ProtectedSecretBase64);
        if (secret is null) return false;
        try
        {
            if (!RemoteAgentProtocolService.VerifyUpdate(update, secret))
            {
                TryDelete(manifestPath);
                return false;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        // A hotfix may legitimately keep the same semantic/informational version.
        // Never decide that the Agent itself is current from the version string alone:
        // compare the running Agent EXE hash with the signed package hash as well.
        var runningAgentHash = await TryComputeCurrentAgentSha256Async(cancellationToken).ConfigureAwait(false);
        var sameAgentBuild = IsSameAgentBuild(
            update.TargetVersion,
            RemoteAgentProtocolService.GetAgentVersion(),
            update.Sha256,
            runningAgentHash);

        var updatesRoot = Path.GetFullPath(RemoteAgentProtocolService.GetUpdatesPath(profile.ServerRoot))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var packagePath = Path.GetFullPath(Path.Combine(updatesRoot, update.PackageFileName));
        if (!packagePath.StartsWith(updatesRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(packagePath))
        {
            return false;
        }

        var packageInfo = new FileInfo(packagePath);
        if (packageInfo.Length is <= 0 or > 512L * 1024L * 1024L)
        {
            TryDelete(manifestPath);
            TryDelete(packagePath);
            return false;
        }

        var actualHash = await ComputeSha256Async(packagePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualHash, update.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            Log("Self-update rejected: package hash mismatch");
            TryDelete(manifestPath);
            TryDelete(packagePath);
            return false;
        }

        if (sameAgentBuild)
        {
            PreferredControlCenterPathService.DiscoverRunningUserInterface();
            var preferredSynchronized = await PreferredControlCenterPathService
                .SynchronizePreferredExecutableAsync(packagePath, cancellationToken)
                .ConfigureAwait(false);
            var fallbackSynchronized = await ManagedControlCenterInstallationService
                .InstallOrStageAsync(packagePath, cancellationToken)
                .ConfigureAwait(false);
            ManagedControlCenterInstallationService.RemoveLegacyShortcuts();
            if (preferredSynchronized || fallbackSynchronized)
            {
                Log(preferredSynchronized
                    ? "Preferred Control Center synchronized with current Agent version"
                    : "Internal fallback Control Center synchronized with current Agent version");
                TryDelete(manifestPath);
                TryDelete(packagePath);
            }
            return false;
        }

        Directory.CreateDirectory(RemoteAgentConfigurationStore.GetAgentHome());
        var pending = RemoteAgentConfigurationStore.GetPendingUpdatePath();
        var temp = pending + ".tmp." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N");
        try
        {
            await using (var source = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temp, pending, overwrite: true);
        }
        finally
        {
            TryDelete(temp);
        }

        var pendingHash = await ComputeSha256Async(pending, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(pendingHash, update.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(pending);
            return false;
        }

        TryDelete(manifestPath);
        TryDelete(packagePath);
        RemoteAgentRecoveryTaskService.MarkUpdateInProgress();
        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = pending,
                Arguments = $"--remote-agent-apply-update {Environment.ProcessId}",
                WorkingDirectory = RemoteAgentConfigurationStore.GetAgentHome(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch
        {
            RemoteAgentRecoveryTaskService.ClearUpdateInProgress();
            throw;
        }
        if (process is null)
        {
            RemoteAgentRecoveryTaskService.ClearUpdateInProgress();
            return false;
        }
        return true;
    }

    internal static bool IsSameAgentBuild(
        string targetVersion,
        string currentVersion,
        string targetSha256,
        string? currentSha256) =>
        string.Equals(targetVersion, currentVersion, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(currentSha256) &&
        string.Equals(targetSha256, currentSha256, StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> TryComputeCurrentAgentSha256Async(CancellationToken cancellationToken)
    {
        var currentPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath)) return null;
        try
        {
            return await ComputeSha256Async(currentPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task DeleteExpiredPairingAsync(RemoteAgentProfileRegistration profile, CancellationToken cancellationToken)
    {
        var path = RemoteAgentProtocolService.GetPairingPath(profile.ServerRoot);
        var pairing = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentPairingEnvelope>(path, cancellationToken)
            .ConfigureAwait(false);
        if (pairing is not null && pairing.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            TryDelete(path);
        }
    }


    private async Task PublishProfileCatalogAsync(
        RemoteAgentProfileRegistration authorityProfile,
        IReadOnlyList<RemoteAgentProfileRegistration> allProfiles,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastCatalogWrite.TryGetValue(authorityProfile.AgentId, out var last) &&
            now - last < TimeSpan.FromSeconds(5)) return;

        var entries = allProfiles
            .Take(8)
            .Select(profile => new RemoteAgentProfileCatalogEntry(
                profile.AgentId,
                profile.DisplayName,
                Path.GetFileName(profile.ServerRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                profile.LauncherRelativePath,
                profile.ServerPort,
                profile.PinteModDetected))
            .Where(entry => RemoteAgentCatalogPathResolver.IsSafeLeaf(entry.RootFolderName))
            .OrderBy(entry => entry.AgentId, StringComparer.Ordinal)
            .ToArray();
        if (entries.Length == 0) return;

        var secret = RemoteAgentConfigurationStore.UnprotectSecret(authorityProfile.ProtectedSecretBase64);
        if (secret is null) return;
        try
        {
            var catalog = new RemoteAgentProfileCatalogEnvelope(
                RemoteAgentProtocol.SchemaVersion,
                authorityProfile.AgentId,
                Environment.MachineName,
                now,
                entries,
                string.Empty);
            catalog = catalog with { Signature = RemoteAgentProtocolService.SignProfileCatalog(catalog, secret) };
            await RemoteAgentProtocolService.WriteJsonAtomicAsync(
                RemoteAgentProtocolService.GetProfileCatalogPath(authorityProfile.ServerRoot),
                catalog,
                cancellationToken).ConfigureAwait(false);
            _lastCatalogWrite[authorityProfile.AgentId] = now;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private async Task PublishServerRuntimeAsync(
        RemoteAgentProfileRegistration profile,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastRuntimeWrite.TryGetValue(profile.AgentId, out var last) &&
            now - last < TimeSpan.FromSeconds(3)) return;

        var secret = RemoteAgentConfigurationStore.UnprotectSecret(profile.ProtectedSecretBase64);
        if (secret is null) return;
        try
        {
            var runtime = new RemoteAgentServerRuntimeEnvelope(
                RemoteAgentProtocol.SchemaVersion,
                profile.AgentId,
                now,
                _runtimeProbe.IsRunning(profile.ServerRoot, profile.ServerPort),
                string.Empty);
            runtime = runtime with { Signature = RemoteAgentProtocolService.SignServerRuntime(runtime, secret) };
            await RemoteAgentProtocolService.WriteJsonAtomicAsync(
                RemoteAgentProtocolService.GetServerRuntimePath(profile.ServerRoot),
                runtime,
                cancellationToken).ConfigureAwait(false);
            _lastRuntimeWrite[profile.AgentId] = now;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private async Task PublishAvailablePackageAsync(
        RemoteAgentProfileRegistration profile,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastAvailablePackageWrite.TryGetValue(profile.AgentId, out var last) &&
            now - last < TimeSpan.FromSeconds(30)) return;

        var sourceExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(sourceExe) || !File.Exists(sourceExe) ||
            !Path.GetExtension(sourceExe).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return;

        RemoteAgentProtocolService.EnsureQueueDirectories(profile.ServerRoot);
        _currentExecutableHash ??= await ComputeSha256Async(sourceExe, cancellationToken).ConfigureAwait(false);
        var hash = _currentExecutableHash;
        var fileName = $"available.{hash[..16]}.exe";
        var packagePath = Path.Combine(RemoteAgentProtocolService.GetUpdatesPath(profile.ServerRoot), fileName);
        var sourceLength = new FileInfo(sourceExe).Length;
        if (sourceLength is <= 0 or > 512L * 1024L * 1024L) return;

        var packageReady = false;
        try
        {
            if (File.Exists(packagePath) && new FileInfo(packagePath).Length == sourceLength)
            {
                var existingHash = await ComputeSha256Async(packagePath, cancellationToken).ConfigureAwait(false);
                packageReady = string.Equals(existingHash, hash, StringComparison.OrdinalIgnoreCase);
            }

            if (!packageReady)
            {
                var temp = packagePath + ".tmp." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N");
                try
                {
                    await using (var source = new FileStream(sourceExe, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    await using (var destination = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                    {
                        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    File.Move(temp, packagePath, overwrite: true);
                }
                finally
                {
                    TryDelete(temp);
                }

                var copiedHash = await ComputeSha256Async(packagePath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(copiedHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(packagePath);
                    return;
                }
            }

            var secret = RemoteAgentConfigurationStore.UnprotectSecret(profile.ProtectedSecretBase64);
            if (secret is null) return;
            try
            {
                var available = new RemoteAgentAvailablePackageEnvelope(
                    RemoteAgentProtocol.SchemaVersion,
                    profile.AgentId,
                    RemoteAgentProtocolService.GetAgentVersion(),
                    fileName,
                    hash,
                    now,
                    string.Empty);
                available = available with { Signature = RemoteAgentProtocolService.SignAvailablePackage(available, secret) };
                await RemoteAgentProtocolService.WriteJsonAtomicAsync(
                    RemoteAgentProtocolService.GetAvailablePackageManifestPath(profile.ServerRoot),
                    available,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }

            foreach (var candidate in Directory.EnumerateFiles(
                         RemoteAgentProtocolService.GetUpdatesPath(profile.ServerRoot),
                         "available.*.exe",
                         SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(packagePath), StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(candidate);
                }
            }
            _lastAvailablePackageWrite[profile.AgentId] = now;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private async Task PublishStatusAsync(RemoteAgentProfileRegistration profile, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastStatusWrite.TryGetValue(profile.AgentId, out var last) && now - last < TimeSpan.FromSeconds(5)) return;
        var secret = RemoteAgentConfigurationStore.UnprotectSecret(profile.ProtectedSecretBase64);
        if (secret is null) return;
        try
        {
            RemoteAgentProtocolService.EnsureQueueDirectories(profile.ServerRoot);
            var status = new RemoteAgentStatusEnvelope(
                RemoteAgentProtocol.SchemaVersion,
                profile.AgentId,
                profile.DisplayName,
                Environment.MachineName,
                "online",
                now,
                RemoteAgentProtocolService.GetAgentVersion(),
                string.Empty);
            status = status with { Signature = RemoteAgentProtocolService.SignStatus(status, secret) };
            await RemoteAgentProtocolService.WriteJsonAtomicAsync(
                RemoteAgentProtocolService.GetStatusPath(profile.ServerRoot), status, cancellationToken).ConfigureAwait(false);
            _lastStatusWrite[profile.AgentId] = now;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private async Task ProcessProfileRequestsAsync(
        RemoteAgentProfileRegistration profile,
        IReadOnlyList<RemoteAgentProfileRegistration> allProfiles,
        CancellationToken cancellationToken)
    {
        var requestFolder = RemoteAgentProtocolService.GetRequestsPath(profile.ServerRoot);
        if (!Directory.Exists(requestFolder)) return;

        IEnumerable<string> requests;
        try
        {
            requests = Directory.EnumerateFiles(requestFolder, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => File.GetCreationTimeUtc(path))
                .Take(20)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var path in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteLaunchRequest>(path, cancellationToken)
                .ConfigureAwait(false);
            if (request is null)
            {
                TryDelete(path);
                continue;
            }

            if (!_processed.Add(request.RequestId))
            {
                TryDelete(path);
                continue;
            }
            if (_processed.Count > 500) _processed.Clear();

            var secret = RemoteAgentConfigurationStore.UnprotectSecret(profile.ProtectedSecretBase64);
            if (secret is null)
            {
                TryDelete(path);
                continue;
            }

            try
            {
                var validation = ValidateRequest(request, profile, secret);
                RemoteLaunchResponse response;
                if (validation is not null)
                {
                    response = CreateResponse(request, profile, "rejected", validation.Value.Code, validation.Value.Message, null, secret);
                }
                else
                {
                    response = string.Equals(request.Action, RemoteAgentProtocol.StopAction, StringComparison.Ordinal)
                        ? await ExecuteStopAsync(request, profile, secret, cancellationToken).ConfigureAwait(false)
                        : await ExecuteLaunchAsync(request, profile, allProfiles, secret, cancellationToken).ConfigureAwait(false);
                }

                await RemoteAgentProtocolService.WriteJsonAtomicAsync(
                    Path.Combine(RemoteAgentProtocolService.GetResponsesPath(profile.ServerRoot), request.RequestId + ".json"),
                    response,
                    cancellationToken).ConfigureAwait(false);
                TryDelete(path);
                CleanupOldResponses(profile.ServerRoot);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }
    }

    private static (string Code, string Message)? ValidateRequest(
        RemoteLaunchRequest request,
        RemoteAgentProfileRegistration profile,
        byte[] secret)
    {
        var now = DateTimeOffset.UtcNow;
        if (request.SchemaVersion != RemoteAgentProtocol.SchemaVersion ||
            string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 64 ||
            !string.Equals(request.AgentId, profile.AgentId, StringComparison.Ordinal) ||
            request.Action is not (RemoteAgentProtocol.LaunchAction or RemoteAgentProtocol.StopAction) ||
            request.CreatedAtUtc > now.AddMinutes(1) || request.ExpiresAtUtc < now ||
            request.ExpiresAtUtc - request.CreatedAtUtc > TimeSpan.FromMinutes(2) ||
            string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length > 128)
        {
            return ("invalid_request", "Requête Agent invalide ou expirée.");
        }

        return RemoteAgentProtocolService.VerifyRequest(request, secret)
            ? null
            : ("bad_signature", "Signature Agent invalide.");
    }

    private async Task<RemoteLaunchResponse> ExecuteLaunchAsync(
        RemoteLaunchRequest request,
        RemoteAgentProfileRegistration profile,
        IReadOnlyList<RemoteAgentProfileRegistration> allProfiles,
        byte[] secret,
        CancellationToken cancellationToken)
    {
        ServerLaunchResult result;
        try
        {
            if (IsUdpPortListening(profile.ServerPort))
            {
                result = new ServerLaunchResult(true, $"BOIII est déjà actif sur UDP/RCON {profile.ServerPort}; aucun doublon lancé.");
            }
            else if (profile.PinteModDetected)
            {
                if (!_orchestrator.IsRconSecretPrepared(profile.LocalProfileId))
                {
                    result = new ServerLaunchResult(
                        false,
                        "Secret Worker absent sur le PC serveur. Ouvrez le profil local dans le Manager, enregistrez son secret RCON puis cliquez ACTIVER / METTRE À JOUR AGENT DISTANT avant de relancer depuis le portable.");
                }
                else
                {
                    var selected = ToLaunchDefinition(profile);
                    var hub = allProfiles
                        .Where(item => item.PinteModDetected && Directory.Exists(Path.Combine(item.ServerRoot, "boiii")))
                        .Select(ToLaunchDefinition)
                        .ToArray();
                    result = await _orchestrator.LaunchAsync([selected], hub, cancellationToken).ConfigureAwait(false);
                    if (result.Success)
                    {
                        var online = await WaitForUdpPortAsync(profile.ServerPort, TimeSpan.FromSeconds(35), cancellationToken)
                            .ConfigureAwait(false);
                        if (!online)
                        {
                            result = new ServerLaunchResult(
                                false,
                                $"Worker v3 a été démarré mais BOIII n'a pas ouvert UDP/RCON {profile.ServerPort}. Consultez le log Agent/Worker sur le PC serveur.",
                                result.ProcessId);
                        }
                        else
                        {
                            result = result with { Message = $"BOIII confirmé en ligne sur UDP/RCON {profile.ServerPort}. Worker v3 et RecordHub ont été lancés." };
                        }
                    }
                }
            }
            else
            {
                result = await _localLaunch.LaunchAsync(profile.ServerRoot, profile.LauncherRelativePath, cancellationToken)
                    .ConfigureAwait(false);
                if (result.Success)
                {
                    var online = await WaitForUdpPortAsync(profile.ServerPort, TimeSpan.FromSeconds(35), cancellationToken)
                        .ConfigureAwait(false);
                    if (!online)
                    {
                        result = new ServerLaunchResult(
                            false,
                            $"Le lanceur a démarré mais BOIII n'a pas ouvert UDP/RCON {profile.ServerPort}.",
                            result.ProcessId);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NetworkInformationException)
        {
            result = new ServerLaunchResult(false, "Lancement Agent refusé : " + exception.Message);
        }

        Log($"Launch {profile.AgentId}: {result.Success} {result.Message}");
        return CreateResponse(
            request,
            profile,
            result.Success ? "applied" : "failed",
            result.Success ? "launch_started" : "launch_failed",
            result.Message,
            result.ProcessId,
            secret);
    }

    private async Task<RemoteLaunchResponse> ExecuteStopAsync(
        RemoteLaunchRequest request,
        RemoteAgentProfileRegistration profile,
        byte[] secret,
        CancellationToken cancellationToken)
    {
        ServerLaunchResult result;
        try
        {
            result = await _stopService.StopAsync(
                profile.LocalProfileId,
                profile.ServerRoot,
                profile.ServerPort,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            result = new ServerLaunchResult(false, "Arrêt Agent refusé : " + exception.Message);
        }

        Log($"Stop {profile.AgentId}: {result.Success} {result.Message}");
        return CreateResponse(
            request,
            profile,
            result.Success ? "applied" : "failed",
            result.Success ? "server_stopped" : "stop_failed",
            result.Message,
            null,
            secret);
    }

    private static bool IsUdpPortListening(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveUdpListeners()
                .Any(endpoint => endpoint.Port == port);
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForUdpPortAsync(
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsUdpPortListening(port)) return true;
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        return IsUdpPortListening(port);
    }

    private static RemoteLaunchResponse CreateResponse(
        RemoteLaunchRequest request,
        RemoteAgentProfileRegistration profile,
        string status,
        string code,
        string message,
        int? processId,
        byte[] secret)
    {
        var bounded = string.IsNullOrWhiteSpace(message) ? code : message.Trim();
        if (bounded.Length > 400) bounded = bounded[..400];
        var response = new RemoteLaunchResponse(
            RemoteAgentProtocol.SchemaVersion,
            request.RequestId,
            profile.AgentId,
            status,
            code,
            bounded,
            DateTimeOffset.UtcNow,
            processId,
            string.Empty);
        return response with { Signature = RemoteAgentProtocolService.SignResponse(response, secret) };
    }

    private static MultiServerLaunchDefinition ToLaunchDefinition(RemoteAgentProfileRegistration profile) => new(
        profile.LocalProfileId,
        profile.DisplayName,
        profile.ServerRoot,
        profile.LauncherRelativePath,
        profile.ServerPort);

    private static void CleanupOldResponses(string serverRoot)
    {
        try
        {
            var folder = RemoteAgentProtocolService.GetResponsesPath(serverRoot);
            var threshold = DateTime.UtcNow.AddHours(-24);
            foreach (var path in Directory.EnumerateFiles(folder, "*.json").Where(path => File.GetLastWriteTimeUtc(path) < threshold).Take(100))
            {
                TryDelete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static void Log(string message)
    {
        try
        {
            var path = RemoteAgentConfigurationStore.GetLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
            {
                File.Move(path, path + ".1", overwrite: true);
            }
            File.AppendAllText(path,
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
