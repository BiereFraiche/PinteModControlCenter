using System.Collections.ObjectModel;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.State;

namespace PinteMod.ControlCenter.ViewModels;

public abstract class PlayerActionsViewModelBase : PageViewModel
{
    private static readonly HashSet<SimulationAction> RealActionMappings =
    [
        SimulationAction.RevivePlayer,
        SimulationAction.RespawnPlayer,
        SimulationAction.GrantPoints,
        SimulationAction.RefillAmmo,
        SimulationAction.ToggleGodmode,
        SimulationAction.GiveWeapon,
        SimulationAction.GivePerk,
        SimulationAction.GiveAllPerks,
        SimulationAction.GivePowerUpPlayer,
        SimulationAction.TeleportPlayer,
        SimulationAction.MutePlayer,
        SimulationAction.UnmutePlayer,
        SimulationAction.KickPlayer,
        SimulationAction.BanPlayer,
        SimulationAction.ChangeRole,
        SimulationAction.RemoveRole
    ];

    private readonly PlayerSelectionState _selectionState;
    private readonly Dictionary<PlayerItemViewModel, string> _playerXuids = [];
    private readonly IPlayerAdministrationCommandService? _playerAdministrationService;
    private readonly IOperatorConfirmationService? _confirmationService;
    private readonly Func<RconEndpoint?>? _rconEndpointFactory;
    private readonly IOperatorActivityStore? _operatorActivityStore;
    private readonly IOperatorRconOperationCoordinator _rconOperations;
    private readonly OperatorMutationSafetyState _mutationSafety;
    private readonly IPlayerModerationHistoryReader? _playerHistoryReader;
    private PlayerItemViewModel? _selectedPlayer;
    private SimulationResultItemViewModel? _lastSimulationResult;
    private bool _synchronizingSelection;
    private bool _isHybridLocal;
    private bool _localPlayerSourceReady;
    private string _playerAdministrationStatus = "AUCUNE ACTION JOUEUR RÉELLE";
    private string _playerAdministrationMessage = "Sélectionnez un joueur issu des logs locaux, puis confirmez une action.";
    private string _playerAdministrationCommandSent = "Commande envoyée : Non";
    private ServiceHealth _playerAdministrationHealth = ServiceHealth.Unknown;
    private bool _hasPlayerAdministrationResult;
    private SelectionOption _selectedWeapon;
    private SelectionOption _selectedPerk;
    private SelectionOption _selectedPowerUp;
    private SelectionOption _selectedBanDuration;
    private SelectionOption _selectedRole;
    private int _selectedPointsAmount = 5000;
    private bool _hasPlayerHistoryResult;
    private string _playerHistoryStatus = "HISTORIQUE NON CHARGÉ";
    private string _playerHistoryCounters = "—";
    private string _playerHistoryLastAction = "Sélectionnez un joueur puis chargez son historique local.";
    private ServiceHealth _playerHistoryHealth = ServiceHealth.Unknown;

    protected PlayerActionsViewModelBase(
        string title,
        string description,
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
        : base(title, description)
    {
        SnapshotStore = snapshotStore;
        SimulationService = simulationService;
        _selectionState = selectionState;
        _playerAdministrationService = playerAdministrationService;
        _confirmationService = confirmationService;
        _rconEndpointFactory = rconEndpointFactory;
        _operatorActivityStore = operatorActivityStore;
        _rconOperations = rconOperations ?? new OperatorRconOperationCoordinator();
        _mutationSafety = mutationSafety ?? new OperatorMutationSafetyState();
        _playerHistoryReader = playerHistoryReader;
        _selectionState.SelectionChanged += SelectionState_SelectionChanged;
        _mutationSafety.Changed += (_, _) => NotifyPlayerActionAuthorizationChanged();

        WeaponOptions =
        [
            new("raygun", "Ray Gun"),
            new("raygunmk2", "Ray Gun Mark II"),
            new("kn44", "KN-44"),
            new("haymaker", "Haymaker"),
            new("dingo", "Dingo")
        ];
        PerkOptions =
        [
            new("jug", "Juggernog"),
            new("quick", "Quick Revive"),
            new("speed", "Speed Cola"),
            new("doubletap", "Double Tap"),
            new("staminup", "Stamin-Up"),
            new("deadshot", "Deadshot"),
            new("mule", "Mule Kick"),
            new("cherry", "Electric Cherry"),
            new("widows", "Widow's Wine")
        ];
        PowerUpOptions =
        [
            new("maxammo", "Max Ammo"),
            new("instakill", "Insta-Kill"),
            new("doublepoints", "Double Points"),
            new("firesale", "Fire Sale"),
            new("carpenter", "Carpenter"),
            new("nuke", "Nuke"),
            new("deathmachine", "Death Machine"),
            new("freeperk", "Free Perk"),
            new("shield", "Shield Charge")
        ];
        PointsOptions = [1000, 5000, 10000, 50000];
        BanDurationOptions =
        [
            new("30m", "30 minutes"),
            new("2h", "2 heures"),
            new("7d", "7 jours"),
            new("4w", "4 semaines"),
            new("perm", "Permanent")
        ];
        RoleOptions =
        [
            new("helper", "Helper"),
            new("moderator", "Modérateur"),
            new("admin", "Administrateur")
        ];
        _selectedWeapon = WeaponOptions[0];
        _selectedPerk = PerkOptions[0];
        _selectedPowerUp = PowerUpOptions[0];
        _selectedBanDuration = BanDurationOptions[0];
        _selectedRole = RoleOptions[0];

        PlayerActionCommand = new AsyncRelayCommand<SimulationAction>(
            action => _rconOperations.RunExclusiveAsync(_ => ExecutePlayerActionCoreAsync(action)),
            CanExecutePlayerAction,
            _ => SetPlayerAdministrationResult(
                "RÉSULTAT INCERTAIN",
                "L’opération s’est interrompue après le début possible de l’envoi. Vérifiez la partie ou la console.",
                true,
                ServiceHealth.Warning,
                blockMutations: true));
        SimulatePlayerActionCommand = PlayerActionCommand;
        AcknowledgePlayerAdministrationCommand = new AsyncRelayCommand(
            AcknowledgePlayerAdministrationAsync,
            () => _mutationSafety.IsBlocked(OperatorMutationScope.PlayerAdministration),
            ReportError);
        LoadPlayerHistoryCommand = new AsyncRelayCommand(
            LoadPlayerHistoryAsync,
            () => CanLoadPlayerHistory,
            _ => SetPlayerHistoryFailure("L’historique local n’a pas pu être chargé."));
    }

    protected IControlCenterSnapshotStore SnapshotStore { get; }

    protected ISimulationActionService SimulationService { get; }

    public ObservableCollection<PlayerItemViewModel> Players { get; } = [];

    public IReadOnlyList<SelectionOption> WeaponOptions { get; }

    public IReadOnlyList<SelectionOption> PerkOptions { get; }

    public IReadOnlyList<SelectionOption> PowerUpOptions { get; }

    public IReadOnlyList<int> PointsOptions { get; }

    public IReadOnlyList<SelectionOption> BanDurationOptions { get; }

    public IReadOnlyList<SelectionOption> RoleOptions { get; }

    public PlayerItemViewModel? SelectedPlayer
    {
        get => _selectedPlayer;
        set
        {
            var previousXuid = GetPlayerXuid(_selectedPlayer);
            var nextXuid = GetPlayerXuid(value);
            if (!SetProperty(ref _selectedPlayer, value))
            {
                return;
            }

            if (!_synchronizingSelection)
            {
                _selectionState.Select(GetPlayerXuid(value));
            }

            if (!string.Equals(previousXuid, nextXuid, StringComparison.OrdinalIgnoreCase))
            {
                ResetPlayerHistory();
            }

            NotifyPlayerActionAuthorizationChanged();
        }
    }

    public SelectionOption SelectedWeapon
    {
        get => _selectedWeapon;
        set => SetProperty(ref _selectedWeapon, value);
    }

    public SelectionOption SelectedPerk
    {
        get => _selectedPerk;
        set => SetProperty(ref _selectedPerk, value);
    }

    public SelectionOption SelectedPowerUp
    {
        get => _selectedPowerUp;
        set => SetProperty(ref _selectedPowerUp, value);
    }

    public int SelectedPointsAmount
    {
        get => _selectedPointsAmount;
        set => SetProperty(ref _selectedPointsAmount, value);
    }

    public SelectionOption SelectedBanDuration
    {
        get => _selectedBanDuration;
        set => SetProperty(ref _selectedBanDuration, value);
    }

    public SelectionOption SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    public SimulationResultItemViewModel? LastSimulationResult
    {
        get => _lastSimulationResult;
        private set => SetProperty(ref _lastSimulationResult, value);
    }

    public string PlayerAdministrationStatus
    {
        get => _playerAdministrationStatus;
        private set => SetProperty(ref _playerAdministrationStatus, value);
    }

    public string PlayerAdministrationMessage
    {
        get => _playerAdministrationMessage;
        private set => SetProperty(ref _playerAdministrationMessage, value);
    }

    public string PlayerAdministrationCommandSent
    {
        get => _playerAdministrationCommandSent;
        private set => SetProperty(ref _playerAdministrationCommandSent, value);
    }

    public ServiceHealth PlayerAdministrationHealth
    {
        get => _playerAdministrationHealth;
        private set => SetProperty(ref _playerAdministrationHealth, value);
    }

    public bool HasPlayerAdministrationResult
    {
        get => _hasPlayerAdministrationResult;
        private set => SetProperty(ref _hasPlayerAdministrationResult, value);
    }

    public string PlayerActionsBadge => _isHybridLocal ? "ACTIONS RÉELLES · XUID" : "ACTIONS SIMULÉES";

    public string PlayerActionsNotice
    {
        get
        {
            if (!_isHybridLocal)
            {
                return "Simulation active · aucune commande serveur n’est envoyée.";
            }

            if (!_localPlayerSourceReady)
            {
                return "Actions verrouillées · présence locale JOIN/LEAVE valide requise.";
            }

            if (_playerAdministrationService is null || _confirmationService is null || _rconEndpointFactory?.Invoke() is null)
            {
                return "Actions verrouillées · configurez le RCON dans Paramètres.";
            }

            if (_mutationSafety.IsAnyBlocked)
            {
                return "Actions verrouillées · une vérification manuelle est encore attendue.";
            }

            return "Prêt · ciblage strict par BOIII_XUID · confirmation obligatoire · aucun retry.";
        }
    }

    public AsyncRelayCommand<SimulationAction> PlayerActionCommand { get; }

    public AsyncRelayCommand<SimulationAction> SimulatePlayerActionCommand { get; }

    public AsyncRelayCommand AcknowledgePlayerAdministrationCommand { get; }

    public AsyncRelayCommand LoadPlayerHistoryCommand { get; }

    public bool CanLoadPlayerHistory =>
        _isHybridLocal && _localPlayerSourceReady && _playerHistoryReader is not null && SelectedPlayer is not null;

    public bool HasPlayerHistoryResult
    {
        get => _hasPlayerHistoryResult;
        private set => SetProperty(ref _hasPlayerHistoryResult, value);
    }

    public string PlayerHistoryStatus
    {
        get => _playerHistoryStatus;
        private set => SetProperty(ref _playerHistoryStatus, value);
    }

    public string PlayerHistoryCounters
    {
        get => _playerHistoryCounters;
        private set => SetProperty(ref _playerHistoryCounters, value);
    }

    public string PlayerHistoryLastAction
    {
        get => _playerHistoryLastAction;
        private set => SetProperty(ref _playerHistoryLastAction, value);
    }

    public ServiceHealth PlayerHistoryHealth
    {
        get => _playerHistoryHealth;
        private set => SetProperty(ref _playerHistoryHealth, value);
    }

    protected void ConfigurePlayerDataContext(DashboardSnapshot snapshot)
    {
        _isHybridLocal = snapshot.DataContext.Mode == ControlCenterDataMode.HybridLocal;
        _localPlayerSourceReady = _isHybridLocal &&
                                  snapshot.DataContext.SessionSource.ReadStatus == LocalReadStatus.Success &&
                                  snapshot.DataContext.SessionSource.Provenance == DataProvenance.LocalFile &&
                                  snapshot.LocalObservation.Logs.Source.ReadStatus == LocalReadStatus.Success &&
                                  snapshot.LocalObservation.Logs.Source.Provenance == DataProvenance.LocalFile;
        OnPropertyChanged(nameof(PlayerActionsBadge));
        OnPropertyChanged(nameof(PlayerActionsNotice));
        NotifyPlayerActionAuthorizationChanged();
    }

    protected void ReplacePlayers(IEnumerable<PlayerState> players)
    {
        var requestedXuid = _selectionState.SelectedXuid ?? GetPlayerXuid(SelectedPlayer);
        Players.Clear();
        _playerXuids.Clear();

        foreach (var player in players)
        {
            var item = new PlayerItemViewModel(player);
            Players.Add(item);
            _playerXuids.Add(item, player.Xuid);
        }

        var replacement = Players.FirstOrDefault(player =>
                string.Equals(GetPlayerXuid(player), requestedXuid, StringComparison.OrdinalIgnoreCase))
            ?? Players.FirstOrDefault();

        _synchronizingSelection = true;
        try
        {
            SelectedPlayer = replacement;
        }
        finally
        {
            _synchronizingSelection = false;
        }

        _selectionState.Select(GetPlayerXuid(replacement));
        OnPropertyChanged(nameof(PlayerCount));
        OnPropertyChanged(nameof(AlivePlayerCount));
        OnPropertyChanged(nameof(HasPlayers));
        NotifyPlayerActionAuthorizationChanged();
    }

    public int PlayerCount => Players.Count;

    public int AlivePlayerCount => Players.Count(player => player.LifeState == PlayerLifeState.Alive);

    public bool HasPlayers => Players.Count > 0;

    private void SelectionState_SelectionChanged(object? sender, EventArgs e)
    {
        _synchronizingSelection = true;
        try
        {
            SelectedPlayer = Players.FirstOrDefault(player =>
                string.Equals(
                    GetPlayerXuid(player),
                    _selectionState.SelectedXuid,
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _synchronizingSelection = false;
        }
    }

    private bool CanExecutePlayerAction(SimulationAction action)
    {
        if (SelectedPlayer is null)
        {
            return false;
        }

        if (!_isHybridLocal)
        {
            return true;
        }

        return RealActionMappings.Contains(action) &&
               _localPlayerSourceReady &&
               _playerAdministrationService is not null &&
               _confirmationService is not null &&
               _rconEndpointFactory?.Invoke() is not null &&
               !_mutationSafety.IsAnyBlocked;
    }

    private Task ExecutePlayerActionCoreAsync(SimulationAction action) =>
        _isHybridLocal
            ? ExecuteRealPlayerActionAsync(action)
            : SimulatePlayerActionAsync(action);

    private async Task SimulatePlayerActionAsync(SimulationAction action)
    {
        var target = SelectedPlayer;
        var targetXuid = GetPlayerXuid(target);
        if (target is null || targetXuid is null)
        {
            return;
        }

        ClearError();
        var option = action switch
        {
            SimulationAction.GrantPoints => $"+{SelectedPointsAmount:N0}",
            SimulationAction.GiveWeapon => SelectedWeapon.Key,
            SimulationAction.GivePerk => SelectedPerk.Key,
            SimulationAction.GivePowerUpPlayer => SelectedPowerUp.Key,
            SimulationAction.BanPlayer => SelectedBanDuration.Key,
            SimulationAction.ChangeRole => SelectedRole.Key,
            _ => null
        };

        var result = await SimulationService.SimulateAsync(
            new SimulationRequest(action, targetXuid, option));
        LastSimulationResult = new SimulationResultItemViewModel(result, target.DisplayName);
        HasPlayerAdministrationResult = false;
    }

    private async Task ExecuteRealPlayerActionAsync(SimulationAction action)
    {
        var target = SelectedPlayer;
        var targetXuid = GetPlayerXuid(target);
        if (target is null || targetXuid is null ||
            _playerAdministrationService is null || _confirmationService is null ||
            _rconEndpointFactory?.Invoke() is not { } endpoint ||
            !TryCreateRealRequest(action, targetXuid, out var request))
        {
            SetPlayerAdministrationResult(
                "ACTION REFUSÉE",
                "Cette action n’est pas disponible en mode réel ou sa configuration est incomplète.",
                false,
                ServiceHealth.Error,
                blockMutations: false);
            return;
        }

        if (!await _confirmationService.ConfirmAsync(ConfirmationFor(request, target.DisplayName)))
        {
            SetPlayerAdministrationResult("ANNULÉ", "Aucune commande envoyée.", false, ServiceHealth.Unknown, false);
            return;
        }

        var refreshed = await SnapshotStore.RefreshAsync();
        ConfigurePlayerDataContext(refreshed);
        var refreshedTarget = refreshed.Players.FirstOrDefault(player =>
            string.Equals(player.Xuid, targetXuid, StringComparison.OrdinalIgnoreCase) &&
            player.Provenance == DataProvenance.LocalFile);
        if (!_localPlayerSourceReady || refreshedTarget is null)
        {
            ReplacePlayers(refreshed.Players);
            SetPlayerAdministrationResult(
                "AUTORISATION EXPIRÉE",
                "Le joueur ou la source locale a changé depuis la confirmation. Aucune commande envoyée.",
                false,
                ServiceHealth.Warning,
                blockMutations: false);
            return;
        }

        SetPlayerAdministrationResult("ENVOI EN COURS", "Attente de BOIII…", false, ServiceHealth.Unknown, false);
        PlayerAdministrationExecutionResult result;
        try
        {
            result = await _playerAdministrationService.ExecuteAsync(request, endpoint);
        }
        catch (Exception)
        {
            SetPlayerAdministrationResult(
                "RÉSULTAT INCERTAIN",
                "Le transport s’est interrompu après le début possible de l’envoi. Vérifiez la partie ou la console.",
                true,
                ServiceHealth.Warning,
                blockMutations: true);
            return;
        }

        _operatorActivityStore?.RecordPlayerAdministrationResult(result);
        var status = result.Status switch
        {
            PlayerAdministrationExecutionStatus.SentAwaitingManualVerification => "ENVOYÉ · À VÉRIFIER",
            PlayerAdministrationExecutionStatus.InvalidRequest => "ACTION REFUSÉE",
            PlayerAdministrationExecutionStatus.SecretMissing => "SECRET REQUIS",
            PlayerAdministrationExecutionStatus.InvalidConfiguration => "CONFIGURATION INVALIDE",
            PlayerAdministrationExecutionStatus.DeliveryUnknown => "RÉSULTAT INCERTAIN",
            _ => result.CommandSent ? "RÉSULTAT INCERTAIN" : "ÉCHEC"
        };
        SetPlayerAdministrationResult(
            status,
            result.DisplayMessage,
            result.CommandSent,
            result.CommandSent ? ServiceHealth.Warning : ServiceHealth.Error,
            blockMutations: result.CommandSent);
    }

    private bool TryCreateRealRequest(
        SimulationAction action,
        string xuid,
        out PlayerAdministrationRequest request)
    {
        request = action switch
        {
            SimulationAction.RevivePlayer => new(PlayerAdministrationAction.Revive, xuid),
            SimulationAction.RespawnPlayer => new(PlayerAdministrationAction.Respawn, xuid),
            SimulationAction.GrantPoints => new(PlayerAdministrationAction.GrantPoints, xuid, SelectedPointsAmount),
            SimulationAction.RefillAmmo => new(PlayerAdministrationAction.RefillAmmo, xuid),
            SimulationAction.ToggleGodmode => new(PlayerAdministrationAction.ToggleGodMode, xuid),
            SimulationAction.GiveWeapon => new(PlayerAdministrationAction.GiveWeapon, xuid, Option: SelectedWeapon.Key),
            SimulationAction.GivePerk => new(PlayerAdministrationAction.GivePerk, xuid, Option: SelectedPerk.Key),
            SimulationAction.GiveAllPerks => new(PlayerAdministrationAction.GiveAllPerks, xuid),
            SimulationAction.GivePowerUpPlayer => new(PlayerAdministrationAction.GivePowerUp, xuid, Option: SelectedPowerUp.Key),
            SimulationAction.TeleportPlayer => new(PlayerAdministrationAction.TeleportToOwnAim, xuid),
            SimulationAction.MutePlayer => new(PlayerAdministrationAction.Mute, xuid),
            SimulationAction.UnmutePlayer => new(PlayerAdministrationAction.Unmute, xuid),
            SimulationAction.KickPlayer => new(PlayerAdministrationAction.Kick, xuid),
            SimulationAction.BanPlayer => new(PlayerAdministrationAction.Ban, xuid, Option: SelectedBanDuration.Key),
            SimulationAction.ChangeRole => new(PlayerAdministrationAction.SetRole, xuid, Option: SelectedRole.Key),
            SimulationAction.RemoveRole => new(PlayerAdministrationAction.RemoveRole, xuid),
            _ => new((PlayerAdministrationAction)int.MaxValue, xuid)
        };
        return RealActionMappings.Contains(action);
    }

    private static OperatorConfirmationRequest ConfirmationFor(
        PlayerAdministrationRequest request,
        string displayName)
    {
        var target = string.IsNullOrWhiteSpace(displayName) ? "le joueur sélectionné" : displayName;
        return request.Action switch
        {
            PlayerAdministrationAction.Revive => new("Confirmer le revive", $"Réanimation réelle de {target} ?"),
            PlayerAdministrationAction.Respawn => new("Confirmer le respawn", $"Réapparition réelle de {target} ? Cette action vise les spectateurs."),
            PlayerAdministrationAction.GrantPoints => new("Confirmer les points", $"Ajouter réellement {request.PointsAmount:N0} points à {target} ?"),
            PlayerAdministrationAction.RefillAmmo => new("Confirmer les munitions", $"Remplir réellement les munitions de l’arme équipée de {target} ?"),
            PlayerAdministrationAction.ToggleGodMode => new("Confirmer le Godmode", $"Basculer réellement le Godmode de {target} ? L’état actuel n’est pas connu du Control Center."),
            PlayerAdministrationAction.GiveWeapon => new("Confirmer l’arme", $"Donner réellement l’arme « {request.Option} » à {target} ?"),
            PlayerAdministrationAction.GivePerk => new("Confirmer l’atout", $"Donner réellement l’atout « {request.Option} » à {target} ?"),
            PlayerAdministrationAction.GiveAllPerks => new("Confirmer tous les atouts", $"Donner réellement tous les atouts classiques à {target} ?"),
            PlayerAdministrationAction.GivePowerUp => new("Confirmer le power-up", $"Faire apparaître réellement le power-up « {request.Option} » au viseur de {target} ?\n\nLa disponibilité dépend de la carte active."),
            PlayerAdministrationAction.TeleportToOwnAim => new("Confirmer la téléportation", $"Téléporter réellement {target} vers la surface actuellement visée par ce même joueur ?\n\nLe joueur doit être vivant et viser une surface proche valide."),
            PlayerAdministrationAction.Mute => new("Confirmer le mute", $"Muter réellement {target} ?\n\nPinteMod enregistrera l’état et l’historique de modération."),
            PlayerAdministrationAction.Unmute => new("Confirmer le unmute", $"Retirer réellement le mute de {target} ?"),
            PlayerAdministrationAction.Kick => new("Confirmer l’expulsion", $"Expulser immédiatement {target} de la partie ?\n\nCette action est destructive et non annulable."),
            PlayerAdministrationAction.Ban => new("Confirmer le bannissement", $"Bannir réellement {target} pour la durée « {request.Option} » ?\n\nPinteMod créera une demande de ban persistante et le joueur pourra être expulsé."),
            PlayerAdministrationAction.SetRole => new("Confirmer le rôle", $"Attribuer réellement le rôle « {request.Option} » à {target} ?\n\nLe rôle sera enregistré par PinteMod."),
            PlayerAdministrationAction.RemoveRole => new("Confirmer le retrait du rôle", $"Retirer réellement le rôle privilégié de {target} ?"),
            _ => new("Action joueur refusée", "Cette action ne fait pas partie de la liste blanche réelle.")
        };
    }

    private Task AcknowledgePlayerAdministrationAsync()
    {
        _mutationSafety.Clear(OperatorMutationScope.PlayerAdministration);
        PlayerAdministrationStatus = "VÉRIFICATION MANUELLE CONFIRMÉE";
        PlayerAdministrationMessage = "Le verrou est levé. Une nouvelle action exigera une nouvelle confirmation.";
        PlayerAdministrationHealth = ServiceHealth.Healthy;
        return Task.CompletedTask;
    }

    private async Task LoadPlayerHistoryAsync()
    {
        var xuid = GetPlayerXuid(SelectedPlayer);
        if (_playerHistoryReader is null || xuid is null)
        {
            SetPlayerHistoryFailure("Une présence locale XUID et une source hybride sont requises.");
            return;
        }

        var result = await _playerHistoryReader.ReadAsync(xuid);
        HasPlayerHistoryResult = true;
        if (result.Value is { } history && result.Metadata.ReadStatus == LocalReadStatus.Success)
        {
            PlayerHistoryStatus = "HISTORIQUE LOCAL · READ-ONLY";
            PlayerHistoryCounters =
                $"Kicks {history.Kicks} · Mutes {history.Mutes} · Bans temporaires {history.TemporaryBans} · Bans permanents {history.PermanentBans} · Unbans {history.Unbans}";
            PlayerHistoryLastAction = $"Dernière action : {history.LastAction} · {history.LastReason}";
            PlayerHistoryHealth = ServiceHealth.Healthy;
            return;
        }

        PlayerHistoryStatus = result.Metadata.ReadStatus == LocalReadStatus.Missing
            ? "AUCUN HISTORIQUE LOCAL"
            : "HISTORIQUE INDISPONIBLE";
        PlayerHistoryCounters = "Aucun compteur disponible.";
        PlayerHistoryLastAction = result.Metadata.Message;
        PlayerHistoryHealth = result.Metadata.ReadStatus == LocalReadStatus.Missing
            ? ServiceHealth.Unknown
            : ServiceHealth.Error;
    }

    private void ResetPlayerHistory()
    {
        HasPlayerHistoryResult = false;
        PlayerHistoryStatus = "HISTORIQUE NON CHARGÉ";
        PlayerHistoryCounters = "—";
        PlayerHistoryLastAction = "Chargez uniquement l’historique local du joueur sélectionné.";
        PlayerHistoryHealth = ServiceHealth.Unknown;
    }

    private void SetPlayerHistoryFailure(string message)
    {
        HasPlayerHistoryResult = true;
        PlayerHistoryStatus = "HISTORIQUE INDISPONIBLE";
        PlayerHistoryCounters = "Aucun compteur disponible.";
        PlayerHistoryLastAction = message;
        PlayerHistoryHealth = ServiceHealth.Error;
    }

    private void SetPlayerAdministrationResult(
        string status,
        string message,
        bool commandSent,
        ServiceHealth health,
        bool blockMutations)
    {
        if (blockMutations)
        {
            _mutationSafety.Block(OperatorMutationScope.PlayerAdministration);
        }

        LastSimulationResult = null;
        HasPlayerAdministrationResult = true;
        PlayerAdministrationStatus = status;
        PlayerAdministrationMessage = message;
        PlayerAdministrationCommandSent = $"Commande envoyée : {(commandSent ? "Oui" : "Non")}";
        PlayerAdministrationHealth = health;
        NotifyPlayerActionAuthorizationChanged();
    }

    private void NotifyPlayerActionAuthorizationChanged()
    {
        OnPropertyChanged(nameof(PlayerActionsNotice));
        PlayerActionCommand?.NotifyCanExecuteChanged();
        AcknowledgePlayerAdministrationCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanLoadPlayerHistory));
        LoadPlayerHistoryCommand?.NotifyCanExecuteChanged();
    }

    private string? GetPlayerXuid(PlayerItemViewModel? player) =>
        player is not null && _playerXuids.TryGetValue(player, out var xuid)
            ? xuid
            : null;
}
