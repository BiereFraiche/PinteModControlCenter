using System.ComponentModel;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class RemoteAgentActivationDiagnosticTests
{
    [TestMethod]
    public void Describe_CryptographicFailure_ExplainsWindowsSecureStorageWithoutExceptionText()
    {
        var message = RemoteAgentActivationDiagnostic.Describe(new CryptographicException("secret-value"));

        StringAssert.Contains(message, "DPAPI");
        Assert.IsFalse(message.Contains("secret-value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Describe_AccessAndWindowsFailures_ReturnActionableSafeMessages()
    {
        Assert.IsTrue(RemoteAgentActivationDiagnostic.Describe(new UnauthorizedAccessException()).Contains("dossier", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(RemoteAgentActivationDiagnostic.Describe(new Win32Exception()), "Windows");
    }

    [TestMethod]
    public void Describe_UnknownFailure_ExposesOnlyItsType()
    {
        var message = RemoteAgentActivationDiagnostic.Describe(new InvalidOperationException("C:\\private\\server"));

        StringAssert.Contains(message, "InvalidOperationException");
        Assert.IsFalse(message.Contains("C:\\private", StringComparison.Ordinal));
    }
}
