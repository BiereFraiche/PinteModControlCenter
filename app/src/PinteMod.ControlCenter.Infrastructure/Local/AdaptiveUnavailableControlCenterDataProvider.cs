using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

/// <summary>
/// Represents a real BOIII profile for which no structured Control Center data source
/// is currently proven. This is intentionally empty/fail-closed: a registered real
/// server must never fall back to the demo/simulation snapshot.
/// </summary>
public sealed class AdaptiveUnavailableControlCenterDataProvider(
    string serverRoot,
    ServerIntegrationProfile integrationProfile) : IControlCenterDataProvider
{
    private readonly string _serverRoot = Path.GetFullPath(serverRoot.Trim());
    private readonly ServerIntegrationProfile _integrationProfile = integrationProfile;

    public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateSnapshot());
    }

    private DashboardSnapshot CreateSnapshot()
    {
        var unavailable = LocalSourceMetadata.Unavailable(UnavailableMessage());
        var now = DateTimeOffset.UtcNow;
        var isUnc = _serverRoot.StartsWith(@"\\", StringComparison.Ordinal);
        var runtimeProbe = new ManagedServerRuntimeProbe();
        var running = runtimeProbe.IsRunning(_serverRoot, 0);
        // A local process probe can authoritatively distinguish running/stopped for
        // the exact BOIII root. On UNC, only a fresh first-party heartbeat can prove
        // "running"; absence of one must remain unknown rather than "stopped".
        var runningAvailable = !isUnc || running;
        var server = new ServerState(
            "—",
            running,
            string.Empty,
            "—",
            0,
            0,
            0,
            RankedStatus.Unknown,
            TimeSpan.Zero,
            now)
        {
            SessionId = string.Empty,
            MapProvenance = DataProvenance.Unavailable,
            SessionProvenance = DataProvenance.Unavailable,
            RoundAvailable = false,
            PlayersConnectedAvailable = false,
            MaxPlayersAvailable = false,
            RankedStatusAvailable = false,
            SessionDurationAvailable = false,
            ServerRunningAvailable = runningAvailable,
            RuntimeValuesInferred = false,
            RuntimeSource = unavailable,
            ObservedServerHealth = runningAvailable
                ? running ? ServiceHealth.Healthy : ServiceHealth.Offline
                : ServiceHealth.Unknown
        };

        var rankRecords = new RankRecordsSnapshot(
            [],
            [],
            unavailable,
            unavailable,
            0,
            0,
            0,
            0,
            0);
        var eeRecords = new EasterEggRecordsSnapshot(
            [],
            unavailable,
            0,
            0,
            0,
            0,
            false);
        var local = new BlockALocalSnapshot(
            new LocalReadResult<InstallationVerificationReport>(null, unavailable, null),
            new LocalReadResult<BanServiceStatusSnapshot>(null, unavailable, null),
            new LocalReadResult<LocalPlayerMetadataSnapshot>(null, unavailable, null),
            StructuredLogSnapshot.Empty(string.Empty, unavailable))
        {
            CommunityPause = new LocalReadResult<CommunityPauseStatusSnapshot>(null, unavailable, null),
            CommunityPauseLogSource = unavailable,
            PinteModHeartbeat = new LocalReadResult<PinteModHeartbeatSnapshot>(null, unavailable, null),
            RuntimeSnapshot = new LocalReadResult<ControlCenterRuntimeSnapshot>(null, unavailable, null),
            ControlCenterContracts = ControlCenterContractSnapshot.Unavailable
        };

        return new DashboardSnapshot(server, [], [], [], [])
        {
            // HybridLocal means "real/non-simulated" to the historical ViewModels.
            // No local source is claimed: every metadata item remains Unavailable.
            DataContext = new SnapshotDataContext(
                ControlCenterDataMode.HybridLocal,
                ModeLabel(),
                _serverRoot,
                unavailable,
                []),
            RankRecords = rankRecords,
            EasterEggRecords = eeRecords,
            LocalObservation = local
        };
    }

    private string ModeLabel() => _integrationProfile.Kind switch
    {
        ManagedServerIntegrationKind.BoiiiNative => "MODE RÉEL · BOIII NATIF",
        ManagedServerIntegrationKind.ThirdPartyScripts => "MODE RÉEL · GSC TIERS LIMITÉ",
        ManagedServerIntegrationKind.ControlCenterBridge => "MODE RÉEL · BRIDGE À VALIDER",
        ManagedServerIntegrationKind.PinteMod => "MODE RÉEL · PINTE MOD NON OBSERVÉ",
        _ => "MODE RÉEL · DONNÉES INDISPONIBLES"
    };

    private string UnavailableMessage() => _integrationProfile.Kind switch
    {
        ManagedServerIntegrationKind.BoiiiNative =>
            "Serveur BOIII réel détecté ; aucune télémétrie structurée native n’est prouvée dans cette Preview.",
        ManagedServerIntegrationKind.ThirdPartyScripts =>
            "Serveur réel avec GSC tiers détecté ; aucune source structurée compatible n’est prouvée.",
        ManagedServerIntegrationKind.ControlCenterBridge =>
            "Bridge détecté mais aucune observation runtime structurée n’est encore disponible.",
        ManagedServerIntegrationKind.PinteMod =>
            "PinteMod est détecté mais aucune source locale structurée n’est actuellement active.",
        _ => "Serveur réel détecté ; aucune source structurée n’est disponible."
    };
}
