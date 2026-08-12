using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.State;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class PlayersViewModel : PlayerActionsViewModelBase
{
    private string _sourceSummary = "Joueurs simulés";
    private bool _isHybridLocal;
    private bool _runtimePlayersAvailable;

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

    public string AlivePlayerCountDisplay => !_isHybridLocal || _runtimePlayersAvailable
        ? AlivePlayerCount.ToString()
        : "—";

    public string EmptyMessage => _isHybridLocal
        ? "Aucun joueur n’est disponible dans les sources locales de la session active."
        : "Le snapshot simulé ne contient aucun joueur connecté.";

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        var snapshot = await SnapshotStore.GetSnapshotAsync(cancellationToken);
        ConfigurePlayerDataContext(snapshot);
        ReplacePlayers(snapshot.Players);
        _isHybridLocal = snapshot.DataContext.Mode == ControlCenterDataMode.HybridLocal;
        _runtimePlayersAvailable = snapshot.LocalObservation.RuntimeSnapshot.Value is not null &&
                                   snapshot.LocalObservation.RuntimeSnapshot.Metadata.ReadStatus == LocalReadStatus.Success &&
                                   snapshot.LocalObservation.RuntimeSnapshot.Metadata.Freshness == DataFreshness.Fresh &&
                                   snapshot.LocalObservation.RuntimeSnapshot.Metadata.Provenance == DataProvenance.LocalFile;
        SourceSummary = _isHybridLocal
            ? _runtimePlayersAvailable
                ? $"Runtime PinteMod local · {snapshot.Players.Count} joueur(s) · identité XUID abrégée · vie, points et inventaire observables"
                : $"Présence locale de repli · {snapshot.Players.Count} joueur(s) · identité XUID abrégée · détails runtime indisponibles"
            : "Présence et fiches entièrement simulées";
        OnPropertyChanged(nameof(AlivePlayerCountDisplay));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}
