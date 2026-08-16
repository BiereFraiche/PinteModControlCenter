using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.State;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ControlCenterWorkspaceViewModelTests
{
    [TestMethod]
    public async Task SelectingTabs_KeepsEachServerNavigationStateIsolated()
    {
        var first = CreateTab("primary", "Serveur principal");
        var second = CreateTab("srv-second", "Serveur secondaire");
        var selectedProfile = string.Empty;
        var workspace = new ControlCenterWorkspaceViewModel(
            [first, second],
            first.ProfileId,
            () => Task.FromResult(CreateTab("srv-third", "Serveur 3")),
            _ => Task.FromResult(true),
            profileId =>
            {
                selectedProfile = profileId;
                return Task.CompletedTask;
            });
        first.Shell.NavigateTo("Logs");

        workspace.SelectServerCommand.Execute(second);
        await WaitForCommandAsync(workspace.SelectServerCommand);

        Assert.AreSame(second, workspace.ActiveServer);
        Assert.AreEqual("srv-second", selectedProfile);
        Assert.AreEqual("Logs", first.Shell.CurrentPage.Title);
        Assert.AreEqual("Dashboard", second.Shell.CurrentPage.Title);
        Assert.AreEqual(1, workspace.Servers.Count(server => server.IsActive));
    }

    [TestMethod]
    public async Task AddAndRemoveServer_UpdatesTabsWithoutSharingShells()
    {
        var first = CreateTab("primary", "Serveur principal");
        var added = CreateTab("srv-second", "Serveur 2");
        var removed = new List<string>();
        var workspace = new ControlCenterWorkspaceViewModel(
            [first],
            first.ProfileId,
            () => Task.FromResult(added),
            server =>
            {
                removed.Add(server.ProfileId);
                return Task.FromResult(true);
            },
            _ => Task.CompletedTask);

        workspace.AddServerCommand.Execute(null);
        await WaitForCommandAsync(workspace.AddServerCommand);
        Assert.AreEqual(2, workspace.Servers.Count);
        Assert.AreSame(added, workspace.ActiveServer);
        Assert.AreNotSame(first.Shell, added.Shell);

        workspace.RemoveServerCommand.Execute(added);
        await WaitForCommandAsync(workspace.RemoveServerCommand);
        Assert.AreEqual(1, workspace.Servers.Count);
        Assert.AreSame(first, workspace.ActiveServer);
        CollectionAssert.AreEqual(new[] { "srv-second" }, removed);
    }

    [TestMethod]
    public void RenamingActiveTab_UpdatesWindowTitleWithoutChangingOtherTabs()
    {
        var first = CreateTab("primary", "Serveur principal");
        var second = CreateTab("srv-second", "Serveur secondaire");
        var workspace = new ControlCenterWorkspaceViewModel(
            [first, second],
            first.ProfileId,
            () => Task.FromResult(CreateTab("srv-third", "Serveur 3")),
            _ => Task.FromResult(true),
            _ => Task.CompletedTask);

        first.DisplayName = "Salon Zombies";

        StringAssert.Contains(workspace.WindowTitle, "Salon Zombies");
        Assert.AreEqual("Serveur secondaire", second.DisplayName);
    }

    [TestMethod]
    public void EachTabKeepsAnIndependentValidatedAccentColor()
    {
        var first = CreateTab("primary", "Serveur principal");
        var second = CreateTab("srv-second", "Serveur secondaire");
        first.AccentColorKey = "violet";
        second.AccentColorKey = "cyan";

        Assert.AreEqual("violet", first.AccentColorKey);
        Assert.AreEqual("cyan", second.AccentColorKey);
        Assert.AreNotEqual(first.AccentPreviewBrush, second.AccentPreviewBrush);

        second.AccentColorKey = "invalid";
        Assert.AreEqual(OperatorAccentTheme.DefaultKey, second.AccentColorKey);
    }

    private static ServerTabViewModel CreateTab(string profileId, string displayName)
    {
        var store = new CachedControlCenterSnapshotStore(
            new SimulatedControlCenterDataProvider(SimulationScenario.Healthy));
        var simulation = new SimulationActionService();
        var selection = new PlayerSelectionState();
        var shell = new ShellViewModel(
            store,
            new DashboardViewModel(store, simulation, selection),
            new PlayersViewModel(store, simulation, selection),
            new ServerViewModel(store, simulation),
            new RecordsViewModel(store),
            new LogsViewModel(store),
            new SettingsViewModel(),
            startClock: false);
        return new ServerTabViewModel(profileId, displayName, shell);
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

    private static async Task WaitForCommandAsync<T>(AsyncRelayCommand<T> command)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (command.IsExecuting && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(command.IsExecuting);
    }
}
