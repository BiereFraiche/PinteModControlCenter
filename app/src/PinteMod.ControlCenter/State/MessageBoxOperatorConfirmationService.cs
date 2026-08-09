using System.Windows;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.State;

public sealed class MessageBoxOperatorConfirmationService : IOperatorConfirmationService
{
    public Task<bool> ConfirmAsync(
        OperatorConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var result = MessageBox.Show(
            Application.Current?.MainWindow,
            request.Message,
            request.Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
