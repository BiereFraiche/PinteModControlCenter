using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IOperatorConfigurationStore
{
    Task<OperatorConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(OperatorConfiguration configuration, CancellationToken cancellationToken = default);
}
