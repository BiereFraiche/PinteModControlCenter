using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class MultiServerPayloadHardeningTests
{
    [TestMethod]
    public void RecordHubDefinitions_AllowDormantProfilesWithSamePort_ButWorkersRefuseThem()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.RecordHubPortTests", Guid.NewGuid().ToString("N"));
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        Directory.CreateDirectory(Path.Combine(first, "boiii"));
        Directory.CreateDirectory(Path.Combine(second, "boiii"));
        File.WriteAllText(Path.Combine(first, "Server.bat"), "@echo off");
        File.WriteAllText(Path.Combine(second, "Server.bat"), "@echo off");
        try
        {
            IReadOnlyCollection<MultiServerLaunchDefinition> definitions =
            [
                new("first", "Premier", first, "Server.bat", 27017),
                new("second", "Second", second, "Server.bat", 27017)
            ];
            var normalize = typeof(MultiServerOrchestratorService).GetMethod(
                "Normalize",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(normalize);

            var hub = normalize.Invoke(null, [definitions, false, false]) as IReadOnlyList<MultiServerLaunchDefinition>;
            Assert.IsNotNull(hub);
            Assert.AreEqual(2, hub.Count);

            var exception = Assert.ThrowsException<TargetInvocationException>(() =>
                normalize.Invoke(null, [definitions, true, true]));
            Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidOperationException));
            StringAssert.Contains(exception.InnerException.Message, "Port serveur invalide ou dupliqué");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void RecordHubDefinitions_IgnoreDuplicatePhysicalRoots_ButWorkersRefuseThem()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.RecordHubRootTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "boiii"));
        File.WriteAllText(Path.Combine(root, "Server.bat"), "@echo off");
        try
        {
            IReadOnlyCollection<MultiServerLaunchDefinition> definitions =
            [
                new("first", "Premier", root, "Server.bat", 27017),
                new("legacy", "Ancien profil", root, "Server.bat", 27018)
            ];
            var normalize = typeof(MultiServerOrchestratorService).GetMethod(
                "Normalize",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(normalize);

            var hub = normalize.Invoke(null, [definitions, false, false]) as IReadOnlyList<MultiServerLaunchDefinition>;
            Assert.IsNotNull(hub);
            Assert.AreEqual(1, hub.Count);
            Assert.AreEqual("first", hub[0].ProfileId);

            var exception = Assert.ThrowsException<TargetInvocationException>(() =>
                normalize.Invoke(null, [definitions, true, true]));
            Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidOperationException));
            StringAssert.Contains(exception.InnerException.Message, "Racine BOIII locale dupliquée");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EmbeddedMultiServerPayload_ReconcilesLegacyWindowsWorkersAndHardensBanRuntime()
    {
        using var archive = OpenPayload(".Payloads.PinteMod_MULTI_20260819.zip");
        var control = ReadEntry(archive, "PinteMod_MultiServer_Control.ps1");
        var worker = ReadEntry(archive, "PinteMod_MultiServer_Worker.ps1");

        StringAssert.Contains(control, "Stop-LegacyStandaloneTool");
        StringAssert.Contains(control, "Stop-AllStandaloneConsoleProcesses");
        StringAssert.Contains(control, "Disable-LegacyStandaloneConsoleConfig");
        StringAssert.Contains(control, "$wantLive = $false # Manager 3K");
        StringAssert.Contains(control, "$wantRcon = $false # Manager 3K");
        StringAssert.Contains(control, "PinteMod_LiveConsole.ps1");
        StringAssert.Contains(control, "PinteMod_Remote_RCON.ps1");
        StringAssert.Contains(control, "Stop-ConflictingWorkersForRoot");
        StringAssert.Contains(control, "PinteMod_Server_Launcher.ps1");
        StringAssert.Contains(worker, "HardenBanAtomicWrite");
        StringAssert.Contains(worker, ".tmp.{1}.{2}");
    }

    [TestMethod]
    public void EmbeddedCurrentPayload_BanServiceUsesUniqueAtomicTempFiles()
    {
        using var archive = OpenPayload(".Payloads.PinteMod_CURRENT_20260819.zip");
        var ban = ReadEntry(archive, "boiii/tools/PinteMod_Ban_Service.ps1");

        StringAssert.Contains(ban, ".tmp.{1}.{2}");
        StringAssert.Contains(ban, "[System.IO.File]::Replace");
        StringAssert.Contains(ban, "2.1.1-atomicfix1");

        var launcherExample = ReadEntry(archive, "boiii/tools/PinteMod_Server_Launcher.example.json");
        StringAssert.Contains(launcherExample, "\"launch_live_console\": false");
        var launcher = ReadEntry(archive, "boiii/tools/PinteMod_Server_Launcher.ps1");
        StringAssert.Contains(launcher, "launch_live_console = $false");
        StringAssert.Contains(launcher, "-NotePropertyName launch_live_console -NotePropertyValue $false");
    }

    private static ZipArchive OpenPayload(string suffix)
    {
        var assembly = typeof(MultiServerOrchestratorService).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        var stream = assembly.GetManifestResourceStream(resource);
        Assert.IsNotNull(stream, resource);
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    private static string ReadEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        Assert.IsNotNull(entry, path);
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    [TestMethod]
    public void MultiServerPayload_DisablesStandaloneLiveAndRconAtEngineLevel()
    {
        using var archive = OpenPayload(".Payloads.PinteMod_MULTI_20260819.zip");
        var single = ReadEntry(archive, "PinteMod_Launch_SingleInstance.ps1");
        var control = ReadEntry(archive, "PinteMod_MultiServer_Control.ps1");
        var example = ReadEntry(archive, "PinteMod_MultiServer.example.json");

        StringAssert.Contains(single, "$entry.live_console = $false");
        StringAssert.Contains(single, "$entry.rcon = $false");
        StringAssert.Contains(control, "$wantLive = $false");
        StringAssert.Contains(control, "$wantRcon = $false");
        StringAssert.Contains(control, "PinteMod_Server_Launcher.ps1");
        StringAssert.Contains(control, "PinteMod_Launch_SingleInstance.ps1");
        Assert.IsFalse(example.Contains("\"live_console\": true", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(example.Contains("\"rcon\": true", StringComparison.OrdinalIgnoreCase));
    }
}
