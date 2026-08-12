using System.Collections.ObjectModel;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.Services;
using PinteMod.ControlCenter.State;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class ServerViewModel : PageViewModel
{
    private readonly IControlCenterSnapshotStore _snapshotStore;
    private readonly ISimulationActionService _simulationService;
    private readonly ICommunityPauseCommandService? _pauseCommandService;
    private readonly IServerAdministrationCommandService? _serverAdministrationCommandService;
    private readonly IRconDiagnosticService? _rconDiagnosticService;
    private readonly IOperatorConfirmationService? _confirmationService;
    private readonly Func<RconEndpoint?>? _rconEndpointFactory;
    private readonly IOperatorActivityStore? _operatorActivityStore;
    private readonly IOperatorRconOperationCoordinator _rconOperations;
    private readonly OperatorMutationSafetyState _mutationSafety;
    private readonly IMapCatalogService? _mapCatalogService;
    private readonly MapCatalogState? _mapCatalogState;
    private readonly ITextClipboardService? _clipboardService;
    private ServerState? _server;
    private SelectionOption? _selectedMap;
    private int _selectedRound = 20;
    private SimulationResultItemViewModel? _lastSimulationResult;
    private BlockALocalSnapshot _localObservation = BlockALocalSnapshot.Simulation;
    private string _pauseCommandStatus = "AUCUNE COMMANDE RÉELLE ENVOYÉE";
    private string _pauseCommandMessage = "Une source Community Pause locale et fraîche est requise.";
    private string _pauseCommandSent = "Commande envoyée : Non";
    private ServiceHealth _pauseCommandHealth = ServiceHealth.Unknown;
    private string _serverDiagnosticStatus = "AUCUN DIAGNOSTIC RCON";
    private string _serverDiagnosticMessage = "Choisissez un diagnostic manuel en lecture seule.";
    private string _serverDiagnosticCommandSent = "Commande envoyée : Non";
    private ServiceHealth _serverDiagnosticHealth = ServiceHealth.Unknown;
    private string _serverDiagnosticCopyStatus = "Aucune copie effectuée.";
    private bool _pauseMutationAuthorizationBlocked;
    private DateTimeOffset? _pauseMutationBlockedAfterTimestamp;
    private string _serverAdministrationStatus = "AUCUNE ACTION SERVEUR RÉELLE";
    private string _serverAdministrationMessage = "Les actions ci-dessous exigent une confirmation et une vérification manuelle de la console.";
    private string _serverAdministrationCommandSent = "Commande envoyée : Non";
    private ServiceHealth _serverAdministrationHealth = ServiceHealth.Unknown;
    private bool _serverAdministrationMutationBlocked;
    private bool _mapSelectionInitialized;

    public ServerViewModel(
        IControlCenterSnapshotStore snapshotStore,
        ISimulationActionService simulationService,
        ICommunityPauseCommandService? pauseCommandService = null,
        IOperatorConfirmationService? confirmationService = null,
        Func<RconEndpoint?>? rconEndpointFactory = null,
        IOperatorActivityStore? operatorActivityStore = null,
        IRconDiagnosticService? rconDiagnosticService = null,
        IOperatorRconOperationCoordinator? rconOperations = null,
        IServerAdministrationCommandService? serverAdministrationCommandService = null,
        OperatorMutationSafetyState? mutationSafety = null,
        IMapCatalogService? mapCatalogService = null,
        MapCatalogState? mapCatalogState = null,
        ITextClipboardService? clipboardService = null)
        : base("Serveur", "Administration locale — commandes réelles confirmées et strictement listées")
    {
        _snapshotStore = snapshotStore;
        _simulationService = simulationService;
        _pauseCommandService = pauseCommandService;
        _serverAdministrationCommandService = serverAdministrationCommandService;
        _rconDiagnosticService = rconDiagnosticService;
        _confirmationService = confirmationService;
        _rconEndpointFactory = rconEndpointFactory;
        _operatorActivityStore = operatorActivityStore;
        _rconOperations = rconOperations ?? new OperatorRconOperationCoordinator();
        _mutationSafety = mutationSafety ?? new OperatorMutationSafetyState();
        _mapCatalogService = mapCatalogService;
        _mapCatalogState = mapCatalogState;
        _clipboardService = clipboardService;
        _mutationSafety.Changed += (_, _) => NotifyAllMutationAuthorizationChanged();
        if (_mapCatalogState is not null)
        {
            _mapCatalogState.Changed += (_, _) =>
                ReplaceMapOptions(_mapCatalogState.Current, SelectedMap?.Key);
        }
        ReplaceMapOptions(MapCatalogSnapshot.OfficialOnly, null);
        SelectedMap = MapOptions[0];
        RoundOptions = [5, 10, 20, 30, 50, 75, 100];
        SimulateServerActionCommand = new AsyncRelayCommand<SimulationAction>(
            SimulateServerActionAsync,
            null,
            ReportError);
        PauseServerCommand = new AsyncRelayCommand(
            () => _rconOperations.RunExclusiveAsync(
                _ => ExecuteCommunityPauseCoreAsync(CommunityPauseAction.Pause)),
            () => CanPauseServer,
            ReportError);
        ResumeServerCommand = new AsyncRelayCommand(
            () => _rconOperations.RunExclusiveAsync(
                _ => ExecuteCommunityPauseCoreAsync(CommunityPauseAction.Resume)),
            () => CanResumeServer,
            ReportError);
        RefreshPauseStatusCommand = new AsyncRelayCommand(
            () => _rconOperations.RunExclusiveAsync(_ => RefreshPauseStatusCoreAsync()),
            null,
            ReportError);
        HealthDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.HealthFull,
            "Le diagnostic Santé PinteMod n’a pas pu être terminé.");
        MapDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.MapInfo,
            "Le diagnostic Carte n’a pas pu être terminé.");
        PowerDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.PowerStatus,
            "Le diagnostic Courant n’a pas pu être terminé.");
        PackAPunchDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.PackAPunchStatus,
            "Le diagnostic Pack-a-Punch n’a pas pu être terminé.");
        RoundDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.RoundStatus,
            "Le diagnostic Manche n’a pas pu être terminé.");
        PlayersDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.Players,
            "Le diagnostic Joueurs n’a pas pu être terminé.");
        MapAuditDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.MapAudit,
            "L’audit de compatibilité de la carte n’a pas pu être terminé.");
        EventStatusDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.EventStatus,
            "Le diagnostic Événements n’a pas pu être terminé.");
        PowerUpCatalogDiagnosticCommand = CreateServerDiagnosticCommand(
            RconDiagnosticCommand.PowerUpCatalog,
            "Le catalogue des power-ups n’a pas pu être interrogé.");
        CopyServerDiagnosticCommand = new AsyncRelayCommand(
            CopyServerDiagnosticAsync,
            () => CanCopyServerDiagnostic);
        NextRoundCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.NextRound));
        SetRoundCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.SetRound, SelectedRound));
        EnablePowerCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.EnablePower));
        EnablePackAPunchCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.EnablePackAPunch));
        PlayMapMusicCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.PlayMapMusic));
        StopMapMusicCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.StopMapMusic));
        UnlockStandardPassagesCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.UnlockStandardPassages));
        KeepLastZombieCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.KeepLastZombie));
        KillAllZombiesCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.KillAllZombies));
        MakePowerUpsPermanentCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.MakePowerUpsPermanent));
        RestorePowerUpTimeoutCommand = CreateServerAdministrationCommand(
            () => new ServerAdministrationRequest(ServerAdministrationAction.RestorePowerUpTimeout));
        AcknowledgeServerAdministrationCommand = new AsyncRelayCommand(
            AcknowledgeServerAdministrationAsync,
            () => _serverAdministrationMutationBlocked,
            ReportError);
    }

    private ServerState? Server
    {
        get => _server;
        set
        {
            if (SetProperty(ref _server, value))
            {
                OnPropertyChanged(nameof(ServerStatusText));
                OnPropertyChanged(nameof(ServerStatusHealth));
                OnPropertyChanged(nameof(RoundDisplay));
                OnPropertyChanged(nameof(ServerStatusSource));
                OnPropertyChanged(nameof(MapName));
                OnPropertyChanged(nameof(MapCode));
                OnPropertyChanged(nameof(PinteModVersion));
                OnPropertyChanged(nameof(RoundSource));
                OnPropertyChanged(nameof(PowerStateText));
                OnPropertyChanged(nameof(PackAPunchStateText));
            }
        }
    }

    public string ServerStatusText => Server switch
    {
        null => "INCONNU",
        { ServerRunningAvailable: false } => "INCONNU",
        { ObservedServerHealth: ServiceHealth.Error } => "ERREUR",
        { ServerRunning: true } => "EN LIGNE",
        _ => "ARRÊTÉ"
    };

    public ServiceHealth ServerStatusHealth => Server switch
    {
        null => ServiceHealth.Unknown,
        { ServerRunningAvailable: false } => ServiceHealth.Unknown,
        { ObservedServerHealth: ServiceHealth.Error } => ServiceHealth.Error,
        { ServerRunning: true } => ServiceHealth.Healthy,
        _ => ServiceHealth.Offline
    };

    public string RoundDisplay => Server is { RoundAvailable: true } server ? server.Round.ToString() : "—";

    public string MapName => Server?.MapName ?? "—";

    public string MapCode => Server?.MapCode ?? "—";

    public string PinteModVersion => Server?.PinteModVersion ?? "—";

    public string ServerStatusSource => Server?.ServerRunningAvailable == true
        ? "HEARTBEAT PINTE MOD LOCAL"
        : "AUCUNE SOURCE PROCESSUS — ÉTAT INCONNU";

    public string RoundSource => Server?.RuntimeValuesInferred == false &&
                                 Server.RuntimeSource.Freshness == DataFreshness.Fresh
        ? "RUNTIME PINTE MOD LOCAL"
        : "INFÉRÉE DES LOGS SI DISPONIBLE";

    public string PowerStateText => Server?.PowerState switch
    {
        RuntimePowerState.On => "ACTIF",
        RuntimePowerState.Off => "INACTIF",
        RuntimePowerState.NotApplicable => "NON APPLICABLE",
        _ => "INCONNU"
    };

    public string PackAPunchStateText => Server?.PackAPunchState switch
    {
        RuntimePackAPunchState.Available => "DISPONIBLE",
        RuntimePackAPunchState.Unavailable => "INDISPONIBLE",
        RuntimePackAPunchState.NotApplicable => "NON APPLICABLE",
        _ => "INCONNU"
    };

    public string DiagnosticsPreview => LocalObservation.InstallationVerification.Value is { } report
        ? $"Rapport local : PASS {report.PassCount} · WARNING {report.WarningCount} · ERROR {report.ErrorCount}"
        : "Aucun rapport local disponible · action toujours simulée";

    public string InstallationSourceSummary =>
        $"Lecture : {DisplayText.ReadStatus(LocalObservation.InstallationVerification.Metadata.ReadStatus)} · " +
        $"Fraîcheur : {DisplayText.Freshness(LocalObservation.InstallationVerification.Metadata.Freshness)} · " +
        $"Âge : {DisplayText.FormatAge(LocalObservation.InstallationVerification.Metadata.Age)}";

    public string BanServiceDetails => LocalObservation.BanServiceStatus.Value is { } status
        ? $"Bans actifs : {status.ActiveBans} · version déclarée {status.Version} · fraîcheur {DisplayText.Freshness(LocalObservation.BanServiceStatus.Metadata.Freshness)}"
        : "Nombre de bans actifs non disponible";

    public string PauseStatusText => IsPauseStatusCurrent
        ? LocalObservation.CommunityPause.Value!.Active ? "EN PAUSE" : "NON PAUSÉE"
        : "INCONNU";

    public ServiceHealth PauseStatusHealth => IsPauseStatusCurrent
        ? LocalObservation.CommunityPause.Value!.Active ? ServiceHealth.Warning : ServiceHealth.Healthy
        : ServiceHealth.Unknown;

    public string PauseModuleVersion => LocalObservation.CommunityPause.Value is { } pause
        ? $"COMMUNITY PAUSE v{pause.ModuleVersion}"
        : "MODULE NON OBSERVÉ";

    public string PauseDetails
    {
        get
        {
            var result = LocalObservation.CommunityPause;
            if (!IsPauseStatusCurrent)
            {
                return result.Value is not null
                    ? "Dernière donnée valide — périmée"
                    : "Aucun statut local récent";
            }

            var pause = result.Value!;
            var activity = pause.Active
                ? $"Reprise automatique dans {pause.AutomaticResumeSeconds} s"
                : "Partie non pausée";
            var vote = pause.ActiveVote == "Aucun"
                ? "aucun vote actif"
                : $"vote {pause.ActiveVote.ToLowerInvariant()} {pause.VoteYes}/{pause.VoteMajority}";
            return $"{activity} · {pause.SuccessfulPauses}/{pause.MaximumSuccessfulPauses} pauses · {vote}";
        }
    }

    public string PauseSourceSummary
    {
        get
        {
            var metadata = LocalObservation.CommunityPause.Metadata;
            return $"Lecture : {DisplayText.ReadStatus(metadata.ReadStatus)} · " +
                   $"Fraîcheur : {DisplayText.Freshness(metadata.Freshness)} · " +
                   $"Âge : {DisplayText.FormatAge(metadata.Age)} · provenance : {DisplayText.Provenance(metadata.Provenance)}";
        }
    }

    public bool CanPauseServer =>
        PauseCommandInfrastructureAvailable &&
        !_mutationSafety.IsBlockedByOtherThan(OperatorMutationScope.CommunityPause) &&
        !_serverAdministrationMutationBlocked &&
        !_pauseMutationAuthorizationBlocked &&
        IsPauseStatusCurrent &&
        LocalObservation.CommunityPause.Value is { Active: false, ActiveVote: "Aucun" };

    public bool CanResumeServer =>
        PauseCommandInfrastructureAvailable &&
        !_mutationSafety.IsBlockedByOtherThan(OperatorMutationScope.CommunityPause) &&
        !_serverAdministrationMutationBlocked &&
        !_pauseMutationAuthorizationBlocked &&
        IsPauseStatusCurrent &&
        LocalObservation.CommunityPause.Value is { Active: true };

    public bool RealPauseControlsAvailable => CanPauseServer || CanResumeServer;

    public string RealPauseControlsNotice
    {
        get
        {
            if (!PauseCommandInfrastructureAvailable)
            {
                return "Configuration RCON requise dans Paramètres.";
            }

            if (_pauseMutationAuthorizationBlocked)
            {
                return "Verrouillé — résultat précédent incertain. Actualisez le statut avant toute nouvelle commande.";
            }

            if (_serverAdministrationMutationBlocked)
            {
                return "Verrouillé — une action serveur attend votre vérification dans la console.";
            }

            if (_mutationSafety.IsBlockedByOtherThan(OperatorMutationScope.CommunityPause))
            {
                return "Verrouillé — une autre action opérateur attend une vérification manuelle.";
            }

            if (!IsPauseStatusCurrent)
            {
                return "Verrouillé — connectez une source serveur live avec un statut Pause frais.";
            }

            if (LocalObservation.CommunityPause.Value!.Active)
            {
                return "Partie en pause — seule la reprise est disponible.";
            }

            if (LocalObservation.CommunityPause.Value.ActiveVote != "Aucun")
            {
                return "Verrouillé — un vote communautaire est actuellement actif.";
            }

            return "Prêt — confirmation obligatoire avant l’envoi réel.";
        }
    }

    public string PauseCommandStatus
    {
        get => _pauseCommandStatus;
        private set => SetProperty(ref _pauseCommandStatus, value);
    }

    public string PauseCommandMessage
    {
        get => _pauseCommandMessage;
        private set => SetProperty(ref _pauseCommandMessage, value);
    }

    public string PauseCommandSent
    {
        get => _pauseCommandSent;
        private set => SetProperty(ref _pauseCommandSent, value);
    }

    public ServiceHealth PauseCommandHealth
    {
        get => _pauseCommandHealth;
        private set => SetProperty(ref _pauseCommandHealth, value);
    }

    public string ServerDiagnosticStatus
    {
        get => _serverDiagnosticStatus;
        private set => SetProperty(ref _serverDiagnosticStatus, value);
    }

    public string ServerDiagnosticMessage
    {
        get => _serverDiagnosticMessage;
        private set
        {
            if (SetProperty(ref _serverDiagnosticMessage, value))
            {
                OnPropertyChanged(nameof(CanCopyServerDiagnostic));
                CopyServerDiagnosticCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanCopyServerDiagnostic =>
        _clipboardService is not null && !string.IsNullOrWhiteSpace(ServerDiagnosticMessage);

    public string ServerDiagnosticCopyStatus
    {
        get => _serverDiagnosticCopyStatus;
        private set => SetProperty(ref _serverDiagnosticCopyStatus, value);
    }

    public string ServerDiagnosticCommandSent
    {
        get => _serverDiagnosticCommandSent;
        private set => SetProperty(ref _serverDiagnosticCommandSent, value);
    }

    public ServiceHealth ServerDiagnosticHealth
    {
        get => _serverDiagnosticHealth;
        private set => SetProperty(ref _serverDiagnosticHealth, value);
    }

    public string ServerAdministrationStatus
    {
        get => _serverAdministrationStatus;
        private set => SetProperty(ref _serverAdministrationStatus, value);
    }

    public string ServerAdministrationMessage
    {
        get => _serverAdministrationMessage;
        private set => SetProperty(ref _serverAdministrationMessage, value);
    }

    public string ServerAdministrationCommandSent
    {
        get => _serverAdministrationCommandSent;
        private set => SetProperty(ref _serverAdministrationCommandSent, value);
    }

    public ServiceHealth ServerAdministrationHealth
    {
        get => _serverAdministrationHealth;
        private set => SetProperty(ref _serverAdministrationHealth, value);
    }

    public bool CanRunServerAdministration =>
        _serverAdministrationCommandService is not null &&
        _confirmationService is not null &&
        _rconEndpointFactory?.Invoke() is not null &&
        !_mutationSafety.IsBlockedByOtherThan(OperatorMutationScope.ServerAdministration) &&
        !_pauseMutationAuthorizationBlocked &&
        !_serverAdministrationMutationBlocked;

    public string ServerAdministrationNotice
    {
        get
        {
            if (_serverAdministrationCommandService is null ||
                _confirmationService is null ||
                _rconEndpointFactory?.Invoke() is null)
            {
                return "Configuration RCON requise dans Paramètres.";
            }

            if (_pauseMutationAuthorizationBlocked)
            {
                return "Verrouillé — le résultat d’une commande Pause/Reprendre doit d’abord être confirmé.";
            }

            if (_serverAdministrationMutationBlocked)
            {
                return "Verrouillé — vérifiez la console BOIII, puis utilisez « J’AI VÉRIFIÉ LA CONSOLE ».";
            }

            if (_mutationSafety.IsBlockedByOtherThan(OperatorMutationScope.ServerAdministration))
            {
                return "Verrouillé — une autre action opérateur attend une vérification manuelle.";
            }

            return "Prêt · confirmation obligatoire · résultat à vérifier dans la console BOIII.";
        }
    }

    private bool PauseCommandInfrastructureAvailable =>
        _pauseCommandService is not null &&
        _confirmationService is not null &&
        _rconEndpointFactory?.Invoke() is not null;

    private bool IsPauseStatusCurrent =>
        LocalObservation.CommunityPause.Value is not null &&
        LocalObservation.CommunityPause.SourceTimestampUtc is not null &&
        LocalObservation.CommunityPause.Metadata.ReadStatus == LocalReadStatus.Success &&
        LocalObservation.CommunityPause.Metadata.Freshness == DataFreshness.Fresh;

    private BlockALocalSnapshot LocalObservation
    {
        get => _localObservation;
        set
        {
            if (SetProperty(ref _localObservation, value))
            {
                ClearMutationBlockIfNewerStatus(value.CommunityPause);
                OnPropertyChanged(nameof(DiagnosticsPreview));
                OnPropertyChanged(nameof(InstallationSourceSummary));
                OnPropertyChanged(nameof(BanServiceDetails));
                OnPropertyChanged(nameof(PauseStatusText));
                OnPropertyChanged(nameof(PauseStatusHealth));
                OnPropertyChanged(nameof(PauseModuleVersion));
                OnPropertyChanged(nameof(PauseDetails));
                OnPropertyChanged(nameof(PauseSourceSummary));
                OnPropertyChanged(nameof(CanPauseServer));
                OnPropertyChanged(nameof(CanResumeServer));
                OnPropertyChanged(nameof(RealPauseControlsAvailable));
                OnPropertyChanged(nameof(RealPauseControlsNotice));
                PauseServerCommand.NotifyCanExecuteChanged();
                ResumeServerCommand.NotifyCanExecuteChanged();
                NotifyServerAdministrationAuthorizationChanged();
            }
        }
    }

    public ObservableCollection<InstallationCheckItemViewModel> InstallationChecks { get; } = [];

    public ObservableCollection<ServiceItemViewModel> Services { get; } = [];

    public ObservableCollection<SelectionOption> MapOptions { get; } = [];

    public IReadOnlyList<int> RoundOptions { get; }

    public SelectionOption? SelectedMap
    {
        get => _selectedMap;
        set => SetProperty(ref _selectedMap, value);
    }

    public int SelectedRound
    {
        get => _selectedRound;
        set => SetProperty(ref _selectedRound, value);
    }

    public SimulationResultItemViewModel? LastSimulationResult
    {
        get => _lastSimulationResult;
        private set => SetProperty(ref _lastSimulationResult, value);
    }

    public AsyncRelayCommand<SimulationAction> SimulateServerActionCommand { get; }

    public AsyncRelayCommand PauseServerCommand { get; }

    public AsyncRelayCommand ResumeServerCommand { get; }

    public AsyncRelayCommand RefreshPauseStatusCommand { get; }

    public AsyncRelayCommand HealthDiagnosticCommand { get; }

    public AsyncRelayCommand MapDiagnosticCommand { get; }

    public AsyncRelayCommand PowerDiagnosticCommand { get; }

    public AsyncRelayCommand PackAPunchDiagnosticCommand { get; }

    public AsyncRelayCommand RoundDiagnosticCommand { get; }

    public AsyncRelayCommand PlayersDiagnosticCommand { get; }

    public AsyncRelayCommand MapAuditDiagnosticCommand { get; }

    public AsyncRelayCommand EventStatusDiagnosticCommand { get; }

    public AsyncRelayCommand PowerUpCatalogDiagnosticCommand { get; }

    public AsyncRelayCommand CopyServerDiagnosticCommand { get; }

    public AsyncRelayCommand NextRoundCommand { get; }

    public AsyncRelayCommand SetRoundCommand { get; }

    public AsyncRelayCommand EnablePowerCommand { get; }

    public AsyncRelayCommand EnablePackAPunchCommand { get; }

    public AsyncRelayCommand PlayMapMusicCommand { get; }

    public AsyncRelayCommand StopMapMusicCommand { get; }

    public AsyncRelayCommand UnlockStandardPassagesCommand { get; }

    public AsyncRelayCommand KeepLastZombieCommand { get; }

    public AsyncRelayCommand KillAllZombiesCommand { get; }

    public AsyncRelayCommand MakePowerUpsPermanentCommand { get; }

    public AsyncRelayCommand RestorePowerUpTimeoutCommand { get; }

    public AsyncRelayCommand AcknowledgeServerAdministrationCommand { get; }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        var snapshot = await _snapshotStore.GetSnapshotAsync(cancellationToken);
        Server = snapshot.Server;
        LocalObservation = snapshot.LocalObservation;
        await RefreshMapCatalogAsync(snapshot, cancellationToken);
        Services.Clear();
        foreach (var service in snapshot.Services)
        {
            Services.Add(new ServiceItemViewModel(service));
        }

        InstallationChecks.Clear();
        if (snapshot.LocalObservation.InstallationVerification.Value is { } report)
        {
            foreach (var check in report.Checks)
            {
                InstallationChecks.Add(new InstallationCheckItemViewModel(check));
            }
        }
    }

    private async Task RefreshMapCatalogAsync(
        DashboardSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var preferredCode = _mapSelectionInitialized ? SelectedMap?.Key : snapshot.Server.MapCode;
        var catalog = MapCatalogSnapshot.OfficialOnly;
        if (_mapCatalogService is not null)
        {
            await _mapCatalogService.ObserveMapAsync(
                snapshot.Server.MapCode,
                snapshot.Server.MapName,
                cancellationToken);
            catalog = await _mapCatalogService.GetSnapshotAsync(cancellationToken);
        }

        ReplaceMapOptions(catalog, preferredCode ?? snapshot.Server.MapCode);
        _mapCatalogState?.Update(catalog);
        _mapSelectionInitialized = true;
    }

    private void ReplaceMapOptions(MapCatalogSnapshot catalog, string? preferredCode)
    {
        MapOptions.Clear();
        foreach (var entry in catalog.Entries)
        {
            var label = entry.IsOfficial
                ? entry.DisplayName
                : $"{entry.DisplayName} · CUSTOM";
            MapOptions.Add(new SelectionOption(entry.Code, label));
        }

        if (MapOptions.Count == 0)
        {
            foreach (var entry in OfficialMapCatalog.Entries)
            {
                MapOptions.Add(new SelectionOption(entry.Code, entry.DisplayName));
            }
        }

        SelectedMap = MapOptions.FirstOrDefault(option =>
                          string.Equals(option.Key, preferredCode, StringComparison.OrdinalIgnoreCase))
                      ?? MapOptions[0];
    }

    private async Task SimulateServerActionAsync(SimulationAction action)
    {
        ClearError();
        var option = action switch
        {
            SimulationAction.ChangeMap => SelectedMap?.Key,
            SimulationAction.SetRound => SelectedRound.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SimulationAction.PlayMusic => "musique_1",
            SimulationAction.TriggerEvent => "événement_carte",
            SimulationAction.SpawnBoss => "boss_compatible",
            SimulationAction.SpawnPowerUp => "maxammo",
            _ => null
        };

        var result = await _simulationService.SimulateAsync(new SimulationRequest(action, null, option));
        LastSimulationResult = new SimulationResultItemViewModel(result, "Serveur local simulé");
    }

    private async Task ExecuteCommunityPauseCoreAsync(CommunityPauseAction action)
    {
        if (_pauseCommandService is null || _confirmationService is null ||
            _rconEndpointFactory?.Invoke() is not { } endpoint)
        {
            SetPauseCommandResult(
                "CONFIGURATION REQUISE",
                "Configurez l’adresse, le port et le secret RCON dans Paramètres.",
                false,
                ServiceHealth.Error);
            return;
        }

        var request = action == CommunityPauseAction.Pause
            ? new OperatorConfirmationRequest(
                "Confirmer la mise en pause",
                "Mettre réellement la partie BOIII en pause ?\n\n" +
                "Au moins un joueur vivant doit être connecté et aucun joueur ne doit être à terre. " +
                "La commande agit immédiatement sur la partie.")
            : new OperatorConfirmationRequest(
                "Confirmer la reprise",
                "Reprendre réellement la partie BOIII ?\n\nLa commande agit immédiatement sur la partie.");

        if (!await _confirmationService.ConfirmAsync(request))
        {
            SetPauseCommandResult("ANNULÉ", "Aucune commande envoyée.", false, ServiceHealth.Unknown);
            return;
        }

        var revalidatedSnapshot = await _snapshotStore.RefreshAsync();
        ApplySnapshot(revalidatedSnapshot);
        if (!IsActionAllowed(action))
        {
            SetPauseCommandResult(
                "AUTORISATION EXPIRÉE",
                "Le statut a changé ou n’est plus frais depuis la confirmation. Actualisez le statut avant toute commande.",
                false,
                ServiceHealth.Warning);
            return;
        }

        ClearError();
        var beforeTimestamp = LocalObservation.CommunityPause.SourceTimestampUtc;
        SetPauseCommandResult("ENVOI EN COURS", "Attente de BOIII…", false, ServiceHealth.Unknown);
        CommunityPauseExecutionResult result;
        try
        {
            result = await _pauseCommandService.ExecuteAsync(action, endpoint);
        }
        catch (Exception)
        {
            BlockMutationsUntilNewerStatus(beforeTimestamp);
            SetPauseCommandResult(
                "RÉSULTAT INCERTAIN",
                "Le transport s’est interrompu après le début de l’opération. Actualisez le statut avant toute nouvelle commande.",
                true,
                ServiceHealth.Warning);
            return;
        }

        _operatorActivityStore?.RecordPauseResult(result);
        PauseCommandSent = $"Commande envoyée : {(result.CommandSent ? "Oui" : "Non")}";
        if (result.CommandSent)
        {
            BlockMutationsUntilNewerStatus(beforeTimestamp);
        }

        if (result.Status != CommunityPauseExecutionStatus.SentAwaitingObservation)
        {
            var deliveryIsUncertain = result.CommandSent;
            SetPauseCommandResult(
                deliveryIsUncertain ? "RÉSULTAT INCERTAIN" : "ÉCHEC",
                result.DisplayMessage,
                result.CommandSent,
                deliveryIsUncertain ? ServiceHealth.Warning : ServiceHealth.Error);
            return;
        }

        PauseCommandStatus = "ENVOYÉ · VÉRIFICATION EN COURS";
        PauseCommandMessage = result.DisplayMessage;
        PauseCommandHealth = ServiceHealth.Warning;
        var expectedActive = action == CommunityPauseAction.Pause;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            await Task.Delay(250);
            var snapshot = await _snapshotStore.RefreshAsync();
            ApplySnapshot(snapshot);
            var observation = LocalObservation.CommunityPause;
            if (IsPauseStatusCurrent &&
                observation.SourceTimestampUtc is { } currentTimestamp &&
                (beforeTimestamp is null || currentTimestamp > beforeTimestamp) &&
                observation.Value!.Active == expectedActive)
            {
                SetPauseCommandResult(
                    "CONFIRMÉ PAR LE STATUT LOCAL",
                    expectedActive ? "La partie est maintenant en pause." : "La partie a repris.",
                    true,
                    ServiceHealth.Healthy);
                return;
            }
        }

        SetPauseCommandResult(
            "ENVOYÉ · NON CONFIRMÉ",
            "L’état local attendu n’a pas été observé. Ne répétez pas la commande avant vérification de la console serveur.",
            true,
            ServiceHealth.Warning);
    }

    private async Task RefreshPauseStatusCoreAsync()
    {
        if (_rconDiagnosticService is null || _rconEndpointFactory?.Invoke() is not { } endpoint)
        {
            SetPauseCommandResult(
                "CONFIGURATION REQUISE",
                "Configurez l’adresse, le port et le secret RCON dans Paramètres.",
                false,
                ServiceHealth.Error);
            return;
        }

        if (PauseServerCommand.IsExecuting || ResumeServerCommand.IsExecuting)
        {
            SetPauseCommandResult(
                "ACTION EN COURS",
                "Attendez la fin de la commande Pause ou Reprendre.",
                false,
                ServiceHealth.Warning);
            return;
        }

        var beforeTimestamp = LocalObservation.CommunityPause.SourceTimestampUtc;
        SetPauseCommandResult(
            "ACTUALISATION EN COURS",
            "Demande explicite du statut Community Pause…",
            false,
            ServiceHealth.Unknown);

        var result = await _rconDiagnosticService.ExecuteAsync(RconDiagnosticCommand.PauseStatus, endpoint);
        _operatorActivityStore?.RecordRconResult(result);
        if (!result.CommandSent)
        {
            SetPauseCommandResult("ÉCHEC", result.DisplayResponse, false, ServiceHealth.Error);
            return;
        }

        PauseCommandSent = "Commande envoyée : Oui";
        PauseCommandStatus = "ENVOYÉ · VÉRIFICATION EN COURS";
        PauseCommandMessage = "Attente du nouveau statut local…";
        PauseCommandHealth = ServiceHealth.Warning;

        for (var attempt = 0; attempt < 12; attempt++)
        {
            await Task.Delay(250);
            var snapshot = await _snapshotStore.RefreshAsync();
            ApplySnapshot(snapshot);
            var observation = LocalObservation.CommunityPause;
            if (IsPauseStatusCurrent &&
                observation.SourceTimestampUtc is { } currentTimestamp &&
                (beforeTimestamp is null || currentTimestamp > beforeTimestamp))
            {
                SetPauseCommandResult(
                    "STATUT ACTUALISÉ",
                    observation.Value!.Active
                        ? "La partie est en pause · Reprendre est disponible."
                        : "La partie n’est pas pausée · Mettre en pause est disponible.",
                    true,
                    ServiceHealth.Healthy);
                return;
            }
        }

        SetPauseCommandResult(
            "ENVOYÉ · ÉTAT NON CONFIRMÉ",
            "Aucun nouveau statut local frais n’a été observé. Vérifiez la console avant toute action réelle.",
            true,
            ServiceHealth.Warning);
    }

    private void ApplySnapshot(DashboardSnapshot snapshot)
    {
        Server = snapshot.Server;
        LocalObservation = snapshot.LocalObservation;
        Services.Clear();
        foreach (var service in snapshot.Services)
        {
            Services.Add(new ServiceItemViewModel(service));
        }
    }

    private bool IsActionAllowed(CommunityPauseAction action) =>
        !_pauseMutationAuthorizationBlocked &&
        IsPauseStatusCurrent &&
        action switch
        {
            CommunityPauseAction.Pause =>
                LocalObservation.CommunityPause.Value is { Active: false, ActiveVote: "Aucun" },
            CommunityPauseAction.Resume =>
                LocalObservation.CommunityPause.Value is { Active: true },
            _ => false
        };

    private void BlockMutationsUntilNewerStatus(DateTimeOffset? sourceTimestampUtc)
    {
        _pauseMutationAuthorizationBlocked = true;
        _mutationSafety.Block(OperatorMutationScope.CommunityPause);
        _pauseMutationBlockedAfterTimestamp = sourceTimestampUtc;
        ClearMutationBlockIfNewerStatus(LocalObservation.CommunityPause);
        NotifyPauseAuthorizationChanged();
    }

    private void ClearMutationBlockIfNewerStatus(LocalReadResult<CommunityPauseStatusSnapshot> observation)
    {
        if (!_pauseMutationAuthorizationBlocked ||
            observation.Value is null ||
            observation.Metadata.ReadStatus != LocalReadStatus.Success ||
            observation.Metadata.Freshness != DataFreshness.Fresh ||
            observation.SourceTimestampUtc is not { } currentTimestamp ||
            _pauseMutationBlockedAfterTimestamp is { } blockedAfter && currentTimestamp <= blockedAfter)
        {
            return;
        }

        _pauseMutationAuthorizationBlocked = false;
        _pauseMutationBlockedAfterTimestamp = null;
        _mutationSafety.Clear(OperatorMutationScope.CommunityPause);
    }

    private void NotifyPauseAuthorizationChanged()
    {
        OnPropertyChanged(nameof(CanPauseServer));
        OnPropertyChanged(nameof(CanResumeServer));
        OnPropertyChanged(nameof(RealPauseControlsAvailable));
        OnPropertyChanged(nameof(RealPauseControlsNotice));
        PauseServerCommand.NotifyCanExecuteChanged();
        ResumeServerCommand.NotifyCanExecuteChanged();
        NotifyServerAdministrationAuthorizationChanged();
    }

    private void SetPauseCommandResult(
        string status,
        string message,
        bool commandSent,
        ServiceHealth health)
    {
        PauseCommandStatus = status;
        PauseCommandMessage = message;
        PauseCommandSent = $"Commande envoyée : {(commandSent ? "Oui" : "Non")}";
        PauseCommandHealth = health;
    }

    private AsyncRelayCommand CreateServerDiagnosticCommand(
        RconDiagnosticCommand command,
        string failureMessage) => new(
        () => _rconOperations.RunExclusiveAsync(_ => ExecuteServerDiagnosticCoreAsync(command)),
        null,
        _ => SetServerDiagnosticFailure(failureMessage));

    private async Task ExecuteServerDiagnosticCoreAsync(RconDiagnosticCommand command)
    {
        if (_rconDiagnosticService is null || _rconEndpointFactory?.Invoke() is not { } endpoint)
        {
            SetServerDiagnosticResult(
                "CONFIGURATION REQUISE",
                "Configurez l’adresse, le port et le secret RCON dans Paramètres.",
                false,
                ServiceHealth.Error);
            return;
        }

        SetServerDiagnosticResult(
            "EN COURS",
            "En attente de la réponse BOIII…",
            false,
            ServiceHealth.Unknown);

        var result = await _rconDiagnosticService.ExecuteAsync(command, endpoint);
        _operatorActivityStore?.RecordRconResult(result);
        if (result.Status == RconExecutionStatus.EmptyResponse && result.CommandSent)
        {
            var refreshed = await _snapshotStore.RefreshAsync();
            ApplySnapshot(refreshed);
            if (LocalDiagnosticFallback.TryCreate(command, refreshed, out var fallback))
            {
                SetServerDiagnosticResult(fallback.Status, fallback.Message, true, fallback.Health);
                return;
            }
        }

        SetServerDiagnosticResult(
            result.Status switch
            {
                RconExecutionStatus.Success => "RÉUSSI",
                RconExecutionStatus.SecretMissing => "SECRET REQUIS",
                RconExecutionStatus.InvalidConfiguration => "CONFIGURATION INVALIDE",
                RconExecutionStatus.Timeout => "DÉLAI DÉPASSÉ",
                RconExecutionStatus.EmptyResponse => "ENVOYÉ · SANS TEXTE",
                RconExecutionStatus.UnexpectedResponse => "RÉPONSE NON RECONNUE",
                _ => "ÉCHEC"
            },
            result.DisplayResponse,
            result.CommandSent,
            result.Status switch
            {
                RconExecutionStatus.Success => ServiceHealth.Healthy,
                RconExecutionStatus.Timeout or
                RconExecutionStatus.EmptyResponse or
                RconExecutionStatus.UnexpectedResponse => ServiceHealth.Warning,
                _ => ServiceHealth.Error
            });
    }

    private void SetServerDiagnosticFailure(string message) =>
        SetServerDiagnosticResult("ERREUR", message, false, ServiceHealth.Error);

    private Task CopyServerDiagnosticAsync()
    {
        ServerDiagnosticCopyStatus = _clipboardService?.TrySetText(ServerDiagnosticMessage) == true
            ? "Réponse neutralisée copiée."
            : "Copie impossible : le presse-papiers Windows est momentanément indisponible.";
        return Task.CompletedTask;
    }

    private void SetServerDiagnosticResult(
        string status,
        string message,
        bool commandSent,
        ServiceHealth health)
    {
        ServerDiagnosticStatus = status;
        ServerDiagnosticMessage = message;
        ServerDiagnosticCommandSent = $"Commande envoyée : {(commandSent ? "Oui" : "Non")}";
        ServerDiagnosticHealth = health;
    }

    private AsyncRelayCommand CreateServerAdministrationCommand(
        Func<ServerAdministrationRequest> requestFactory) => new(
        () => _rconOperations.RunExclusiveAsync(
            _ => ExecuteServerAdministrationCoreAsync(requestFactory())),
        () => CanRunServerAdministration,
        _ => SetServerAdministrationResult(
            "ERREUR",
            "L’opération n’a pas pu être préparée. Aucune commande n’est considérée comme envoyée.",
            false,
            ServiceHealth.Error,
            blockMutations: false));

    private async Task ExecuteServerAdministrationCoreAsync(ServerAdministrationRequest request)
    {
        if (_serverAdministrationCommandService is null ||
            _confirmationService is null ||
            _rconEndpointFactory?.Invoke() is not { } endpoint)
        {
            SetServerAdministrationResult(
                "CONFIGURATION REQUISE",
                "Configurez l’adresse, le port et le secret RCON dans Paramètres.",
                false,
                ServiceHealth.Error,
                blockMutations: false);
            return;
        }

        var confirmation = ConfirmationFor(request);
        bool confirmed;
        try
        {
            confirmed = await _confirmationService.ConfirmAsync(confirmation);
        }
        catch (Exception)
        {
            SetServerAdministrationResult(
                "ERREUR DE CONFIRMATION",
                "La confirmation n’a pas pu être affichée. Aucune commande envoyée.",
                false,
                ServiceHealth.Error,
                blockMutations: false);
            return;
        }

        if (!confirmed)
        {
            SetServerAdministrationResult(
                "ANNULÉ",
                "Aucune commande envoyée.",
                false,
                ServiceHealth.Unknown,
                blockMutations: false);
            return;
        }

        SetServerAdministrationResult(
            "ENVOI EN COURS",
            "Attente de BOIII…",
            false,
            ServiceHealth.Unknown,
            blockMutations: false);

        ServerAdministrationExecutionResult result;
        try
        {
            result = await _serverAdministrationCommandService.ExecuteAsync(request, endpoint);
        }
        catch (Exception)
        {
            SetServerAdministrationResult(
                "RÉSULTAT INCERTAIN",
                "Le transport s’est interrompu après le début possible de l’envoi. Vérifiez la console avant toute autre mutation.",
                true,
                ServiceHealth.Warning,
                blockMutations: true);
            return;
        }

        _operatorActivityStore?.RecordServerAdministrationResult(result);
        var status = result.Status switch
        {
            ServerAdministrationExecutionStatus.SentAwaitingManualVerification => "ENVOYÉ · À VÉRIFIER",
            ServerAdministrationExecutionStatus.InvalidRequest => "ACTION REFUSÉE",
            ServerAdministrationExecutionStatus.SecretMissing => "SECRET REQUIS",
            ServerAdministrationExecutionStatus.InvalidConfiguration => "CONFIGURATION INVALIDE",
            ServerAdministrationExecutionStatus.DeliveryUnknown => "RÉSULTAT INCERTAIN",
            _ => result.CommandSent ? "RÉSULTAT INCERTAIN" : "ÉCHEC"
        };
        SetServerAdministrationResult(
            status,
            result.DisplayMessage,
            result.CommandSent,
            result.CommandSent ? ServiceHealth.Warning : ServiceHealth.Error,
            blockMutations: result.CommandSent);
    }

    private Task AcknowledgeServerAdministrationAsync()
    {
        _serverAdministrationMutationBlocked = false;
        _mutationSafety.Clear(OperatorMutationScope.ServerAdministration);
        ServerAdministrationStatus = "VÉRIFICATION MANUELLE CONFIRMÉE";
        ServerAdministrationMessage = "Le verrou est levé. Une nouvelle action exigera une nouvelle confirmation.";
        ServerAdministrationHealth = ServiceHealth.Healthy;
        NotifyServerAdministrationAuthorizationChanged();
        NotifyPauseAuthorizationChanged();
        return Task.CompletedTask;
    }

    private static OperatorConfirmationRequest ConfirmationFor(ServerAdministrationRequest request) =>
        request.Action switch
        {
            ServerAdministrationAction.NextRound => new(
                "Confirmer la fin de manche",
                "Terminer réellement la manche actuelle ?\n\nLes IA vivantes seront éliminées et la partie avancera. Cette action n’est pas annulable."),
            ServerAdministrationAction.SetRound => new(
                "Confirmer le changement de manche",
                $"Avancer réellement jusqu’à la manche {request.TargetRound} ?\n\nLa cible doit être supérieure à la manche actuelle. Cette action n’est pas annulable."),
            ServerAdministrationAction.EnablePower => new(
                "Confirmer l’activation du courant",
                "Activer réellement le courant global ?\n\nLes objectifs propres à la carte peuvent rester incomplets. Cette action n’est pas annulable pour la session."),
            ServerAdministrationAction.EnablePackAPunch => new(
                "Confirmer l’activation du Pack-a-Punch",
                "Activer réellement les machines Pack-a-Punch prises en charge ?\n\nL’accès ou la quête propre à la carte peut rester incomplet."),
            ServerAdministrationAction.PlayMapMusic => new(
                "Confirmer la musique de carte",
                "Lancer réellement la première musique spéciale prise en charge pour tous les joueurs ?"),
            ServerAdministrationAction.StopMapMusic => new(
                "Confirmer l’arrêt de la musique",
                "Arrêter réellement la musique spéciale pour tous les joueurs ?"),
            ServerAdministrationAction.UnlockStandardPassages => new(
                "Confirmer le déverrouillage",
                "Déverrouiller réellement les portes et débris standard pris en charge ?\n\nUn joueur connecté est requis. Les portes de quête ou personnalisées restent exclues."),
            ServerAdministrationAction.KeepLastZombie => new(
                "Confirmer la conservation d’un zombie",
                "Éliminer réellement toutes les IA vivantes sauf une ?\n\nCette action modifie immédiatement la manche."),
            ServerAdministrationAction.KillAllZombies => new(
                "Confirmer l’élimination des zombies",
                "Éliminer réellement toutes les IA vivantes ?\n\nLa manche peut se terminer immédiatement. Cette action n’est pas annulable."),
            ServerAdministrationAction.MakePowerUpsPermanent => new(
                "Confirmer les power-ups permanents",
                "Rendre permanents les futurs power-ups créés par PinteMod pour cette session ?"),
            ServerAdministrationAction.RestorePowerUpTimeout => new(
                "Confirmer le délai normal",
                "Restaurer le délai normal des futurs power-ups créés par PinteMod ?\n\nLes power-ups déjà actifs ne sont pas modifiés."),
            _ => new("Action serveur refusée", "Cette action ne fait pas partie de la liste blanche.")
        };

    private void SetServerAdministrationResult(
        string status,
        string message,
        bool commandSent,
        ServiceHealth health,
        bool blockMutations)
    {
        if (blockMutations)
        {
            _serverAdministrationMutationBlocked = true;
            _mutationSafety.Block(OperatorMutationScope.ServerAdministration);
        }

        ServerAdministrationStatus = status;
        ServerAdministrationMessage = message;
        ServerAdministrationCommandSent = $"Commande envoyée : {(commandSent ? "Oui" : "Non")}";
        ServerAdministrationHealth = health;
        NotifyServerAdministrationAuthorizationChanged();
        OnPropertyChanged(nameof(CanPauseServer));
        OnPropertyChanged(nameof(CanResumeServer));
        OnPropertyChanged(nameof(RealPauseControlsNotice));
        PauseServerCommand.NotifyCanExecuteChanged();
        ResumeServerCommand.NotifyCanExecuteChanged();
    }

    private void NotifyServerAdministrationAuthorizationChanged()
    {
        OnPropertyChanged(nameof(CanRunServerAdministration));
        OnPropertyChanged(nameof(ServerAdministrationNotice));
        NextRoundCommand.NotifyCanExecuteChanged();
        SetRoundCommand.NotifyCanExecuteChanged();
        EnablePowerCommand.NotifyCanExecuteChanged();
        EnablePackAPunchCommand.NotifyCanExecuteChanged();
        PlayMapMusicCommand.NotifyCanExecuteChanged();
        StopMapMusicCommand.NotifyCanExecuteChanged();
        UnlockStandardPassagesCommand.NotifyCanExecuteChanged();
        KeepLastZombieCommand.NotifyCanExecuteChanged();
        KillAllZombiesCommand.NotifyCanExecuteChanged();
        MakePowerUpsPermanentCommand.NotifyCanExecuteChanged();
        RestorePowerUpTimeoutCommand.NotifyCanExecuteChanged();
        AcknowledgeServerAdministrationCommand.NotifyCanExecuteChanged();
    }

    private void NotifyAllMutationAuthorizationChanged()
    {
        NotifyPauseAuthorizationChanged();
        NotifyServerAdministrationAuthorizationChanged();
    }
}
