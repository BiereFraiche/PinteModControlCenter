using PinteMod.ControlCenter.Core.Contracts;

namespace PinteMod.ControlCenter.Infrastructure.Rcon;

public sealed class RconOperationGate : IRconOperationGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public static RconOperationGate Shared { get; } = new();

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
