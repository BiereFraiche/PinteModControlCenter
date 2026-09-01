using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ControlCenterSelfTestServiceTests
{
    [TestMethod]
    public async Task RunAsync_ValidatesProductAndEmbeddedPayloadsWithoutPrivateData()
    {
        var service = new ControlCenterSelfTestService(() => new ControlCenterSelfTestCheck(
            "Interface WPF",
            true,
            "Les six pages sont disponibles."));

        var report = await service.RunAsync();
        var text = report.ToDisplayText();

        Assert.IsTrue(report.Success, text);
        Assert.AreEqual("2.4.5-rc13", report.ProductVersion);
        StringAssert.Contains(text, "RESULTAT=PASS");
        StringAssert.Contains(text, "Payloads embarqués");
        StringAssert.Contains(text, "aucun réseau");
        Assert.IsFalse(Regex.IsMatch(text, @"(?i)[A-Z]:\\"));
        Assert.IsFalse(text.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(text.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Report_FailureRemainsExplicitAndContainsNoExceptionDetails()
    {
        var report = ControlCenterSelfTestReport.CreateStartupFailure();
        var text = report.ToDisplayText();

        Assert.IsFalse(report.Success);
        StringAssert.Contains(text, "RESULTAT=FAIL");
        StringAssert.Contains(text, "aucun secret lu");
    }
}
