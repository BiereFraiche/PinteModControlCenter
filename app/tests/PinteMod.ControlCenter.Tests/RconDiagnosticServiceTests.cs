using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Rcon;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RconDiagnosticServiceTests
{
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27017, TimeSpan.FromSeconds(3));

    [TestMethod]
    public async Task HealthAndPause_UseOnlyWhitelistedCommandTexts()
    {
        var client = new CommandAwareRconClient();
        var service = new RconDiagnosticService(client, new MemorySecretStore("safe-secret"), new FakeClock(DateTimeOffset.UtcNow));

        var health = await service.ExecuteAsync(RconDiagnosticCommand.HealthFull, Endpoint);
        var pause = await service.ExecuteAsync(RconDiagnosticCommand.PauseStatus, Endpoint);

        CollectionAssert.AreEqual(new[] { "ezzhealth full", "ezzpausestatus" }, client.Commands);
        Assert.AreEqual(RconExecutionStatus.Success, health.Status);
        Assert.AreEqual(RconExecutionStatus.Success, pause.Status);
        Assert.IsTrue(health.CommandSent);
    }

    [TestMethod]
    public async Task BlockCReadOnlyDiagnostics_UseOnlyClosedWhitelistedCommandTexts()
    {
        var client = new CommandAwareRconClient();
        var service = new RconDiagnosticService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));
        var commands = new[]
        {
            RconDiagnosticCommand.MapInfo,
            RconDiagnosticCommand.PowerStatus,
            RconDiagnosticCommand.PackAPunchStatus,
            RconDiagnosticCommand.RoundStatus,
            RconDiagnosticCommand.Players,
            RconDiagnosticCommand.MapAudit,
            RconDiagnosticCommand.EventStatus,
            RconDiagnosticCommand.PowerUpCatalog
        };

        var results = new List<RconExecutionResult>();
        foreach (var command in commands)
        {
            results.Add(await service.ExecuteAsync(command, Endpoint));
        }

        CollectionAssert.AreEqual(
            new[] { "ezzmap", "ezzpowerstatus", "ezzpapstatus", "ezzround", "ezzplayers", "ezzmapaudit full", "ezzeventstatus", "ezzpowerups" },
            client.Commands);
        Assert.IsTrue(results.All(result => result.Status == RconExecutionStatus.Success));
        Assert.IsTrue(results.All(result => result.CommandSent));
    }

    [TestMethod]
    public async Task PlayersDiagnostic_NeutralizesCompleteXuidBeforePresentation()
    {
        const string fullXuid = "1234567890abcdef";
        var service = new RconDiagnosticService(
            new CapturingRconClient($"[PinteMod] Connected players: 1\n[0] Joueur BOIII_XUID: {fullXuid}"),
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.Players, Endpoint);

        Assert.AreEqual(RconExecutionStatus.Success, result.Status);
        Assert.IsFalse(result.DisplayResponse.Contains(fullXuid, StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(result.DisplayResponse, "1234…cdef");
    }

    [TestMethod]
    public async Task UnknownDiagnostic_IsRejectedBeforeTransport()
    {
        var client = new CapturingRconClient("unused");
        var service = new RconDiagnosticService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync((RconDiagnosticCommand)int.MaxValue, Endpoint);

        Assert.AreEqual(RconExecutionStatus.TransportError, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task MissingSecret_NeverCallsTransport()
    {
        var client = new CapturingRconClient("OK");
        var service = new RconDiagnosticService(client, new MemorySecretStore(null), new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.HealthFull, Endpoint);

        Assert.AreEqual(RconExecutionStatus.SecretMissing, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task PublicAddress_IsRejectedBeforeSecretOrTransport()
    {
        var client = new CapturingRconClient("OK");
        var service = new RconDiagnosticService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(
            RconDiagnosticCommand.HealthFull,
            new RconEndpoint("8.8.8.8", 27018, TimeSpan.FromSeconds(3)));

        Assert.AreEqual(RconExecutionStatus.InvalidConfiguration, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task SharedGate_SerializesDiagnosticsAndMutationsAcrossServices()
    {
        var client = new BlockingFirstCallClient();
        var secretStore = new MemorySecretStore("safe-secret");
        var gate = new RconOperationGate();
        var diagnostics = new RconDiagnosticService(
            client,
            secretStore,
            new FakeClock(DateTimeOffset.UtcNow),
            gate);
        var pause = new CommunityPauseCommandService(
            client,
            secretStore,
            new FakeClock(DateTimeOffset.UtcNow),
            gate);

        var diagnosticTask = diagnostics.ExecuteAsync(RconDiagnosticCommand.HealthFull, Endpoint);
        await client.FirstCallEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var pauseTask = pause.ExecuteAsync(CommunityPauseAction.Pause, Endpoint);
        await Task.Delay(100);

        Assert.AreEqual(1, client.CallCount);
        Assert.AreEqual(1, client.MaximumConcurrentCalls);

        client.ReleaseFirstCall.TrySetResult();
        await Task.WhenAll(diagnosticTask, pauseTask);

        Assert.AreEqual(1, client.MaximumConcurrentCalls);
        CollectionAssert.AreEqual(
            new[] { "ezzhealth full", "ezzpauseforce", "ezzpausestatus" },
            client.Commands);
    }

    [TestMethod]
    public async Task Response_IsNeutralizedBeforePresentation()
    {
        const string privateResponse = "========== [PinteMod Health] ==========\n" +
                                       "Results PASS=51 | WARNING=0 | ERROR=0\n" +
                                       "xuid=1111111111111111 ip=192.168.1.44 guid=123e4567-e89b-42d3-a456-426614174000 path=C:\\private\\server";
        var service = new RconDiagnosticService(
            new CapturingRconClient(privateResponse),
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.HealthFull, Endpoint);

        Assert.IsFalse(result.DisplayResponse.Contains("1111111111111111", StringComparison.Ordinal));
        Assert.IsFalse(result.DisplayResponse.Contains("192.168.1.44", StringComparison.Ordinal));
        Assert.IsFalse(result.DisplayResponse.Contains("123e4567-e89b-42d3-a456-426614174000", StringComparison.Ordinal));
        Assert.IsFalse(result.DisplayResponse.Contains("C:\\private", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(RconExecutionStatus.Success, result.Status);
    }

    [TestMethod]
    public async Task NonEmptyUnrecognizedResponse_IsNeverAcceptedAsSuccess()
    {
        var service = new RconDiagnosticService(
            new CapturingRconClient("Unknown command ezzhealth"),
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.HealthFull, Endpoint);

        Assert.AreEqual(RconExecutionStatus.UnexpectedResponse, result.Status);
        Assert.IsTrue(result.CommandSent);
        StringAssert.Contains(result.DisplayResponse, "non reconnue");
    }

    [TestMethod]
    public async Task HeaderOnlyBoiiiReply_IsReportedAsSentWithoutText()
    {
        var service = new RconDiagnosticService(
            new CapturingRconClient(string.Empty),
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.HealthFull, Endpoint);

        Assert.AreEqual(RconExecutionStatus.EmptyResponse, result.Status);
        Assert.IsTrue(result.CommandSent);
        StringAssert.Contains(result.DisplayResponse, "BOIII a répondu sans texte");
        StringAssert.Contains(result.DisplayResponse, "console du serveur");
    }

    [TestMethod]
    public async Task HealthResponse_RequiresEveryStableMarker()
    {
        var service = new RconDiagnosticService(
            new CapturingRconClient("[PinteMod Health] PASS=51 WARNING=0"),
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.HealthFull, Endpoint);

        Assert.AreEqual(RconExecutionStatus.UnexpectedResponse, result.Status);
    }

    [TestMethod]
    public async Task PauseResponse_RequiresEveryStableMarker()
    {
        var service = new RconDiagnosticService(
            new CapturingRconClient("PINTEMOD COMMUNITY PAUSE\nModule: EXPERIMENTAL v0.3\nActive: 0"),
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.PauseStatus, Endpoint);

        Assert.AreEqual(RconExecutionStatus.UnexpectedResponse, result.Status);
    }

    [TestMethod]
    public async Task PauseResponse_AcceptsCommunityPauseV04WithStableMarkers()
    {
        var service = new RconDiagnosticService(
            new CapturingRconClient(
                "===== PINTEMOD COMMUNITY PAUSE =====\nModule: EXPERIMENTAL v0.4\nActive: 0\nSuccessful pauses: 0/2"),
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.PauseStatus, Endpoint);

        Assert.AreEqual(RconExecutionStatus.Success, result.Status);
    }

    [TestMethod]
    public async Task Timeout_IsReportedWithoutExceptionOrSecretLeak()
    {
        var service = new RconDiagnosticService(
            new ThrowingRconClient(new TimeoutException()),
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.PauseStatus, Endpoint);

        Assert.AreEqual(RconExecutionStatus.Timeout, result.Status);
        Assert.IsTrue(result.CommandSent);
        Assert.IsFalse(result.DisplayResponse.Contains("safe-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SocketFailureDuringDiagnosticCall_IsConservativelyMarkedAsPossiblySent()
    {
        var client = new ThrowingRconClient(new SocketException());
        var service = new RconDiagnosticService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(RconDiagnosticCommand.HealthFull, Endpoint);

        Assert.AreEqual(RconExecutionStatus.TransportError, result.Status);
        Assert.IsTrue(result.CommandSent);
        Assert.AreEqual(1, client.CallCount);
        StringAssert.Contains(result.DisplayResponse, "envoi potentiellement effectué");
    }

    [TestMethod]
    public void OperatorActivity_IsInMemoryBoundedAndNeutralized()
    {
        var store = new InMemoryOperatorActivityStore();
        for (var index = 0; index < 120; index++)
        {
            store.RecordRconResult(new RconExecutionResult(
                RconDiagnosticCommand.HealthFull,
                RconExecutionStatus.Success,
                $"xuid=1111111111111111 ip=192.168.1.44 result={index}",
                true,
                DateTimeOffset.UtcNow.AddSeconds(index)));
        }

        var events = store.GetSnapshot();

        Assert.AreEqual(100, events.Count);
        Assert.IsTrue(events.All(item => item.Category == "RCON"));
        Assert.IsTrue(events.All(item => !item.Details.Contains("1111111111111111", StringComparison.Ordinal)));
        Assert.IsTrue(events.All(item => !item.Details.Contains("192.168.1.44", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ServerAdministrationActivity_IsNeutralizedAndExplicitlyUnconfirmed()
    {
        const string fullXuid = "1234567890abcdef";
        var store = new InMemoryOperatorActivityStore();
        var request = new ServerAdministrationRequest(ServerAdministrationAction.EnablePower);
        store.RecordServerAdministrationResult(new ServerAdministrationExecutionResult(
            request,
            ServerAdministrationExecutionStatus.SentAwaitingManualVerification,
            $"Console BOIII · BOIII_XUID: {fullXuid}",
            true,
            DateTimeOffset.UtcNow));

        var item = store.GetSnapshot().Single();

        Assert.AreEqual("RCON", item.Category);
        StringAssert.Contains(item.Title, "Activer le courant");
        Assert.IsFalse(item.Details.Contains(fullXuid, StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(item.Details, "Commande envoyée : Oui");
    }

    [TestMethod]
    public void PlayerAdministrationActivity_NeverStoresCompleteTargetXuid()
    {
        const string fullXuid = "1234567890abcdef";
        var store = new InMemoryOperatorActivityStore();
        var request = new PlayerAdministrationRequest(
            PlayerAdministrationAction.GiveWeapon,
            fullXuid,
            Option: "raygun");
        store.RecordPlayerAdministrationResult(new PlayerAdministrationExecutionResult(
            request,
            PlayerAdministrationExecutionStatus.SentAwaitingManualVerification,
            $"Cible {fullXuid}",
            true,
            DateTimeOffset.UtcNow));

        var item = store.GetSnapshot().Single();

        Assert.AreEqual("RCON", item.Category);
        StringAssert.Contains(item.Title, "Administration joueur");
        Assert.IsFalse(item.Details.Contains(fullXuid, StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(item.Details, "Cible XUID neutralisée");
    }

    private sealed class CapturingRconClient(string response) : IRconClient
    {
        public List<string> Commands { get; } = [];

        public Task<string> SendAsync(
            RconEndpoint endpoint,
            string password,
            string command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(response);
        }
    }

    private sealed class CommandAwareRconClient : IRconClient
    {
        public List<string> Commands { get; } = [];

        public Task<string> SendAsync(
            RconEndpoint endpoint,
            string password,
            string command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            var response = command switch
            {
                "ezzhealth full" => "========== [PinteMod Health] ==========\nResults PASS=51 | WARNING=0 | ERROR=0",
                "ezzpausestatus" => "===== PINTEMOD COMMUNITY PAUSE =====\nModule: EXPERIMENTAL v0.3\nActive: 0\nSuccessful pauses: 0",
                "ezzmap" => "========== PinteMod MAP INFO v0.11.0 ==========\nMap: zm_tomb\nPack-a-Punch triggers: 1\nProfile power: generators\nProfile PaP: quest",
                "ezzpowerstatus" => "[PinteMod] Profile: Origins generators\n[PinteMod] Global power flag is OFF",
                "ezzpapstatus" => "========== PinteMod PACK-A-PUNCH ==========\nMap: Origins\nAccess profile: quest\nPack-a-Punch triggers: 1\nPowered machines: 0",
                "ezzround" => "[PinteMod] Current round: 12\n[PinteMod] Living AI: 8\n[PinteMod] Remaining spawn queue: 17",
                "ezzplayers" => "[PinteMod] Connected players: 1\n[0] Joueur",
                "ezzmapaudit full" => "========== [PinteMod Map Audit] ==========\nMap Origins\nProfile official\nPower generators\nPack-a-Punch quest\nEvents supported\nBosses supported",
                "ezzeventstatus" => "========== PINTEMOD EVENTS ==========\nEnabled: true\nMap: zm_tomb\nBackend: native SpawnActor",
                "ezzpowerups" => "========== PinteMod POWERUPS v0.6.1 ==========\nmaxammo - available\ninstakill - available\ndoublepoints - available",
                _ => "Unknown command"
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingRconClient(Exception exception) : IRconClient
    {
        public int CallCount { get; private set; }

        public Task<string> SendAsync(
            RconEndpoint endpoint,
            string password,
            string command,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromException<string>(exception);
        }
    }

    private sealed class BlockingFirstCallClient : IRconClient
    {
        private int _activeCalls;
        private int _callCount;
        private int _maximumConcurrentCalls;

        public TaskCompletionSource FirstCallEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Commands { get; } = [];

        public int CallCount => Volatile.Read(ref _callCount);

        public int MaximumConcurrentCalls => Volatile.Read(ref _maximumConcurrentCalls);

        public async Task<string> SendAsync(
            RconEndpoint endpoint,
            string password,
            string command,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            var active = Interlocked.Increment(ref _activeCalls);
            UpdateMaximum(active);
            lock (Commands)
            {
                Commands.Add(command);
            }

            try
            {
                if (call == 1)
                {
                    FirstCallEntered.TrySetResult();
                    await ReleaseFirstCall.Task.WaitAsync(cancellationToken);
                }

                return command == "ezzhealth full"
                    ? "[PinteMod Health] PASS=51 WARNING=0 ERROR=0"
                    : string.Empty;
            }
            finally
            {
                Interlocked.Decrement(ref _activeCalls);
            }
        }

        private void UpdateMaximum(int candidate)
        {
            var current = Volatile.Read(ref _maximumConcurrentCalls);
            while (candidate > current)
            {
                var observed = Interlocked.CompareExchange(
                    ref _maximumConcurrentCalls,
                    candidate,
                    current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }

    private sealed class MemorySecretStore(string? secret) : IRconSecretStore
    {
        public Task<bool> HasSecretAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(!string.IsNullOrEmpty(secret));

        public Task SaveAsync(string value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(secret);
    }
}
