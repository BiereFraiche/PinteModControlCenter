using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ServerStorageAnalyzerTests
{
    [TestMethod]
    public void Analyze_DetectsOversizedGeoIpStatisticsWithoutTouchingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.StorageTests", Guid.NewGuid().ToString("N"));
        try
        {
            var stats = Path.Combine(root, "boiii", "scriptdata", "pintemod", "localization", "stats");
            Directory.CreateDirectory(stats);
            var countries = Path.Combine(stats, "countries.json");
            File.WriteAllBytes(countries, new byte[2 * 1024 * 1024]);

            var result = new ServerStorageAnalyzer().Analyze(root);

            Assert.IsTrue(result.GeoIpStatisticsAnomalous);
            Assert.AreEqual(2L * 1024 * 1024, result.CountryJsonBytes);
            Assert.IsTrue(File.Exists(countries));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Analyze_DetectsLegacyGeoIpBridgeEvenAfterLargeStatsWereDeleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.StorageTests", Guid.NewGuid().ToString("N"));
        try
        {
            var scriptData = Path.Combine(root, "boiii", "scriptdata");
            Directory.CreateDirectory(scriptData);
            var tools = Path.Combine(root, "boiii", "tools");
            Directory.CreateDirectory(tools);
            File.WriteAllText(Path.Combine(tools, "PinteMod_GeoIP_Bridge.ps1"), "# old bridge without stats guards");

            var result = new ServerStorageAnalyzer().Analyze(root);

            Assert.IsFalse(result.GeoIpStatisticsAnomalous);
            Assert.IsTrue(result.GeoIpBridgeNeedsHardening);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
