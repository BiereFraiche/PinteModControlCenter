namespace PinteMod.ControlCenter.Core.Contracts;

public interface IRconSecretStore
{
    Task<bool> HasSecretAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string secret, CancellationToken cancellationToken = default);

    Task<string?> ReadAsync(CancellationToken cancellationToken = default);
}
