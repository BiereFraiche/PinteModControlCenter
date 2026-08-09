namespace PinteMod.ControlCenter.Core.Models;

public sealed record RankProfile(
    string Xuid,
    string DisplayName,
    int Sessions,
    TimeSpan TotalPlayTime,
    int BestOverallRound);

public sealed record RoundRecord(
    string MapCode,
    string MapName,
    int PlayerCount,
    int Position,
    int Round,
    TimeSpan Duration,
    string Holders,
    IReadOnlyList<string> HolderXuids,
    string MatchId);

public sealed record RankProfileCatalog(
    IReadOnlyList<RankProfile> Profiles,
    int FilesScanned,
    int FilesSkipped);

public sealed record RoundRecordCatalog(
    IReadOnlyList<RoundRecord> Records,
    int FilesScanned,
    int FilesSkipped,
    int SlotsSkipped);

public sealed record RankRecordsSnapshot(
    IReadOnlyList<RankProfile> Profiles,
    IReadOnlyList<RoundRecord> RoundRecords,
    LocalSourceMetadata ProfilesSource,
    LocalSourceMetadata RoundRecordsSource,
    int ProfileFilesScanned,
    int ProfileFilesSkipped,
    int MapFilesScanned,
    int MapFilesSkipped,
    int RecordSlotsSkipped)
{
    public static RankRecordsSnapshot Simulation { get; } = new(
        [],
        [],
        LocalSourceMetadata.Simulation("Profils Ranks simulés"),
        LocalSourceMetadata.Simulation("Records de manches simulés"),
        0,
        0,
        0,
        0,
        0);
}
