using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

// First-run setup only. It never replaces a configured RCON secret. When
// PinteMod is present, it mirrors its supported local setup: a private cfg
// loaded last and the same secret protected with the current user's DPAPI.
public sealed class BoiiiRconBootstrapService : IBoiiiRconBootstrapService
{
    private readonly Func<string, string, CancellationToken, Task<bool>> _writeDpapiSecretAsync;
    private static readonly Regex ServerFilenameRegex = new(
        @"^\s*set\s+""?serverfilename\s*=\s*""?(?<file>[A-Za-z0-9_.-]+\.cfg)""?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex ExistingRconRegex = new(
        @"^\s*(?:set\s+)?""?rcon_password\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex ExistingRconLineRegex = new(
        @"^\s*(?:set\s+)?""?rcon_password""?(?:\s+.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex ExistingPinteModBlockRegex = new(
        @"(?ms)^\s*// BEGIN PINTEMOD LOCAL SECRETS\s*$.*?^\s*// END PINTEMOD LOCAL SECRETS\s*$(?:\r?\n)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex GamePortRegex = new(
        @"(?im)\bGamePort\s*=\s*""?(?<port>\d{2,5})""?",
        RegexOptions.CultureInvariant);
    private static readonly Regex NetPortRegex = new(
        @"(?im)(?:\+set\s+|\+)?net_port\s+""?(?<port>\d{2,5})""?",
        RegexOptions.CultureInvariant);
    private static readonly Regex NetPortDirectiveRegex = new(
        @"^\s*(?:set\s+)?net_port\s+""?\d{1,5}""?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex SecretRegex = new(
        @"\A[A-Za-z0-9!@#$%^&*+=._-]{8,128}\z",
        RegexOptions.CultureInvariant);

    public BoiiiRconBootstrapService(
        Func<string, string, CancellationToken, Task<bool>>? writeDpapiSecretAsync = null)
    {
        _writeDpapiSecretAsync = writeDpapiSecretAsync ?? WriteDpapiSecretAsync;
    }

    public async Task<bool> HasConfiguredRconAsync(string serverRoot, CancellationToken cancellationToken = default)
    {
        var root = serverRoot?.Trim() ?? string.Empty;
        if (root.Length == 0 || !Directory.Exists(root) || !Directory.Exists(Path.Combine(root, "boiii")))
        {
            return false;
        }

        try
        {
            var launcher = Path.Combine(root, "Server.bat");
            if (!File.Exists(launcher)) return false;
            var launcherText = await File.ReadAllTextAsync(launcher, cancellationToken).ConfigureAwait(false);
            var configMatch = ServerFilenameRegex.Match(launcherText);
            if (!configMatch.Success) return false;

            var configPath = new[]
            {
                Path.Combine(root, "zone", configMatch.Groups["file"].Value),
                Path.Combine(root, configMatch.Groups["file"].Value)
            }.FirstOrDefault(File.Exists);
            if (configPath is null) return false;

            // Deliberately inspect only the presence of the directive. The
            // secret itself is never parsed, returned, logged, or displayed.
            var configText = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            return ExistingRconRegex.IsMatch(configText) || ExistingPinteModBlockRegex.IsMatch(configText);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    public async Task<BoiiiRconBootstrapResult> UpdateServerPortAsync(
        string serverRoot,
        int port,
        CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535)
        {
            return new BoiiiRconBootstrapResult(false, "Port refusé : entrez une valeur comprise entre 1 et 65535.");
        }

        var root = serverRoot?.Trim() ?? string.Empty;
        if (root.Length == 0 || !Directory.Exists(root) || !Directory.Exists(Path.Combine(root, "boiii")))
        {
            return new BoiiiRconBootstrapResult(false, "Modification du port refusée : choisissez d’abord la racine BOIII complète.");
        }

        try
        {
            var launcherPath = Path.Combine(root, "Server.bat");
            if (!File.Exists(launcherPath))
            {
                return new BoiiiRconBootstrapResult(false, "Modification du port refusée : Server.bat est introuvable à la racine serveur.");
            }

            var launcherText = await File.ReadAllTextAsync(launcherPath, cancellationToken).ConfigureAwait(false);
            var configMatch = ServerFilenameRegex.Match(launcherText);
            if (!configMatch.Success)
            {
                return new BoiiiRconBootstrapResult(false, "Modification du port refusée : Server.bat ne déclare pas un fichier server .cfg explicite.");
            }

            var configPath = new[]
            {
                Path.Combine(root, "zone", configMatch.Groups["file"].Value),
                Path.Combine(root, configMatch.Groups["file"].Value)
            }.FirstOrDefault(File.Exists);
            if (configPath is null)
            {
                return new BoiiiRconBootstrapResult(false, "Modification du port refusée : le fichier serveur déclaré par Server.bat est introuvable.");
            }

            var configText = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            var directive = "set net_port \"" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"";
            var updated = NetPortDirectiveRegex.IsMatch(configText)
                ? NetPortDirectiveRegex.Replace(configText, directive)
                : configText.TrimEnd() + "\r\n\r\n// PinteMod Control Center : port BOIII configuré localement.\r\n" + directive + "\r\n";

            await ReplaceTextAtomicallyAsync(configPath, updated, cancellationToken).ConfigureAwait(false);
            return new BoiiiRconBootstrapResult(
                true,
                "Port BOIII mis à jour dans " + Path.GetFileName(configPath) + ". Redémarrez le serveur pour l’utiliser.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new BoiiiRconBootstrapResult(false, "Modification du port impossible : aucune modification fiable n’a été finalisée.");
        }
    }

    public async Task<BoiiiRconBootstrapResult> InitializeAsync(
        string serverRoot,
        string secret,
        CancellationToken cancellationToken = default)
    {
        var suppliedSecret = secret ?? string.Empty;
        if (!SecretRegex.IsMatch(suppliedSecret))
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

        var configCandidates = new[]
        {
            Path.Combine(root, "zone", configMatch.Groups["file"].Value),
            Path.Combine(root, configMatch.Groups["file"].Value)
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
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON impossible : le fichier serveur n’est pas accessible.");
        }

        if (ExistingRconRegex.IsMatch(configText) || ExistingPinteModBlockRegex.IsMatch(configText))
        {
            return new BoiiiRconBootstrapResult(false, "Un mot de passe RCON existe déjà dans ce serveur : il n’a pas été remplacé.");
        }

        var isPinteModServer = File.Exists(Path.Combine(root, "boiii", "tools", "PinteMod_Server_Launcher.ps1"));
        return isPinteModServer
            ? await InitializePinteModAsync(root, configPath, configText, launcherText, suppliedSecret, _writeDpapiSecretAsync, cancellationToken).ConfigureAwait(false)
            : await InitializeBoiiiOnlyAsync(configPath, configText, suppliedSecret, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BoiiiRconBootstrapResult> ReplaceAsync(
        string serverRoot,
        string secret,
        CancellationToken cancellationToken = default)
    {
        var suppliedSecret = secret ?? string.Empty;
        if (!SecretRegex.IsMatch(suppliedSecret))
        {
            return new BoiiiRconBootstrapResult(false, "Secret refusé : 8 à 128 caractères, sans espace ni guillemet.");
        }

        var root = serverRoot?.Trim() ?? string.Empty;
        if (root.Length == 0 || !Directory.Exists(root) || !Directory.Exists(Path.Combine(root, "boiii")))
        {
            return new BoiiiRconBootstrapResult(false, "Remplacement RCON refusé : choisissez d’abord la racine BOIII complète.");
        }

        try
        {
            var launcherPath = Path.Combine(root, "Server.bat");
            if (!File.Exists(launcherPath))
            {
                return new BoiiiRconBootstrapResult(false, "Remplacement RCON refusé : Server.bat est introuvable à la racine serveur.");
            }

            var launcherText = await File.ReadAllTextAsync(launcherPath, cancellationToken).ConfigureAwait(false);
            var configMatch = ServerFilenameRegex.Match(launcherText);
            if (!configMatch.Success)
            {
                return new BoiiiRconBootstrapResult(false, "Remplacement RCON refusé : Server.bat ne déclare pas un fichier server .cfg explicite.");
            }

            var configPath = new[]
            {
                Path.Combine(root, "zone", configMatch.Groups["file"].Value),
                Path.Combine(root, configMatch.Groups["file"].Value)
            }.FirstOrDefault(File.Exists);
            if (configPath is null)
            {
                return new BoiiiRconBootstrapResult(false, "Remplacement RCON refusé : le fichier serveur déclaré par Server.bat est introuvable.");
            }

            var configText = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            if (ExistingPinteModBlockRegex.IsMatch(configText))
            {
                return await ReplacePinteModSecretAsync(root, configPath, suppliedSecret, cancellationToken).ConfigureAwait(false);
            }

            if (!ExistingRconLineRegex.IsMatch(configText))
            {
                return new BoiiiRconBootstrapResult(false, "Remplacement RCON refusé : aucun RCON BOIII explicite n’a été trouvé.");
            }

            await ReplaceTextAtomicallyAsync(
                configPath,
                ReplaceRconDirective(configText, suppliedSecret),
                cancellationToken).ConfigureAwait(false);
            return new BoiiiRconBootstrapResult(true, "RCON BOIII remplacé. L’ancienne valeur reste masquée ; redémarrez le serveur pour utiliser la nouvelle.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new BoiiiRconBootstrapResult(false, "Remplacement RCON impossible : aucune modification fiable n’a été finalisée.");
        }
    }

    private async Task<BoiiiRconBootstrapResult> ReplacePinteModSecretAsync(
        string root,
        string serverConfigPath,
        string secret,
        CancellationToken cancellationToken)
    {
        var zoneRoot = Path.GetDirectoryName(serverConfigPath)!;
        var localSecretsPath = Path.Combine(zoneRoot, "pintemod_server_secrets.cfg");
        var bridgeSecretPath = Path.Combine(root, "boiii", "tools", "PinteMod_GeoIP_Bridge.secret.txt");
        var localSecretsExisted = File.Exists(localSecretsPath);
        var bridgeSecretExisted = File.Exists(bridgeSecretPath);
        var localSecrets = localSecretsExisted
            ? await File.ReadAllTextAsync(localSecretsPath, cancellationToken).ConfigureAwait(false)
            : "// PinteMod local server secrets - repaired locally\r\n" +
              "// Keep this file private and never upload it to GitHub.\r\n" +
              "set rcon_password \"" + secret + "\"\r\n" +
              "set g_password \"\"\r\n";
        if (localSecretsExisted && !ExistingRconLineRegex.IsMatch(localSecrets))
        {
            return new BoiiiRconBootstrapResult(false, "Remplacement RCON refusé : le fichier PinteMod ne contient pas de directive RCON reconnue.");
        }

        var localTemporary = localSecretsPath + ".pintemod-controlcenter.tmp";
        var bridgeTemporary = bridgeSecretPath + ".pintemod-controlcenter.tmp";
        var bridgeBackup = bridgeSecretPath + ".pintemod-controlcenter.replace-backup";
        var bridgeApplied = false;
        try
        {
            await File.WriteAllTextAsync(
                localTemporary,
                localSecretsExisted ? ReplaceRconDirective(localSecrets, secret) : localSecrets,
                new UTF8Encoding(false),
                cancellationToken).ConfigureAwait(false);
            if (!await _writeDpapiSecretAsync(bridgeTemporary, secret, cancellationToken).ConfigureAwait(false))
            {
                return new BoiiiRconBootstrapResult(false, "Remplacement RCON impossible : Windows n’a pas pu protéger le nouveau secret GeoIP PinteMod.");
            }

            if (bridgeSecretExisted)
            {
                File.Move(bridgeSecretPath, bridgeBackup, overwrite: true);
            }
            File.Move(bridgeTemporary, bridgeSecretPath, overwrite: false);
            bridgeApplied = true;
            File.Move(localTemporary, localSecretsPath, overwrite: true);
            TryDelete(bridgeBackup);
            return new BoiiiRconBootstrapResult(
                true,
                localSecretsExisted && bridgeSecretExisted
                    ? "RCON PinteMod remplacé et bridge GeoIP synchronisé. L’ancienne valeur reste masquée ; redémarrez le serveur."
                    : "RCON PinteMod créé dans les fichiers secrets manquants et bridge GeoIP synchronisé. Redémarrez le serveur.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try
            {
                if (File.Exists(bridgeBackup)) File.Move(bridgeBackup, bridgeSecretPath, overwrite: true);
                else if (!bridgeSecretExisted && bridgeApplied) TryDelete(bridgeSecretPath);
            }
            catch (Exception rollbackException) when (rollbackException is IOException or UnauthorizedAccessException)
            {
                return new BoiiiRconBootstrapResult(false, "Remplacement RCON interrompu : vérifiez les permissions des fichiers PinteMod avant de recommencer.");
            }

            return new BoiiiRconBootstrapResult(false, "Remplacement RCON impossible : aucune modification fiable n’a été finalisée.");
        }
        finally
        {
            TryDelete(localTemporary);
            TryDelete(bridgeTemporary);
            TryDelete(bridgeBackup);
        }
    }

    private static string ReplaceRconDirective(string source, string secret) =>
        ExistingRconLineRegex.Replace(source, "set rcon_password \"" + secret + "\"");

    private static async Task ReplaceTextAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporary = path + ".pintemod-controlcenter.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static async Task<BoiiiRconBootstrapResult> InitializeBoiiiOnlyAsync(
        string configPath, string configText, string secret, CancellationToken cancellationToken)
    {
        var updated = configText.TrimEnd() + "\r\n\r\n// PinteMod Control Center : premier secret RCON, créé sur confirmation.\r\n" +
                      "set rcon_password \"" + secret + "\"\r\n";
        var temporary = configPath + ".pintemod-controlcenter.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, updated, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, configPath, overwrite: true);
            return new BoiiiRconBootstrapResult(true, "Premier secret RCON enregistré. Redémarrez le serveur pour l’utiliser.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON impossible : aucune modification fiable n’a été finalisée.");
        }
    }

    private static async Task<BoiiiRconBootstrapResult> InitializePinteModAsync(
        string root, string serverConfigPath, string serverConfigText, string launcherText, string secret,
        Func<string, string, CancellationToken, Task<bool>> writeDpapiSecretAsync, CancellationToken cancellationToken)
    {
        var zoneRoot = Path.GetDirectoryName(serverConfigPath)!;
        var toolsRoot = Path.Combine(root, "boiii", "tools");
        var localSecretsPath = Path.Combine(zoneRoot, "pintemod_server_secrets.cfg");
        var bridgeSecretPath = Path.Combine(toolsRoot, "PinteMod_GeoIP_Bridge.secret.txt");
        var bridgeConfigPath = Path.Combine(toolsRoot, "PinteMod_GeoIP_Bridge.local.json");
        var bridgeExamplePath = Path.Combine(toolsRoot, "PinteMod_GeoIP_Bridge.example.json");

        if (File.Exists(localSecretsPath) || File.Exists(bridgeSecretPath))
        {
            return new BoiiiRconBootstrapResult(false, "Des secrets PinteMod existent déjà : ils n’ont pas été remplacés.");
        }

        var bridgeConfigExisted = File.Exists(bridgeConfigPath);
        string bridgeSource;
        try
        {
            bridgeSource = bridgeConfigExisted
                ? await File.ReadAllTextAsync(bridgeConfigPath, cancellationToken).ConfigureAwait(false)
                : await File.ReadAllTextAsync(bridgeExamplePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON refusée : la configuration GeoIP PinteMod est introuvable ou inaccessible.");
        }

        JsonObject bridgeConfig;
        try
        {
            bridgeConfig = JsonNode.Parse(bridgeSource)?.AsObject() ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return new BoiiiRconBootstrapResult(false, "Initialisation RCON refusée : la configuration GeoIP PinteMod n’est pas un JSON valide.");
        }

        bridgeConfig["server_address"] = "127.0.0.1";
        bridgeConfig["server_port"] = ResolvePort(launcherText);

        var serverConfigTemporary = serverConfigPath + ".pintemod-controlcenter.tmp";
        var localSecretsTemporary = localSecretsPath + ".pintemod-controlcenter.tmp";
        var bridgeSecretTemporary = bridgeSecretPath + ".pintemod-controlcenter.tmp";
        var bridgeConfigTemporary = bridgeConfigPath + ".pintemod-controlcenter.tmp";
        var localSecretsCreated = false;
        var bridgeSecretCreated = false;
        var bridgeConfigWritten = false;
        try
        {
            var managedBlock = "// BEGIN PINTEMOD LOCAL SECRETS\r\n" +
                               "// Generated locally. Do not publish this file or its contents.\r\n" +
                               "// Loaded LAST so other CFG files cannot overwrite these values afterwards.\r\n" +
                               "exec \"pintemod_server_secrets.cfg\"\r\n" +
                               "// END PINTEMOD LOCAL SECRETS\r\n";
            var updatedServerConfig = serverConfigText.TrimEnd() + "\r\n\r\n" + managedBlock;
            var localSecrets = "// PinteMod local server secrets - generated locally\r\n" +
                               "// Keep this file private and never upload it to GitHub.\r\n" +
                               "set rcon_password \"" + secret + "\"\r\n" +
                               "set g_password \"\"\r\n";
            var bridgeJson = bridgeConfig.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\r\n";

            await File.WriteAllTextAsync(serverConfigTemporary, updatedServerConfig, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(localSecretsTemporary, localSecrets, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(bridgeConfigTemporary, bridgeJson, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            if (!await writeDpapiSecretAsync(bridgeSecretTemporary, secret, cancellationToken).ConfigureAwait(false))
            {
                return new BoiiiRconBootstrapResult(false, "Initialisation RCON impossible : Windows n’a pas pu protéger le secret GeoIP PinteMod.");
            }

            File.Move(localSecretsTemporary, localSecretsPath, overwrite: false);
            localSecretsCreated = true;
            File.Move(bridgeSecretTemporary, bridgeSecretPath, overwrite: false);
            bridgeSecretCreated = true;
            File.Move(bridgeConfigTemporary, bridgeConfigPath, overwrite: true);
            bridgeConfigWritten = true;
            File.Move(serverConfigTemporary, serverConfigPath, overwrite: true);
            return new BoiiiRconBootstrapResult(true, "Premier secret RCON PinteMod enregistré localement et synchronisé au bridge GeoIP. Redémarrez le serveur pour l’utiliser.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (localSecretsCreated) TryDelete(localSecretsPath);
            if (bridgeSecretCreated) TryDelete(bridgeSecretPath);
            if (bridgeConfigWritten)
            {
                try
                {
                    if (bridgeConfigExisted)
                    {
                        File.WriteAllText(bridgeConfigPath, bridgeSource, new UTF8Encoding(false));
                    }
                    else
                    {
                        TryDelete(bridgeConfigPath);
                    }
                }
                catch (Exception rollbackException) when (rollbackException is IOException or UnauthorizedAccessException)
                {
                    return new BoiiiRconBootstrapResult(false, "Initialisation RCON interrompue : vérifiez les permissions du dossier PinteMod avant de recommencer.");
                }
            }

            return new BoiiiRconBootstrapResult(false, "Initialisation RCON impossible : les fichiers temporaires ont été retirés, aucun secret n’a été remplacé.");
        }
        finally
        {
            TryDelete(serverConfigTemporary);
            TryDelete(localSecretsTemporary);
            TryDelete(bridgeSecretTemporary);
            TryDelete(bridgeConfigTemporary);
        }
    }

    private static int ResolvePort(string launcherText)
    {
        foreach (var regex in new[] { GamePortRegex, NetPortRegex })
        {
            var match = regex.Match(launcherText);
            if (match.Success && int.TryParse(match.Groups["port"].Value, out var port) && port is >= 1 and <= 65535)
            {
                return port;
            }
        }

        return 27017;
    }

    private static async Task<bool> WriteDpapiSecretAsync(string destination, string secret, CancellationToken cancellationToken)
    {
        const string command = "$value=[Console]::In.ReadToEnd();if([string]::IsNullOrWhiteSpace($value)){exit 2};$secure=ConvertTo-SecureString -String $value -AsPlainText -Force;$protected=$secure|ConvertFrom-SecureString;[IO.File]::WriteAllText($env:PINTE_CC_SECRET_PATH,$protected,[Text.UTF8Encoding]::new($false))";
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true
            }
        };
        process.StartInfo.ArgumentList.Add("-NoLogo");
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-EncodedCommand");
        process.StartInfo.ArgumentList.Add(encodedCommand);
        process.StartInfo.Environment["PINTE_CC_SECRET_PATH"] = destination;

        try
        {
            if (!process.Start()) return false;
            await process.StandardInput.WriteAsync(secret.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0 && File.Exists(destination) && new FileInfo(destination).Length > 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
    }
}
