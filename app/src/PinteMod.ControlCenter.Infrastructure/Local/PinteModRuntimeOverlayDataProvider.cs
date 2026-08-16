using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class PinteModRuntimeOverlayDataProvider(
    IControlCenterDataProvider baselineProvider,
    IPinteModHeartbeatReader heartbeatReader,
    IControlCenterRuntimeSnapshotReader runtimeReader) : IControlCenterDataProvider
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var baseline = await baselineProvider.GetSnapshotAsync(cancellationToken);
        var sessionReady = baseline.DataContext.Mode == ControlCenterDataMode.HybridLocal &&
                           baseline.DataContext.SessionSource.ReadStatus == LocalReadStatus.Success &&
                           baseline.DataContext.SessionSource.Provenance == DataProvenance.LocalFile &&
                           baseline.Server.SessionProvenance == DataProvenance.LocalFile;
        var sessionId = sessionReady ? baseline.Server.SessionId : null;
        var mapCode = sessionReady ? baseline.Server.MapCode : null;
        var heartbeatTask = heartbeatReader.ReadAsync(sessionId, cancellationToken);
        var runtimeTask = runtimeReader.ReadAsync(sessionId, mapCode, cancellationToken);
        await Task.WhenAll(heartbeatTask, runtimeTask);

        var heartbeat = await heartbeatTask;
        var runtime = await runtimeTask;
        var runtimeIsAuthoritative = IsFreshLocal(runtime.Metadata) && runtime.Value is not null;
        var heartbeatIsAuthoritative = IsFreshLocal(heartbeat.Metadata) && heartbeat.Value is not null;

        var server = OverlayServer(
            baseline.Server,
            runtimeIsAuthoritative ? runtime.Value : null,
            runtime.Metadata,
            runtime.SourceTimestampUtc,
            heartbeatIsAuthoritative ? heartbeat.Value : null);
        var players = runtimeIsAuthoritative
            ? OverlayPlayers(runtime.Value!.Players, baseline.Players)
            : baseline.Players;
        var services = baseline.Services
            .Where(service => !string.Equals(service.Name, "PinteMod", StringComparison.OrdinalIgnoreCase))
            .Prepend(CreatePinteModStatus(heartbeat))
            .ToArray();

        return baseline with
        {
            Server = server,
            Players = players,
            Services = services,
            DataContext = baseline.DataContext with
            {
                SimulatedAreas = ["Changement/restart de carte", "Événements génériques", "Boss génériques"]
            },
            LocalObservation = baseline.LocalObservation with
            {
                PinteModHeartbeat = heartbeat,
                RuntimeSnapshot = runtime
            }
        };
    }

    private static ServerState OverlayServer(
        ServerState baseline,
        ControlCenterRuntimeSnapshot? runtime,
        LocalSourceMetadata runtimeSource,
        DateTimeOffset? runtimeTimestamp,
        PinteModHeartbeatSnapshot? heartbeat)
    {
        var runningAvailable = heartbeat?.DeclaredState is ServiceDeclaredState.Running or ServiceDeclaredState.Stopped or ServiceDeclaredState.Error;
        var server = baseline with
        {
            PinteModVersion = heartbeat?.ModuleVersion ?? runtime?.ModuleVersion ?? baseline.PinteModVersion,
            ServerRunning = heartbeat?.DeclaredState == ServiceDeclaredState.Running,
            ServerRunningAvailable = runningAvailable,
            ObservedServerHealth = heartbeat?.DeclaredState switch
            {
                ServiceDeclaredState.Running => ServiceHealth.Healthy,
                ServiceDeclaredState.Stopped => ServiceHealth.Offline,
                ServiceDeclaredState.Error => ServiceHealth.Error,
                _ => ServiceHealth.Unknown
            },
            RuntimeSource = runtimeSource
        };

        if (runtime is null)
        {
            return server;
        }

        return server with
        {
            MapCode = runtime.MapCode,
            MapName = OfficialMapNameResolver.Resolve(runtime.MapCode),
            Round = runtime.Round ?? 0,
            PlayersConnected = runtime.ConnectedPlayers,
            MaxPlayers = runtime.MaximumPlayers ?? 0,
            RankedStatus = runtime.RankedStatus,
            SessionDuration = runtime.SessionElapsed ?? TimeSpan.Zero,
            UpdatedAtUtc = runtimeTimestamp ?? baseline.UpdatedAtUtc,
            MapProvenance = DataProvenance.LocalFile,
            RoundAvailable = runtime.Round is not null,
            PlayersConnectedAvailable = true,
            MaxPlayersAvailable = runtime.MaximumPlayers is not null,
            RankedStatusAvailable = runtime.RankedStatus != RankedStatus.Unknown,
            SessionDurationAvailable = runtime.SessionElapsed is not null,
            RuntimeValuesInferred = false,
            PowerState = runtime.PowerState,
            PackAPunchState = runtime.PackAPunchState
        };
    }

    private static IReadOnlyList<PlayerState> OverlayPlayers(
        IReadOnlyList<RuntimePlayerSnapshot> runtimePlayers,
        IReadOnlyList<PlayerState> baselinePlayers)
    {
        var metadataByXuid = baselinePlayers.ToDictionary(player => player.Xuid, StringComparer.OrdinalIgnoreCase);
        return runtimePlayers.Select(runtime =>
        {
            metadataByXuid.TryGetValue(runtime.Xuid, out var local);
            return new PlayerState(
                runtime.ClientNumber,
                runtime.Xuid,
                runtime.DisplayName,
                local?.Role ?? "unknown",
                local?.Language ?? "unknown",
                local?.CountryCode ?? "--",
                runtime.LifeState,
                runtime.Points ?? 0,
                local?.Presence ?? TimeSpan.Zero,
                false,
                false)
            {
                LifeStateAvailable = true,
                PointsAvailable = runtime.Points is not null,
                PresenceAvailable = local?.PresenceAvailable == true,
                Provenance = DataProvenance.LocalFile,
                ModerationStateAvailable = false,
                RuntimeDetails = runtime
            };
        }).ToArray();
    }

    private static ServiceStatus CreatePinteModStatus(LocalReadResult<PinteModHeartbeatSnapshot> result)
    {
        var description = result.Value is null
            ? result.Metadata.Message
            : result.Metadata.Freshness == DataFreshness.Expired
                ? "Dernière donnée valide — périmée."
            : result.Metadata.Provenance == DataProvenance.MemoryCache
                ? result.Metadata.Message
                : $"État déclaré : {DisplayDeclaredState(result.Value.DeclaredState)} · version {result.Value.ModuleVersion}";
        return new(
            "PinteMod",
            description,
            SynthesizeHealth(result),
            result.SourceTimestampUtc ?? DateTimeOffset.MinValue)
        {
            DeclaredState = result.Value?.DeclaredState ?? ServiceDeclaredState.Unknown,
            Source = result.Metadata
        };
    }

    internal static ServiceHealth SynthesizeHealth(LocalReadResult<PinteModHeartbeatSnapshot> result)
    {
        if (result.Metadata.Freshness is DataFreshness.Expired or DataFreshness.Unknown)
        {
            return ServiceHealth.Unknown;
        }

        if (result.Metadata.ReadStatus != LocalReadStatus.Success ||
            result.Metadata.Provenance != DataProvenance.LocalFile ||
            result.Metadata.Freshness == DataFreshness.Stale)
        {
            return ServiceHealth.Warning;
        }

        return result.Value?.DeclaredState switch
        {
            ServiceDeclaredState.Running => ServiceHealth.Healthy,
            ServiceDeclaredState.Stopped => ServiceHealth.Offline,
            ServiceDeclaredState.Error => ServiceHealth.Error,
            _ => ServiceHealth.Unknown
        };
    }

    private static string DisplayDeclaredState(ServiceDeclaredState state) => state switch
    {
        ServiceDeclaredState.Running => "running",
        ServiceDeclaredState.Stopped => "stopped",
        ServiceDeclaredState.Error => "error",
        _ => "unknown"
    };

    private static bool IsFreshLocal(LocalSourceMetadata metadata) =>
        metadata.ReadStatus == LocalReadStatus.Success &&
        metadata.Freshness == DataFreshness.Fresh &&
        metadata.Provenance == DataProvenance.LocalFile;
}
