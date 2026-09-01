using System.Text.RegularExpressions;

namespace PinteMod.ControlCenter.Services;

internal static partial class StartupFailureDiagnostic
{
    private const int MaximumMessageLength = 280;

    internal static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var deepest = exception;
        while (deepest.InnerException is not null)
        {
            deepest = deepest.InnerException;
        }

        var message = string.IsNullOrWhiteSpace(deepest.Message)
            ? "Aucun détail public fourni par Windows."
            : deepest.Message.Trim();
        message = SecretValueRegex().Replace(message, "$1=[donnée masquée]");
        message = WindowsPathRegex().Replace(message, "[chemin masqué]");
        message = UncPathRegex().Replace(message, "[chemin masqué]");
        message = WhitespaceRegex().Replace(message, " ");

        if (message.Length > MaximumMessageLength)
        {
            message = string.Concat(message.AsSpan(0, MaximumMessageLength - 1), "…");
        }

        return $"{deepest.GetType().Name} : {message}";
    }

    [GeneratedRegex(@"(?i)\b(password|mot de passe|secret|rcon|token|apikey|api key)\s*[=:]\s*[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretValueRegex();

    [GeneratedRegex("(?<![A-Za-z0-9])[A-Za-z]:\\\\[^\\s\\\"']+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex("\\\\\\\\[^\\s\\\"']+", RegexOptions.CultureInvariant)]
    private static partial Regex UncPathRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
