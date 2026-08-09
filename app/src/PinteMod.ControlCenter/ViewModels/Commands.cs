using System.Windows.Input;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class RelayCommand<T>(Action<T> execute, Predicate<T>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        parameter is T value && (canExecute?.Invoke(value) ?? true);

    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            execute(value);
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand(
    Func<Task> execute,
    Func<bool>? canExecute = null,
    Action<Exception>? onException = null) : ICommand
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public Exception? LastException { get; private set; }

    public bool IsExecuting => _isExecuting;

    public bool CanExecute(object? parameter) => !_isExecuting && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        LastException = null;
        NotifyCanExecuteChanged();
        try
        {
            await execute();
        }
        catch (Exception exception)
        {
            LastException = exception;
            onException?.Invoke(exception);
        }
        finally
        {
            _isExecuting = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand<T>(
    Func<T, Task> execute,
    Predicate<T>? canExecute = null,
    Action<Exception>? onException = null) : ICommand
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public Exception? LastException { get; private set; }

    public bool IsExecuting => _isExecuting;

    public bool CanExecute(object? parameter) =>
        !_isExecuting && parameter is T value && (canExecute?.Invoke(value) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter) || parameter is not T value)
        {
            return;
        }

        _isExecuting = true;
        LastException = null;
        NotifyCanExecuteChanged();
        try
        {
            await execute(value);
        }
        catch (Exception exception)
        {
            LastException = exception;
            onException?.Invoke(exception);
        }
        finally
        {
            _isExecuting = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
