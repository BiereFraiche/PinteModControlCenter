using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.Converters;
using PinteMod.ControlCenter.Infrastructure.Simulation;
using PinteMod.ControlCenter.State;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ViewModelTests
{
    [TestMethod]
    public async Task Shell_StartsOnDashboard_AndNavigatesToAllSixPages()
    {
        var composition = CreateComposition(SimulationScenario.Healthy);
        await composition.Shell.InitializeAsync();

        Assert.AreEqual("Dashboard", composition.Shell.CurrentPage.Title);
        CollectionAssert.AreEqual(
            new[] { "Dashboard", "Joueurs", "Serveur", "Records", "Logs", "Paramètres" },
            composition.Shell.NavigationItems.Select(item => item.Title).ToArray());

        foreach (var item in composition.Shell.NavigationItems)
        {
            Assert.IsTrue(composition.Shell.NavigateTo(item.Title));
            Assert.AreSame(item.Page, composition.Shell.CurrentPage);
            Assert.AreEqual(1, composition.Shell.NavigationItems.Count(candidate => candidate.IsSelected));
        }
    }


    [TestMethod]
    public void Shell_RealUnstructuredServer_DisablesUnprovedDataPages()
    {
        var store = new CachedControlCenterSnapshotStore(
            new SimulatedControlCenterDataProvider(SimulationScenario.Healthy));
        var simulation = new SimulationActionService();
        var selection = new PlayerSelectionState();
        var shell = new ShellViewModel(
            store,
            new DashboardViewModel(store, simulation, selection),
            new PlayersViewModel(store, simulation, selection),
            new ServerViewModel(store, simulation),
            new RecordsViewModel(store),
            new LogsViewModel(store),
            new SettingsViewModel(),
            startClock: false,
            restrictUnprovedCapabilities: true);

        var players = shell.NavigationItems.Single(item => item.Title == "Joueurs");
        var records = shell.NavigationItems.Single(item => item.Title == "Records");
        Assert.IsFalse(players.IsEnabled);
        Assert.IsFalse(records.IsEnabled);
        Assert.IsFalse(shell.NavigateTo("Joueurs"));
        Assert.AreEqual("Dashboard", shell.CurrentPage.Title);
    }

    [TestMethod]
    public void Shell_AdaptiveThirdPartyProfile_GraysUnprovedPages()
    {
        var store = new CachedControlCenterSnapshotStore(
            new SimulatedControlCenterDataProvider(SimulationScenario.Healthy));
        var simulation = new SimulationActionService();
        var selection = new PlayerSelectionState();
        var profile = new ServerIntegrationProfile(
            ManagedServerIntegrationKind.ThirdPartyScripts,
            "GSC tiers · audit read-only",
            IntegrationCommandTransport.None,
            [
                new IntegrationCapability(IntegrationCapabilityKey.ServerLifecycle, IntegrationCapabilityAvailability.Available, "BOIII", "BOIII"),
                new IntegrationCapability(IntegrationCapabilityKey.Players, IntegrationCapabilityAvailability.Observed, "Hooks observés", "Audit"),
                new IntegrationCapability(IntegrationCapabilityKey.Chat, IntegrationCapabilityAvailability.Observed, "Hooks observés", "Audit")
            ],
            ThirdPartyGscAudit.Empty);
        var server = new ServerViewModel(store, simulation, integrationProfile: profile);
        var shell = new ShellViewModel(
            store,
            new DashboardViewModel(store, simulation, selection),
            new PlayersViewModel(store, simulation, selection),
            server,
            new RecordsViewModel(store),
            new LogsViewModel(store),
            new SettingsViewModel(),
            startClock: false,
            integrationProfile: profile);

        Assert.IsFalse(shell.NavigationItems.Single(item => item.Title == "Joueurs").IsEnabled);
        Assert.IsFalse(shell.NavigationItems.Single(item => item.Title == "Records").IsEnabled);
        Assert.IsFalse(server.SupportsPinteModClosedCommands);
    }

    [TestMethod]
    public void ServerView_PinteModProfile_RecognizesClosedCommandTransport()
    {
        var store = new CachedControlCenterSnapshotStore(
            new SimulatedControlCenterDataProvider(SimulationScenario.Healthy));
        var profile = new ServerIntegrationProfile(
            ManagedServerIntegrationKind.PinteMod,
            "PinteMod",
            IntegrationCommandTransport.PinteModClosedRconV1,
            [],
            ThirdPartyGscAudit.Empty);

        var server = new ServerViewModel(
            store,
            new SimulationActionService(),
            integrationProfile: profile);

        Assert.IsTrue(server.SupportsPinteModClosedCommands);
    }

    [TestMethod]
    public void AdaptiveSettingsAndDashboard_ShowThirdPartyLimitedMode()
    {
        var profile = new ServerIntegrationProfile(
            ManagedServerIntegrationKind.ThirdPartyScripts,
            "GSC tiers · audit read-only",
            IntegrationCommandTransport.None,
            [new IntegrationCapability(IntegrationCapabilityKey.ServerLifecycle, IntegrationCapabilityAvailability.Available, "BOIII", "BOIII")],
            ThirdPartyGscAudit.Empty);
        var settings = new SettingsViewModel(integrationProfile: profile);
        var store = new CachedControlCenterSnapshotStore(
            new SimulatedControlCenterDataProvider(SimulationScenario.Healthy));
        var dashboard = new DashboardViewModel(
            store,
            new SimulationActionService(),
            new PlayerSelectionState(),
            integrationProfile: profile);

        Assert.AreEqual("MODE ADAPTATIF LIMITÉ", settings.ModeLabel);
        Assert.AreEqual("ADP", settings.ModeShortLabel);
        Assert.AreEqual("GSC TIERS · MODE LIMITÉ", dashboard.IntegrationProviderLabel);
    }

    [TestMethod]
    public async Task Dashboard_StartServer_UsesRegisteredProfileLauncher()
    {
        var store = new MutableSnapshotStore(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy));
        var launched = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dashboard = new DashboardViewModel(
            store,
            new SimulationActionService(),
            new PlayerSelectionState(),
            serverLaunchAction: () =>
            {
                launched.TrySetResult(true);
                return Task.FromResult(new ServerLaunchResult(true, "Serveur démarré."));
            });

        Assert.IsTrue(dashboard.CanStartServer);
        Assert.IsTrue(dashboard.StartServerCommand.CanExecute(null));
        dashboard.StartServerCommand.Execute(null);

        await launched.Task.WaitAsync(TimeSpan.FromSeconds(2));
        for (var attempt = 0; attempt < 20 && dashboard.StartServerCommand.IsExecuting; attempt++)
        {
            await Task.Delay(50);
        }

        Assert.IsFalse(dashboard.StartServerCommand.IsExecuting);
        Assert.AreEqual("Serveur démarré.", dashboard.ServerLaunchStatus);
    }

    [TestMethod]
    public async Task Dashboard_StartServer_IsDisabledWhenHybridServerIsAlreadyRunning()
    {
        var simulated = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var snapshot = simulated with
        {
            Server = simulated.Server with { ServerRunning = true, ServerRunningAvailable = true },
            DataContext = simulated.DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE HYBRIDE LOCAL",
                ServerRoot = "C:\\Server3"
            }
        };
        var dashboard = new DashboardViewModel(
            new MutableSnapshotStore(snapshot),
            new SimulationActionService(),
            new PlayerSelectionState(),
            serverLaunchAction: () => Task.FromResult(new ServerLaunchResult(true, "Ne doit pas être appelé.")));

        await dashboard.InitializeAsync();

        Assert.IsTrue(dashboard.ServerAlreadyRunning);
        Assert.IsFalse(dashboard.CanStartServer);
        Assert.IsFalse(dashboard.StartServerCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task Dashboard_StopServer_IsEnabledOnlyWhenServerIsRunning()
    {
        var simulated = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var running = simulated with
        {
            Server = simulated.Server with { ServerRunning = true, ServerRunningAvailable = true },
            DataContext = simulated.DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE HYBRIDE LOCAL",
                ServerRoot = "C:\\Server3"
            }
        };
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new MutableSnapshotStore(running);
        var dashboard = new DashboardViewModel(
            store,
            new SimulationActionService(),
            new PlayerSelectionState(),
            confirmationService: new AlwaysConfirmService(),
            serverLaunchAction: () => Task.FromResult(new ServerLaunchResult(false, "Déjà lancé.")),
            serverStopAction: () =>
            {
                store.SetSnapshot(running with
                {
                    Server = running.Server with { ServerRunning = false, ServerRunningAvailable = true },
                    Services = []
                });
                stopped.TrySetResult(true);
                return Task.FromResult(new ServerLaunchResult(true, "Serveur arrêté."));
            });

        await dashboard.InitializeAsync();

        Assert.IsTrue(dashboard.ServerAlreadyRunning);
        Assert.IsTrue(dashboard.CanStopServer);
        Assert.IsFalse(dashboard.CanStartServer);
        dashboard.StopServerCommand.Execute(null);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));
        for (var attempt = 0; attempt < 50 && dashboard.StopServerCommand.IsExecuting; attempt++)
        {
            await Task.Delay(50);
        }

        Assert.IsFalse(dashboard.StopServerCommand.IsExecuting);
        Assert.IsFalse(dashboard.ServerAlreadyRunning);
        Assert.IsFalse(dashboard.CanStopServer);
        Assert.IsTrue(dashboard.CanStartServer);
    }

    [TestMethod]
    public async Task Dashboard_RemoteServerControlsStayDisabledWhenAuthenticatedTransportIsUnavailable()
    {
        var simulated = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var running = simulated with
        {
            Server = simulated.Server with { ServerRunning = true, ServerRunningAvailable = true },
            DataContext = simulated.DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE RÉEL · BOIII NATIF",
                ServerRoot = @"\\server\share\Server4"
            }
        };
        var dashboard = new DashboardViewModel(
            new MutableSnapshotStore(running),
            new SimulationActionService(),
            new PlayerSelectionState(),
            serverLaunchAction: () => Task.FromResult(new ServerLaunchResult(true, "Ne doit pas être appelé.")),
            serverStopAction: () => Task.FromResult(new ServerLaunchResult(true, "Ne doit pas être appelé.")),
            serverRunningProbe: () => true,
            serverControlTransportAvailabilityProbe: _ => Task.FromResult(false));

        await dashboard.InitializeAsync();

        Assert.IsFalse(dashboard.ServerControlTransportAvailable);
        Assert.IsFalse(dashboard.CanStartServer);
        Assert.IsFalse(dashboard.CanStopServer);
        Assert.IsFalse(dashboard.StartServerCommand.CanExecute(null));
        Assert.IsFalse(dashboard.StopServerCommand.CanExecute(null));
        StringAssert.Contains(dashboard.ServerControlTransportStatus, "Agent SMB");
    }

    [TestMethod]
    public async Task Dashboard_RealNativeStoppedSnapshotDisplaysObservedServerStateWithoutGameplayData()
    {
        var simulated = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var real = simulated with
        {
            Server = simulated.Server with
            {
                ServerRunning = false,
                ServerRunningAvailable = true,
                RoundAvailable = false,
                PlayersConnectedAvailable = false,
                SessionDurationAvailable = false,
                RankedStatusAvailable = false,
                MapProvenance = DataProvenance.Unavailable,
                SessionProvenance = DataProvenance.Unavailable
            },
            DataContext = simulated.DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE RÉEL · BOIII NATIF",
                ServerRoot = @"C:\Server4",
                SessionSource = LocalSourceMetadata.Unavailable("Aucune télémétrie structurée")
            },
            Players = [],
            Services = [],
            Events = []
        };
        var nativeProfile = new ServerIntegrationProfile(
            ManagedServerIntegrationKind.BoiiiNative,
            "BOIII natif",
            IntegrationCommandTransport.None,
            [],
            ThirdPartyGscAudit.Empty);
        var dashboard = new DashboardViewModel(
            new MutableSnapshotStore(real),
            new SimulationActionService(),
            new PlayerSelectionState(),
            integrationProfile: nativeProfile,
            serverRunningProbe: () => false);

        await dashboard.InitializeAsync();

        Assert.AreEqual("SERVEUR ARRÊTÉ · PROCESSUS BOIII ABSENT", dashboard.ServerRuntimeStatusLabel);
        Assert.AreEqual("—", dashboard.RoundDisplay);
        Assert.AreEqual("— / —", dashboard.PlayersDisplay);
        StringAssert.Contains(dashboard.ModeSummary, "aucune donnée de démonstration");
    }

    [TestMethod]
    public async Task PlayerSelection_IsSharedBetweenDashboardAndPlayersByXuid()
    {
        var composition = CreateComposition(SimulationScenario.Healthy);
        await composition.Shell.InitializeAsync();
        var mason = composition.Dashboard.Players.Single(player => player.DisplayName == "Mason");

        composition.Dashboard.SelectedPlayer = mason;

        Assert.AreEqual(mason.DisplayName, composition.Players.SelectedPlayer?.DisplayName);
        Assert.AreNotSame(mason, composition.Players.SelectedPlayer);
    }

    [TestMethod]
    public async Task SelectedPlayer_WhenMissing_IsExplicitlyInvalidated()
    {
        var initial = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var store = new MutableSnapshotStore(initial);
        var service = new SimulationActionService();
        var selection = new PlayerSelectionState();
        var dashboard = new DashboardViewModel(store, service, selection);
        var players = new PlayersViewModel(store, service, selection);
        await dashboard.InitializeAsync();
        await players.InitializeAsync();
        dashboard.SelectedPlayer = dashboard.Players.Single(player => player.DisplayName == "Mason");

        store.SetSnapshot(SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Empty));
        await players.InitializeAsync();

        Assert.IsNull(selection.SelectedXuid);
        Assert.IsNull(players.SelectedPlayer);
        Assert.IsNull(dashboard.SelectedPlayer);
    }

    [TestMethod]
    public async Task PlayerAction_IsDisabledWhenThereIsNoPlayer()
    {
        var composition = CreateComposition(SimulationScenario.Empty);
        await composition.Shell.InitializeAsync();

        Assert.IsFalse(composition.Dashboard.SimulatePlayerActionCommand.CanExecute(SimulationAction.KickPlayer));
        Assert.IsFalse(composition.Players.SimulatePlayerActionCommand.CanExecute(SimulationAction.RevivePlayer));
    }

    [TestMethod]
    public async Task PlayerAction_UsesXuidAndNeverDisplayName()
    {
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var store = new MutableSnapshotStore(snapshot);
        var capture = new CapturingSimulationService();
        var dashboard = new DashboardViewModel(store, capture, new PlayerSelectionState());
        await dashboard.InitializeAsync();
        dashboard.SelectedPlayer = dashboard.Players.Single(player => player.DisplayName == "Mason");

        dashboard.SimulatePlayerActionCommand.Execute(SimulationAction.KickPlayer);

        Assert.IsNotNull(capture.LastRequest);
        Assert.AreEqual("0000000000000003", capture.LastRequest.TargetXuid);
        Assert.AreNotEqual("Mason", capture.LastRequest.TargetXuid);
        Assert.AreEqual("Mason", dashboard.LastSimulationResult?.TargetDisplay);
        Assert.AreEqual("false", dashboard.LastSimulationResult?.CommandSent);
    }

    [TestMethod]
    public async Task Logs_SelectedFilter_HasDistinctMvvmState()
    {
        var store = new MutableSnapshotStore(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy));
        var logs = new LogsViewModel(store);
        await logs.InitializeAsync();
        var system = logs.Filters.Single(filter => filter.Key == "SYSTÈME");

        logs.SelectFilterCommand.Execute(system);

        Assert.AreEqual("SYSTÈME", logs.SelectedFilter);
        Assert.IsTrue(system.IsSelected);
        Assert.IsTrue(logs.Filters.Where(filter => filter != system).All(filter => !filter.IsSelected));
    }

    [TestMethod]
    public async Task Logs_FilterAndSearch_AreCombined()
    {
        var store = new MutableSnapshotStore(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy));
        var logs = new LogsViewModel(store);
        await logs.InitializeAsync();
        logs.SelectFilterCommand.Execute(logs.Filters.Single(filter => filter.Key == "SYSTÈME"));

        logs.SearchText = "diagnostic";

        Assert.AreEqual(1, logs.Events.Count);
        Assert.AreEqual("Diagnostic sain", logs.Events[0].Title);
    }

    [TestMethod]
    public async Task Logs_SearchWithoutMatch_ExposesEmptyState()
    {
        var store = new MutableSnapshotStore(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy));
        var logs = new LogsViewModel(store);
        await logs.InitializeAsync();

        logs.SearchText = "aucun-résultat-attendu";

        Assert.IsFalse(logs.HasEvents);
        Assert.AreEqual(0, logs.Events.Count);
    }

    [TestMethod]
    public async Task Records_RankedStatus_ComesFromSnapshot()
    {
        var healthy = new RecordsViewModel(new MutableSnapshotStore(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy)));
        var warning = new RecordsViewModel(new MutableSnapshotStore(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Warning)));

        await healthy.InitializeAsync();
        await warning.InitializeAsync();

        Assert.AreEqual(RankedStatus.Ranked, healthy.RankedStatus);
        Assert.AreEqual(RankedStatus.Unranked, warning.RankedStatus);
        Assert.AreEqual("SuccessBrush", StatusBrushConverter.GetResourceKey(healthy.RankedStatus));
        Assert.AreEqual("WarningBrush", StatusBrushConverter.GetResourceKey(warning.RankedStatus));
        StringAssert.Contains(healthy.CurrentMapProfile, healthy.Server!.MapName.ToUpperInvariant());
    }

    [TestMethod]
    public async Task Records_HybridSourcesExposeOnlyAbbreviatedXuidsAndSourceState()
    {
        var simulated = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        const string fullXuid = "abcdef0123456789";
        var source = new LocalSourceMetadata(
            LocalReadStatus.Success,
            DataFreshness.Fresh,
            TimeSpan.FromMinutes(2),
            DataProvenance.LocalFile,
            "ranks_v2/players/*.json",
            "Lecture réussie.");
        var snapshot = simulated with
        {
            DataContext = simulated.DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE HYBRIDE LOCAL",
                ServerRoot = "C:\\test"
            },
            RankRecords = new RankRecordsSnapshot(
                [new RankProfile(fullXuid, "Profil local", 2, TimeSpan.FromHours(3), 30)],
                [],
                source,
                source with { SourceLabel = "ranks_v2/maps/*.json" },
                1,
                0,
                1,
                0,
                0),
            EasterEggRecords = new EasterEggRecordsSnapshot(
                [],
                source with { SourceLabel = "easter_eggs_v2/maps/*.json" },
                1,
                0,
                0,
                0,
                false)
        };
        var viewModel = new RecordsViewModel(new MutableSnapshotStore(snapshot));

        await viewModel.InitializeAsync();

        Assert.AreEqual("LECTURE LOCALE READ-ONLY", viewModel.DataBadge);
        Assert.AreEqual(1, viewModel.RankProfileCount);
        Assert.AreEqual("abcd…6789", viewModel.RankProfiles.Single().ShortXuid);
        Assert.IsFalse(viewModel.RankProfiles.Single().ShortXuid.Contains(fullXuid, StringComparison.Ordinal));
        Assert.IsNull(typeof(RankProfileItemViewModel).GetProperty("Xuid"));
        StringAssert.Contains(viewModel.RankSourceSummary, "Réussie");
        StringAssert.Contains(viewModel.EasterEggRecordSourceSummary, "Réussie");
        StringAssert.Contains(viewModel.SectionDescription, "Easter Egg Records officiels v2 locaux");
        Assert.AreEqual("OFFICIELS LOCAUX · TOP 5", viewModel.EasterEggKpiCaption);
    }

    [TestMethod]
    public void UiFacingViewModels_NeverExposeACompleteXuid()
    {
        const string fullXuid = "abcdef0123456789";
        var player = new PlayerItemViewModel(new PlayerState(
            0,
            fullXuid,
            "Profil sûr",
            "user",
            "fr",
            "FR",
            PlayerLifeState.Alive,
            1000,
            TimeSpan.FromMinutes(2),
            false,
            false));
        var rank = new RankProfileItemViewModel(new RankProfile(
            fullXuid,
            "Profil sûr",
            1,
            TimeSpan.FromHours(2),
            10));
        var record = new RecordItemViewModel(new RecordEntry(
            "zm_tomb",
            "Origins",
            1,
            10,
            TimeSpan.FromHours(1),
            "Profil sûr",
            false)
        {
            HolderXuids = [fullXuid]
        });
        var simulation = new SimulationResultItemViewModel(
            new SimulationResult(
                SimulationStatus.Simulated,
                "Simulation.",
                SimulationAction.RevivePlayer,
                fullXuid,
                null,
                false,
                DateTimeOffset.UtcNow),
            "Profil sûr");

        foreach (var viewModel in new object[] { player, rank, record, simulation })
        {
            var publicStrings = viewModel.GetType()
                .GetProperties()
                .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
                .Select(property => (string?)property.GetValue(viewModel))
                .Where(value => value is not null);
            Assert.IsFalse(publicStrings.Any(value => value!.Contains(fullXuid, StringComparison.OrdinalIgnoreCase)));
        }

        Assert.IsNull(typeof(PlayerItemViewModel).GetProperty("Xuid"));
        Assert.IsNull(typeof(PlayerItemViewModel).GetProperty("Model"));
        Assert.IsNull(typeof(SimulationResultItemViewModel).GetProperty("FullXuid"));
        Assert.AreEqual("abcd…6789", player.ShortXuid);
        Assert.AreEqual("abcd…6789", simulation.ShortXuid);
    }

    [TestMethod]
    public async Task DashboardAndServer_ExposeOnlyNeutralizedDisplayState()
    {
        const string fullXuid = "abcdef0123456789";
        const string privateRoot = "C:\\Users\\private\\UnrankedServer";
        const string privateSession = "private-session-identifier";
        var simulated = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var source = new LocalSourceMetadata(
            LocalReadStatus.Success,
            DataFreshness.Fresh,
            TimeSpan.Zero,
            DataProvenance.LocalFile,
            "source locale",
            "Lecture réussie.");
        var hybrid = simulated with
        {
            Server = simulated.Server with
            {
                SessionId = privateSession,
                SessionProvenance = DataProvenance.LocalFile
            },
            DataContext = simulated.DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE HYBRIDE LOCAL",
                ServerRoot = privateRoot,
                SessionSource = source
            },
            LocalObservation = simulated.LocalObservation with
            {
                PlayerMetadata = new LocalReadResult<LocalPlayerMetadataSnapshot>(
                    new LocalPlayerMetadataSnapshot(
                        [new LocalPlayerMetadata(fullXuid, "Profil", "user", "fr", "FR")],
                        1,
                        0),
                    source,
                    DateTimeOffset.UtcNow)
            }
        };
        var store = new MutableSnapshotStore(hybrid);
        var service = new SimulationActionService();
        var dashboard = new DashboardViewModel(store, service, new PlayerSelectionState());
        var server = new ServerViewModel(store, service);
        var logs = new LogsViewModel(store);

        await dashboard.InitializeAsync();
        await server.InitializeAsync();
        await logs.InitializeAsync();

        foreach (var type in new[] { typeof(DashboardViewModel), typeof(ServerViewModel) })
        {
            Assert.IsNull(type.GetProperty("LocalObservation"));
            Assert.IsNull(type.GetProperty("SnapshotContext"));
            Assert.IsNull(type.GetProperty("Server"));
            Assert.IsFalse(type.GetProperties().Any(property =>
                property.PropertyType == typeof(BlockALocalSnapshot) ||
                property.PropertyType == typeof(SnapshotDataContext) ||
                property.PropertyType == typeof(ServerState)));
        }

        var displayedStrings = new object[] { dashboard, server, logs }
            .SelectMany(viewModel => viewModel.GetType().GetProperties()
                .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
                .Select(property => (string?)property.GetValue(viewModel)))
            .Where(value => value is not null)
            .ToArray();
        Assert.IsFalse(displayedStrings.Any(value => value!.Contains(fullXuid, StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(displayedStrings.Any(value => value!.Contains("Users", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(displayedStrings.Any(value => value!.Contains(privateSession, StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual("SESSION LOCALE ACTIVE", dashboard.SessionSourceLabel);
        Assert.AreEqual("logs/sessions/<session-active>", logs.SourceLabel);
    }

    [TestMethod]
    public void Errors_NeverExposeExceptionMessagesOrPaths()
    {
        var composition = CreateComposition(SimulationScenario.Healthy);
        var page = new ErrorPageViewModel();
        var exception = new IOException("Échec dans C:\\Users\\private\\UnrankedServer\\secret.json");

        composition.Shell.ReportError(exception);
        page.Capture(exception);

        Assert.IsFalse(composition.Shell.GlobalErrorMessage!.Contains("Users", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(composition.Shell.GlobalErrorMessage.Contains("secret.json", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(page.ErrorMessage!.Contains("Users", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(page.ErrorMessage.Contains("secret.json", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void AppLifecycle_StoresAndAwaitsMonitorBeforeDisposingReaders()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var appSource = File.ReadAllText(Path.Combine(presentationRoot, "App.xaml.cs"));
        var contextSource = File.ReadAllText(Path.Combine(
            presentationRoot,
            "Composition",
            "ServerRuntimeContext.cs"));

        StringAssert.Contains(contextSource, "_monitorTask = RunMonitorAsync");
        StringAssert.Contains(contextSource, "await _monitorTask;");
        StringAssert.Contains(contextSource, "RconOperations.StopAcceptingNewOperations();");
        StringAssert.Contains(contextSource, "await RconOperations.WaitForIdleAsync();");
        StringAssert.Contains(contextSource, "rconOperationGate");
        Assert.AreEqual(4, CountOccurrences(contextSource, "rconOperationGate);"));
        StringAssert.Contains(contextSource, "new ServerAdministrationCommandService(");
        StringAssert.Contains(contextSource, "new PlayerAdministrationCommandService(");
        StringAssert.Contains(appSource, "StopAllContexts();");
        StringAssert.Contains(appSource, "context.StopAcceptingNewOperations();");
        StringAssert.Contains(appSource, "context.WaitForShutdownAsync()");
        StringAssert.Contains(appSource, "DisposeResources();");
        Assert.IsFalse(contextSource.Contains("_ = RunMonitorAsync", StringComparison.Ordinal));
        var closingHandler = appSource[appSource.IndexOf("private async void OnMainWindowClosing", StringComparison.Ordinal)..];
        Assert.IsTrue(
            closingHandler.IndexOf("context.WaitForShutdownAsync()", StringComparison.Ordinal) <
            closingHandler.IndexOf("DisposeResources();", StringComparison.Ordinal));
    }

    [TestMethod]
    public void XamlTooltips_DoNotBindToCompleteXuids()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = Directory.EnumerateFiles(presentationRoot, "*.xaml", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.IsFalse(xaml.Any(contents => contents.Contains("FullXuid", StringComparison.Ordinal)));
        Assert.IsFalse(xaml.Any(contents => contents.Contains("SelectedPlayer.Xuid", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MainWindow_UsesIsolatedServerTabsAndActiveShellBindings()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(presentationRoot, "MainWindow.xaml"));

        StringAssert.Contains(xaml, "ItemsSource=\"{Binding Servers}\"");
        StringAssert.Contains(xaml, "Command=\"{Binding AddServerCommand}\"");
        StringAssert.Contains(xaml, "Command=\"{Binding RemoveServerCommand}\"");
        StringAssert.Contains(xaml, "ActiveServer.Shell.NavigationItems");
        StringAssert.Contains(xaml, "ActiveServer.Shell.CurrentPage");
        StringAssert.Contains(xaml, "ActiveServer.Shell.RefreshCommand");
        StringAssert.Contains(xaml, "Title=\"{Binding WindowTitle}\"");
        Assert.IsFalse(xaml.Contains("WindowChrome", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("WindowStyle=\"None\"", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "SelectedValue=\"{Binding SelectedUiLanguage, Mode=OneWay}\"");
        StringAssert.Contains(xaml, "<Border Grid.Row=\"1\" Background=\"{StaticResource SidebarBrush}\"");
        StringAssert.Contains(xaml, "Fill=\"{Binding AccentPreviewBrush}\"");
    }

    [TestMethod]
    public void ServerView_UsesUnboundPasswordBoxForLoopbackOnlyPasswordAction()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(presentationRoot, "Views", "ServerView.xaml"));

        StringAssert.Contains(xaml, "x:Name=\"JoinPasswordBox\"");
        StringAssert.Contains(xaml, "Click=\"SetJoinPassword_Click\"");
        StringAssert.Contains(xaml, "IsEnabled=\"{Binding CanSetJoinPassword}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding ServerActionModeTitle}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding ServerActionModeDescription}\"");
        StringAssert.Contains(xaml, "MOT DE PASSE RÉSEAU BOIII");
        StringAssert.Contains(xaml, "<controls:BoiiiHostnameEditor");
        StringAssert.Contains(xaml, "EncodedText=\"{Binding RequestedHostname, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"");
        StringAssert.Contains(xaml, "Command=\"{Binding RestoreObservedHostnameCommand}\"");
        Assert.IsFalse(xaml.Contains("Password=\"{Binding", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AccentResourcesAreDynamicAndSettingsExposePerServerPalette()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xamlFiles = Directory.EnumerateFiles(presentationRoot, "*.xaml", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var settings = File.ReadAllText(Path.Combine(presentationRoot, "Views", "SettingsView.xaml"));

        Assert.IsFalse(xamlFiles.Any(source =>
            source.Contains("{StaticResource AccentBrush}", StringComparison.Ordinal) ||
            source.Contains("{StaticResource AccentBrightBrush}", StringComparison.Ordinal) ||
            source.Contains("{StaticResource AccentSoftBrush}", StringComparison.Ordinal)));
        Assert.IsTrue(xamlFiles.Any(source => source.Contains("{DynamicResource AccentBrush}", StringComparison.Ordinal)));
        StringAssert.Contains(settings, "ItemsSource=\"{Binding AccentColorOptions}\"");
        StringAssert.Contains(settings, "SelectedItem=\"{Binding SelectedAccentTheme, Mode=TwoWay}\"");
        StringAssert.Contains(settings, "Command=\"{Binding SaveAppearanceCommand}\"");
        StringAssert.Contains(settings, "Les couleurs d’état restent inchangées.");
    }

    [TestMethod]
    public void Dashboard_ContainsReadOnlyRecentPlayerChatPanel()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(presentationRoot, "Views", "DashboardView.xaml"));

        StringAssert.Contains(xaml, "DERNIERS MESSAGES JOUEURS");
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding PlayerChat.RecentMessages}\"");
        StringAssert.Contains(xaml, "CHAT LOCAL · READ-ONLY");
        Assert.IsFalse(xaml.Contains("PlayerChat.Xuid", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DashboardServiceDiagnostics_AreCollapsedByDefaultAndRemainAvailable()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(presentationRoot, "Views", "DashboardView.xaml"));

        StringAssert.Contains(xaml, "Header=\"DÉTAILS\" IsExpanded=\"False\"");
        StringAssert.Contains(xaml, "Style=\"{StaticResource CompactDetailsExpanderStyle}\"");
        StringAssert.Contains(xaml, "{Binding DeclaredStateText, Mode=OneWay}");
        StringAssert.Contains(xaml, "{Binding ReadStatusText, Mode=OneWay}");
        StringAssert.Contains(xaml, "{Binding FreshnessText, Mode=OneWay}");
        StringAssert.Contains(xaml, "{Binding ProvenanceText, Mode=OneWay}");
    }

    [TestMethod]
    public void RecordsView_UsesStableFilterKeysAndPlacesRankProfilesAfterRecords()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(presentationRoot, "Views", "RecordsView.xaml"));

        StringAssert.Contains(xaml, "SelectedValuePath=\"Key\"");
        StringAssert.Contains(xaml, "SelectedRecordMapKey");
        StringAssert.Contains(xaml, "SelectedRecordHolderKey");
        StringAssert.Contains(xaml, "<StackPanel Grid.Row=\"2\">");
        StringAssert.Contains(xaml, "<Border Grid.Row=\"3\" Style=\"{StaticResource CardStyle}\"");
    }

    [TestMethod]
    public void Dashboard_ModeDetails_AreCompactAndCollapsedByDefault()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(presentationRoot, "Views", "DashboardView.xaml"));

        StringAssert.Contains(xaml, "Header=\"DÉTAILS SOURCE\" IsExpanded=\"False\"");
        StringAssert.Contains(xaml, "Text=\"{Binding ModeLabel}\" Style=\"{StaticResource EyebrowTextStyle}\"");
    }

    [TestMethod]
    public void ServerView_IdentityAndPassword_AreProminentFullWidth()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(presentationRoot, "Views", "ServerView.xaml"));

        StringAssert.Contains(xaml, "Grid.Row=\"2\" Style=\"{StaticResource CardStyle}\"");
        StringAssert.Contains(xaml, "IDENTITÉ PUBLIQUE &amp; MOT DE PASSE");
        StringAssert.Contains(xaml, "Autoriser l’envoi du mot de passe via RCON LAN pour cette session");
        StringAssert.Contains(xaml, "x:Name=\"JoinPasswordBox\"");
    }

    [TestMethod]
    public void RankProfileIdentifier_IsCollapsedAndBestRoundHasNoPrefix()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(presentationRoot, "Views", "RecordsView.xaml"));
        var theme = File.ReadAllText(Path.Combine(presentationRoot, "Themes", "PinteModTheme.xaml"));
        var expanderStart = theme.IndexOf("x:Key=\"CompactDetailsExpanderStyle\"", StringComparison.Ordinal);
        var expanderEnd = theme.IndexOf("<Style TargetType=\"ComboBox\">", expanderStart, StringComparison.Ordinal);
        var expanderStyle = theme[expanderStart..expanderEnd];

        StringAssert.Contains(xaml, "Header=\"IDENTIFIANT\" IsExpanded=\"False\"");
        StringAssert.Contains(xaml, "Text=\"{Binding ShortXuid}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding BestOverallRound}\"");
        Assert.IsFalse(xaml.Contains("StringFormat=M{0}", StringComparison.Ordinal));
        StringAssert.Contains(expanderStyle, "<Setter Property=\"FontSize\" Value=\"8\" />");
        StringAssert.Contains(expanderStyle, "Opacity=\"0.72\"");
    }

    [TestMethod]
    public void PlayerWeaponPerkAndPowerUpActions_UseIndependentResponsiveGrids()
    {
        var presentationRoot = FindPresentationSourceRoot();
        var source = File.ReadAllText(Path.Combine(presentationRoot, "Controls", "PlayerDetailsControl.xaml"));
        var sectionStart = source.IndexOf("Text=\"ARMES &amp; ATOUTS\"", StringComparison.Ordinal);
        var sectionEnd = source.IndexOf("Text=\"MODÉRATION &amp; IDENTITÉ\"", sectionStart, StringComparison.Ordinal);
        var section = source[sectionStart..sectionEnd];

        StringAssert.Contains(section, "x:Name=\"WeaponActionGrid\"");
        StringAssert.Contains(section, "x:Name=\"PerkActionGrid\"");
        StringAssert.Contains(section, "x:Name=\"PowerUpActionGrid\"");
        Assert.AreEqual(3, CountOccurrences(section, "<controls:ResponsiveUniformGrid"));
        Assert.IsFalse(section.Contains("<WrapPanel", StringComparison.Ordinal));
        Assert.IsFalse(section.Contains("SelectedWeapon}\" Width=", StringComparison.Ordinal));
        Assert.IsFalse(section.Contains("SelectedPerk}\" Width=", StringComparison.Ordinal));
        Assert.IsFalse(section.Contains("SelectedPowerUp}\" Width=", StringComparison.Ordinal));
        Assert.AreEqual(2, CountOccurrences(section, "TextWrapping=\"Wrap\" TextAlignment=\"Center\""));
    }

    [TestMethod]
    public void DurationsOverTwentyFourHours_DisplayTotalHoursWithoutWrapping()
    {
        var converter = new DurationConverter();
        var converted = converter.Convert(
            new TimeSpan(1, 3, 5, 6),
            typeof(string),
            null,
            System.Globalization.CultureInfo.InvariantCulture);
        var record = new RecordItemViewModel(new RecordEntry(
            "zm_tomb",
            "Origins",
            1,
            10,
            new TimeSpan(2, 1, 2, 3),
            "Équipe",
            false));
        var rank = new RankProfileItemViewModel(new RankProfile(
            "abcdef0123456789",
            "Profil",
            1,
            new TimeSpan(4, 4, 5, 6),
            10));

        Assert.AreEqual("27:05:06", converted);
        Assert.AreEqual("49:02:03", record.Duration);
        Assert.AreEqual("100:05:06", rank.TotalPlayTime);
    }

    [TestMethod]
    public async Task ServerStatus_ComesFromServerRunning()
    {
        var running = new ServerViewModel(new MutableSnapshotStore(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy)), new SimulationActionService());
        var stopped = new ServerViewModel(new MutableSnapshotStore(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.ServerStopped)), new SimulationActionService());

        await running.InitializeAsync();
        await stopped.InitializeAsync();

        Assert.AreEqual("EN LIGNE", running.ServerStatusText);
        Assert.AreEqual(ServiceHealth.Healthy, running.ServerStatusHealth);
        Assert.AreEqual("ARRÊTÉ", stopped.ServerStatusText);
        Assert.AreEqual(ServiceHealth.Offline, stopped.ServerStatusHealth);
    }

    [TestMethod]
    public void ServerMapOptions_ContainTheFourteenMapsDeclaredByServerCatalog()
    {
        var viewModel = new ServerViewModel(
            new MutableSnapshotStore(SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy)),
            new SimulationActionService());
        var expectedCodes = new[]
        {
            "zm_zod", "zm_castle", "zm_island", "zm_stalingrad", "zm_genesis",
            "zm_cosmodrome", "zm_theater", "zm_moon", "zm_prototype", "zm_tomb",
            "zm_temple", "zm_sumpf", "zm_factory", "zm_asylum"
        };

        CollectionAssert.AreEqual(expectedCodes, viewModel.MapOptions.Select(option => option.Key).ToArray());
        Assert.AreEqual(expectedCodes.Length, viewModel.MapOptions.Select(option => option.Key).Distinct().Count());
        Assert.IsTrue(viewModel.MapOptions.All(option => !string.IsNullOrWhiteSpace(option.Label)));
    }

    [TestMethod]
    public void RealAdaptiveServer_DisablesSimulationOnlyServerActions()
    {
        var viewModel = new ServerViewModel(
            new MutableSnapshotStore(SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy)),
            new SimulationActionService(),
            integrationProfile: ServerIntegrationProfile.Unknown,
            allowSimulationActions: false);

        Assert.IsFalse(viewModel.SimulateServerActionCommand.CanExecute(SimulationAction.ChangeMap));
        StringAssert.Contains(viewModel.ServerActionModeTitle, "FAIL-CLOSED");
        StringAssert.Contains(viewModel.ServerActionModeDescription, "aucune simulation");
    }

    [TestMethod]
    public async Task ServerPauseStatus_UsesOnlyFreshSuccessfulObservation()
    {
        var pause = new CommunityPauseStatusSnapshot(
            "0.3", 1000, true, 120, 1, 2, 1, "Aucun", null, null, null, true, true, true);
        var currentSource = new LocalSourceMetadata(
            LocalReadStatus.Success, DataFreshness.Fresh, TimeSpan.FromSeconds(2),
            DataProvenance.LocalFile, "feedback.latest.txt", "OK");
        var snapshot = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy) with
        {
            LocalObservation = BlockALocalSnapshot.Simulation with
            {
                CommunityPause = new(pause, currentSource, DateTimeOffset.UtcNow)
            }
        };
        var store = new MutableSnapshotStore(snapshot);
        var viewModel = new ServerViewModel(store, new SimulationActionService());

        await viewModel.InitializeAsync();

        Assert.AreEqual("EN PAUSE", viewModel.PauseStatusText);
        Assert.AreEqual(ServiceHealth.Warning, viewModel.PauseStatusHealth);
        Assert.IsTrue(viewModel.PauseDetails.Contains("120 s", StringComparison.Ordinal));

        store.SetSnapshot(snapshot with
        {
            LocalObservation = snapshot.LocalObservation with
            {
                CommunityPause = new(pause, currentSource with
                {
                    ReadStatus = LocalReadStatus.Invalid,
                    Freshness = DataFreshness.Stale,
                    Provenance = DataProvenance.MemoryCache
                }, DateTimeOffset.UtcNow)
            }
        });
        await viewModel.InitializeAsync();

        Assert.AreEqual("INCONNU", viewModel.PauseStatusText);
        Assert.AreEqual(ServiceHealth.Unknown, viewModel.PauseStatusHealth);
        Assert.AreEqual("Dernière donnée valide — périmée", viewModel.PauseDetails);
        Assert.IsFalse(viewModel.RealPauseControlsAvailable);
        Assert.IsTrue(viewModel.RealPauseControlsNotice.Contains("Configuration RCON", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Settings_UnimplementedOptions_AreFalseAndUnavailable()
    {
        var settings = new SettingsViewModel();

        Assert.IsFalse(settings.AutomaticRefresh);
        Assert.IsFalse(settings.SoundAlerts);
        Assert.IsFalse(settings.CompactMode);
        Assert.IsFalse(settings.AutomaticRefreshAvailable);
        Assert.IsFalse(settings.SoundAlertsAvailable);
        Assert.IsFalse(settings.CompactModeAvailable);
    }

    [TestMethod]
    public void HybridPlayerUnavailableFields_AreNeverDisplayedAsRealValues()
    {
        var player = new PlayerItemViewModel(new PlayerState(
            0,
            "abcdef0123456789",
            "Joueur local",
            "unknown",
            "unknown",
            "--",
            PlayerLifeState.Unknown,
            0,
            TimeSpan.Zero,
            false,
            false)
        {
            LifeStateAvailable = false,
            PointsAvailable = false,
            PresenceAvailable = false,
            Provenance = DataProvenance.LocalFile
        });

        Assert.AreEqual("NON DISPONIBLE", player.LifeStateText);
        Assert.AreEqual("NON DISPONIBLE", player.Points);
        Assert.AreEqual("NON DISPONIBLE", player.PresenceText);
        Assert.AreEqual("INCONNUE", player.Language);
        Assert.AreEqual("INCONNU", player.CountryCode);
        Assert.AreEqual("Inconnu", player.Role);
    }

    [TestMethod]
    public void HybridSettings_EnableMonitorWithoutExposingCompleteRoot()
    {
        var settings = new SettingsViewModel(
            ControlCenterDataMode.HybridLocal,
            "BlockAControlCenterDataProvider",
            "C:\\Users\\private\\UnrankedServer",
            TimeSpan.FromSeconds(2));

        Assert.IsTrue(settings.AutomaticRefresh);
        Assert.AreEqual("ACTIF · 2 S", settings.AutomaticRefreshStatus);
        Assert.IsFalse(settings.ServerRootDisplay.Contains("Users", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(settings.ServerRootDisplay, "UnrankedServer");
    }

    [TestMethod]
    public void LocalSessionEvent_UsesRelativeTimeAndNeutralUnknownStatuses()
    {
        var item = new EventItemViewModel(new LiveEvent(
            DateTimeOffset.UnixEpoch,
            "SYSTÈME",
            "Événement",
            "Détail",
            EventSeverity.Information)
        {
            SessionElapsed = TimeSpan.FromHours(27)
        });

        Assert.AreEqual("T+27:00:00", item.Time);
        Assert.AreEqual("TextSecondaryBrush", StatusBrushConverter.GetResourceKey(RankedStatus.Unknown));
        Assert.AreEqual("TextSecondaryBrush", StatusBrushConverter.GetResourceKey(PlayerLifeState.Unknown));
    }

    [TestMethod]
    public async Task HybridShellAndLogs_DoNotClaimThatLocalDataAreSimulated()
    {
        var simulated = SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy);
        var hybrid = simulated with
        {
            DataContext = simulated.DataContext with
            {
                Mode = ControlCenterDataMode.HybridLocal,
                ModeLabel = "MODE HYBRIDE LOCAL"
            },
            LocalObservation = simulated.LocalObservation with
            {
                Logs = StructuredLogSnapshot.Empty("session", new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    TimeSpan.Zero,
                    DataProvenance.LocalFile,
                    "logs/sessions/session",
                    "OK"))
            }
        };
        var store = new MutableSnapshotStore(hybrid);
        var logs = new LogsViewModel(store);
        var settings = new SettingsViewModel(ControlCenterDataMode.HybridLocal);
        var service = new SimulationActionService();
        var selection = new PlayerSelectionState();
        var shell = new ShellViewModel(
            store,
            new DashboardViewModel(store, service, selection),
            new PlayersViewModel(store, service, selection),
            new ServerViewModel(store, service),
            new RecordsViewModel(store),
            logs,
            settings,
            startClock: false);

        await shell.InitializeAsync();

        StringAssert.Contains(logs.Description, "locaux neutralisés");
        StringAssert.Contains(shell.ReadOnlyFooterLabel, "DONNÉES LOCALES");
        Assert.IsFalse(logs.Description.Contains("simulé", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task AsyncRelayCommand_CatchesExceptionAndIsReenabled()
    {
        var observed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new AsyncRelayCommand(
            () => Task.FromException(new InvalidOperationException("échec simulé")),
            onException: exception => observed.TrySetResult(exception));

        command.Execute(null);
        var exception = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsInstanceOfType<InvalidOperationException>(exception);
        Assert.IsNotNull(command.LastException);
        Assert.IsFalse(command.IsExecuting);
        Assert.IsTrue(command.CanExecute(null));
    }

    [TestMethod]
    public async Task CachedStore_ReadsProviderOnlyOnceUntilRefresh()
    {
        var provider = new CountingProvider(
            SimulatedControlCenterDataProvider.CreateSnapshot(SimulationScenario.Healthy));
        var store = new CachedControlCenterSnapshotStore(provider);

        await store.GetSnapshotAsync();
        await store.GetSnapshotAsync();
        Assert.AreEqual(1, provider.ReadCount);

        await store.RefreshAsync();
        Assert.AreEqual(2, provider.ReadCount);
    }

    private static string FindPresentationSourceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "PinteMod.ControlCenter");
            if (File.Exists(Path.Combine(candidate, "App.xaml")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Le dossier source WPF est introuvable.");
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static TestComposition CreateComposition(SimulationScenario scenario)
    {
        var store = new CachedControlCenterSnapshotStore(new SimulatedControlCenterDataProvider(scenario));
        var service = new SimulationActionService();
        var selection = new PlayerSelectionState();
        var dashboard = new DashboardViewModel(store, service, selection);
        var players = new PlayersViewModel(store, service, selection);
        var server = new ServerViewModel(store, service);
        var records = new RecordsViewModel(store);
        var logs = new LogsViewModel(store);
        var settings = new SettingsViewModel();
        var shell = new ShellViewModel(store, dashboard, players, server, records, logs, settings, startClock: false);
        return new(shell, dashboard, players);
    }

    private sealed record TestComposition(
        ShellViewModel Shell,
        DashboardViewModel Dashboard,
        PlayersViewModel Players);

    private sealed class MutableSnapshotStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current { get; private set; } = snapshot;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public void SetSnapshot(DashboardSnapshot value) => Current = value;
    }

    private sealed class AlwaysConfirmService : IOperatorConfirmationService
    {
        public Task<bool> ConfirmAsync(OperatorConfirmationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class CapturingSimulationService : ISimulationActionService
    {
        public SimulationRequest? LastRequest { get; private set; }

        public Task<SimulationResult> SimulateAsync(
            SimulationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new SimulationResult(
                SimulationStatus.Simulated,
                "Simulation capturée.",
                request.Action,
                request.TargetXuid,
                request.OptionKey,
                false,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class CountingProvider(DashboardSnapshot snapshot) : IControlCenterDataProvider
    {
        public int ReadCount { get; private set; }

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(snapshot);
        }
    }

    private sealed class ErrorPageViewModel : PageViewModel
    {
        public ErrorPageViewModel() : base("Erreur", "Test")
        {
        }

        public void Capture(Exception exception) => ReportError(exception);

        public override Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
