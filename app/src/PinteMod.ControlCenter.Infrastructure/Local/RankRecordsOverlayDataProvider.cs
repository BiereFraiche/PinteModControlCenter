using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class RankRecordsOverlayDataProvider(
    IControlCenterDataProvider baseProvider,
    IRankProfileReader rankProfileReader,
    IRoundRecordReader roundRecordReader) : IControlCenterDataProvider
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var baseTask = baseProvider.GetSnapshotAsync(cancellationToken);
        var profilesTask = rankProfileReader.ReadAsync(cancellationToken);
        var recordsTask = roundRecordReader.ReadAsync(cancellationToken);

        await Task.WhenAll(baseTask, profilesTask, recordsTask);

        var snapshot = await baseTask;
        var profiles = await profilesTask;
        var records = await recordsTask;
        var localRecords = records.Value?.Records.Select(record => ToRecordEntry(record, records.Metadata.Provenance)) ?? [];
        var easterEggSimulation = snapshot.Records.Where(record => record.IsEasterEgg);

        return snapshot with
        {
            Records = localRecords.Concat(easterEggSimulation).ToArray(),
            RankRecords = new RankRecordsSnapshot(
                profiles.Value?.Profiles ?? [],
                records.Value?.Records ?? [],
                profiles.Metadata,
                records.Metadata,
                profiles.Value?.FilesScanned ?? 0,
                profiles.Value?.FilesSkipped ?? 0,
                records.Value?.FilesScanned ?? 0,
                records.Value?.FilesSkipped ?? 0,
                records.Value?.SlotsSkipped ?? 0),
            DataContext = snapshot.DataContext with
            {
                SimulatedAreas =
                [
                    "Manche",
                    "Durée",
                    "Ranked",
                    "Serveur BOIII",
                    "Joueurs",
                    "Événements",
                    "Easter Egg Records"
                ]
            }
        };
    }

    private static RecordEntry ToRecordEntry(RoundRecord record, DataProvenance provenance) =>
        new(
            record.MapCode,
            record.MapName,
            record.PlayerCount,
            record.Round,
            record.Duration,
            record.Holders,
            false)
        {
            Position = record.Position,
            HolderXuids = record.HolderXuids,
            Provenance = provenance
        };
}
