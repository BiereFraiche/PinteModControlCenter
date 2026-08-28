using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RemoteLaunchClientServiceTests
{
    [TestMethod]
    public async Task Pairing_RefusesLocalPath()
    {
        var result = await new RemoteLaunchClientService().PairAsync(
            @"C:\Servers\Server3",
            "primary");

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Message, "UNC");
    }
}
