using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Configuration;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ApplicationStartupOptionsTests
{
    [TestMethod]
    public void SavedOperatorSource_IsUsedOnlyWithoutExplicitDataArguments()
    {
        var saved = OperatorConfiguration.Default with
        {
            ActivateDataSourceOnStartup = true,
            ServerRoot = "C:\\Server\\UnrankedServer"
        };

        var fromSaved = ApplicationStartupOptions.Resolve([], saved);
        var explicitSimulation = ApplicationStartupOptions.Resolve(["--data-mode=simulation"], saved);
        var explicitHybrid = ApplicationStartupOptions.Resolve(
            ["--data-mode=hybrid-local", "--server-root=D:\\OtherServer"],
            saved);

        Assert.AreEqual(ControlCenterDataMode.HybridLocal, fromSaved.DataMode);
        Assert.AreEqual(saved.ServerRoot, fromSaved.ServerRoot);
        Assert.AreEqual(ControlCenterDataMode.Simulation, explicitSimulation.DataMode);
        Assert.AreEqual("D:\\OtherServer", explicitHybrid.ServerRoot);
    }

    [TestMethod]
    public void NoArguments_SelectsSimulationWithoutServerRoot()
    {
        var options = ApplicationStartupOptions.Parse([]);

        Assert.AreEqual(ControlCenterDataMode.Simulation, options.DataMode);
        Assert.IsNull(options.ServerRoot);
    }

    [TestMethod]
    public void HybridMode_RequiresExplicitServerRoot()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            ApplicationStartupOptions.Parse(["--data-mode=hybrid-local"]));
    }

    [TestMethod]
    public void ServerRootWithoutHybridMode_IsRejected()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            ApplicationStartupOptions.Parse(["--server-root=C:\\Server"]));
    }

    [TestMethod]
    public void ExplicitHybridPair_IsAcceptedWithoutDiscovery()
    {
        var options = ApplicationStartupOptions.Parse(
            ["--data-mode=hybrid-local", "--server-root=C:\\Server"]);

        Assert.AreEqual(ControlCenterDataMode.HybridLocal, options.DataMode);
        Assert.AreEqual("C:\\Server", options.ServerRoot);
    }
}
