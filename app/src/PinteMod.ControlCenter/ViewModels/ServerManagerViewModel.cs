using System.IO;
using System.Net;
using System.Collections.ObjectModel;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Security;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.ViewModels;


public enum ServerCapabilityState
{
    Available,
    Limited,
    Unavailable
}

public sealed class ServerCapabilityViewModel
{
    public ServerCapabilityViewModel(string name, ServerCapabilityState state, string details)
    {
        Name = name;
        State = state;
        Details = details;
    }

    public string Name { get; }

    public ServerCapabilityState State { get; }

    public string Details { get; }

    public string StatusLabel => State switch
    {
        ServerCapabilityState.Available => "DISPONIBLE",
        ServerCapabilityState.Limited => "LIMITÉ",
        _ => "INDISPONIBLE"
    };
}

public sealed class ServerManagerProfileViewModel : ObservableObject
{
    private string _displayName;
    private string _serverRoot;
    private string _rconAddress;
    private string _rconPortText;
    private string _launcherRelativePath;
    private string _analysisSummary = "NON ANALYSÉ";
    private ManagedServerAnalysis? _analysis;
    private ServerStorageSummary? _storage;
    private string _remoteAgentId;
    private string _remoteAgentStatus = "AGENT NON CONFIGURÉ";
    private bool _remoteAgentOnline;
    private bool _remoteAgentPaired;
    private bool _remoteAgentDetected;
    private string _remoteAgentVersion = string.Empty;
    private string _remoteAgentMachineName = string.Empty;
    private string _serverPortSummary = "Port BOIII/RCON à vérifier dans le lanceur.";
    private bool _serverRunning;

    public ServerManagerProfileViewModel(
        string profileId,
        OperatorConfiguration configuration,
        ManagedServerProfileConfiguration managedConfiguration)
    {
        ProfileId = profileId;
        OriginalConfiguration = configuration;
        _displayName = configuration.ProfileDisplayName;
        _serverRoot = configuration.ServerRoot;
        _rconAddress = configuration.RconAddress;
        _rconPortText = configuration.RconPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _launcherRelativePath = managedConfiguration.LauncherRelativePath;
        _remoteAgentId = managedConfiguration.RemoteAgentId ?? string.Empty;
        RefreshCapabilities();
    }

    public string ProfileId { get; }

    internal OperatorConfiguration OriginalConfiguration { get; set; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string ServerRoot
    {
        get => _serverRoot;
        set
        {
            if (SetProperty(ref _serverRoot, value))
            {
                ServerRunning = false;
                OnPropertyChanged(nameof(IsUncProfile));
                OnPropertyChanged(nameof(CanUpdateRemoteAgent));
                OnPropertyChanged(nameof(CanLaunchSelected));
                OnPropertyChanged(nameof(CanStopSelected));
                OnPropertyChanged(nameof(SimpleStateTitle));
                OnPropertyChanged(nameof(SimpleStateMessage));
                OnPropertyChanged(nameof(RecommendedActionLabel));
                NotifyRemoteConnectionVisualState();
                TryApplyRconAddressFromUnc(value);
            }
        }
    }

    public string RconAddress
    {
        get => _rconAddress;
        set => SetProperty(ref _rconAddress, value);
    }

    private void TryApplyRconAddressFromUnc(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !root.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return;
        }

        var host = root.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (IPAddress.TryParse(host, out var address) &&
            PinteMod.ControlCenter.Core.Security.RconEndpointValidator.IsLocalOrPrivateAddress(address.ToString()))
        {
            RconAddress = address.ToString();
        }
    }

    public string RconPortText
    {
        get => _rconPortText;
        set
        {
            if (SetProperty(ref _rconPortText, value))
            {
                ServerRunning = false;
                OnPropertyChanged(nameof(CanLaunchSelected));
                OnPropertyChanged(nameof(CanStopSelected));
            }
        }
    }

    public string ServerPortSummary
    {
        get => _serverPortSummary;
        private set => SetProperty(ref _serverPortSummary, value);
    }

    public string LauncherRelativePath
    {
        get => _launcherRelativePath;
        set
        {
            if (SetProperty(ref _launcherRelativePath, value))
            {
                OnPropertyChanged(nameof(CanLaunchLocally));
                OnPropertyChanged(nameof(CanEnableRemoteAgentLocal));
                OnPropertyChanged(nameof(CanPairRemoteAgent));
                OnPropertyChanged(nameof(CanLaunchSelected));
                OnPropertyChanged(nameof(CanStopSelected));
            }
        }
    }

    public string AnalysisSummary
    {
        get => _analysisSummary;
        private set => SetProperty(ref _analysisSummary, value);
    }

    public ManagedServerAnalysis? Analysis
    {
        get => _analysis;
        private set
        {
            if (SetProperty(ref _analysis, value))
            {
                OnPropertyChanged(nameof(IntegrationLabel));
                OnPropertyChanged(nameof(AnalysisDetails));
                OnPropertyChanged(nameof(CanInstallPinteMod));
                OnPropertyChanged(nameof(PinteModInstallActionLabel));
                OnPropertyChanged(nameof(CanInstallBridge));
                OnPropertyChanged(nameof(CanLaunchLocally));
                OnPropertyChanged(nameof(CanEnableRemoteAgentLocal));
                OnPropertyChanged(nameof(CanPairRemoteAgent));
                OnPropertyChanged(nameof(CanLaunchSelected));
                OnPropertyChanged(nameof(CanStopSelected));
                OnPropertyChanged(nameof(SimpleStateTitle));
                OnPropertyChanged(nameof(SimpleStateMessage));
                OnPropertyChanged(nameof(RecommendedActionLabel));
                OnPropertyChanged(nameof(HasThirdPartyScripts));
                OnPropertyChanged(nameof(ThirdPartySummary));
                OnPropertyChanged(nameof(IsReadyForControlCenter));
                OnPropertyChanged(nameof(CanInstallPinteModSafely));
                RefreshCapabilities();
            }
        }
    }

    public ServerStorageSummary? Storage
    {
        get => _storage;
        private set
        {
            if (SetProperty(ref _storage, value))
            {
                OnPropertyChanged(nameof(StorageSummary));
                OnPropertyChanged(nameof(GeoIpStatisticsAnomalous));
                OnPropertyChanged(nameof(GeoIpMaintenanceRecommended));
            }
        }
    }

    public string StorageSummary => Storage?.DisplaySummary ?? "Stockage non analysé.";

    public bool GeoIpStatisticsAnomalous => Storage?.GeoIpStatisticsAnomalous == true;

    public bool GeoIpMaintenanceRecommended => Storage is { } value &&
        (value.GeoIpStatisticsAnomalous || value.GeoIpBridgeNeedsHardening);

    public void ApplyStorage(ServerStorageSummary summary) => Storage = summary;


    public string RemoteAgentId
    {
        get => _remoteAgentId;
        private set => SetProperty(ref _remoteAgentId, value);
    }

    public string RemoteAgentStatus
    {
        get => _remoteAgentStatus;
        private set => SetProperty(ref _remoteAgentStatus, value);
    }

    public string RemoteAgentVersion
    {
        get => _remoteAgentVersion;
        private set => SetProperty(ref _remoteAgentVersion, value);
    }

    public string RemoteAgentMachineName
    {
        get => _remoteAgentMachineName;
        private set => SetProperty(ref _remoteAgentMachineName, value);
    }

    public bool RemoteAgentOnline
    {
        get => _remoteAgentOnline;
        private set
        {
            if (SetProperty(ref _remoteAgentOnline, value))
            {
                OnPropertyChanged(nameof(CanLaunchRemote));
                OnPropertyChanged(nameof(CanStopSelected));
                NotifyRemoteConnectionVisualState();
            }
        }
    }

    public bool RemoteAgentPaired
    {
        get => _remoteAgentPaired;
        private set
        {
            if (SetProperty(ref _remoteAgentPaired, value))
            {
                OnPropertyChanged(nameof(CanLaunchRemote));
                OnPropertyChanged(nameof(CanUpdateRemoteAgent));
                OnPropertyChanged(nameof(CanPairRemoteAgent));
                OnPropertyChanged(nameof(CanStopSelected));
                NotifyRemoteConnectionVisualState();
            }
        }
    }

    public bool IsUncProfile => ServerRoot.StartsWith("\\\\", StringComparison.Ordinal);

    public bool CanEnableRemoteAgentLocal => Analysis?.CanLaunchLocally == true;

    public bool CanPairRemoteAgent => Analysis is { IsUnc: true, BoiiiRootDetected: true } && !RemoteAgentPaired;

    public bool RemoteAgentDetected
    {
        get => _remoteAgentDetected;
        private set
        {
            if (SetProperty(ref _remoteAgentDetected, value))
            {
                OnPropertyChanged(nameof(CanUpdateRemoteAgent));
                NotifyRemoteConnectionVisualState();
            }
        }
    }

    public bool CanLaunchRemote => IsUncProfile && RemoteAgentPaired && RemoteAgentOnline;

    public bool CanUpdateRemoteAgent => IsUncProfile && RemoteAgentDetected && RemoteAgentPaired;

    public bool RemoteConnectionLinked => IsUncProfile && RemoteAgentDetected && RemoteAgentPaired && RemoteAgentOnline;

    public bool RemoteConnectionPairedOffline => IsUncProfile && RemoteAgentPaired && !RemoteAgentOnline;

    public bool RemoteConnectionNeedsPairing => IsUncProfile && RemoteAgentDetected && !RemoteAgentPaired;

    public bool RemoteConnectionNotDetected => IsUncProfile && !RemoteAgentDetected;

    public string RemoteConnectionTitle => !IsUncProfile
        ? "SERVEUR SUR CE PC"
        : RemoteConnectionLinked
            ? "PC SERVEUR RELIÉ"
            : RemoteConnectionPairedOffline
                ? "PC SERVEUR APPARIÉ · HORS LIGNE"
                : RemoteConnectionNeedsPairing
                    ? "PC SERVEUR DÉTECTÉ · CONNEXION À TERMINER"
                    : "PC SERVEUR NON RELIÉ";

    public string RemoteConnectionSummary
    {
        get
        {
            var machine = string.IsNullOrWhiteSpace(RemoteAgentMachineName) ? "PC serveur" : RemoteAgentMachineName;
            var version = string.IsNullOrWhiteSpace(RemoteAgentVersion) ? string.Empty : $" · Agent {RemoteAgentVersion}";

            if (RemoteConnectionLinked)
            {
                return $"Ce Control Center est appairé à {machine} · liaison sécurisée active{version}.";
            }

            if (RemoteConnectionPairedOffline)
            {
                return $"Appairage mémorisé avec {machine}{version}, mais l’Agent ne répond pas actuellement.";
            }

            if (RemoteConnectionNeedsPairing)
            {
                return $"L’Agent de {machine} est détecté. Terminez la connexion une seule fois pour administrer ce PC.";
            }

            return IsUncProfile
                ? "Aucune liaison authentifiée n’est actuellement confirmée avec le PC serveur."
                : "Ce profil utilise directement une installation présente sur cette machine.";
        }
    }

    public bool ServerRunning
    {
        get => _serverRunning;
        private set
        {
            if (SetProperty(ref _serverRunning, value))
            {
                OnPropertyChanged(nameof(CanLaunchSelected));
                OnPropertyChanged(nameof(CanStopSelected));
            }
        }
    }

    public bool CanLaunchSelected => !ServerRunning && (CanLaunchLocally || CanLaunchRemote);

    public bool CanStopSelected => ServerRunning &&
        (IsUncProfile ? CanLaunchRemote : Analysis?.BoiiiRootDetected == true);

    public void ApplyServerRunning(bool running) => ServerRunning = running;

    public void SetRemoteAgentId(string agentId)
    {
        RemoteAgentId = agentId?.Trim() ?? string.Empty;
        OnPropertyChanged(nameof(CanLaunchRemote));
        OnPropertyChanged(nameof(CanUpdateRemoteAgent));
        NotifyRemoteConnectionVisualState();
    }

    public void ApplyRemoteAgentProbe(RemoteAgentProbeResult result)
    {
        RemoteAgentStatus = result.Message;
        RemoteAgentVersion = result.AgentVersion;
        RemoteAgentMachineName = result.MachineName;
        RemoteAgentDetected = result.AgentDetected;
        RemoteAgentPaired = result.Paired;
        RemoteAgentOnline = result.Online;
        OnPropertyChanged(nameof(CanLaunchRemote));
        OnPropertyChanged(nameof(CanUpdateRemoteAgent));
        OnPropertyChanged(nameof(CanLaunchSelected));
        OnPropertyChanged(nameof(CanStopSelected));
        OnPropertyChanged(nameof(CanPairRemoteAgent));
        NotifyRemoteConnectionVisualState();
    }

    private void NotifyRemoteConnectionVisualState()
    {
        OnPropertyChanged(nameof(RemoteConnectionLinked));
        OnPropertyChanged(nameof(RemoteConnectionPairedOffline));
        OnPropertyChanged(nameof(RemoteConnectionNeedsPairing));
        OnPropertyChanged(nameof(RemoteConnectionNotDetected));
        OnPropertyChanged(nameof(RemoteConnectionTitle));
        OnPropertyChanged(nameof(RemoteConnectionSummary));
    }

    public ObservableCollection<string> LauncherCandidates { get; } = [];

    public ObservableCollection<ServerCapabilityViewModel> Capabilities { get; } = [];

    public string IntegrationLabel => Analysis?.IntegrationKind switch
    {
        ManagedServerIntegrationKind.PinteMod => "PINTEMOD",
        ManagedServerIntegrationKind.ControlCenterBridge => "COMPATIBILITÉ",
        ManagedServerIntegrationKind.ThirdPartyScripts => "SCRIPTS TIERS",
        ManagedServerIntegrationKind.BoiiiNative => "BOIII",
        _ => "NON CONFIGURÉ"
    };

    public string SimpleStateTitle => Analysis switch
    {
        null when string.IsNullOrWhiteSpace(ServerRoot) => "Serveur à configurer",
        null => "Analyse requise",
        { BoiiiRootDetected: false } => "BOIII non détecté",
        { PinteModDetected: true } => "Serveur prêt avec PinteMod",
        { ControlCenterBridgeDetected: true } or { GenericBridgeDetected: true } => "Serveur compatible avec le Control Center",
        { ThirdPartyGscDetected: true } => "Scripts personnalisés détectés",
        { BoiiiRootDetected: true } => "Serveur BOIII détecté"
    };

    public string SimpleStateMessage => Analysis switch
    {
        null when string.IsNullOrWhiteSpace(ServerRoot) => "Choisissez le dossier du serveur. Le Control Center vous proposera ensuite uniquement les actions utiles.",
        null => "Lancez l’analyse pour identifier BOIII, PinteMod et les scripts existants.",
        { BoiiiRootDetected: false } => "Le dossier est accessible mais ne ressemble pas à une installation BOIII serveur.",
        { PinteModDetected: true } => "PinteMod est déjà présent. Le Control Center l’utilisera sans réinstaller vos données ni vos profils.",
        { ControlCenterBridgeDetected: true } or { GenericBridgeDetected: true } => "Un module de compatibilité connu est présent. Seules les fonctions annoncées comme disponibles seront activées.",
        { ThirdPartyGscDetected: true } analysis => $"{analysis.ThirdPartyGscCount} script(s) personnalisé(s) détecté(s). Aucun script tiers ne sera modifié ; les fonctions non prouvées resteront grisées.",
        { BoiiiRootDetected: true } => "BOIII est prêt. Le bouton principal installe PinteMod, prépare la gestion locale et lance le premier démarrage."
    };

    public string RecommendedActionLabel => Analysis switch
    {
        null when string.IsNullOrWhiteSpace(ServerRoot) => "CHOISIR LE DOSSIER DU SERVEUR",
        null => "ANALYSER CE SERVEUR",
        { BoiiiRootDetected: false } => "CHOISIR UN AUTRE DOSSIER",
        { PinteModDetected: true } => "UTILISER CE SERVEUR",
        { ControlCenterBridgeDetected: true } or { GenericBridgeDetected: true } => "UTILISER CE SERVEUR",
        { ThirdPartyGscDetected: true } => "ENREGISTRER EN MODE LIMITÉ",
        { BoiiiRootDetected: true } => "PRÉPARER ET DÉMARRER"
    };

    public bool HasThirdPartyScripts => Analysis?.ThirdPartyGscDetected == true;

    public string ThirdPartySummary
    {
        get
        {
            if (Analysis is not { ThirdPartyGscDetected: true } analysis)
            {
                return "Aucun script tiers détecté.";
            }

            var files = string.Join(", ", analysis.ThirdPartyGscNames.Take(6)) +
                        (analysis.ThirdPartyGscCount > 6 ? "…" : string.Empty);
            var audit = analysis.IntegrationProfile.ThirdPartyAudit;
            var commands = audit.DeclaredCommands.Count == 0
                ? "aucune commande déclarée cataloguée"
                : $"commandes observées : {string.Join(", ", audit.DeclaredCommands.Take(10))}" +
                  (audit.DeclaredCommands.Count > 10 ? "…" : string.Empty);
            return $"{files} · {commands}. Audit uniquement en lecture : aucune commande tierce n’est exécutée automatiquement.";
        }
    }

    public bool IsReadyForControlCenter => Analysis is { BoiiiRootDetected: true } analysis &&
        (analysis.PinteModDetected || analysis.ControlCenterBridgeDetected || analysis.GenericBridgeDetected);

    public bool CanInstallPinteModSafely => Analysis is
    {
        CanDeployFirstPartyFiles: true,
        PinteModDetected: false,
        ControlCenterBridgeDetected: false,
        GenericBridgeDetected: false,
        ThirdPartyGscDetected: false
    };

    public string AnalysisDetails => Analysis is null
        ? "Sélectionnez ANALYSER pour détecter BOIII, PinteMod et les intégrations disponibles."
        : $"Provider={Analysis.IntegrationProfile.ProviderLabel} · " +
          $"PinteMod={(Analysis.PinteModDetected ? "OUI" : "NON")} · " +
          $"Runtime CC={(Analysis.ControlCenterRuntimeDetected ? "OUI" : "NON")} · " +
          $"Bridge={(Analysis.ControlCenterBridgeDetected || Analysis.GenericBridgeDetected ? "OUI" : "NON")} · " +
          $"GSC={Analysis.GscFileCount} · Tiers={Analysis.ThirdPartyGscCount}";

    public bool CanRepairPinteModSafely => Analysis is
    {
        CanDeployFirstPartyFiles: true,
        PinteModDetected: true
    };

    // A deployed PinteMod can need a narrowly-scoped first-party repair: for
    // example the stock v2.1.1 verifier predates the supported 35-module
    // inventory. The payload service still refuses every unknown collision.
    public bool CanInstallPinteMod => CanInstallPinteModSafely || CanRepairPinteModSafely;

    public string PinteModInstallActionLabel => Analysis?.PinteModDetected == true
        ? "VÉRIFIER / RÉPARER PINTE MOD"
        : "INSTALLER PINTE MOD";

    public bool CanInstallBridge => Analysis is { CanDeployFirstPartyFiles: true, PinteModDetected: true };

    public bool CanLaunchLocally => Analysis?.CanLaunchLocally == true && !string.IsNullOrWhiteSpace(LauncherRelativePath);

    private void RefreshCapabilities()
    {
        Capabilities.Clear();
        var analysis = Analysis;
        if (analysis is null || !analysis.BoiiiRootDetected)
        {
            AddCapability("Démarrage du serveur", ServerCapabilityState.Unavailable, "Sélectionnez d’abord une racine BOIII valide.");
            AddCapability("Informations serveur", ServerCapabilityState.Unavailable, "Aucun provider n’est encore détecté.");
            AddCapability("Joueurs / chat", ServerCapabilityState.Unavailable, "Aucune source structurée n’est encore disponible.");
            AddCapability("Commandes", ServerCapabilityState.Unavailable, "Aucune commande n’est activée sans capacité prouvée.");
            return;
        }

        var profile = analysis.IntegrationProfile;
        AddFromIntegration("Démarrage du serveur", profile, IntegrationCapabilityKey.ServerLifecycle);
        AddFromIntegration("Informations serveur", profile, IntegrationCapabilityKey.ServerInformation);
        AddCombined("Carte / manche", profile, IntegrationCapabilityKey.MapAndRound);
        AddCombined("Joueurs", profile, IntegrationCapabilityKey.Players);
        AddCombined("Chat", profile, IntegrationCapabilityKey.Chat);
        AddCombined("Commandes serveur", profile, IntegrationCapabilityKey.ServerCommands);
        AddCombined("Commandes joueur", profile, IntegrationCapabilityKey.PlayerCommands);
        AddCombined("Identité publique", profile, IntegrationCapabilityKey.PublicIdentity);
        AddCombined("Ranks", profile, IntegrationCapabilityKey.Ranks);
        AddCombined("Records", profile, IntegrationCapabilityKey.Records);
        AddCombined("Bosses / événements", profile, IntegrationCapabilityKey.BossesAndEvents);
    }

    private void AddFromIntegration(
        string name,
        ServerIntegrationProfile profile,
        IntegrationCapabilityKey key) =>
        AddCombined(name, profile, key);

    private void AddCombined(
        string name,
        ServerIntegrationProfile profile,
        IntegrationCapabilityKey key)
    {
        var capability = profile.Capabilities.FirstOrDefault(item => item.Key == key);
        if (capability is null)
        {
            AddCapability(name, ServerCapabilityState.Unavailable, "Capacité non annoncée par le provider.");
            return;
        }

        var state = capability.Availability switch
        {
            IntegrationCapabilityAvailability.Available => ServerCapabilityState.Available,
            IntegrationCapabilityAvailability.Observed => ServerCapabilityState.Limited,
            _ => ServerCapabilityState.Unavailable
        };
        AddCapability(name, state, $"{capability.Evidence} · Source : {capability.Source}");
    }

    private void AddCapability(string name, ServerCapabilityState state, string details) =>
        Capabilities.Add(new ServerCapabilityViewModel(name, state, details));

    public void ApplyAnalysis(ManagedServerAnalysis analysis)
    {
        Analysis = analysis;
        AnalysisSummary = analysis.Summary;
        LauncherCandidates.Clear();
        foreach (var launcher in analysis.LauncherCandidates)
        {
            LauncherCandidates.Add(launcher);
        }

        if (string.IsNullOrWhiteSpace(LauncherRelativePath) && LauncherCandidates.Count > 0)
        {
            LauncherRelativePath = LauncherCandidates[0];
            OnPropertyChanged(nameof(CanLaunchLocally));
            OnPropertyChanged(nameof(CanEnableRemoteAgentLocal));
            OnPropertyChanged(nameof(CanPairRemoteAgent));
            OnPropertyChanged(nameof(CanLaunchSelected));
        }

        if (analysis.DetectedServerPort is not { } detectedPort)
        {
            ServerPortSummary = "Port BOIII/RCON non trouvé automatiquement : vérifiez le lanceur sélectionné.";
            return;
        }

        var selectedLauncher = string.IsNullOrWhiteSpace(LauncherRelativePath)
            ? analysis.DetectedServerPortLauncher
            : LauncherRelativePath;
        ServerPortSummary = $"Port BOIII/RCON détecté dans {analysis.DetectedServerPortLauncher} : {detectedPort}.";

        // A new profile used to start at 27017 even when Server.bat explicitly
        // declared another port. Apply only over that historical default, never
        // over a port already chosen by the operator.
        if (int.TryParse(RconPortText, out var configuredPort) &&
            configuredPort == OperatorConfiguration.Default.RconPort &&
            string.Equals(selectedLauncher, analysis.DetectedServerPortLauncher, StringComparison.OrdinalIgnoreCase))
        {
            RconPortText = detectedPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ServerPortSummary = $"Port BOIII/RCON détecté et appliqué depuis {analysis.DetectedServerPortLauncher} : {detectedPort}.";
        }
    }
}

public sealed class ServerManagerViewModel : ObservableObject
{
    private sealed record RemoteCatalogImportSummary(
        int CandidateSources,
        int PairedSources,
        int AuthenticatedCatalogs,
        int ImportedProfiles,
        int KnownProfiles,
        int UnresolvedProfiles,
        IReadOnlyList<string> Diagnostics);

    private readonly JsonOperatorWorkspaceConfigurationStore _workspaceStore;
    private readonly ServerInstallationAnalyzer _analyzer;
    private readonly EmbeddedServerPayloadService _payloadService;
    private readonly ServerStorageAnalyzer _storageAnalyzer;
    private readonly LocalServerLaunchService _launchService;
    private readonly MultiServerOrchestratorService _orchestratorService;
    private readonly RemoteLaunchClientService _remoteLaunchClient;
    private readonly RemoteAgentInstallerService _remoteAgentInstaller;
    private readonly ManagedServerRuntimeProbe _runtimeProbe;
    private readonly ManagedServerStopService _stopService;
    private readonly GitHubUpdateCheckService _githubUpdateService = new();
    private OperatorWorkspaceConfiguration _workspaceConfiguration;
    private ServerManagerProfileViewModel? _selectedProfile;
    private string _statusMessage = "Sélectionnez un serveur ou ajoutez-en un.";
    private bool _isBusy;
    private bool _isAdvancedMode;
    private bool _isGitHubCheckBusy;
    private bool _githubUpdateAvailable;
    private string? _githubLatestVersion;
    private string _githubUpdateStatus = "GitHub : vérification en attente…";
    private bool _keepManagerOpenAfterControlCenter;
    private string _selectedUiLanguage;
    private bool _updateAttentionVisible;
    private string _updateAttentionTitle = "MISE À JOUR";
    private string _updateAttentionMessage = string.Empty;

    private ServerManagerViewModel(
        JsonOperatorWorkspaceConfigurationStore workspaceStore,
        OperatorWorkspaceConfiguration workspaceConfiguration,
        ServerInstallationAnalyzer analyzer,
        EmbeddedServerPayloadService payloadService,
        ServerStorageAnalyzer storageAnalyzer,
        LocalServerLaunchService launchService,
        MultiServerOrchestratorService orchestratorService,
        RemoteLaunchClientService remoteLaunchClient,
        RemoteAgentInstallerService remoteAgentInstaller,
        ManagedServerRuntimeProbe runtimeProbe,
        ManagedServerStopService stopService)
    {
        _workspaceStore = workspaceStore;
        _workspaceConfiguration = workspaceConfiguration;
        _analyzer = analyzer;
        _payloadService = payloadService;
        _storageAnalyzer = storageAnalyzer;
        _launchService = launchService;
        _orchestratorService = orchestratorService;
        _remoteLaunchClient = remoteLaunchClient;
        _remoteAgentInstaller = remoteAgentInstaller;
        _runtimeProbe = runtimeProbe;
        _stopService = stopService;
        _isAdvancedMode = workspaceConfiguration.AdvancedMode;
        _keepManagerOpenAfterControlCenter = workspaceConfiguration.KeepManagerOpenAfterControlCenter;
        _selectedUiLanguage = NormalizeUiLanguage(workspaceConfiguration.UiLanguageCode);
    }

    public ObservableCollection<ServerManagerProfileViewModel> Profiles { get; } = [];

    public ServerManagerProfileViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                OnPropertyChanged(nameof(HasSelectedProfile));
                OnPropertyChanged(nameof(CanSynchronizeSelectedRemote));
                RefreshUpdateAttention();
            }
        }
    }

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool IsAdvancedMode
    {
        get => _isAdvancedMode;
        private set
        {
            if (SetProperty(ref _isAdvancedMode, value))
            {
                OnPropertyChanged(nameof(IsSimpleMode));
                OnPropertyChanged(nameof(DisplayModeLabel));
            }
        }
    }

    public bool IsSimpleMode => !IsAdvancedMode;

    public IReadOnlyList<UiLanguageOption> UiLanguages { get; } =
    [
        new("fr-FR", "FR", "Français"),
        new("en-US", "EN", "English")
    ];

    public string SelectedUiLanguage
    {
        get => _selectedUiLanguage;
        private set => SetProperty(ref _selectedUiLanguage, value);
    }

    public string DisplayModeLabel => IsAdvancedMode ? "MODE AVANCÉ" : "MODE SIMPLE";

    public bool KeepManagerOpenAfterControlCenter
    {
        get => _keepManagerOpenAfterControlCenter;
        private set => SetProperty(ref _keepManagerOpenAfterControlCenter, value);
    }

    public bool UpdateAttentionVisible
    {
        get => _updateAttentionVisible;
        private set => SetProperty(ref _updateAttentionVisible, value);
    }

    public string UpdateAttentionTitle
    {
        get => _updateAttentionTitle;
        private set => SetProperty(ref _updateAttentionTitle, value);
    }

    public string UpdateAttentionMessage
    {
        get => _updateAttentionMessage;
        private set => SetProperty(ref _updateAttentionMessage, value);
    }

    public bool CanSynchronizeSelectedRemote
    {
        get
        {
            var remote = SelectedProfile;
            return remote?.CanUpdateRemoteAgent == true && !string.IsNullOrWhiteSpace(remote.RemoteAgentVersion);
        }
    }

    public async Task SetKeepManagerOpenAfterControlCenterAsync(bool keepOpen, CancellationToken cancellationToken = default)
    {
        KeepManagerOpenAfterControlCenter = keepOpen;
        _workspaceConfiguration = _workspaceConfiguration with { KeepManagerOpenAfterControlCenter = keepOpen };
        await _workspaceStore.SaveAsync(_workspaceConfiguration, cancellationToken);
        StatusMessage = keepOpen
            ? "La fenêtre de configuration restera ouverte pendant l’utilisation du Control Center."
            : "La fenêtre de configuration se fermera après l’ouverture du Control Center.";
    }

    public async Task SetAdvancedModeAsync(bool advanced, CancellationToken cancellationToken = default)
    {
        IsAdvancedMode = advanced;
        _workspaceConfiguration = _workspaceConfiguration with { AdvancedMode = advanced };
        await _workspaceStore.SaveAsync(_workspaceConfiguration, cancellationToken);
        StatusMessage = advanced
            ? "Mode avancé activé : les détails techniques et outils de maintenance sont visibles."
            : "Mode simple activé : seuls les choix utiles au quotidien sont affichés.";
    }

    public async Task SetUiLanguageAsync(string? languageCode, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUiLanguage(languageCode);
        if (string.Equals(SelectedUiLanguage, normalized, StringComparison.Ordinal))
        {
            return;
        }

        SelectedUiLanguage = normalized;
        _workspaceConfiguration = _workspaceConfiguration with { UiLanguageCode = normalized };
        await _workspaceStore.SaveAsync(_workspaceConfiguration, cancellationToken);
        StatusMessage = normalized == "en-US"
            ? "Language saved. The French interface remains the current reference."
            : "Langue enregistrée. L’interface française reste la référence actuelle.";
    }

    private static string NormalizeUiLanguage(string? languageCode) =>
        string.Equals(languageCode, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "fr-FR";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string CurrentApplicationVersion => GitHubUpdateCheckService.GetCurrentVersion();

    public bool IsGitHubCheckBusy
    {
        get => _isGitHubCheckBusy;
        private set => SetProperty(ref _isGitHubCheckBusy, value);
    }

    public bool GitHubUpdateAvailable
    {
        get => _githubUpdateAvailable;
        private set => SetProperty(ref _githubUpdateAvailable, value);
    }

    public string GitHubUpdateStatus
    {
        get => _githubUpdateStatus;
        private set => SetProperty(ref _githubUpdateStatus, value);
    }

    public async Task CheckGitHubUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (IsGitHubCheckBusy) return;
        IsGitHubCheckBusy = true;
        GitHubUpdateStatus = "GitHub : vérification de la dernière version…";
        try
        {
            var result = await _githubUpdateService.CheckAsync(cancellationToken);
            GitHubUpdateAvailable = result.UpdateAvailable;
            _githubLatestVersion = result.LatestVersion;
            GitHubUpdateStatus = result.Message;
            RefreshUpdateAttention();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            GitHubUpdateStatus = "GitHub : vérification interrompue.";
            RefreshUpdateAttention();
        }
        finally
        {
            IsGitHubCheckBusy = false;
        }
    }

    private void RefreshUpdateAttention()
    {
        var messages = new List<string>();
        var title = "VERSIONS À SYNCHRONISER";
        var current = CurrentApplicationVersion;

        if (GitHubUpdateAvailable && !string.IsNullOrWhiteSpace(_githubLatestVersion))
        {
            title = "MISE À JOUR DISPONIBLE";
            messages.Add($"GitHub publie {_githubLatestVersion} alors que ce PC utilise {current}.");
        }

        var remote = SelectedProfile;
        if (remote is { IsUncProfile: true, RemoteAgentDetected: true } &&
            !string.IsNullOrWhiteSpace(remote.RemoteAgentVersion) &&
            !string.Equals(remote.RemoteAgentVersion, current, StringComparison.OrdinalIgnoreCase))
        {
            var comparison = GitHubUpdateCheckService.CompareVersions(remote.RemoteAgentVersion, current);
            if (comparison > 0)
            {
                title = GitHubUpdateAvailable ? title : "VERSIONS À SYNCHRONISER";
                messages.Add($"{remote.RemoteAgentMachineName}: {remote.RemoteAgentVersion} · ce PC: {current}. La version la plus récente sera recopiée vers l’autre PC.");
            }
            else if (comparison < 0)
            {
                messages.Add($"{remote.RemoteAgentMachineName}: {remote.RemoteAgentVersion} · ce PC: {current}. Le PC serveur peut être synchronisé.");
            }
            else
            {
                messages.Add($"Versions différentes détectées entre ce PC ({current}) et {remote.RemoteAgentMachineName} ({remote.RemoteAgentVersion}).");
            }
        }

        UpdateAttentionVisible = messages.Count > 0;
        UpdateAttentionTitle = title;
        UpdateAttentionMessage = string.Join("  •  ", messages);
        OnPropertyChanged(nameof(CanSynchronizeSelectedRemote));
    }

    public static async Task<ServerManagerViewModel> CreateAsync(CancellationToken cancellationToken = default)
    {
        var workspaceStore = new JsonOperatorWorkspaceConfigurationStore();
        var workspace = await workspaceStore.LoadAsync(cancellationToken);
        var viewModel = new ServerManagerViewModel(
            workspaceStore,
            workspace,
            new ServerInstallationAnalyzer(),
            new EmbeddedServerPayloadService(),
            new ServerStorageAnalyzer(),
            new LocalServerLaunchService(),
            new MultiServerOrchestratorService(),
            new RemoteLaunchClientService(),
            new RemoteAgentInstallerService(),
            new ManagedServerRuntimeProbe(),
            new ManagedServerStopService());

        foreach (var profileId in workspace.ProfileIds)
        {
            var configurationStore = new JsonOperatorConfigurationStore(
                OperatorProfileStoragePaths.GetConfigurationPath(profileId));
            var managedStore = new JsonManagedServerProfileStore(
                OperatorProfileStoragePaths.GetManagedServerProfilePath(profileId));
            var configuration = await configurationStore.LoadAsync(cancellationToken);
            var managed = await managedStore.LoadAsync(cancellationToken);
            viewModel.Profiles.Add(new ServerManagerProfileViewModel(profileId, configuration, managed));
        }

        viewModel.SelectedProfile = viewModel.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.ProfileId, workspace.ActiveProfileId, StringComparison.Ordinal))
            ?? viewModel.Profiles.FirstOrDefault();

        // The manager is the recovery point for a local installation.  An
        // unreachable network source or a stale Agent registration must never
        // prevent it from opening: the operator needs this window precisely to
        // refresh or repair those optional integrations.
        await viewModel.RunOptionalStartupStepAsync(
            () => viewModel.RefreshInstalledLocalAgentRegistrationsIfNeededAsync(cancellationToken),
            "L’Agent local n’a pas pu être actualisé automatiquement.",
            cancellationToken);
        await viewModel.RunOptionalStartupStepAsync(
            async () => { await viewModel.ImportAuthenticatedRemoteCatalogProfilesAsync(cancellationToken); },
            "Les serveurs réseau n’ont pas pu être actualisés automatiquement.",
            cancellationToken);

        if (viewModel.SelectedProfile is not null && !string.IsNullOrWhiteSpace(viewModel.SelectedProfile.ServerRoot))
        {
            await viewModel.RunOptionalStartupStepAsync(
                () => viewModel.AnalyzeSelectedAsync(cancellationToken),
                "Le serveur sélectionné n’a pas pu être analysé au démarrage.",
                cancellationToken);
        }

        return viewModel;
    }

    public async Task AnalyzeSelectedAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var analysis = await _analyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken);
            profile.ApplyAnalysis(analysis);
            if (analysis.BoiiiRootDetected)
            {
                profile.ApplyStorage(await _storageAnalyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken));
            }
            await RefreshRemoteAgentAsync(profile, cancellationToken);
            profile.ApplyServerRunning(_runtimeProbe.IsRunning(profile.ServerRoot, ParsePort(profile.RconPortText)));
            StatusMessage = analysis.Summary +
                            (profile.GeoIpMaintenanceRecommended
                                ? " · Maintenance GeoIP recommandée."
                                : string.Empty) +
                            (profile.IsUncProfile ? " · " + profile.RemoteAgentStatus : string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Analyse impossible : racine inaccessible ou permission insuffisante.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // This is intentionally a file-only refresh: known local/UNC roots are
    // rescanned, but no Agent catalog, pairing, remote command or process is
    // touched. Pairing remains required only for remote lifecycle actions.
    public async Task RefreshKnownServerRootsAsync(CancellationToken cancellationToken = default)
    {
        var candidates = Profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.ServerRoot))
            .ToArray();
        if (candidates.Length == 0)
        {
            StatusMessage = "Aucun serveur enregistré à actualiser.";
            return;
        }

        IsBusy = true;
        var refreshed = 0;
        var unavailable = 0;
        try
        {
            foreach (var profile in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var analysis = await _analyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken);
                    profile.ApplyAnalysis(analysis);
                    if (analysis.BoiiiRootDetected)
                    {
                        profile.ApplyStorage(await _storageAnalyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken));
                    }
                    profile.ApplyServerRunning(_runtimeProbe.IsRunning(profile.ServerRoot, ParsePort(profile.RconPortText)));
                    refreshed++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    unavailable++;
                }
            }

            StatusMessage = unavailable == 0
                ? $"Actualisation terminée : {refreshed} serveur(s) relu(s). Aucun appairage ni commande distante n’a été utilisé."
                : $"Actualisation terminée : {refreshed} serveur(s) relu(s), {unavailable} inaccessible(s). Aucun appairage ni commande distante n’a été utilisé.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<ServerManagerProfileViewModel> PrepareOnboardingProfileAsync(
        bool remote,
        CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile;
        if (profile is null || !string.IsNullOrWhiteSpace(profile.ServerRoot))
        {
            await AddProfileAsync(cancellationToken);
            profile = SelectedProfile ?? throw new InvalidOperationException("Impossible de créer le profil serveur.");
        }

        if (!remote)
        {
            profile.RconAddress = "127.0.0.1";
        }
        StatusMessage = remote
            ? "Choisissez le dossier partagé du serveur sur le réseau. Le Control Center détectera ensuite automatiquement ce qu’il peut utiliser."
            : "Choisissez le dossier racine du serveur BOIII sur ce PC.";
        return profile;
    }

    public async Task AddProfileAsync(CancellationToken cancellationToken = default)
    {
        if (Profiles.Count >= OperatorWorkspaceConfiguration.MaximumProfileCount)
        {
            StatusMessage = $"Limite actuelle atteinte : {OperatorWorkspaceConfiguration.MaximumProfileCount} profils.";
            return;
        }

        var profileId = CreateProfileId();
        var displayName = $"Serveur {Profiles.Count + 1}";
        var configuration = OperatorConfiguration.Default with { ProfileDisplayName = displayName };
        var configurationStore = new JsonOperatorConfigurationStore(
            OperatorProfileStoragePaths.GetConfigurationPath(profileId));
        await configurationStore.SaveAsync(configuration, cancellationToken);
        await new JsonManagedServerProfileStore(OperatorProfileStoragePaths.GetManagedServerProfilePath(profileId))
            .SaveAsync(ManagedServerProfileConfiguration.Default, cancellationToken);

        var updatedIds = Profiles.Select(profile => profile.ProfileId).Append(profileId).ToArray();
        _workspaceConfiguration = new OperatorWorkspaceConfiguration(
            OperatorWorkspaceConfiguration.CurrentSchemaVersion,
            updatedIds,
            profileId)
        {
            AdvancedMode = IsAdvancedMode,
            KeepManagerOpenAfterControlCenter = KeepManagerOpenAfterControlCenter
        };
        await _workspaceStore.SaveAsync(_workspaceConfiguration, cancellationToken);
        var profileViewModel = new ServerManagerProfileViewModel(
            profileId,
            configuration,
            ManagedServerProfileConfiguration.Default);
        Profiles.Add(profileViewModel);
        SelectedProfile = profileViewModel;
        StatusMessage = "Nouveau profil créé. Indiquez sa racine BOIII puis ANALYSER.";
    }

    public async Task<bool> RemoveSelectedAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile;
        if (profile is null || Profiles.Count <= 1)
        {
            StatusMessage = "Au moins un profil serveur doit rester configuré.";
            return false;
        }

        var remaining = Profiles.Where(item => item != profile).ToArray();
        var active = string.Equals(_workspaceConfiguration.ActiveProfileId, profile.ProfileId, StringComparison.Ordinal)
            ? remaining[0].ProfileId
            : _workspaceConfiguration.ActiveProfileId;
        _workspaceConfiguration = new OperatorWorkspaceConfiguration(
            OperatorWorkspaceConfiguration.CurrentSchemaVersion,
            remaining.Select(item => item.ProfileId).ToArray(),
            active)
        {
            AdvancedMode = IsAdvancedMode,
            KeepManagerOpenAfterControlCenter = KeepManagerOpenAfterControlCenter
        };
        await _workspaceStore.SaveAsync(_workspaceConfiguration, cancellationToken);
        Profiles.Remove(profile);
        SelectedProfile = remaining.FirstOrDefault(item => string.Equals(item.ProfileId, active, StringComparison.Ordinal))
                          ?? remaining[0];
        StatusMessage = "Profil retiré du gestionnaire. Aucun fichier serveur ni secret local n’a été supprimé.";
        return true;
    }

    public Task SaveSelectedAsync(CancellationToken cancellationToken = default) =>
        SaveSelectedCoreAsync(refreshInstalledAgent: true, cancellationToken: cancellationToken);

    private async Task SaveSelectedCoreAsync(bool refreshInstalledAgent, CancellationToken cancellationToken)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        if (!int.TryParse(profile.RconPortText, out var rconPort) || rconPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("Port RCON invalide.");
        }

        var root = profile.ServerRoot?.Trim() ?? string.Empty;
        var analysis = root.Length == 0 ? null : _analyzer.Analyze(root, cancellationToken);
        if (analysis is not null)
        {
            profile.ApplyAnalysis(analysis);
        }

        var structuredSourceAvailable = analysis?.PinteModDetected == true ||
                                        analysis?.ControlCenterBridgeDetected == true ||
                                        analysis?.GenericBridgeDetected == true;
        var configuration = profile.OriginalConfiguration with
        {
            SchemaVersion = OperatorConfiguration.CurrentSchemaVersion,
            ProfileDisplayName = profile.DisplayName.Trim(),
            ServerRoot = root,
            DataLocation = root.StartsWith("\\\\", StringComparison.Ordinal)
                ? OperatorDataLocation.Lan
                : OperatorDataLocation.Local,
            ActivateDataSourceOnStartup = root.Length > 0 && structuredSourceAvailable,
            RconAddress = profile.RconAddress.Trim(),
            RconPort = rconPort
        };
        var configurationStore = new JsonOperatorConfigurationStore(
            OperatorProfileStoragePaths.GetConfigurationPath(profile.ProfileId));
        await configurationStore.SaveAsync(configuration, cancellationToken);
        await new JsonManagedServerProfileStore(OperatorProfileStoragePaths.GetManagedServerProfilePath(profile.ProfileId))
            .SaveAsync(new ManagedServerProfileConfiguration(
                ManagedServerProfileConfiguration.CurrentSchemaVersion,
                profile.LauncherRelativePath.Trim())
            {
                RemoteAgentId = profile.RemoteAgentId
            }, cancellationToken);
        profile.OriginalConfiguration = configuration;

        _workspaceConfiguration = _workspaceConfiguration with { ActiveProfileId = profile.ProfileId };
        await _workspaceStore.SaveAsync(_workspaceConfiguration, cancellationToken);
        StatusMessage = "Profil enregistré. Le secret RCON reste dans son stockage DPAPI séparé.";

        if (refreshInstalledAgent)
        {
            await RefreshInstalledLocalAgentRegistrationsIfNeededAsync(cancellationToken);
        }
    }

    public async Task<ServerDeploymentResult> PreparePinteModOneClickAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        await SaveSelectedAsync(cancellationToken);

        var analysis = _analyzer.Analyze(profile.ServerRoot, cancellationToken);
        profile.ApplyAnalysis(analysis);
        if (analysis.ThirdPartyGscDetected)
        {
            return new ServerDeploymentResult(
                false,
                "Préparation automatique refusée : des scripts tiers sont présents. Le Control Center les conserve et reste en mode limité tant qu’un provider compatible n’est pas prouvé.",
                [],
                []);
        }

        var pinteMod = await _payloadService.InstallPinteModStableAsync(profile.ServerRoot, cancellationToken);
        if (!pinteMod.Success)
        {
            StatusMessage = pinteMod.Message;
            return pinteMod;
        }

        // Simple mode is fail-closed for Change Map: install the current Bridge
        // with an empty allowlist. The operator can enable installed maps later
        // from Advanced mode without blocking the rest of the integration.
        var bridge = await _payloadService.InstallOrUpdateBridgeAsync(
            profile.ServerRoot,
            Array.Empty<string>(),
            cancellationToken);
        if (!bridge.Success)
        {
            StatusMessage = $"PinteMod installé, mais le module de compatibilité n’a pas pu être finalisé : {bridge.Message}";
            await AnalyzeSelectedAsync(cancellationToken);
            return new ServerDeploymentResult(
                false,
                StatusMessage,
                [.. pinteMod.CreatedFiles, .. bridge.CreatedFiles],
                [.. pinteMod.SkippedFiles, .. bridge.SkippedFiles]);
        }

        var created = new List<string>(pinteMod.CreatedFiles);
        created.AddRange(bridge.CreatedFiles);
        var skipped = new List<string>(pinteMod.SkippedFiles);
        skipped.AddRange(bridge.SkippedFiles);
        var agentMessage = string.Empty;

        await AnalyzeSelectedAsync(cancellationToken);
        if (!profile.IsUncProfile && profile.Analysis?.CanLaunchLocally == true)
        {
            var agent = await EnableRemoteAgentAsync(cancellationToken);
            agentMessage = agent.Success
                ? " · connexion à distance préparée sur ce PC"
                : " · PinteMod prêt, mais la connexion à distance reste à activer manuellement";
            created.AddRange(agent.CreatedFiles);
            skipped.AddRange(agent.SkippedFiles);
        }

        StatusMessage = "Serveur prêt : PinteMod + module Control Center installés" + agentMessage + ".";
        await AnalyzeSelectedAsync(cancellationToken);
        return new ServerDeploymentResult(true, StatusMessage, created, skipped);
    }

    public async Task<ServerDeploymentResult> InstallPinteModAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        await SaveSelectedAsync(cancellationToken);
        IsBusy = true;
        try
        {
            var analysis = _analyzer.Analyze(profile.ServerRoot, cancellationToken);
            profile.ApplyAnalysis(analysis);
            var result = analysis.PinteModDetected
                ? await _payloadService.RepairInstallationVerifierAsync(profile.ServerRoot, cancellationToken)
                : await _payloadService.InstallPinteModStableAsync(profile.ServerRoot, cancellationToken);
            StatusMessage = result.Message;
            await AnalyzeSelectedAsync(cancellationToken);
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<ServerDeploymentResult> InstallBridgeAsync(
        IReadOnlyCollection<string> allowedMaps,
        CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        await SaveSelectedAsync(cancellationToken);
        IsBusy = true;
        try
        {
            var result = await _payloadService.InstallOrUpdateBridgeAsync(
                profile.ServerRoot,
                allowedMaps,
                cancellationToken);
            StatusMessage = result.Message;
            await AnalyzeSelectedAsync(cancellationToken);
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AnalyzeStorageSelectedAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        IsBusy = true;
        try
        {
            var summary = await _storageAnalyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken);
            profile.ApplyStorage(summary);
            StatusMessage = summary.DisplaySummary;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<ServerDeploymentResult> RepairGeoIpSelectedAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        IsBusy = true;
        try
        {
            var result = await _payloadService.RepairGeoIpStatisticsAsync(profile.ServerRoot, cancellationToken);
            StatusMessage = result.Message;
            profile.ApplyStorage(await _storageAnalyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken));
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<ServerLaunchResult> LaunchSelectedAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        await SaveSelectedAsync(cancellationToken);
        var analysis = _analyzer.Analyze(profile.ServerRoot, cancellationToken);
        profile.ApplyAnalysis(analysis);
        profile.ApplyServerRunning(_runtimeProbe.IsRunning(profile.ServerRoot, ParsePort(profile.RconPortText)));
        if (profile.ServerRunning)
        {
            var alreadyRunning = new ServerLaunchResult(false, "Serveur déjà lancé : le bouton de démarrage est désactivé tant que BOIII est en ligne.");
            StatusMessage = alreadyRunning.Message;
            return alreadyRunning;
        }

        ServerLaunchResult result;
        if (analysis.IsUnc)
        {
            await RefreshRemoteAgentAsync(profile, cancellationToken);
            result = profile.CanLaunchRemote
                ? await _remoteLaunchClient.LaunchAsync(
                    profile.ServerRoot,
                    profile.ProfileId,
                    profile.RemoteAgentId,
                    cancellationToken)
                : new ServerLaunchResult(false, "Lancement distant indisponible : " + profile.RemoteAgentStatus);
        }
        else if (analysis.PinteModDetected && analysis.CanLaunchLocally)
        {
            var workerSecretReady = await EnsureWorkerSecretPreparedAsync(profile.ProfileId, cancellationToken);

            if (!workerSecretReady)
            {
                result = await _launchService.LaunchAsync(
                    profile.ServerRoot,
                    GetBootstrapLauncherRelativePath(profile),
                    cancellationToken);
                if (result.Success)
                {
                    result = result with
                    {
                        Message = "Premier lancement BOIII démarré avec Server.bat. Le Worker PinteMod sera utilisé automatiquement après l’initialisation ou l’enregistrement du secret RCON."
                    };
                }
            }
            else
            {
                var selected = BuildLaunchDefinition(profile);
                var hubServers = await GetAllLocalPinteModDefinitionsAsync(cancellationToken);
                result = await _orchestratorService.LaunchAsync(
                    [selected],
                    hubServers,
                    cancellationToken);
            }
        }
        else
        {
            result = await _launchService.LaunchAsync(
                profile.ServerRoot,
                profile.LauncherRelativePath,
                cancellationToken);
        }

        if (result.Success) profile.ApplyServerRunning(true);
        StatusMessage = result.Message;
        return result;
    }

    public async Task<ServerLaunchResult> StopSelectedAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        var port = ParsePort(profile.RconPortText);
        if (port == 0)
        {
            var invalid = new ServerLaunchResult(false, "Port serveur/RCON invalide pour l’arrêt.");
            StatusMessage = invalid.Message;
            return invalid;
        }

        profile.ApplyServerRunning(_runtimeProbe.IsRunning(profile.ServerRoot, port));
        if (!profile.ServerRunning)
        {
            var alreadyStopped = new ServerLaunchResult(true, "Serveur déjà arrêté.");
            StatusMessage = alreadyStopped.Message;
            return alreadyStopped;
        }

        ServerLaunchResult result;
        if (profile.IsUncProfile)
        {
            await RefreshRemoteAgentAsync(profile, cancellationToken);
            result = profile.CanLaunchRemote
                ? await _remoteLaunchClient.StopAsync(
                    profile.ServerRoot,
                    profile.ProfileId,
                    profile.RemoteAgentId,
                    cancellationToken)
                : new ServerLaunchResult(false, "Arrêt distant indisponible : " + profile.RemoteAgentStatus);
        }
        else
        {
            result = await _stopService.StopAsync(
                profile.ProfileId,
                profile.ServerRoot,
                port,
                cancellationToken);
        }

        if (result.Success)
        {
            profile.ApplyServerRunning(false);
            await Task.Delay(500, cancellationToken);
        }
        StatusMessage = result.Message;
        return result;
    }

    public async Task<ServerLaunchResult> LaunchAllLocalPinteModAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProfile is not null)
        {
            await SaveSelectedAsync(cancellationToken);
        }

        var localDefinitions = await GetAllLocalPinteModDefinitionsAsync(cancellationToken);
        var messages = new List<string>();
        var successes = 0;
        var failures = 0;

        var workerDefinitions = new List<MultiServerLaunchDefinition>();
        var bootstrapDefinitions = new List<MultiServerLaunchDefinition>();
        foreach (var definition in localDefinitions)
        {
            if (await EnsureWorkerSecretPreparedAsync(definition.ProfileId, cancellationToken))
            {
                workerDefinitions.Add(definition);
            }
            else
            {
                bootstrapDefinitions.Add(definition);
            }
        }

        if (workerDefinitions.Count > 0)
        {
            var localResult = await _orchestratorService.LaunchAsync(workerDefinitions, workerDefinitions, cancellationToken);
            messages.Add(localResult.Message);
            if (localResult.Success) successes += workerDefinitions.Count; else failures += workerDefinitions.Count;
        }

        foreach (var definition in bootstrapDefinitions)
        {
            var bootstrap = await _launchService.LaunchAsync(
                definition.ServerRoot,
                GetBootstrapLauncherRelativePath(definition.ServerRoot, definition.LauncherRelativePath),
                cancellationToken);
            messages.Add($"{definition.DisplayName}: " + (bootstrap.Success
                ? "premier lancement BOIII démarré avec Server.bat ; le Worker sera utilisé après la configuration RCON."
                : bootstrap.Message));
            if (bootstrap.Success) successes++; else failures++;
        }

        foreach (var profile in Profiles.Where(item => item.IsUncProfile))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ManagedServerAnalysis analysis;
            try
            {
                analysis = await _analyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures++;
                messages.Add($"{profile.DisplayName}: racine UNC inaccessible.");
                continue;
            }

            profile.ApplyAnalysis(analysis);
            await RefreshRemoteAgentAsync(profile, cancellationToken);
            if (!profile.CanLaunchRemote)
            {
                failures++;
                messages.Add($"{profile.DisplayName}: {profile.RemoteAgentStatus}");
                continue;
            }

            var remote = await _remoteLaunchClient.LaunchAsync(
                profile.ServerRoot, profile.ProfileId, profile.RemoteAgentId, cancellationToken);
            messages.Add($"{profile.DisplayName}: {remote.Message}");
            if (remote.Success) successes++; else failures++;
        }

        if (successes == 0 && failures == 0)
        {
            var none = new ServerLaunchResult(false, "Aucun profil local lançable ni profil distant pairé n’est disponible.");
            StatusMessage = none.Message;
            return none;
        }

        var result = new ServerLaunchResult(
            failures == 0,
            $"Lancement groupé : {successes} succès, {failures} échec(s). " + string.Join(" | ", messages));
        StatusMessage = result.Message;
        return result;
    }

    private async Task<IReadOnlyList<MultiServerLaunchDefinition>> GetAllLocalPinteModDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        var result = new List<MultiServerLaunchDefinition>();
        foreach (var profile in Profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(profile.ServerRoot) ||
                profile.ServerRoot.StartsWith("\\\\", StringComparison.Ordinal))
            {
                continue;
            }

            ManagedServerAnalysis analysis;
            try
            {
                analysis = await _analyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            profile.ApplyAnalysis(analysis);
            if (!analysis.PinteModDetected || !analysis.CanLaunchLocally)
            {
                continue;
            }

            if (!int.TryParse(profile.RconPortText, out var port) || port is < 1 or > 65535)
            {
                continue;
            }

            result.Add(new MultiServerLaunchDefinition(
                profile.ProfileId,
                profile.DisplayName,
                profile.ServerRoot,
                profile.LauncherRelativePath,
                port));
        }

        return result;
    }

    private async Task<bool> EnsureWorkerSecretPreparedAsync(string profileId, CancellationToken cancellationToken)
    {
        if (_orchestratorService.IsRconSecretPrepared(profileId))
        {
            return true;
        }

        var secret = await new DpapiRconSecretStore(
            OperatorProfileStoragePaths.GetRconSecretPath(profileId)).ReadAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(secret) &&
               await _orchestratorService.PrepareRconSecretAsync(profileId, secret, cancellationToken);
    }

    private static int ParsePort(string value) =>
        int.TryParse(value, out var port) && port is >= 1 and <= 65535 ? port : 0;

    private static MultiServerLaunchDefinition BuildLaunchDefinition(ServerManagerProfileViewModel profile)
    {
        if (!int.TryParse(profile.RconPortText, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Port serveur/RCON invalide pour le profil sélectionné.");
        }

        return new MultiServerLaunchDefinition(
            profile.ProfileId,
            profile.DisplayName,
            profile.ServerRoot,
            profile.LauncherRelativePath,
            port);
    }

    private static string GetBootstrapLauncherRelativePath(ServerManagerProfileViewModel profile)
        => GetBootstrapLauncherRelativePath(profile.ServerRoot, profile.LauncherRelativePath);

    private static string GetBootstrapLauncherRelativePath(string serverRoot, string selected)
    {
        var selectedName = Path.GetFileName(selected);
        var directServerBat = Path.Combine(serverRoot, "Server.bat");
        return selectedName.StartsWith("Launch_PinteMod_", StringComparison.OrdinalIgnoreCase) && File.Exists(directServerBat)
            ? "Server.bat"
            : selected;
    }

    public async Task<ServerDeploymentResult> EnableRemoteAgentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (SelectedProfile is not null)
            {
                await SaveSelectedCoreAsync(refreshInstalledAgent: false, cancellationToken: cancellationToken);
            }
            var definitions = await GetAllLocalAgentDefinitionsAsync(cancellationToken);
            var result = await _remoteAgentInstaller.InstallOrUpdateAsync(definitions, cancellationToken);
            StatusMessage = result.Message;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var result = new ServerDeploymentResult(
                false,
                RemoteAgentActivationDiagnostic.Describe(exception),
                Array.Empty<string>(),
                Array.Empty<string>());
            StatusMessage = result.Message;
            return result;
        }
    }

    public async Task<ServerLaunchResult> UpdateSelectedRemoteAgentAsync(
        IProgress<RemoteAgentUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        await SaveSelectedAsync(cancellationToken);
        var result = await _remoteLaunchClient.UpdateAgentAsync(
            profile.ServerRoot,
            profile.ProfileId,
            profile.RemoteAgentId,
            progress,
            cancellationToken);
        await RefreshRemoteAgentAsync(profile, cancellationToken);
        StatusMessage = result.Message;
        return result;
    }

    public async Task<RemoteAgentPairingResult> PairSelectedRemoteAgentAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");
        await SaveSelectedAsync(cancellationToken);
        var result = await _remoteLaunchClient.PairAsync(profile.ServerRoot, profile.ProfileId, cancellationToken);
        if (result.Success && !string.IsNullOrWhiteSpace(result.AgentId))
        {
            profile.SetRemoteAgentId(result.AgentId);
            await new JsonManagedServerProfileStore(OperatorProfileStoragePaths.GetManagedServerProfilePath(profile.ProfileId))
                .SaveAsync(new ManagedServerProfileConfiguration(
                    ManagedServerProfileConfiguration.CurrentSchemaVersion,
                    profile.LauncherRelativePath.Trim())
                {
                    RemoteAgentId = result.AgentId
                }, cancellationToken);
            await RefreshRemoteAgentAsync(profile, cancellationToken);
        }
        StatusMessage = result.Message;
        return result;
    }

    public async Task RefreshSelectedRemoteAgentAsync(CancellationToken cancellationToken = default)
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Aucun profil sélectionné.");

        // Refresh every known remote link. A catalog belonging to Server3 may
        // legitimately announce Server4, even when another/local profile is
        // currently selected in the Manager.
        foreach (var remote in Profiles.Where(item => item.IsUncProfile).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RefreshRemoteAgentAsync(remote, cancellationToken);
        }
        if (!profile.IsUncProfile)
        {
            await RefreshRemoteAgentAsync(profile, cancellationToken);
        }

        var summary = await ImportAuthenticatedRemoteCatalogProfilesAsync(cancellationToken);
        if (summary.ImportedProfiles > 0)
        {
            StatusMessage = $"Catalogue Agent authentifié · {summary.ImportedProfiles} nouveau(x) serveur(s) ajouté(s).";
            return;
        }

        if (summary.CandidateSources == 0)
        {
            StatusMessage = "Découverte distante : aucun profil UNC enregistré sur ce PC. Ajoutez ou conservez au moins un serveur réseau déjà connu.";
            return;
        }
        if (summary.PairedSources == 0)
        {
            StatusMessage = "Découverte distante : aucun profil UNC n’est appairé avec un Agent. Le catalogue Server4 ne peut pas être authentifié.";
            return;
        }
        if (summary.AuthenticatedCatalogs == 0)
        {
            StatusMessage = "Découverte distante : " +
                            (summary.Diagnostics.FirstOrDefault() ?? "aucun catalogue Agent authentifié n’a été reçu.");
            return;
        }
        if (summary.UnresolvedProfiles > 0)
        {
            StatusMessage = $"Catalogue Agent reçu, mais {summary.UnresolvedProfiles} serveur(s) annoncé(s) n’ont pas de racine UNC sœur accessible depuis ce PC. " +
                            (summary.Diagnostics.LastOrDefault() ?? string.Empty);
            return;
        }

        StatusMessage = summary.KnownProfiles > 0
            ? $"Catalogue Agent authentifié · aucun nouveau serveur ({summary.KnownProfiles} déjà connu(s))."
            : "Catalogue Agent authentifié · aucun nouveau serveur annoncé.";
    }

    private async Task<IReadOnlyList<RemoteAgentRegistrationDefinition>> GetAllLocalAgentDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        var definitions = new List<RemoteAgentRegistrationDefinition>();
        foreach (var profile in Profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(profile.ServerRoot) || profile.ServerRoot.StartsWith(@"\\", StringComparison.Ordinal)) continue;
            ManagedServerAnalysis analysis;
            try
            {
                analysis = await _analyzer.AnalyzeAsync(profile.ServerRoot, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            profile.ApplyAnalysis(analysis);
            if (!analysis.BoiiiRootDetected || !analysis.CanLaunchLocally) continue;
            if (!int.TryParse(profile.RconPortText, out var port) || port is < 1 or > 65535) continue;
            definitions.Add(new RemoteAgentRegistrationDefinition(
                profile.ProfileId,
                profile.DisplayName,
                profile.ServerRoot,
                profile.LauncherRelativePath,
                port,
                analysis.PinteModDetected));
        }
        return definitions;
    }

    private async Task RefreshInstalledLocalAgentRegistrationsIfNeededAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(RemoteAgentConfigurationStore.GetExecutablePath())) return;
        var definitions = await GetAllLocalAgentDefinitionsAsync(cancellationToken);
        if (definitions.Count == 0 ||
            !await _remoteAgentInstaller.NeedsRegistrationRefreshAsync(definitions, cancellationToken))
        {
            return;
        }

        var result = await _remoteAgentInstaller.InstallOrUpdateAsync(
            definitions, cancellationToken, hardenPinteModTooling: false);
        if (!result.Success)
        {
            StatusMessage = "Le profil a été enregistré, mais le catalogue de l’Agent local n’a pas pu être actualisé : " + result.Message;
        }
    }

    private async Task RunOptionalStartupStepAsync(
        Func<Task> operation,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Keep this deliberately generic: startup diagnostics must not leak
            // a private UNC path, local path, RCON setting, or agent secret.
            StatusMessage = failureMessage + " Le gestionnaire reste disponible pour réessayer ou corriger la source.";
        }
    }

    private async Task<RemoteCatalogImportSummary> ImportAuthenticatedRemoteCatalogProfilesAsync(CancellationToken cancellationToken)
    {
        var imported = 0;
        var authenticated = 0;
        var known = 0;
        var unresolved = 0;
        var diagnostics = new List<string>();
        var remoteProfiles = Profiles.Where(profile => profile.IsUncProfile).ToArray();
        var sources = remoteProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.RemoteAgentId))
            .ToArray();
        var pairedSources = sources.Count(profile => profile.RemoteAgentPaired);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!source.RemoteAgentPaired)
            {
                diagnostics.Add($"{source.DisplayName} : Agent non appairé ou statut non authentifié.");
                continue;
            }

            var catalog = await _remoteLaunchClient.ReadProfileCatalogAsync(
                source.ServerRoot, source.ProfileId, source.RemoteAgentId, cancellationToken);
            if (!catalog.Success)
            {
                diagnostics.Add($"{source.DisplayName} : {catalog.Message}");
                continue;
            }
            authenticated++;

            foreach (var entry in catalog.Profiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Profiles.Count >= OperatorWorkspaceConfiguration.MaximumProfileCount) break;
                if (string.Equals(entry.AgentId, source.RemoteAgentId, StringComparison.Ordinal) ||
                    Profiles.Any(profile => string.Equals(profile.RemoteAgentId, entry.AgentId, StringComparison.Ordinal)))
                {
                    known++;
                    continue;
                }

                string? remoteRoot = null;
                foreach (var candidate in RemoteAgentCatalogPathResolver.BuildSiblingCandidates(source.ServerRoot, entry.RootFolderName))
                {
                    try
                    {
                        if (Directory.Exists(Path.Combine(candidate, "boiii")))
                        {
                            remoteRoot = candidate;
                            break;
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                    }
                }
                if (string.IsNullOrWhiteSpace(remoteRoot))
                {
                    unresolved++;
                    diagnostics.Add($"{entry.DisplayName} : catalogue reçu, mais aucun dossier réseau sœur contenant BOIII n’est accessible.");
                    continue;
                }
                if (Profiles.Any(profile => string.Equals(
                        profile.ServerRoot.TrimEnd('\\', '/'),
                        remoteRoot.TrimEnd('\\', '/'),
                        StringComparison.OrdinalIgnoreCase)))
                {
                    known++;
                    continue;
                }

                ManagedServerAnalysis analysis;
                try
                {
                    analysis = await _analyzer.AnalyzeAsync(remoteRoot, cancellationToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    unresolved++;
                    diagnostics.Add($"{entry.DisplayName} : racine réseau trouvée mais analyse refusée/inaccessible.");
                    continue;
                }
                if (!analysis.BoiiiRootDetected)
                {
                    unresolved++;
                    diagnostics.Add($"{entry.DisplayName} : racine réseau trouvée mais BOIII n’y est pas prouvé.");
                    continue;
                }

                var profileId = CreateProfileId();
                var structuredSourceAvailable = analysis.PinteModDetected ||
                                                analysis.ControlCenterBridgeDetected ||
                                                analysis.GenericBridgeDetected;
                var configuration = OperatorConfiguration.Default with
                {
                    ProfileDisplayName = entry.DisplayName,
                    AccentColorKey = OperatorAccentTheme.DefaultKey,
                    ServerRoot = remoteRoot,
                    DataLocation = OperatorDataLocation.Lan,
                    ActivateDataSourceOnStartup = structuredSourceAvailable,
                    RconAddress = source.OriginalConfiguration.RconAddress,
                    RconPort = entry.ServerPort
                };
                await new JsonOperatorConfigurationStore(OperatorProfileStoragePaths.GetConfigurationPath(profileId))
                    .SaveAsync(configuration, cancellationToken);
                var managed = new ManagedServerProfileConfiguration(
                    ManagedServerProfileConfiguration.CurrentSchemaVersion,
                    entry.LauncherRelativePath)
                {
                    RemoteAgentId = entry.AgentId
                };
                await new JsonManagedServerProfileStore(OperatorProfileStoragePaths.GetManagedServerProfilePath(profileId))
                    .SaveAsync(managed, cancellationToken);

                var viewModel = new ServerManagerProfileViewModel(profileId, configuration, managed);
                viewModel.ApplyAnalysis(analysis);
                Profiles.Add(viewModel);
                await RefreshRemoteAgentAsync(viewModel, cancellationToken);
                imported++;
            }
        }

        if (imported > 0)
        {
            _workspaceConfiguration = new OperatorWorkspaceConfiguration(
                OperatorWorkspaceConfiguration.CurrentSchemaVersion,
                Profiles.Select(profile => profile.ProfileId).ToArray(),
                _workspaceConfiguration.ActiveProfileId)
            {
                AdvancedMode = IsAdvancedMode,
                KeepManagerOpenAfterControlCenter = KeepManagerOpenAfterControlCenter
            };
            await _workspaceStore.SaveAsync(_workspaceConfiguration, cancellationToken);
        }

        return new RemoteCatalogImportSummary(
            remoteProfiles.Length, pairedSources, authenticated, imported, known, unresolved, diagnostics);
    }

    private async Task RefreshRemoteAgentAsync(ServerManagerProfileViewModel profile, CancellationToken cancellationToken)
    {
        if (!profile.IsUncProfile)
        {
            profile.ApplyRemoteAgentProbe(new RemoteAgentProbeResult(false, false, false, "Agent local : utilisez ACTIVER AGENT DISTANT sur le PC serveur."));
            return;
        }

        var probe = await _remoteLaunchClient.ProbeAsync(
            profile.ServerRoot,
            profile.ProfileId,
            profile.RemoteAgentId,
            cancellationToken);
        profile.ApplyRemoteAgentProbe(probe);
        RefreshUpdateAttention();
    }

    public async Task SelectForControlCenterAsync(CancellationToken cancellationToken = default)
    {
        await SaveSelectedAsync(cancellationToken);
    }

    private string CreateProfileId()
    {
        string profileId;
        do
        {
            profileId = $"srv-{Guid.NewGuid():N}"[..16];
        }
        while (Profiles.Any(profile => string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal)));
        return profileId;
    }
}
