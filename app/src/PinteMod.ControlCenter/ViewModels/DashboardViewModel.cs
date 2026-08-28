using System.IO;
using System.Text.Json;
using System.Collections.ObjectModel;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.State;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class DashboardViewModel : PlayerActionsViewModelBase
{
    private ServerState? _server;
    private SnapshotDataContext _snapshotContext = SnapshotDataContext.Simulation;
    private BlockALocalSnapshot _localObservation = BlockALocalSnapshot.Simulation;
    private readonly Func<Task<ServerLaunchResult>>? _serverLaunchAction;
    private readonly Func<Task<ServerLaunchResult>>? _serverStopAction;
    private readonly Func<bool>? _serverRunningProbe;
    private readonly Func<CancellationToken, Task<bool>>? _serverControlTransportAvailabilityProbe;
    private readonly IOperatorConfirmationService? _serverConfirmationService;
    private bool _serverControlTransportAvailable = true;
    private bool _serverControlTransportChecked = true;
    private readonly ServerIntegrationProfile _integrationProfile;
    private string? _serverLaunchStatus;

    public DashboardViewModel(
        IControlCenterSnapshotStore snapshotStore,
        ISimulationActionService simulationService,
        PlayerSelectionState selectionState,
        IPlayerAdministrationCommandService? playerAdministrationService = null,
        IOperatorConfirmationService? confirmationService = null,
        Func<RconEndpoint?>? rconEndpointFactory = null,
        IOperatorActivityStore? operatorActivityStore = null,
        IOperatorRconOperationCoordinator? rconOperations = null,
        OperatorMutationSafetyState? mutationSafety = null,
        IPlayerModerationHistoryReader? playerHistoryReader = null,
        PlayerChatViewModel? playerChat = null,
        Func<Task<ServerLaunchResult>>? serverLaunchAction = null,
        Func<Task<ServerLaunchResult>>? serverStopAction = null,
        ServerIntegrationProfile? integrationProfile = null,
        Func<bool>? serverRunningProbe = null,
        Func<CancellationToken, Task<bool>>? serverControlTransportAvailabilityProbe = null)
        : base(
            "Dashboard",
            "Vue générale de la session, des services et des joueurs connectés",
            snapshotStore,
            simulationService,
            selectionState,
            playerAdministrationService,
            confirmationService,
            rconEndpointFactory,
            operatorActivityStore,
            rconOperations,
            mutationSafety,
            playerHistoryReader)
    {
        PlayerChat = playerChat;
        _serverLaunchAction = serverLaunchAction;
        _serverStopAction = serverStopAction;
        _serverRunningProbe = serverRunningProbe;
        _serverControlTransportAvailabilityProbe = serverControlTransportAvailabilityProbe;
        _serverControlTransportAvailable = serverControlTransportAvailabilityProbe is null;
        _serverControlTransportChecked = serverControlTransportAvailabilityProbe is null;
        _serverConfirmationService = confirmationService;
        _integrationProfile = integrationProfile ?? ServerIntegrationProfile.Unknown;
        StartServerCommand = new AsyncRelayCommand(StartServerAsync, () => CanStartServer, ReportError);
        StopServerCommand = new AsyncRelayCommand(StopServerAsync, () => CanStopServer, ReportError);
    }

    public PlayerChatViewModel? PlayerChat { get; }

    public AsyncRelayCommand StartServerCommand { get; }

    public AsyncRelayCommand StopServerCommand { get; }

    public string IntegrationProviderLabel => _integrationProfile.Kind switch
    {
        ManagedServerIntegrationKind.PinteMod => "PINTEMOD · INTÉGRATION COMPLÈTE",
        ManagedServerIntegrationKind.ControlCenterBridge => "MODULE DE COMPATIBILITÉ · VALIDATION EN COURS",
        ManagedServerIntegrationKind.ThirdPartyScripts => "GSC TIERS · MODE LIMITÉ",
        ManagedServerIntegrationKind.BoiiiNative => "BOIII NATIF · MODE LIMITÉ",
        _ => "INTÉGRATION NON DÉTECTÉE"
    };

    public string IntegrationProviderDetails
    {
        get
        {
            var available = _integrationProfile.Capabilities.Count(item => item.Availability == IntegrationCapabilityAvailability.Available);
            var observed = _integrationProfile.Capabilities.Count(item => item.Availability == IntegrationCapabilityAvailability.Observed);
            return _integrationProfile.Kind == ManagedServerIntegrationKind.Unknown
                ? "Aucun provider adaptatif n’est associé à ce profil."
                : $"Provider : {_integrationProfile.ProviderLabel} · {available} capacité(s) disponible(s) · {observed} observée(s).";
        }
    }

    public bool ServerAlreadyRunning => IsServerRunningByProbe() ||
        SnapshotContext.Mode == ControlCenterDataMode.HybridLocal &&
        ((Server?.ServerRunningAvailable == true && Server.ServerRunning) ||
         Services.Any(service =>
             service.Name.Equals("Supervisor", StringComparison.OrdinalIgnoreCase) &&
             service.Freshness == DataFreshness.Fresh &&
             service.DeclaredState is ServiceDeclaredState.Monitoring or ServiceDeclaredState.Running or
                 ServiceDeclaredState.Connected or ServiceDeclaredState.Active));

    private bool IsServerRunningByProbe()
    {
        if (_serverRunningProbe is null) return false;
        try
        {
            return _serverRunningProbe();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    public bool HasServerLaunchAction => _serverLaunchAction is not null;

    public bool HasServerStopAction => _serverStopAction is not null;

    public bool ServerControlTransportAvailable => _serverControlTransportAvailable;

    public string ServerControlTransportStatus => !_serverControlTransportChecked || ServerControlTransportAvailable
        ? string.Empty
        : "COMMANDE DISTANTE INDISPONIBLE · Agent SMB non appairé, hors ligne ou version non synchronisée.";

    public bool CanStartServer => _serverLaunchAction is not null && ServerControlTransportAvailable && !ServerAlreadyRunning;

    public bool CanStopServer => _serverStopAction is not null && ServerControlTransportAvailable && ServerAlreadyRunning;

    public string ServerRuntimeStatusLabel
    {
        get
        {
            if (SnapshotContext.Mode == ControlCenterDataMode.Simulation)
            {
                return "ÉTAT SERVEUR SIMULÉ";
            }

            var localNativeProcess = !string.IsNullOrWhiteSpace(SnapshotContext.ServerRoot) &&
                                     !SnapshotContext.ServerRoot.StartsWith(@"\\", StringComparison.Ordinal) &&
                                     _integrationProfile.Kind is ManagedServerIntegrationKind.BoiiiNative or
                                         ManagedServerIntegrationKind.ThirdPartyScripts;
            if (Server?.ServerRunningAvailable == true)
            {
                if (localNativeProcess)
                {
                    return Server.ServerRunning
                        ? "SERVEUR EN LIGNE · PROCESSUS BOIII DÉTECTÉ"
                        : "SERVEUR ARRÊTÉ · PROCESSUS BOIII ABSENT";
                }

                return Server.ServerRunning
                    ? "SERVEUR EN LIGNE · ÉTAT RUNTIME PROUVÉ"
                    : "SERVEUR ARRÊTÉ · ÉTAT RUNTIME PROUVÉ";
            }

            return ServerAlreadyRunning
                ? "SERVEUR EN LIGNE · ÉTAT OBSERVÉ"
                : "ÉTAT SERVEUR NON OBSERVÉ";
        }
    }

    public string? ServerLaunchStatus
    {
        get => _serverLaunchStatus;
        private set => SetProperty(ref _serverLaunchStatus, value);
    }

    private ServerState? Server
    {
        get => _server;
        set
        {
            if (SetProperty(ref _server, value))
            {
                OnPropertyChanged(nameof(RoundDisplay));
                OnPropertyChanged(nameof(DurationDisplayText));
                OnPropertyChanged(nameof(PlayersDisplay));
                OnPropertyChanged(nameof(RankedStatusDisplay));
                OnPropertyChanged(nameof(RuntimeSourceLabel));
                OnPropertyChanged(nameof(MapName));
                OnPropertyChanged(nameof(MapCode));
                OnPropertyChanged(nameof(ServerAlreadyRunning));
                OnPropertyChanged(nameof(ServerRuntimeStatusLabel));
                OnPropertyChanged(nameof(ModeSummary));
                OnPropertyChanged(nameof(CanStartServer));
                OnPropertyChanged(nameof(CanStopServer));
                StartServerCommand.NotifyCanExecuteChanged();
                StopServerCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<ServiceItemViewModel> Services { get; } = [];

    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    private BlockALocalSnapshot LocalObservation
    {
        get => _localObservation;
        set
        {
            if (SetProperty(ref _localObservation, value))
            {
                OnPropertyChanged(nameof(InstallationSummary));
                OnPropertyChanged(nameof(InstallationHealth));
                OnPropertyChanged(nameof(LogsSourceSummary));
                OnPropertyChanged(nameof(RuntimeSourceLabel));
            }
        }
    }

    public string RoundDisplay => Server?.RoundAvailable == true ? Server.Round.ToString() : "—";

    public string MapName => Server?.MapName ?? "—";

    public string MapCode => Server?.MapCode ?? "—";

    public string DurationDisplayText => Server?.SessionDurationAvailable == true
        ? DurationDisplay.Format(Server.SessionDuration)
        : "—";

    public string PlayersDisplay => Server?.PlayersConnectedAvailable == true
        ? Server.MaxPlayersAvailable
            ? $"{Server.PlayersConnected} / {Server.MaxPlayers}"
            : $"{Server.PlayersConnected} / —"
        : "— / —";

    public RankedStatus RankedStatusDisplay => Server?.RankedStatusAvailable == true
        ? Server.RankedStatus
        : RankedStatus.Unknown;

    public string RuntimeSourceLabel => Server switch
    {
        { RuntimeValuesInferred: true } => "INFÉRÉ DEPUIS LES LOGS",
        not null when LocalObservation.RuntimeSnapshot.Metadata.ReadStatus == LocalReadStatus.Success &&
                      LocalObservation.RuntimeSnapshot.Metadata.Freshness == DataFreshness.Fresh => "RUNTIME PINTE MOD LOCAL",
        not null when Server.SessionProvenance == DataProvenance.Unavailable => "DONNÉE INDISPONIBLE",
        _ => "DONNÉE SIMULÉE"
    };

    public string InstallationSummary => LocalObservation.InstallationVerification.Value is { } report
        ? $"INSTALLATION · PASS {report.PassCount} · WARNING {report.WarningCount} · ERROR {report.ErrorCount}"
        : LocalObservation.InstallationVerification.Metadata.Message;

    public ServiceHealth InstallationHealth => LocalObservation.InstallationVerification.Value switch
    {
        { ErrorCount: > 0 } => ServiceHealth.Error,
        { WarningCount: > 0 } => ServiceHealth.Warning,
        not null when LocalObservation.InstallationVerification.Metadata.Freshness == DataFreshness.Fresh => ServiceHealth.Healthy,
        not null => ServiceHealth.Warning,
        _ => ServiceHealth.Unknown
    };

    public string LogsSourceSummary => LocalObservation.Logs.Source.Provenance switch
    {
        DataProvenance.Simulation => "ÉVÉNEMENTS SIMULÉS",
        DataProvenance.Unavailable => "AUCUN JOURNAL STRUCTURÉ DISPONIBLE",
        _ => $"LOGS LOCAUX · {LocalObservation.Logs.FilesScanned} SOURCE(S) · {LocalObservation.Logs.MalformedLines} MALFORMÉE(S)"
    };

    private SnapshotDataContext SnapshotContext
    {
        get => _snapshotContext;
        set
        {
            if (SetProperty(ref _snapshotContext, value))
            {
                OnPropertyChanged(nameof(ModeLabel));
                OnPropertyChanged(nameof(ModeSummary));
                OnPropertyChanged(nameof(MapSourceLabel));
                OnPropertyChanged(nameof(SessionSourceLabel));
                OnPropertyChanged(nameof(SessionReadStatus));
                OnPropertyChanged(nameof(SessionFreshness));
                OnPropertyChanged(nameof(SessionAge));
                OnPropertyChanged(nameof(SessionAgeLabel));
                OnPropertyChanged(nameof(SessionProvenance));
                OnPropertyChanged(nameof(DeclaredVersionLabel));
                OnPropertyChanged(nameof(ServicesSourceLabel));
                OnPropertyChanged(nameof(ServerAlreadyRunning));
                OnPropertyChanged(nameof(ServerRuntimeStatusLabel));
                OnPropertyChanged(nameof(CanStartServer));
                OnPropertyChanged(nameof(CanStopServer));
                StartServerCommand.NotifyCanExecuteChanged();
                StopServerCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ModeLabel => SnapshotContext.ModeLabel;

    public string ModeSummary => SnapshotContext.Mode == ControlCenterDataMode.HybridLocal
        ? SnapshotContext.SessionSource.Provenance == DataProvenance.Unavailable
            ? Server is { ServerRunningAvailable: true, ServerRunning: true } serverRunning
                ? string.Equals(serverRunning.RuntimeSource.SourceLabel, "Agent SMB", StringComparison.Ordinal)
                    ? "Serveur BOIII réel en ligne · processus distant prouvé par l’Agent SMB · carte/manche/joueurs restent indisponibles sans télémétrie structurée."
                    : "Serveur BOIII réel en ligne · processus local prouvé · carte/manche/joueurs restent indisponibles sans télémétrie structurée."
                : Server is { ServerRunningAvailable: true, ServerRunning: false } serverStopped
                    ? string.Equals(serverStopped.RuntimeSource.SourceLabel, "Agent SMB", StringComparison.Ordinal)
                        ? "Serveur BOIII réel arrêté · état distant prouvé par l’Agent SMB · aucune donnée de démonstration n’est injectée."
                        : "Serveur BOIII réel arrêté · processus local absent · aucune donnée de démonstration n’est injectée."
                    : "Serveur réel détecté · état runtime non prouvé · aucune donnée de démonstration n’est injectée."
            : SnapshotContext.SimulatedAreas.Count == 0
                ? "Lecture locale structurée read-only · aucune zone simulée."
                : $"Lecture locale structurée read-only · simulation limitée à : {string.Join(", ", SnapshotContext.SimulatedAreas)}"
        : "Toutes les informations affichées sont simulées.";

    public string MapSourceLabel => Server?.MapProvenance switch
    {
        DataProvenance.LocalFile => "CARTE LOCALE",
        DataProvenance.MemoryCache => "CARTE LOCALE EN CACHE",
        DataProvenance.Unavailable => "CARTE INDISPONIBLE",
        _ => "CARTE SIMULÉE"
    };

    public string SessionSourceLabel => Server?.SessionProvenance switch
    {
        DataProvenance.LocalFile => "SESSION LOCALE ACTIVE",
        DataProvenance.MemoryCache => "SESSION LOCALE EN CACHE",
        DataProvenance.Unavailable => "SESSION NON OBSERVÉE",
        _ => "SESSION SIMULÉE"
    };

    public LocalReadStatus SessionReadStatus => SnapshotContext.SessionSource.ReadStatus;

    public DataFreshness SessionFreshness => SnapshotContext.SessionSource.Freshness;

    public string SessionAge => DisplayText.FormatAge(SnapshotContext.SessionSource.Age);

    public string SessionAgeLabel => $"Âge fichier : {SessionAge}";

    public string SessionProvenance => DisplayText.Provenance(SnapshotContext.SessionSource.Provenance);

    public string DeclaredVersionLabel => Server is null
        ? "Version déclarée : —"
        : Server.SessionProvenance is DataProvenance.LocalFile or DataProvenance.MemoryCache
            ? $"Version déclarée : {Server.PinteModVersion} · ne prouve pas l’état de santé"
            : Server.SessionProvenance == DataProvenance.Unavailable
                ? "Version PinteMod : non disponible"
                : $"Version simulée : {Server.PinteModVersion}";

    public string ServicesSourceLabel => SnapshotContext.Mode == ControlCenterDataMode.HybridLocal
        ? SnapshotContext.SessionSource.Provenance == DataProvenance.Unavailable
            ? "AUCUN HEARTBEAT STRUCTURÉ"
            : "HEARTBEATS LOCAUX"
        : "HEARTBEATS SIMULÉS";

    private async Task<bool> RefreshServerControlTransportAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var available = true;
        if (_serverControlTransportAvailabilityProbe is not null)
        {
            try
            {
                available = await _serverControlTransportAvailabilityProbe(cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                available = false;
            }
        }

        var changed = _serverControlTransportAvailable != available || !_serverControlTransportChecked;
        _serverControlTransportAvailable = available;
        _serverControlTransportChecked = true;
        if (changed)
        {
            OnPropertyChanged(nameof(ServerControlTransportAvailable));
            OnPropertyChanged(nameof(ServerControlTransportStatus));
            OnPropertyChanged(nameof(CanStartServer));
            OnPropertyChanged(nameof(CanStopServer));
            StartServerCommand.NotifyCanExecuteChanged();
            StopServerCommand.NotifyCanExecuteChanged();
        }

        return available;
    }

    private async Task StartServerAsync()
    {
        if (_serverLaunchAction is null)
        {
            ServerLaunchStatus = "Aucun profil de lancement n’est associé à cet onglet.";
            return;
        }

        if (!await RefreshServerControlTransportAvailabilityAsync())
        {
            ServerLaunchStatus = "Lancement distant indisponible : l’Agent SMB doit être appairé, ONLINE et sur la même version.";
            return;
        }

        ServerLaunchStatus = "Lancement du serveur en cours…";
        var result = await _serverLaunchAction();
        ServerLaunchStatus = result.Message;
        OnPropertyChanged(nameof(ServerAlreadyRunning));
        OnPropertyChanged(nameof(CanStartServer));
        OnPropertyChanged(nameof(CanStopServer));
        StartServerCommand.NotifyCanExecuteChanged();
        StopServerCommand.NotifyCanExecuteChanged();
        if (result.Success && SnapshotContext.Mode == ControlCenterDataMode.HybridLocal)
        {
            await RefreshAfterServerStateChangeAsync(expectRunning: true);
        }
    }

    private async Task StopServerAsync()
    {
        if (_serverStopAction is null)
        {
            ServerLaunchStatus = "Aucun profil d’arrêt n’est associé à cet onglet.";
            return;
        }

        if (!await RefreshServerControlTransportAvailabilityAsync())
        {
            ServerLaunchStatus = "Arrêt distant indisponible : l’Agent SMB doit être appairé, ONLINE et sur la même version.";
            return;
        }

        if (_serverConfirmationService is not null)
        {
            var confirmed = await _serverConfirmationService.ConfirmAsync(new OperatorConfirmationRequest(
                "Arrêter le serveur",
                "Arrêter maintenant ce serveur BOIII ?\n\nLa partie en cours sera interrompue. Le Worker et les services PinteMod liés à ce profil seront également arrêtés."));
            if (!confirmed) return;
        }

        ServerLaunchStatus = "Arrêt du serveur en cours…";
        var result = await _serverStopAction();
        ServerLaunchStatus = result.Message;
        OnPropertyChanged(nameof(ServerAlreadyRunning));
        OnPropertyChanged(nameof(CanStartServer));
        OnPropertyChanged(nameof(CanStopServer));
        StartServerCommand.NotifyCanExecuteChanged();
        StopServerCommand.NotifyCanExecuteChanged();
        if (result.Success)
        {
            await RefreshAfterServerStateChangeAsync(expectRunning: false);
        }
    }

    private async Task RefreshAfterServerStateChangeAsync(bool expectRunning)
    {
        var delays = new[] { 600, 900, 1400, 2000 };
        foreach (var delay in delays)
        {
            await Task.Delay(delay);
            try
            {
                await SnapshotStore.RefreshAsync();
                await InitializeAsync();
                if (ServerAlreadyRunning == expectRunning)
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                // Runtime/heartbeat files are created and removed atomically around BOIII
                // transitions. A transient read race must not turn a successful start/stop
                // into a UI error; retry on the next bounded pass instead.
            }
        }
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        await RefreshServerControlTransportAvailabilityAsync(cancellationToken);
        var snapshot = await SnapshotStore.GetSnapshotAsync(cancellationToken);
        Server = snapshot.Server;
        SnapshotContext = snapshot.DataContext;
        LocalObservation = snapshot.LocalObservation;
        ConfigurePlayerDataContext(snapshot);
        OnPropertyChanged(nameof(MapSourceLabel));
        OnPropertyChanged(nameof(SessionSourceLabel));
        OnPropertyChanged(nameof(DeclaredVersionLabel));
        ReplacePlayers(snapshot.Players);

        Services.Clear();
        foreach (var service in snapshot.Services)
        {
            Services.Add(new ServiceItemViewModel(service));
        }
        OnPropertyChanged(nameof(ServerAlreadyRunning));
        OnPropertyChanged(nameof(ServerRuntimeStatusLabel));
        OnPropertyChanged(nameof(ModeSummary));
        OnPropertyChanged(nameof(CanStartServer));
        OnPropertyChanged(nameof(CanStopServer));
        StartServerCommand.NotifyCanExecuteChanged();
        StopServerCommand.NotifyCanExecuteChanged();

        if (SnapshotContext.Mode == ControlCenterDataMode.HybridLocal && Server?.ServerRunningAvailable == true)
        {
            ServerLaunchStatus = Server.ServerRunning
                ? "Serveur BOIII en ligne détecté localement."
                : "Serveur BOIII arrêté.";
        }

        Events.Clear();
        foreach (var item in snapshot.Events)
        {
            Events.Add(new EventItemViewModel(item));
        }
    }
}
