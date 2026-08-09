using System.Text.RegularExpressions;

namespace PinteMod.ControlCenter.Core.Security;

public static partial class XuidValidator
{
    public const int ExpectedLength = 16;

    public static bool IsValid(string? xuid) =>
        xuid is not null && XuidPattern().IsMatch(xuid);

    public static string Abbreviate(string xuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xuid);

        return xuid.Length <= 8
            ? xuid
            : $"{xuid[..4]}…{xuid[^4..]}";
    }

    [GeneratedRegex("^[0-9a-fA-F]{16}$", RegexOptions.CultureInvariant)]
    private static partial Regex XuidPattern();
}
