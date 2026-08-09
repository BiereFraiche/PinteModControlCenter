namespace PinteMod.ControlCenter.ViewModels;

public abstract class PageViewModel(string title, string description) : ObservableObject
{
    private string? _errorMessage;
    private string _description = description;

    public string Title { get; } = title;

    public string Description
    {
        get => _description;
        protected set => SetProperty(ref _description, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    protected void ClearError() => ErrorMessage = null;

    protected void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ErrorMessage = "Une opération locale a échoué. Les données affichées peuvent être incomplètes.";
    }

    public abstract Task InitializeAsync(CancellationToken cancellationToken = default);
}
