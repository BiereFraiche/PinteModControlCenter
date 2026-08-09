namespace PinteMod.ControlCenter.Core.Security;

public static class MapCodeValidator
{
    public const int MaximumLength = 64;

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < 1 or > MaximumLength)
        {
            return false;
        }

        return normalized.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }
}
