using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface ILocalDataSourceProbe
{
    Task<LocalDataSourceProbeResult> ProbeAsync(
        LocalDataSourceProbeRequest request,
        CancellationToken cancellationToken = default);
}
