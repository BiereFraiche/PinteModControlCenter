namespace PinteMod.ControlCenter.Core.Models;

public sealed record InstallationVerificationCheck(
    string Name,
    string Status,
    string Recommendation);

public sealed record InstallationVerificationReport(
    int SchemaVersion,
    string Tool,
    string Version,
    DateTimeOffset CheckedAtUtc,
    int PassCount,
    int WarningCount,
    int ErrorCount,
    IReadOnlyList<InstallationVerificationCheck> Checks);

public sealed record BanServiceStatusSnapshot(
    int SchemaVersion,
    string Version,
    bool Running,
    DateTimeOffset UpdatedAtUtc,
    int ActiveBans,
    string PrivacyMode);

public sealed record LocalPlayerMetadata(
    string Xuid,
    string? DisplayName,
    string? Role,
    string? Language,
    string? CountryCode);

public sealed record LocalPlayerMetadataSnapshot(
    IReadOnlyList<LocalPlayerMetadata> Players,
    int FilesScanned,
    int FilesSkipped);

public sealed record CommunityPauseStatusSnapshot(
    string ModuleVersion,
    long GeneratedGetTime,
    bool Active,
    int AutomaticResumeSeconds,
    int SuccessfulPauses,
    int MaximumSuccessfulPauses,
    int PauseProposalsUsed,
    string ActiveVote,
    int? VoteYes,
    int? VoteNo,
    int? VoteMajority,
    bool TemporaryGodMode,
    bool SpectatorSpawnGuard,
    bool NewAiSpawningBlocked);

public sealed record CommunityPauseLogSnapshot(
    IReadOnlyList<LiveEvent> Events,
    LocalSourceMetadata Source,
    int LinesIgnored,
    int MalformedLines)
{
    public static CommunityPauseLogSnapshot Empty(LocalSourceMetadata source) =>
        new([], source, 0, 0);
}

public sealed record StructuredLogSnapshot(
    string SessionId,
    IReadOnlyList<LiveEvent> Events,
    IReadOnlyList<PlayerState> Players,
    int? Round,
    TimeSpan? SessionDuration,
    RankedStatus RankedStatus,
    bool RankedStatusAvailable,
    LocalSourceMetadata Source,
    int FilesScanned,
    int LinesIgnored,
    int MalformedLines,
    int CachedEventCount)
{
    public static StructuredLogSnapshot Empty(string sessionId, LocalSourceMetadata source) =>
        new(sessionId, [], [], null, null, RankedStatus.Unknown, false, source, 0, 0, 0, 0);
}

public sealed record BlockALocalSnapshot(
    LocalReadResult<InstallationVerificationReport> InstallationVerification,
    LocalReadResult<BanServiceStatusSnapshot> BanServiceStatus,
    LocalReadResult<LocalPlayerMetadataSnapshot> PlayerMetadata,
    StructuredLogSnapshot Logs)
{
    private static readonly LocalSourceMetadata SimulationMetadata = LocalSourceMetadata.Simulation();

    public LocalReadResult<CommunityPauseStatusSnapshot> CommunityPause { get; init; } =
        new(null, LocalSourceMetadata.Unavailable("Statut Community Pause non lu."), null);

    public LocalSourceMetadata CommunityPauseLogSource { get; init; } =
        LocalSourceMetadata.Unavailable("Journal Community Pause non lu.");

    public LocalReadResult<PinteModHeartbeatSnapshot> PinteModHeartbeat { get; init; } =
        new(null, LocalSourceMetadata.Unavailable("Heartbeat PinteMod non lu."), null);

    public LocalReadResult<ControlCenterRuntimeSnapshot> RuntimeSnapshot { get; init; } =
        new(null, LocalSourceMetadata.Unavailable("Snapshot runtime PinteMod non lu."), null);

    public static BlockALocalSnapshot Simulation { get; } = new(
        new(null, SimulationMetadata, null),
        new(null, SimulationMetadata, null),
        new(null, SimulationMetadata, null),
        StructuredLogSnapshot.Empty("simulation-session", SimulationMetadata));
}
