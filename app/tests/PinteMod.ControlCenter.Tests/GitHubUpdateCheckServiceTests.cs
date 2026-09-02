using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Services;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class GitHubUpdateCheckServiceTests
{
    [TestMethod]
    public void ExtractVersion_FindsControlCenterSemanticVersionInsideAssetName()
    {
        var version = GitHubUpdateCheckService.TryExtractVersion(
            "PinteMod-ControlCenter-v2.4.1-win-x64.zip");

        Assert.AreEqual("2.4.1", version);
    }

    [TestMethod]
    public void ExtractVersion_PreservesPreviewButDropsWindowsPackagingSuffix()
    {
        var version = GitHubUpdateCheckService.TryExtractVersion(
            "PinteMod-ControlCenter-v2.4.0-preview-onboarding.4a3-win-x64.zip");

        Assert.AreEqual("2.4.0-preview-onboarding.4a3", version);
    }

    [TestMethod]
    public void ReleaseVersion_StableBeatsPreviewWithSameCore()
    {
        Assert.IsTrue(GitHubUpdateCheckService.ReleaseVersion.TryParse("2.4.0", out var stable));
        Assert.IsTrue(GitHubUpdateCheckService.ReleaseVersion.TryParse("2.4.0-preview-onboarding.4a3", out var preview));

        Assert.IsTrue(stable.CompareTo(preview) > 0);
    }

    [TestMethod]
    public void ReleaseVersion_NewerPreviewSequenceWins()
    {
        Assert.IsTrue(GitHubUpdateCheckService.ReleaseVersion.TryParse("2.4.0-preview-onboarding.4a10", out var newer));
        Assert.IsTrue(GitHubUpdateCheckService.ReleaseVersion.TryParse("2.4.0-preview-onboarding.4a3", out var older));

        Assert.IsTrue(newer.CompareTo(older) > 0);
    }

    [TestMethod]
    public void CompareVersions_DetectsNewerRemotePreview()
    {
        var comparison = GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-onboarding.4a5",
            "2.4.0-preview-onboarding.4a4.fix4");

        Assert.IsTrue(comparison > 0);
    }

    [TestMethod]
    public void CompareVersions_Integration4B1BeatsOnboarding4A7Fix2()
    {
        var comparison = GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1",
            "2.4.0-preview-onboarding.4a7.fix2");

        Assert.IsTrue(comparison > 0);
    }

    [TestMethod]
    public void CompareVersions_UsesPreviewSequenceInsteadOfTrackLabel()
    {
        var comparison = GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1",
            "2.4.0-preview-onboarding.4b1");

        Assert.AreEqual(0, comparison);
    }

    [TestMethod]
    public void CompareVersions_NewerFixWinsWithinSamePreview()
    {
        var comparison = GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix3",
            "2.4.0-preview-integration.4b1.fix2");

        Assert.IsTrue(comparison > 0);
    }

    [TestMethod]
    public void OfficialRepository_IsPinned()
    {
        Assert.AreEqual("BiereFraiche/PinteModControlCenter", GitHubUpdateCheckService.Repository);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix4IsNewerThanFix3()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix4",
            "2.4.0-preview-integration.4b1.fix3") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix5IsNewerThanFix4()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix5",
            "2.4.0-preview-integration.4b1.fix4") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix6IsNewerThanFix5()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix6",
            "2.4.0-preview-integration.4b1.fix5") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix7IsNewerThanFix6()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix7",
            "2.4.0-preview-integration.4b1.fix6") > 0);
    }
    [TestMethod]
    public void CompareVersions_IntegrationFix8IsNewerThanFix7()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix8",
            "2.4.0-preview-integration.4b1.fix7") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix9IsNewerThanFix8()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix9",
            "2.4.0-preview-integration.4b1.fix8") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix10IsNewerThanFix9()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix10",
            "2.4.0-preview-integration.4b1.fix9") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix11IsNewerThanFix10()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix11",
            "2.4.0-preview-integration.4b1.fix10") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix12IsNewerThanFix11()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix12",
            "2.4.0-preview-integration.4b1.fix11") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix13IsNewerThanFix12()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix13",
            "2.4.0-preview-integration.4b1.fix12") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix14IsNewerThanFix13()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix14",
            "2.4.0-preview-integration.4b1.fix13") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix15IsNewerThanFix14()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix15",
            "2.4.0-preview-integration.4b1.fix14") > 0);
    }

    [TestMethod]
    public void CompareVersions_IntegrationFix16IsNewerThanFix15()
    {
        Assert.IsTrue(GitHubUpdateCheckService.CompareVersions(
            "2.4.0-preview-integration.4b1.fix16",
            "2.4.0-preview-integration.4b1.fix15") > 0);
    }

}
