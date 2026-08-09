using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Rcon;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PlayerAdministrationCommandServiceTests
{
    private const string Xuid = "1234567890abcdef";
    private static readonly RconEndpoint Endpoint = new("127.0.0.1", 27018, TimeSpan.FromSeconds(3));

    [TestMethod]
    public async Task SupportedActions_UseOnlyClosedWhitelistedCommandTexts()
    {
        var client = new CapturingClient(string.Empty);
        var service = CreateService(client);
        var requests = new[]
        {
            new PlayerAdministrationRequest(PlayerAdministrationAction.Revive, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.Respawn, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GrantPoints, Xuid, 5000),
            new PlayerAdministrationRequest(PlayerAdministrationAction.RefillAmmo, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.ToggleGodMode, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GiveWeapon, Xuid, Option: "raygun"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GivePerk, Xuid, Option: "jug"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GiveAllPerks, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GivePowerUp, Xuid, Option: "maxammo"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.TeleportToOwnAim, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.Mute, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.Unmute, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.Kick, Xuid),
            new PlayerAdministrationRequest(PlayerAdministrationAction.Ban, Xuid, Option: "7d"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.SetRole, Xuid, Option: "moderator"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.RemoveRole, Xuid)
        };

        var results = new List<PlayerAdministrationExecutionResult>();
        foreach (var request in requests)
        {
            results.Add(await service.ExecuteAsync(request, Endpoint));
        }

        CollectionAssert.AreEqual(
            new[]
            {
                $"ezzrevive {Xuid}",
                $"ezzspawn {Xuid}",
                $"points {Xuid} 5000",
                $"ammo {Xuid}",
                $"godmode {Xuid}",
                $"ezzweapon {Xuid} raygun",
                $"ezzperk {Xuid} jug",
                $"ezzallperks {Xuid}",
                $"ezzpowerup {Xuid} maxammo",
                $"ezztp {Xuid}",
                $"ezzmute {Xuid} control-center",
                $"ezzunmute {Xuid}",
                $"ezzkick {Xuid} control-center",
                $"ezzban {Xuid} 7d control-center",
                $"ezzidsetrole {Xuid} moderator",
                $"ezzidremoverole {Xuid}"
            },
            client.Commands);
        Assert.IsTrue(results.All(result => result.CommandSent));
    }

    [TestMethod]
    public async Task EveryConfiguredWeaponAndPerkAlias_IsAcceptedExactly()
    {
        var client = new CapturingClient(string.Empty);
        var service = CreateService(client);
        var weapons = new[] { "raygun", "raygunmk2", "kn44", "haymaker", "dingo" };
        var perks = new[] { "jug", "quick", "speed", "doubletap", "staminup", "deadshot", "mule", "cherry", "widows" };
        var durations = new[] { "30m", "2h", "7d", "4w", "perm" };
        var roles = new[] { "helper", "moderator", "admin" };
        var powerUps = new[] { "maxammo", "instakill", "doublepoints", "firesale", "carpenter", "nuke", "deathmachine", "freeperk", "shield" };

        foreach (var alias in weapons)
        {
            await service.ExecuteAsync(
                new PlayerAdministrationRequest(PlayerAdministrationAction.GiveWeapon, Xuid, Option: alias),
                Endpoint);
        }

        foreach (var alias in perks)
        {
            await service.ExecuteAsync(
                new PlayerAdministrationRequest(PlayerAdministrationAction.GivePerk, Xuid, Option: alias),
                Endpoint);
        }

        foreach (var duration in durations)
        {
            await service.ExecuteAsync(
                new PlayerAdministrationRequest(PlayerAdministrationAction.Ban, Xuid, Option: duration),
                Endpoint);
        }

        foreach (var alias in powerUps)
        {
            await service.ExecuteAsync(
                new PlayerAdministrationRequest(PlayerAdministrationAction.GivePowerUp, Xuid, Option: alias),
                Endpoint);
        }

        foreach (var role in roles)
        {
            await service.ExecuteAsync(
                new PlayerAdministrationRequest(PlayerAdministrationAction.SetRole, Xuid, Option: role),
                Endpoint);
        }

        CollectionAssert.AreEqual(
            weapons.Select(alias => $"ezzweapon {Xuid} {alias}")
                .Concat(perks.Select(alias => $"ezzperk {Xuid} {alias}"))
                .Concat(durations.Select(duration => $"ezzban {Xuid} {duration} control-center"))
                .Concat(powerUps.Select(alias => $"ezzpowerup {Xuid} {alias}"))
                .Concat(roles.Select(role => $"ezzidsetrole {Xuid} {role}"))
                .ToArray(),
            client.Commands);
    }

    [TestMethod]
    public async Task InvalidTargetOptionOrAmount_IsRejectedBeforeSecretAndTransport()
    {
        var client = new CapturingClient(string.Empty);
        var secret = new CountingSecretStore("secret");
        var service = new PlayerAdministrationCommandService(client, secret, new FakeClock(DateTimeOffset.UtcNow));
        var requests = new[]
        {
            new PlayerAdministrationRequest(PlayerAdministrationAction.Revive, "Alice"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GrantPoints, Xuid, 0),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GrantPoints, Xuid, 1_000_000),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GiveWeapon, Xuid, Option: "raygun;quit"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GivePerk, Xuid, Option: "unknown"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.GivePowerUp, Xuid, Option: "maxammo;quit"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.Ban, Xuid, Option: "forever;quit"),
            new PlayerAdministrationRequest(PlayerAdministrationAction.SetRole, Xuid, Option: "owner"),
            new PlayerAdministrationRequest((PlayerAdministrationAction)int.MaxValue, Xuid)
        };

        foreach (var request in requests)
        {
            var result = await service.ExecuteAsync(request, Endpoint);
            Assert.AreEqual(PlayerAdministrationExecutionStatus.InvalidRequest, result.Status);
            Assert.IsFalse(result.CommandSent);
        }

        Assert.AreEqual(0, secret.ReadCount);
        Assert.AreEqual(0, client.Commands.Count);
    }

    [TestMethod]
    public async Task TimeoutAndSocketFailure_ArePossiblySentAndNeverRetried()
    {
        foreach (var exception in new Exception[]
                 {
                     new TimeoutException(),
                     new SocketException((int)SocketError.ConnectionReset)
                 })
        {
            var client = new ThrowingClient(exception);
            var result = await CreateService(client).ExecuteAsync(
                new PlayerAdministrationRequest(PlayerAdministrationAction.RefillAmmo, Xuid),
                Endpoint);

            Assert.IsTrue(result.CommandSent);
            Assert.AreEqual(1, client.CallCount);
        }
    }

    [TestMethod]
    public async Task TransportResponse_NeverExposesCompleteTargetXuid()
    {
        var result = await CreateService(new CapturingClient($"Done for {Xuid}")).ExecuteAsync(
            new PlayerAdministrationRequest(PlayerAdministrationAction.Revive, Xuid),
            Endpoint);

        Assert.IsFalse(result.DisplayMessage.Contains(Xuid, StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(result.DisplayMessage, "1234…cdef");
    }

    private static PlayerAdministrationCommandService CreateService(IRconClient client) => new(
        client,
        new CountingSecretStore("secret"),
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

    private sealed class CountingSecretStore(string? secret) : IRconSecretStore
    {
        public int ReadCount { get; private set; }

        public Task<bool> HasSecretAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(!string.IsNullOrEmpty(secret));

        public Task SaveAsync(string value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(secret);
        }
    }
}
