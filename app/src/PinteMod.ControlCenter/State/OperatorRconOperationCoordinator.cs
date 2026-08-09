namespace PinteMod.ControlCenter.State;

public interface IOperatorRconOperationCoordinator
{
    Task RunExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    void StopAcceptingNewOperations();

    Task WaitForIdleAsync(CancellationToken cancellationToken = default);
}

public sealed class OperatorRconOperationCoordinator : IOperatorRconOperationCoordinator
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TaskCompletionSource<object?> _idle = CompletedSignal();
    private int _pendingOperations;
    private bool _accepting = true;

    public async Task RunExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        RegisterOperation();
        var entered = false;
        try
        {
            await _gate.WaitAsync(cancellationToken);
            entered = true;
            await operation(cancellationToken);
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }

            CompleteOperation();
        }
    }

    public void StopAcceptingNewOperations()
    {
        lock (_sync)
        {
            _accepting = false;
        }
    }

    public Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        Task idleTask;
        lock (_sync)
        {
            idleTask = _idle.Task;
        }

        return cancellationToken.CanBeCanceled
            ? idleTask.WaitAsync(cancellationToken)
            : idleTask;
    }

    private void RegisterOperation()
    {
        lock (_sync)
        {
            if (!_accepting)
            {
                throw new InvalidOperationException("La fermeture est en cours ; aucune nouvelle opération RCON n’est acceptée.");
            }

            if (_pendingOperations++ == 0)
            {
                _idle = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private void CompleteOperation()
    {
        lock (_sync)
        {
            _pendingOperations--;
            if (_pendingOperations == 0)
            {
                _idle.TrySetResult(null);
            }
        }
    }

    private static TaskCompletionSource<object?> CompletedSignal()
    {
        var signal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult(null);
        return signal;
    }
}
