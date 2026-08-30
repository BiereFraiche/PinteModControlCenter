using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class EmbeddedServerPayloadServiceTests
{
    [TestMethod]
    public async Task InstallStable_CreatesPinteModWithoutOverwritingThirdPartyGsc()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var thirdParty = Path.Combine(directory.CustomScripts, "custom_owner_script.gsc");
        await File.WriteAllTextAsync(thirdParty, "OWNER-CONTENT");

        var result = await new EmbeddedServerPayloadService().InstallPinteModStableAsync(directory.Root);

        Assert.IsTrue(result.Success, result.Message);
        Assert.IsTrue(File.Exists(Path.Combine(directory.CustomScripts, "ezz_admin_01_main.gsc")));
        Assert.AreEqual("OWNER-CONTENT", await File.ReadAllTextAsync(thirdParty));
    }

    [TestMethod]
    public async Task InstallStable_ProvidesVerifierCompatibleWithCurrentModuleInventory()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();

        var result = await new EmbeddedServerPayloadService().InstallPinteModStableAsync(directory.Root);

        Assert.IsTrue(result.Success, result.Message);
        var verifier = Path.Combine(directory.Root, "boiii", "tools", "Verify_PinteMod_Installation.ps1");
        var content = await File.ReadAllTextAsync(verifier);
        StringAssert.Contains(content, "$gsc.Count -in @(28,35)");
        StringAssert.Contains(content, "Not provided by current Ezz BOIII distributions");
        Assert.IsFalse(content.Contains("Add-Result WARNING 'BOIII hotfix.gsc'", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RepairVerifier_UpgradesOnlyTheKnownLegacyInstallationVerifier()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var verifier = Path.Combine(directory.Root, "boiii", "tools", "Verify_PinteMod_Installation.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(verifier)!);

        using (var archive = ZipFile.OpenRead(FindRepositoryFile("reference", "PinteMod_v2.1.1.zip")))
        {
            var legacy = archive.GetEntry("boiii/tools/Verify_PinteMod_Installation.ps1");
            Assert.IsNotNull(legacy);
            await using var source = legacy.Open();
            await using var destination = new FileStream(verifier, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(destination);
        }

        var result = await new EmbeddedServerPayloadService().RepairInstallationVerifierAsync(directory.Root);

        Assert.IsTrue(result.Success, result.Message);
        Assert.IsTrue(result.CreatedFiles.Any(path =>
            path.Replace('\\', '/').Equals("boiii/tools/Verify_PinteMod_Installation.ps1", StringComparison.OrdinalIgnoreCase)));
        StringAssert.Contains(await File.ReadAllTextAsync(verifier), "$gsc.Count -in @(28,35)");
    }

    [TestMethod]
    public async Task RepairVerifier_LeavesExistingPinteModModulesUntouched()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var service = new EmbeddedServerPayloadService();
        var install = await service.InstallPinteModStableAsync(directory.Root);
        Assert.IsTrue(install.Success, install.Message);

        var banService = Path.Combine(directory.Root, "boiii", "tools", "PinteMod_Ban_Service.ps1");
        const string customBanService = "OPERATOR-SPECIFIC-BAN-SERVICE";
        await File.WriteAllTextAsync(banService, customBanService);
        var verifier = Path.Combine(directory.Root, "boiii", "tools", "Verify_PinteMod_Installation.ps1");
        await WriteLegacyVerifierAsync(verifier);

        var result = await service.RepairInstallationVerifierAsync(directory.Root);

        Assert.IsTrue(result.Success, result.Message);
        Assert.AreEqual(customBanService, await File.ReadAllTextAsync(banService));
        StringAssert.Contains(await File.ReadAllTextAsync(verifier), "$gsc.Count -in @(28,35)");
    }

    [TestMethod]
    public async Task RepairVerifier_RefusesAnUnknownVerifierWithoutWritingAnything()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var verifier = Path.Combine(directory.Root, "boiii", "tools", "Verify_PinteMod_Installation.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(verifier)!);
        await File.WriteAllTextAsync(verifier, "OPERATOR-OWNED-VERIFIER");

        var result = await new EmbeddedServerPayloadService().RepairInstallationVerifierAsync(directory.Root);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("OPERATOR-OWNED-VERIFIER", await File.ReadAllTextAsync(verifier));
    }

    [TestMethod]
    public async Task InstallStable_RefusesDifferentExistingFirstPartyFileBeforeWritingAnything()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var collision = Path.Combine(directory.CustomScripts, "ezz_admin_01_main.gsc");
        await File.WriteAllTextAsync(collision, "UNKNOWN-CONTENT");

        var result = await new EmbeddedServerPayloadService().InstallPinteModStableAsync(directory.Root);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("UNKNOWN-CONTENT", await File.ReadAllTextAsync(collision));
        Assert.IsFalse(File.Exists(Path.Combine(directory.CustomScripts, "ezz_admin_storage.gsc")));
    }

    [TestMethod]
    public async Task InstallBridge_WritesOnlyDeclaredOfficialMapsAndNeverPasswordData()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var service = new EmbeddedServerPayloadService();
        var install = await service.InstallPinteModStableAsync(directory.Root);
        Assert.IsTrue(install.Success, install.Message);

        var result = await service.InstallOrUpdateBridgeAsync(
            directory.Root,
            ["zm_castle", "invalid_custom", "zm_tomb"]);

        Assert.IsTrue(result.Success, result.Message);
        var allowlist = Path.Combine(
            directory.Root,
            "boiii", "scriptdata", "pintemod", "config", "control_center_map_allowlist.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(allowlist));
        Assert.AreEqual(2, document.RootElement.GetProperty("count").GetInt32());
        Assert.AreEqual("zm_castle", document.RootElement.GetProperty("map_1").GetString());
        Assert.AreEqual("zm_tomb", document.RootElement.GetProperty("map_2").GetString());
        Assert.IsFalse((await File.ReadAllTextAsync(allowlist)).Contains("password"));
    }


    [TestMethod]
    public async Task RepairGeoIpStatistics_ResetsOnlyCountryStatsAndPreservesOtherData()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();

        // Install the exact embedded current payload first so the GeoIP script hash is known.
        var service = new EmbeddedServerPayloadService();
        var install = await service.InstallPinteModStableAsync(directory.Root);
        Assert.IsTrue(install.Success, install.Message);

        var stats = Path.Combine(directory.Root, "boiii", "scriptdata", "pintemod", "localization", "stats");
        Directory.CreateDirectory(stats);
        await File.WriteAllTextAsync(Path.Combine(stats, "countries.json"), new string('X', 2 * 1024 * 1024));
        await File.WriteAllTextAsync(Path.Combine(stats, "countries.json.tmp"), new string('Y', 2 * 1024 * 1024));
        await File.WriteAllTextAsync(Path.Combine(stats, "countries_summary.txt"), new string('Z', 2 * 1024 * 1024));

        var ranks = Path.Combine(directory.Root, "boiii", "scriptdata", "pintemod", "ranks_v2", "profiles", "keep.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ranks)!);
        await File.WriteAllTextAsync(ranks, "KEEP-RANKS");

        var result = await service.RepairGeoIpStatisticsAsync(directory.Root);

        Assert.IsTrue(result.Success, result.Message);
        Assert.AreEqual("KEEP-RANKS", await File.ReadAllTextAsync(ranks));
        Assert.IsFalse(File.Exists(Path.Combine(stats, "countries.json.tmp")));
        StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(stats, "countries.json")), "\"entries\":[]");
        Assert.AreEqual(0L, new FileInfo(Path.Combine(stats, "countries_summary.txt")).Length);
    }

    [TestMethod]
    public async Task HardenExistingManagerTooling_DisablesLegacyLiveConsoleConfig()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var service = new EmbeddedServerPayloadService();
        var install = await service.InstallPinteModStableAsync(directory.Root);
        Assert.IsTrue(install.Success, install.Message);

        var config = Path.Combine(directory.Root, "boiii", "tools", "PinteMod_Server_Launcher.local.json");
        await File.WriteAllTextAsync(
            config,
            "{\"server_launcher\":\"Server.bat\",\"launch_live_console\":true}");

        var result = await service.HardenExistingManagerToolingAsync(directory.Root);

        Assert.IsTrue(result.Success, result.Message);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(config));
        Assert.IsFalse(document.RootElement.GetProperty("launch_live_console").GetBoolean());
    }

    [TestMethod]
    public async Task HardenExistingManagerTooling_RefusesUnknownFirstPartyLauncher()
    {
        using var directory = new TemporaryServerDirectory();
        directory.CreateBoiii();
        var service = new EmbeddedServerPayloadService();
        var install = await service.InstallPinteModStableAsync(directory.Root);
        Assert.IsTrue(install.Success, install.Message);

        var launcher = Path.Combine(directory.Root, "boiii", "tools", "PinteMod_Server_Launcher.ps1");
        await File.WriteAllTextAsync(launcher, "UNKNOWN-OPERATOR-MODIFICATION");

        var result = await service.HardenExistingManagerToolingAsync(directory.Root);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("UNKNOWN-OPERATOR-MODIFICATION", await File.ReadAllTextAsync(launcher));
    }

    private sealed class TemporaryServerDirectory : IDisposable
    {
        public TemporaryServerDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.PayloadTests", Guid.NewGuid().ToString("N"));
        }

        public string Root { get; }
        public string CustomScripts => Path.Combine(Root, "boiii", "custom_scripts");
        public void CreateBoiii() => Directory.CreateDirectory(CustomScripts);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Fichier de référence PinteMod introuvable pour le test.");
    }

    private static async Task WriteLegacyVerifierAsync(string verifier)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(verifier)!);
        using var archive = ZipFile.OpenRead(FindRepositoryFile("reference", "PinteMod_v2.1.1.zip"));
        var legacy = archive.GetEntry("boiii/tools/Verify_PinteMod_Installation.ps1");
        Assert.IsNotNull(legacy);
        await using var source = legacy.Open();
        await using var destination = new FileStream(verifier, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination);
    }
}
