using System.Diagnostics;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class LocalServerLaunchService
{
    public Task<ServerLaunchResult> LaunchAsync(
        string serverRoot,
        string launcherRelativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(serverRoot) || serverRoot.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return Task.FromResult(new ServerLaunchResult(
                false,
                "Lancement distant refusé : exécutez le même PinteMod Manager directement sur le PC serveur. Un fichier UNC lancé ici s’exécuterait sur ce PC, pas sur le serveur."));
        }

        if (!Directory.Exists(serverRoot))
        {
            return Task.FromResult(new ServerLaunchResult(false, "Racine serveur locale introuvable."));
        }

        var root = Path.GetFullPath(serverRoot.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        var relative = launcherRelativePath?.Trim() ?? string.Empty;
        if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == ".."))
        {
            return Task.FromResult(new ServerLaunchResult(false, "Sélectionnez un lanceur local valide sous la racine serveur."));
        }

        var launcher = Path.GetFullPath(Path.Combine(root, relative));
        if (!launcher.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(launcher))
        {
            return Task.FromResult(new ServerLaunchResult(false, "Lanceur introuvable ou hors racine serveur."));
        }

        var extension = Path.GetExtension(launcher);
        ProcessStartInfo info;
        if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            info = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
                Arguments = $"/d /s /c call \"{launcher}\"",
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = false
            };
        }
        else if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            info = new ProcessStartInfo
            {
                FileName = launcher,
                WorkingDirectory = root,
                UseShellExecute = true
            };
        }
        else
        {
            return Task.FromResult(new ServerLaunchResult(false, "Type de lanceur refusé. Utilisez .bat, .cmd ou .exe."));
        }

        var process = Process.Start(info);
        return Task.FromResult(process is null
            ? new ServerLaunchResult(false, "Windows n’a pas démarré le lanceur.")
            : new ServerLaunchResult(true, "Lanceur démarré. Le Control Center va s’ouvrir sur ce profil.", process.Id));
    }
}
