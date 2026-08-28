using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Rcon;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class CommunityPauseCommandServiceTests
{
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27018, TimeSpan.FromSeconds(3));

    [TestMethod]
    public async Task PauseAndResume_UseOnlyClosedWhitelistAndRequestStatus()
    {
        var client = new CapturingClient();
        var service = new CommunityPauseCommandService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var pause = await service.ExecuteAsync(CommunityPauseAction.Pause, Endpoint);
        var resume = await service.ExecuteAsync(CommunityPauseAction.Resume, Endpoint);

        CollectionAssert.AreEqual(
            new[] { "ezzpauseforce", "ezzpausestatus", "ezzresume", "ezzpausestatus" },
            client.Commands);
        Assert.AreEqual(CommunityPauseExecutionStatus.SentAwaitingObservation, pause.Status);
        Assert.AreEqual(CommunityPauseExecutionStatus.SentAwaitingObservation, resume.Status);
        Assert.IsTrue(pause.CommandSent);
        Assert.IsTrue(pause.StatusRefreshRequested);
    }

    [TestMethod]
    public async Task MissingSecret_NeverCallsTransport()
    {
        var client = new CapturingClient();
        var service = new CommunityPauseCommandService(
            client,
            new MemorySecretStore(null),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(CommunityPauseAction.Pause, Endpoint);

        Assert.AreEqual(CommunityPauseExecutionStatus.SecretMissing, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task PublicAddress_IsRejectedBeforeTransport()
    {
        var client = new CapturingClient();
        var service = new CommunityPauseCommandService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(
            CommunityPauseAction.Pause,
            new RconEndpoint("8.8.8.8", 27018, TimeSpan.FromSeconds(3)));

        Assert.AreEqual(CommunityPauseExecutionStatus.InvalidConfiguration, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task Timeout_IsDeliveryUnknownAndIsNeverRetried()
    {
        var client = new ThrowingClient(new TimeoutException());
        var service = new CommunityPauseCommandService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(CommunityPauseAction.Pause, Endpoint);

        Assert.AreEqual(CommunityPauseExecutionStatus.DeliveryUnknown, result.Status);
        Assert.IsTrue(result.CommandSent);
        Assert.AreEqual(1, client.CallCount);
        StringAssert.Contains(result.DisplayMessage, "ne recommencez pas");
    }

    [TestMethod]
    public async Task SocketFailureDuringFirstMutationCall_IsConservativelyMarkedAsPossiblySent()
    {
        var client = new ThrowingClient(new System.Net.Sockets.SocketException(
            (int)System.Net.Sockets.SocketError.ConnectionReset));
        var service = new CommunityPauseCommandService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(CommunityPauseAction.Pause, Endpoint);

        Assert.AreEqual(CommunityPauseExecutionStatus.TransportError, result.Status);
        Assert.IsTrue(result.CommandSent);
        Assert.IsFalse(result.StatusRefreshRequested);
        Assert.AreEqual(1, client.CallCount);
        StringAssert.Contains(result.DisplayMessage, "Résultat incertain");
    }

    [TestMethod]
    public async Task StatusRequestFailure_AfterMutation_RemainsMarkedAsSentAndUncertain()
    {
        var client = new SecondCallThrowingClient();
        var service = new CommunityPauseCommandService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync(CommunityPauseAction.Pause, Endpoint);

        Assert.AreEqual(CommunityPauseExecutionStatus.TransportError, result.Status);
        Assert.IsTrue(result.CommandSent);
        Assert.IsFalse(result.StatusRefreshRequested);
        Assert.AreEqual(2, client.CallCount);
    }

    [TestMethod]
    public async Task UnknownAction_IsRejectedBeforeTransport()
    {
        var client = new CapturingClient();
        var service = new CommunityPauseCommandService(
            client,
            new MemorySecretStore("safe-secret"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await service.ExecuteAsync((CommunityPauseAction)int.MaxValue, Endpoint);

        Assert.AreEqual(CommunityPauseExecutionStatus.TransportError, result.Status);
        Assert.IsFalse(result.CommandSent);
        Assert.AreEqual(0, client.Commands.Count);
    }

    private sealed class CapturingClient : IRconClient
    {
        public List<string> Commands { get; } = [];

        public Task<string> SendAsync(
            RconEndpoint endpoint,
            string password,
            string command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(string.Empty);
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

    private sealed class SecondCallThrowingClient : IRconClient
    {
        public int CallCount { get; private set; }

        public Task<string> SendAsync(
            RconEndpoint endpoint,
            string password,
            string command,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return CallCount == 1
                ? Task.FromResult(string.Empty)
                : Task.FromException<string>(new System.IO.IOException("status unavailable"));
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
