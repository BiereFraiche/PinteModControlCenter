using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Rcon;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ServerAdministrationCommandServiceTests
{
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27018, TimeSpan.FromSeconds(3));

    [TestMethod]
    public async Task SupportedActions_UseOnlyClosedWhitelistedCommandTexts()
    {
        var client = new CapturingClient(string.Empty);
        var service = CreateService(client);
        var requests = new[]
        {
            new ServerAdministrationRequest(ServerAdministrationAction.NextRound),
            new ServerAdministrationRequest(ServerAdministrationAction.SetRound, 42),
            new ServerAdministrationRequest(ServerAdministrationAction.EnablePower),
            new ServerAdministrationRequest(ServerAdministrationAction.EnablePackAPunch),
            new ServerAdministrationRequest(ServerAdministrationAction.PlayMapMusic),
            new ServerAdministrationRequest(ServerAdministrationAction.StopMapMusic),
            new ServerAdministrationRequest(ServerAdministrationAction.UnlockStandardPassages),
            new ServerAdministrationRequest(ServerAdministrationAction.KeepLastZombie),
            new ServerAdministrationRequest(ServerAdministrationAction.KillAllZombies),
            new ServerAdministrationRequest(ServerAdministrationAction.MakePowerUpsPermanent),
            new ServerAdministrationRequest(ServerAdministrationAction.RestorePowerUpTimeout)
        };

        var results = new List<ServerAdministrationExecutionResult>();
        foreach (var request in requests)
        {
            results.Add(await service.ExecuteAsync(request, Endpoint));
        }

        CollectionAssert.AreEqual(
            new[]
            {
                "ezznextround",
                "ezzsetround 42",
                "ezzpower",
                "ezzpap",
                "ezzmusicplayall",
                "ezzmusicstopall",
                "ezzunlock",
                "ezzlastzombie",
                "ezzkillzombies",
                "ezzfreezepowerups on",
                "ezzfreezepowerups off"
            },
            client.Commands);
        Assert.IsTrue(results.All(result =>
            result.Status == ServerAdministrationExecutionStatus.SentAwaitingManualVerification));
        Assert.IsTrue(results.All(result => result.CommandSent));
    }

    [TestMethod]
    public async Task NonSecretContractActions_UseOnlyFourClosedCommandsAndNeverExposeEvent()
    {
        var client = new CapturingClient(string.Empty);
        var service = CreateService(client);
        var requests = new[]
        {
            new ServerAdministrationRequest(
                ServerAdministrationAction.RestartMap,
                RequestId: "request_0001"),
            new ServerAdministrationRequest(
                ServerAdministrationAction.SpawnBoss,
                RequestId: "request_0002",
                Option: "margwa",
                TargetXuid: "0000000000000001"),
            new ServerAdministrationRequest(
                ServerAdministrationAction.SetHostname,
                RequestId: "request_0003",
                Option: "^7[^4FR^7] ^1PinteMod"),
            new ServerAdministrationRequest(
                ServerAdministrationAction.ClearJoinPassword,
                RequestId: "request_0004")
        };

        foreach (var request in requests)
        {
            var result = await service.ExecuteAsync(request, Endpoint);
            Assert.AreEqual(ServerAdministrationExecutionStatus.SentAwaitingManualVerification, result.Status);
            Assert.IsTrue(result.CommandSent);
        }

        CollectionAssert.AreEqual(
            new[]
            {
                "ezzccrestartmap request_0001",
                "ezzccboss request_0002 margwa 0000000000000001",
                "ezzccsethostname request_0003 ^7[^4FR^7] ^1PinteMod",
                "ezzccclearjoinpassword request_0004"
            },
            client.Commands);
        Assert.IsFalse(client.Commands.Any(command =>
            command.Contains("ezzccmap", StringComparison.Ordinal) ||
            command.Contains("ezzccevent", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ChangeMap_OfficialMapUsesClosedCommandAndLanEndpointIsAllowed()
    {
        var client = new CapturingClient("CHANGE_MAP_ACCEPTED");
        var service = CreateService(client);
        var lanEndpoint = new RconEndpoint("192.168.50.25", 27018, TimeSpan.FromSeconds(3));

        var result = await service.ExecuteAsync(
            new ServerAdministrationRequest(
                ServerAdministrationAction.ChangeMap,
                RequestId: "0123456789abcdef0123456789abcdef",
                Option: "zm_castle"),
            lanEndpoint);

        Assert.AreEqual(ServerAdministrationExecutionStatus.SentAwaitingManualVerification, result.Status);
        Assert.IsTrue(result.CommandSent);
        CollectionAssert.AreEqual(new[] { "ezzccmap 0123456789abcdef0123456789abcdef zm_castle" }, client.Commands);
    }

    [TestMethod]
    public async Task ChangeMap_CustomOrInjectedMapIsRejectedBeforeSecretAndTransport()
    {
        var client = new CapturingClient(string.Empty);
        var secret = new CountingSecretStore("secret");
        var service = new ServerAdministrationCommandService(client, secret, new FakeClock(DateTimeOffset.UtcNow));
        var invalid = new[]
        {
            "zm_custom",
            "zm_castle;quit",
            "map zm_castle",
            "../zm_castle"
        };

        foreach (var map in invalid)
        {
            var result = await service.ExecuteAsync(
                new ServerAdministrationRequest(
                    ServerAdministrationAction.ChangeMap,
                    RequestId: "fedcba9876543210fedcba9876543210",
                    Option: map),
                Endpoint);

            Assert.AreEqual(ServerAdministrationExecutionStatus.InvalidRequest, result.Status);
            Assert.IsFalse(result.CommandSent);
        }

        Assert.AreEqual(0, secret.ReadCount);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task SetJoinPassword_LoopbackUsesDedicatedCommandWithoutReturningSecret()
    {
        var client = new CapturingClient(string.Empty);
        var service = CreateService(client);

        var result = await service.SetJoinPasswordAsync(
            "request_0005",
            "Safe#2026",
            Endpoint);

        Assert.AreEqual(ServerAdministrationExecutionStatus.SentAwaitingManualVerification, result.Status);
        Assert.IsTrue(result.CommandSent);
        CollectionAssert.AreEqual(new[] { "ezzccsetjoinpassword request_0005 Safe#2026" }, client.Commands);
        Assert.AreEqual(ServerAdministrationAction.SetJoinPassword, result.Request.Action);
        Assert.IsNull(result.Request.Option);
        Assert.IsFalse(result.DisplayMessage.Contains("Safe#2026", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SetJoinPassword_PrivateLanAddressUsesClosedCommandWithoutReturningSecret()
    {
        var client = new CapturingClient(string.Empty);
        var service = CreateService(client);

        var result = await service.SetJoinPasswordAsync(
            "request_0005",
            "Safe#2026",
            new RconEndpoint("192.168.50.25", 27018, TimeSpan.FromSeconds(3)));

        Assert.AreEqual(ServerAdministrationExecutionStatus.SentAwaitingManualVerification, result.Status);
        Assert.IsTrue(result.CommandSent);
        CollectionAssert.AreEqual(new[] { "ezzccsetjoinpassword request_0005 Safe#2026" }, client.Commands);
        Assert.IsFalse(result.DisplayMessage.Contains("Safe#2026", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SetJoinPassword_PublicAddressIsRejectedBeforeSecretAndTransport()
    {
        var client = new CapturingClient(string.Empty);
        var secret = new CountingSecretStore("secret");
        var service = new ServerAdministrationCommandService(client, secret, new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.SetJoinPasswordAsync(
            "request_0005",
            "Safe#2026",
            new RconEndpoint("8.8.8.8", 27018, TimeSpan.FromSeconds(3)));

        Assert.AreEqual(ServerAdministrationExecutionStatus.InvalidConfiguration, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, secret.ReadCount);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task SetJoinPassword_InvalidValueIsRejectedBeforeSecretAndTransport()
    {
        var client = new CapturingClient(string.Empty);
        var secret = new CountingSecretStore("secret");
        var service = new ServerAdministrationCommandService(client, secret, new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.SetJoinPasswordAsync("request_0005", "bad value;quit", Endpoint);

        Assert.AreEqual(ServerAdministrationExecutionStatus.InvalidRequest, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, secret.ReadCount);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task ContractInjectionAndUnpublishedBossAlias_AreRejectedBeforeSecretAndTransport()
    {
        var invalid = new[]
        {
            new ServerAdministrationRequest(ServerAdministrationAction.RestartMap, RequestId: "bad id;map"),
            new ServerAdministrationRequest(
                ServerAdministrationAction.SpawnBoss,
                RequestId: "request_0002",
                Option: "custom;boss",
                TargetXuid: "0000000000000001"),
            new ServerAdministrationRequest(
                ServerAdministrationAction.SpawnBoss,
                RequestId: "request_0002",
                Option: "margwa",
                TargetXuid: "name-only"),
            new ServerAdministrationRequest(
                ServerAdministrationAction.SetHostname,
                RequestId: "request_0003",
                Option: "bad;hostname"),
            new ServerAdministrationRequest(
                ServerAdministrationAction.ClearJoinPassword,
                RequestId: "request_0004",
                Option: "secret"),
            new ServerAdministrationRequest(
                ServerAdministrationAction.EnablePower,
                Option: "unexpected")
        };
        var client = new CapturingClient(string.Empty);
        var secret = new CountingSecretStore("secret");
        var service = new ServerAdministrationCommandService(client, secret, new FakeClock(DateTimeOffset.UtcNow));

        foreach (var request in invalid)
        {
            var result = await service.ExecuteAsync(request, Endpoint);
            Assert.AreEqual(ServerAdministrationExecutionStatus.InvalidRequest, result.Status);
            Assert.IsFalse(result.CommandSent);
        }

        Assert.AreEqual(0, secret.ReadCount);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    [DataRow(ServerAdministrationAction.SetRound, null)]
    [DataRow(ServerAdministrationAction.SetRound, 1)]
    [DataRow(ServerAdministrationAction.SetRound, 256)]
    [DataRow(ServerAdministrationAction.NextRound, 10)]
    [DataRow((ServerAdministrationAction)int.MaxValue, null)]
    public async Task InvalidRequest_IsRejectedBeforeSecretAndTransport(
        ServerAdministrationAction action,
        int? targetRound)
    {
        var client = new CapturingClient(string.Empty);
        var secret = new CountingSecretStore("secret");
        var service = new ServerAdministrationCommandService(
            client,
            secret,
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(
            new ServerAdministrationRequest(action, targetRound),
            Endpoint);

        Assert.AreEqual(ServerAdministrationExecutionStatus.InvalidRequest, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, secret.ReadCount);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task PublicAddress_IsRejectedBeforeTransport()
    {
        var client = new CapturingClient(string.Empty);
        var service = CreateService(client);

        var result = await service.ExecuteAsync(
            new ServerAdministrationRequest(ServerAdministrationAction.EnablePower),
            new RconEndpoint("8.8.8.8", 27018, TimeSpan.FromSeconds(3)));

        Assert.AreEqual(ServerAdministrationExecutionStatus.InvalidConfiguration, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task MissingSecret_NeverCallsTransport()
    {
        var client = new CapturingClient(string.Empty);
        var service = CreateService(client, null);

        var result = await service.ExecuteAsync(
            new ServerAdministrationRequest(ServerAdministrationAction.EnablePackAPunch),
            Endpoint);

        Assert.AreEqual(ServerAdministrationExecutionStatus.SecretMissing, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task Timeout_IsConservativelyMarkedAsPossiblySentWithoutRetry()
    {
        var client = new ThrowingClient(new TimeoutException());
        var service = CreateService(client);

        var result = await service.ExecuteAsync(
            new ServerAdministrationRequest(ServerAdministrationAction.NextRound),
            Endpoint);

        Assert.AreEqual(ServerAdministrationExecutionStatus.DeliveryUnknown, result.Status);
        Assert.IsTrue(result.CommandSent);
        Assert.AreEqual(1, client.CallCount);
    }

    [TestMethod]
    public async Task SocketFailure_IsConservativelyMarkedAsPossiblySentWithoutRetry()
    {
        var client = new ThrowingClient(
            new SocketException((int)SocketError.ConnectionReset));
        var service = CreateService(client);

        var result = await service.ExecuteAsync(
            new ServerAdministrationRequest(ServerAdministrationAction.EnablePower),
            Endpoint);

        Assert.AreEqual(ServerAdministrationExecutionStatus.TransportError, result.Status);
        Assert.IsTrue(result.CommandSent);
        Assert.AreEqual(1, client.CallCount);
    }

    [TestMethod]
    public async Task TextResponse_IsNeutralizedBeforePresentation()
    {
        const string fullXuid = "1234567890abcdef";
        var service = CreateService(new CapturingClient($"Done for BOIII_XUID: {fullXuid}"));

        var result = await service.ExecuteAsync(
            new ServerAdministrationRequest(ServerAdministrationAction.EnablePower),
            Endpoint);

        Assert.IsFalse(result.DisplayMessage.Contains(fullXuid, StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(result.DisplayMessage, "1234…cdef");
    }

    [TestMethod]
    public async Task SharedGate_SerializesConcurrentServerMutations()
    {
        var client = new ConcurrencyTrackingClient();
        var service = new ServerAdministrationCommandService(
            client,
            new CountingSecretStore("secret"),
            new FakeClock(DateTimeOffset.UtcNow),
            new RconOperationGate());

        await Task.WhenAll(
            service.ExecuteAsync(
                new ServerAdministrationRequest(ServerAdministrationAction.EnablePower),
                Endpoint),
            service.ExecuteAsync(
                new ServerAdministrationRequest(ServerAdministrationAction.EnablePackAPunch),
                Endpoint));

        Assert.AreEqual(1, client.MaximumConcurrency);
    }

    private static ServerAdministrationCommandService CreateService(
        IRconClient client,
        string? secret = "secret") => new(
        client,
        new CountingSecretStore(secret),
        new FakeClock(DateTimeOffset.UtcNow));

    private sealed class CapturingClient(string response) : IRconClient
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

    private sealed class ThrowingClient(Exception exception) : IRconClient
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

    private sealed class ConcurrencyTrackingClient : IRconClient
    {
        private int _concurrency;

        public int MaximumConcurrency { get; private set; }

        public async Task<string> SendAsync(
            RconEndpoint endpoint,
            string password,
            string command,
            CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref _concurrency);
            MaximumConcurrency = Math.Max(MaximumConcurrency, current);
            try
            {
                await Task.Delay(40, cancellationToken);
                return string.Empty;
            }
            finally
            {
                Interlocked.Decrement(ref _concurrency);
            }
        }
    }

    private sealed class CountingSecretStore(string? secret) : IRconSecretStore
    {
        public int ReadCount { get; private set; }

        public Task<bool> HasSecretAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(!string.IsNullOrEmpty(secret));

        public Task SaveAsync(string value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(secret);
        }
    }
}
