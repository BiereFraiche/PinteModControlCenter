using System.IO;
using System.Collections.ObjectModel;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;
using PinteMod.ControlCenter.Services;
using PinteMod.ControlCenter.State;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class SettingsViewModel : PageViewModel
{
    private readonly ILocalDataSourceProbe? _localDataSourceProbe;
    private readonly IOperatorConfigurationStore? _configurationStore;
    private readonly IRconDiagnosticService? _rconDiagnosticService;
    private readonly IRconSecretStore? _rconSecretStore;
    private readonly IOperatorActivityStore? _operatorActivityStore;
    private readonly IOperatorRconOperationCoordinator _rconOperations;
    private readonly IMapCatalogService? _mapCatalogService;
    private readonly MapCatalogState? _mapCatalogState;
    private readonly ITextClipboardService? _clipboardService;
    private readonly IControlCenterSnapshotStore? _snapshotStore;
    private string _profileDisplayName;
    private string _selectedOperatorMode;
    private string _operatorServerRoot;
    private bool _activateDataSourceOnStartup;
    private string _configurationSaveStatus = "NON ENREGISTRÉ";
    private string _configurationSaveMessage = "Le mode simulé reste utilisé tant que l’activation n’est pas enregistrée.";
    private string? _lastAcceptedProbeSignature;
    private string _rconAddress;
    private string _rconPortText;
    private string _rconSecretStatus = "NON CONFIGURÉ";
    private string _rconTestStatus = "NON TESTÉ";
    private string _rconResponse = "Aucune commande RCON envoyée.";
    private string _rconCommandSent = "Commande envoyée : Non";
    private ServiceHealth _rconTestHealth = ServiceHealth.Unknown;
    private string _rconCopyStatus = "Aucune copie effectuée.";
    private bool _rconSecretStateInitialized;
    private string _dataSourceTestStatus = "NON TESTÉ";
    private string _dataSourceTestMessage = "Indiquez une racine locale ou LAN, puis lancez un test read-only.";
    private ServiceHealth _dataSourceTestHealth = ServiceHealth.Unknown;
    private string _mapRotationLine = string.Empty;
    private string _manualMapCode = string.Empty;
    private string _manualMapName = string.Empty;
    private MapCatalogItemViewModel? _selectedMapCatalogEntry;
    private string _mapCatalogStatus = "CATALOGUE OFFICIEL";
    private string _mapCatalogMessage = "14 cartes officielles disponibles · ajoutez les customs localement ou importez une ligne de rotation.";
    private ServiceHealth _mapCatalogHealth = ServiceHealth.Unknown;

    public SettingsViewModel(
        ControlCenterDataMode dataMode = ControlCenterDataMode.Simulation,
        string activeProviderName = "SimulatedControlCenterDataProvider",
        string? serverRoot = null,
        TimeSpan? automaticRefreshInterval = null,
        ILocalDataSourceProbe? localDataSourceProbe = null,
        IOperatorConfigurationStore? configurationStore = null,
        OperatorConfiguration? initialConfiguration = null,
        IRconDiagnosticService? rconDiagnosticService = null,
        IRconSecretStore? rconSecretStore = null,
        IOperatorActivityStore? operatorActivityStore = null,
        IOperatorRconOperationCoordinator? rconOperations = null,
        IMapCatalogService? mapCatalogService = null,
        MapCatalogState? mapCatalogState = null,
        ITextClipboardService? clipboardService = null,
        IControlCenterSnapshotStore? snapshotStore = null)
        : base("Paramètres", "Configuration opérateur locale ou LAN · diagnostics RCON manuels")
    {
        _localDataSourceProbe = localDataSourceProbe;
        _configurationStore = configurationStore;
        _rconDiagnosticService = rconDiagnosticService;
        _rconSecretStore = rconSecretStore;
        _operatorActivityStore = operatorActivityStore;
        _rconOperations = rconOperations ?? new OperatorRconOperationCoordinator();
        _mapCatalogService = mapCatalogService;
        _mapCatalogState = mapCatalogState;
        _clipboardService = clipboardService;
        _snapshotStore = snapshotStore;
        var configuration = initialConfiguration ?? OperatorConfiguration.Default;
        _profileDisplayName = configuration.ProfileDisplayName;
        var configuredRoot = serverRoot ?? configuration.ServerRoot;
        _selectedOperatorMode = serverRoot is not null
            ? IsUnc(serverRoot) ? "LAN" : "LOCAL"
            : configuration.DataLocation == OperatorDataLocation.Lan ? "LAN" : "LOCAL";
        _operatorServerRoot = configuredRoot;
        _activateDataSourceOnStartup = configuration.ActivateDataSourceOnStartup;
        _rconAddress = configuration.RconAddress;
        _rconPortText = configuration.RconPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

        AutomaticRefresh = automaticRefreshInterval is not null;
        AutomaticRefreshStatus = automaticRefreshInterval is null
            ? "À VENIR"
            : $"ACTIF · {automaticRefreshInterval.Value.TotalSeconds:0} S";
        ActiveProviderName = activeProviderName;
        DataMode = dataMode == ControlCenterDataMode.HybridLocal ? "HYBRIDE LOCAL" : "SIMULATION";
        ModeLabel = dataMode == ControlCenterDataMode.HybridLocal ? "MODE HYBRIDE LOCAL" : "MODE SIMULATION";
        ModeShortLabel = dataMode == ControlCenterDataMode.HybridLocal ? "HYB" : "SIM";
        ModeDescription = dataMode == ControlCenterDataMode.HybridLocal
            ? "Lecture locale read-only · diagnostics RCON sur action explicite"
            : "Données simulées · diagnostics RCON sur action explicite";
        ServerRootDisplay = FormatServerRoot(serverRoot);
        DataScope = dataMode == ControlCenterDataMode.HybridLocal
            ? "Session et heartbeats, Ranks, records, Easter Eggs, logs structurés, diagnostics et métadonnées joueur locales"
            : "Toutes les données sont simulées";

        TestDataSourceCommand = new AsyncRelayCommand(
            TestDataSourceAsync,
            () => CanTestDataSource,
            _ => SetProbeFailure("Le test read-only n’a pas pu être terminé."));
        SaveConfigurationCommand = new AsyncRelayCommand(
            SaveConfigurationAsync,
            () => CanSaveConfiguration,
            _ => SetConfigurationFailure("La configuration locale n’a pas pu être enregistrée."));
        TestRconHealthCommand = new AsyncRelayCommand(
            () => _rconOperations.RunExclusiveAsync(
                _ => ExecuteRconDiagnosticCoreAsync(RconDiagnosticCommand.HealthFull)),
            () => CanRunRconDiagnostic,
            _ => SetRconFailure("Le diagnostic Health n’a pas pu être terminé."));
        TestRconPauseCommand = new AsyncRelayCommand(
            () => _rconOperations.RunExclusiveAsync(
                _ => ExecuteRconDiagnosticCoreAsync(RconDiagnosticCommand.PauseStatus)),
            () => CanRunRconDiagnostic,
            _ => SetRconFailure("Le diagnostic Pause n’a pas pu être terminé."));
        TestRconMapCommand = CreateRconDiagnosticCommand(
            RconDiagnosticCommand.MapInfo,
            "Le diagnostic Carte n’a pas pu être terminé.");
        TestRconPowerCommand = CreateRconDiagnosticCommand(
            RconDiagnosticCommand.PowerStatus,
            "Le diagnostic Courant n’a pas pu être terminé.");
        TestRconPackAPunchCommand = CreateRconDiagnosticCommand(
            RconDiagnosticCommand.PackAPunchStatus,
            "Le diagnostic Pack-a-Punch n’a pas pu être terminé.");
        TestRconRoundCommand = CreateRconDiagnosticCommand(
            RconDiagnosticCommand.RoundStatus,
            "Le diagnostic Manche n’a pas pu être terminé.");
        TestRconPlayersCommand = CreateRconDiagnosticCommand(
            RconDiagnosticCommand.Players,
            "Le diagnostic Joueurs n’a pas pu être terminé.");
        TestRconMapAuditCommand = CreateRconDiagnosticCommand(
            RconDiagnosticCommand.MapAudit,
            "L’audit de compatibilité de la carte n’a pas pu être terminé.");
        TestRconEventStatusCommand = CreateRconDiagnosticCommand(
            RconDiagnosticCommand.EventStatus,
            "Le diagnostic Événements n’a pas pu être terminé.");
        TestRconPowerUpCatalogCommand = CreateRconDiagnosticCommand(
            RconDiagnosticCommand.PowerUpCatalog,
            "Le catalogue des power-ups n’a pas pu être interrogé.");
        CopyRconResponseCommand = new AsyncRelayCommand(
            CopyRconResponseAsync,
            () => CanCopyRconResponse);
        ImportMapRotationCommand = new AsyncRelayCommand(
            ImportMapRotationAsync,
            () => CanImportMapRotation,
            _ => SetMapCatalogFailure("La ligne de rotation n’a pas pu être traitée."));
        AddManualMapCommand = new AsyncRelayCommand(
            AddManualMapAsync,
            () => CanAddManualMap,
            _ => SetMapCatalogFailure("La carte custom n’a pas pu être ajoutée."));
        RemoveManualMapCommand = new AsyncRelayCommand(
            RemoveManualMapAsync,
            () => CanRemoveManualMap,
            _ => SetMapCatalogFailure("La carte custom n’a pas pu être retirée."));
    }

    public IReadOnlyList<string> OperatorModes { get; } = ["LOCAL", "LAN"];

    public event Action<string>? ProfileDisplayNameSaved;

    public AsyncRelayCommand TestDataSourceCommand { get; }

    public AsyncRelayCommand SaveConfigurationCommand { get; }

    public AsyncRelayCommand TestRconHealthCommand { get; }

    public AsyncRelayCommand TestRconPauseCommand { get; }

    public AsyncRelayCommand TestRconMapCommand { get; }

    public AsyncRelayCommand TestRconPowerCommand { get; }

    public AsyncRelayCommand TestRconPackAPunchCommand { get; }

    public AsyncRelayCommand TestRconRoundCommand { get; }

    public AsyncRelayCommand TestRconPlayersCommand { get; }

    public AsyncRelayCommand TestRconMapAuditCommand { get; }

    public AsyncRelayCommand TestRconEventStatusCommand { get; }

    public AsyncRelayCommand TestRconPowerUpCatalogCommand { get; }

    public AsyncRelayCommand CopyRconResponseCommand { get; }

    public AsyncRelayCommand ImportMapRotationCommand { get; }

    public AsyncRelayCommand AddManualMapCommand { get; }

    public AsyncRelayCommand RemoveManualMapCommand { get; }

    public ObservableCollection<MapCatalogItemViewModel> MapCatalogEntries { get; } = [];

    public string MapRotationLine
    {
        get => _mapRotationLine;
        set
        {
            if (SetProperty(ref _mapRotationLine, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanImportMapRotation));
                ImportMapRotationCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ManualMapCode
    {
        get => _manualMapCode;
        set
        {
            if (SetProperty(ref _manualMapCode, value ?? string.Empty))
            {
                NotifyManualMapCommandState();
            }
        }
    }

    public string ManualMapName
    {
        get => _manualMapName;
        set
        {
            if (SetProperty(ref _manualMapName, value ?? string.Empty))
            {
                NotifyManualMapCommandState();
            }
        }
    }

    public MapCatalogItemViewModel? SelectedMapCatalogEntry
    {
        get => _selectedMapCatalogEntry;
        set
        {
            if (SetProperty(ref _selectedMapCatalogEntry, value))
            {
                OnPropertyChanged(nameof(CanRemoveManualMap));
                RemoveManualMapCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string MapCatalogStatus
    {
        get => _mapCatalogStatus;
        private set => SetProperty(ref _mapCatalogStatus, value);
    }

    public string MapCatalogMessage
    {
        get => _mapCatalogMessage;
        private set => SetProperty(ref _mapCatalogMessage, value);
    }

    public ServiceHealth MapCatalogHealth
    {
        get => _mapCatalogHealth;
        private set => SetProperty(ref _mapCatalogHealth, value);
    }

    public bool CanImportMapRotation =>
        _mapCatalogService is not null && !string.IsNullOrWhiteSpace(MapRotationLine);

    public bool CanAddManualMap =>
        _mapCatalogService is not null &&
        !string.IsNullOrWhiteSpace(ManualMapCode) &&
        !string.IsNullOrWhiteSpace(ManualMapName);

    public bool CanRemoveManualMap =>
        _mapCatalogService is not null && SelectedMapCatalogEntry?.IsManual == true;

    public string ProfileDisplayName
    {
        get => _profileDisplayName;
        set
        {
            var normalized = value ?? string.Empty;
            if (SetProperty(ref _profileDisplayName, normalized))
            {
                ConfigurationSaveStatus = "MODIFIÉ";
                ConfigurationSaveMessage = "Enregistrez pour appliquer le nom de cet onglet serveur.";
                OnPropertyChanged(nameof(CanSaveConfiguration));
                SaveConfigurationCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedOperatorMode
    {
        get => _selectedOperatorMode;
        set
        {
            var normalized = string.Equals(value, "LAN", StringComparison.OrdinalIgnoreCase) ? "LAN" : "LOCAL";
            if (SetProperty(ref _selectedOperatorMode, normalized))
            {
                OnPropertyChanged(nameof(OperatorModeHelp));
                ResetDataSourceTest();
            }
        }
    }

    public string OperatorServerRoot
    {
        get => _operatorServerRoot;
        set
        {
            if (SetProperty(ref _operatorServerRoot, value ?? string.Empty))
            {
                ResetDataSourceTest();
            }
        }
    }

    public string OperatorModeHelp => SelectedOperatorMode == "LAN"
        ? "Partage read-only PinteModData, par exemple \\\\portable\\PinteModData. Aucune découverte réseau."
        : "Chemin absolu présent sur cette machine.";

    public string DataSourceTestStatus
    {
        get => _dataSourceTestStatus;
        private set => SetProperty(ref _dataSourceTestStatus, value);
    }

    public string DataSourceTestMessage
    {
        get => _dataSourceTestMessage;
        private set => SetProperty(ref _dataSourceTestMessage, value);
    }

    public ServiceHealth DataSourceTestHealth
    {
        get => _dataSourceTestHealth;
        private set => SetProperty(ref _dataSourceTestHealth, value);
    }

    public bool CanTestDataSource => _localDataSourceProbe is not null && !string.IsNullOrWhiteSpace(OperatorServerRoot);

    public bool ActivateDataSourceOnStartup
    {
        get => _activateDataSourceOnStartup;
        set
        {
            if (SetProperty(ref _activateDataSourceOnStartup, value))
            {
                OnPropertyChanged(nameof(CanSaveConfiguration));
                SaveConfigurationCommand.NotifyCanExecuteChanged();
                NotifyRconCommandState();
            }
        }
    }

    public string RconAddress
    {
        get => _rconAddress;
        set
        {
            if (SetProperty(ref _rconAddress, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanSaveConfiguration));
                SaveConfigurationCommand.NotifyCanExecuteChanged();
                NotifyRconCommandState();
            }
        }
    }

    public string RconPortText
    {
        get => _rconPortText;
        set
        {
            if (SetProperty(ref _rconPortText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanSaveConfiguration));
                SaveConfigurationCommand.NotifyCanExecuteChanged();
                NotifyRconCommandState();
            }
        }
    }

    public string ConfigurationSaveStatus
    {
        get => _configurationSaveStatus;
        private set => SetProperty(ref _configurationSaveStatus, value);
    }

    public string ConfigurationSaveMessage
    {
        get => _configurationSaveMessage;
        private set => SetProperty(ref _configurationSaveMessage, value);
    }

    public bool CanSaveConfiguration =>
        _configurationStore is not null &&
        OperatorConfiguration.IsValidProfileDisplayName(ProfileDisplayName) &&
        CreateRconEndpoint() is not null &&
        (!ActivateDataSourceOnStartup || string.Equals(_lastAcceptedProbeSignature, ProbeSignature(), StringComparison.Ordinal));

    public bool CanRunRconDiagnostic =>
        _rconDiagnosticService is not null &&
        CreateRconEndpoint() is not null;

    public RconEndpoint? CreateRconEndpoint()
    {
        if (!TryGetRconPort(out var port))
        {
            return null;
        }

        var endpoint = new RconEndpoint(RconAddress.Trim(), port, TimeSpan.FromSeconds(3));
        return RconEndpointValidator.IsAllowed(endpoint) ? endpoint : null;
    }

    public string RconSecretStatus
    {
        get => _rconSecretStatus;
        private set => SetProperty(ref _rconSecretStatus, value);
    }

    public string RconTestStatus
    {
        get => _rconTestStatus;
        private set => SetProperty(ref _rconTestStatus, value);
    }

    public string RconResponse
    {
        get => _rconResponse;
        private set
        {
            if (SetProperty(ref _rconResponse, value))
            {
                OnPropertyChanged(nameof(CanCopyRconResponse));
                CopyRconResponseCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanCopyRconResponse => _clipboardService is not null && !string.IsNullOrWhiteSpace(RconResponse);

    public string RconCopyStatus
    {
        get => _rconCopyStatus;
        private set => SetProperty(ref _rconCopyStatus, value);
    }

    public string RconCommandSent
    {
        get => _rconCommandSent;
        private set => SetProperty(ref _rconCommandSent, value);
    }

    public ServiceHealth RconTestHealth
    {
        get => _rconTestHealth;
        private set => SetProperty(ref _rconTestHealth, value);
    }

    public bool AutomaticRefresh { get; }

    public bool SoundAlerts => false;

    public bool CompactMode => false;

    public bool AutomaticRefreshAvailable => false;

    public string AutomaticRefreshStatus { get; }

    public bool SoundAlertsAvailable => false;

    public bool CompactModeAvailable => false;

    public string ActiveProviderName { get; }

    public string DataMode { get; }

    public string ModeLabel { get; }

    public string ModeShortLabel { get; }

    public string ModeDescription { get; }

    public string ServerRootDisplay { get; }

    public string DataScope { get; }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClearError();
        if (!_rconSecretStateInitialized)
        {
            RconSecretStatus = _rconSecretStore is not null && await _rconSecretStore.HasSecretAsync(cancellationToken)
                ? "SECRET DPAPI ENREGISTRÉ"
                : "SECRET NON CONFIGURÉ";
            _rconSecretStateInitialized = true;
        }

        await RefreshMapCatalogAsync(cancellationToken);
    }

    private async Task ImportMapRotationAsync()
    {
        if (_mapCatalogService is null)
        {
            return;
        }

        var line = MapRotationLine;
        MapRotationLine = string.Empty;
        var result = await _mapCatalogService.ImportRotationLineAsync(line);
        ApplyMapCatalogResult(result);
        await RefreshMapCatalogAsync();
    }

    private async Task AddManualMapAsync()
    {
        if (_mapCatalogService is null)
        {
            return;
        }

        var result = await _mapCatalogService.AddManualMapAsync(ManualMapCode, ManualMapName);
        ApplyMapCatalogResult(result);
        if (result.Success)
        {
            ManualMapCode = string.Empty;
            ManualMapName = string.Empty;
        }

        await RefreshMapCatalogAsync();
    }

    private async Task RemoveManualMapAsync()
    {
        if (_mapCatalogService is null || SelectedMapCatalogEntry is not { } selected)
        {
            return;
        }

        var result = await _mapCatalogService.RemoveManualMapAsync(selected.Code);
        ApplyMapCatalogResult(result);
        await RefreshMapCatalogAsync();
    }

    private async Task RefreshMapCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (_mapCatalogService is null)
        {
            return;
        }

        var selectedCode = SelectedMapCatalogEntry?.Code;
        var snapshot = await _mapCatalogService.GetSnapshotAsync(cancellationToken);
        _mapCatalogState?.Update(snapshot);
        var desiredEntries = snapshot.Entries
            .Select(entry => new MapCatalogItemViewModel(entry))
            .ToArray();
        var catalogChanged = MapCatalogEntries.Count != desiredEntries.Length ||
                             MapCatalogEntries.Zip(desiredEntries).Any(pair =>
                                 !string.Equals(pair.First.Code, pair.Second.Code, StringComparison.Ordinal) ||
                                 !string.Equals(pair.First.DisplayLabel, pair.Second.DisplayLabel, StringComparison.Ordinal) ||
                                 pair.First.IsManual != pair.Second.IsManual);
        if (catalogChanged)
        {
            MapCatalogEntries.Clear();
            foreach (var entry in desiredEntries)
            {
                MapCatalogEntries.Add(entry);
            }
        }

        SelectedMapCatalogEntry = MapCatalogEntries.FirstOrDefault(entry =>
            string.Equals(entry.Code, selectedCode, StringComparison.OrdinalIgnoreCase));
        if (MapCatalogHealth == ServiceHealth.Unknown)
        {
            MapCatalogStatus = "CATALOGUE PRÊT";
            MapCatalogMessage = $"{MapCatalogEntries.Count} carte(s) disponible(s) · stockage local au Control Center.";
            MapCatalogHealth = ServiceHealth.Healthy;
        }
    }

    private void ApplyMapCatalogResult(MapCatalogOperationResult result)
    {
        MapCatalogStatus = result.Status;
        MapCatalogMessage = result.Message;
        MapCatalogHealth = result.Success ? ServiceHealth.Healthy : ServiceHealth.Error;
    }

    private void SetMapCatalogFailure(string message)
    {
        MapCatalogStatus = "ERREUR LOCALE";
        MapCatalogMessage = message;
        MapCatalogHealth = ServiceHealth.Error;
    }

    private void NotifyManualMapCommandState()
    {
        OnPropertyChanged(nameof(CanAddManualMap));
        AddManualMapCommand.NotifyCanExecuteChanged();
    }

    public async Task SaveRconSecretAsync(string secret, CancellationToken cancellationToken = default)
    {
        if (_rconSecretStore is null)
        {
            RconSecretStatus = "STOCKAGE INDISPONIBLE";
            return;
        }

        try
        {
            await _rconSecretStore.SaveAsync(secret, cancellationToken);
            _rconSecretStateInitialized = true;
            RconSecretStatus = "SECRET DPAPI ENREGISTRÉ";
            RconResponse = "Secret protégé pour le compte Windows courant. Il ne sera pas réaffiché.";
        }
        catch (ArgumentException)
        {
            RconSecretStatus = "SECRET REFUSÉ";
            RconResponse = "Utilisez un secret non vide, sans espace, guillemet ou retour à la ligne.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            RconSecretStatus = "ERREUR DPAPI";
            RconResponse = "Le secret n’a pas pu être protégé localement.";
        }
    }

    private async Task TestDataSourceAsync()
    {
        if (_localDataSourceProbe is null)
        {
            return;
        }

        DataSourceTestStatus = "TEST EN COURS";
        DataSourceTestMessage = "Lecture des sources autorisées…";
        DataSourceTestHealth = ServiceHealth.Unknown;

        var location = SelectedOperatorMode == "LAN"
            ? OperatorDataLocation.Lan
            : OperatorDataLocation.Local;
        var result = await _localDataSourceProbe.ProbeAsync(
            new LocalDataSourceProbeRequest(location, OperatorServerRoot));

        DataSourceTestMessage = result.Message;
        if (!result.RootAccepted)
        {
            DataSourceTestStatus = "REFUSÉ";
            DataSourceTestHealth = ServiceHealth.Error;
        }
        else if (result.ReadableSourceCount == result.TotalSourceCount)
        {
            _lastAcceptedProbeSignature = ProbeSignature();
            DataSourceTestStatus = "PRÊT";
            DataSourceTestHealth = ServiceHealth.Healthy;
        }
        else
        {
            if (result.HasReadableSource)
            {
                _lastAcceptedProbeSignature = ProbeSignature();
            }
            DataSourceTestStatus = result.HasReadableSource ? "PARTIEL" : "INCOMPLET";
            DataSourceTestHealth = ServiceHealth.Warning;
        }

        OnPropertyChanged(nameof(CanSaveConfiguration));
        SaveConfigurationCommand.NotifyCanExecuteChanged();
    }

    private void ResetDataSourceTest()
    {
        DataSourceTestStatus = "NON TESTÉ";
        DataSourceTestMessage = "Configuration modifiée · relancez le test read-only.";
        DataSourceTestHealth = ServiceHealth.Unknown;
        _lastAcceptedProbeSignature = null;
        ConfigurationSaveStatus = "MODIFIÉ";
        ConfigurationSaveMessage = "Testez la source avant d’activer son chargement au démarrage.";
        OnPropertyChanged(nameof(CanTestDataSource));
        OnPropertyChanged(nameof(CanSaveConfiguration));
        TestDataSourceCommand.NotifyCanExecuteChanged();
        SaveConfigurationCommand.NotifyCanExecuteChanged();
    }

    private void SetProbeFailure(string message)
    {
        DataSourceTestStatus = "ERREUR";
        DataSourceTestMessage = message;
        DataSourceTestHealth = ServiceHealth.Error;
    }

    private async Task SaveConfigurationAsync()
    {
        if (_configurationStore is null || CreateRconEndpoint() is not { } endpoint)
        {
            return;
        }

        var configuration = new OperatorConfiguration(
            OperatorConfiguration.CurrentSchemaVersion,
            SelectedOperatorMode == "LAN" ? OperatorDataLocation.Lan : OperatorDataLocation.Local,
            OperatorServerRoot,
            ActivateDataSourceOnStartup,
            endpoint.Address,
            endpoint.Port)
        {
            ProfileDisplayName = ProfileDisplayName.Trim()
        };
        await _configurationStore.SaveAsync(configuration);
        ProfileDisplayName = configuration.ProfileDisplayName;
        ProfileDisplayNameSaved?.Invoke(configuration.ProfileDisplayName);
        ConfigurationSaveStatus = "ENREGISTRÉ";
        ConfigurationSaveMessage = ActivateDataSourceOnStartup
            ? "Le mode opérateur sera appliqué au prochain démarrage."
            : "Configuration conservée · le démarrage reste en simulation.";
    }

    private void SetConfigurationFailure(string message)
    {
        ConfigurationSaveStatus = "ERREUR";
        ConfigurationSaveMessage = message;
    }

    private async Task ExecuteRconDiagnosticCoreAsync(RconDiagnosticCommand command)
    {
        if (_rconDiagnosticService is null || CreateRconEndpoint() is not { } endpoint)
        {
            return;
        }

        RconTestStatus = "EN COURS";
        RconResponse = "En attente de la réponse BOIII…";
        RconCommandSent = "Commande envoyée : Non";
        RconTestHealth = ServiceHealth.Unknown;

        var result = await _rconDiagnosticService.ExecuteAsync(
            command,
            endpoint);
        _operatorActivityStore?.RecordRconResult(result);
        if (result.Status == RconExecutionStatus.EmptyResponse &&
            result.CommandSent &&
            _snapshotStore is not null)
        {
            var snapshot = await _snapshotStore.RefreshAsync();
            if (LocalDiagnosticFallback.TryCreate(command, snapshot, out var fallback))
            {
                RconResponse = fallback.Message;
                RconCommandSent = "Commande envoyée : Oui";
                RconTestStatus = fallback.Status;
                RconTestHealth = fallback.Health;
                return;
            }
        }

        RconResponse = result.DisplayResponse;
        RconCommandSent = $"Commande envoyée : {(result.CommandSent ? "Oui" : "Non")}";
        RconTestStatus = result.Status switch
        {
            RconExecutionStatus.Success => "RÉUSSI",
            RconExecutionStatus.SecretMissing => "SECRET REQUIS",
            RconExecutionStatus.InvalidConfiguration => "CONFIGURATION INVALIDE",
            RconExecutionStatus.Timeout => "DÉLAI DÉPASSÉ",
            RconExecutionStatus.EmptyResponse => "ENVOYÉ · SANS TEXTE",
            RconExecutionStatus.UnexpectedResponse => "RÉPONSE NON RECONNUE",
            _ => "ÉCHEC"
        };
        RconTestHealth = result.Status switch
        {
            RconExecutionStatus.Success => ServiceHealth.Healthy,
            RconExecutionStatus.Timeout or RconExecutionStatus.EmptyResponse or RconExecutionStatus.UnexpectedResponse => ServiceHealth.Warning,
            _ => ServiceHealth.Error
        };
    }

    private AsyncRelayCommand CreateRconDiagnosticCommand(
        RconDiagnosticCommand command,
        string failureMessage) => new(
        () => _rconOperations.RunExclusiveAsync(
            _ => ExecuteRconDiagnosticCoreAsync(command)),
        () => CanRunRconDiagnostic,
        _ => SetRconFailure(failureMessage));

    private Task CopyRconResponseAsync()
    {
        RconCopyStatus = _clipboardService?.TrySetText(RconResponse) == true
            ? "Réponse neutralisée copiée."
            : "Copie impossible : le presse-papiers Windows est momentanément indisponible.";
        return Task.CompletedTask;
    }

    private void SetRconFailure(string message)
    {
        RconTestStatus = "ERREUR";
        RconResponse = message;
        RconCommandSent = "Commande envoyée : Non";
        RconTestHealth = ServiceHealth.Error;
    }

    private void NotifyRconCommandState()
    {
        OnPropertyChanged(nameof(CanRunRconDiagnostic));
        TestRconHealthCommand.NotifyCanExecuteChanged();
        TestRconPauseCommand.NotifyCanExecuteChanged();
        TestRconMapCommand.NotifyCanExecuteChanged();
        TestRconPowerCommand.NotifyCanExecuteChanged();
        TestRconPackAPunchCommand.NotifyCanExecuteChanged();
        TestRconRoundCommand.NotifyCanExecuteChanged();
        TestRconPlayersCommand.NotifyCanExecuteChanged();
        TestRconMapAuditCommand.NotifyCanExecuteChanged();
        TestRconEventStatusCommand.NotifyCanExecuteChanged();
        TestRconPowerUpCatalogCommand.NotifyCanExecuteChanged();
    }

    private bool TryGetRconPort(out int port) =>
        int.TryParse(RconPortText, out port) && port is >= 1 and <= 65535;

    private string ProbeSignature() => $"{SelectedOperatorMode}\n{OperatorServerRoot.Trim()}";

    private static bool IsUnc(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.TrimStart().StartsWith("\\\\", StringComparison.Ordinal);

    private static string FormatServerRoot(string? serverRoot)
    {
        if (string.IsNullOrWhiteSpace(serverRoot))
        {
            return "Aucune racine locale active";
        }

        var leaf = Path.GetFileName(Path.TrimEndingDirectorySeparator(serverRoot));
        return string.IsNullOrWhiteSpace(leaf)
            ? "Racine locale configurée"
            : $"Racine locale configurée · …\\{leaf}";
    }
}
