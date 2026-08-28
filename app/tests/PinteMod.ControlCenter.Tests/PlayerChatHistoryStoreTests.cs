using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PlayerChatHistoryStoreTests
{
    [TestMethod]
    public async Task History_PersistsAcrossReopenAndKeepsMapChanges()
    {
        using var temp = new TemporaryChatStorage();
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero));
        var first = Message(1, clock.UtcNow.AddMinutes(-2), "PlayerOne", "ready", "zm_castle", "Der Eisendrache");
        var second = Message(2, clock.UtcNow.AddMinutes(-1), "Player", "hello", "zm_stalingrad", "Gorod Krovi");

        using (var store = new JsonPlayerChatHistoryStore(temp.Path, clock))
        {
            await store.MergeAsync([first]);
            await store.MergeAsync([second]);
        }

        using var reopened = new JsonPlayerChatHistoryStore(temp.Path, clock);
        var restored = await reopened.LoadAsync();

        Assert.AreEqual(2, restored.Count);
        Assert.AreEqual("Der Eisendrache", restored[0].MapLabel);
        Assert.AreEqual("Gorod Krovi", restored[1].MapLabel);
    }

    [TestMethod]
    public async Task DuplicateEvent_IsNotPersistedTwice()
    {
        using var temp = new TemporaryChatStorage();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var message = Message(1, clock.UtcNow, "PlayerOne", "SALUT", "zm_castle", "Der Eisendrache");
        using var store = new JsonPlayerChatHistoryStore(temp.Path, clock);

        await store.MergeAsync([message]);
        var result = await store.MergeAsync([message]);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task History_IsCappedAtTwoThousandMessages()
    {
        using var temp = new TemporaryChatStorage();
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero));
        using var store = new JsonPlayerChatHistoryStore(temp.Path, clock);
        var messages = Enumerable.Range(0, PlayerChatHistoryPolicy.MaximumMessages + 25)
            .Select(index => Message(
                index,
                clock.UtcNow.AddSeconds(index),
                "Player",
                $"message {index}",
                "zm_castle",
                "Der Eisendrache"))
            .ToArray();

        var result = await store.MergeAsync(messages);

        Assert.AreEqual(PlayerChatHistoryPolicy.MaximumMessages, result.Count);
        Assert.AreEqual("message 25", result[0].Message);
    }

    [TestMethod]
    public async Task SeparateProfilePaths_NeverMixHistories()
    {
        using var temp = new TemporaryChatStorage();
        var profileOnePath = System.IO.Path.Combine(temp.Directory, "profile-one", "player-chat-history.json");
        var profileTwoPath = System.IO.Path.Combine(temp.Directory, "profile-two", "player-chat-history.json");
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        using var firstStore = new JsonPlayerChatHistoryStore(profileOnePath, clock);
        using var secondStore = new JsonPlayerChatHistoryStore(profileTwoPath, clock);

        await firstStore.MergeAsync([Message(1, clock.UtcNow, "One", "srv1", "zm_castle", "Der Eisendrache")]);
        await secondStore.MergeAsync([Message(2, clock.UtcNow, "Two", "srv2", "zm_tomb", "Origins")]);

        var first = await firstStore.LoadAsync();
        var second = await secondStore.LoadAsync();
        Assert.AreEqual("srv1", first.Single().Message);
        Assert.AreEqual("srv2", second.Single().Message);
    }

    [TestMethod]
    public async Task ClearMarker_PreventsOldServerMessagesFromReappearingAfterReopen()
    {
        using var temp = new TemporaryChatStorage();
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero));
        var oldMessage = Message(1, clock.UtcNow.AddMinutes(-1), "PlayerOne", "old", "zm_castle", "Der Eisendrache");
        using (var store = new JsonPlayerChatHistoryStore(temp.Path, clock))
        {
            await store.MergeAsync([oldMessage]);
            await store.ClearAsync();
        }

        using var reopened = new JsonPlayerChatHistoryStore(temp.Path, clock);
        var afterReimportAttempt = await reopened.MergeAsync([oldMessage]);

        Assert.AreEqual(0, afterReimportAttempt.Count);
    }

    [TestMethod]
    public async Task PersistedJson_NeverContainsFullXuidIpOrPrivatePath()
    {
        using var temp = new TemporaryChatStorage();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        using var store = new JsonPlayerChatHistoryStore(temp.Path, clock);
        var unsafeMessage = Message(
            1,
            clock.UtcNow,
            "Player 8877665544332211",
            "xuid=1122334455667788 ip=192.0.2.15 path=C:\\private\\server rcon_password=TopSecret42",
            "zm_castle",
            "Der Eisendrache");

        await store.MergeAsync([unsafeMessage]);
        var json = await File.ReadAllTextAsync(temp.Path);

        Assert.IsFalse(json.Contains("8877665544332211", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("1122334455667788", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("192.0.2.15", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("C:\\private\\server", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("TopSecret42", StringComparison.Ordinal));
    }

    private static PlayerChatMessage Message(
        int id,
        DateTimeOffset occurredAtUtc,
        string name,
        string text,
        string mapCode,
        string mapLabel) =>
        new(id.ToString("x32"), occurredAtUtc, name, text, mapCode, mapLabel);

    private sealed class TemporaryChatStorage : IDisposable
    {
        public TemporaryChatStorage()
        {
            Directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "PinteMod.ControlCenter.Tests",
                Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            Path = System.IO.Path.Combine(Directory, "player-chat-history.json");
        }

        public string Directory { get; }

        public string Path { get; }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }
}
