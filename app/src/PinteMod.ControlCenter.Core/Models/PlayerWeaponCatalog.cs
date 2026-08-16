namespace PinteMod.ControlCenter.Core.Models;

public sealed record PlayerWeaponCatalogEntry(
    string Alias,
    string DisplayName,
    bool IsMapSpecific = false);

public static class PlayerWeaponCatalog
{
    private static readonly IReadOnlyList<PlayerWeaponCatalogEntry> StandardEntries =
    [
        new("kn44", "KN-44"),
        new("hvk", "HVK-30"),
        new("icr", "ICR-1"),
        new("manowar", "Man-O-War"),
        new("kuda", "Kuda"),
        new("vmp", "VMP"),
        new("krm", "KRM-262"),
        new("brecci", "205 Brecci"),
        new("haymaker", "Haymaker 12"),
        new("argus", "Argus"),
        new("brm", "BRM"),
        new("dingo", "Dingo"),
        new("gorgon", "Gorgon"),
        new("dredge", "48 Dredge"),
        new("drakon", "Drakon"),
        new("locus", "Locus"),
        new("svg", "SVG-100"),
        new("raygun", "Ray Gun"),
        new("raygunmk2", "Ray Gun Mark II")
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<PlayerWeaponCatalogEntry>> MapEntries =
        new Dictionary<string, IReadOnlyList<PlayerWeaponCatalogEntry>>(StringComparer.OrdinalIgnoreCase)
        {
            ["zm_zod"] = Special(
                ("apothicon", "Apothicon Servant"),
                ("apothiconup", "Apothicon Servant amélioré"),
                ("arnies", "Lil' Arnies"),
                ("arniesup", "Lil' Arnies améliorés"),
                ("annihilator", "Annihilator"),
                ("apothiconsword", "Épée Apothicon"),
                ("keepersword", "Épée du Gardien")),
            ["zm_factory"] = Special(
                ("wunderwaffe", "Wunderwaffe DG-2"),
                ("wunderwaffeup", "Wunderwaffe améliorée"),
                ("annihilator", "Annihilator")),
            ["zm_castle"] = Special(
                ("bow", "Arc des Anciens"),
                ("stormbow", "Arc de la foudre"),
                ("firebow", "Arc de feu"),
                ("wolfbow", "Arc du loup"),
                ("voidbow", "Arc du néant"),
                ("ragnarok", "Ragnarok DG-4")),
            ["zm_island"] = Special(
                ("kt4", "KT-4"),
                ("masamune", "Masamune"),
                ("skull", "Crâne de Nan Sapwe")),
            ["zm_stalingrad"] = Special(
                ("raygunmk3", "GKZ-45 Mk3"),
                ("raygunmk3up", "GKZ-45 Mk3 amélioré"),
                ("gauntlet", "Gantelet de Siegfried"),
                ("dragonstrike", "Frappe du dragon")),
            ["zm_genesis"] = Special(
                ("apothicon", "Apothicon Servant"),
                ("apothiconup", "Apothicon Servant amélioré"),
                ("thundergun", "Thundergun"),
                ("arnies", "Lil' Arnies"),
                ("ragnarok", "Ragnarok DG-4"),
                ("katana", "Katana")),
            ["zm_prototype"] = Special(("thundergun", "Thundergun")),
            ["zm_asylum"] = Special(("wunderwaffe", "Wunderwaffe DG-2")),
            ["zm_sumpf"] = Special(("wunderwaffe", "Wunderwaffe DG-2")),
            ["zm_theater"] = Special(("thundergun", "Thundergun")),
            ["zm_cosmodrome"] = Special(
                ("thundergun", "Thundergun"),
                ("gersh", "Dispositif Gersh"),
                ("dolls", "Poupées Matriochka")),
            ["zm_temple"] = Special(
                ("babygun", "31-79 JGb215"),
                ("babygunup", "31-79 JGb215 amélioré"),
                ("monkey", "Bombe singe")),
            ["zm_moon"] = Special(
                ("wavegun", "Wave Gun"),
                ("wavegunup", "Wave Gun amélioré"),
                ("qed", "QED"),
                ("gersh", "Dispositif Gersh")),
            ["zm_tomb"] = Special(
                ("windstaff", "Bâton de vent"),
                ("icestaff", "Bâton de glace"),
                ("lightningstaff", "Bâton de foudre"),
                ("firestaff", "Bâton de feu"),
                ("windstaffup", "Bâton de vent amélioré"),
                ("icestaffup", "Bâton de glace amélioré"),
                ("lightningstaffup", "Bâton de foudre amélioré"),
                ("firestaffup", "Bâton de feu amélioré"),
                ("gstrike", "G-Strike"),
                ("oneinch", "Coup de poing amélioré"),
                ("firefists", "Poings de feu"),
                ("icefists", "Poings de glace"),
                ("windfists", "Poings de vent"),
                ("lightningfists", "Poings de foudre"))
        };

    private static readonly HashSet<string> AllowedAliases = StandardEntries
        .Concat(MapEntries.Values.SelectMany(entries => entries))
        .Select(entry => entry.Alias)
        .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<PlayerWeaponCatalogEntry> Standard => StandardEntries;

    public static IReadOnlyList<PlayerWeaponCatalogEntry> ForMap(string? mapCode)
    {
        if (string.IsNullOrWhiteSpace(mapCode) || !MapEntries.TryGetValue(mapCode, out var entries))
        {
            return [];
        }

        return entries;
    }

    public static IReadOnlyList<PlayerWeaponCatalogEntry> AvailableForMap(string? mapCode) =>
        StandardEntries.Concat(ForMap(mapCode)).ToArray();

    public static bool IsAllowedAlias(string? alias) =>
        alias is not null && AllowedAliases.Contains(alias);

    private static IReadOnlyList<PlayerWeaponCatalogEntry> Special(
        params (string Alias, string DisplayName)[] entries) =>
        entries.Select(entry => new PlayerWeaponCatalogEntry(entry.Alias, entry.DisplayName, true)).ToArray();
}
