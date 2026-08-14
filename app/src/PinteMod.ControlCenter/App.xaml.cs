using System.ComponentModel;
using System.Windows;
using PinteMod.ControlCenter.Composition;
using PinteMod.ControlCenter.Configuration;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Services;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter;

public partial class App : Application
{
    private readonly CancellationTokenSource _applicationLifetime = new();
    private readonly Dictionary<string, ServerRuntimeContext> _serverContexts = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _profileOperations = new(1, 1);
    private IOperatorWorkspaceConfigurationStore? _workspaceStore;
    private OperatorWorkspaceConfiguration _workspaceConfiguration = OperatorWorkspaceConfiguration.Default;
    private ControlCenterWorkspaceViewModel? _workspace;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;
    private bool _resourcesDisposed;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _workspaceStore = new JsonOperatorWorkspaceConfigurationStore();
            _workspaceConfiguration = await _workspaceStore.LoadAsync(_applicationLifetime.Token);
            var parsedStartup = ApplicationStartupOptions.Parse(e.Args);
            var hasExplicitDataSelection = e.Args.Any(argument =>
                argument.StartsWith("--data-mode=", StringComparison.OrdinalIgnoreCase) ||
                argument.StartsWith("--server-root=", StringComparison.OrdinalIgnoreCase));
            var tabs = new List<ServerTabViewModel>();

            foreach (var profileId in _workspaceConfiguration.ProfileIds)
            {
                var configurationStore = CreateConfigurationStore(profileId);
                var savedConfiguration = await configurationStore.LoadAsync(_applicationLifetime.Token);
                var receivesCommandLine = hasExplicitDataSelection &&
                                          string.Equals(
                                              profileId,
                                              _workspaceConfiguration.ActiveProfileId,
                                              StringComparison.Ordinal);
                var usingSavedDataSource = !receivesCommandLine &&
                                           savedConfiguration.ActivateDataSourceOnStartup &&
                                           !string.IsNullOrWhiteSpace(savedConfiguration.ServerRoot);
                var startup = receivesCommandLine
                    ? parsedStartup
                    : ApplicationStartupOptions.Resolve([], savedConfiguration);
                var context = CreateServerContext(
                    profileId,
                    savedConfiguration,
                    configurationStore,
                    startup,
                    usingSavedDataSource);
                var tab = CreateTab(context, savedConfiguration.ProfileDisplayName);
                _serverContexts.Add(profileId, context);
                tabs.Add(tab);
            }

            _workspace = new ControlCenterWorkspaceViewModel(
                tabs,
                _workspaceConfiguration.ActiveProfileId,
                AddServerAsync,
                RemoveServerAsync,
                SetActiveServerAsync);
            AccentThemeService.Apply(_workspace.ActiveServer.AccentColorKey);
            var window = new MainWindow { DataContext = _workspace };
            MainWindow = window;
            window.Closing += OnMainWindowClosing;
            window.Show();

            foreach (var context in _serverContexts.Values)
            {
                try
                {
                    await context.StartAsync(window.Dispatcher);
                }
                catch (Exception exception)
                {
                    context.Shell.ReportError(exception);
                }
            }
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Le Control Center n’a pas pu être initialisé. Vérifiez les paramètres de lancement et les sources locales.",
                "PinteMod Control Center",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_shutdownCompleted)
        {
            _shutdownStarted = true;
            StopAllContexts();
            try
            {
                Task.WhenAll(_serverContexts.Values.Select(context => context.WaitForShutdownAsync()))
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception)
            {
            }

            DisposeResources();
        }

        _applicationLifetime.Dispose();
        _profileOperations.Dispose();
        base.OnExit(e);
    }

    private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        e.Cancel = true;
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        await _profileOperations.WaitAsync();
        _profileOperations.Release();
        StopAllContexts();
        await Task.WhenAll(_serverContexts.Values.Select(context => context.WaitForShutdownAsync()));
        DisposeResources();
        _shutdownCompleted = true;
        if (sender is Window window)
        {
            window.Closing -= OnMainWindowClosing;
            window.Close();
        }
    }

    private ServerRuntimeContext CreateServerContext(
        string profileId,
        OperatorConfiguration savedConfiguration,
        IOperatorConfigurationStore configurationStore,
        ApplicationStartupOptions startup,
        bool usingSavedDataSource) =>
        ServerRuntimeContext.Create(
            profileId,
            savedConfiguration,
            configurationStore,
            startup,
            ApplicationStartupOptions.Parse([]),
            usingSavedDataSource,
            OperatorProfileStoragePaths.GetRconSecretPath(profileId),
            OperatorProfileStoragePaths.GetMapCatalogPath(profileId),
            _applicationLifetime.Token);

    private static ServerTabViewModel CreateTab(
        ServerRuntimeContext context,
        string displayName)
    {
        var tab = new ServerTabViewModel(
            context.ProfileId,
            displayName,
            context.Shell,
            context.Settings.SelectedAccentTheme.Key);
        context.Settings.ProfileDisplayNameSaved += name => tab.DisplayName = name;
        context.Settings.AccentThemeChanged += key =>
        {
            tab.AccentColorKey = key;
            if (tab.IsActive)
            {
                AccentThemeService.Apply(key);
            }
        };
        return tab;
    }

    private async Task<ServerTabViewModel> AddServerAsync()
    {
        await _profileOperations.WaitAsync(_applicationLifetime.Token);
        try
        {
            if (_shutdownStarted || _workspaceStore is null || MainWindow is null)
            {
                throw new InvalidOperationException("Le gestionnaire multi-serveurs n’est pas disponible.");
            }

            var profileId = CreateProfileId();
            var displayName = $"Serveur {_workspaceConfiguration.ProfileIds.Count + 1}";
            var configuration = OperatorConfiguration.Default with
            {
                ProfileDisplayName = displayName
            };
            var configurationStore = CreateConfigurationStore(profileId);
            await configurationStore.SaveAsync(configuration, _applicationLifetime.Token);
            var context = CreateServerContext(
                profileId,
                configuration,
                configurationStore,
                ApplicationStartupOptions.Parse([]),
                usingSavedDataSource: false);
            try
            {
                await context.StartAsync(MainWindow.Dispatcher);
                var updated = _workspaceConfiguration with
                {
                    ProfileIds = [.. _workspaceConfiguration.ProfileIds, profileId],
                    ActiveProfileId = profileId
                };
                await _workspaceStore.SaveAsync(updated, _applicationLifetime.Token);
                _workspaceConfiguration = updated;
                _serverContexts.Add(profileId, context);
                return CreateTab(context, displayName);
            }
            catch
            {
                context.StopAcceptingNewOperations();
                context.Cancel();
                await context.WaitForShutdownAsync();
                context.Dispose();
                throw;
            }
        }
        finally
        {
            _profileOperations.Release();
        }
    }

    private async Task<bool> RemoveServerAsync(ServerTabViewModel server)
    {
        await _profileOperations.WaitAsync(_applicationLifetime.Token);
        try
        {
            if (_shutdownStarted ||
                _workspaceStore is null ||
                !_serverContexts.TryGetValue(server.ProfileId, out var context))
            {
                return false;
            }

            var answer = MessageBox.Show(
                $"Retirer l’onglet « {server.DisplayName} » ?\n\nLe serveur BOIII ne sera pas touché. La configuration locale et le secret protégé resteront conservés sur ce PC.",
                "Retirer un serveur",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return false;
            }

            var remainingIds = _workspaceConfiguration.ProfileIds
                .Where(profileId => !string.Equals(profileId, server.ProfileId, StringComparison.Ordinal))
                .ToArray();
            var activeProfileId = string.Equals(
                _workspaceConfiguration.ActiveProfileId,
                server.ProfileId,
                StringComparison.Ordinal)
                ? remainingIds[0]
                : _workspaceConfiguration.ActiveProfileId;
            var updated = _workspaceConfiguration with
            {
                ProfileIds = remainingIds,
                ActiveProfileId = activeProfileId
            };
            await _workspaceStore.SaveAsync(updated, _applicationLifetime.Token);
            _workspaceConfiguration = updated;
            context.StopAcceptingNewOperations();
            context.Cancel();
            await context.WaitForShutdownAsync();
            context.Dispose();
            _serverContexts.Remove(server.ProfileId);
            return true;
        }
        finally
        {
            _profileOperations.Release();
        }
    }

    private async Task SetActiveServerAsync(string profileId)
    {
        await _profileOperations.WaitAsync(_applicationLifetime.Token);
        try
        {
            var selectedTab = _workspace?.Servers.FirstOrDefault(server =>
                string.Equals(server.ProfileId, profileId, StringComparison.Ordinal));
            if (selectedTab is not null)
            {
                AccentThemeService.Apply(selectedTab.AccentColorKey);
            }

            if (_shutdownStarted ||
                _workspaceStore is null ||
                string.Equals(_workspaceConfiguration.ActiveProfileId, profileId, StringComparison.Ordinal))
            {
                return;
            }

            var updated = _workspaceConfiguration with { ActiveProfileId = profileId };
            await _workspaceStore.SaveAsync(updated, _applicationLifetime.Token);
            _workspaceConfiguration = updated;
        }
        finally
        {
            _profileOperations.Release();
        }
    }

    private static JsonOperatorConfigurationStore CreateConfigurationStore(string profileId) =>
        new(OperatorProfileStoragePaths.GetConfigurationPath(profileId));

    private string CreateProfileId()
    {
        string profileId;
        do
        {
            profileId = $"srv-{Guid.NewGuid():N}"[..16];
        }
        while (_serverContexts.ContainsKey(profileId));

        return profileId;
    }

    private void StopAllContexts()
    {
        foreach (var context in _serverContexts.Values)
        {
            context.StopAcceptingNewOperations();
        }

        _applicationLifetime.Cancel();
        foreach (var context in _serverContexts.Values)
        {
            context.Cancel();
        }
    }

    private void DisposeResources()
    {
        if (_resourcesDisposed)
        {
            return;
        }

        foreach (var context in _serverContexts.Values.Reverse())
        {
            context.Dispose();
        }

        _resourcesDisposed = true;
    }
}
