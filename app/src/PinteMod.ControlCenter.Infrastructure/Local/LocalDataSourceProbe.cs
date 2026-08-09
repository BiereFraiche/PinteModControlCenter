using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class LocalDataSourceProbe : ILocalDataSourceProbe
{
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
            var options = new LocalPinteModOptions(
                request.ServerRoot.Trim(),
                request.Location == OperatorDataLocation.Lan
                    ? LocalPinteModRootLayout.PinteModDataRoot
                    : LocalPinteModRootLayout.ServerRoot);
            var clock = new SystemClock();
            using var sessionReader = new SessionManifestReader(options, clock);
            using var heartbeatReader = new ServiceHeartbeatReader(options, clock);

            var sessionTask = sessionReader.ReadAsync(cancellationToken);
            var heartbeatTasks = Enum.GetValues<LocalServiceKind>()
                .Select(service => heartbeatReader.ReadAsync(service, cancellationToken))
                .ToArray();

            await sessionTask.ConfigureAwait(false);
            await Task.WhenAll(heartbeatTasks).ConfigureAwait(false);

            var sources = new List<LocalDataSourceProbeItem>(5)
            {
                new("Session", sessionTask.Result.Metadata.ReadStatus, sessionTask.Result.Metadata.Freshness)
            };
            sources.AddRange(Enum.GetValues<LocalServiceKind>().Zip(
                heartbeatTasks,
                (service, task) => new LocalDataSourceProbeItem(
                    ServiceName(service),
                    task.Result.Metadata.ReadStatus,
                    task.Result.Metadata.Freshness)));

            var readable = sources.Count(source => source.ReadStatus == LocalReadStatus.Success);
            var message = readable == sources.Count
                ? "Source valide · session et quatre heartbeats lisibles."
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
