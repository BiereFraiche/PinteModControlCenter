using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PlayerWeaponCatalogTests
{
    [TestMethod]
    public void StandardCatalog_ContainsOnlyTheNineteenCanonicalAliases()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "kn44", "hvk", "icr", "manowar", "kuda", "vmp", "krm", "brecci", "haymaker", "argus",
                "brm", "dingo", "gorgon", "dredge", "drakon", "locus", "svg", "raygun", "raygunmk2"
            },
            PlayerWeaponCatalog.Standard.Select(entry => entry.Alias).ToArray());
        Assert.IsTrue(PlayerWeaponCatalog.Standard.All(entry => !entry.IsMapSpecific));
    }

    [DataTestMethod]
    [DataRow("zm_zod", "apothicon,apothiconup,arnies,arniesup,annihilator,apothiconsword,keepersword")]
    [DataRow("zm_factory", "wunderwaffe,wunderwaffeup,annihilator")]
    [DataRow("zm_castle", "bow,stormbow,firebow,wolfbow,voidbow,ragnarok")]
    [DataRow("zm_island", "kt4,masamune,skull")]
    [DataRow("zm_stalingrad", "raygunmk3,raygunmk3up,gauntlet,dragonstrike")]
    [DataRow("zm_genesis", "apothicon,apothiconup,thundergun,arnies,ragnarok,katana")]
    [DataRow("zm_prototype", "thundergun")]
    [DataRow("zm_asylum", "wunderwaffe")]
    [DataRow("zm_sumpf", "wunderwaffe")]
    [DataRow("zm_theater", "thundergun")]
    [DataRow("zm_cosmodrome", "thundergun,gersh,dolls")]
    [DataRow("zm_temple", "babygun,babygunup,monkey")]
    [DataRow("zm_moon", "wavegun,wavegunup,qed,gersh")]
    [DataRow("zm_tomb", "windstaff,icestaff,lightningstaff,firestaff,windstaffup,icestaffup,lightningstaffup,firestaffup,gstrike,oneinch,firefists,icefists,windfists,lightningfists")]
    public void MapCatalog_ContainsOnlyCanonicalSpecialAliases(string mapCode, string expectedCsv)
    {
        CollectionAssert.AreEqual(
            expectedCsv.Split(','),
            PlayerWeaponCatalog.ForMap(mapCode).Select(entry => entry.Alias).ToArray());
        Assert.IsTrue(PlayerWeaponCatalog.ForMap(mapCode).All(entry => entry.IsMapSpecific));
    }

    [TestMethod]
    public void UnknownMap_HasNoSpecials_AndTechnicalAliasesAreRejected()
    {
        Assert.AreEqual(0, PlayerWeaponCatalog.ForMap("zm_custom_unknown").Count);
        Assert.AreEqual(19, PlayerWeaponCatalog.AvailableForMap("zm_custom_unknown").Count);
        foreach (var forbidden in new[] { "rg", "kn", "hvk30", "ar_standard", "ray_gun", "weapon_none", "raygun;quit" })
        {
            Assert.IsFalse(PlayerWeaponCatalog.IsAllowedAlias(forbidden), forbidden);
        }
    }
}
