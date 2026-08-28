using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class LocalServerLaunchServiceTests
{
    [TestMethod]
    public async Task UncRoot_IsNeverLaunchedFromRemoteOperatorPc()
    {
        var result = await new LocalServerLaunchService().LaunchAsync(
            "\\\\server-pc\\BOIII\\Server1",
            "Server.bat");

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Message, "Lancement distant refusé");
    }
}
