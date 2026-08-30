using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class EmbeddedServerPayloadService
{
    public const string StablePinteModVersion = "2.1.1";
    public const string CurrentPinteModPayloadLabel = "2.1.1 + modules validés 2026-08-19";
    public const string BridgeVersion = "0.3.1";

    private const string PinteModResourceSuffix = ".Payloads.PinteMod_CURRENT_20260819.zip";
    private const string BridgeResourceSuffix = ".Payloads.ezz_admin_control_center_contracts_v0.3.1.gsc";
    private const string InstallationVerifierRelativePath = "boiii/tools/Verify_PinteMod_Installation.ps1";
    internal const string LegacyInstallationVerifierSha256 = "02DF5C769D6B3E38B6053989078D2BC1A28810BB32D21584DD39502A4B11BE3B";
    private readonly Assembly _assembly;

    public EmbeddedServerPayloadService(Assembly? assembly = null)
    {
        _assembly = assembly ?? typeof(EmbeddedServerPayloadService).Assembly;
    }

    public async Task<ServerDeploymentResult> InstallPinteModStableAsync(
        string serverRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateServerRoot(serverRoot);
        var created = new List<string>();
        var skipped = new List<string>();
        await using var resource = OpenResource(PinteModResourceSuffix);
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read, leaveOpen: false);
        var operations = new List<(ZipArchiveEntry Entry, string Target, bool ReplaceKnownLegacyFile)>();

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var target = ResolveSafeTarget(root, entry.FullName);
            if (File.Exists(target))
            {
                await using var entryStream = entry.Open();
                var embeddedHash = await ComputeSha256Async(entryStream, cancellationToken).ConfigureAwait(false);
                var existingHash = await ComputeFileSha256Async(target, cancellationToken).ConfigureAwait(false);
                if (embeddedHash.Equals(existingHash, StringComparison.OrdinalIgnoreCase))
                {
                    skipped.Add(Relative(root, target));
                    continue;
                }

                if (CanReplaceKnownLegacyFile(entry.FullName, existingHash))
                {
                    operations.Add((entry, target, true));
                    continue;
                }

                return new ServerDeploymentResult(
                    false,
                    $"Installation refusée : le fichier « {Relative(root, target)} » existe déjà avec un contenu différent. Aucun fichier existant n’a été écrasé.",
                    [],
                    skipped);
            }

            operations.Add((entry, target, false));
        }

        try
        {
            foreach (var operation in operations.OrderBy(operation => operation.ReplaceKnownLegacyFile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(operation.Target)!);
                var temp = operation.Target + ".pintemod-manager.tmp";
                await using (var source = operation.Entry.Open())
                await using (var destination = new FileStream(
                                 temp,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                if (operation.ReplaceKnownLegacyFile)
                {
                    File.Replace(temp, operation.Target, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temp, operation.Target);
                }
                created.Add(Relative(root, operation.Target));
            }

            return new ServerDeploymentResult(
                true,
                $"PinteMod {CurrentPinteModPayloadLabel} installé/vérifié sans écraser de fichier existant. Le vérificateur v2.1.1 stock connu peut être mis à niveau. Installez ensuite le Bridge Control Center.",
                created,
                skipped);
        }
        catch
        {
            foreach (var path in created.AsEnumerable().Reverse())
            {
                try
                {
                    var full = ResolveSafeTarget(root, path);
                    if (File.Exists(full))
                    {
                        File.Delete(full);
                    }
                }
                catch
                {
                }
            }

            throw;
        }
    }

    private static bool CanReplaceKnownLegacyFile(string entryPath, string existingHash) =>
        entryPath.Replace('\\', '/').Equals(InstallationVerifierRelativePath, StringComparison.OrdinalIgnoreCase) &&
        existingHash.Equals(LegacyInstallationVerifierSha256, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Répare uniquement le vérificateur d'installation PinteMod. Cette voie est
    /// réservée à une installation PinteMod déjà détectée : aucun module, service
    /// ou script existant n'est relu ni modifié.
    /// </summary>
    public async Task<ServerDeploymentResult> RepairInstallationVerifierAsync(
        string serverRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateServerRoot(serverRoot);
        var target = ResolveSafeTarget(root, InstallationVerifierRelativePath);
        byte[] embeddedBytes;
        await using (var resource = OpenResource(PinteModResourceSuffix))
        using (var archive = new ZipArchive(resource, ZipArchiveMode.Read, leaveOpen: false))
        {
            var entry = archive.GetEntry(InstallationVerifierRelativePath)
                        ?? throw new InvalidOperationException("Vérificateur PinteMod absent du payload embarqué.");
            await using var source = entry.Open();
            using var memory = new MemoryStream();
            await source.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            embeddedBytes = memory.ToArray();
        }

        var embeddedHash = Convert.ToHexString(SHA256.HashData(embeddedBytes));
        if (File.Exists(target))
        {
            var existingHash = await ComputeFileSha256Async(target, cancellationToken).ConfigureAwait(false);
            if (existingHash.Equals(embeddedHash, StringComparison.OrdinalIgnoreCase))
            {
                return new ServerDeploymentResult(
                    true,
                    "Vérificateur PinteMod déjà à jour. Aucun module ni service existant n’a été modifié.",
                    [],
                    [Relative(root, target)]);
            }

            if (!CanReplaceKnownLegacyFile(InstallationVerifierRelativePath, existingHash))
            {
                return new ServerDeploymentResult(
                    false,
                    "Réparation refusée : le vérificateur PinteMod présent est inconnu. Aucun module, service ou script existant n’a été modifié.",
                    [],
                    []);
            }

            var backup = target + ".manager-backup-v2.1.1";
            if (!File.Exists(backup))
            {
                File.Copy(target, backup, overwrite: false);
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        }

        await WriteAllBytesSafeAsync(target, embeddedBytes, cancellationToken).ConfigureAwait(false);
        return new ServerDeploymentResult(
            true,
            "Vérificateur PinteMod mis à jour. Aucun module ni service PinteMod existant n’a été modifié.",
            [Relative(root, target)],
            []);
    }

    public async Task<ServerDeploymentResult> InstallOrUpdateBridgeAsync(
        string serverRoot,
        IReadOnlyCollection<string> allowedMaps,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateServerRoot(serverRoot);
        var analyzer = new ServerInstallationAnalyzer();
        var analysis = analyzer.Analyze(root, cancellationToken);
        if (!analysis.PinteModDetected)
        {
            return new ServerDeploymentResult(
                false,
                "Le Bridge PinteMod v0.3.1 exige une installation PinteMod cohérente. Pour BOIII sans PinteMod, utilisez le futur Generic Bridge.",
                [],
                []);
        }

        var maps = NormalizeMaps(allowedMaps);
        var target = Path.Combine(root, "boiii", "custom_scripts", "ezz_admin_control_center_contracts.gsc");
        byte[] bridgeBytes;
        await using (var stream = OpenResource(BridgeResourceSuffix))
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            bridgeBytes = memory.ToArray();
        }

        var embeddedHash = Convert.ToHexString(SHA256.HashData(bridgeBytes));
        var created = new List<string>();
        var skipped = new List<string>();
        if (File.Exists(target))
        {
            var currentHash = await ComputeFileSha256Async(target, cancellationToken).ConfigureAwait(false);
            if (!PinteModFirstPartyTrust.IsKnownBridgeHash(currentHash))
            {
                return new ServerDeploymentResult(
                    false,
                    "Bridge non remplacé : un fichier ezz_admin_control_center_contracts.gsc inconnu existe déjà. Sauvegardez/auditez-le avant toute mise à jour.",
                    [],
                    []);
            }

            if (currentHash.Equals(embeddedHash, StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(Relative(root, target));
            }
            else
            {
                var backup = target + ".manager-backup-previous";
                if (!File.Exists(backup))
                {
                    File.Copy(target, backup, overwrite: false);
                }

                await WriteAllBytesSafeAsync(target, bridgeBytes, cancellationToken).ConfigureAwait(false);
                created.Add(Relative(root, target));
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await WriteAllBytesSafeAsync(target, bridgeBytes, cancellationToken).ConfigureAwait(false);
            created.Add(Relative(root, target));
        }

        var allowlistPath = Path.Combine(
            root,
            "boiii",
            "scriptdata",
            "pintemod",
            "config",
            "control_center_map_allowlist.json");
        Directory.CreateDirectory(Path.GetDirectoryName(allowlistPath)!);
        if (File.Exists(allowlistPath))
        {
            var backup = allowlistPath + ".manager-backup";
            if (!File.Exists(backup))
            {
                File.Copy(allowlistPath, backup, overwrite: false);
            }
        }

        var json = BuildMapAllowlistJson(maps);
        await WriteAllBytesSafeAsync(allowlistPath, Encoding.UTF8.GetBytes(json), cancellationToken)
            .ConfigureAwait(false);
        created.Add(Relative(root, allowlistPath));

        return new ServerDeploymentResult(
            true,
            $"Control Center Bridge v{BridgeVersion} installé · {maps.Count} carte(s) explicitement autorisée(s) pour Change Map. Redémarrez BOIII pour charger le GSC.",
            created,
            skipped);
    }


    public async Task<ServerDeploymentResult> RepairGeoIpStatisticsAsync(
        string serverRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateServerRoot(serverRoot);
        var geoTarget = Path.Combine(root, "boiii", "tools", "PinteMod_GeoIP_Bridge.ps1");
        if (!File.Exists(geoTarget))
        {
            return new ServerDeploymentResult(
                false,
                "Réparation GeoIP impossible : PinteMod_GeoIP_Bridge.ps1 est absent.",
                [],
                []);
        }

        byte[] patchedGeo;
        await using (var resource = OpenResource(PinteModResourceSuffix))
        using (var archive = new ZipArchive(resource, ZipArchiveMode.Read, leaveOpen: false))
        {
            var entry = archive.GetEntry("boiii/tools/PinteMod_GeoIP_Bridge.ps1")
                        ?? throw new InvalidOperationException("GeoIP Bridge absent du payload embarqué.");
            await using var source = entry.Open();
            using var memory = new MemoryStream();
            await source.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            patchedGeo = memory.ToArray();
        }

        var currentHash = await ComputeFileSha256Async(geoTarget, cancellationToken).ConfigureAwait(false);
        var patchedHash = Convert.ToHexString(SHA256.HashData(patchedGeo));
        var knownGeoHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "F52FD0BB34BE21BE6A71A6F0308368767B0842EF83AF7D06F06674A24F4D39A8",
            "E253653DC1AD1803F0414625C01D1E44EC726611FA1152F01C57B7620429AFFD"
        };
        if (!knownGeoHashes.Contains(currentHash))
        {
            return new ServerDeploymentResult(
                false,
                "Réparation refusée : le GeoIP Bridge présent est une version inconnue. Aucun fichier n’a été modifié.",
                [],
                []);
        }

        var changed = new List<string>();
        if (!currentHash.Equals(patchedHash, StringComparison.OrdinalIgnoreCase))
        {
            var backup = geoTarget + ".manager-backup-before-geoip-stats-fix1";
            if (!File.Exists(backup))
            {
                File.Copy(geoTarget, backup, overwrite: false);
            }
            await WriteAllBytesSafeAsync(geoTarget, patchedGeo, cancellationToken).ConfigureAwait(false);
            changed.Add(Relative(root, geoTarget));
        }

        var statsRoot = Path.Combine(root, "boiii", "scriptdata", "pintemod", "localization", "stats");
        Directory.CreateDirectory(statsRoot);
        var exactFiles = new[]
        {
            Path.Combine(statsRoot, "countries.json"),
            Path.Combine(statsRoot, "countries_summary.txt")
        };
        foreach (var path in exactFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                File.Delete(path);
                changed.Add(Relative(root, path));
            }
        }

        foreach (var pattern in new[] { "countries.json.tmp*", "countries.json.bak*" })
        {
            foreach (var path in Directory.EnumerateFiles(statsRoot, pattern, SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(path);
                changed.Add(Relative(root, path));
            }
        }

        var cleanJson = Encoding.UTF8.GetBytes("{\"schema_version\":1,\"total_connections\":0,\"entries\":[]}");
        await WriteAllBytesSafeAsync(Path.Combine(statsRoot, "countries.json"), cleanJson, cancellationToken)
            .ConfigureAwait(false);
        await WriteAllBytesSafeAsync(Path.Combine(statsRoot, "countries_summary.txt"), [], cancellationToken)
            .ConfigureAwait(false);

        return new ServerDeploymentResult(
            true,
            "Statistiques GeoIP réinitialisées et Bridge durci installé. Ranks, records, langues, bans, profils et secret RCON sont inchangés.",
            changed,
            []);
    }

    public async Task<ServerDeploymentResult> HardenExistingManagerToolingAsync(
        string serverRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateServerRoot(serverRoot);
        var changed = new List<string>();
        var skipped = new List<string>();

        var launcherConfig = Path.Combine(root, "boiii", "tools", "PinteMod_Server_Launcher.local.json");
        if (File.Exists(launcherConfig))
        {
            try
            {
                var raw = await File.ReadAllTextAsync(launcherConfig, cancellationToken).ConfigureAwait(false);
                var node = JsonNode.Parse(raw) as JsonObject;
                if (node is null)
                {
                    return new ServerDeploymentResult(
                        false,
                        "Migration outils refusée : PinteMod_Server_Launcher.local.json est illisible.",
                        changed,
                        skipped);
                }

                var liveNode = node["launch_live_console"];
                var liveEnabled = true;
                if (liveNode is JsonValue liveValue && liveValue.TryGetValue<bool>(out var parsedLive))
                {
                    liveEnabled = parsedLive;
                }

                if (liveEnabled || liveNode is null)
                {
                    var backup = launcherConfig + ".manager-backup-before-no-standalone-consoles";
                    if (!File.Exists(backup))
                    {
                        File.Copy(launcherConfig, backup, overwrite: false);
                    }

                    node["launch_live_console"] = false;
                    var json = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    await WriteAllBytesSafeAsync(
                            launcherConfig,
                            new UTF8Encoding(false).GetBytes(json),
                            cancellationToken)
                        .ConfigureAwait(false);
                    changed.Add(Relative(root, launcherConfig));
                }
                else
                {
                    skipped.Add(Relative(root, launcherConfig));
                }
            }
            catch (JsonException)
            {
                return new ServerDeploymentResult(
                    false,
                    "Migration outils refusée : PinteMod_Server_Launcher.local.json contient un JSON invalide.",
                    changed,
                    skipped);
            }
        }

        var knownHashes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["boiii/tools/PinteMod_Server_Launcher.ps1"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "94387220A9DF50416B8BE44BD35260F86B390C87F79712FE1BB0CFB6D1149A64",
                "374F1CEC032FD3C7DD6F47E5FFA41FEA2BAB3480D894ECAB25095D7F430D9CF2"
            },
            ["boiii/tools/PinteMod_Server_Launcher.example.json"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "6CDBB3269874CA59DD3E0E221FFF2385A1B732AFC35A8D8F02F2211D4F76E9E4",
                "80DA4F94B990556B739F5A9D696C750D168E89FDD4A77D61E58EFD340AC749EF"
            },
            ["Launch_PinteMod_Server.bat"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "2D04113185697EEEF5949EC04A0E91220F680F78AC293EA573EC909730A536CA",
                "B64B1AD999C10165B8A5B9A05DDC83931BF5F9393805F1ABAF1C4CBEBC2B69A4"
            }
        };

        await using var resource = OpenResource(PinteModResourceSuffix);
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var item in knownHashes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = archive.GetEntry(item.Key.Replace('\\', '/'));
            if (entry is null)
            {
                return new ServerDeploymentResult(false, $"Payload incomplet : {item.Key} absent.", changed, skipped);
            }

            var target = ResolveSafeTarget(root, item.Key);
            if (!File.Exists(target))
            {
                skipped.Add(Relative(root, target));
                continue;
            }

            var currentHash = await ComputeFileSha256Async(target, cancellationToken).ConfigureAwait(false);
            await using var source = entry.Open();
            using var memory = new MemoryStream();
            await source.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            var embedded = memory.ToArray();
            var embeddedHash = Convert.ToHexString(SHA256.HashData(embedded));

            if (currentHash.Equals(embeddedHash, StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(Relative(root, target));
                continue;
            }

            if (!item.Value.Contains(currentHash))
            {
                return new ServerDeploymentResult(
                    false,
                    $"Migration outils refusée : « {Relative(root, target)} » est une version inconnue. Aucun script first-party inconnu n'est écrasé.",
                    changed,
                    skipped);
            }

            var backup = target + ".manager-backup-before-no-standalone-consoles";
            if (!File.Exists(backup))
            {
                File.Copy(target, backup, overwrite: false);
            }

            await WriteAllBytesSafeAsync(target, embedded, cancellationToken).ConfigureAwait(false);
            changed.Add(Relative(root, target));
        }

        return new ServerDeploymentResult(
            true,
            "Outils PinteMod existants migrés en mode Control Center géré : Live Console standalone désactivée.",
            changed,
            skipped);
    }

    private static string ValidateServerRoot(string serverRoot)
    {
        if (string.IsNullOrWhiteSpace(serverRoot) || !Directory.Exists(serverRoot))
        {
            throw new DirectoryNotFoundException("Racine serveur absente ou inaccessible.");
        }

        var root = Path.GetFullPath(serverRoot.Trim());
        if (!Directory.Exists(Path.Combine(root, "boiii")))
        {
            throw new DirectoryNotFoundException("Le dossier boiii est introuvable sous la racine sélectionnée.");
        }

        return root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private Stream OpenResource(string suffix)
    {
        var name = _assembly.GetManifestResourceNames()
            .SingleOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return name is null
            ? throw new InvalidOperationException($"Payload embarqué introuvable : {suffix}")
            : _assembly.GetManifestResourceStream(name)
              ?? throw new InvalidOperationException($"Payload embarqué illisible : {suffix}");
    }

    private static string ResolveSafeTarget(string root, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, normalized));
        var prefix = root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Entrée de payload hors racine refusée.");
        }

        return full;
    }

    private static string Relative(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    private static async Task<string> ComputeFileSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await ComputeSha256Async(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task WriteAllBytesSafeAsync(
        string target,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var temp = target + ".pintemod-manager.tmp";
        if (File.Exists(temp))
        {
            File.Delete(temp);
        }

        await using (var stream = new FileStream(
                         temp,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, target, overwrite: true);
    }

    private static IReadOnlyList<string> NormalizeMaps(IReadOnlyCollection<string> maps)
    {
        var official = new HashSet<string>(StringComparer.Ordinal)
        {
            "zm_zod", "zm_castle", "zm_island", "zm_stalingrad", "zm_genesis",
            "zm_cosmodrome", "zm_theater", "zm_moon", "zm_prototype", "zm_tomb",
            "zm_temple", "zm_sumpf", "zm_factory", "zm_asylum"
        };
        return maps
            .Select(map => map?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(official.Contains)
            .Distinct(StringComparer.Ordinal)
            .Take(14)
            .ToArray();
    }

    private static string BuildMapAllowlistJson(IReadOnlyList<string> maps)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"schema_version\": 1,");
        builder.AppendLine("  \"authority\": \"operator_declared\",");
        builder.Append("  \"count\": ").Append(maps.Count);
        if (maps.Count > 0)
        {
            builder.AppendLine(",");
            for (var index = 0; index < maps.Count; index++)
            {
                builder.Append("  \"map_").Append(index + 1).Append("\": \"")
                    .Append(maps[index]).Append('"')
                    .AppendLine(index == maps.Count - 1 ? string.Empty : ",");
            }
        }
        else
        {
            builder.AppendLine();
        }

        builder.AppendLine("}");
        return builder.ToString();
    }
}
