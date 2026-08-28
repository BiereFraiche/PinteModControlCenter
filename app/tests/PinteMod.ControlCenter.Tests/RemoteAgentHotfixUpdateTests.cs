using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RemoteAgentHotfixUpdateTests
{
    [TestMethod]
    public void SameVersion_ButDifferentHash_IsNotSameAgentBuild()
    {
        Assert.IsFalse(RemoteLaunchAgentHost.IsSameAgentBuild(
            "2.4.0-preview-onboarding.4a4",
            "2.4.0-preview-onboarding.4a4",
            new string('A', 64),
            new string('B', 64)));
    }

    [TestMethod]
    public void SameVersion_AndSameHash_IsSameAgentBuild()
    {
        Assert.IsTrue(RemoteLaunchAgentHost.IsSameAgentBuild(
            "2.4.0-preview-onboarding.4a4",
            "2.4.0-preview-onboarding.4a4",
            new string('A', 64),
            new string('a', 64)));
    }

    [TestMethod]
    public void DifferentVersion_IsNeverSameAgentBuild()
    {
        Assert.IsFalse(RemoteLaunchAgentHost.IsSameAgentBuild(
            "2.4.0-preview-onboarding.4a4.fix4",
            "2.4.0-preview-onboarding.4a4",
            new string('A', 64),
            new string('A', 64)));
    }
}
