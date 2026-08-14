namespace PinteMod.ControlCenter.Core.Models;

public enum OperatorDataLocation
{
    Local,
    Lan
}

public static class OperatorAccentTheme
{
    public const string DefaultKey = "blue";

    public static IReadOnlyList<string> AllowedKeys { get; } =
        [DefaultKey, "cyan", "indigo", "violet", "pink", "teal"];

    public static bool IsValid(string? value) =>
        value is not null && AllowedKeys.Contains(value, StringComparer.Ordinal);

    public static string NormalizeOrDefault(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return IsValid(normalized) ? normalized! : DefaultKey;
    }
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
    public const int MaximumProfileDisplayNameLength = 48;
    public const string DefaultProfileDisplayName = "Serveur principal";

    public string ProfileDisplayName { get; init; } = DefaultProfileDisplayName;

    public string AccentColorKey { get; init; } = OperatorAccentTheme.DefaultKey;

    public static bool IsValidProfileDisplayName(string? value)
    {
        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) &&
               normalized.Length <= MaximumProfileDisplayNameLength &&
               normalized.All(character => !char.IsControl(character));
    }

    public static OperatorConfiguration Default { get; } = new(
        CurrentSchemaVersion,
        OperatorDataLocation.Local,
        string.Empty,
        false,
        "127.0.0.1",
        27017);
}

public sealed record OperatorWorkspaceConfiguration(
    int SchemaVersion,
    IReadOnlyList<string> ProfileIds,
    string ActiveProfileId)
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumProfileCount = 8;
    public const string PrimaryProfileId = "primary";

    public static OperatorWorkspaceConfiguration Default { get; } = new(
        CurrentSchemaVersion,
        [PrimaryProfileId],
        PrimaryProfileId);
}
