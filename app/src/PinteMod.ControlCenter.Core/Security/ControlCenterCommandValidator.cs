namespace PinteMod.ControlCenter.Core.Security;

public static class ControlCenterCommandValidator
{
    private static readonly HashSet<string> KnownBossAliases = new(StringComparer.Ordinal)
    {
        "margwa", "panzer", "thrasher", "shadow_margwa", "fire_margwa", "astronaut"
    };

    public static bool IsValidRequestId(string? value) =>
        value is { Length: >= 8 and <= 32 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    public static bool IsValidBossAlias(string? value) =>
        value is not null && KnownBossAliases.Contains(value);

    public static bool IsValidHostname(string? value)
    {
        if (value is not { Length: >= 1 and <= 64 } || value != value.Trim())
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '^')
            {
                if (++index >= value.Length || value[index] is < '0' or > '9')
                {
                    return false;
                }

                continue;
            }

            if (!char.IsAsciiLetterOrDigit(character) &&
                character is not (' ' or '-' or '_' or '.' or '[' or ']' or '(' or ')'))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsValidJoinPassword(string? value) =>
        value is { Length: >= 4 and <= 32 } &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '!' or '@' or '#' or '$' or '%' or '+');
}
