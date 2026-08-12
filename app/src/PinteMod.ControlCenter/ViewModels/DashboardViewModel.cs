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
        IPlayerModerationHistoryReader? playerHistoryReader = null)
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

    public string LogsSourceSummary => LocalObservation.Logs.Source.Provenance == DataProvenance.Simulation
        ? "ÉVÉNEMENTS SIMULÉS"
        : $"LOGS LOCAUX · {LocalObservation.Logs.FilesScanned} SOURCE(S) · {LocalObservation.Logs.MalformedLines} MALFORMÉE(S)";

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
            }
        }
    }

    public string ModeLabel => SnapshotContext.ModeLabel;

    public string ModeSummary => SnapshotContext.Mode == ControlCenterDataMode.HybridLocal
        ? $"Lecture locale structurée read-only · simulation limitée à : {string.Join(", ", SnapshotContext.SimulatedAreas)}"
        : "Toutes les informations affichées sont simulées.";

    public string MapSourceLabel => Server?.MapProvenance switch
    {
        DataProvenance.LocalFile => "CARTE LOCALE",
        DataProvenance.MemoryCache => "CARTE LOCALE EN CACHE",
        _ => "CARTE SIMULÉE"
    };

    public string SessionSourceLabel => Server?.SessionProvenance switch
    {
        DataProvenance.LocalFile => "SESSION LOCALE ACTIVE",
        DataProvenance.MemoryCache => "SESSION LOCALE EN CACHE",
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
            : $"Version simulée : {Server.PinteModVersion}";

    public string ServicesSourceLabel => SnapshotContext.Mode == ControlCenterDataMode.HybridLocal
        ? "HEARTBEATS LOCAUX"
        : "HEARTBEATS SIMULÉS";

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
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

        Events.Clear();
        foreach (var item in snapshot.Events)
        {
            Events.Add(new EventItemViewModel(item));
        }
    }
}
