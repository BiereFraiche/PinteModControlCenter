using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Configuration;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class GraphicsCompatibilityStartupOptionsTests
{
    [TestMethod]
    public void IsRequested_AcceptsSoftwareRenderingFlagWithoutCaseSensitivity()
    {
        Assert.IsTrue(GraphicsCompatibilityStartupOptions.IsRequested(["--SOFTWARE-RENDERING"]));
    }

    [TestMethod]
    public void IsRequested_RejectsOtherArguments()
    {
        Assert.IsFalse(GraphicsCompatibilityStartupOptions.IsRequested(["--self-test"]));
    }
}
