using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class EasterEggRecordsOverlayDataProvider(
    IControlCenterDataProvider baseProvider,
    IEasterEggRecordReader easterEggRecordReader) : IControlCenterDataProvider
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var baseTask = baseProvider.GetSnapshotAsync(cancellationToken);
        var recordsTask = easterEggRecordReader.ReadAsync(cancellationToken);

        await Task.WhenAll(baseTask, recordsTask);

        var snapshot = await baseTask;
        var records = await recordsTask;
        var standardRecords = snapshot.Records.Where(record => !record.IsEasterEgg);
        var localRecords = records.Value?.Records.Select(record => ToRecordEntry(record, records.Metadata.Provenance)) ?? [];

        return snapshot with
        {
            Records = standardRecords.Concat(localRecords).ToArray(),
            EasterEggRecords = new EasterEggRecordsSnapshot(
                records.Value?.Records ?? [],
                records.Metadata,
                records.Value?.OfficialProfileCount ?? 0,
                records.Value?.MapFilesScanned ?? 0,
                records.Value?.MapFilesSkipped ?? 0,
                records.Value?.RecordSlotsSkipped ?? 0,
                records.Value?.MapsDirectoryPresent ?? false),
            DataContext = snapshot.DataContext with
            {
                SimulatedAreas = snapshot.DataContext.SimulatedAreas
                    .Where(area => !string.Equals(area, "Easter Egg Records", StringComparison.Ordinal))
                    .ToArray()
            }
        };
    }

    private static RecordEntry ToRecordEntry(EasterEggRecord record, DataProvenance provenance) =>
        new(
            record.MapCode,
            record.MapName,
            record.PlayerCount,
            record.Round,
            record.Duration,
            record.Holders,
            true)
        {
            Position = record.Position,
            HolderXuids = record.HolderXuids,
            Provenance = provenance
        };
}
