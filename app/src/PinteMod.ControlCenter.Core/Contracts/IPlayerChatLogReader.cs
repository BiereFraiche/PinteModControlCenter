using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IPlayerChatLogReader
{
    Task<PlayerChatReadResult> ReadAsync(
        string? sessionId,
        string? mapCode,
        CancellationToken cancellationToken = default);
}
