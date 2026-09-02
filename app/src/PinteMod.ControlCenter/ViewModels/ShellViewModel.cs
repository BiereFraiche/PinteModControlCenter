using System.IO;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

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
        bool startClock = true,
        PlayerChatViewModel? playerChat = null,
        bool restrictUnprovedCapabilities = false,
        ServerIntegrationProfile? integrationProfile = null)
    {
        _snapshotStore = snapshotStore;
        _settings = settings;
        var adaptiveProfile = integrationProfile ?? ServerIntegrationProfile.Unknown;
        var useAdaptiveCapabilities = adaptiveProfile.Kind != ManagedServerIntegrationKind.Unknown;
        var structuredDataAvailable = !restrictUnprovedCapabilities ||
                                      string.Equals(settings.DataMode, "HYBRIDE LOCAL", StringComparison.Ordinal);
        var playersAvailable = useAdaptiveCapabilities
            ? adaptiveProfile.Supports(IntegrationCapabilityKey.Players)
            : structuredDataAvailable;
        var recordsAvailable = useAdaptiveCapabilities
            ? adaptiveProfile.Supports(IntegrationCapabilityKey.Records)
            : structuredDataAvailable;
        var chatAvailable = useAdaptiveCapabilities
            ? adaptiveProfile.Supports(IntegrationCapabilityKey.Chat)
            : structuredDataAvailable;
        var navigationItems = new List<NavigationItemViewModel>
        {
            new("DB", dashboard, true, "Vue d’ensemble du serveur"),
            new("JR", players, playersAvailable, playersAvailable ? "Joueurs disponibles" : "Indisponible : aucune source joueurs structurée n’est prouvée par le provider détecté."),
            new("SV", server, true, "Paramètres et actions serveur"),
            new("RC", records, recordsAvailable, recordsAvailable ? "Records disponibles" : "Indisponible : aucun contrat records compatible n’est prouvé."),
            new("LG", logs, true, "Journaux et événements disponibles")
        };
        if (playerChat is not null)
        {
            navigationItems.Add(new NavigationItemViewModel(
                "CH",
                playerChat,
                chatAvailable,
                chatAvailable ? "Chat disponible" : "Indisponible : aucune source chat structurée n’est prouvée par le provider détecté."));
        }

        navigationItems.Add(new NavigationItemViewModel("PR", settings, true, "Paramètres du Control Center"));
        NavigationItems = new ObservableCollection<NavigationItemViewModel>(navigationItems);

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

    public string ReadOnlyFooterLabel => _settings.ModeShortLabel == "ADP"
        ? "INTÉGRATION ADAPTATIVE · CAPACITÉS FAIL-CLOSED · AUCUNE COMMANDE TIERCE DEVINÉE"
        : _settings.DataMode == "HYBRIDE LOCAL"
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
        if (string.Equals(_settings.DataMode, "HYBRIDE LOCAL", StringComparison.Ordinal))
        {
            await RefreshSnapshotAtStartupAsync(cancellationToken);
        }
        else
        {
            await _snapshotStore.GetSnapshotAsync(cancellationToken);
        }
        await InitializePagesAsync(cancellationToken);
        if (string.Equals(_settings.DataMode, "HYBRIDE LOCAL", StringComparison.Ordinal) &&
            GlobalErrorMessage is not null)
        {
            _ = RecoverHybridSnapshotAfterStartupAsync(cancellationToken);
        }
    }

    private async Task RefreshSnapshotAtStartupAsync(CancellationToken cancellationToken)
    {
        Exception? lastSourceError = null;
        var delays = new[] { 0, 250, 750, 1500 };
        foreach (var delay in delays)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (delay > 0) await Task.Delay(delay, cancellationToken);
            try
            {
                await _snapshotStore.RefreshAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                lastSourceError = exception;
            }
        }

        // A transient file/SMB race must not crash the first Control Center open.
        // Keep the last valid/cached snapshot and let the monitor/manual refresh recover.
        await _snapshotStore.GetSnapshotAsync(cancellationToken);
        if (lastSourceError is not null)
        {
            GlobalErrorMessage = "Connexion en cours : certaines données PinteMod seront réessayées automatiquement.";
        }
    }

    private async Task RecoverHybridSnapshotAfterStartupAsync(CancellationToken cancellationToken)
    {
        foreach (var delay in new[] { 1500, 3000, 5000 })
        {
            try
            {
                await Task.Delay(delay, cancellationToken);
                await _snapshotStore.RefreshAsync(cancellationToken);
                await InitializePagesAsync(cancellationToken);
                GlobalErrorMessage = null;
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                // BOIII/PinteMod may still be creating its atomic runtime files.
            }
        }
    }

    public bool NavigateTo(string pageTitle)
    {
        var item = NavigationItems.FirstOrDefault(candidate =>
            string.Equals(candidate.Title, pageTitle, StringComparison.OrdinalIgnoreCase));
        if (item is null || !item.IsEnabled)
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
        // Settings contain static configuration and embedded-payload checks. They
        // do not need to be reloaded every two seconds with live player data.
        return InitializePagesAsync(cancellationToken, includeSettings: false);
    }

    private async Task RefreshAllAsync()
    {
        GlobalErrorMessage = null;
        await _snapshotStore.RefreshAsync();
        await InitializePagesAsync();
    }

    private async Task InitializePagesAsync(CancellationToken cancellationToken = default, bool includeSettings = true)
    {
        foreach (var item in NavigationItems)
        {
            if (!includeSettings && ReferenceEquals(item.Page, _settings))
            {
                continue;
            }

            await item.Page.InitializeAsync(cancellationToken);
        }
    }

    private void Navigate(NavigationItemViewModel item)
    {
        if (!item.IsEnabled)
        {
            return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, item);
        }

        CurrentPage = item.Page;
    }
}

public sealed class NavigationItemViewModel(
    string glyph,
    PageViewModel page,
    bool isEnabled = true,
    string? availabilityHint = null) : ObservableObject
{
    private bool _isSelected;

    public string Glyph { get; } = glyph;

    public string Title => Page.Title;

    public PageViewModel Page { get; } = page;

    public bool IsEnabled { get; } = isEnabled;

    public string AvailabilityHint { get; } = availabilityHint ?? page.Description;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
