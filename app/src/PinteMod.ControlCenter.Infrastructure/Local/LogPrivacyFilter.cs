using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public static partial class LogPrivacyFilter
{
    public static string SanitizeDisplayText(string? value, int maximumLength = 240)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = ControlCharacters().Replace(value, " ");
        sanitized = UncPath().Replace(sanitized, "[chemin masqué]");
        sanitized = WindowsPath().Replace(sanitized, "[chemin masqué]");
        sanitized = UnixPath().Replace(sanitized, "[chemin masqué]");
        sanitized = GuidValue().Replace(sanitized, "[identifiant masqué]");
        sanitized = Ipv6Address().Replace(sanitized, "[adresse masquée]");
        sanitized = IpAddress().Replace(sanitized, "[adresse masquée]");
        sanitized = CompleteXuid().Replace(sanitized, match => XuidValidator.Abbreviate(match.Value));
        sanitized = PathKeyValue().Replace(sanitized, match => $"{match.Groups[1].Value}=[chemin masqué]");
        sanitized = AddressKeyValue().Replace(sanitized, match => $"{match.Groups[1].Value}=[adresse masquée]");
        sanitized = IdentifierKeyValue().Replace(sanitized, match => $"{match.Groups[1].Value}=[identifiant masqué]");
        sanitized = SensitiveKeyValue().Replace(sanitized, match => $"{match.Groups[1].Value}=[masqué]");
        sanitized = Whitespace().Replace(sanitized, " ").Trim();

        if (sanitized.Length > maximumLength)
        {
            sanitized = sanitized[..Math.Max(0, maximumLength - 1)] + "…";
        }

        return sanitized;
    }

    public static string SanitizeChatText(string? value, int maximumLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutCompleteXuids = CompleteXuid().Replace(value, "[identifiant masqué]");
        withoutCompleteXuids = AbbreviatedXuid().Replace(withoutCompleteXuids, "[identifiant masqué]");
        withoutCompleteXuids = ChatSensitiveKeyValue().Replace(
            withoutCompleteXuids,
            match => $"{match.Groups[1].Value}=[masqué]");
        return SanitizeDisplayText(withoutCompleteXuids, maximumLength);
    }

    public static string SafeChatPlayerName(string? value)
    {
        var safe = SanitizeChatText(value, 48);
        return string.IsNullOrWhiteSpace(safe) ? "Joueur" : safe;
    }

    public static string SafePlayerName(string? value)
    {
        var safe = SanitizeDisplayText(value, 48);
        return string.IsNullOrWhiteSpace(safe) ? "Joueur local" : safe;
    }

    [GeneratedRegex(@"[\x00-\x1F\x7F]", RegexOptions.CultureInvariant)]
    private static partial Regex ControlCharacters();

    [GeneratedRegex(@"(?i)\b[a-z]:\\[^|;\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPath();

    [GeneratedRegex(@"(?i)(?<!\\)\\\\[^\\\s|;]+\\[^|;\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex UncPath();

    [GeneratedRegex(@"(?<![:\w])/(?:[^/\s|;]+/)*[^/\s|;]+", RegexOptions.CultureInvariant)]
    private static partial Regex UnixPath();

    [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b", RegexOptions.CultureInvariant)]
    private static partial Regex GuidValue();

    [GeneratedRegex(@"(?<![\d.])(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}(?::\d{1,5})?(?![\d.])", RegexOptions.CultureInvariant)]
    private static partial Regex IpAddress();

    [GeneratedRegex(@"(?i)(?<![0-9a-f:])(?:\[(?:[0-9a-f]{0,4}:){2,7}[0-9a-f:.]{0,39}\](?::\d{1,5})?|(?:[0-9a-f]{0,4}:){2,7}[0-9a-f]{0,4})(?![0-9a-f:])", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv6Address();

    [GeneratedRegex(@"(?i)(?<![0-9a-f])[0-9a-f]{16}(?![0-9a-f])", RegexOptions.CultureInvariant)]
    private static partial Regex CompleteXuid();

    [GeneratedRegex(@"(?i)(?<![0-9a-f])[0-9a-f]{4,8}\.{3}[0-9a-f]{4,8}(?![0-9a-f])", RegexOptions.CultureInvariant)]
    private static partial Regex AbbreviatedXuid();

    [GeneratedRegex(@"(?i)\b(password|passwd|rcon_password|rcon|secret|token|api_key)\s*=\s*[^|;\r\n\s]*", RegexOptions.CultureInvariant)]
    private static partial Regex ChatSensitiveKeyValue();

    [GeneratedRegex(@"(?i)\b(root|path|active_root)\s*=\s*[^|;\r\n]*?(?=\s+\b(?:root|path|active_root|ip|address|guid|command|args?|reason|updated_by|xuid)\s*=|$)", RegexOptions.CultureInvariant)]
    private static partial Regex PathKeyValue();

    [GeneratedRegex(@"(?i)\b(ip|address)\s*=\s*[^|;\r\n]*?(?=\s+\b(?:root|path|active_root|ip|address|guid|command|args?|reason|updated_by|xuid)\s*=|$)", RegexOptions.CultureInvariant)]
    private static partial Regex AddressKeyValue();

    [GeneratedRegex(@"(?i)\bguid\s*=\s*[^|;\r\n]*?(?=\s+\b(?:root|path|active_root|ip|address|guid|command|args?|reason|updated_by|xuid)\s*=|$)", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierKeyValue();

    [GeneratedRegex(@"(?i)\b(command|args?|reason|updated_by)\s*=\s*[^|;\r\n]*?(?=\s+\b(?:root|path|active_root|ip|address|guid|command|args?|reason|updated_by|xuid)\s*=|$)", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveKeyValue();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
