using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed partial class FinalPrivacyRegressionTests
{
    [TestMethod]
    public void ProductionAndContracts_ContainOnlyReservedSyntheticXuidLiterals()
    {
        var repositoryRoot = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "app", "src"),
                "*.cs",
                SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "contracts"),
                "*.json",
                SearchOption.TopDirectoryOnly));

        foreach (var file in files)
        {
            var contents = File.ReadAllText(file);
            foreach (Match match in QuotedXuidLiteral().Matches(contents))
            {
                var value = match.Groups["xuid"].Value;
                Assert.IsTrue(
                    ReservedSyntheticXuid().IsMatch(value),
                    $"Identifiant XUID littéral non réservé dans {Path.GetRelativePath(repositoryRoot, file)}.");
            }
        }
    }

    [TestMethod]
    public void PublicReaderCode_DoesNotReflectSystemExceptionMessages()
    {
        var repositoryRoot = FindRepositoryRoot();
        var localReadersRoot = Path.Combine(
            repositoryRoot,
            "app",
            "src",
            "PinteMod.ControlCenter.Infrastructure",
            "Local");

        foreach (var file in Directory.EnumerateFiles(localReadersRoot, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var contents = File.ReadAllText(file);
            Assert.IsFalse(
                SystemExceptionMessageFlow().IsMatch(contents),
                $"Message brut d’exception système détecté dans {Path.GetFileName(file)}.");
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "app")) &&
                Directory.Exists(Path.Combine(current.FullName, "contracts")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Racine du dépôt introuvable pour les tests de confidentialité.");
    }

    [GeneratedRegex("\\\"(?<xuid>[0-9a-fA-F]{16})\\\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedXuidLiteral();

    [GeneratedRegex("^000000000000000[1-9a-fA-F]$", RegexOptions.CultureInvariant)]
    private static partial Regex ReservedSyntheticXuid();

    [GeneratedRegex(
        @"catch\s*\(\s*(?:IOException|UnauthorizedAccessException|JsonException)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)[\s\S]{0,500}?\k<name>\.Message",
        RegexOptions.CultureInvariant)]
    private static partial Regex SystemExceptionMessageFlow();
}
