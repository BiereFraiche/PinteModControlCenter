using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class BlockAControlCenterDataProvider(
    IControlCenterDataProvider baselineProvider,
    ISessionManifestReader sessionReader,
    IInstallationVerificationReader installationReader,
    IBanServiceStatusReader banServiceStatusReader,
    ILocalPlayerMetadataReader metadataReader,
    IStructuredLogReader logReader,
    ICommunityPauseStatusReader? communityPauseStatusReader = null,
    ICommunityPauseLogReader? communityPauseLogReader = null) : IControlCenterDataProvider
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var baselineTask = baselineProvider.GetSnapshotAsync(cancellationToken);
        var sessionTask = sessionReader.ReadAsync(cancellationToken);
        var installationTask = installationReader.ReadAsync(cancellationToken);
        var banStatusTask = banServiceStatusReader.ReadAsync(cancellationToken);
        var metadataTask = metadataReader.ReadAsync(cancellationToken);
        var pauseStatusTask = communityPauseStatusReader?.ReadAsync(cancellationToken) ??
                              Task.FromResult(new LocalReadResult<CommunityPauseStatusSnapshot>(
                                  null,
                                  LocalSourceMetadata.Unavailable("Statut Community Pause non configuré."),
                                  null));

        await Task.WhenAll(baselineTask, sessionTask, installationTask, banStatusTask, metadataTask, pauseStatusTask);
        var baseline = await baselineTask;
        var session = await sessionTask;
        var installation = await installationTask;
        var banStatus = await banStatusTask;
        var metadata = await metadataTask;
        var pauseStatus = NormalizePauseStatusForSession(await pauseStatusTask, session);
        var logs = await logReader.ReadAsync(session.Value, cancellationToken);
        var pauseLogs = communityPauseLogReader is null
            ? CommunityPauseLogSnapshot.Empty(LocalSourceMetadata.Unavailable("Journal Community Pause non configuré."))
            : await communityPauseLogReader.ReadAsync(session.Value, cancellationToken);
        var mergedLogs = MergeLogs(logs, pauseLogs);

        var players = EnrichPlayers(mergedLogs.Players, metadata.Value);
        var logSourceAvailable = mergedLogs.Source.ReadStatus == LocalReadStatus.Success ||
                                 mergedLogs.Source.Provenance == DataProvenance.MemoryCache;
        var server = baseline.Server with
        {
            Round = mergedLogs.Round ?? 0,
            PlayersConnected = logSourceAvailable ? players.Count : 0,
            MaxPlayers = 0,
            RankedStatus = mergedLogs.RankedStatusAvailable ? mergedLogs.RankedStatus : RankedStatus.Unknown,
            SessionDuration = mergedLogs.SessionDuration ?? TimeSpan.Zero,
            ServerRunning = false,
            RoundAvailable = mergedLogs.Round is not null,
            PlayersConnectedAvailable = logSourceAvailable,
            MaxPlayersAvailable = false,
            RankedStatusAvailable = mergedLogs.RankedStatusAvailable,
            SessionDurationAvailable = mergedLogs.SessionDuration is not null,
            ServerRunningAvailable = false,
            RuntimeValuesInferred = true
        };

        return baseline with
        {
            Server = server,
            Players = players,
            Events = mergedLogs.Events,
            DataContext = baseline.DataContext with
            {
                SimulatedAreas = ["Actions joueur et serveur uniquement"]
            },
            LocalObservation = new BlockALocalSnapshot(installation, banStatus, metadata, mergedLogs)
            {
                CommunityPause = pauseStatus,
                CommunityPauseLogSource = pauseLogs.Source
            }
        };
    }

    private static StructuredLogSnapshot MergeLogs(
        StructuredLogSnapshot logs,
        CommunityPauseLogSnapshot pauseLogs)
    {
        var events = logs.Events
            .Concat(pauseLogs.Events)
            .OrderByDescending(item => item.SessionElapsed)
            .Take(500)
            .ToArray();
        return logs with
        {
            Events = events,
            FilesScanned = logs.FilesScanned + (pauseLogs.Source.ReadStatus == LocalReadStatus.Success ? 1 : 0),
            LinesIgnored = logs.LinesIgnored + pauseLogs.LinesIgnored,
            MalformedLines = logs.MalformedLines + pauseLogs.MalformedLines,
            CachedEventCount = events.Length
        };
    }

    private static LocalReadResult<CommunityPauseStatusSnapshot> NormalizePauseStatusForSession(
        LocalReadResult<CommunityPauseStatusSnapshot> pauseStatus,
        LocalReadResult<SessionManifest> session)
    {
        if (pauseStatus.Value is null || pauseStatus.SourceTimestampUtc is null || session.SourceTimestampUtc is null ||
            pauseStatus.SourceTimestampUtc >= session.SourceTimestampUtc)
        {
            return pauseStatus;
        }

        return new(null, pauseStatus.Metadata with
        {
            ReadStatus = LocalReadStatus.NotAttempted,
            Freshness = DataFreshness.Unknown,
            Age = null,
            Message = "Statut Community Pause antérieur à la session active."
        }, pauseStatus.SourceTimestampUtc);
    }

    private static IReadOnlyList<PlayerState> EnrichPlayers(
        IReadOnlyList<PlayerState> players,
        LocalPlayerMetadataSnapshot? metadata)
    {
        if (metadata is null)
        {
            return players;
        }

        var byXuid = metadata.Players.ToDictionary(item => item.Xuid, StringComparer.OrdinalIgnoreCase);
        return players.Select(player =>
        {
            if (!byXuid.TryGetValue(player.Xuid, out var local))
            {
                return player;
            }

            return player with
            {
                DisplayName = string.IsNullOrWhiteSpace(player.DisplayName) ? local.DisplayName ?? "Joueur local" : player.DisplayName,
                Role = local.Role ?? player.Role,
                Language = local.Language ?? player.Language,
                CountryCode = local.CountryCode ?? player.CountryCode
            };
        }).ToArray();
    }
}
