using System.Collections.ObjectModel;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class LogsViewModel : PageViewModel
{
    private readonly IControlCenterSnapshotStore _snapshotStore;
    private readonly IOperatorActivityStore? _operatorActivityStore;
    private readonly ITextClipboardService? _clipboardService;
    private readonly List<LiveEvent> _source = [];
    private string _searchText = string.Empty;
    private string _selectedFilter = "TOUS";
    private StructuredLogSnapshot _logSnapshot = StructuredLogSnapshot.Empty("—", LocalSourceMetadata.Simulation());
    private LocalSourceMetadata _pauseLogSource = LocalSourceMetadata.Unavailable("Journal Community Pause non lu.");
    private bool _isHybridLocal;
    private bool _isDisplayPaused;
    private bool _autoScrollEnabled = true;
    private int _pendingEventCount;
    private string _clipboardStatus = "Aucune copie effectuée.";

    public LogsViewModel(
        IControlCenterSnapshotStore snapshotStore,
        IOperatorActivityStore? operatorActivityStore = null,
        ITextClipboardService? clipboardService = null)
        : base("Logs", "Flux d’événements simulé, filtrable et strictement en lecture seule")
    {
        _snapshotStore = snapshotStore;
        _operatorActivityStore = operatorActivityStore;
        _clipboardService = clipboardService;
        Filters = new[] { "TOUS", "JOUEURS", "SYSTÈME", "PAUSE", "RANKS", "EASTER EGG", "MODÉRATION", "LANGUE", "RCON", "ALERTES" }
            .Select(key => new FilterOptionViewModel(key))
            .ToArray();
        SelectFilterCommand = new RelayCommand<FilterOptionViewModel>(SelectFilter);
        ToggleDisplayPauseCommand = new AsyncRelayCommand(ToggleDisplayPauseAsync);
        CopyVisibleEventsCommand = new AsyncRelayCommand(
            CopyVisibleEventsAsync,
            () => CanCopyVisibleEvents);
        UpdateSelectedFilterItems();
    }

    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    public IReadOnlyList<FilterOptionViewModel> Filters { get; }

    public RelayCommand<FilterOptionViewModel> SelectFilterCommand { get; }

    public AsyncRelayCommand ToggleDisplayPauseCommand { get; }

    public AsyncRelayCommand CopyVisibleEventsCommand { get; }

    public bool CanCopyVisibleEvents => _clipboardService is not null && Events.Count > 0;

    public string ClipboardStatus
    {
        get => _clipboardStatus;
        private set => SetProperty(ref _clipboardStatus, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        private set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                UpdateSelectedFilterItems();
            }
        }
    }

    public bool HasEvents => Events.Count > 0;

    public bool IsDisplayPaused
    {
        get => _isDisplayPaused;
        private set
        {
            if (SetProperty(ref _isDisplayPaused, value))
            {
                OnPropertyChanged(nameof(PauseButtonLabel));
                OnPropertyChanged(nameof(DisplayStateLabel));
            }
        }
    }

    public bool AutoScrollEnabled
    {
        get => _autoScrollEnabled;
        set => SetProperty(ref _autoScrollEnabled, value);
    }

    public int PendingEventCount
    {
        get => _pendingEventCount;
        private set
        {
            if (SetProperty(ref _pendingEventCount, value))
            {
                OnPropertyChanged(nameof(DisplayStateLabel));
            }
        }
    }

    public string PauseButtonLabel => IsDisplayPaused ? "REPRENDRE" : "PAUSE AFFICHAGE";

    public string DisplayStateLabel => IsDisplayPaused
        ? $"AFFICHAGE EN PAUSE · {PendingEventCount} NOUVEAU(X)"
        : "FLUX ACTIF";

    public string SourceSummary => _isHybridLocal
        ? $"Session locale active · lecture {DisplayText.ReadStatus(_logSnapshot.Source.ReadStatus)} · " +
          $"{_logSnapshot.FilesScanned} source(s) · pause {DisplayText.ReadStatus(_pauseLogSource.ReadStatus)} · " +
          $"{_logSnapshot.LinesIgnored} ignorée(s) · {_logSnapshot.MalformedLines} malformée(s)"
        : "Source entièrement simulée";

    public string SourceLabel => _isHybridLocal
        ? _pauseLogSource.ReadStatus == LocalReadStatus.Success
            ? "logs/sessions/<session-active> + logs/pause.log"
            : "logs/sessions/<session-active>"
        : _logSnapshot.Source.SourceLabel;

    public string FreshnessSummary => _isHybridLocal
        ? $"Fraîcheur {DisplayText.Freshness(_logSnapshot.Source.Freshness)} · âge {DisplayText.FormatAge(_logSnapshot.Source.Age)} · " +
          $"pause {DisplayText.Freshness(_pauseLogSource.Freshness)} · cache mémoire {_logSnapshot.CachedEventCount} événement(s)"
        : "Actualisation simulée";

    public string RefreshLabel => _isHybridLocal ? "ACTUALISATION LOCALE · 2 S" : "ACTUALISATION SIMULÉE";

    public string SearchToolTip => _isHybridLocal
        ? "Filtrer uniquement les champs déjà neutralisés"
        : "Filtrer le flux simulé";

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        var snapshot = await _snapshotStore.GetSnapshotAsync(cancellationToken);
        _isHybridLocal = snapshot.DataContext.Mode == ControlCenterDataMode.HybridLocal;
        Description = _isHybridLocal
            ? "Flux d’événements locaux neutralisés, filtrable et strictement en lecture seule"
            : "Flux d’événements simulé, filtrable et strictement en lecture seule";
        _logSnapshot = snapshot.LocalObservation.Logs;
        _pauseLogSource = snapshot.LocalObservation.CommunityPauseLogSource;
        var combinedEvents = (_operatorActivityStore?.GetSnapshot() ?? [])
            .Concat(snapshot.Events)
            .ToArray();
        if (IsDisplayPaused)
        {
            PendingEventCount = combinedEvents.Count(item => !_source.Contains(item));
            NotifySourceProperties();
            return;
        }

        _source.Clear();
        _source.AddRange(combinedEvents);
        PendingEventCount = 0;
        ApplyFilter();
        NotifySourceProperties();
    }

    private void SelectFilter(FilterOptionViewModel filter)
    {
        SelectedFilter = filter.Key;
        ApplyFilter();
    }

    private async Task ToggleDisplayPauseAsync()
    {
        IsDisplayPaused = !IsDisplayPaused;
        if (!IsDisplayPaused)
        {
            PendingEventCount = 0;
            await InitializeAsync();
        }
    }

    private Task CopyVisibleEventsAsync()
    {
        if (_clipboardService is null || Events.Count == 0)
        {
            ClipboardStatus = "Aucun événement visible à copier.";
            return Task.CompletedTask;
        }

        var lines = new List<string>
        {
            $"PinteMod Control Center · filtre {SelectedFilter} · {Events.Count} événement(s) visible(s)"
        };
        lines.AddRange(Events.Select(item =>
            $"{item.Time} | {item.Category} | {item.Title} | {item.Details}"));
        ClipboardStatus = _clipboardService.TrySetText(string.Join(Environment.NewLine, lines))
            ? $"{Events.Count} événement(s) neutralisé(s) copié(s)."
            : "Copie impossible : le presse-papiers Windows est momentanément indisponible.";
        return Task.CompletedTask;
    }

    private void NotifySourceProperties()
    {
        OnPropertyChanged(nameof(SourceSummary));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(FreshnessSummary));
        OnPropertyChanged(nameof(RefreshLabel));
        OnPropertyChanged(nameof(SearchToolTip));
    }

    private void UpdateSelectedFilterItems()
    {
        foreach (var filter in Filters)
        {
            filter.IsSelected = filter.Key == SelectedFilter;
        }
    }

    private void ApplyFilter()
    {
        var filtered = _source.Where(item =>
            (SelectedFilter == "TOUS" || string.Equals(item.Category, SelectedFilter, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(SearchText) ||
             item.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             item.Details.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

        Events.Clear();
        foreach (var item in filtered)
        {
            Events.Add(new EventItemViewModel(item));
        }

        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(CanCopyVisibleEvents));
        CopyVisibleEventsCommand.NotifyCanExecuteChanged();
    }
}
