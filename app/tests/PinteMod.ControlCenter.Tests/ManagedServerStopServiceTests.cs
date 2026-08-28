using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ManagedServerStopServiceTests
{
    [TestMethod]
    public async Task Stop_RefusesUncRootBeforeStartingAnyProcess()
    {
        var result = await new ManagedServerStopService().StopAsync(
            "srv-test",
            @"\\server\share\Server3",
            27021);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Message, "locale");
    }

    [TestMethod]
    public void StopSuccessToken_IsRenderedWithFrenchAccentsInManagedCode()
    {
        var message = ManagedServerStopService.GetSuccessMessage("STOPPED");

        Assert.AreEqual(
            "Serveur BOIII arrêté. Les services PinteMod de ce profil ont également été arrêtés.",
            message);
    }

    [TestMethod]
    public async Task Stop_RefusesInvalidPortBeforeStartingAnyProcess()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.StopTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "boiii"));
        try
        {
            var result = await new ManagedServerStopService().StopAsync("srv-test", root, 0);
            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Message, "Port BOIII invalide");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
