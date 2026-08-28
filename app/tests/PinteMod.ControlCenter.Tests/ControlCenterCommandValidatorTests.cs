using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ControlCenterCommandValidatorTests
{
    [DataTestMethod]
    [DataRow("^7[^4FR^7] ^1PinteMod")]
    [DataRow("Serveur ^9Orange")]
    [DataRow("PinteMod Test [EU]")]
    [DataRow("^7PinteMod | ^4DE")]
    public void Hostname_ClosedTextAndColorCodesAreAccepted(string value)
    {
        Assert.IsTrue(ControlCenterCommandValidator.IsValidHostname(value));
    }

    [DataTestMethod]
    [DataRow("Serveur ^")]
    [DataRow("Serveur ^x")]
    [DataRow("Serveur;quit")]
    [DataRow(" Serveur")]
    public void Hostname_InvalidColorOrInjectionIsRejected(string value)
    {
        Assert.IsFalse(ControlCenterCommandValidator.IsValidHostname(value));
    }

    [DataTestMethod]
    [DataRow("Safe#2026")]
    [DataRow("abcd")]
    [DataRow("A_B-C.D!E@F#G$H%I+J")]
    public void JoinPassword_ClosedAlphabetIsAccepted(string value)
    {
        Assert.IsTrue(ControlCenterCommandValidator.IsValidJoinPassword(value));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("bad value")]
    [DataRow("bad;quit")]
    [DataRow("bad\nquit")]
    [DataRow("123456789012345678901234567890123")]
    public void JoinPassword_OutsideClosedContractIsRejected(string value)
    {
        Assert.IsFalse(ControlCenterCommandValidator.IsValidJoinPassword(value));
    }

    [DataTestMethod]
    [DataRow("0123456789abcdef0123456789abcdef")]
    [DataRow("ffffffffffffffffffffffffffffffff")]
    public void MapRequestId_ExactLowerHex32IsAccepted(string value)
    {
        Assert.IsTrue(ControlCenterCommandValidator.IsValidMapRequestId(value));
    }

    [DataTestMethod]
    [DataRow("request_map1")]
    [DataRow("0123456789ABCDEF0123456789ABCDEF")]
    [DataRow("0123456789abcdef0123456789abcde")]
    [DataRow("0123456789abcdef0123456789abcdeg")]
    public void MapRequestId_AnythingElseIsRejected(string value)
    {
        Assert.IsFalse(ControlCenterCommandValidator.IsValidMapRequestId(value));
    }
}
