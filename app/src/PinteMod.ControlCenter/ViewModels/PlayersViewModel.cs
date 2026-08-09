using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.State;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class PlayersViewModel : PlayerActionsViewModelBase
{
    private string _sourceSummary = "Joueurs simulés";
    private bool _isHybridLocal;

    public PlayersViewModel(
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
            "Joueurs",
            "Présence et fiche détaillée — le pseudo reste réservé à l'affichage",
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

    public string SourceSummary
    {
        get => _sourceSummary;
        private set => SetProperty(ref _sourceSummary, value);
    }

    public string AlivePlayerCountDisplay => _isHybridLocal ? "—" : AlivePlayerCount.ToString();

    public string EmptyMessage => _isHybridLocal
        ? "Aucune présence JOIN/LEAVE active n’est disponible pour la session locale."
        : "Le snapshot simulé ne contient aucun joueur connecté.";

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        var snapshot = await SnapshotStore.GetSnapshotAsync(cancellationToken);
        ConfigurePlayerDataContext(snapshot);
        ReplacePlayers(snapshot.Players);
        _isHybridLocal = snapshot.DataContext.Mode == ControlCenterDataMode.HybridLocal;
        SourceSummary = _isHybridLocal
            ? $"Présence inférée par JOIN/LEAVE · {snapshot.Players.Count} joueur(s) · XUID abrégés · points, vie et inventaire non disponibles"
            : "Présence et fiches entièrement simulées";
        OnPropertyChanged(nameof(AlivePlayerCountDisplay));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}
