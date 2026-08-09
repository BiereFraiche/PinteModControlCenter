namespace PinteMod.ControlCenter.Core.Models;

public sealed record MapCatalogEntry(
    string Code,
    string DisplayName,
    bool IsOfficial,
    bool IsInServerRotation,
    bool IsManual,
    bool IsObserved);

public sealed record MapCatalogSnapshot(IReadOnlyList<MapCatalogEntry> Entries)
{
    public static MapCatalogSnapshot OfficialOnly { get; } = new(OfficialMapCatalog.Entries);
}

public sealed record MapCatalogOperationResult(
    bool Success,
    string Status,
    string Message,
    int AffectedCount = 0);

public static class OfficialMapCatalog
{
    public static IReadOnlyList<MapCatalogEntry> Entries { get; } =
    [
        Official("zm_zod", "Shadows of Evil"),
        Official("zm_castle", "Der Eisendrache"),
        Official("zm_island", "Zetsubou No Shima"),
        Official("zm_stalingrad", "Gorod Krovi"),
        Official("zm_genesis", "Revelations"),
        Official("zm_cosmodrome", "Ascension"),
        Official("zm_theater", "Kino der Toten"),
        Official("zm_moon", "Moon"),
        Official("zm_prototype", "Nacht der Untoten"),
        Official("zm_tomb", "Origins"),
        Official("zm_temple", "Shangri-La"),
        Official("zm_sumpf", "Shi No Numa"),
        Official("zm_factory", "The Giant"),
        Official("zm_asylum", "Verrückt")
    ];

    public static string ResolveName(string code) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.Code, code, StringComparison.OrdinalIgnoreCase))?.DisplayName
        ?? code;

    public static bool Contains(string code) =>
        Entries.Any(entry => string.Equals(entry.Code, code, StringComparison.OrdinalIgnoreCase));

    private static MapCatalogEntry Official(string code, string name) =>
        new(code, name, true, false, false, false);
}
