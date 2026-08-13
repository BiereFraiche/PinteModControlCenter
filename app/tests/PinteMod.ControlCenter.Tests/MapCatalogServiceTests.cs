using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.State;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class MapCatalogServiceTests
{
    [TestMethod]
    public async Task MissingCatalog_ReturnsTheFourteenOfficialMaps()
    {
        using var directory = new TemporaryCatalogDirectory();
        var snapshot = await new JsonMapCatalogService(directory.CatalogPath).GetSnapshotAsync();

        Assert.AreEqual(14, snapshot.Entries.Count);
        Assert.IsTrue(snapshot.Entries.All(entry => entry.IsOfficial));
        Assert.AreEqual("zm_zod", snapshot.Entries[0].Code);
        Assert.AreEqual("zm_asylum", snapshot.Entries[^1].Code);
    }

    [TestMethod]
    public async Task RotationImport_MergesOfficialAndCustomMapsWithoutDuplicates()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);

        var result = await service.ImportRotationLineAsync(
            "set sv_maprotation \"gametype zclassic map zm_tomb map zm_custom_one map zm_custom_one\"");
        var snapshot = await service.GetSnapshotAsync();

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.AffectedCount);
        Assert.AreEqual(15, snapshot.Entries.Count);
        Assert.IsTrue(snapshot.Entries.Single(entry => entry.Code == "zm_tomb").IsInServerRotation);
        var custom = snapshot.Entries.Single(entry => entry.Code == "zm_custom_one");
        Assert.IsFalse(custom.IsOfficial);
        Assert.IsTrue(custom.IsInServerRotation);
        Assert.AreEqual("zm_custom_one", custom.DisplayName);
    }

    [DataTestMethod]
    [DataRow("//set sv_maprotation \"gametype zclassic map zm_tomb\"")]
    [DataRow("set sv_maprotation \"gametype zclassic map zm_tomb;quit\"")]
    [DataRow("set sv_maprotation \"gametype zclassic exec server_zm.cfg\"")]
    [DataRow("set sv_maprotation \"gametype zclassic map zm_tomb\"\nset rcon_password secret")]
    [DataRow("map zm_tomb")]
    public async Task RotationImport_RejectsCommentsCommandsAndMultilineInput(string value)
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);

        var result = await service.ImportRotationLineAsync(value);
        var snapshot = await service.GetSnapshotAsync();

        Assert.IsFalse(result.Success);
        Assert.AreEqual(14, snapshot.Entries.Count);
        Assert.IsFalse(File.Exists(directory.CatalogPath));
        Assert.IsFalse(result.Message.Contains(value, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ManualMap_IsPersistedAndRemovalOnlyChangesLocalCatalog()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);

        var added = await service.AddManualMapAsync("ZM_MY_CUSTOM", "Ma carte custom");
        var reloaded = new JsonMapCatalogService(directory.CatalogPath);
        var persisted = await reloaded.GetSnapshotAsync();
        var removed = await reloaded.RemoveManualMapAsync("zm_my_custom");
        var afterRemoval = await reloaded.GetSnapshotAsync();

        Assert.IsTrue(added.Success);
        Assert.IsTrue(removed.Success);
        var custom = persisted.Entries.Single(entry => entry.Code == "zm_my_custom");
        Assert.AreEqual("Ma carte custom", custom.DisplayName);
        Assert.IsTrue(custom.IsManual);
        Assert.IsFalse(afterRemoval.Entries.Any(entry => entry.Code == "zm_my_custom"));
        Assert.IsTrue(File.Exists(directory.CatalogPath));
        Assert.IsFalse(File.Exists(directory.CatalogPath + ".tmp"));
    }

    [TestMethod]
    public async Task RemovingManualFlag_DoesNotHideMapStillPresentInRotation()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);
        await service.ImportRotationLineAsync(
            "set sv_maprotation \"gametype zclassic map zm_rotation_custom\"");
        await service.AddManualMapAsync("zm_rotation_custom", "Rotation Custom");

        var removed = await service.RemoveManualMapAsync("zm_rotation_custom");
        var entry = (await service.GetSnapshotAsync()).Entries.Single(item => item.Code == "zm_rotation_custom");

        Assert.IsTrue(removed.Success);
        Assert.IsFalse(entry.IsManual);
        Assert.IsTrue(entry.IsInServerRotation);
    }

    [TestMethod]
    public async Task NewRotation_ReplacesPreviousRotationWithoutKeepingStaleCustomMaps()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);
        await service.ImportRotationLineAsync(
            "set sv_maprotation \"gametype zclassic map zm_first_custom\"");

        var result = await service.ImportRotationLineAsync(
            "set sv_maprotation \"gametype zclassic map zm_tomb\"");
        var snapshot = await service.GetSnapshotAsync();

        Assert.IsTrue(result.Success);
        Assert.IsFalse(snapshot.Entries.Any(entry => entry.Code == "zm_first_custom"));
        Assert.IsTrue(snapshot.Entries.Single(entry => entry.Code == "zm_tomb").IsInServerRotation);
    }

    [DataTestMethod]
    [DataRow("../zone/server_zm.cfg")]
    [DataRow("zm_custom;quit")]
    [DataRow("zm custom")]
    [DataRow("zm_custom\\payload")]
    public async Task ManualMap_RejectsUnsafeCodesWithoutCreatingCatalog(string code)
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);

        var result = await service.AddManualMapAsync(code, "Carte refusée");

        Assert.IsFalse(result.Success);
        Assert.IsFalse(File.Exists(directory.CatalogPath));
        Assert.AreEqual(14, (await service.GetSnapshotAsync()).Entries.Count);
    }

    [TestMethod]
    public async Task UnknownCurrentMap_IsObservedAndBecomesPersistent()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);

        var result = await service.ObserveMapAsync("zm_live_custom", "Custom actuellement jouée");
        var reloaded = await new JsonMapCatalogService(directory.CatalogPath).GetSnapshotAsync();

        Assert.IsTrue(result.Success);
        var observed = reloaded.Entries.Single(entry => entry.Code == "zm_live_custom");
        Assert.IsTrue(observed.IsObserved);
        Assert.AreEqual("Custom actuellement jouée", observed.DisplayName);
    }

    [TestMethod]
    public async Task InvalidOrTemporaryCatalog_IsIgnoredWithoutLosingOfficialFallback()
    {
        using var directory = new TemporaryCatalogDirectory();
        Directory.CreateDirectory(Path.GetDirectoryName(directory.CatalogPath)!);
        await File.WriteAllTextAsync(directory.CatalogPath, "{");
        await File.WriteAllTextAsync(directory.CatalogPath + ".tmp", "secret material that must stay inactive");

        var snapshot = await new JsonMapCatalogService(directory.CatalogPath).GetSnapshotAsync();

        Assert.AreEqual(14, snapshot.Entries.Count);
        Assert.IsTrue(snapshot.Entries.All(entry => entry.IsOfficial));
    }

    [TestMethod]
    public async Task SettingsCommands_ImportAddAndRemoveWithoutAnyRconDependency()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);
        var viewModel = new SettingsViewModel(mapCatalogService: service);
        await viewModel.InitializeAsync();
        viewModel.MapRotationLine = "set sv_maprotation \"gametype zclassic map zm_tomb map zm_cfg_custom\"";

        viewModel.ImportMapRotationCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ImportMapRotationCommand);
        viewModel.ManualMapCode = "zm_manual_custom";
        viewModel.ManualMapName = "Custom manuelle";
        viewModel.AddManualMapCommand.Execute(null);
        await WaitForCommandAsync(viewModel.AddManualMapCommand);

        Assert.AreEqual("CARTE AJOUTÉE", viewModel.MapCatalogStatus);
        Assert.IsTrue(viewModel.MapCatalogEntries.Any(entry => entry.Code == "zm_cfg_custom"));
        var manual = viewModel.MapCatalogEntries.Single(entry => entry.Code == "zm_manual_custom");
        Assert.IsTrue(manual.IsManual);
        viewModel.SelectedMapCatalogEntry = manual;
        viewModel.RemoveManualMapCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RemoveManualMapCommand);

        Assert.AreEqual("CARTE RETIRÉE", viewModel.MapCatalogStatus);
        Assert.IsFalse(viewModel.MapCatalogEntries.Any(entry => entry.Code == "zm_manual_custom"));
    }

    [TestMethod]
    public async Task ServerViewModel_AddsObservedCustomMapAndKeepsItSelected()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        snapshot = snapshot with
        {
            Server = snapshot.Server with
            {
                MapCode = "zm_runtime_custom",
                MapName = "Runtime Custom"
            }
        };
        var viewModel = new ServerViewModel(
            new FixedSnapshotStore(snapshot),
            new SimulationActionService(),
            mapCatalogService: service);

        await viewModel.InitializeAsync();

        Assert.AreEqual("zm_runtime_custom", viewModel.SelectedMap?.Key);
        StringAssert.Contains(viewModel.SelectedMap!.Label, "CUSTOM");
        Assert.AreEqual(15, viewModel.MapOptions.Count);
    }

    [TestMethod]
    public async Task UnchangedMapCatalog_DoesNotRebuildOpenMapMenu()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var viewModel = new ServerViewModel(
            new FixedSnapshotStore(snapshot),
            new SimulationActionService(),
            mapCatalogService: service);
        await viewModel.InitializeAsync();
        var firstOption = viewModel.MapOptions[0];
        var collectionChanges = 0;
        viewModel.MapOptions.CollectionChanged += (_, _) => collectionChanges++;

        await viewModel.InitializeAsync();

        Assert.AreEqual(0, collectionChanges);
        Assert.AreSame(firstOption, viewModel.MapOptions[0]);
    }

    [TestMethod]
    public async Task UnchangedSettingsCatalog_DoesNotRebuildOpenCatalogMenu()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);
        var viewModel = new SettingsViewModel(mapCatalogService: service);
        await viewModel.InitializeAsync();
        var firstEntry = viewModel.MapCatalogEntries[0];
        var collectionChanges = 0;
        viewModel.MapCatalogEntries.CollectionChanged += (_, _) => collectionChanges++;

        await viewModel.InitializeAsync();

        Assert.AreEqual(0, collectionChanges);
        Assert.AreSame(firstEntry, viewModel.MapCatalogEntries[0]);
    }

    [TestMethod]
    public async Task SettingsChange_RefreshesServerMenuImmediatelyThroughSharedState()
    {
        using var directory = new TemporaryCatalogDirectory();
        var service = new JsonMapCatalogService(directory.CatalogPath);
        var state = new MapCatalogState();
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var server = new ServerViewModel(
            new FixedSnapshotStore(snapshot),
            new SimulationActionService(),
            mapCatalogService: service,
            mapCatalogState: state);
        var settings = new SettingsViewModel(mapCatalogService: service, mapCatalogState: state);
        await server.InitializeAsync();
        await settings.InitializeAsync();
        settings.ManualMapCode = "zm_shared_custom";
        settings.ManualMapName = "Custom partagée";

        settings.AddManualMapCommand.Execute(null);
        await WaitForCommandAsync(settings.AddManualMapCommand);

        Assert.IsTrue(server.MapOptions.Any(option => option.Key == "zm_shared_custom"));
    }

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (command.IsExecuting && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(command.IsExecuting);
    }

    private sealed class FixedSnapshotStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current => snapshot;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class TemporaryCatalogDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "PinteMod.ControlCenter.MapCatalogTests",
            Guid.NewGuid().ToString("N"));

        public string CatalogPath => Path.Combine(_root, "map-catalog.json");

        public void Dispose()
        {
            var resolvedRoot = Path.GetFullPath(_root);
            var expectedRoot = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.MapCatalogTests");
            if (Directory.Exists(resolvedRoot) &&
                resolvedRoot.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(resolvedRoot, recursive: true);
            }
        }
    }
}
