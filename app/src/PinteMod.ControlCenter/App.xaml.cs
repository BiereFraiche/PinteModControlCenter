using System.IO;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
        // The Control Center is often used through AnyDesk, RDP or a VM
        // console. Software rendering prevents a remote GPU/driver from
        // rejecting the first WPF window; this UI has no graphics workload.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        if (SelfTestStartupOptions.IsRequested(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            await RunSelfTestModeAsync(e.Args);
            return;
        }

        var startupPhase = "préparation locale";
        try
        {
            startupPhase = "vérification des mises à jour locales";
            var preferredUiUpdateIndex = Array.FindIndex(e.Args, argument => string.Equals(argument, "--preferred-ui-apply-update", StringComparison.OrdinalIgnoreCase));
            if (preferredUiUpdateIndex >= 0 && preferredUiUpdateIndex + 1 < e.Args.Length && int.TryParse(e.Args[preferredUiUpdateIndex + 1], out var previousPreferredUiPid))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _ = ApplyPreferredUiUpdateAsync(previousPreferredUiPid);
                return;
            }

            var managedUiUpdateIndex = Array.FindIndex(e.Args, argument => string.Equals(argument, "--managed-ui-apply-update", StringComparison.OrdinalIgnoreCase));
            if (managedUiUpdateIndex >= 0 && managedUiUpdateIndex + 1 < e.Args.Length && int.TryParse(e.Args[managedUiUpdateIndex + 1], out var previousUiPid))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _ = ApplyManagedUiUpdateAsync(previousUiPid);
                return;
            }

            var updateIndex = Array.FindIndex(e.Args, argument => string.Equals(argument, "--remote-agent-apply-update", StringComparison.OrdinalIgnoreCase));
            if (updateIndex >= 0 && updateIndex + 1 < e.Args.Length && int.TryParse(e.Args[updateIndex + 1], out var previousAgentPid))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _ = ApplyRemoteAgentUpdateAsync(previousAgentPid);
                return;
            }

            if (RemoteAgentStartupOptions.IsAgentRequested(e.Args))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                // Only the local installer may request this repair launch. A
                // stale update marker must never block an operator who has
                // explicitly restarted the Agent; scheduled recovery keeps its
                // normal update-marker protection because it uses no repair flag.
                if (!RemoteAgentStartupOptions.IsManualRepairRequested(e.Args) &&
                    RemoteAgentRecoveryTaskService.ShouldSuppressAgentStartForUpdate())
                {
                    Shutdown(0);
                    return;
                }
                _ = RunRemoteAgentAsync();
                return;
            }

            if (ManagedControlCenterInstallationService.ShouldApplyPendingOnStartup())
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                if (ManagedControlCenterInstallationService.StartPendingUpdate(Environment.ProcessId) is not null)
                {
                    Shutdown(0);
                    return;
                }
            }

            // Remember the exact user-facing EXE location on this machine. Remote
            // Agent updates will keep this same file synchronized instead of
            // imposing a Start-menu/Desktop installation. Internal Agent/fallback
            // executables are excluded by the registration service.
            PreferredControlCenterPathService.RegisterCurrentExecutable();
            ManagedControlCenterInstallationService.RemoveLegacyShortcuts();

            // The same executable is both the server/profile manager and the Control Center.
            // Keep shutdown explicit while the modal manager is the only window.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            startupPhase = "chargement du gestionnaire de serveurs";
            var managerViewModel = await ServerManagerViewModel.CreateAsync(_applicationLifetime.Token);
            startupPhase = "ouverture du gestionnaire de serveurs";
            var managerWindow = new ServerManagerWindow(managerViewModel);
            // A modal first window otherwise becomes WPF's MainWindow.  On
            // some remote sessions its close starts an implicit shutdown before
            // the actual dashboard can call Show().
            MainWindow = null;
            if (managerWindow.ShowDialog() != true)
            {
                Shutdown(0);
                return;
            }

            // Reassert the intended lifetime after the modal window closed.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var managerSelectedProfileId = managerViewModel.SelectedProfile?.ProfileId;

            startupPhase = "lecture des profils enregistrés";
            _workspaceStore = new JsonOperatorWorkspaceConfigurationStore();
            _workspaceConfiguration = await _workspaceStore.LoadAsync(_applicationLifetime.Token);
            var parsedStartup = ApplicationStartupOptions.Parse(e.Args);
            var hasExplicitDataSelection = e.Args.Any(argument =>
                argument.StartsWith("--data-mode=", StringComparison.OrdinalIgnoreCase) ||
                argument.StartsWith("--server-root=", StringComparison.OrdinalIgnoreCase));
            var tabs = new List<ServerTabViewModel>();
            var unavailableProfileCount = 0;
            var recoveryTabUsed = false;

            foreach (var profileId in _workspaceConfiguration.ProfileIds)
            {
                ServerRuntimeContext? context = null;
                try
                {
                    var configurationStore = CreateConfigurationStore(profileId);
                    var savedConfiguration = await configurationStore.LoadAsync(_applicationLifetime.Token);
                    var managedConfiguration = await new JsonManagedServerProfileStore(
                        OperatorProfileStoragePaths.GetManagedServerProfilePath(profileId))
                        .LoadAsync(_applicationLifetime.Token);
                    var receivesCommandLine = hasExplicitDataSelection &&
                                              string.Equals(
                                                  profileId,
                                                  _workspaceConfiguration.ActiveProfileId,
                                                  StringComparison.Ordinal);
                    var managerSelectedProfile = !receivesCommandLine &&
                                                 !string.IsNullOrWhiteSpace(managerSelectedProfileId) &&
                                                 string.Equals(profileId, managerSelectedProfileId, StringComparison.Ordinal);
                    var usingSavedDataSource = !receivesCommandLine &&
                                               savedConfiguration.ActivateDataSourceOnStartup &&
                                               !string.IsNullOrWhiteSpace(savedConfiguration.ServerRoot);
                    var startup = receivesCommandLine
                        ? parsedStartup
                        : ApplicationStartupOptions.Resolve(
                            [],
                            savedConfiguration,
                            managerSelectedProfile && savedConfiguration.ActivateDataSourceOnStartup);
                    context = CreateServerContext(
                        profileId,
                        savedConfiguration,
                        configurationStore,
                        startup,
                        usingSavedDataSource,
                        managedConfiguration.RemoteAgentId);
                    var tab = CreateTab(context, savedConfiguration.ProfileDisplayName);
                    _serverContexts.Add(profileId, context);
                    tabs.Add(tab);
                    context = null;
                }
                catch (Exception)
                {
                    // A saved profile can contain an obsolete local integration.
                    // Never make that profile prevent access to the other tabs.
                    context?.Dispose();
                    unavailableProfileCount++;
                }
            }

            if (tabs.Count == 0)
            {
                // The recovery tab is deliberately not persisted: it gives the
                // operator access to the Manager without overwriting profiles,
                // local paths, RCON configuration or DPAPI secrets.
                const string recoveryProfileId = "recovery";
                var recoveryStore = CreateConfigurationStore(recoveryProfileId);
                var recoveryConfiguration = OperatorConfiguration.Default with
                {
                    ProfileDisplayName = "Récupération locale"
                };
                var recoveryContext = CreateServerContext(
                    recoveryProfileId,
                    recoveryConfiguration,
                    recoveryStore,
                    ApplicationStartupOptions.Parse([]),
                    usingSavedDataSource: false);
                _serverContexts.Add(recoveryProfileId, recoveryContext);
                tabs.Add(CreateTab(recoveryContext, recoveryConfiguration.ProfileDisplayName));
                recoveryTabUsed = true;
            }

            startupPhase = "création du tableau de bord";
            _workspace = new ControlCenterWorkspaceViewModel(
                tabs,
                _workspaceConfiguration.ActiveProfileId,
                AddServerAsync,
                RemoveServerAsync,
                SetActiveServerAsync,
                _workspaceConfiguration.AdvancedMode,
                SetDisplayModeAsync,
                _workspaceConfiguration.UiLanguageCode,
                SetUiLanguageAsync);
            startupPhase = "chargement de l’interface";
            MainWindow? window = null;
            await Dispatcher.InvokeAsync(() =>
            {
                startupPhase = "application du thème";
                AccentThemeService.Apply(_workspace.ActiveServer.AccentColorKey);
                startupPhase = "création de la fenêtre principale";
                window = new MainWindow { DataContext = _workspace };
                startupPhase = "branchement de la fermeture";
                window.Closing += OnMainWindowClosing;
                startupPhase = "affichage de la fenêtre principale";
                window.Show();
                startupPhase = "enregistrement de la fenêtre principale";
                MainWindow = window;
            });
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            if (unavailableProfileCount > 0 || recoveryTabUsed)
            {
                PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                    recoveryTabUsed
                        ? "Les profils enregistrés n’ont pas pu être chargés. Le Control Center reste ouvert en mode récupération ; ouvrez le Gestionnaire pour les analyser ou les corriger."
                        : unavailableProfileCount == 1
                        ? "Un ancien profil n’a pas pu être chargé. Les autres profils restent disponibles ; ouvrez le Gestionnaire pour l’analyser ou le corriger."
                        : $"{unavailableProfileCount} anciens profils n’ont pas pu être chargés. Le Control Center reste ouvert en mode récupération ; ouvrez le Gestionnaire pour les analyser ou les corriger.",
                    "PinteMod Control Center",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            if (managerViewModel.KeepManagerOpenAfterControlCenter)
            {
                var companionManager = new ServerManagerWindow(managerViewModel, companionWindow: true);
                companionManager.Show();
                window?.Activate();
            }

            startupPhase = "démarrage des services locaux";
            foreach (var context in _serverContexts.Values)
            {
                try
                {
                    await context.StartAsync(window?.Dispatcher ?? Dispatcher);
                }
                catch (Exception exception)
                {
                    context.Shell.ReportError(exception);
                }
            }
        }
        catch (Exception exception)
        {
            var diagnostic = StartupFailureDiagnostic.Describe(exception);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                $"Le Control Center n’a pas pu être initialisé pendant : {startupPhase}.\n\nDiagnostic filtré : {diagnostic}\n\nLes chemins privés et les secrets sont masqués.",
                "PinteMod Control Center",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private async Task SetUiLanguageAsync(string languageCode)
    {
        var normalized = string.Equals(languageCode, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "fr-FR";
        var culture = CultureInfo.GetCultureInfo(normalized);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        _workspaceConfiguration = _workspaceConfiguration with { UiLanguageCode = normalized };
        if (_workspaceStore is not null)
        {
            await _workspaceStore.SaveAsync(_workspaceConfiguration, _applicationLifetime.Token);
        }
    }

    private async Task RunSelfTestModeAsync(IReadOnlyList<string> arguments)
    {
        var reportPath = SelfTestStartupOptions.DefaultReportPath;
        try
        {
            var options = SelfTestStartupOptions.Parse(arguments);
            reportPath = options.ReportPath;
            var report = await new ControlCenterSelfTestService()
                .RunAsync(_applicationLifetime.Token);
            SelfTestReportFileWriter.Write(reportPath, report.ToDisplayText());
            Shutdown(report.Success ? 0 : 8);
        }
        catch
        {
            try
            {
                SelfTestReportFileWriter.Write(
                    reportPath,
                    ControlCenterSelfTestReport.CreateStartupFailure().ToDisplayText());
            }
            catch
            {
            }

            Shutdown(8);
        }
    }


    private async Task ApplyRemoteAgentUpdateAsync(int previousAgentPid)
    {
        try
        {
            var source = Environment.ProcessPath;
            var target = RemoteAgentConfigurationStore.GetExecutablePath();
            var pending = RemoteAgentConfigurationStore.GetPendingUpdatePath();
            if (string.IsNullOrWhiteSpace(source) ||
                !string.Equals(Path.GetFullPath(source), Path.GetFullPath(pending), StringComparison.OrdinalIgnoreCase))
            {
                RemoteAgentRecoveryTaskService.ClearUpdateInProgress();
                Shutdown(-3);
                return;
            }

            try
            {
                using var previous = System.Diagnostics.Process.GetProcessById(previousAgentPid);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                await previous.WaitForExitAsync(timeout.Token);
            }
            catch (ArgumentException)
            {
                // Previous Agent already exited.
            }
            catch (OperationCanceledException)
            {
                RemoteAgentRecoveryTaskService.ClearUpdateInProgress();
                Shutdown(-4);
                return;
            }

            Directory.CreateDirectory(RemoteAgentConfigurationStore.GetAgentHome());
            var replaced = false;
            for (var attempt = 1; attempt <= 10 && !replaced; attempt++)
            {
                try
                {
                    File.Copy(source, target, overwrite: true);
                    replaced = true;
                }
                catch (IOException)
                {
                    if (attempt < 10) await Task.Delay(250 * attempt);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt < 10) await Task.Delay(250 * attempt);
                }
            }

            try
            {
                if (File.Exists(RemoteAgentConfigurationStore.GetStopRequestPath()))
                {
                    File.Delete(RemoteAgentConfigurationStore.GetStopRequestPath());
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }

            if (!replaced)
            {
                RemoteAgentRecoveryTaskService.ClearUpdateInProgress();
                if (File.Exists(target))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = target,
                        Arguments = "--remote-agent",
                        WorkingDirectory = RemoteAgentConfigurationStore.GetAgentHome(),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    });
                }
                Shutdown(-5);
                return;
            }

            PreferredControlCenterPathService.DiscoverRunningUserInterface();
            await PreferredControlCenterPathService.SynchronizePreferredExecutableAsync(source, _applicationLifetime.Token);
            await ManagedControlCenterInstallationService.InstallOrStageAsync(source, _applicationLifetime.Token);
            ManagedControlCenterInstallationService.RemoveLegacyShortcuts();

            RemoteAgentRecoveryTaskService.ClearUpdateInProgress();
            RemoteAgentRecoveryTaskService.EnsureInstalled(target, out _);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = target,
                Arguments = "--remote-agent",
                WorkingDirectory = RemoteAgentConfigurationStore.GetAgentHome(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            });
            Shutdown(0);
        }
        catch
        {
            RemoteAgentRecoveryTaskService.ClearUpdateInProgress();
            Shutdown(-5);
        }
    }


    private async Task ApplyPreferredUiUpdateAsync(int previousUiPid)
    {
        try
        {
            var success = await PreferredControlCenterPathService.ApplyPendingCurrentExecutableUpdateAsync(previousUiPid, _applicationLifetime.Token);
            Shutdown(success ? 0 : -7);
        }
        catch (OperationCanceledException)
        {
            Shutdown(-7);
        }
        catch
        {
            Shutdown(-7);
        }
    }

    private async Task ApplyManagedUiUpdateAsync(int previousUiPid)
    {
        try
        {
            var success = await ManagedControlCenterInstallationService.ApplyPendingAsync(previousUiPid, _applicationLifetime.Token);
            Shutdown(success ? 0 : -6);
        }
        catch (OperationCanceledException)
        {
            Shutdown(-6);
        }
        catch
        {
            Shutdown(-6);
        }
    }

    private async Task RunRemoteAgentAsync()
    {
        var host = new RemoteLaunchAgentHost();
        try
        {
            var exitCode = await host.RunAsync(_applicationLifetime.Token).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => Shutdown(exitCode));
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.InvokeAsync(() => Shutdown(0));
        }
        catch
        {
            await Dispatcher.InvokeAsync(() => Shutdown(-2));
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
        bool usingSavedDataSource,
        string remoteAgentId = "") =>
        ServerRuntimeContext.Create(
            profileId,
            savedConfiguration,
            configurationStore,
            startup,
            ApplicationStartupOptions.Parse([]),
            usingSavedDataSource,
            OperatorProfileStoragePaths.GetRconSecretPath(profileId),
            OperatorProfileStoragePaths.GetMapCatalogPath(profileId),
            _applicationLifetime.Token,
            () => LaunchManagedProfileAsync(profileId),
            () => StopManagedProfileAsync(profileId),
            remoteAgentId);

    private async Task<ServerLaunchResult> LaunchManagedProfileAsync(string profileId)
    {
        try
        {
            var manager = await ServerManagerViewModel.CreateAsync(_applicationLifetime.Token);
            var profile = manager.Profiles.FirstOrDefault(candidate =>
                string.Equals(candidate.ProfileId, profileId, StringComparison.Ordinal));
            if (profile is null)
            {
                return new ServerLaunchResult(false, "Profil serveur introuvable dans le Manager.");
            }

            manager.SelectedProfile = profile;
            return await manager.LaunchSelectedAsync(_applicationLifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return new ServerLaunchResult(false, "Lancement annulé.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ServerLaunchResult(false, exception.Message);
        }
    }

    private async Task<ServerLaunchResult> StopManagedProfileAsync(string profileId)
    {
        try
        {
            var manager = await ServerManagerViewModel.CreateAsync(_applicationLifetime.Token);
            var profile = manager.Profiles.FirstOrDefault(candidate =>
                string.Equals(candidate.ProfileId, profileId, StringComparison.Ordinal));
            if (profile is null)
            {
                return new ServerLaunchResult(false, "Profil serveur introuvable dans le Manager.");
            }

            manager.SelectedProfile = profile;
            return await manager.StopSelectedAsync(_applicationLifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return new ServerLaunchResult(false, "Arrêt annulé.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ServerLaunchResult(false, exception.Message);
        }
    }

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

            var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
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

    private async Task SetDisplayModeAsync(bool advancedMode)
    {
        await _profileOperations.WaitAsync(_applicationLifetime.Token);
        try
        {
            if (_shutdownStarted || _workspaceStore is null)
            {
                return;
            }

            var updated = _workspaceConfiguration with { AdvancedMode = advancedMode };
            await _workspaceStore.SaveAsync(updated, _applicationLifetime.Token);
            _workspaceConfiguration = updated;
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
