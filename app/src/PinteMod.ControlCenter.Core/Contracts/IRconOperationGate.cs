namespace PinteMod.ControlCenter.Core.Contracts;

public interface IRconOperationGate
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
