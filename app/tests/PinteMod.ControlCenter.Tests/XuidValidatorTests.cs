using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class XuidValidatorTests
{
    [TestMethod]
    public void IsValid_AcceptsSixteenHexadecimalCharacters()
    {
        Assert.IsTrue(XuidValidator.IsValid("0000000000000001"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("PlayerName")]
    [DataRow("111111111111111")]
    [DataRow("111111111111111g")]
    public void IsValid_RejectsUnsafeSelectors(string? value)
    {
        Assert.IsFalse(XuidValidator.IsValid(value));
    }

    [TestMethod]
    public void Abbreviate_HidesTheMiddleOfTheXuid()
    {
        Assert.AreEqual("0000…0001", XuidValidator.Abbreviate("0000000000000001"));
    }
}
