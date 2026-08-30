using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IBoiiiRconBootstrapService
{
    Task<BoiiiRconBootstrapResult> InitializeAsync(
        string serverRoot,
        string secret,
        CancellationToken cancellationToken = default);
}
