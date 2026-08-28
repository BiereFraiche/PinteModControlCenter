using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class HybridControlCenterDataProvider(
    IControlCenterDataProvider simulatedProvider,
    ISessionManifestReader sessionReader,
    IServiceHeartbeatReader heartbeatReader,
    LocalPinteModOptions options) : IControlCenterDataProvider
{
    private static readonly LocalServiceKind[] ServiceKinds =
    [
        LocalServiceKind.Supervisor,
        LocalServiceKind.BanService,
        LocalServiceKind.GeoIpBridge
    ];

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var simulatedTask = simulatedProvider.GetSnapshotAsync(cancellationToken);
        var sessionTask = sessionReader.ReadAsync(cancellationToken);
        var heartbeatTasks = ServiceKinds
            .Select(kind => heartbeatReader.ReadAsync(kind, cancellationToken))
            .ToArray();

        await Task.WhenAll(heartbeatTasks.Prepend<Task>(simulatedTask).Append(sessionTask));

        var simulated = await simulatedTask;
        var session = await sessionTask;
        var heartbeatResults = heartbeatTasks.Select(task => task.Result).ToArray();

        var server = OverlaySession(simulated.Server, session);
        var services = new List<ServiceStatus>
        {
            CreatePinteModStatus()
        };
        services.AddRange(heartbeatResults.Select(CreateServiceStatus));

        return simulated with
        {
            Server = server,
            Services = services,
            DataContext = new SnapshotDataContext(
                ControlCenterDataMode.HybridLocal,
                "MODE HYBRIDE LOCAL",
                options.ServerRoot,
                session.Metadata,
                ["Manche", "Durée", "Ranked", "Serveur BOIII", "Joueurs", "Événements", "Records"])
        };
    }

    private static ServerState OverlaySession(
        ServerState simulated,
        LocalReadResult<SessionManifest> session)
    {
        if (session.Value is null)
        {
            return simulated;
        }

        return simulated with
        {
            PinteModVersion = session.Value.ModuleVersion,
            MapCode = session.Value.MapCode,
            MapName = OfficialMapNameResolver.Resolve(session.Value.MapCode),
            SessionId = session.Value.SessionId,
            MapProvenance = session.Metadata.Provenance,
            SessionProvenance = session.Metadata.Provenance
        };
    }

    private static ServiceStatus CreatePinteModStatus() =>
        new(
            "PinteMod",
            "État inconnu — aucun heartbeat dédié",
            ServiceHealth.Unknown,
            DateTimeOffset.MinValue)
        {
            DeclaredState = ServiceDeclaredState.Unknown,
            Source = LocalSourceMetadata.Unavailable("État inconnu — aucun heartbeat dédié")
        };

    private static ServiceStatus CreateServiceStatus(LocalReadResult<ServiceHeartbeat> result)
    {
        var name = GetDisplayName(result.Value?.Kind, result.Metadata.SourceLabel);
        var description = result.Value is null
            ? result.Metadata.Message
            : result.Metadata.Provenance == DataProvenance.MemoryCache
                ? result.Metadata.Message
                : $"État déclaré : {result.Value.RawState} · version {result.Value.Version}";

        return new ServiceStatus(
            name,
            description,
            SynthesizeHealth(result),
            result.Value?.UpdatedAtUtc ?? DateTimeOffset.MinValue)
        {
            DeclaredState = result.Value?.DeclaredState ?? ServiceDeclaredState.Unknown,
            Source = result.Metadata
        };
    }

    public static ServiceHealth SynthesizeHealth(LocalReadResult<ServiceHeartbeat> result)
    {
        if (result.Value?.DeclaredState == ServiceDeclaredState.Error)
        {
            return ServiceHealth.Error;
        }

        if (result.Value?.DeclaredState == ServiceDeclaredState.Stopped)
        {
            return ServiceHealth.Offline;
        }

        if (result.Metadata.IsDurableFailure)
        {
            return ServiceHealth.Error;
        }

        if (result.Metadata.Freshness is DataFreshness.Expired or DataFreshness.Unknown)
        {
            return ServiceHealth.Unknown;
        }

        if (result.Metadata.ReadStatus != LocalReadStatus.Success ||
            result.Metadata.Freshness == DataFreshness.Stale)
        {
            return ServiceHealth.Warning;
        }

        return result.Value?.DeclaredState switch
        {
            ServiceDeclaredState.Running or
                ServiceDeclaredState.Monitoring or
                ServiceDeclaredState.Connected or
                ServiceDeclaredState.Active => ServiceHealth.Healthy,
            ServiceDeclaredState.Paused or ServiceDeclaredState.Configured => ServiceHealth.Warning,
            _ => ServiceHealth.Unknown
        };
    }

    private static string GetDisplayName(LocalServiceKind? service, string sourceLabel) =>
        service switch
        {
            LocalServiceKind.Supervisor => "Supervisor",
            LocalServiceKind.BanService => "Ban Service",
            LocalServiceKind.GeoIpBridge => "GeoIP Bridge",
            LocalServiceKind.LiveConsole => "Live Console",
            _ when sourceLabel.Contains("supervisor", StringComparison.OrdinalIgnoreCase) => "Supervisor",
            _ when sourceLabel.Contains("ban_service", StringComparison.OrdinalIgnoreCase) => "Ban Service",
            _ when sourceLabel.Contains("geoip_bridge", StringComparison.OrdinalIgnoreCase) => "GeoIP Bridge",
            _ when sourceLabel.Contains("live_console", StringComparison.OrdinalIgnoreCase) => "Live Console",
            _ => "Service local"
        };
}
