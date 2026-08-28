using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ManagerPayloadPrivacyTests
{
    [TestMethod]
    public void EmbeddedPayloads_DoNotContainRuntimeSecretsLocalConfigOrScriptData()
    {
        var assembly = typeof(EmbeddedServerPayloadService).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".zip", StringComparison.Ordinal))
            .Where(name => name.Contains(".Payloads.", StringComparison.Ordinal))
            .ToArray();

        Assert.IsTrue(resources.Length >= 2);
        foreach (var resourceName in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            Assert.IsNotNull(stream, resourceName);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var path = entry.FullName.Replace('\\', '/').ToLowerInvariant();
                Assert.IsFalse(path.Contains("/scriptdata/"), $"{resourceName}: {entry.FullName}");
                Assert.IsFalse(path.StartsWith("scriptdata/"), $"{resourceName}: {entry.FullName}");
                Assert.IsFalse(path.EndsWith(".secret.txt"), $"{resourceName}: {entry.FullName}");
                Assert.IsFalse(path.EndsWith(".local.json"), $"{resourceName}: {entry.FullName}");
                Assert.IsFalse(path.Contains("/runtime/"), $"{resourceName}: {entry.FullName}");
                Assert.IsFalse(path.EndsWith(".log"), $"{resourceName}: {entry.FullName}");
            }
        }
    }
}
