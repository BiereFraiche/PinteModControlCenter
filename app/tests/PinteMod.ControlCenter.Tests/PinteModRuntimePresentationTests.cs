using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class PinteModRuntimePresentationTests
{
    [TestMethod]
    public void RuntimePlayerPresentation_IsReadableAndNeverExposesCompleteXuid()
    {
        const string xuid = "0000000000000001";
        var runtime = new RuntimePlayerSnapshot(
            xuid,
            "Joueur fictif",
            0,
            "connected",
            PlayerLifeState.Alive,
            RuntimeGodModeState.On,
            12500,
            100,
            150,
            "ray_gun",
            RuntimeWeaponPackAPunchState.Upgraded,
            20,
            160,
            [new("ray_gun", RuntimeWeaponPackAPunchState.Upgraded, 20, 160)],
            false,
            ["jug", "speed"]);
        var player = new PlayerState(
            0, xuid, runtime.DisplayName, "user", "fr", "FR", runtime.LifeState,
            runtime.Points ?? 0, TimeSpan.Zero, false, false)
        {
            RuntimeDetails = runtime,
            ModerationStateAvailable = false,
            Provenance = DataProvenance.LocalFile
        };

        var viewModel = new PlayerItemViewModel(player);
        var publicStrings = viewModel.GetType()
            .GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(viewModel) as string)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        Assert.IsTrue(viewModel.RuntimeDataAvailable);
        Assert.AreEqual("100 / 150", viewModel.HealthText);
        StringAssert.Contains(viewModel.EquippedWeaponText, "PAP");
        Assert.AreEqual("20 / 160", viewModel.AmmunitionText);
        StringAssert.Contains(viewModel.PerksText, "JUG");
        Assert.AreEqual("INCONNU", viewModel.ModerationStatus);
        Assert.IsFalse(publicStrings.Any(value => value.Contains(xuid, StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual("0000…0001", viewModel.ShortXuid);
    }

    [TestMethod]
    public void DeadRuntimeState_UsesDangerSemantics()
    {
        var text = new PinteMod.ControlCenter.Converters.StatusTextConverter()
            .Convert(PlayerLifeState.Dead, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual("MORT", text);
        Assert.AreEqual("DangerBrush", PinteMod.ControlCenter.Converters.StatusBrushConverter.GetResourceKey(PlayerLifeState.Dead));
    }
}
