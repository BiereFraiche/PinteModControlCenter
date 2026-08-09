namespace PinteMod.ControlCenter.Core.Models;

public enum ControlCenterDataMode
{
    Simulation,
    HybridLocal
}

public enum LocalReadStatus
{
    NotAttempted,
    Success,
    Missing,
    Empty,
    Invalid,
    UnsupportedSchema,
    AccessDenied,
    IoError
}

public enum DataFreshness
{
    Fresh,
    Stale,
    Expired,
    Unknown
}

public enum DataProvenance
{
    Simulation,
    LocalFile,
    MemoryCache,
    Unavailable
}

public enum ServiceDeclaredState
{
    Running,
    Monitoring,
    Connected,
    Active,
    Paused,
    Configured,
    Stopped,
    Error,
    Unknown
}

public enum LocalServiceKind
{
    Supervisor,
    BanService,
    GeoIpBridge,
    LiveConsole
}

public sealed record LocalSourceMetadata(
    LocalReadStatus ReadStatus,
    DataFreshness Freshness,
    TimeSpan? Age,
    DataProvenance Provenance,
    string SourceLabel,
    string Message,
    int ConsecutiveFailures = 0,
    bool IsDurableFailure = false)
{
    public static LocalSourceMetadata Simulation(string sourceLabel = "Données simulées") =>
        new(
            LocalReadStatus.NotAttempted,
            DataFreshness.Unknown,
            null,
            DataProvenance.Simulation,
            sourceLabel,
            "Valeur simulée");

    public static LocalSourceMetadata Unavailable(string message) =>
        new(
            LocalReadStatus.NotAttempted,
            DataFreshness.Unknown,
            null,
            DataProvenance.Unavailable,
            "Aucune source dédiée",
            message);
}

public sealed record LocalReadResult<T>(
    T? Value,
    LocalSourceMetadata Metadata,
    DateTimeOffset? SourceTimestampUtc)
    where T : class;

public sealed record SessionManifest(
    int SchemaVersion,
    string ModuleVersion,
    string SessionId,
    string MapCode,
    long StartedGetTime);

public sealed record ServiceHeartbeat(
    int SchemaVersion,
    LocalServiceKind Kind,
    string Tool,
    string Version,
    string RawState,
    ServiceDeclaredState DeclaredState,
    long Sequence,
    DateTimeOffset UpdatedAtUtc,
    string? LastErrorCode);

public sealed record SnapshotDataContext(
    ControlCenterDataMode Mode,
    string ModeLabel,
    string? ServerRoot,
    LocalSourceMetadata SessionSource,
    IReadOnlyList<string> SimulatedAreas)
{
    public static SnapshotDataContext Simulation { get; } = new(
        ControlCenterDataMode.Simulation,
        "MODE SIMULATION",
        null,
        LocalSourceMetadata.Simulation(),
        ["Carte", "Session", "Manche", "Durée", "Ranked", "Joueurs", "Événements", "Records", "Services"]);
}
