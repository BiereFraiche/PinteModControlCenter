using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IOperatorConfirmationService
{
    Task<bool> ConfirmAsync(
        OperatorConfirmationRequest request,
        CancellationToken cancellationToken = default);
}
