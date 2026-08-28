using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Configuration;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class SelfTestStartupOptionsTests
{
    [TestMethod]
    public void Parse_SelfTestWithoutPath_UsesLocalTemporaryReport()
    {
        var options = SelfTestStartupOptions.Parse(["--self-test"]);

        Assert.IsTrue(Path.IsPathFullyQualified(options.ReportPath));
        Assert.IsFalse(options.ReportPath.StartsWith(@"\\", StringComparison.Ordinal));
        Assert.AreEqual(".txt", Path.GetExtension(options.ReportPath));
    }

    [TestMethod]
    public void Parse_ExplicitLocalTextReport_IsAccepted()
    {
        var path = Path.Combine(Path.GetTempPath(), "pintemod-ci.self-test.txt");

        var options = SelfTestStartupOptions.Parse(
            ["--self-test", $"--self-test-report={path}"]);

        Assert.AreEqual(Path.GetFullPath(path), options.ReportPath);
    }

    [DataTestMethod]
    [DataRow("rapport.txt")]
    [DataRow(@"\\server\share\rapport.txt")]
    [DataRow(@"C:\Temp\rapport.json")]
    public void Parse_UnsafeOrUnsupportedReportPath_IsRejected(string path)
    {
        Assert.ThrowsException<ArgumentException>(() => SelfTestStartupOptions.Parse(
            ["--self-test", $"--self-test-report={path}"]));
    }

    [TestMethod]
    public void Parse_DuplicateArguments_AreRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => SelfTestStartupOptions.Parse(
            ["--self-test", "--self-test"]));
    }
}
