using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class StartupFailureDiagnosticTests
{
    [TestMethod]
    public void Describe_KeepsUsefulWpfMessageButMasksPrivateValues()
    {
        var exception = new InvalidOperationException(
            "Cannot show C:\\Users\\operator\\ControlCenter.exe; rcon=do-not-show; secret: hidden.");

        var diagnostic = StartupFailureDiagnostic.Describe(exception);

        StringAssert.Contains(diagnostic, "InvalidOperationException");
        StringAssert.Contains(diagnostic, "Cannot show");
        Assert.IsFalse(diagnostic.Contains("C:\\Users", StringComparison.Ordinal));
        Assert.IsFalse(diagnostic.Contains("do-not-show", StringComparison.Ordinal));
        Assert.IsFalse(diagnostic.Contains("hidden", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Describe_UsesMostSpecificInnerException()
    {
        var exception = new Exception("outer", new InvalidOperationException("Window is already closed."));

        var diagnostic = StartupFailureDiagnostic.Describe(exception);

        StringAssert.Contains(diagnostic, "InvalidOperationException : Window is already closed.");
    }
}
