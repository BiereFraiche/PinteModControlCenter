using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

/// <summary>
/// Edits only the explicitly delimited public-tips block in PinteMod's public
/// configuration. It never scans or returns unrelated configuration content.
/// </summary>
public sealed class PinteModPublicChatTipsConfigurationService : IPublicChatTipsConfigurationService
{
    private const string CommunityRelativePath = "boiii/custom_scripts/ezz_admin_community.gsc";
    private const string ConfigRelativePath = "boiii/custom_scripts/ezz_admin_config.gsc";
    private const string CompatibilityMarker = "PINTEMOD_CONTROL_CENTER_PUBLIC_TIPS_V1";
    private const string BeginMarker = "// BEGIN PINTEMOD CONTROL CENTER PUBLIC TIPS";
    private const string EndMarker = "// END PINTEMOD CONTROL CENTER PUBLIC TIPS";

    // Reviewed versions published before this configurable public-tips module.
    // A familiar filename is not enough: only these exact files may be upgraded.
    private static readonly HashSet<string> LegacyCommunityHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        "643887C78D13570C8149B3C536679B29F36487A4C2A03A7E6658BF866EB3FA84",
        "855525EEE276F18818A4355A81C5D667683895B222291F494E1631271D53181C"
    };

    private static readonly Regex ManagedBlockRegex = new(
        @"(?ms)^\s*// BEGIN PINTEMOD CONTROL CENTER PUBLIC TIPS\s*$.*?^\s*// END PINTEMOD CONTROL CENTER PUBLIC TIPS\s*$(?:\r?\n)?",
        RegexOptions.CultureInvariant);
    private static readonly Regex BooleanRegex = new(
        @"(?im)^\s*level\.pintemod_public_tips_enabled\s*=\s*(?<value>true|false)\s*;\s*$",
        RegexOptions.CultureInvariant);
    private static readonly Regex FirstDelayRegex = CreateDelayRegex("pintemod_public_tips_first_delay");
    private static readonly Regex MinimumDelayRegex = CreateDelayRegex("pintemod_public_tips_min_delay");
    private static readonly Regex MaximumDelayRegex = CreateDelayRegex("pintemod_public_tips_max_delay");
    private static readonly Regex MessageRegex = new(
        "(?im)^\\s*level\\.pintemod_public_tip_messages\\s*\\[\\s*\\d+\\s*\\]\\s*=\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"\\s*;\\s*$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ConfigInsertionAnchorRegex = new(
        @"(?m)^(?<indent>[ \t]*)level\.pintemod_vote_duration\s*=",
        RegexOptions.CultureInvariant);

    private readonly EmbeddedServerPayloadService _payloadService;

    public PinteModPublicChatTipsConfigurationService(EmbeddedServerPayloadService? payloadService = null)
    {
        _payloadService = payloadService ?? new EmbeddedServerPayloadService();
    }

    public async Task<PublicChatTipsLoadResult> LoadAsync(
        string serverRoot,
        CancellationToken cancellationToken = default)
    {
        var root = TryResolveRoot(serverRoot, out var error);
        if (root is null)
        {
            return Unsupported(error!);
        }

        var communityPath = Path.Combine(root, CommunityRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var configPath = Path.Combine(root, ConfigRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(communityPath) || !File.Exists(configPath))
        {
            return Unsupported("Messages automatiques indisponibles : PinteMod n’est pas complet dans ce serveur.");
        }

        try
        {
            var communityHash = await ComputeFileHashAsync(communityPath, cancellationToken).ConfigureAwait(false);
            var expected = await GetCurrentCommunityHashAsync(cancellationToken).ConfigureAwait(false);
            if (!communityHash.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                return LegacyCommunityHashes.Contains(communityHash)
                    ? new PublicChatTipsLoadResult(
                        false,
                        true,
                        PublicChatTipsConfiguration.Default,
                        "Mise à jour PinteMod requise pour modifier les messages. Elle sera faite au premier enregistrement, avec une sauvegarde du module officiel.")
                    : Unsupported("Messages automatiques indisponibles : le module PinteMod présent a été modifié ou n’est pas reconnu. Aucun fichier n’est touché.");
            }

            var currentConfigText = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            var block = ManagedBlockRegex.Match(currentConfigText);
            var configuration = block.Success
                ? ParseManagedBlock(block.Value)
                : PublicChatTipsConfiguration.Default;
            return new PublicChatTipsLoadResult(
                true,
                false,
                configuration,
                block.Success
                    ? "Messages automatiques chargés depuis ce serveur."
                    : "Réglages PinteMod par défaut prêts à être personnalisés.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Unsupported("Messages automatiques indisponibles : les fichiers PinteMod ne sont pas accessibles.");
        }
    }

    public async Task<PublicChatTipsSaveResult> SaveAsync(
        string serverRoot,
        PublicChatTipsConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidate(configuration, out var normalized, out var validationMessage))
        {
            return new PublicChatTipsSaveResult(false, validationMessage);
        }

        var root = TryResolveRoot(serverRoot, out var rootError);
        if (root is null)
        {
            return new PublicChatTipsSaveResult(false, rootError!);
        }

        var communityPath = Path.Combine(root, CommunityRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var configPath = Path.Combine(root, ConfigRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(communityPath) || !File.Exists(configPath))
        {
            return new PublicChatTipsSaveResult(false, "Enregistrement refusé : PinteMod n’est pas complet dans ce serveur.");
        }

        try
        {
            var configText = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            var managedBlock = BuildManagedBlock(normalized);
            var updatedConfig = InsertManagedBlockIntoConfig(configText, managedBlock);
            if (updatedConfig is null)
            {
                return new PublicChatTipsSaveResult(false, "Enregistrement refusé : la structure de configuration PinteMod n’est pas reconnue. Aucun fichier n’est touché.");
            }

            var communityHash = await ComputeFileHashAsync(communityPath, cancellationToken).ConfigureAwait(false);
            var expected = await GetCurrentCommunityHashAsync(cancellationToken).ConfigureAwait(false);
            var upgraded = false;
            if (!communityHash.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                if (!LegacyCommunityHashes.Contains(communityHash))
                {
                    return new PublicChatTipsSaveResult(false, "Enregistrement refusé : le module PinteMod présent n’est pas reconnu. Aucun fichier n’est touché.");
                }

                var backup = communityPath + ".pintemod-controlcenter.public-tips-backup";
                if (!File.Exists(backup))
                {
                    File.Copy(communityPath, backup, overwrite: false);
                }

                var updatedCommunity = await _payloadService
                    .ReadPinteModPayloadFileAsync(CommunityRelativePath, cancellationToken)
                    .ConfigureAwait(false);
                await ReplaceBytesAtomicallyAsync(communityPath, updatedCommunity, cancellationToken).ConfigureAwait(false);
                upgraded = true;
            }

            await ReplaceTextAtomicallyAsync(configPath, updatedConfig, cancellationToken).ConfigureAwait(false);
            return new PublicChatTipsSaveResult(
                true,
                upgraded
                    ? "Messages enregistrés. Le module PinteMod officiel a aussi été mis à jour avec une sauvegarde locale ; redémarrez le serveur."
                    : "Messages automatiques enregistrés. Redémarrez le serveur pour appliquer les nouveaux réglages.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new PublicChatTipsSaveResult(false, "Enregistrement impossible : vérifiez que le serveur est arrêté et que le dossier est accessible.");
        }
    }

    private async Task<string> GetCurrentCommunityHashAsync(CancellationToken cancellationToken)
    {
        var bytes = await _payloadService
            .ReadPinteModPayloadFileAsync(CommunityRelativePath, cancellationToken)
            .ConfigureAwait(false);
        if (!Encoding.UTF8.GetString(bytes).Contains(CompatibilityMarker, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Le payload PinteMod ne contient pas le module de messages attendu.");
        }

        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static PublicChatTipsConfiguration ParseManagedBlock(string block)
    {
        var defaults = PublicChatTipsConfiguration.Default;
        var enabled = BooleanRegex.Match(block) is { Success: true } booleanMatch &&
                      bool.TryParse(booleanMatch.Groups["value"].Value, out var parsedEnabled)
            ? parsedEnabled
            : defaults.Enabled;
        var messages = MessageRegex.Matches(block)
            .Select(match => DecodeGscString(match.Groups["value"].Value))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();
        return new PublicChatTipsConfiguration(
            enabled,
            ReadDelay(FirstDelayRegex, block, defaults.FirstDelaySeconds),
            ReadDelay(MinimumDelayRegex, block, defaults.MinimumDelaySeconds),
            ReadDelay(MaximumDelayRegex, block, defaults.MaximumDelaySeconds),
            messages.Length == 0 ? defaults.Messages : messages);
    }

    private static int ReadDelay(Regex regex, string input, int fallback) =>
        regex.Match(input) is { Success: true } match && int.TryParse(match.Groups["value"].Value, out var parsed)
            ? parsed
            : fallback;

    private static bool TryValidate(
        PublicChatTipsConfiguration configuration,
        out PublicChatTipsConfiguration normalized,
        out string message)
    {
        var messages = (configuration.Messages ?? [])
            .Select(item => (item ?? string.Empty).Trim())
            .ToArray();
        if (configuration.FirstDelaySeconds is < 60 or > 3600 ||
            configuration.MinimumDelaySeconds is < 60 or > 86400 ||
            configuration.MaximumDelaySeconds is < 60 or > 86400 ||
            configuration.MaximumDelaySeconds < configuration.MinimumDelaySeconds)
        {
            normalized = PublicChatTipsConfiguration.Default;
            message = "Délais invalides : premier message entre 60 et 3 600 secondes, récurrence entre 60 et 86 400 secondes.";
            return false;
        }

        if (messages.Length is < 1 or > 12 || messages.Any(item => item.Length is < 3 or > 180 || item.Any(char.IsControl)))
        {
            normalized = PublicChatTipsConfiguration.Default;
            message = "Ajoutez entre 1 et 12 messages de 3 à 180 caractères, sans retour à la ligne.";
            return false;
        }

        normalized = configuration with { Messages = messages };
        message = string.Empty;
        return true;
    }

    private static string BuildManagedBlock(PublicChatTipsConfiguration configuration)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BeginMarker);
        builder.AppendLine("// Réglages gérés par PinteMod Control Center. Les messages restent visibles dans ce fichier serveur.");
        builder.AppendLine("level.pintemod_public_tips_enabled = " + (configuration.Enabled ? "true" : "false") + ";");
        builder.AppendLine("level.pintemod_public_tips_first_delay = " + configuration.FirstDelaySeconds + ";");
        builder.AppendLine("level.pintemod_public_tips_min_delay = " + configuration.MinimumDelaySeconds + ";");
        builder.AppendLine("level.pintemod_public_tips_max_delay = " + configuration.MaximumDelaySeconds + ";");
        builder.AppendLine("level.pintemod_public_tip_messages = [];");
        for (var index = 0; index < configuration.Messages.Count; index++)
        {
            builder.AppendLine("level.pintemod_public_tip_messages[" + index + "] = \"" + EncodeGscString(configuration.Messages[index]) + "\";");
        }

        builder.Append(EndMarker);
        return builder.ToString();
    }

    private static string? InsertManagedBlockIntoConfig(string configText, string managedBlock)
    {
        if (ManagedBlockRegex.IsMatch(configText))
        {
            return ManagedBlockRegex.Replace(configText, managedBlock + Environment.NewLine);
        }

        var anchor = ConfigInsertionAnchorRegex.Match(configText);
        if (!anchor.Success)
        {
            return null;
        }

        var indent = anchor.Groups["indent"].Value;
        var indentedBlock = string.Join(
            Environment.NewLine,
            managedBlock.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => line.Length == 0 ? line : indent + line));
        return configText.Insert(anchor.Index, indentedBlock + Environment.NewLine);
    }

    private static string EncodeGscString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string DecodeGscString(string value) => value.Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static Regex CreateDelayRegex(string variable) => new(
        @"(?im)^\s*level\." + Regex.Escape(variable) + @"\s*=\s*(?<value>\d{1,5})\s*;\s*$",
        RegexOptions.CultureInvariant);

    private static string? TryResolveRoot(string serverRoot, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(serverRoot) || !Directory.Exists(serverRoot))
        {
            error = "Messages automatiques indisponibles : choisissez d’abord la racine BOIII complète.";
            return null;
        }

        var root = Path.GetFullPath(serverRoot.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(Path.Combine(root, "boiii")))
        {
            error = "Messages automatiques indisponibles : le dossier boiii est introuvable.";
            return null;
        }

        return root;
    }

    private static PublicChatTipsLoadResult Unsupported(string message) => new(false, false, PublicChatTipsConfiguration.Default, message);

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static async Task ReplaceTextAtomicallyAsync(string path, string text, CancellationToken cancellationToken) =>
        await ReplaceBytesAtomicallyAsync(path, new UTF8Encoding(false).GetBytes(text), cancellationToken).ConfigureAwait(false);

    private static async Task ReplaceBytesAtomicallyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var temporary = path + ".pintemod-controlcenter.tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path, overwrite: true);
    }
}
