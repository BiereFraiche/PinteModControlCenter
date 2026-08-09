namespace PinteMod.ControlCenter.Core.Models;

public sealed record EasterEggRecord(
    string MapCode,
    string MapName,
    int PlayerCount,
    int Position,
    int Round,
    TimeSpan Duration,
    string Holders,
    IReadOnlyList<string> HolderXuids,
    string RunId,
    string Source);

public sealed record EasterEggRecordCatalog(
    IReadOnlyList<EasterEggRecord> Records,
    int OfficialProfileCount,
    int MapFilesScanned,
    int MapFilesSkipped,
    int RecordSlotsSkipped,
    bool MapsDirectoryPresent);

public sealed record EasterEggRecordsSnapshot(
    IReadOnlyList<EasterEggRecord> Records,
    LocalSourceMetadata Source,
    int OfficialProfileCount,
    int MapFilesScanned,
    int MapFilesSkipped,
    int RecordSlotsSkipped,
    bool MapsDirectoryPresent)
{
    public static EasterEggRecordsSnapshot Simulation { get; } = new(
        [],
        LocalSourceMetadata.Simulation("Easter Egg Records simulés"),
        0,
        0,
        0,
        0,
        false);
}
