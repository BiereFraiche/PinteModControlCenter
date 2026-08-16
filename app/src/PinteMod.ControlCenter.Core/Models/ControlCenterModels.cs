namespace PinteMod.ControlCenter.Core.Models;

public enum ServiceHealth
{
    Healthy,
    Warning,
    Offline,
    Error,
    Unknown
}

public enum RankedStatus
{
    Ranked,
    Unranked,
    Unknown
}

public enum PlayerLifeState
{
    Alive,
    Downed,
    Dead,
    Spectator,
    Unknown
}

public enum EventSeverity
{
    Information,
    Success,
    Warning,
    Danger
}

public sealed record ServerState(
    string PinteModVersion,
    bool ServerRunning,
    string MapCode,
    string MapName,
    int Round,
    int PlayersConnected,
    int MaxPlayers,
    RankedStatus RankedStatus,
    TimeSpan SessionDuration,
    DateTimeOffset UpdatedAtUtc)
{
    public string SessionId { get; init; } = "simulation-session";

    public DataProvenance MapProvenance { get; init; } = DataProvenance.Simulation;

    public DataProvenance SessionProvenance { get; init; } = DataProvenance.Simulation;

    public bool RoundAvailable { get; init; } = true;

    public bool PlayersConnectedAvailable { get; init; } = true;

    public bool MaxPlayersAvailable { get; init; } = true;

    public bool RankedStatusAvailable { get; init; } = true;

    public bool SessionDurationAvailable { get; init; } = true;

    public bool ServerRunningAvailable { get; init; } = true;

    public bool RuntimeValuesInferred { get; init; }

    public RuntimePowerState PowerState { get; init; } = RuntimePowerState.Unknown;

    public RuntimePackAPunchState PackAPunchState { get; init; } = RuntimePackAPunchState.Unknown;

    public LocalSourceMetadata RuntimeSource { get; init; } = LocalSourceMetadata.Simulation();

    public ServiceHealth ObservedServerHealth { get; init; } = ServiceHealth.Unknown;
}

public sealed record ServiceStatus(
    string Name,
    string Description,
    ServiceHealth Health,
    DateTimeOffset LastSeenUtc)
{
    public ServiceDeclaredState DeclaredState { get; init; } = ServiceDeclaredState.Unknown;

    public LocalSourceMetadata Source { get; init; } = LocalSourceMetadata.Simulation();
}

public sealed record PlayerState(
    int ClientNumber,
    string Xuid,
    string DisplayName,
    string Role,
    string Language,
    string CountryCode,
    PlayerLifeState LifeState,
    int Points,
    TimeSpan Presence,
    bool IsMuted,
    bool IsBanned)
{
    public bool LifeStateAvailable { get; init; } = true;

    public bool PointsAvailable { get; init; } = true;

    public bool PresenceAvailable { get; init; } = true;

    public DataProvenance Provenance { get; init; } = DataProvenance.Simulation;

    public bool ModerationStateAvailable { get; init; } = true;

    public RuntimePlayerSnapshot? RuntimeDetails { get; init; }
}

public sealed record LiveEvent(
    DateTimeOffset OccurredAt,
    string Category,
    string Title,
    string Details,
    EventSeverity Severity)
{
    public TimeSpan? SessionElapsed { get; init; }

    public DataProvenance Provenance { get; init; } = DataProvenance.Simulation;

    public string SourceLabel { get; init; } = "Simulation";
}

public sealed record RecordEntry(
    string MapCode,
    string MapName,
    int PlayerCount,
    int Round,
    TimeSpan Duration,
    string Holder,
    bool IsEasterEgg)
{
    public int Position { get; init; }

    public IReadOnlyList<string> HolderXuids { get; init; } = [];

    public DataProvenance Provenance { get; init; } = DataProvenance.Simulation;
}

public sealed record DashboardSnapshot(
    ServerState Server,
    IReadOnlyList<ServiceStatus> Services,
    IReadOnlyList<PlayerState> Players,
    IReadOnlyList<LiveEvent> Events,
    IReadOnlyList<RecordEntry> Records)
{
    public SnapshotDataContext DataContext { get; init; } = SnapshotDataContext.Simulation;

    public RankRecordsSnapshot RankRecords { get; init; } = RankRecordsSnapshot.Simulation;

    public EasterEggRecordsSnapshot EasterEggRecords { get; init; } = EasterEggRecordsSnapshot.Simulation;

    public BlockALocalSnapshot LocalObservation { get; init; } = BlockALocalSnapshot.Simulation;
}
