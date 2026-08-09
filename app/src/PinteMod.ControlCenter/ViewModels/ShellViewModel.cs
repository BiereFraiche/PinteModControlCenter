using System.Collections.ObjectModel;
using System.Windows.Threading;
using PinteMod.ControlCenter.Core.Contracts;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly IControlCenterSnapshotStore _snapshotStore;
    private readonly SettingsViewModel _settings;
    private readonly DispatcherTimer? _clock;
    private PageViewModel _currentPage;
    private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
    private string? _globalErrorMessage;

    public ShellViewModel(
        IControlCenterSnapshotStore snapshotStore,
        DashboardViewModel dashboard,
        PlayersViewModel players,
        ServerViewModel server,
        RecordsViewModel records,
        LogsViewModel logs,
        SettingsViewModel settings,
        bool startClock = true)
    {
        _snapshotStore = snapshotStore;
        _settings = settings;
        NavigationItems =
        [
            new("DB", dashboard),
            new("JR", players),
            new("SV", server),
            new("RC", records),
            new("LG", logs),
            new("PR", settings)
        ];

        _currentPage = dashboard;
        NavigationItems[0].IsSelected = true;
        NavigateCommand = new RelayCommand<NavigationItemViewModel>(Navigate);
        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync, null, ReportError);

        if (startClock)
        {
            _clock = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clock.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            _clock.Start();
        }
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public RelayCommand<NavigationItemViewModel> NavigateCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public string ModeLabel => _settings.ModeLabel;

    public string ModeShortLabel => _settings.ModeShortLabel;

    public string ModeDescription => _settings.ModeDescription;

    public string ReadOnlyFooterLabel => _settings.DataMode == "HYBRIDE LOCAL"
        ? "DONNÉES LOCALES NEUTRALISÉES · COMMANDES RCON CONFIRMÉES"
        : "ACTIONS GAMEPLAY SIMULÉES · DIAGNOSTIC RCON MANUEL";

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentTime
    {
        get => _currentTime;
        private set => SetProperty(ref _currentTime, value);
    }

    public string? GlobalErrorMessage
    {
        get => _globalErrorMessage;
        private set => SetProperty(ref _globalErrorMessage, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        GlobalErrorMessage = null;
        await _snapshotStore.GetSnapshotAsync(cancellationToken);
        await InitializePagesAsync(cancellationToken);
    }

    public bool NavigateTo(string pageTitle)
    {
        var item = NavigationItems.FirstOrDefault(candidate =>
            string.Equals(candidate.Title, pageTitle, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return false;
        }

        Navigate(item);
        return true;
    }

    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        GlobalErrorMessage = "Initialisation partielle : une source locale n’a pas pu être actualisée.";
    }

    public void ReportConfigurationNotice(string message)
    {
        GlobalErrorMessage = message;
    }

    public Task ApplyCurrentSnapshotAsync(CancellationToken cancellationToken = default)
    {
        GlobalErrorMessage = null;
        return InitializePagesAsync(cancellationToken);
    }

    private async Task RefreshAllAsync()
    {
        GlobalErrorMessage = null;
        await _snapshotStore.RefreshAsync();
        await InitializePagesAsync();
    }

    private async Task InitializePagesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var item in NavigationItems)
        {
            await item.Page.InitializeAsync(cancellationToken);
        }
    }

    private void Navigate(NavigationItemViewModel item)
    {
        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, item);
        }

        CurrentPage = item.Page;
    }
}

public sealed class NavigationItemViewModel(string glyph, PageViewModel page) : ObservableObject
{
    private bool _isSelected;

    public string Glyph { get; } = glyph;

    public string Title => Page.Title;

    public PageViewModel Page { get; } = page;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
