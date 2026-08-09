namespace PinteMod.ControlCenter.Core.Models;

public enum OperatorDataLocation
{
    Local,
    Lan
}

public sealed record LocalDataSourceProbeRequest(
    OperatorDataLocation Location,
    string ServerRoot);

public sealed record LocalDataSourceProbeItem(
    string Name,
    LocalReadStatus ReadStatus,
    DataFreshness Freshness);

public sealed record LocalDataSourceProbeResult(
    bool RootAccepted,
    IReadOnlyList<LocalDataSourceProbeItem> Sources,
    string Message)
{
    public int ReadableSourceCount => Sources.Count(source => source.ReadStatus == LocalReadStatus.Success);

    public int TotalSourceCount => Sources.Count;

    public bool HasReadableSource => ReadableSourceCount > 0;
}

public sealed record OperatorConfiguration(
    int SchemaVersion,
    OperatorDataLocation DataLocation,
    string ServerRoot,
    bool ActivateDataSourceOnStartup,
    string RconAddress,
    int RconPort)
{
    public const int CurrentSchemaVersion = 1;

    public static OperatorConfiguration Default { get; } = new(
        CurrentSchemaVersion,
        OperatorDataLocation.Local,
        string.Empty,
        false,
        "127.0.0.1",
        27017);
}
