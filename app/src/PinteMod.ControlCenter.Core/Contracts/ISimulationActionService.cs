using PinteMod.ControlCenter.Core.Simulation;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface ISimulationActionService
{
    Task<SimulationResult> SimulateAsync(
        SimulationRequest request,
        CancellationToken cancellationToken = default);
}
