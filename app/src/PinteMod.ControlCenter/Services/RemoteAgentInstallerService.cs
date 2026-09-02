using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Security;

namespace PinteMod.ControlCenter.Services;

internal sealed record RemoteAgentRegistrationDefinition(
    string ProfileId,
    string DisplayName,
    string ServerRoot,
    string LauncherRelativePath,
    int ServerPort,
    bool PinteModDetected);

internal sealed class RemoteAgentInstallerService
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "PinteModControlCenterAgent";
    private readonly RemoteAgentConfigurationStore _store = new();
    private readonly MultiServerOrchestratorService _orchestrator = new();
    private readonly EmbeddedServerPayloadService _payloadService = new();


    public async Task<bool> NeedsRegistrationRefreshAsync(
        IReadOnlyCollection<RemoteAgentRegistrationDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        var installedAgentPath = RemoteAgentConfigurationStore.GetExecutablePath();
        if (!File.Exists(installedAgentPath)) return false;

        // The Agent is a background process, not merely an installed EXE. A stale
        // status.json can make a remote PC look connected even after the Agent has
        // stopped. Treat a missing Agent mutex as a refresh requirement so opening
        // the Control Center locally repairs/restarts the first-party Agent.
        if (!IsAgentHostRunning()) return true;
        if (!RemoteAgentRecoveryTaskService.IsInstalledFor(installedAgentPath)) return true;

        // A new Control Center build must also refresh the background Agent even
        // when the registered server list itself did not change. Preview 4B1
        // adds Agent-side contracts (catalog/runtime), so keeping an older Agent
        // silently would make the UI look current while the SMB peer is not.
        var currentExecutable = Environment.ProcessPath;
        var isControlCenterProcess = string.Equals(
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name,
            "PinteMod.ControlCenter",
            StringComparison.Ordinal);
        if (isControlCenterProcess &&
            !string.IsNullOrWhiteSpace(currentExecutable) &&
            File.Exists(currentExecutable) &&
            Path.GetExtension(currentExecutable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var source = Path.GetFullPath(currentExecutable);
                var target = Path.GetFullPath(installedAgentPath);
                if (!string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                {
                    var sourceHash = await ComputeSha256Async(source, cancellationToken).ConfigureAwait(false);
                    var targetHash = await ComputeSha256Async(target, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // Fail closed toward refreshing the local first-party Agent.
                return true;
            }
        }

        var expected = new List<(string ProfileId, string Root, string DisplayName, string Launcher, int ServerPort, bool PinteModDetected)>();
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.ServerRoot) ||
                definition.ServerRoot.StartsWith(@"\\", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var root = Path.GetFullPath(definition.ServerRoot.Trim()).TrimEnd(Path.DirectorySeparatorChar);
                if (!Directory.Exists(Path.Combine(root, "boiii"))) continue;
                expected.Add((
                    definition.ProfileId,
                    root,
                    NormalizeDisplayName(definition.DisplayName),
                    NormalizeLauncher(definition.LauncherRelativePath),
                    definition.ServerPort,
                    definition.PinteModDetected));
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                continue;
            }
        }
        expected.Sort((left, right) => StringComparer.Ordinal.Compare(left.ProfileId, right.ProfileId));
        if (expected.Count == 0) return false;

        var existing = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var actual = existing.Profiles
            .OrderBy(item => item.LocalProfileId, StringComparer.Ordinal)
            .ToArray();
        if (actual.Length != expected.Count) return true;

        for (var index = 0; index < expected.Count; index++)
        {
            var left = expected[index];
            var right = actual[index];
            string actualRoot;
            try
            {
                actualRoot = Path.GetFullPath(right.ServerRoot).TrimEnd(Path.DirectorySeparatorChar);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return true;
            }

            if (!string.Equals(left.ProfileId, right.LocalProfileId, StringComparison.Ordinal) ||
                !string.Equals(left.Root, actualRoot, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) ||
                !string.Equals(left.Launcher, right.LauncherRelativePath, StringComparison.OrdinalIgnoreCase) ||
                left.ServerPort != right.ServerPort ||
                left.PinteModDetected != right.PinteModDetected)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<ServerDeploymentResult> InstallOrUpdateAsync(
        IReadOnlyCollection<RemoteAgentRegistrationDefinition> definitions,
        CancellationToken cancellationToken = default,
        bool hardenPinteModTooling = true)
    {
        if (definitions.Count == 0)
        {
            return new ServerDeploymentResult(false, "Aucun profil serveur local valide à enregistrer dans l’Agent.", [], []);
        }

        var sourceExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(sourceExe) || !File.Exists(sourceExe) ||
            !Path.GetExtension(sourceExe).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return new ServerDeploymentResult(false, "L’Agent doit être installé depuis le PinteMod.ControlCenter.exe publié.", [], []);
        }
        var agentSourceExe = RemoteAgentExecutableSourceResolver.Resolve(sourceExe);
        if (!File.Exists(agentSourceExe))
        {
            return new ServerDeploymentResult(false, "Le package Agent autonome est introuvable à côté du Control Center.", [], []);
        }

        // This explicit local Agent activation is the authoritative moment to
        // remember the exact Control Center EXE chosen by the operator.
        PreferredControlCenterPathService.RegisterExecutable(sourceExe);
        ManagedControlCenterInstallationService.RemoveLegacyShortcuts();

        var existing = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var existingById = existing.Profiles
            .Where(item => !string.IsNullOrWhiteSpace(item.AgentId))
            .GroupBy(item => item.AgentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var registrations = new List<RemoteAgentProfileRegistration>();
        var created = new List<string>();
        var skipped = new List<string>();
        var preparedWorkerSecrets = 0;
        var hardenedServers = 0;

        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(definition.ServerRoot) ||
                definition.ServerRoot.StartsWith("\\\\", StringComparison.Ordinal))
            {
                continue;
            }

            var root = Path.GetFullPath(definition.ServerRoot.Trim()).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(Path.Combine(root, "boiii")))
            {
                continue;
            }

            if (definition.PinteModDetected && hardenPinteModTooling)
            {
                var hardening = await _payloadService.HardenExistingManagerToolingAsync(root, cancellationToken)
                    .ConfigureAwait(false);
                created.AddRange(hardening.CreatedFiles);
                skipped.AddRange(hardening.SkippedFiles);
                if (!hardening.Success)
                {
                    return new ServerDeploymentResult(
                        false,
                        $"Agent non mis à jour pour {definition.DisplayName} : {hardening.Message}",
                        created,
                        skipped);
                }
                hardenedServers++;
            }

            var agentId = CreateStableAgentId(root);
            byte[] secret;
            if (existingById.TryGetValue(agentId, out var previous))
            {
                secret = RemoteAgentConfigurationStore.UnprotectSecret(previous.ProtectedSecretBase64) ?? RandomNumberGenerator.GetBytes(32);
            }
            else
            {
                secret = RandomNumberGenerator.GetBytes(32);
            }

            try
            {
                registrations.Add(new RemoteAgentProfileRegistration(
                    agentId,
                    definition.ProfileId,
                    NormalizeDisplayName(definition.DisplayName),
                    root,
                    NormalizeLauncher(definition.LauncherRelativePath),
                    definition.ServerPort,
                    definition.PinteModDetected,
                    RemoteAgentConfigurationStore.ProtectSecret(secret)));

                RemoteAgentProtocolService.EnsureQueueDirectories(root);
                var now = DateTimeOffset.UtcNow;
                var pairing = new RemoteAgentPairingEnvelope(
                    RemoteAgentProtocol.SchemaVersion,
                    agentId,
                    NormalizeDisplayName(definition.DisplayName),
                    Environment.MachineName,
                    Convert.ToBase64String(secret),
                    now,
                    now.AddMinutes(15));
                await RemoteAgentProtocolService.WriteJsonAtomicAsync(
                    RemoteAgentProtocolService.GetPairingPath(root), pairing, cancellationToken).ConfigureAwait(false);
                created.Add(Path.Combine(root, RemoteAgentProtocol.QueueFolderName, RemoteAgentProtocol.AgentFolderName, RemoteAgentProtocol.PairingFileName));

                var rconStore = new DpapiRconSecretStore(OperatorProfileStoragePaths.GetRconSecretPath(definition.ProfileId));
                var rconSecret = await rconStore.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(rconSecret) &&
                    await _orchestrator.PrepareRconSecretAsync(definition.ProfileId, rconSecret, cancellationToken).ConfigureAwait(false))
                {
                    preparedWorkerSecrets++;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }

        if (registrations.Count == 0)
        {
            return new ServerDeploymentResult(false, "Aucune racine BOIII locale valide n’a été trouvée.", [], []);
        }

        await _store.SaveAsync(new RemoteAgentConfiguration(RemoteAgentProtocol.SchemaVersion, registrations), cancellationToken)
            .ConfigureAwait(false);

        // The Control Center supersedes the historical visible Live/RCON tools.
        // Stop any leftovers before replacing/restarting the Agent so an old
        // first-party wrapper cannot keep those windows alive.
        await StopLegacyStandaloneConsolesAsync(cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(RemoteAgentConfigurationStore.GetAgentHome());
        var targetExe = RemoteAgentConfigurationStore.GetExecutablePath();

        // Stop the previous background copy before overwriting the single EXE.
        // Wait for the Agent mutex to disappear instead of relying on an arbitrary
        // fixed delay; otherwise the stop file can be left behind and the Agent can
        // remain permanently offline after a failed overwrite.
        try
        {
            await File.WriteAllTextAsync(
                    RemoteAgentConfigurationStore.GetStopRequestPath(),
                    DateTimeOffset.UtcNow.ToString("O"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        var stopped = await WaitForAgentStateAsync(expectedRunning: false, TimeSpan.FromSeconds(12), cancellationToken)
            .ConfigureAwait(false);
        if (!stopped)
        {
            TryDeleteStopRequest();
            return new ServerDeploymentResult(
                false,
                "L’Agent local n’a pas répondu à la demande d’arrêt. Son EXE n’a pas été remplacé afin d’éviter une installation partielle. Fermez puis relancez le Control Center sur ce PC.",
                created,
                skipped);
        }

        try
        {
            if (!string.Equals(Path.GetFullPath(sourceExe), Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(agentSourceExe, targetExe, overwrite: true);
            }
            created.Add(targetExe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteStopRequest();
            TryStartAgentProcess(targetExe);
            return new ServerDeploymentResult(
                false,
                "L’ancien Agent a été arrêté mais son EXE n’a pas pu être remplacé. L’Agent précédent a été relancé par sécurité : " + exception.Message,
                created,
                skipped);
        }

        if (await ManagedControlCenterInstallationService.InstallOrStageAsync(agentSourceExe, cancellationToken).ConfigureAwait(false))
        {
            created.Add(ManagedControlCenterInstallationService.GetExecutablePath());
        }
        else
        {
            skipped.Add("Application Control Center gérée : installation différée/impossible. L’Agent reste opérationnel.");
        }

        using (var run = Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true))
        {
            run?.SetValue(RunValueName, $"\"{targetExe}\" --remote-agent", RegistryValueKind.String);
        }

        TryDeleteStopRequest();
        RemoteAgentRecoveryTaskService.ClearUpdateInProgress();

        if (!RemoteAgentRecoveryTaskService.EnsureInstalled(targetExe, out var recoveryDiagnostic))
        {
            TryStartAgentProcess(targetExe);
            return new ServerDeploymentResult(
                false,
                "L’Agent a été préparé mais son auto-récupération Windows n’a pas pu être installée. " + recoveryDiagnostic,
                created,
                skipped);
        }
        created.Add("Auto-récupération Windows de l’Agent (ouverture de session + contrôle chaque minute)");

        var process = TryStartAgentProcess(targetExe);
        if (process is null)
        {
            return new ServerDeploymentResult(false, "Configuration Agent écrite mais Windows n’a pas démarré l’Agent.", created, skipped);
        }

        var started = await WaitForAgentStateAsync(expectedRunning: true, TimeSpan.FromSeconds(8), cancellationToken)
            .ConfigureAwait(false);
        if (!started)
        {
            return new ServerDeploymentResult(
                false,
                "Windows a lancé le processus Agent mais son verrou d’exécution n’est pas apparu. L’Agent s’est probablement arrêté immédiatement ; ouvrez de nouveau le Control Center sur ce PC pour lancer l’auto-réparation.",
                created,
                skipped);
        }

        return new ServerDeploymentResult(
            true,
            $"PC serveur préparé pour {registrations.Count} serveur(s). Agent en arrière-plan activé, vérifié et auto-récupérable par Windows ; s’il tombe, il est relancé sans ouvrir le Control Center. Le chemin du Control Center utilisé sur ce PC est mémorisé et sera mis à jour en place. Aucun raccourci Bureau/Menu Démarrer n’est imposé. Pairing valable 15 minutes. Outils PinteMod migrés sans consoles standalone : {hardenedServers}. Secrets Worker préparés automatiquement depuis le stockage RCON local : {preparedWorkerSecrets}/{registrations.Count}.",
            created,
            skipped);
    }

    public async Task<ServerDeploymentResult> DisableAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true);
            run?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return new ServerDeploymentResult(false, "L’Agent n’a pas pu être désactivé : le démarrage automatique Windows est inaccessible.", [], []);
        }

        RemoteAgentRecoveryTaskService.Remove(out _);
        try
        {
            Directory.CreateDirectory(RemoteAgentConfigurationStore.GetAgentHome());
            await File.WriteAllTextAsync(
                RemoteAgentConfigurationStore.GetStopRequestPath(),
                DateTimeOffset.UtcNow.ToString("O"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ServerDeploymentResult(false, "L’Agent n’a pas pu être arrêté : la demande d’arrêt locale est inaccessible.", [], []);
        }

        var stopped = await WaitForAgentStateAsync(expectedRunning: false, TimeSpan.FromSeconds(12), cancellationToken)
            .ConfigureAwait(false);
        if (!stopped)
        {
            return new ServerDeploymentResult(false, "L’Agent ne s’est pas arrêté. Terminez une fois PinteMod.ControlCenter dans le Gestionnaire des tâches : il ne redémarrera plus automatiquement.", [], []);
        }

        await _store.SaveAsync(new RemoteAgentConfiguration(RemoteAgentProtocol.SchemaVersion, []), cancellationToken)
            .ConfigureAwait(false);
        var removed = new List<string>();
        var skipped = new List<string>();
        foreach (var profile in existing.Profiles)
        {
            try
            {
                var root = Path.GetFullPath(profile.ServerRoot);
                var queue = Path.Combine(root, RemoteAgentProtocol.QueueFolderName);
                if (Directory.Exists(queue))
                {
                    Directory.Delete(queue, recursive: true);
                    removed.Add(queue);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                skipped.Add("File d’attente Agent non supprimée pour un serveur local.");
            }
        }

        return new ServerDeploymentResult(
            true,
            "Agent distant désactivé sur ce PC : démarrage automatique retiré, processus arrêté et files .pintemod-controlcenter supprimées.",
            removed,
            skipped);
    }


    internal static bool IsAgentHostRunning()
    {
        try
        {
            if (!Mutex.TryOpenExisting("PinteMod.ControlCenter.RemoteAgent.CurrentUser", out var mutex))
            {
                return false;
            }

            mutex.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // A mutex that exists but cannot be opened still proves another Agent
            // process owns the named synchronization object.
            return true;
        }
    }

    private static async Task<bool> WaitForAgentStateAsync(
        bool expectedRunning,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsAgentHostRunning() == expectedRunning) return true;
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }
        return IsAgentHostRunning() == expectedRunning;
    }

    private static Process? TryStartAgentProcess(string targetExe)
    {
        try
        {
            if (!File.Exists(targetExe)) return null;
            return Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                Arguments = "--remote-agent --agent-manual-repair",
                WorkingDirectory = RemoteAgentConfigurationStore.GetAgentHome(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void TryDeleteStopRequest()
    {
        try
        {
            var path = RemoteAgentConfigurationStore.GetStopRequestPath();
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task StopLegacyStandaloneConsolesAsync(CancellationToken cancellationToken)
    {
        const string script = @"
$targets=@(
 'PinteMod_LiveConsole.ps1',
 'PinteMod_Remote_RCON.ps1',
 'PinteMod_Remote_Tools_Launcher.ps1',
 'PinteMod_Launch_SingleInstance.ps1',
 'PinteMod_Server_Launcher.ps1'
)
Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -in @('powershell.exe','pwsh.exe') } |
  ForEach-Object {
    $cmd=[string]$_.CommandLine
    if([string]::IsNullOrWhiteSpace($cmd)){ return }
    foreach($target in $targets){
      if($cmd.IndexOf($target,[StringComparison]::OrdinalIgnoreCase) -ge 0){
        Stop-Process -Id ([int]$_.ProcessId) -Force -ErrorAction SilentlyContinue
        break
      }
    }
  }";

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var info = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = Process.Start(info);
            if (process is null) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(6));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Best-effort cleanup only. Agent installation itself must not hang.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }
    }

    private static string CreateStableAgentId(string root)
    {
        var canonical = Environment.MachineName.ToUpperInvariant() + "|" + root.ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return "agent-" + Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant();
    }

    private static string NormalizeDisplayName(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0) normalized = "Serveur BOIII";
        return normalized.Length <= 48 ? normalized : normalized[..48];
    }

    private static string NormalizeLauncher(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0) normalized = "Server.bat";
        if (Path.IsPathRooted(normalized) || normalized.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == ".."))
        {
            throw new InvalidOperationException("Le lanceur Agent doit rester relatif à la racine serveur.");
        }
        return normalized;
    }
}
