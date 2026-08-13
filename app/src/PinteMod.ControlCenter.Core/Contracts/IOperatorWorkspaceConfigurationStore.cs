using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IOperatorWorkspaceConfigurationStore
{
    Task<OperatorWorkspaceConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        OperatorWorkspaceConfiguration configuration,
        CancellationToken cancellationToken = default);
}
