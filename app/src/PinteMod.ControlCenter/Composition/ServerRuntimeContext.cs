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
        CancellationToken applicationLifetime,
        Func<Task<ServerLaunchResult>>? serverLaunchAction = null,
        Func<Task<ServerLaunchResult>>? serverStopAction = null,
        string remoteAgentId = "")
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
        var integrationProfile = ServerIntegrationProfile.Unknown;
        var integrationRoot = startup.ServerRoot ?? savedConfiguration.ServerRoot;
        if (!string.IsNullOrWhiteSpace(integrationRoot))
        {
            try
            {
                integrationProfile = new ServerInstallationAnalyzer().Analyze(integrationRoot).IntegrationProfile;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                integrationProfile = ServerIntegrationProfile.Unknown;
            }
        }

        // A real registered BOIII root must never inherit the demo snapshot just
        // because it has no PinteMod/Bridge data source. Keep it real and empty.
        if (startup.DataMode == ControlCenterDataMode.Simulation &&
            !string.IsNullOrWhiteSpace(integrationRoot))
        {
            dataProvider = new AdaptiveUnavailableControlCenterDataProvider(
                integrationRoot,
                integrationProfile);
        }

        try
        {
            if (startup.DataMode == ControlCenterDataMode.HybridLocal)
            {
                var configuredRoot = startup.ServerRoot!;
                var rootLayout = Directory.Exists(Path.Combine(configuredRoot, "boiii", "scriptdata", "pintemod"))
                    ? LocalPinteModRootLayout.ServerRoot
                    : usingSavedDataSource && savedConfiguration.DataLocation == OperatorDataLocation.Lan
                        ? LocalPinteModRootLayout.PinteModDataRoot
                        : LocalPinteModRootLayout.ServerRoot;
                localOptions = new LocalPinteModOptions(configuredRoot, rootLayout);
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
            dataProvider = !string.IsNullOrWhiteSpace(integrationRoot)
                ? new AdaptiveUnavailableControlCenterDataProvider(integrationRoot, integrationProfile)
                : simulatedProvider;
            configurationNotice = !string.IsNullOrWhiteSpace(integrationRoot)
                ? "La source structurée enregistrée est inaccessible · le profil réel reste affiché sans données inventées."
                : "La source enregistrée est inaccessible · démarrage sécurisé en simulation.";
            foreach (var disposable in disposables.AsEnumerable().Reverse())
            {
                disposable.Dispose();
            }

            disposables.Clear();
        }

        if (!string.IsNullOrWhiteSpace(integrationRoot) &&
            integrationRoot.StartsWith(@"\\", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(remoteAgentId))
        {
            dataProvider = new RemoteAgentRuntimeControlCenterDataProvider(
                dataProvider, integrationRoot, profileId, remoteAgentId);
        }

        var snapshotStore = new CachedControlCenterSnapshotStore(dataProvider);
        disposables.Add(snapshotStore);
        if (startup.DataMode == ControlCenterDataMode.HybridLocal)
        {
            snapshotMonitor = new HybridLocalSnapshotMonitor(snapshotStore);
        }

        var chatClock = new SystemClock();
        PlayerChatLogReader? playerChatReader = null;
        if (localOptions is not null)
        {
            playerChatReader = new PlayerChatLogReader(localOptions, chatClock);
            disposables.Add(playerChatReader);
        }

        var playerChatHistoryStore = new JsonPlayerChatHistoryStore(
            OperatorProfileStoragePaths.GetPlayerChatHistoryPath(profileId),
            chatClock);
        disposables.Add(playerChatHistoryStore);

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
        var playerChat = new PlayerChatViewModel(snapshotStore, playerChatHistoryStore, playerChatReader);
        var settings = new SettingsViewModel(
            startup.DataMode,
            integrationProfile.Kind == ManagedServerIntegrationKind.Unknown
                ? dataProvider.GetType().Name
                : $"{integrationProfile.ProviderLabel} · {dataProvider.GetType().Name}",
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
            snapshotStore,
            integrationProfile,
            allowPinteModDiagnostics: string.IsNullOrWhiteSpace(integrationRoot) ||
                                     integrationProfile.SupportsPinteModClosedCommands,
            selfTestService: new ControlCenterSelfTestService(),
            rconBootstrapService: new BoiiiRconBootstrapService(),
            publicChatTipsConfigurationService: new PinteModPublicChatTipsConfigurationService());
        var managedRuntimeProbe = new ManagedServerRuntimeProbe();
        bool ProbeManagedServerRunning()
        {
            if (string.IsNullOrWhiteSpace(integrationRoot)) return false;
            var port = settings.CreateRconEndpoint()?.Port ?? savedConfiguration.RconPort;
            return managedRuntimeProbe.IsRunning(integrationRoot, port);
        }

        async Task<bool> ProbeServerControlTransportAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(integrationRoot) ||
                !integrationRoot.StartsWith(@"\\", StringComparison.Ordinal))
            {
                // Local launch/stop never depends on the remote Agent.
                return true;
            }

            var managedStore = new JsonManagedServerProfileStore(
                OperatorProfileStoragePaths.GetManagedServerProfilePath(profileId));
            var managedConfiguration = await managedStore.LoadAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(managedConfiguration.RemoteAgentId)) return false;

            var remoteProbe = await new RemoteLaunchClientService().ProbeAsync(
                integrationRoot,
                profileId,
                managedConfiguration.RemoteAgentId,
                cancellationToken);
            return remoteProbe.Paired && remoteProbe.Online;
        }

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
            playerHistoryReader,
            playerChat,
            serverLaunchAction,
            serverStopAction,
            integrationProfile,
            ProbeManagedServerRunning,
            ProbeServerControlTransportAsync);
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
            selectionState,
            integrationProfile: integrationProfile,
            allowSimulationActions: startup.DataMode == ControlCenterDataMode.Simulation &&
                                    string.IsNullOrWhiteSpace(integrationRoot));
        var shell = new ShellViewModel(
            snapshotStore,
            dashboard,
            players,
            server,
            records,
            logs,
            settings,
            playerChat: playerChat,
            restrictUnprovedCapabilities: startup.DataMode == ControlCenterDataMode.Simulation &&
                                           !string.IsNullOrWhiteSpace(integrationRoot),
            integrationProfile: integrationProfile);
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
