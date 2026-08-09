using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Rcon;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class BoiiiUdpRconClientTests
{
    [TestMethod]
    public async Task LoopbackPacket_MatchesBoiiiProtocolAndCleansPrintPrefix()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        var serverTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync(cancellation.Token);
            Assert.IsTrue(request.Buffer.Length > 4);
            CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, request.Buffer[..4]);
            Assert.AreEqual("rcon test-secret ezzhealth full", Encoding.UTF8.GetString(request.Buffer, 4, request.Buffer.Length - 4));

            var text = Encoding.UTF8.GetBytes("print\nHEALTH OK");
            var response = new byte[text.Length + 4];
            response.AsSpan(0, 4).Fill(0xFF);
            text.CopyTo(response, 4);
            await server.SendAsync(response, request.RemoteEndPoint, cancellation.Token);
        }, cancellation.Token);

        var result = await new BoiiiUdpRconClient().SendAsync(
            new RconEndpoint("127.0.0.1", serverEndpoint.Port, TimeSpan.FromSeconds(2)),
            "test-secret",
            "ezzhealth full",
            cancellation.Token);
        await serverTask;

        Assert.AreEqual("HEALTH OK", result);
    }

    [TestMethod]
    public async Task CommandContainingNewline_IsRejectedBeforeNetworkAccess()
    {
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => new BoiiiUdpRconClient().SendAsync(
            new RconEndpoint("127.0.0.1", 27017, TimeSpan.FromSeconds(1)),
            "test-secret",
            "ezzhealth full\nquit"));
    }

    [DataTestMethod]
    [DataRow("8.8.8.8")]
    [DataRow("portable")]
    public async Task PublicAddressOrHostname_IsRejectedBeforeNetworkAccess(string address)
    {
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => new BoiiiUdpRconClient().SendAsync(
            new RconEndpoint(address, 27017, TimeSpan.FromSeconds(1)),
            "test-secret",
            "ezzhealth full"));
    }
}
