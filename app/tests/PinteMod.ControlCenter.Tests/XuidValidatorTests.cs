using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class XuidValidatorTests
{
    [TestMethod]
    public void IsValid_AcceptsSixteenHexadecimalCharacters()
    {
        Assert.IsTrue(XuidValidator.IsValid("9cf34426f668fb8b"));
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
        Assert.AreEqual("9cf3…fb8b", XuidValidator.Abbreviate("9cf34426f668fb8b"));
    }
}
