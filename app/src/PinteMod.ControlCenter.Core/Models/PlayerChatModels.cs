namespace PinteMod.ControlCenter.Core.Models;

public static class PlayerChatHistoryPolicy
{
    public const int MaximumMessages = 2000;
}

public sealed record PlayerChatMessage(
    string EventId,
    DateTimeOffset OccurredAtUtc,
    string DisplayName,
    string Message,
    string MapCode,
    string MapLabel);

public sealed record PlayerChatReadResult(
    IReadOnlyList<PlayerChatMessage> Messages,
    LocalSourceMetadata Source,
    int LinesIgnored,
    int MalformedLines)
{
    public static PlayerChatReadResult Empty(LocalSourceMetadata source) =>
        new([], source, 0, 0);
}
