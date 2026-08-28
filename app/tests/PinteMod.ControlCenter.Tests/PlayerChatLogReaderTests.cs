using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PlayerChatLogReaderTests
{
    private const string Session = "zm_castle_s17_84500000";
    private const string Xuid = "a1b2c3d4e5f60718";

    [TestMethod]
    public void RealLiveConsoleChatLine_ParsesOnlyDisplayNameAndMessage()
    {
        const string line = "[23:42:40][CHAT] [84525700 ms][chat] PlayerOne [a1b2c3d4e5f60718]: SALUT";

        var parsed = PlayerChatLogReader.TryParseLine(line, out var chat);

        Assert.IsTrue(parsed);
        Assert.AreEqual(84525700L, chat.GetTime);
        Assert.AreEqual("PlayerOne", chat.DisplayName);
        Assert.AreEqual("SALUT", chat.Message);
        Assert.IsFalse(typeof(PlayerChatMessage).GetProperties()
            .Any(property => property.Name.Contains("xuid", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void JoinAndCommandLines_AreNotChatMessages()
    {
        const string join = "[23:41:35][JOIN] [84459900 ms][round 6][JOIN] PlayerOne | xuid=a1b2c3d4e5f60718 | client=2 | players=3";
        const string command = "[23:33:25][COMMAND] DENIED actor=PlayerTwo actor_xuid=1122334455667788 command=perks";

        Assert.IsFalse(PlayerChatLogReader.TryParseLine(join, out _));
        Assert.IsFalse(PlayerChatLogReader.TryParseLine(command, out _));
    }

    [TestMethod]
    public async Task DedicatedChatLog_DiscardsSourceXuidAndNeutralizesSensitiveText()
    {
        using var root = new TemporaryServerRoot();
        var timestamp = new DateTimeOffset(2026, 8, 18, 21, 42, 47, TimeSpan.Zero);
        var path = root.WriteSessionChatLog(
            Session,
            $"[84525700 ms][chat] PlayerOne [{Xuid}]: SALUT\n" +
            "[84532050 ms][chat] PlayerTwo [1122334455667788]: ip 192.0.2.15 path=C:\\private\\server\n");
        File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
        using var reader = new PlayerChatLogReader(root.Options, new FakeClock(timestamp));

        var result = await reader.ReadAsync(Session, "zm_castle");

        Assert.AreEqual(LocalReadStatus.Success, result.Source.ReadStatus);
        Assert.AreEqual(2, result.Messages.Count);
        Assert.AreEqual("Der Eisendrache", result.Messages[0].MapLabel);
        Assert.IsFalse(result.Messages.Any(message =>
            message.DisplayName.Contains(Xuid, StringComparison.OrdinalIgnoreCase) ||
            message.Message.Contains(Xuid, StringComparison.OrdinalIgnoreCase) ||
            message.Message.Contains("192.0.2.15", StringComparison.Ordinal) ||
            message.Message.Contains("C:\\private", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.Messages[1].Message.Contains("[adresse masquée]", StringComparison.Ordinal));
        Assert.IsTrue(result.Messages[1].Message.Contains("[chemin masqué]", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AppendedChatLine_IsReadOnNextIncrementalRefresh()
    {
        using var root = new TemporaryServerRoot();
        var timestamp = new DateTimeOffset(2026, 8, 18, 21, 42, 40, TimeSpan.Zero);
        var path = root.WriteSessionChatLog(
            Session,
            $"[84525700 ms][chat] PlayerOne [{Xuid}]: SALUT\n");
        File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
        var clock = new FakeClock(timestamp);
        using var reader = new PlayerChatLogReader(root.Options, clock);

        var first = await reader.ReadAsync(Session, "zm_castle");
        File.AppendAllText(
            path,
            "[84532050 ms][chat] PlayerTwo [1122334455667788]: hi\n",
            new System.Text.UTF8Encoding(false));
        timestamp = timestamp.AddSeconds(7);
        clock.UtcNow = timestamp;
        File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
        var second = await reader.ReadAsync(Session, "zm_castle");

        Assert.AreEqual(1, first.Messages.Count);
        Assert.AreEqual(1, second.Messages.Count);
        Assert.AreEqual("PlayerTwo", second.Messages[0].DisplayName);
        Assert.AreEqual("hi", second.Messages[0].Message);
    }

    [TestMethod]
    public async Task SameLine_IsReturnedOnlyOnceDuringIncrementalReads()
    {
        using var root = new TemporaryServerRoot();
        var timestamp = new DateTimeOffset(2026, 8, 18, 21, 42, 40, TimeSpan.Zero);
        var path = root.WriteSessionChatLog(
            Session,
            $"[84525700 ms][chat] PlayerOne [{Xuid}]: SALUT\n");
        File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
        using var reader = new PlayerChatLogReader(root.Options, new FakeClock(timestamp));

        var first = await reader.ReadAsync(Session, "zm_castle");
        var second = await reader.ReadAsync(Session, "zm_castle");

        Assert.AreEqual(1, first.Messages.Count);
        Assert.AreEqual(0, second.Messages.Count);
    }
}
