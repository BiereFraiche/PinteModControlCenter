using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.Infrastructure.Simulation;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class SimulationActionServiceTests
{
    [TestMethod]
    public async Task PlayerAction_WithXuid_IsSimulatedWithoutCommand()
    {
        var service = new SimulationActionService();

        var result = await service.SimulateAsync(
            new SimulationRequest(SimulationAction.RefillAmmo, "1111111111111111"));

        Assert.AreEqual(SimulationStatus.Simulated, result.Status);
        Assert.IsFalse(result.CommandSent);
        StringAssert.Contains(result.Message, "Aucune commande envoyée");
    }

    [TestMethod]
    public async Task PlayerAction_WithDisplayNameInsteadOfXuid_IsRejected()
    {
        var service = new SimulationActionService();

        var result = await service.SimulateAsync(
            new SimulationRequest(SimulationAction.KickPlayer, "PlayerTwo"));

        Assert.AreEqual(SimulationStatus.Rejected, result.Status);
        Assert.IsFalse(result.CommandSent);
    }

    [TestMethod]
    public async Task ServerAction_DoesNotRequireAPlayerTarget()
    {
        var service = new SimulationActionService();

        var result = await service.SimulateAsync(
            new SimulationRequest(SimulationAction.RunDiagnostics));

        Assert.AreEqual(SimulationStatus.Simulated, result.Status);
        Assert.IsFalse(result.CommandSent);
    }

    [TestMethod]
    public async Task EveryWhitelistedAction_AlwaysReturnsCommandSentFalse()
    {
        var service = new SimulationActionService();
        var playerActions = new HashSet<SimulationAction>
        {
            SimulationAction.RevivePlayer,
            SimulationAction.RespawnPlayer,
            SimulationAction.GrantPoints,
            SimulationAction.RefillAmmo,
            SimulationAction.GiveWeapon,
            SimulationAction.PackAPunchCurrentWeapon,
            SimulationAction.GivePerk,
            SimulationAction.RemovePerk,
            SimulationAction.GiveAllPerks,
            SimulationAction.TeleportPlayer,
            SimulationAction.ToggleGodmode,
            SimulationAction.MutePlayer,
            SimulationAction.KickPlayer,
            SimulationAction.BanPlayer,
            SimulationAction.ChangeRole,
            SimulationAction.ViewHistory
        };

        foreach (var action in Enum.GetValues<SimulationAction>())
        {
            var request = new SimulationRequest(
                action,
                playerActions.Contains(action) ? "1111111111111111" : null,
                "option-test");
            var result = await service.SimulateAsync(request);

            Assert.IsFalse(result.CommandSent, $"{action} ne doit jamais envoyer de commande.");
        }
    }

    [TestMethod]
    public async Task UnknownAction_IsRejectedWithoutCommand()
    {
        var service = new SimulationActionService();

        var result = await service.SimulateAsync(
            new SimulationRequest((SimulationAction)int.MaxValue));

        Assert.AreEqual(SimulationStatus.Rejected, result.Status);
        Assert.IsFalse(result.CommandSent);
        StringAssert.Contains(result.Message, "liste blanche");
    }

    [TestMethod]
    public async Task OptionWithControlCharacters_IsRejectedWithoutCommand()
    {
        var service = new SimulationActionService();

        var result = await service.SimulateAsync(
            new SimulationRequest(SimulationAction.RunDiagnostics, OptionKey: "valeur\ninjectée"));

        Assert.AreEqual(SimulationStatus.Rejected, result.Status);
        Assert.IsFalse(result.CommandSent);
    }
}
