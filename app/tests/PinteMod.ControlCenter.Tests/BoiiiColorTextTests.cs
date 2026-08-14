using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class BoiiiColorTextTests
{
    [TestMethod]
    public void Parse_ProducesOrderedSegmentsAndKeepsUnknownCaretLiteral()
    {
        var segments = BoiiiColorText.Parse("^1Rou^2ge^x");

        Assert.AreEqual(2, segments.Count);
        Assert.AreEqual(new BoiiiColorTextSegment('1', "Rou"), segments[0]);
        Assert.AreEqual(new BoiiiColorTextSegment('2', "ge^x"), segments[1]);
    }

    [TestMethod]
    public void ApplyColor_AtCaretInsertsTokenForFollowingCharacters()
    {
        var result = BoiiiColorText.ApplyColor("AB", 1, 0, '4', 64);

        Assert.IsTrue(result.Applied);
        Assert.AreEqual("A^4B", result.Text);
        Assert.AreEqual(3, result.SelectionStart);
        Assert.AreEqual(0, result.SelectionLength);
    }

    [TestMethod]
    public void ApplyColor_ToSelectionRestoresPreviousColorAfterSelection()
    {
        var result = BoiiiColorText.ApplyColor("^1ABCD", 3, 2, '4', 64);

        Assert.IsTrue(result.Applied);
        Assert.AreEqual("^1A^4BC^1D", result.Text);
        Assert.AreEqual(5, result.SelectionStart);
        Assert.AreEqual(2, result.SelectionLength);
    }

    [TestMethod]
    public void ApplyColor_RefusesEditThatWouldExceedRawContractLimit()
    {
        var original = new string('A', 64);
        var result = BoiiiColorText.ApplyColor(original, 64, 0, '6', 64);

        Assert.IsFalse(result.Applied);
        Assert.AreEqual(original, result.Text);
    }
}
