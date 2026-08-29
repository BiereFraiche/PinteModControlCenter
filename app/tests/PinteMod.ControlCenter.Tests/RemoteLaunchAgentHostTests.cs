using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RemoteLaunchAgentHostTests
{
    [TestMethod]
    public void ShouldHonorStopRequest_RejectsRequestCreatedBeforeAgentStarted()
    {
        using var file = new TemporaryTextFile(DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"));

        Assert.IsFalse(RemoteLaunchAgentHost.ShouldHonorStopRequest(file.Path, DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void ShouldHonorStopRequest_AcceptsRequestCreatedAfterAgentStarted()
    {
        var started = DateTimeOffset.UtcNow;
        using var file = new TemporaryTextFile(started.AddSeconds(1).ToString("O"));

        Assert.IsTrue(RemoteLaunchAgentHost.ShouldHonorStopRequest(file.Path, started));
    }

    private sealed class TemporaryTextFile : IDisposable
    {
        public TemporaryTextFile(string content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PinteMod.AgentTests.{Guid.NewGuid():N}.txt");
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
    }
}
