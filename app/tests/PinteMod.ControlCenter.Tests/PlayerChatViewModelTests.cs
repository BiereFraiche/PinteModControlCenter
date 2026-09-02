using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PlayerChatViewModelTests
{
    [TestMethod]
    public async Task SessionAndMapChanges_KeepHistoryAndAddOnlyMapSeparators()
    {
        using var temp = new TemporaryHistory();
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero));
        using var history = new JsonPlayerChatHistoryStore(temp.Path, clock);
        var store = new MutableSnapshotStore(HybridSnapshot("session-1", "zm_castle"));
        var reader = new QueueChatReader(
            Result(Message(1, clock.UtcNow, "PlayerOne", "ready", "zm_castle", "Der Eisendrache")),
            Result(Message(2, clock.UtcNow.AddSeconds(5), "Islam", "go", "zm_castle", "Der Eisendrache")),
            Result(Message(3, clock.UtcNow.AddSeconds(10), "Player", "hello", "zm_stalingrad", "Gorod Krovi")));
        var viewModel = new PlayerChatViewModel(store, history, reader);

        await viewModel.InitializeAsync();
        store.Set(HybridSnapshot("session-2", "zm_castle"));
        await viewModel.InitializeAsync();
        store.Set(HybridSnapshot("session-3", "zm_stalingrad"));
        await viewModel.InitializeAsync();

        Assert.AreEqual(3, viewModel.Messages.Count);
        Assert.IsTrue(viewModel.Messages[0].ShowMapSeparator);
        Assert.IsFalse(viewModel.Messages[1].ShowMapSeparator, "Une nouvelle session sur la même carte ne doit pas effacer l’historique.");
        Assert.IsTrue(viewModel.Messages[2].ShowMapSeparator);
        Assert.AreEqual("Gorod Krovi", viewModel.Messages[2].MapLabel);
        CollectionAssert.AreEqual(
            new[] { "session-1", "session-2", "session-3" },
            reader.SessionIds.ToArray());
    }


    [TestMethod]
    public async Task DashboardRecentMessages_ContainsOnlyTheLastEightChronologicalMessages()
    {
        using var temp = new TemporaryHistory();
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero));
        using var history = new JsonPlayerChatHistoryStore(temp.Path, clock);
        await history.MergeAsync(
            Enumerable.Range(1, 10)
                .Select(index => Message(index, clock.UtcNow.AddSeconds(index), $"P{index}", $"M{index}", "zm_castle", "Der Eisendrache"))
                .ToArray());
        var viewModel = new PlayerChatViewModel(
            new MutableSnapshotStore(HybridSnapshot("session-1", "zm_castle")),
            history);

        await viewModel.InitializeAsync();

        Assert.AreEqual(8, viewModel.RecentMessages.Count);
        Assert.AreEqual("P3", viewModel.RecentMessages[0].DisplayName);
        Assert.AreEqual("P10", viewModel.RecentMessages[^1].DisplayName);
    }

    [TestMethod]
    public async Task ConnectionEvents_AppearInPersistentPlayerActivityWithoutChatMessages()
    {
        using var temp = new TemporaryHistory();
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero));
        using var history = new JsonPlayerChatHistoryStore(temp.Path, clock);
        var snapshot = HybridSnapshot("session-1", "zm_castle",
        [
            ConnectionEvent("Joueur connecté", "Joueur : Alice · Client : 1", TimeSpan.FromMinutes(2)),
            ConnectionEvent("Joueur déconnecté", "Joueur : Alice · Client : 1", TimeSpan.FromMinutes(4))
        ]);
        var viewModel = new PlayerChatViewModel(new MutableSnapshotStore(snapshot), history);

        await viewModel.InitializeAsync();

        Assert.AreEqual(2, viewModel.Messages.Count);
        Assert.AreEqual("Alice", viewModel.Messages[0].DisplayName);
        Assert.AreEqual("a rejoint le serveur.", viewModel.Messages[0].Message);
        Assert.AreEqual("a quitté le serveur.", viewModel.Messages[1].Message);
        Assert.AreEqual(2, viewModel.RecentMessages.Count);
    }

    [TestMethod]
    public async Task ClearChatCommand_DoesNotModifyServerChatLog()
    {
        using var root = new TemporaryServerRoot();
        using var temp = new TemporaryHistory();
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero));
        const string session = "zm_castle_s17_84500000";
        var serverLog = root.WriteSessionChatLog(
            session,
            "[84525700 ms][chat] PlayerOne [a1b2c3d4e5f60718]: SALUT\n");
        File.SetLastWriteTimeUtc(serverLog, clock.UtcNow.UtcDateTime);
        var before = TemporaryServerRoot.Fingerprint(serverLog);
        using var reader = new PlayerChatLogReader(root.Options, clock);
        using var history = new JsonPlayerChatHistoryStore(temp.Path, clock);
        var store = new MutableSnapshotStore(HybridSnapshot(session, "zm_castle"));
        var viewModel = new PlayerChatViewModel(store, history, reader);
        await viewModel.InitializeAsync();
        Assert.AreEqual(1, viewModel.Messages.Count);

        viewModel.ClearChatCommand.Execute(null);
        for (var attempt = 0; attempt < 100 && viewModel.ClearChatCommand.IsExecuting; attempt++)
        {
            await Task.Delay(10);
        }

        var after = TemporaryServerRoot.Fingerprint(serverLog);
        Assert.AreEqual(0, viewModel.Messages.Count);
        Assert.AreEqual(before, after);
    }

    private static PlayerChatReadResult Result(PlayerChatMessage message) =>
        new(
            [message],
            new LocalSourceMetadata(
                LocalReadStatus.Success,
                DataFreshness.Fresh,
                TimeSpan.Zero,
                DataProvenance.LocalFile,
                "chat/session.log",
                "ok"),
            0,
            0);

    private static PlayerChatMessage Message(
        int id,
        DateTimeOffset occurredAtUtc,
        string name,
        string text,
        string mapCode,
        string mapLabel) =>
        new(id.ToString("x32"), occurredAtUtc, name, text, mapCode, mapLabel);

    private static DashboardSnapshot HybridSnapshot(
        string sessionId,
        string mapCode,
        IReadOnlyList<LiveEvent>? events = null)
    {
        var now = new DateTimeOffset(2026, 8, 18, 22, 0, 0, TimeSpan.Zero);
        var server = new ServerState(
            "2.1.1",
            true,
            mapCode,
            OfficialMapCatalog.ResolveName(mapCode),
            6,
            2,
            4,
            RankedStatus.Ranked,
            TimeSpan.FromMinutes(10),
            now)
        {
            SessionId = sessionId,
            MapProvenance = DataProvenance.LocalFile,
            SessionProvenance = DataProvenance.LocalFile
        };
        return new DashboardSnapshot(server, [], [], events ?? [], [])
        {
            DataContext = new SnapshotDataContext(
                ControlCenterDataMode.HybridLocal,
                "MODE HYBRIDE LOCAL",
                null,
                new LocalSourceMetadata(
                    LocalReadStatus.Success,
                    DataFreshness.Fresh,
                    TimeSpan.Zero,
                    DataProvenance.LocalFile,
                    "current_session.json",
                    "ok"),
                [])
        };
    }

    private static LiveEvent ConnectionEvent(string title, string details, TimeSpan elapsed) =>
        new(DateTimeOffset.UnixEpoch, "JOUEURS", title, details, EventSeverity.Information)
        {
            SessionElapsed = elapsed,
            Provenance = DataProvenance.LocalFile,
            SourceLabel = "connections.log"
        };

    private sealed class QueueChatReader(params PlayerChatReadResult[] results) : IPlayerChatLogReader
    {
        private readonly Queue<PlayerChatReadResult> _results = new(results);

        public List<string> SessionIds { get; } = [];

        public Task<PlayerChatReadResult> ReadAsync(
            string? sessionId,
            string? mapCode,
            CancellationToken cancellationToken = default)
        {
            SessionIds.Add(sessionId ?? string.Empty);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class MutableSnapshotStore(DashboardSnapshot snapshot) : IControlCenterSnapshotStore
    {
        public DashboardSnapshot? Current { get; private set; } = snapshot;

        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public Task<DashboardSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current!);

        public void Set(DashboardSnapshot snapshot) => Current = snapshot;
    }

    private sealed class TemporaryHistory : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PinteMod.ControlCenter.Tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryHistory()
        {
            System.IO.Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "player-chat-history.json");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(_directory))
            {
                System.IO.Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
