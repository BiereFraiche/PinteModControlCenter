using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RconEndpointValidatorTests
{
    [DataTestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("10.20.30.40")]
    [DataRow("172.16.0.1")]
    [DataRow("172.31.255.254")]
    [DataRow("192.168.50.25")]
    [DataRow("169.254.10.20")]
    [DataRow("::1")]
    [DataRow("fe80::1234")]
    [DataRow("fd12:3456:789a::1")]
    public void LocalOrPrivateLiteralAddress_IsAllowed(string address)
    {
        Assert.IsTrue(RconEndpointValidator.IsAllowed(Endpoint(address)));
    }

    [DataTestMethod]
    [DataRow("8.8.8.8")]
    [DataRow("1.1.1.1")]
    [DataRow("172.32.0.1")]
    [DataRow("203.0.113.10")]
    [DataRow("2001:4860:4860::8888")]
    [DataRow("portable")]
    [DataRow("server.example.test")]
    [DataRow("0.0.0.0")]
    [DataRow("::")]
    public void PublicUnspecifiedOrHostnameTarget_IsRejected(string address)
    {
        Assert.IsFalse(RconEndpointValidator.IsAllowed(Endpoint(address)));
    }

    private static RconEndpoint Endpoint(string address) =>
        new(address, 27018, TimeSpan.FromSeconds(3));
}
