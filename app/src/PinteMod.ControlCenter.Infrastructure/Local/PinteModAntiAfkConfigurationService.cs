using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class PinteModAntiAfkConfigurationService : IAntiAfkConfigurationService
{
    private const string ConfigRelativePath = "boiii/custom_scripts/ezz_admin_config.gsc";
    private static readonly Regex EnabledRegex = CreateValueRegex("pintemod_afk_enabled", "true|false");
    private static readonly Regex TimeoutRegex = CreateValueRegex("pintemod_afk_timeout_seconds", "\\d{1,5}");
    private static readonly Regex WarningRegex = CreateValueRegex("pintemod_afk_warning_seconds", "\\d{1,5}");

    public async Task<AntiAfkConfigurationLoadResult> LoadAsync(string serverRoot, CancellationToken cancellationToken = default)
    {
        var path = ResolveConfigPath(serverRoot, out var error);
        if (path is null) return new(false, AntiAfkConfiguration.Default, error!);

        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (!TryParse(text, out var configuration))
            {
                return new(false, AntiAfkConfiguration.Default, "Anti-AFK indisponible : les réglages PinteMod attendus sont absents.");
            }

            return new(true, configuration, "Réglages anti-AFK chargés depuis ce serveur.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, AntiAfkConfiguration.Default, "Anti-AFK indisponible : le fichier PinteMod n’est pas accessible.");
        }
    }

    public async Task<AntiAfkConfigurationSaveResult> SaveAsync(
        string serverRoot,
        AntiAfkConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidate(configuration, out var message)) return new(false, message);
        var path = ResolveConfigPath(serverRoot, out var error);
        if (path is null) return new(false, error!);

        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (!TryParse(text, out _))
            {
                return new(false, "Enregistrement refusé : les réglages anti-AFK PinteMod ne sont pas reconnus. Aucun fichier n’est touché.");
            }

            text = EnabledRegex.Replace(text, "level.pintemod_afk_enabled = " + (configuration.Enabled ? "true" : "false") + ";", 1);
            text = TimeoutRegex.Replace(text, "level.pintemod_afk_timeout_seconds = " + configuration.TimeoutSeconds + ";", 1);
            text = WarningRegex.Replace(text, "level.pintemod_afk_warning_seconds = " + configuration.WarningSeconds + ";", 1);
            var temporary = path + ".pintemod-controlcenter.tmp";
            await File.WriteAllTextAsync(temporary, text, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
            return new(true, "Anti-AFK enregistré. Redémarrez le serveur pour appliquer les nouveaux réglages.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, "Enregistrement impossible : arrêtez le serveur puis vérifiez l’accès au dossier.");
        }
    }

    private static bool TryParse(string text, out AntiAfkConfiguration configuration)
    {
        configuration = AntiAfkConfiguration.Default;
        var enabled = EnabledRegex.Match(text);
        var timeout = TimeoutRegex.Match(text);
        var warning = WarningRegex.Match(text);
        if (!enabled.Success || !timeout.Success || !warning.Success ||
            !bool.TryParse(enabled.Groups["value"].Value, out var enabledValue) ||
            !int.TryParse(timeout.Groups["value"].Value, out var timeoutValue) ||
            !int.TryParse(warning.Groups["value"].Value, out var warningValue))
        {
            return false;
        }

        var candidate = new AntiAfkConfiguration(enabledValue, timeoutValue, warningValue);
        if (!TryValidate(candidate, out _)) return false;
        configuration = candidate;
        return true;
    }

    private static bool TryValidate(AntiAfkConfiguration configuration, out string message)
    {
        if (configuration.TimeoutSeconds is < 120 or > 7200 ||
            configuration.WarningSeconds is < 30 or > 3600 ||
            configuration.WarningSeconds >= configuration.TimeoutSeconds - 15)
        {
            message = "Délais invalides : inactivité entre 120 et 7 200 s, avertissement entre 30 s et au moins 15 s avant le passage spectateur.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string? ResolveConfigPath(string serverRoot, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(serverRoot) || !Directory.Exists(serverRoot))
        {
            error = "Anti-AFK indisponible : choisissez d’abord la racine BOIII complète.";
            return null;
        }

        var root = Path.GetFullPath(serverRoot.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.Combine(root, ConfigRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            error = "Anti-AFK indisponible : la configuration PinteMod est introuvable.";
            return null;
        }

        return path;
    }

    private static Regex CreateValueRegex(string variable, string valuePattern) => new(
        @"(?im)^\s*level\." + Regex.Escape(variable) + @"\s*=\s*(?<value>" + valuePattern + @")\s*;\s*$",
        RegexOptions.CultureInvariant);
}
