using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RemoteAgentRecoveryTaskServiceTests
{
    [TestMethod]
    public void UpdateMarker_FreshMarkerSuppressesRecoveryButStaleMarkerDoesNot()
    {
        var now = DateTimeOffset.Parse("2026-08-21T13:00:00Z");

        Assert.IsTrue(RemoteAgentRecoveryTaskService.IsFreshUpdateMarker(now, now.AddSeconds(-45)));
        Assert.IsFalse(RemoteAgentRecoveryTaskService.IsFreshUpdateMarker(now, now.AddMinutes(-10)));
        Assert.IsFalse(RemoteAgentRecoveryTaskService.IsFreshUpdateMarker(now, now.AddSeconds(1)));
    }
}
