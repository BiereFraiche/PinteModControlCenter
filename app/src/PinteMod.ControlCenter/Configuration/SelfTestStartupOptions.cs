using System.IO;

namespace PinteMod.ControlCenter.Configuration;

internal sealed record SelfTestStartupOptions(string ReportPath)
{
    private const string SelfTestArgument = "--self-test";
    private const string ReportPrefix = "--self-test-report=";

    internal static string DefaultReportPath => Path.Combine(
        Path.GetTempPath(),
        "PinteMod.ControlCenter.self-test.txt");

    internal static bool IsRequested(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => string.Equals(
            argument,
            SelfTestArgument,
            StringComparison.OrdinalIgnoreCase));

    internal static SelfTestStartupOptions Parse(IReadOnlyList<string> arguments)
    {
        var selfTestCount = arguments.Count(argument => string.Equals(
            argument,
            SelfTestArgument,
            StringComparison.OrdinalIgnoreCase));
        if (selfTestCount != 1)
        {
            throw new ArgumentException("Le mode self-test doit être demandé exactement une fois.");
        }

        var reportArguments = arguments
            .Where(argument => argument.StartsWith(ReportPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (reportArguments.Length > 1)
        {
            throw new ArgumentException("Le chemin du rapport self-test ne peut être fourni qu’une fois.");
        }

        if (reportArguments.Length == 0)
        {
            return new SelfTestStartupOptions(DefaultReportPath);
        }

        var candidate = reportArguments[0][ReportPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(candidate) ||
            !Path.IsPathFullyQualified(candidate) ||
            candidate.StartsWith(@"\\", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(candidate), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Le rapport self-test exige un chemin local absolu avec l’extension .txt.");
        }

        return new SelfTestStartupOptions(Path.GetFullPath(candidate));
    }
}
