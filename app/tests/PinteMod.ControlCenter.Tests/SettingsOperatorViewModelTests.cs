using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Services;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class SettingsOperatorViewModelTests
{
    [TestMethod]
    public void AccentPaletteMatchesClosedCoreKeysAndLoadsSavedProfileChoice()
    {
        var configuration = OperatorConfiguration.Default with { AccentColorKey = "pink" };
        var viewModel = new SettingsViewModel(initialConfiguration: configuration);

        CollectionAssert.AreEqual(
            OperatorAccentTheme.AllowedKeys.ToArray(),
            AccentThemeService.Options.Select(option => option.Key).ToArray());
        Assert.AreEqual("pink", viewModel.SelectedAccentTheme.Key);
        Assert.AreEqual(AccentThemeService.Resolve("pink"), viewModel.SelectedAccentTheme);
    }

    [TestMethod]
    public async Task TestCommand_ReportsReadyForFiveReadableSources()
    {
        var probe = new StubProbe(new LocalDataSourceProbeResult(
            true,
            Enumerable.Range(1, 5)
                .Select(index => new LocalDataSourceProbeItem($"Source {index}", LocalReadStatus.Success, DataFreshness.Fresh))
                .ToArray(),
            "Source valide."));
        var viewModel = new SettingsViewModel(localDataSourceProbe: probe)
        {
            OperatorServerRoot = "C:\\Server\\UnrankedServer"
        };

        viewModel.TestDataSourceCommand.Execute(null);
        await probe.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.TestDataSourceCommand);

        Assert.AreEqual("PRÊT", viewModel.DataSourceTestStatus);
        Assert.AreEqual(ServiceHealth.Healthy, viewModel.DataSourceTestHealth);
        Assert.AreEqual(OperatorDataLocation.Local, probe.Request!.Location);
    }

    [TestMethod]
    public async Task LanSelection_ForwardsExplicitLanModeAndShowsSafeFailure()
    {
        var probe = new StubProbe(new LocalDataSourceProbeResult(false, [], "Le dossier indiqué est introuvable ou inaccessible."));
        var viewModel = new SettingsViewModel(localDataSourceProbe: probe)
        {
            SelectedOperatorMode = "LAN",
            OperatorServerRoot = "\\\\serveur\\partage\\UnrankedServer"
        };

        viewModel.TestDataSourceCommand.Execute(null);
        await probe.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.TestDataSourceCommand);

        Assert.AreEqual("REFUSÉ", viewModel.DataSourceTestStatus);
        Assert.AreEqual(ServiceHealth.Error, viewModel.DataSourceTestHealth);
        Assert.AreEqual(OperatorDataLocation.Lan, probe.Request!.Location);
        Assert.IsFalse(viewModel.DataSourceTestMessage.Contains("serveur", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task AcceptedSource_CanBePersistedForNextStartup()
    {
        var probe = new StubProbe(new LocalDataSourceProbeResult(
            true,
            [new LocalDataSourceProbeItem("Session", LocalReadStatus.Success, DataFreshness.Fresh)],
            "Source valide."));
        var store = new CapturingConfigurationStore();
        var viewModel = new SettingsViewModel(localDataSourceProbe: probe, configurationStore: store)
        {
            ProfileDisplayName = "Serveur salon",
            OperatorServerRoot = "C:\\Server\\UnrankedServer"
        };
        var savedDisplayName = string.Empty;
        var previewedAccent = string.Empty;
        viewModel.ProfileDisplayNameSaved += value => savedDisplayName = value;
        viewModel.AccentThemeChanged += value => previewedAccent = value;
        viewModel.SelectedAccentTheme = viewModel.AccentColorOptions.Single(option => option.Key == "violet");

        viewModel.TestDataSourceCommand.Execute(null);
        await probe.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.TestDataSourceCommand);
        viewModel.ActivateDataSourceOnStartup = true;
        viewModel.SaveConfigurationCommand.Execute(null);
        await store.Saved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.SaveConfigurationCommand);

        Assert.IsTrue(store.Configuration!.ActivateDataSourceOnStartup);
        Assert.AreEqual("C:\\Server\\UnrankedServer", store.Configuration.ServerRoot);
        Assert.AreEqual("Serveur salon", store.Configuration.ProfileDisplayName);
        Assert.AreEqual("violet", store.Configuration.AccentColorKey);
        Assert.AreEqual("violet", previewedAccent);
        Assert.AreEqual("Serveur salon", savedDisplayName);
        Assert.AreEqual("ENREGISTRÉ", viewModel.ConfigurationSaveStatus);
    }

    [TestMethod]
    public async Task AppearanceSaveDoesNotPersistAnUnverifiedSourceOrRconChange()
    {
        var store = new CapturingConfigurationStore();
        var viewModel = new SettingsViewModel(configurationStore: store)
        {
            ProfileDisplayName = "Serveur rose",
            OperatorServerRoot = "C:\\Unverified\\Server",
            ActivateDataSourceOnStartup = true,
            RconAddress = "198.51.100.42" // Adresse TEST-NET-2 réservée aux exemples.
        };
        viewModel.SelectedAccentTheme = viewModel.AccentColorOptions.Single(option => option.Key == "pink");

        Assert.IsFalse(viewModel.CanSaveConfiguration);
        Assert.IsTrue(viewModel.CanSaveAppearance);
        viewModel.SaveAppearanceCommand.Execute(null);
        await store.Saved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.SaveAppearanceCommand);

        Assert.AreEqual("Serveur rose", store.Configuration!.ProfileDisplayName);
        Assert.AreEqual("pink", store.Configuration.AccentColorKey);
        Assert.AreEqual(string.Empty, store.Configuration.ServerRoot);
        Assert.AreEqual("127.0.0.1", store.Configuration.RconAddress);
        Assert.IsFalse(store.Configuration.ActivateDataSourceOnStartup);
        Assert.AreEqual("APPARENCE ENREGISTRÉE", viewModel.ConfigurationSaveStatus);
    }

    [TestMethod]
    public async Task RconDiagnostic_ReportsRealCommandSentStateWithoutExposingSecret()
    {
        var secretStore = new MemorySecretStore();
        var service = new StubRconDiagnosticService(new RconExecutionResult(
            RconDiagnosticCommand.HealthFull,
            RconExecutionStatus.Success,
            "PASS=51 | WARNING=0 | ERROR=0",
            true,
            DateTimeOffset.UtcNow));
        var viewModel = new SettingsViewModel(rconDiagnosticService: service, rconSecretStore: secretStore);
        await viewModel.SaveRconSecretAsync("safe-secret");

        viewModel.TestRconHealthCommand.Execute(null);
        await service.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.TestRconHealthCommand);

        Assert.AreEqual(RconDiagnosticCommand.HealthFull, service.Command);
        Assert.AreEqual("RÉUSSI", viewModel.RconTestStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.RconCommandSent);
        Assert.IsFalse(viewModel.RconResponse.Contains("safe-secret", StringComparison.Ordinal));
        Assert.IsFalse(viewModel.GetType().GetProperties().Any(property =>
            property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Equals("Secret", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task FirstPinteModRcon_StoresTheSameSecretForTheControlCenter()
    {
        var secretStore = new MemorySecretStore();
        var bootstrap = new StubRconBootstrapService(new BoiiiRconBootstrapResult(true, "RCON PinteMod prêt."));
        var viewModel = new SettingsViewModel(
            serverRoot: "C:\\Server\\BOIII",
            rconSecretStore: secretStore,
            rconBootstrapService: bootstrap);

        await viewModel.InitializeFirstRconSecretAsync("SafeRcon-2026");

        Assert.AreEqual("C:\\Server\\BOIII", bootstrap.ServerRoot);
        Assert.AreEqual("RCON INITIALISÉ · SECRET DPAPI ENREGISTRÉ", viewModel.RconSecretStatus);
        Assert.AreEqual("SafeRcon-2026", await secretStore.ReadAsync());
        Assert.IsFalse(viewModel.RconResponse.Contains("SafeRcon-2026", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FirstPinteModRcon_WhenControlCenterSecretSaveFails_GuidesUserWithoutExposingSecret()
    {
        var bootstrap = new StubRconBootstrapService(new BoiiiRconBootstrapResult(true, "RCON PinteMod prêt."));
        var viewModel = new SettingsViewModel(
            serverRoot: "C:\\Server\\BOIII",
            rconSecretStore: new FailingSecretStore(),
            rconBootstrapService: bootstrap);

        await viewModel.InitializeFirstRconSecretAsync("SafeRcon-2026");

        Assert.AreEqual("RCON INITIALISÉ · SECRET LOCAL À ENREGISTRER", viewModel.RconSecretStatus);
        StringAssert.Contains(viewModel.RconResponse, "Enregistrer RCON");
        Assert.IsFalse(viewModel.RconResponse.Contains("SafeRcon-2026", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task UnexpectedRconResponse_IsShownAsWarningAndNotAsSuccess()
    {
        var service = new StubRconDiagnosticService(new RconExecutionResult(
            RconDiagnosticCommand.PauseStatus,
            RconExecutionStatus.UnexpectedResponse,
            "Réponse reçue mais non reconnue pour ce diagnostic.",
            true,
            DateTimeOffset.UtcNow));
        var viewModel = new SettingsViewModel(
            rconDiagnosticService: service,
            rconSecretStore: new MemorySecretStore());

        viewModel.TestRconPauseCommand.Execute(null);
        await service.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.TestRconPauseCommand);

        Assert.AreEqual("RÉPONSE NON RECONNUE", viewModel.RconTestStatus);
        Assert.AreEqual(ServiceHealth.Warning, viewModel.RconTestHealth);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.RconCommandSent);
    }

    [TestMethod]
    public async Task HeaderOnlyBoiiiReply_GuidesOperatorToServerConsole()
    {
        var service = new StubRconDiagnosticService(new RconExecutionResult(
            RconDiagnosticCommand.HealthFull,
            RconExecutionStatus.EmptyResponse,
            "BOIII a répondu sans texte · vérifiez le résultat dans la console du serveur.",
            true,
            DateTimeOffset.UtcNow));
        var viewModel = new SettingsViewModel(
            rconDiagnosticService: service,
            rconSecretStore: new MemorySecretStore());

        viewModel.TestRconHealthCommand.Execute(null);
        await service.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.TestRconHealthCommand);

        Assert.AreEqual("ENVOYÉ · SANS TEXTE", viewModel.RconTestStatus);
        Assert.AreEqual(ServiceHealth.Warning, viewModel.RconTestHealth);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.RconCommandSent);
        StringAssert.Contains(viewModel.RconResponse, "console du serveur");
    }

    [TestMethod]
    public async Task MapDiagnostic_UsesTypedReadOnlyCommandAndDisplaysResult()
    {
        var service = new StubRconDiagnosticService(new RconExecutionResult(
            RconDiagnosticCommand.MapInfo,
            RconExecutionStatus.Success,
            "Map: zm_tomb · Global power: OFF",
            true,
            DateTimeOffset.UtcNow));
        var viewModel = new SettingsViewModel(
            rconDiagnosticService: service,
            rconSecretStore: new MemorySecretStore());

        viewModel.TestRconMapCommand.Execute(null);
        await service.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.TestRconMapCommand);

        Assert.AreEqual(RconDiagnosticCommand.MapInfo, service.Command);
        Assert.AreEqual("RÉUSSI", viewModel.RconTestStatus);
        Assert.AreEqual("Commande envoyée : Oui", viewModel.RconCommandSent);
        StringAssert.Contains(viewModel.RconResponse, "zm_tomb");
    }

    [TestMethod]
    public async Task SelfTest_ProducesAndCopiesAnAnonymizedReportWithoutRcon()
    {
        var report = new ControlCenterSelfTestReport(
            "2.4.0-preview-integration.4b1.fix16",
            DateTimeOffset.Parse("2026-08-28T16:00:00Z"),
            [new ControlCenterSelfTestCheck("Interface WPF", true, "Six pages chargées.")]);
        var selfTest = new StubSelfTestService(report);
        var clipboard = new CapturingClipboard();
        var viewModel = new SettingsViewModel(
            clipboardService: clipboard,
            selfTestService: selfTest);

        viewModel.RunSelfTestCommand.Execute(null);
        await selfTest.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForCommandAsync(viewModel.RunSelfTestCommand);

        Assert.AreEqual("RÉUSSI", viewModel.SelfTestStatus);
        Assert.AreEqual(ServiceHealth.Healthy, viewModel.SelfTestHealth);
        StringAssert.Contains(viewModel.SelfTestReport, "RESULTAT=PASS");
        Assert.IsTrue(viewModel.CanCopySelfTestReport);

        viewModel.CopySelfTestReportCommand.Execute(null);
        await WaitForCommandAsync(viewModel.CopySelfTestReportCommand);
        Assert.AreEqual(viewModel.SelfTestReport, clipboard.Text);
        Assert.AreEqual("Rapport anonymisé copié.", viewModel.SelfTestCopyStatus);
    }

    [TestMethod]
    public void AdaptiveNativeProfile_DisablesPinteModRconDiagnostics()
    {
        var service = new StubRconDiagnosticService(new RconExecutionResult(
            RconDiagnosticCommand.HealthFull,
            RconExecutionStatus.Success,
            "OK",
            true,
            DateTimeOffset.UtcNow));
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.ControlCenter.SettingsAdaptiveTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "boiii"));
        try
        {
            var viewModel = new SettingsViewModel(
                rconDiagnosticService: service,
                rconSecretStore: new MemorySecretStore(),
                integrationProfile: new ServerInstallationAnalyzer().Analyze(root).IntegrationProfile,
                allowPinteModDiagnostics: false);

            Assert.IsFalse(viewModel.CanRunRconDiagnostic);
            Assert.IsFalse(viewModel.TestRconHealthCommand.CanExecute(null));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow("8.8.8.8")]
    [DataRow("portable")]
    [DataRow("server.example.test")]
    public void PublicAddressOrHostname_DisablesRconAndConfigurationActions(string address)
    {
        var viewModel = new SettingsViewModel(
            configurationStore: new CapturingConfigurationStore(),
            rconDiagnosticService: new StubRconDiagnosticService(new RconExecutionResult(
                RconDiagnosticCommand.HealthFull,
                RconExecutionStatus.Success,
                "OK",
                true,
                DateTimeOffset.UtcNow)))
        {
            RconAddress = address,
            RconPortText = "27018"
        };

        Assert.IsNull(viewModel.CreateRconEndpoint());
        Assert.IsFalse(viewModel.CanRunRconDiagnostic);
        Assert.IsFalse(viewModel.CanSaveConfiguration);
    }

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (command.IsExecuting && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(command.IsExecuting);
    }

    private sealed class StubProbe(LocalDataSourceProbeResult result) : ILocalDataSourceProbe
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LocalDataSourceProbeRequest? Request { get; private set; }

        public Task<LocalDataSourceProbeResult> ProbeAsync(
            LocalDataSourceProbeRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            Called.TrySetResult();
            return Task.FromResult(result);
        }
    }

    private sealed class CapturingConfigurationStore : IOperatorConfigurationStore
    {
        public TaskCompletionSource Saved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public OperatorConfiguration? Configuration { get; private set; }

        public Task<OperatorConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OperatorConfiguration.Default);

        public Task SaveAsync(OperatorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            Configuration = configuration;
            Saved.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class StubRconDiagnosticService(RconExecutionResult result) : IRconDiagnosticService
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RconDiagnosticCommand? Command { get; private set; }

        public Task<RconExecutionResult> ExecuteAsync(
            RconDiagnosticCommand command,
            RconEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            Called.TrySetResult();
            return Task.FromResult(result);
        }
    }

    private sealed class MemorySecretStore : IRconSecretStore
    {
        private string? _secret;

        public Task<bool> HasSecretAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_secret is not null);

        public Task SaveAsync(string secret, CancellationToken cancellationToken = default)
        {
            _secret = secret;
            return Task.CompletedTask;
        }

        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_secret);
    }

    private sealed class FailingSecretStore : IRconSecretStore
    {
        public Task<bool> HasSecretAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task SaveAsync(string secret, CancellationToken cancellationToken = default) =>
            Task.FromException(new System.Security.Cryptography.CryptographicException());

        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class StubRconBootstrapService(BoiiiRconBootstrapResult result) : IBoiiiRconBootstrapService
    {
        public string? ServerRoot { get; private set; }

        public Task<bool> HasConfiguredRconAsync(string serverRoot, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<BoiiiRconBootstrapResult> InitializeAsync(
            string serverRoot,
            string secret,
            CancellationToken cancellationToken = default)
        {
            ServerRoot = serverRoot;
            return Task.FromResult(result);
        }

        public Task<BoiiiRconBootstrapResult> ReplaceAsync(
            string serverRoot,
            string secret,
            CancellationToken cancellationToken = default)
        {
            ServerRoot = serverRoot;
            return Task.FromResult(result);
        }
    }

    private sealed class StubSelfTestService(ControlCenterSelfTestReport report) : IControlCenterSelfTestService
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ControlCenterSelfTestReport> RunAsync(CancellationToken cancellationToken = default)
        {
            Called.TrySetResult();
            return Task.FromResult(report);
        }
    }

    private sealed class CapturingClipboard : ITextClipboardService
    {
        public string? Text { get; private set; }

        public bool TrySetText(string text)
        {
            Text = text;
            return true;
        }
    }
}
