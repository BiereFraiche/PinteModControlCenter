using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ManagedServerRuntimeProbeTests
{
    [TestMethod]
    public void IsRunning_FreshSupervisorHeartbeat_ReturnsTrue()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.RuntimeProbeTests", Guid.NewGuid().ToString("N"));
        var health = Path.Combine(root, "boiii", "scriptdata", "pintemod", "health");
        Directory.CreateDirectory(health);
        try
        {
            File.WriteAllText(Path.Combine(health, "supervisor.json"), "{\"state\":\"monitoring\"}");
            var probe = new ManagedServerRuntimeProbe();
            Assert.IsTrue(probe.IsRunning(root, 0));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void IsRunning_StoppedHeartbeat_ReturnsFalse()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.RuntimeProbeTests", Guid.NewGuid().ToString("N"));
        var health = Path.Combine(root, "boiii", "scriptdata", "pintemod", "health");
        Directory.CreateDirectory(health);
        try
        {
            File.WriteAllText(Path.Combine(health, "supervisor.json"), "{\"state\":\"stopped\"}");
            var probe = new ManagedServerRuntimeProbe();
            Assert.IsFalse(probe.IsRunning(root, 0));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void IsRunning_UnrelatedUdpListener_DoesNotMarkDifferentServerRootAsRunning()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.RuntimeProbeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "boiii"));
        try
        {
            using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
            var probe = new ManagedServerRuntimeProbe();

            Assert.IsFalse(probe.IsRunning(root, port));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
