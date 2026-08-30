using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class ControlCenterWorkspaceViewModel : ObservableObject
{
    private readonly Func<Task<ServerTabViewModel>> _addServer;
    private readonly Func<ServerTabViewModel, Task<bool>> _removeServer;
    private readonly Func<string, Task> _activeServerChanged;
    private readonly Func<bool, Task> _displayModeChanged;
    private readonly Func<string, Task> _languageChanged;
    private ServerTabViewModel _activeServer;
    private string? _workspaceNotice;
    private bool _advancedMode;
    private string _selectedUiLanguage;

    public ControlCenterWorkspaceViewModel(
        IEnumerable<ServerTabViewModel> servers,
        string? activeProfileId,
        Func<Task<ServerTabViewModel>> addServer,
        Func<ServerTabViewModel, Task<bool>> removeServer,
        Func<string, Task> activeServerChanged,
        bool advancedMode = false,
        Func<bool, Task>? displayModeChanged = null,
        string uiLanguageCode = "fr-FR",
        Func<string, Task>? languageChanged = null)
    {
        ArgumentNullException.ThrowIfNull(servers);
        _addServer = addServer ?? throw new ArgumentNullException(nameof(addServer));
        _removeServer = removeServer ?? throw new ArgumentNullException(nameof(removeServer));
        _activeServerChanged = activeServerChanged ?? throw new ArgumentNullException(nameof(activeServerChanged));
        _displayModeChanged = displayModeChanged ?? (_ => Task.CompletedTask);
        _languageChanged = languageChanged ?? (_ => Task.CompletedTask);
        _advancedMode = advancedMode;
        _selectedUiLanguage = NormalizeUiLanguage(uiLanguageCode);
        Servers = new ObservableCollection<ServerTabViewModel>(servers);
        if (Servers.Count == 0)
        {
            throw new ArgumentException("Au moins un profil serveur est requis.", nameof(servers));
        }

        _activeServer = Servers.FirstOrDefault(server =>
                            string.Equals(server.ProfileId, activeProfileId, StringComparison.Ordinal))
                        ?? Servers[0];
        foreach (var server in Servers)
        {
            server.PropertyChanged += OnServerPropertyChanged;
        }
        UpdateActiveFlags();
        SelectServerCommand = new AsyncRelayCommand<ServerTabViewModel>(
            SelectServerAsync,
            null,
            _ => WorkspaceNotice = "L’onglet actif n’a pas pu être mémorisé.");
        AddServerCommand = new AsyncRelayCommand(
            AddServerAsync,
            () => Servers.Count < OperatorWorkspaceConfiguration.MaximumProfileCount,
            _ => WorkspaceNotice = "Le nouvel onglet serveur n’a pas pu être créé.");
        RemoveServerCommand = new AsyncRelayCommand<ServerTabViewModel>(
            RemoveServerAsync,
            _ => Servers.Count > 1,
            _ => WorkspaceNotice = "L’onglet serveur n’a pas pu être retiré.");
        ToggleDisplayModeCommand = new AsyncRelayCommand(
            ToggleDisplayModeAsync,
            null,
            _ => WorkspaceNotice = "Le mode d’affichage n’a pas pu être mémorisé.");
    }

    public ObservableCollection<ServerTabViewModel> Servers { get; }

    public AsyncRelayCommand<ServerTabViewModel> SelectServerCommand { get; }

    public AsyncRelayCommand AddServerCommand { get; }

    public AsyncRelayCommand<ServerTabViewModel> RemoveServerCommand { get; }

    public AsyncRelayCommand ToggleDisplayModeCommand { get; }

    public IReadOnlyList<UiLanguageOption> UiLanguages { get; } =
    [
        new("fr-FR", "🇫🇷", "Français"),
        new("en-US", "🇬🇧", "English")
    ];

    public string SelectedUiLanguage
    {
        get => _selectedUiLanguage;
        private set => SetProperty(ref _selectedUiLanguage, value);
    }

    public bool AdvancedMode
    {
        get => _advancedMode;
        private set
        {
            if (SetProperty(ref _advancedMode, value))
            {
                OnPropertyChanged(nameof(SimpleMode));
                OnPropertyChanged(nameof(DisplayModeButtonLabel));
            }
        }
    }

    public bool SimpleMode => !AdvancedMode;

    public string DisplayModeButtonLabel => AdvancedMode ? "MODE SIMPLE" : "MODE AVANCÉ";

    public ServerTabViewModel ActiveServer
    {
        get => _activeServer;
        private set
        {
            if (SetProperty(ref _activeServer, value))
            {
                UpdateActiveFlags();
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string WindowTitle => $"PinteMod Control Center · {ActiveServer.DisplayName}";

    public string? WorkspaceNotice
    {
        get => _workspaceNotice;
        private set => SetProperty(ref _workspaceNotice, value);
    }

    private async Task ToggleDisplayModeAsync()
    {
        AdvancedMode = !AdvancedMode;
        await _displayModeChanged(AdvancedMode);
    }

    public async Task SetUiLanguageAsync(string? languageCode)
    {
        var normalized = NormalizeUiLanguage(languageCode);
        if (string.Equals(SelectedUiLanguage, normalized, StringComparison.Ordinal))
        {
            return;
        }

        SelectedUiLanguage = normalized;
        var culture = CultureInfo.GetCultureInfo(normalized);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        await _languageChanged(normalized);
        WorkspaceNotice = normalized == "en-US"
            ? "Language saved. Full English interface will apply progressively as translations are added."
            : "Langue enregistrée. L’interface française reste la référence actuelle.";
    }

    private static string NormalizeUiLanguage(string? code) =>
        string.Equals(code, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "fr-FR";

    private async Task SelectServerAsync(ServerTabViewModel server)
    {
        if (!Servers.Contains(server))
        {
            return;
        }

        WorkspaceNotice = null;
        ActiveServer = server;
        await _activeServerChanged(server.ProfileId);
    }

    private async Task AddServerAsync()
    {
        WorkspaceNotice = null;
        var server = await _addServer();
        server.PropertyChanged += OnServerPropertyChanged;
        Servers.Add(server);
        ActiveServer = server;
        NotifyServerCountChanged();
        await _activeServerChanged(server.ProfileId);
    }

    private async Task RemoveServerAsync(ServerTabViewModel server)
    {
        if (Servers.Count <= 1 || !Servers.Contains(server))
        {
            return;
        }

        WorkspaceNotice = null;
        if (!await _removeServer(server))
        {
            return;
        }

        var removedIndex = Servers.IndexOf(server);
        var wasActive = ReferenceEquals(server, ActiveServer);
        server.PropertyChanged -= OnServerPropertyChanged;
        Servers.Remove(server);
        if (wasActive)
        {
            ActiveServer = Servers[Math.Min(removedIndex, Servers.Count - 1)];
            await _activeServerChanged(ActiveServer.ProfileId);
        }

        NotifyServerCountChanged();
    }

    private void NotifyServerCountChanged()
    {
        AddServerCommand.NotifyCanExecuteChanged();
        RemoveServerCommand.NotifyCanExecuteChanged();
    }

    private void UpdateActiveFlags()
    {
        foreach (var server in Servers)
        {
            server.IsActive = ReferenceEquals(server, ActiveServer);
        }
    }

    private void OnServerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, ActiveServer) &&
            string.Equals(e.PropertyName, nameof(ServerTabViewModel.DisplayName), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }
}

public sealed record UiLanguageOption(string Code, string Flag, string Label)
{
    public string DisplayName => $"{Flag} {Label}";
}

public sealed class ServerTabViewModel : ObservableObject
{
    private string _displayName;
    private string _accentColorKey;
    private bool _isActive;

    public ServerTabViewModel(
        string profileId,
        string displayName,
        ShellViewModel shell,
        string accentColorKey = OperatorAccentTheme.DefaultKey)
    {
        if (!JsonProfileIdRules.IsValid(profileId))
        {
            throw new ArgumentException("Identifiant de profil serveur invalide.", nameof(profileId));
        }

        ProfileId = profileId;
        _displayName = NormalizeDisplayName(displayName);
        _accentColorKey = OperatorAccentTheme.NormalizeOrDefault(accentColorKey);
        Shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public string ProfileId { get; }

    public ShellViewModel Shell { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, NormalizeDisplayName(value));
    }

    public string AccentColorKey
    {
        get => _accentColorKey;
        set
        {
            if (SetProperty(ref _accentColorKey, OperatorAccentTheme.NormalizeOrDefault(value)))
            {
                OnPropertyChanged(nameof(AccentPreviewBrush));
            }
        }
    }

    public System.Windows.Media.Brush AccentPreviewBrush =>
        AccentThemeService.Resolve(AccentColorKey).PreviewBrush;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private static string NormalizeDisplayName(string? value)
    {
        var normalized = value?.Trim();
        return !OperatorConfiguration.IsValidProfileDisplayName(normalized)
            ? OperatorConfiguration.DefaultProfileDisplayName
            : normalized!;
    }

    private static class JsonProfileIdRules
    {
        public static bool IsValid(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 40 &&
            char.IsLetterOrDigit(value[0]) &&
            value.All(character =>
                character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }
}
