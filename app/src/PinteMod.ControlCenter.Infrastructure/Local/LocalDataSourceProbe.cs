using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class LocalDataSourceProbe : ILocalDataSourceProbe
{
    private static readonly LocalServiceKind[] RequiredServiceKinds =
    [
        LocalServiceKind.Supervisor,
        LocalServiceKind.BanService,
        LocalServiceKind.GeoIpBridge
    ];

    public Task<LocalDataSourceProbeResult> ProbeAsync(
        LocalDataSourceProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => ProbeWorkerAsync(request, cancellationToken), cancellationToken);
    }

    private static async Task<LocalDataSourceProbeResult> ProbeWorkerAsync(
        LocalDataSourceProbeRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.ServerRoot))
        {
            return Rejected("Indiquez un chemin de données PinteMod.");
        }

        var isUnc = request.ServerRoot.TrimStart().StartsWith("\\\\", StringComparison.Ordinal);
        if (request.Location == OperatorDataLocation.Local && isUnc)
        {
            return Rejected("Le mode Local exige un chemin présent sur cette machine.");
        }

        if (request.Location == OperatorDataLocation.Lan && !isUnc)
        {
            return Rejected("Le mode LAN exige un chemin UNC explicite.");
        }

        try
        {
            var configuredRoot = request.ServerRoot.Trim();
            var rootLayout = Directory.Exists(Path.Combine(configuredRoot, "boiii", "scriptdata", "pintemod"))
                ? LocalPinteModRootLayout.ServerRoot
                : request.Location == OperatorDataLocation.Lan
                    ? LocalPinteModRootLayout.PinteModDataRoot
                    : LocalPinteModRootLayout.ServerRoot;
            var options = new LocalPinteModOptions(configuredRoot, rootLayout);
            var clock = new SystemClock();
            using var sessionReader = new SessionManifestReader(options, clock);
            using var heartbeatReader = new ServiceHeartbeatReader(options, clock);

            var sessionTask = sessionReader.ReadAsync(cancellationToken);
            var heartbeatTasks = RequiredServiceKinds
                .Select(service => heartbeatReader.ReadAsync(service, cancellationToken))
                .ToArray();

            await sessionTask.ConfigureAwait(false);
            await Task.WhenAll(heartbeatTasks).ConfigureAwait(false);

            var sources = new List<LocalDataSourceProbeItem>(5)
            {
                new("Session", sessionTask.Result.Metadata.ReadStatus, sessionTask.Result.Metadata.Freshness)
            };
            sources.AddRange(RequiredServiceKinds.Zip(
                heartbeatTasks,
                (service, task) => new LocalDataSourceProbeItem(
                    ServiceName(service),
                    task.Result.Metadata.ReadStatus,
                    task.Result.Metadata.Freshness)));

            var readable = sources.Count(source => source.ReadStatus == LocalReadStatus.Success);
            var message = readable == sources.Count
                ? "Source valide · session et trois heartbeats lisibles."
                : readable > 0
                    ? $"Racine accessible · {readable}/{sources.Count} sources lisibles."
                    : "Racine accessible, mais aucune source PinteMod reconnue n’est lisible.";

            return new LocalDataSourceProbeResult(true, sources, message);
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or
                                          IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Rejected(exception switch
            {
                DirectoryNotFoundException => "Le dossier indiqué est introuvable ou inaccessible.",
                UnauthorizedAccessException => "L’accès au dossier indiqué est refusé.",
                _ => "Le chemin indiqué ne peut pas être utilisé comme racine PinteMod."
            });
        }
    }

    private static LocalDataSourceProbeResult Rejected(string message) => new(false, [], message);

    private static string ServiceName(LocalServiceKind service) => service switch
    {
        LocalServiceKind.Supervisor => "Supervisor",
        LocalServiceKind.BanService => "Ban Service",
        LocalServiceKind.GeoIpBridge => "GeoIP Bridge",
        LocalServiceKind.LiveConsole => "Live Console",
        _ => "Service local"
    };
}
