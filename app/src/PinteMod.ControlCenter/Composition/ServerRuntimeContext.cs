using System.IO;
using System.Windows.Threading;
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

namespace PinteMod.ControlCenter.Composition;

public sealed class ServerRuntimeContext : IDisposable
{
    private readonly CancellationTokenSource _lifetime;
    private readonly List<IDisposable> _disposables;
    private readonly IControlCenterSnapshotMonitor? _snapshotMonitor;
    private Task? _monitorTask;
    private bool _disposed;

    private ServerRuntimeContext(
        string profileId,
        ShellViewModel shell,
        SettingsViewModel settings,
        OperatorRconOperationCoordinator rconOperations,
        IControlCenterSnapshotMonitor? snapshotMonitor,
        List<IDisposable> disposables,
        CancellationToken applicationLifetime,
        string? configurationNotice)
    {
        ProfileId = profileId;
        Shell = shell;
        Settings = settings;
        RconOperations = rconOperations;
        _snapshotMonitor = snapshotMonitor;
        _disposables = disposables;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime);
        ConfigurationNotice = configurationNotice;
    }

    public string ProfileId { get; }

    public ShellViewModel Shell { get; }

    public SettingsViewModel Settings { get; }

    public OperatorRconOperationCoordinator RconOperations { get; }

    public string? ConfigurationNotice { get; }

    public static ServerRuntimeContext Create(
        string profileId,
        OperatorConfiguration savedConfiguration,
        IOperatorConfigurationStore configurationStore,
        ApplicationStartupOptions startup,
        ApplicationStartupOptions safeFallback,
        bool usingSavedDataSource,
        string rconSecretPath,
        string mapCatalogPath,
        CancellationToken applicationLifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(savedConfiguration);
        ArgumentNullException.ThrowIfNull(configurationStore);
        var disposables = new List<IDisposable>();
        var simulatedProvider = new SimulatedControlCenterDataProvider(startup.Scenario);
        IControlCenterDataProvider dataProvider = simulatedProvider;
        LocalPinteModOptions? localOptions = null;
        IControlCenterSnapshotMonitor? snapshotMonitor = null;
        string? configurationNotice = null;

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
                var contractReader = new ControlCenterContractReader(localOptions, clock);
                var rankProfileReader = new RankProfileReader(localOptions, clock);
                var roundRecordReader = new RoundRecordReader(localOptions, clock);
                var easterEggRecordReader = new EasterEggRecordReader(localOptions, clock);
                var installationReader = new InstallationVerificationReader(localOptions, clock);
                var banStatusReader = new BanServiceStatusReader(localOptions, clock);
                var metadataReader = new LocalPlayerMetadataReader(localOptions, clock);
                var logReader = new StructuredLogReader(localOptions);
                var communityPauseStatusReader = new CommunityPauseStatusReader(localOptions, clock);
                var communityPauseLogReader = new CommunityPauseLogReader(localOptions, clock);
                disposables.AddRange(
                [
                    sessionReader,
                    heartbeatReader,
                    pinteModHeartbeatReader,
                    runtimeSnapshotReader,
                    contractReader,
                    rankProfileReader,
                    roundRecordReader,
                    easterEggRecordReader,
                    installationReader,
                    banStatusReader,
                    metadataReader,
                    logReader,
                    communityPauseStatusReader,
                    communityPauseLogReader
                ]);
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
                dataProvider = new ControlCenterContractsOverlayDataProvider(
                    dataProvider,
                    contractReader);
            }
        }
        catch (Exception exception) when (usingSavedDataSource &&
                                          exception is ArgumentException or DirectoryNotFoundException or
                                              UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            startup = safeFallback;
            localOptions = null;
            dataProvider = simulatedProvider;
            configurationNotice = "La source enregistrée est inaccessible · démarrage sécurisé en simulation.";
            foreach (var disposable in disposables.AsEnumerable().Reverse())
            {
                disposable.Dispose();
            }

            disposables.Clear();
        }

        var snapshotStore = new CachedControlCenterSnapshotStore(dataProvider);
        disposables.Add(snapshotStore);
        if (startup.DataMode == ControlCenterDataMode.HybridLocal)
        {
            snapshotMonitor = new HybridLocalSnapshotMonitor(snapshotStore);
        }

        var simulationService = new SimulationActionService();
        var selectionState = new PlayerSelectionState();
        var rconSecretStore = new DpapiRconSecretStore(rconSecretPath);
        var operatorActivityStore = new InMemoryOperatorActivityStore();
        var mapCatalogService = new JsonMapCatalogService(mapCatalogPath);
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
        var rconOperations = new OperatorRconOperationCoordinator();
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
            rconOperations,
            mapCatalogService,
            mapCatalogState,
            clipboardService,
            snapshotStore);
        var dashboard = new DashboardViewModel(
            snapshotStore,
            simulationService,
            selectionState,
            playerAdministrationCommandService,
            confirmationService,
            settings.CreateRconEndpoint,
            operatorActivityStore,
            rconOperations,
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
            rconOperations,
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
            rconOperations,
            serverAdministrationCommandService,
            mutationSafety,
            mapCatalogService,
            mapCatalogState,
            clipboardService,
            selectionState);
        var shell = new ShellViewModel(
            snapshotStore,
            dashboard,
            players,
            server,
            records,
            logs,
            settings);
        return new ServerRuntimeContext(
            profileId,
            shell,
            settings,
            rconOperations,
            snapshotMonitor,
            disposables,
            applicationLifetime,
            configurationNotice);
    }

    public async Task StartAsync(Dispatcher dispatcher)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Shell.InitializeAsync(_lifetime.Token);
        if (ConfigurationNotice is not null)
        {
            Shell.ReportConfigurationNotice(ConfigurationNotice);
        }

        if (_snapshotMonitor is not null)
        {
            _monitorTask = RunMonitorAsync(
                _snapshotMonitor,
                Shell,
                dispatcher,
                _lifetime.Token);
        }
    }

    public void StopAcceptingNewOperations() => RconOperations.StopAcceptingNewOperations();

    public void Cancel() => _lifetime.Cancel();

    public async Task WaitForShutdownAsync()
    {
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

        await RconOperations.WaitForIdleAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var disposable in _disposables.AsEnumerable().Reverse())
        {
            disposable.Dispose();
        }

        _lifetime.Dispose();
        _disposed = true;
    }

    private static async Task RunMonitorAsync(
        IControlCenterSnapshotMonitor monitor,
        ShellViewModel shell,
        Dispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        try
        {
            await monitor.RunAsync(
                async (_, token) =>
                {
                    await dispatcher.InvokeAsync(
                        () => shell.ApplyCurrentSnapshotAsync(token),
                        DispatcherPriority.Background,
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
