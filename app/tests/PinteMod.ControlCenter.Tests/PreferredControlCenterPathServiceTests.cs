using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PreferredControlCenterPathServiceTests
{
    [TestMethod]
    public void EligibleUserExecutable_AcceptsExistingLocalExeOutsideInternalHomes()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.PreferredUiTests", Guid.NewGuid().ToString("N"));
        var executable = Path.Combine(root, "PinteMod.ControlCenter.exe");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(executable, [0x4D, 0x5A]);
            Assert.IsTrue(PreferredControlCenterPathService.IsEligibleUserExecutable(executable));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EligibleUserExecutable_RejectsMissingOrNonExePaths()
    {
        Assert.IsFalse(PreferredControlCenterPathService.IsEligibleUserExecutable(string.Empty));
        Assert.IsFalse(PreferredControlCenterPathService.IsEligibleUserExecutable(Path.Combine(Path.GetTempPath(), "missing.txt")));
    }
}
