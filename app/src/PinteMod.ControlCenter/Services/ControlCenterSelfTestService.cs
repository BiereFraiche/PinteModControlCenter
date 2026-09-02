using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Views;

namespace PinteMod.ControlCenter.Services;

public interface IControlCenterSelfTestService
{
    Task<ControlCenterSelfTestReport> RunAsync(CancellationToken cancellationToken = default);
}

public sealed record ControlCenterSelfTestCheck(
    string Name,
    bool Success,
    string Message);

public sealed record ControlCenterSelfTestReport(
    string ProductVersion,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ControlCenterSelfTestCheck> Checks)
{
    public bool Success => Checks.Count > 0 && Checks.All(check => check.Success);

    public string ToDisplayText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("PINTE MOD CONTROL CENTER — AUTO-DIAGNOSTIC LOCAL");
        builder.AppendLine($"VERSION={ProductVersion}");
        builder.AppendLine($"DATE_UTC={GeneratedAtUtc:O}");
        builder.AppendLine($"RESULTAT={(Success ? "PASS" : "FAIL")}");
        builder.AppendLine();
        foreach (var check in Checks)
        {
            builder.Append(check.Success ? "PASS" : "FAIL");
            builder.Append(" | ");
            builder.Append(check.Name);
            builder.Append(" | ");
            builder.AppendLine(check.Message);
        }

        builder.AppendLine();
        builder.AppendLine("GARANTIES=aucun profil serveur lu; aucun secret lu; aucun réseau; aucune commande BOIII/RCON");
        return builder.ToString();
    }

    internal static ControlCenterSelfTestReport CreateStartupFailure() => new(
        ControlCenterSelfTestService.ExpectedProductVersion,
        DateTimeOffset.UtcNow,
        [new ControlCenterSelfTestCheck(
            "Initialisation",
            false,
            "Le self-test n’a pas pu être initialisé dans son environnement local.")]);
}

public sealed class ControlCenterSelfTestService : IControlCenterSelfTestService
{
    internal const string ExpectedProductVersion = "2.4.5-rc26";

    private readonly Func<ControlCenterSelfTestCheck> _userInterfaceProbe;

    public ControlCenterSelfTestService()
        : this(CheckUserInterface)
    {
    }

    internal ControlCenterSelfTestService(Func<ControlCenterSelfTestCheck> userInterfaceProbe)
    {
        _userInterfaceProbe = userInterfaceProbe ?? throw new ArgumentNullException(nameof(userInterfaceProbe));
    }

    public async Task<ControlCenterSelfTestReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<ControlCenterSelfTestCheck>
        {
            CheckPlatform(),
            CheckVersion(),
            CheckAssemblies(),
            RunFixedProbe(_userInterfaceProbe, "Interface WPF")
        };
        checks.Add(await CheckEmbeddedPayloadsAsync(cancellationToken).ConfigureAwait(false));
        checks.Add(new ControlCenterSelfTestCheck(
            "Isolation",
            true,
            "Exécution confinée à la mémoire et à une racine temporaire jetable."));

        return new ControlCenterSelfTestReport(
            CurrentProductVersion(),
            DateTimeOffset.UtcNow,
            checks);
    }

    private static ControlCenterSelfTestCheck CheckPlatform() =>
        OperatingSystem.IsWindows() && Environment.Is64BitProcess
            ? new ControlCenterSelfTestCheck(
                "Plateforme",
                true,
                $"Windows x64 · .NET {Environment.Version.Major}.{Environment.Version.Minor}")
            : new ControlCenterSelfTestCheck(
                "Plateforme",
                false,
                "Windows x64 est requis.");

    private static ControlCenterSelfTestCheck CheckVersion()
    {
        var current = CurrentProductVersion();
        return string.Equals(current, ExpectedProductVersion, StringComparison.Ordinal)
            ? new ControlCenterSelfTestCheck("Version", true, $"Version attendue {current}.")
            : new ControlCenterSelfTestCheck(
                "Version",
                false,
                "Les métadonnées produit ne correspondent pas à la version candidate 2.4.5 attendue.");
    }

    private static ControlCenterSelfTestCheck CheckAssemblies()
    {
        try
        {
            var assemblies = new[]
            {
                typeof(ControlCenterDataMode).Assembly,
                typeof(EmbeddedServerPayloadService).Assembly,
                typeof(ControlCenterSelfTestService).Assembly
            };
            return assemblies.All(assembly => !string.IsNullOrWhiteSpace(assembly.GetName().Name))
                ? new ControlCenterSelfTestCheck(
                    "Assemblages",
                    true,
                    "Core, Infrastructure et WPF sont chargés.")
                : new ControlCenterSelfTestCheck(
                    "Assemblages",
                    false,
                    "Un assemblage produit est incomplet.");
        }
        catch
        {
            return new ControlCenterSelfTestCheck(
                "Assemblages",
                false,
                "Le chargement des assemblages produit a échoué.");
        }
    }

    private static ControlCenterSelfTestCheck CheckUserInterface()
    {
        if (Application.Current is null)
        {
            return new ControlCenterSelfTestCheck(
                "Interface WPF",
                false,
                "Les ressources WPF ne sont pas initialisées.");
        }

        try
        {
            FrameworkElement[] views =
            [
                new DashboardView(),
                new PlayersView(),
                new ServerView(),
                new RecordsView(),
                new LogsView(),
                new SettingsView()
            ];
            var resourcesReady = Application.Current.TryFindResource("AccentBrush") is not null &&
                                 Application.Current.TryFindResource("CardStyle") is not null;
            return views.Length == 6 && resourcesReady
                ? new ControlCenterSelfTestCheck(
                    "Interface WPF",
                    true,
                    "Les six pages et le thème ont été chargés sans affichage de fenêtre.")
                : new ControlCenterSelfTestCheck(
                    "Interface WPF",
                    false,
                    "Une vue ou une ressource graphique obligatoire est absente.");
        }
        catch
        {
            return new ControlCenterSelfTestCheck(
                "Interface WPF",
                false,
                "Le chargement hors écran des vues WPF a échoué.");
        }
    }

    private static async Task<ControlCenterSelfTestCheck> CheckEmbeddedPayloadsAsync(
        CancellationToken cancellationToken)
    {
        var parent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "PinteMod.ControlCenter.SelfTest"));
        var root = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
        var prefix = parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return new ControlCenterSelfTestCheck(
                "Payloads embarqués",
                false,
                "La racine temporaire de contrôle a été refusée.");
        }

        var cleanupSucceeded = true;
        var payloadSucceeded = false;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "boiii"));
            var service = new EmbeddedServerPayloadService();
            var pinteMod = await service
                .InstallPinteModStableAsync(root, cancellationToken)
                .ConfigureAwait(false);
            var bridge = pinteMod.Success
                ? await service
                    .InstallOrUpdateBridgeAsync(root, ["zm_tomb"], cancellationToken)
                    .ConfigureAwait(false)
                : new ServerDeploymentResult(false, string.Empty, [], []);
            var analysis = bridge.Success
                ? new ServerInstallationAnalyzer().Analyze(root, cancellationToken)
                : null;
            payloadSucceeded = pinteMod.Success &&
                               bridge.Success &&
                               analysis?.PinteModDetected == true &&
                               analysis.ControlCenterBridgeDetected;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            payloadSucceeded = false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch
            {
                cleanupSucceeded = false;
            }
        }

        return payloadSucceeded && cleanupSucceeded
            ? new ControlCenterSelfTestCheck(
                "Payloads embarqués",
                true,
                "PinteMod et Bridge reconnus par leurs empreintes dans une racine temporaire supprimée.")
            : new ControlCenterSelfTestCheck(
                "Payloads embarqués",
                false,
                cleanupSucceeded
                    ? "Un payload embarqué n’a pas satisfait son contrôle d’intégrité."
                    : "La racine temporaire du self-test n’a pas pu être supprimée.");
    }

    private static ControlCenterSelfTestCheck RunFixedProbe(
        Func<ControlCenterSelfTestCheck> probe,
        string name)
    {
        try
        {
            return probe();
        }
        catch
        {
            return new ControlCenterSelfTestCheck(
                name,
                false,
                "Le contrôle local n’a pas pu être terminé.");
        }
    }

    private static string CurrentProductVersion() =>
        typeof(ControlCenterSelfTestService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";
}

internal static class SelfTestReportFileWriter
{
    internal static void Write(string reportPath, string contents)
    {
        var fullPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Dossier de rapport introuvable.");
        Directory.CreateDirectory(directory);
        var temporary = fullPath + $".{Environment.ProcessId}.tmp";
        try
        {
            File.WriteAllText(temporary, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
