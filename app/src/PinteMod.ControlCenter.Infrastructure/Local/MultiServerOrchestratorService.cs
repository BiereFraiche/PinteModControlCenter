using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed record MultiServerLaunchDefinition(
    string ProfileId,
    string DisplayName,
    string ServerRoot,
    string LauncherRelativePath,
    int ServerPort);

public sealed class MultiServerOrchestratorService
{
    private const string PayloadResourceSuffix = ".Payloads.PinteMod_MULTI_20260819.zip";
    private readonly Assembly _assembly;

    public MultiServerOrchestratorService(Assembly? assembly = null)
    {
        _assembly = assembly ?? typeof(MultiServerOrchestratorService).Assembly;
    }

    public async Task<bool> PrepareRconSecretAsync(
        string profileId,
        string secret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false;
        var root = GetOrchestratorRoot();
        await ExtractPayloadAsync(root, cancellationToken).ConfigureAwait(false);
        var safeProfileId = SafeId(profileId);
        var secretsRoot = Path.Combine(root, "runtime", "secrets");
        Directory.CreateDirectory(secretsRoot);
        var secretPath = Path.Combine(secretsRoot, "group_manager-" + safeProfileId + ".secret.txt");
        if (File.Exists(secretPath) && new FileInfo(secretPath).Length > 0) return true;

        var script = "$s=[Console]::In.ReadToEnd();$sec=ConvertTo-SecureString $s -AsPlainText -Force;$enc=$sec|ConvertFrom-SecureString;[IO.File]::WriteAllText($env:PINTE_CC_SECRET_PATH,$enc,[Text.UTF8Encoding]::new($false))";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var info = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.Environment["PINTE_CC_SECRET_PATH"] = secretPath;
        using var process = Process.Start(info);
        if (process is null) return false;
        await process.StandardInput.WriteAsync(secret).ConfigureAwait(false);
        process.StandardInput.Close();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0 && File.Exists(secretPath) && new FileInfo(secretPath).Length > 0;
    }

    public bool IsRconSecretPrepared(string profileId)
    {
        var safeProfileId = SafeId(profileId);
        var secretPath = Path.Combine(
            GetOrchestratorRoot(),
            "runtime",
            "secrets",
            "group_manager-" + safeProfileId + ".secret.txt");
        return File.Exists(secretPath) && new FileInfo(secretPath).Length > 0;
    }

    public async Task<ServerLaunchResult> LaunchAsync(
        IReadOnlyCollection<MultiServerLaunchDefinition> launchServers,
        IReadOnlyCollection<MultiServerLaunchDefinition> recordHubServers,
        CancellationToken cancellationToken = default)
    {
        if (launchServers.Count == 0)
        {
            return new ServerLaunchResult(false, "Aucun serveur local à lancer.");
        }

        var launch = Normalize(launchServers, requireLauncher: true);
        var hub = Normalize(recordHubServers.Count > 0 ? recordHubServers : launchServers, requireLauncher: false);

        foreach (var server in launch)
        {
            if (!IsRconSecretPrepared(server.ProfileId))
            {
                return new ServerLaunchResult(
                    false,
                    $"Secret Worker non préparé pour {server.DisplayName}. Enregistrez le RCON dans le Control Center puis mettez à jour l’Agent sur le PC serveur.");
            }
        }

        var root = GetOrchestratorRoot();
        await ExtractPayloadAsync(root, cancellationToken).ConfigureAwait(false);

        var launchConfig = Path.Combine(root, "manager-launch.local.json");
        var hubConfig = Path.Combine(root, "manager-recordhub.local.json");
        await WriteConfigAsync(launchConfig, launch, launchServer: true, cancellationToken).ConfigureAwait(false);
        await WriteConfigAsync(hubConfig, hub, launchServer: false, cancellationToken).ConfigureAwait(false);

        var recordHubScript = Path.Combine(root, "PinteMod_RecordHub.ps1");
        var controlScript = Path.Combine(root, "PinteMod_MultiServer_Control.ps1");
        if (!File.Exists(recordHubScript) || !File.Exists(controlScript))
        {
            return new ServerLaunchResult(false, "Payload MultiServer incomplet dans le Manager.");
        }

        // RecordHub must always see every configured local PinteMod root, even when
        // the operator launches only one server. Its mutex prevents duplicates.
        var hubRoot = Path.Combine(root, "PinteModRecordHub");
        Directory.CreateDirectory(hubRoot);
        StartPowerShell(
            recordHubScript,
            $"-ConfigPath {Quote(hubConfig)} -HubRoot {Quote(hubRoot)} -IntervalSeconds 10",
            hidden: true);

        // Worker prerequisites are validated above. Keep the controller hidden:
        // only the BOIII server window should remain visible to the operator.
        var process = StartPowerShell(
            controlScript,
            $"-ConfigPath {Quote(launchConfig)}",
            hidden: true);

        return process is null
            ? new ServerLaunchResult(false, "Windows n’a pas démarré le contrôleur MultiServer.")
            : new ServerLaunchResult(
                true,
                launch.Count == 1
                    ? "Serveur lancé via Worker v3. RecordHub utilise tous les profils locaux configurés."
                    : $"{launch.Count} serveurs lancés via Worker v3. RecordHub utilise tous les profils locaux configurés.",
                process.Id);
    }

    private async Task ExtractPayloadAsync(string root, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        await using var resource = OpenResource(PayloadResourceSuffix);
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var target = ResolveSafeTarget(root, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var source = entry.Open();
            var temp = target + ".manager.tmp";
            await using (var destination = new FileStream(
                             temp,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temp, target, overwrite: true);
        }
    }

    private static IReadOnlyList<MultiServerLaunchDefinition> Normalize(
        IReadOnlyCollection<MultiServerLaunchDefinition> definitions,
        bool requireLauncher)
    {
        var result = new List<MultiServerLaunchDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ports = new HashSet<int>();

        foreach (var item in definitions)
        {
            var id = SafeId(item.ProfileId);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException("Identifiant serveur MultiServer dupliqué.");
            }

            if (string.IsNullOrWhiteSpace(item.ServerRoot) ||
                item.ServerRoot.StartsWith("\\\\", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Le Worker v3 ne peut lancer que des racines locales sur ce PC serveur.");
            }

            var root = Path.GetFullPath(item.ServerRoot.Trim()).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(Path.Combine(root, "boiii")) || !roots.Add(root))
            {
                throw new InvalidOperationException("Racine BOIII locale invalide ou dupliquée.");
            }

            if (item.ServerPort is < 1 or > 65535 || !ports.Add(item.ServerPort))
            {
                throw new InvalidOperationException("Port serveur invalide ou dupliqué.");
            }

            var launcher = ResolveRawLauncher(root, item.LauncherRelativePath, requireLauncher);
            result.Add(item with { ProfileId = id, ServerRoot = root, LauncherRelativePath = launcher });
        }

        return result;
    }

    private static string ResolveRawLauncher(string root, string requested, bool required)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(requested))
        {
            candidates.Add(requested.Trim());
        }
        candidates.Add("Server.bat");
        candidates.Add("server.bat");

        foreach (var relative in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == ".."))
            {
                continue;
            }

            // Do not recursively start PinteMod's mono-server supervisor from
            // the MultiServer Worker. It needs the raw BOIII launcher.
            if (Path.GetFileName(relative).StartsWith("Launch_PinteMod_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = Path.GetFullPath(Path.Combine(root, relative));
            if (path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(path))
            {
                return Path.GetRelativePath(root, path);
            }
        }

        if (required)
        {
            throw new InvalidOperationException("Aucun lanceur BOIII brut trouvé. Sélectionnez Server.bat dans le profil.");
        }

        return "Server.bat";
    }

    private static async Task WriteConfigAsync(
        string path,
        IReadOnlyList<MultiServerLaunchDefinition> servers,
        bool launchServer,
        CancellationToken cancellationToken)
    {
        var palette = new[] { "DarkBlue", "DarkGreen", "DarkMagenta", "DarkCyan", "DarkRed", "DarkGray" };
        var values = servers.Select((server, index) => new Dictionary<string, object?>
        {
            ["id"] = server.ProfileId,
            ["name"] = server.DisplayName,
            ["enabled"] = true,
            ["mode"] = "local",
            ["server_address"] = "127.0.0.1",
            ["server_port"] = server.ServerPort,
            ["server_root"] = server.ServerRoot,
            ["server_launcher"] = server.LauncherRelativePath,
            ["supervisor"] = launchServer,
            ["ban_service"] = launchServer,
            ["geoip"] = launchServer,
            ["live_console"] = false,
            ["rcon"] = false,
            // Per-server group by default: never assume different servers share a secret.
            ["rcon_secret_group"] = "manager-" + server.ProfileId,
            ["console_background"] = palette[index % palette.Length],
            ["console_foreground"] = "White",
            ["show_initial_history"] = false,
            ["enable_critical_sound"] = false,
            ["launch_server"] = launchServer
        }).ToArray();

        var json = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["schema_version"] = 4,
                ["servers"] = values
            },
            new JsonSerializerOptions { WriteIndented = true });

        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }

    private static Process? StartPowerShell(string script, string arguments, bool hidden)
    {
        var info = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -File {Quote(script)} {arguments}",
            WorkingDirectory = Path.GetDirectoryName(script)!,
            UseShellExecute = false,
            CreateNoWindow = hidden,
            WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
        };
        return Process.Start(info);
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

    private static string GetOrchestratorRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PinteMod",
            "ControlCenter",
            "orchestrator");

    private static string ResolveSafeTarget(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Entrée MultiServer hors racine refusée.");
        }
        return full;
    }

    private static string SafeId(string value)
    {
        var safe = new string((value ?? string.Empty)
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.')
            .Take(48)
            .ToArray());
        if (safe.Length == 0) throw new InvalidOperationException("Identifiant serveur invalide.");
        return safe;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
