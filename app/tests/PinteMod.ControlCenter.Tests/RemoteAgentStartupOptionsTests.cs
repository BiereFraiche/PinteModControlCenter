using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Configuration;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RemoteAgentStartupOptionsTests
{
    [TestMethod]
    public void IsAgentRequested_RecognizesTheFixedAgentArgument()
    {
        Assert.IsTrue(RemoteAgentStartupOptions.IsAgentRequested(["--REMOTE-AGENT"]));
        Assert.IsFalse(RemoteAgentStartupOptions.IsAgentRequested(["--agent-manual-repair"]));
    }

    [TestMethod]
    public void IsManualRepairRequested_RequiresTheDedicatedLocalRepairArgument()
    {
        Assert.IsTrue(RemoteAgentStartupOptions.IsManualRepairRequested(["--agent-manual-repair"]));
        Assert.IsFalse(RemoteAgentStartupOptions.IsManualRepairRequested(["--remote-agent"]));
    }
}
