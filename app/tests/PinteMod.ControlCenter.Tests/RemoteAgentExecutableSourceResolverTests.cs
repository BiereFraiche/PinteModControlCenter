using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RemoteAgentExecutableSourceResolverTests
{
    [TestMethod]
    public void Resolve_UsesBundledStandaloneAgentWhenFolderPublishProvidesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.AgentSourceTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var controlCenter = Path.Combine(root, "PinteMod.ControlCenter.exe");
            var companion = Path.Combine(root, RemoteAgentExecutableSourceResolver.CompanionFileName);
            File.WriteAllBytes(controlCenter, [0x4D, 0x5A]);
            File.WriteAllBytes(companion, [0x4D, 0x5A]);

            Assert.AreEqual(companion, RemoteAgentExecutableSourceResolver.Resolve(controlCenter));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Resolve_UsesSingleExecutableWhenNoCompanionExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "PinteMod.AgentSourceTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var controlCenter = Path.Combine(root, "PinteMod.ControlCenter.exe");
            File.WriteAllBytes(controlCenter, [0x4D, 0x5A]);

            Assert.AreEqual(controlCenter, RemoteAgentExecutableSourceResolver.Resolve(controlCenter));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
