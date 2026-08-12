using System.ComponentModel;
using System.IO;
using System.Windows;
using PinteMod.ControlCenter.Configuration;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Infrastructure.Rcon;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.Security;
using PinteMod.ControlCenter.Services;
using PinteMod.ControlCenter.State;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter;

public partial class App : Application
{
    private readonly CancellationTokenSource _applicationLifetime = new();
    private readonly List<IDisposable> _disposables = [];
    private readonly OperatorRconOperationCoordinator _rconOperations = new();
    private Task? _monitorTask;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;
    private bool _resourcesDisposed;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var configurationStore = new JsonOperatorConfigurationStore();
            var savedConfiguration = await configurationStore.LoadAsync(_applicationLifetime.Token);
            var parsedStartup = ApplicationStartupOptions.Parse(e.Args);
            var hasExplicitDataSelection = e.Args.Any(argument =>
                argument.StartsWith("--data-mode=", StringComparison.OrdinalIgnoreCase) ||
                argument.StartsWith("--server-root=", StringComparison.OrdinalIgnoreCase));
            var usingSavedDataSource = !hasExplicitDataSelection &&
                                       savedConfiguration.ActivateDataSourceOnStartup &&
                                       !string.IsNullOrWhiteSpace(savedConfiguration.ServerRoot);
            var startup = ApplicationStartupOptions.Resolve(e.Args, savedConfiguration);
            string? configurationNotice = null;
            var simulatedProvider = new SimulatedControlCenterDataProvider(startup.Scenario);
            IControlCenterDataProvider dataProvider = simulatedProvider;
            LocalPinteModOptions? localOptions = null;
            IControlCenterSnapshotMonitor? snapshotMonitor = null;

            try
            {
                if (startup.DataMode == ControlCenterDataMode.HybridLocal)
                {
                    localOptions = new LocalPinteModOptions(
                        startup.ServerRoot!,
                        usingSavedDataSource && savedConfiguration.DataLocation == OperatorDataLocation.Lan
                            ? LocalPinteModRootLayout.PinteModDataRoot
                            : LocalPinteModRootLayout.ServerRoot);
                    var clock = new SystemClock();
                    var sessionReader = new SessionManifestReader(localOptions, clock);
                    var heartbeatReader = new ServiceHeartbeatReader(localOptions, clock);
                    var pinteModHeartbeatReader = new PinteModHeartbeatReader(localOptions, clock);
                    var runtimeSnapshotReader = new ControlCenterRuntimeSnapshotReader(localOptions, clock);
                    var rankProfileReader = new RankProfileReader(localOptions, clock);
                    var roundRecordReader = new RoundRecordReader(localOptions, clock);
                    var easterEggRecordReader = new EasterEggRecordReader(localOptions, clock);
                    var installationReader = new InstallationVerificationReader(localOptions, clock);
                    var banStatusReader = new BanServiceStatusReader(localOptions, clock);
                    var metadataReader = new LocalPlayerMetadataReader(localOptions, clock);
                    var logReader = new StructuredLogReader(localOptions);
                    var communityPauseStatusReader = new CommunityPauseStatusReader(localOptions, clock);
                    var communityPauseLogReader = new CommunityPauseLogReader(localOptions, clock);
                    _disposables.Add(sessionReader);
                    _disposables.Add(heartbeatReader);
                    _disposables.Add(pinteModHeartbeatReader);
                    _disposables.Add(runtimeSnapshotReader);
                    _disposables.Add(rankProfileReader);
                    _disposables.Add(roundRecordReader);
                    _disposables.Add(easterEggRecordReader);
                    _disposables.Add(installationReader);
                    _disposables.Add(banStatusReader);
                    _disposables.Add(metadataReader);
                    _disposables.Add(logReader);
                    _disposables.Add(communityPauseStatusReader);
                    _disposables.Add(communityPauseLogReader);
                    var phase21Provider = new HybridControlCenterDataProvider(
                        simulatedProvider,
                        sessionReader,
                        heartbeatReader,
                        localOptions);
                    var phase22Provider = new RankRecordsOverlayDataProvider(
                        phase21Provider,
                        rankProfileReader,
                        roundRecordReader);
                    var phase23Provider = new EasterEggRecordsOverlayDataProvider(
                        phase22Provider,
                        easterEggRecordReader);
                    dataProvider = new BlockAControlCenterDataProvider(
                        phase23Provider,
                        sessionReader,
                        installationReader,
                        banStatusReader,
                        metadataReader,
                        logReader,
                        communityPauseStatusReader,
                        communityPauseLogReader);
                    dataProvider = new PinteModRuntimeOverlayDataProvider(
                        dataProvider,
                        pinteModHeartbeatReader,
                        runtimeSnapshotReader);
                }
            }
            catch (Exception exception) when (usingSavedDataSource &&
                                              exception is ArgumentException or DirectoryNotFoundException or
                                                  UnauthorizedAccessException or IOException or InvalidOperationException)
            {
                startup = parsedStartup;
                localOptions = null;
                dataProvider = simulatedProvider;
                configurationNotice = "La source enregistrée est inaccessible · démarrage sécurisé en simulation.";
            }

            var snapshotStore = new CachedControlCenterSnapshotStore(dataProvider);
            _disposables.Add(snapshotStore);
            if (startup.DataMode == ControlCenterDataMode.HybridLocal)
            {
                snapshotMonitor = new HybridLocalSnapshotMonitor(snapshotStore);
            }
            var simulationService = new SimulationActionService();
            var selectionState = new PlayerSelectionState();
            var rconSecretStore = new DpapiRconSecretStore();
            var operatorActivityStore = new InMemoryOperatorActivityStore();
            var mapCatalogService = new JsonMapCatalogService();
            var mapCatalogState = new MapCatalogState();
            var clipboardService = new WindowsTextClipboardService();
            IPlayerModerationHistoryReader? playerHistoryReader = localOptions is null
                ? null
                : new LocalPlayerModerationHistoryReader(localOptions, new SystemClock());
            var rconOperationGate = new RconOperationGate();
            var rconClient = new BoiiiUdpRconClient();
            var rconDiagnosticService = new RconDiagnosticService(
                rconClient,
                rconSecretStore,
                new SystemClock(),
                rconOperationGate);
            var communityPauseCommandService = new CommunityPauseCommandService(
                rconClient,
                rconSecretStore,
                new SystemClock(),
                rconOperationGate);
            var serverAdministrationCommandService = new ServerAdministrationCommandService(
                rconClient,
                rconSecretStore,
                new SystemClock(),
                rconOperationGate);
            var playerAdministrationCommandService = new PlayerAdministrationCommandService(
                rconClient,
                rconSecretStore,
                new SystemClock(),
                rconOperationGate);
            var confirmationService = new MessageBoxOperatorConfirmationService();
            var mutationSafety = new OperatorMutationSafetyState();
            var records = new RecordsViewModel(snapshotStore);
            var logs = new LogsViewModel(snapshotStore, operatorActivityStore, clipboardService);
            var settings = new SettingsViewModel(
                startup.DataMode,
                dataProvider.GetType().Name,
                localOptions?.ServerRoot,
                snapshotMonitor?.Interval,
                new LocalDataSourceProbe(),
                configurationStore,
                savedConfiguration,
                rconDiagnosticService,
                rconSecretStore,
                operatorActivityStore,
                _rconOperations,
                mapCatalogService,
                mapCatalogState,
                clipboardService);
            var dashboard = new DashboardViewModel(
                snapshotStore,
                simulationService,
                selectionState,
                playerAdministrationCommandService,
                confirmationService,
                settings.CreateRconEndpoint,
                operatorActivityStore,
                _rconOperations,
                mutationSafety,
                playerHistoryReader);
            var players = new PlayersViewModel(
                snapshotStore,
                simulationService,
                selectionState,
                playerAdministrationCommandService,
                confirmationService,
                settings.CreateRconEndpoint,
                operatorActivityStore,
                _rconOperations,
                mutationSafety,
                playerHistoryReader);
            var server = new ServerViewModel(
                snapshotStore,
                simulationService,
                communityPauseCommandService,
                confirmationService,
                settings.CreateRconEndpoint,
                operatorActivityStore,
                rconDiagnosticService,
                _rconOperations,
                serverAdministrationCommandService,
                mutationSafety,
                mapCatalogService,
                mapCatalogState,
                clipboardService);
            var shell = new ShellViewModel(
                snapshotStore,
                dashboard,
                players,
                server,
                records,
                logs,
                settings);

            var window = new MainWindow { DataContext = shell };
            MainWindow = window;
            window.Closing += OnMainWindowClosing;
            window.Show();

            try
            {
                await shell.InitializeAsync(_applicationLifetime.Token);
                if (configurationNotice is not null)
                {
                    shell.ReportConfigurationNotice(configurationNotice);
                }
                if (snapshotMonitor is not null)
                {
                    _monitorTask = RunMonitorAsync(snapshotMonitor, shell, window.Dispatcher, _applicationLifetime.Token);
                }
            }
            catch (Exception exception)
            {
                shell.ReportError(exception);
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
            _rconOperations.StopAcceptingNewOperations();
            _applicationLifetime.Cancel();
            try
            {
                _monitorTask?.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }

            try
            {
                _rconOperations.WaitForIdleAsync().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
            }

            DisposeResources();
        }

        _applicationLifetime.Dispose();
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
        _rconOperations.StopAcceptingNewOperations();
        _applicationLifetime.Cancel();
        try
        {
            if (_monitorTask is not null)
            {
                await _monitorTask;
            }

        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }

        await _rconOperations.WaitForIdleAsync();

        DisposeResources();
        _shutdownCompleted = true;
        if (sender is Window window)
        {
            window.Closing -= OnMainWindowClosing;
            window.Close();
        }
    }

    private void DisposeResources()
    {
        if (_resourcesDisposed)
        {
            return;
        }

        foreach (var disposable in _disposables.AsEnumerable().Reverse())
        {
            disposable.Dispose();
        }

        _resourcesDisposed = true;
    }

    private static async Task RunMonitorAsync(
        IControlCenterSnapshotMonitor monitor,
        ShellViewModel shell,
        System.Windows.Threading.Dispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        try
        {
            await monitor.RunAsync(
                async (_, token) =>
                {
                    await dispatcher.InvokeAsync(
                        () => shell.ApplyCurrentSnapshotAsync(token),
                        System.Windows.Threading.DispatcherPriority.Background,
                        token).Task.Unwrap();
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await dispatcher.InvokeAsync(() => shell.ReportError(exception));
        }
    }

}
