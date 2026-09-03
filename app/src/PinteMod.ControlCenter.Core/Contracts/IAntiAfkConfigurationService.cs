using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IAntiAfkConfigurationService
{
    Task<AntiAfkConfigurationLoadResult> LoadAsync(string serverRoot, CancellationToken cancellationToken = default);

    Task<AntiAfkConfigurationSaveResult> SaveAsync(
        string serverRoot,
        AntiAfkConfiguration configuration,
        CancellationToken cancellationToken = default);
}
