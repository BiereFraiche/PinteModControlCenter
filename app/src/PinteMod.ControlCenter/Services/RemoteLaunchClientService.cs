using System.IO;
using System.Security.Cryptography;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Security;

namespace PinteMod.ControlCenter.Services;

internal sealed class RemoteLaunchClientService
{
    public async Task<RemoteAgentPairingResult> PairAsync(
        string serverRoot,
        string profileId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverRoot) || !serverRoot.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return new RemoteAgentPairingResult(false, "Le pairing distant exige une racine UNC.");
        }

        var pairingPath = RemoteAgentProtocolService.GetPairingPath(serverRoot.Trim());
        var pairing = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentPairingEnvelope>(pairingPath, cancellationToken)
            .ConfigureAwait(false);
        if (pairing is null || pairing.SchemaVersion != RemoteAgentProtocol.SchemaVersion ||
            string.IsNullOrWhiteSpace(pairing.AgentId) || pairing.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            pairing.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return new RemoteAgentPairingResult(false, "Pairing absent, invalide ou expiré. Sur le PC serveur, régénérez l’Agent/pairing puis réessayez sous 15 minutes.");
        }

        byte[]? secret = null;
        try
        {
            secret = Convert.FromBase64String(pairing.SecretBase64);
            if (secret.Length != 32)
            {
                return new RemoteAgentPairingResult(false, "Clé de pairing invalide.");
            }

            var store = new DpapiRemoteAgentSecretStore(
                OperatorProfileStoragePaths.GetRemoteAgentSecretPath(profileId));
            await store.SaveAsync(secret, cancellationToken).ConfigureAwait(false);
            try { File.Delete(pairingPath); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            return new RemoteAgentPairingResult(true, $"Pairing réussi avec {pairing.MachineName}. La clé Agent est stockée localement via DPAPI.", pairing.AgentId);
        }
        catch (FormatException)
        {
            return new RemoteAgentPairingResult(false, "Clé de pairing illisible.");
        }
        finally
        {
            if (secret is not null) CryptographicOperations.ZeroMemory(secret);
        }
    }


    public async Task<RemoteAgentProfileCatalogResult> ReadProfileCatalogAsync(
        string serverRoot,
        string profileId,
        string remoteAgentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverRoot) || !serverRoot.StartsWith(@"\\", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(remoteAgentId))
        {
            return RemoteAgentProfileCatalogResult.Unavailable("Catalogue distant indisponible : profil UNC non appairé.");
        }

        var secretStore = new DpapiRemoteAgentSecretStore(
            OperatorProfileStoragePaths.GetRemoteAgentSecretPath(profileId));
        var secret = await secretStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (secret is null)
        {
            return RemoteAgentProfileCatalogResult.Unavailable("Catalogue distant disponible uniquement après pairing sécurisé.");
        }

        try
        {
            var catalog = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentProfileCatalogEnvelope>(
                RemoteAgentProtocolService.GetProfileCatalogPath(serverRoot), cancellationToken).ConfigureAwait(false);
            if (catalog is null ||
                catalog.SchemaVersion != RemoteAgentProtocol.SchemaVersion ||
                !string.Equals(catalog.AuthorityAgentId, remoteAgentId, StringComparison.Ordinal) ||
                catalog.Profiles is null || catalog.Profiles.Count is < 1 or > 8 ||
                string.IsNullOrWhiteSpace(catalog.MachineName) || catalog.MachineName.Length > 128 ||
                catalog.MachineName.Any(char.IsControl) ||
                catalog.UpdatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(2) ||
                DateTimeOffset.UtcNow - catalog.UpdatedAtUtc > TimeSpan.FromSeconds(30) ||
                !RemoteAgentProtocolService.VerifyProfileCatalog(catalog, secret) ||
                catalog.Profiles.Any(entry => !IsValidCatalogEntry(entry)))
            {
                return RemoteAgentProfileCatalogResult.Unavailable("Catalogue distant absent, périmé ou non authentifié.");
            }

            return new RemoteAgentProfileCatalogResult(
                true,
                $"Catalogue authentifié de {catalog.MachineName} : {catalog.Profiles.Count} serveur(s).",
                catalog.MachineName,
                catalog.Profiles);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static bool IsValidCatalogEntry(RemoteAgentProfileCatalogEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.AgentId) || entry.AgentId.Length > 80 ||
            entry.AgentId.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')) ||
            !OperatorConfiguration.IsValidProfileDisplayName(entry.DisplayName) ||
            !RemoteAgentCatalogPathResolver.IsSafeLeaf(entry.RootFolderName) ||
            entry.ServerPort is < 1 or > 65535)
        {
            return false;
        }

        var launcher = entry.LauncherRelativePath?.Trim() ?? string.Empty;
        return launcher.Length <= 260 &&
               !Path.IsPathRooted(launcher) &&
               launcher.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
               !launcher.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   .Any(segment => segment == "..");
    }

    public async Task<RemoteAgentProbeResult> ProbeAsync(
        string serverRoot,
        string profileId,
        string remoteAgentId,
        CancellationToken cancellationToken = default)
    {
        var statusPath = RemoteAgentProtocolService.GetStatusPath(serverRoot);
        var status = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentStatusEnvelope>(statusPath, cancellationToken)
            .ConfigureAwait(false);
        if (status is null)
        {
            return new RemoteAgentProbeResult(false, false, false, "Agent distant non détecté.");
        }

        RemoteAgentProbeResult WithIdentity(RemoteAgentProbeResult result) => result with
        {
            AgentVersion = status.AgentVersion ?? string.Empty,
            MachineName = status.MachineName ?? string.Empty
        };

        var store = new DpapiRemoteAgentSecretStore(
            OperatorProfileStoragePaths.GetRemoteAgentSecretPath(profileId));
        var secret = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (secret is null || string.IsNullOrWhiteSpace(remoteAgentId))
        {
            return WithIdentity(new RemoteAgentProbeResult(true, false, false, "Agent détecté · pairing requis.", status.UpdatedAtUtc));
        }

        try
        {
            if (!string.Equals(status.AgentId, remoteAgentId, StringComparison.Ordinal) ||
                !RemoteAgentProtocolService.VerifyStatus(status, secret))
            {
                return WithIdentity(new RemoteAgentProbeResult(true, true, false, "Agent détecté mais statut non authentifié.", status.UpdatedAtUtc));
            }

            var currentVersion = RemoteAgentProtocolService.GetAgentVersion();
            if (!string.Equals(status.AgentVersion, currentVersion, StringComparison.Ordinal))
            {
                var comparison = GitHubUpdateCheckService.CompareVersions(status.AgentVersion, currentVersion);
                var message = comparison switch
                {
                    > 0 => $"Agent {status.AgentVersion} détecté sur {status.MachineName} · ce PC utilise encore {currentVersion}. Utilisez SYNCHRONISER LES DEUX PC : la version distante plus récente sera récupérée ici après vérification.",
                    < 0 => $"Agent {status.AgentVersion} détecté sur {status.MachineName} · ce PC utilise {currentVersion}, plus récent. Utilisez SYNCHRONISER LES DEUX PC pour mettre à niveau l’application et l’Agent distants.",
                    _ => $"Versions Control Center différentes : ce PC utilise {currentVersion}, {status.MachineName} utilise {status.AgentVersion}. Synchronisez les deux PC avant les commandes distantes."
                };
                return WithIdentity(new RemoteAgentProbeResult(true, true, false, message, status.UpdatedAtUtc));
            }

            var online = DateTimeOffset.UtcNow - status.UpdatedAtUtc <= TimeSpan.FromSeconds(15);
            return WithIdentity(new RemoteAgentProbeResult(true, true, online,
                online ? $"Agent ONLINE · {status.MachineName} · {status.AgentVersion}" : "Agent OFFLINE ou heartbeat périmé.", status.UpdatedAtUtc));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }


    public async Task<ServerLaunchResult> UpdateAgentAsync(
        string serverRoot,
        string profileId,
        string remoteAgentId,
        IProgress<RemoteAgentUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new RemoteAgentUpdateProgress(5, "Vérification de la liaison sécurisée…"));
        if (string.IsNullOrWhiteSpace(serverRoot) || !serverRoot.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return new ServerLaunchResult(false, "La mise à jour distante de l’Agent exige une racine UNC.");
        }
        if (string.IsNullOrWhiteSpace(remoteAgentId))
        {
            return new ServerLaunchResult(false, "Profil non pairé avec l’Agent distant.");
        }

        progress?.Report(new RemoteAgentUpdateProgress(10, "Lecture de l’appairage local…"));
        var store = new DpapiRemoteAgentSecretStore(
            OperatorProfileStoragePaths.GetRemoteAgentSecretPath(profileId));
        var secret = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (secret is null)
        {
            return new ServerLaunchResult(false, "Clé Agent distante absente. Refaire le pairing.");
        }

        try
        {
            progress?.Report(new RemoteAgentUpdateProgress(15, "Vérification de l’Agent distant…"));
            var status = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentStatusEnvelope>(
                RemoteAgentProtocolService.GetStatusPath(serverRoot), cancellationToken).ConfigureAwait(false);
            if (status is null ||
                !string.Equals(status.AgentId, remoteAgentId, StringComparison.Ordinal) ||
                !RemoteAgentProtocolService.VerifyStatus(status, secret))
            {
                return new ServerLaunchResult(false, "Agent distant absent ou statut non authentifié.");
            }

            progress?.Report(new RemoteAgentUpdateProgress(18, "Vérification du heartbeat Agent en temps réel…"));
            var liveStatus = await WaitForAuthenticatedHeartbeatAdvanceAsync(
                serverRoot, remoteAgentId, status, secret, cancellationToken).ConfigureAwait(false);
            if (liveStatus is null)
            {
                return new ServerLaunchResult(
                    false,
                    $"Le statut Agent {status.AgentVersion} est authentifié mais son heartbeat n’évolue plus. L’Agent est arrêté ou bloqué sur le PC serveur ; aucune mise à jour n’a été déposée. Cette installation est antérieure au superviseur de récupération Agent : SMB peut déposer des fichiers mais ne peut pas démarrer un processus Windows arrêté. Réactivez une seule fois l’Agent existant sur {status.MachineName}; dès qu’une version équipée du superviseur est poussée, Windows maintiendra ensuite l’Agent auto-récupérable sans ouvrir le Control Center distant.");
            }
            status = liveStatus;

            var targetVersion = RemoteAgentProtocolService.GetAgentVersion();
            var agentAlreadyCurrent = string.Equals(status.AgentVersion, targetVersion, StringComparison.Ordinal);
            var remoteVsLocal = GitHubUpdateCheckService.CompareVersions(status.AgentVersion, targetVersion);
            if (remoteVsLocal > 0)
            {
                return await PullNewerRemotePackageAsync(
                    serverRoot,
                    remoteAgentId,
                    status,
                    secret,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!agentAlreadyCurrent && !SupportsSelfUpdate(status.AgentVersion))
            {
                return new ServerLaunchResult(
                    false,
                    $"Agent {status.AgentVersion} détecté. Cette ancienne version ne sait pas encore s’auto-mettre à jour : installez la Preview 3M une dernière fois directement sur le PC serveur. Après 3M, les mises à jour suivantes pourront être poussées depuis ce PC.");
            }

            progress?.Report(new RemoteAgentUpdateProgress(25, "Préparation de la nouvelle version…"));
            var sourceExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(sourceExe) || !File.Exists(sourceExe) ||
                !Path.GetExtension(sourceExe).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return new ServerLaunchResult(false, "La mise à jour Agent doit être lancée depuis le PinteMod.ControlCenter.exe publié.");
            }

            RemoteAgentProtocolService.EnsureQueueDirectories(serverRoot);
            progress?.Report(new RemoteAgentUpdateProgress(35, "Calcul de l’empreinte SHA-256…"));
            var hash = await ComputeSha256Async(sourceExe, cancellationToken).ConfigureAwait(false);

            // Never rely on wall-clock ordering between the laptop and the server PC.
            // A signed available-package manifest is an exact proof of the Agent EXE
            // currently being republished by the remote Agent because it contains the
            // SHA-256 computed from Environment.ProcessPath on that machine.
            var publishedBeforeUpdate = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentAvailablePackageEnvelope>(
                RemoteAgentProtocolService.GetAvailablePackageManifestPath(serverRoot), cancellationToken).ConfigureAwait(false);
            if (IsExactPublishedAgentBuild(status, publishedBeforeUpdate, remoteAgentId, targetVersion, hash, secret))
            {
                progress?.Report(new RemoteAgentUpdateProgress(100, "Les deux PC utilisent déjà exactement la même build."));
                return new ServerLaunchResult(
                    true,
                    $"PC serveur déjà synchronisé avec {targetVersion} · SHA-256 Agent identique et authentifié.");
            }

            var fileName = $"PinteMod.ControlCenter.{hash[..16]}.exe";
            var packagePath = Path.Combine(RemoteAgentProtocolService.GetUpdatesPath(serverRoot), fileName);
            var temp = packagePath + ".tmp." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N");
            progress?.Report(new RemoteAgentUpdateProgress(45, "Envoi du Control Center vers le PC serveur…"));
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
                try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }

            progress?.Report(new RemoteAgentUpdateProgress(60, "Signature et validation du package…"));
            var now = DateTimeOffset.UtcNow;
            var update = new RemoteAgentUpdateEnvelope(
                RemoteAgentProtocol.SchemaVersion,
                remoteAgentId,
                targetVersion,
                fileName,
                hash,
                now,
                now.AddMinutes(5),
                string.Empty);
            update = update with { Signature = RemoteAgentProtocolService.SignUpdate(update, secret) };
            var manifestPath = RemoteAgentProtocolService.GetUpdateManifestPath(serverRoot);
            await RemoteAgentProtocolService.WriteJsonAtomicAsync(
                manifestPath, update, cancellationToken).ConfigureAwait(false);

            progress?.Report(new RemoteAgentUpdateProgress(72, "Redémarrage de l’Agent distant…"));
            var waitStarted = DateTimeOffset.UtcNow;
            var deadline = waitStarted.AddSeconds(75);
            RemoteAgentStatusEnvelope? lastAuthenticatedStatus = null;
            RemoteAgentAvailablePackageEnvelope? lastPublishedPackage = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var elapsed = DateTimeOffset.UtcNow - waitStarted;
                var waitPercent = 72 + (int)Math.Min(23, elapsed.TotalSeconds / 75d * 23d);
                progress?.Report(new RemoteAgentUpdateProgress(waitPercent, "Attente de la preuve SHA-256 du nouvel Agent…"));
                await Task.Delay(750, cancellationToken).ConfigureAwait(false);

                var refreshed = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentStatusEnvelope>(
                    RemoteAgentProtocolService.GetStatusPath(serverRoot), cancellationToken).ConfigureAwait(false);
                if (refreshed is not null &&
                    string.Equals(refreshed.AgentId, remoteAgentId, StringComparison.Ordinal) &&
                    RemoteAgentProtocolService.VerifyStatus(refreshed, secret))
                {
                    lastAuthenticatedStatus = refreshed;
                }

                var published = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentAvailablePackageEnvelope>(
                    RemoteAgentProtocolService.GetAvailablePackageManifestPath(serverRoot), cancellationToken).ConfigureAwait(false);
                if (published is not null &&
                    string.Equals(published.AgentId, remoteAgentId, StringComparison.Ordinal) &&
                    RemoteAgentProtocolService.VerifyAvailablePackage(published, secret))
                {
                    lastPublishedPackage = published;
                }

                // Exact cryptographic proof, independent of any clock difference
                // between the two PCs: authenticated Agent version + authenticated
                // SHA-256 republished from the running Agent executable.
                if (!File.Exists(manifestPath) &&
                    IsExactPublishedAgentBuild(lastAuthenticatedStatus, lastPublishedPackage, remoteAgentId, targetVersion, hash, secret))
                {
                    progress?.Report(new RemoteAgentUpdateProgress(100, "Synchronisation confirmée par SHA-256."));
                    return new ServerLaunchResult(
                        true,
                        agentAlreadyCurrent
                            ? $"PC serveur synchronisé avec {targetVersion}. Le nouvel Agent republie exactement le SHA-256 attendu ; aucun décalage d’horloge entre les PC n’est utilisé pour la confirmation."
                            : $"PC serveur mis à jour automatiquement vers {targetVersion}. Le nouvel Agent republie exactement le SHA-256 attendu ; le Control Center utilisateur sera maintenu sur cette même build dès que son EXE pourra être remplacé en place.");
                }
            }

            progress?.Report(new RemoteAgentUpdateProgress(100, "Synchronisation non confirmée dans le délai prévu."));
            if (File.Exists(manifestPath))
            {
                return new ServerLaunchResult(false, "Le paquet est bien déposé, mais l’Agent distant n’a pas consommé la demande update.json. Vérifiez que l’Agent tourne sur le PC serveur puis réessayez.");
            }
            if (lastAuthenticatedStatus is null)
            {
                return new ServerLaunchResult(false, "La demande de mise à jour a été consommée, mais aucun statut Agent authentifié n’a été republié après le redémarrage.");
            }
            if (!string.Equals(lastAuthenticatedStatus.AgentVersion, targetVersion, StringComparison.Ordinal))
            {
                return new ServerLaunchResult(false, $"L’Agent a redémarré mais annonce encore {lastAuthenticatedStatus.AgentVersion} au lieu de {targetVersion}. La mise à jour de l’Agent n’a pas été appliquée.");
            }
            if (lastPublishedPackage is null)
            {
                return new ServerLaunchResult(false, $"L’Agent annonce {targetVersion}, mais n’a pas encore republié son package SHA-256 authentifié. Relancez la synchronisation dans quelques secondes.");
            }
            return new ServerLaunchResult(false, $"L’Agent annonce {targetVersion}, mais son SHA-256 republié ({ShortHash(lastPublishedPackage.Sha256)}) ne correspond pas au SHA attendu ({ShortHash(hash)}). Aucune synchronisation n’est déclarée réussie.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }


    internal static bool IsAuthenticatedHeartbeatAdvance(
        RemoteAgentStatusEnvelope initial,
        RemoteAgentStatusEnvelope candidate,
        string remoteAgentId,
        byte[] secret)
    {
        return candidate.SchemaVersion == RemoteAgentProtocol.SchemaVersion &&
               string.Equals(candidate.AgentId, remoteAgentId, StringComparison.Ordinal) &&
               RemoteAgentProtocolService.VerifyStatus(candidate, secret) &&
               candidate.UpdatedAtUtc != initial.UpdatedAtUtc;
    }

    private static async Task<RemoteAgentStatusEnvelope?> WaitForAuthenticatedHeartbeatAdvanceAsync(
        string serverRoot,
        string remoteAgentId,
        RemoteAgentStatusEnvelope initial,
        byte[] secret,
        CancellationToken cancellationToken)
    {
        // Clock-independent liveness proof: the Agent republishes a signed status
        // every five seconds. We only require the authenticated timestamp to change,
        // never compare the two PCs' wall clocks.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(9);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(750, cancellationToken).ConfigureAwait(false);
            var candidate = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentStatusEnvelope>(
                RemoteAgentProtocolService.GetStatusPath(serverRoot), cancellationToken).ConfigureAwait(false);
            if (candidate is not null &&
                IsAuthenticatedHeartbeatAdvance(initial, candidate, remoteAgentId, secret))
            {
                return candidate;
            }
        }
        return null;
    }

    internal static bool IsExactPublishedAgentBuild(
        RemoteAgentStatusEnvelope? status,
        RemoteAgentAvailablePackageEnvelope? publishedPackage,
        string remoteAgentId,
        string targetVersion,
        string targetSha256,
        byte[] secret)
    {
        if (status is null || publishedPackage is null ||
            string.IsNullOrWhiteSpace(remoteAgentId) ||
            string.IsNullOrWhiteSpace(targetVersion) ||
            string.IsNullOrWhiteSpace(targetSha256) || targetSha256.Length != 64)
        {
            return false;
        }

        return status.SchemaVersion == RemoteAgentProtocol.SchemaVersion &&
               publishedPackage.SchemaVersion == RemoteAgentProtocol.SchemaVersion &&
               string.Equals(status.AgentId, remoteAgentId, StringComparison.Ordinal) &&
               string.Equals(publishedPackage.AgentId, remoteAgentId, StringComparison.Ordinal) &&
               string.Equals(status.AgentVersion, targetVersion, StringComparison.Ordinal) &&
               string.Equals(publishedPackage.Version, targetVersion, StringComparison.Ordinal) &&
               string.Equals(publishedPackage.Sha256, targetSha256, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(publishedPackage.PackageFileName) &&
               string.Equals(Path.GetFileName(publishedPackage.PackageFileName), publishedPackage.PackageFileName, StringComparison.Ordinal) &&
               RemoteAgentProtocolService.VerifyStatus(status, secret) &&
               RemoteAgentProtocolService.VerifyAvailablePackage(publishedPackage, secret);
    }

    private static string ShortHash(string? hash) =>
        string.IsNullOrWhiteSpace(hash) ? "inconnu" : hash[..Math.Min(12, hash.Length)];

    private static async Task<ServerLaunchResult> PullNewerRemotePackageAsync(
        string serverRoot,
        string remoteAgentId,
        RemoteAgentStatusEnvelope status,
        byte[] secret,
        IProgress<RemoteAgentUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new RemoteAgentUpdateProgress(25, "Le PC serveur possède la version la plus récente…"));
        var manifestPath = RemoteAgentProtocolService.GetAvailablePackageManifestPath(serverRoot);
        RemoteAgentAvailablePackageEnvelope? available = null;

        var deadline = DateTimeOffset.UtcNow.AddSeconds(12);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            available = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentAvailablePackageEnvelope>(
                manifestPath, cancellationToken).ConfigureAwait(false);
            if (available is not null &&
                available.SchemaVersion == RemoteAgentProtocol.SchemaVersion &&
                string.Equals(available.AgentId, remoteAgentId, StringComparison.Ordinal) &&
                string.Equals(available.Version, status.AgentVersion, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(available.PackageFileName) && available.PackageFileName.Length <= 120 &&
                string.Equals(Path.GetFileName(available.PackageFileName), available.PackageFileName, StringComparison.Ordinal) &&
                available.PackageFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(available.Sha256) && available.Sha256.Length == 64 && available.Sha256.All(Uri.IsHexDigit) &&
                RemoteAgentProtocolService.VerifyAvailablePackage(available, secret))
            {
                break;
            }

            available = null;
            await Task.Delay(750, cancellationToken).ConfigureAwait(false);
        }

        if (available is null)
        {
            return new ServerLaunchResult(
                false,
                $"{status.MachineName} utilise {status.AgentVersion}, plus récent que ce PC. L’Agent distant n’a pas encore publié de package retour authentifié. Laissez l’Agent tourner quelques secondes puis relancez SYNCHRONISER LES DEUX PC.");
        }

        var updatesRoot = Path.GetFullPath(RemoteAgentProtocolService.GetUpdatesPath(serverRoot))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var remotePackagePath = Path.GetFullPath(Path.Combine(updatesRoot, available.PackageFileName));
        if (!remotePackagePath.StartsWith(updatesRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(remotePackagePath))
        {
            return new ServerLaunchResult(false, "Package retour distant absent ou hors de la zone de mise à jour autorisée.");
        }

        var info = new FileInfo(remotePackagePath);
        if (info.Length is <= 0 or > 512L * 1024L * 1024L)
        {
            return new ServerLaunchResult(false, "Package retour distant de taille invalide.");
        }

        progress?.Report(new RemoteAgentUpdateProgress(45, "Récupération de la version la plus récente…"));
        var localStagingDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PinteMod",
            "ControlCenter",
            "incoming");
        Directory.CreateDirectory(localStagingDirectory);
        var localPackage = Path.Combine(localStagingDirectory, $"PinteMod.ControlCenter.{available.Sha256[..16]}.exe");
        var temp = localPackage + ".tmp." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N");
        try
        {
            await using (var source = new FileStream(remotePackagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temp, localPackage, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }

        progress?.Report(new RemoteAgentUpdateProgress(65, "Vérification SHA-256 du package récupéré…"));
        var localHash = await ComputeSha256Async(localPackage, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(localHash, available.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(localPackage); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            return new ServerLaunchResult(false, "Package retour rejeté : l’empreinte SHA-256 ne correspond pas au manifeste authentifié.");
        }

        progress?.Report(new RemoteAgentUpdateProgress(82, "Préparation de la mise à jour de ce Control Center…"));
        var staged = await PreferredControlCenterPathService.StageCurrentExecutableUpdateAsync(
            localPackage, Environment.ProcessId, cancellationToken).ConfigureAwait(false);
        if (!staged)
        {
            return new ServerLaunchResult(false, "La version distante est authentifiée mais ce Control Center n’a pas pu préparer son remplacement au même emplacement.");
        }

        progress?.Report(new RemoteAgentUpdateProgress(100, "Version la plus récente récupérée · redémarrage du Control Center…"));
        return new ServerLaunchResult(
            true,
            $"{status.MachineName} possède {available.Version}, plus récent que ce PC. Le package a été vérifié par HMAC + SHA-256 et ce Control Center va redémarrer automatiquement sur cette version. Les deux PC convergent vers la version la plus récente disponible.",
            ApplicationRestartRequired: true);
    }

    private static bool SupportsSelfUpdate(string version)
    {
        const string marker = "preview-manager.3";
        var index = version.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return true; // RC/stable versions produced after 3M keep the capability.
        var suffixIndex = index + marker.Length;
        return suffixIndex < version.Length && char.ToLowerInvariant(version[suffixIndex]) >= 'm';
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    public Task<ServerLaunchResult> LaunchAsync(
        string serverRoot,
        string profileId,
        string remoteAgentId,
        CancellationToken cancellationToken = default) =>
        SendActionAsync(
            serverRoot,
            profileId,
            remoteAgentId,
            RemoteAgentProtocol.LaunchAction,
            "démarrage",
            cancellationToken);

    public Task<ServerLaunchResult> StopAsync(
        string serverRoot,
        string profileId,
        string remoteAgentId,
        CancellationToken cancellationToken = default) =>
        SendActionAsync(
            serverRoot,
            profileId,
            remoteAgentId,
            RemoteAgentProtocol.StopAction,
            "arrêt",
            cancellationToken);

    private async Task<ServerLaunchResult> SendActionAsync(
        string serverRoot,
        string profileId,
        string remoteAgentId,
        string action,
        string operationLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteAgentId))
        {
            return new ServerLaunchResult(false, "Profil non pairé avec l’Agent distant.");
        }

        if (action is not (RemoteAgentProtocol.LaunchAction or RemoteAgentProtocol.StopAction))
        {
            return new ServerLaunchResult(false, "Action Agent non autorisée.");
        }

        var store = new DpapiRemoteAgentSecretStore(
            OperatorProfileStoragePaths.GetRemoteAgentSecretPath(profileId));
        var secret = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (secret is null)
        {
            return new ServerLaunchResult(false, "Clé Agent distante absente. Refaire le pairing.");
        }

        try
        {
            RemoteAgentProtocolService.EnsureQueueDirectories(serverRoot);
            var now = DateTimeOffset.UtcNow;
            var request = new RemoteLaunchRequest(
                RemoteAgentProtocol.SchemaVersion,
                Guid.NewGuid().ToString("N"),
                remoteAgentId,
                action,
                now,
                now.AddSeconds(90),
                Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                string.Empty);
            request = request with { Signature = RemoteAgentProtocolService.SignRequest(request, secret) };

            var requestPath = Path.Combine(RemoteAgentProtocolService.GetRequestsPath(serverRoot), request.RequestId + ".json");
            var responsePath = Path.Combine(RemoteAgentProtocolService.GetResponsesPath(serverRoot), request.RequestId + ".json");
            await RemoteAgentProtocolService.WriteJsonAtomicAsync(requestPath, request, cancellationToken).ConfigureAwait(false);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(50);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteLaunchResponse>(responsePath, cancellationToken)
                    .ConfigureAwait(false);
                if (response is not null)
                {
                    try { File.Delete(responsePath); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                    if (response.SchemaVersion != RemoteAgentProtocol.SchemaVersion ||
                        !string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal) ||
                        !string.Equals(response.AgentId, remoteAgentId, StringComparison.Ordinal) ||
                        !RemoteAgentProtocolService.VerifyResponse(response, secret))
                    {
                        return new ServerLaunchResult(false, "Réponse Agent invalide ou non authentifiée.");
                    }
                    return new ServerLaunchResult(
                        string.Equals(response.Status, "applied", StringComparison.Ordinal),
                        response.Message,
                        response.ProcessId);
                }
                await Task.Delay(350, cancellationToken).ConfigureAwait(false);
            }

            return new ServerLaunchResult(false, $"L’Agent distant n’a pas répondu avant la fin de la vérification de {operationLabel}. Vérifiez son état sur le PC serveur.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }
}
