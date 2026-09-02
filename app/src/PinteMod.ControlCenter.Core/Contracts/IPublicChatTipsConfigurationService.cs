using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IPublicChatTipsConfigurationService
{
    Task<PublicChatTipsLoadResult> LoadAsync(
        string serverRoot,
        CancellationToken cancellationToken = default);

    Task<PublicChatTipsSaveResult> SaveAsync(
        string serverRoot,
        PublicChatTipsConfiguration configuration,
        CancellationToken cancellationToken = default);
}
