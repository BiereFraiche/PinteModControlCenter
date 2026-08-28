using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IPlayerChatHistoryStore
{
    Task<IReadOnlyList<PlayerChatMessage>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerChatMessage>> MergeAsync(
        IReadOnlyCollection<PlayerChatMessage> messages,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
