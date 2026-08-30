using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

// First-run setup only. The service deliberately refuses to replace an
// existing RCON setting and does not attempt to discover configuration files.
public sealed class BoiiiRconBootstrapService : IBoiiiRconBootstrapService
{
    private static readonly Regex ServerFilenameRegex = new(
        @"^\s*set\s+""?serverfilename\s*=\s*""?(?<file>[A-Za-z0-9_.-]+\.cfg)""?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex ExistingRconRegex = new(
        @"^\s*set\s+""?rcon_password\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex SecretRegex = new(
        @"\A[A-Za-z0-9!@#$%^&*+=._-]{8,128}\z",
        RegexOptions.CultureInvariant);

    public async Task<BoiiiRconBootstrapResult> InitializeAsync(
        string serverRoot,
        string secret,
        CancellationToken cancellationToken = default)
    {
        if (!SecretRegex.IsMatch(secret ?? string.Empty))
        {
            return new BoiiiRconBootstrapResult(false, "Secret refusé : 8 à 128 caractères, sans espace ni guillemet.");
        }

        var root = serverRoot?.Trim() ?? string.Empty;
        if (root.Length == 0 || !Directory.Exists(root) || !Directory.Exists(Path.Combine(root, "boiii")))
        {
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON refusée : choisissez d’abord la racine BOIII complète.");
        }

        var launcher = Path.Combine(root, "Server.bat");
        if (!File.Exists(launcher))
        {
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON refusée : Server.bat est introuvable à la racine serveur.");
        }

        string launcherText;
        try
        {
            launcherText = await File.ReadAllTextAsync(launcher, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON impossible : Server.bat n’est pas lisible.");
        }

        var configMatch = ServerFilenameRegex.Match(launcherText);
        if (!configMatch.Success)
        {
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON refusée : Server.bat ne déclare pas un fichier server .cfg explicite.");
        }

        var configName = configMatch.Groups["file"].Value;
        var configCandidates = new[]
        {
            Path.Combine(root, "zone", configName),
            Path.Combine(root, configName)
        };
        var configPath = configCandidates.FirstOrDefault(File.Exists);
        if (configPath is null)
        {
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON refusée : le fichier serveur déclaré par Server.bat est introuvable.");
        }

        string configText;
        try
        {
            configText = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON impossible : le fichier serveur n’est pas accessible en écriture.");
        }

        if (ExistingRconRegex.IsMatch(configText))
        {
            return new BoiiiRconBootstrapResult(false, "Un mot de passe RCON existe déjà dans ce serveur : il n’a pas été remplacé.");
        }

        var updated = configText.TrimEnd() + "\r\n\r\n// PinteMod Control Center : premier secret RCON, créé sur confirmation.\r\n" +
                      "set rcon_password \"" + secret + "\"\r\n";
        var temporary = configPath + ".pintemod-controlcenter.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, updated, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, configPath, overwrite: true);
            return new BoiiiRconBootstrapResult(true, "Premier secret RCON enregistré dans le fichier BOIII déclaré par Server.bat. Redémarrez le serveur pour l’utiliser.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON impossible : aucune modification fiable n’a été finalisée.");
        }
    }
}
