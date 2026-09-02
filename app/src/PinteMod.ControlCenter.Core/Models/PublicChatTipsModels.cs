namespace PinteMod.ControlCenter.Core.Models;

/// <summary>
/// Public reminders displayed by PinteMod in the native BOIII chat. The game
/// controls how long a chat line remains visible; this configuration controls
/// only its content and scheduling.
/// </summary>
public sealed record PublicChatTipsConfiguration(
    bool Enabled,
    int FirstDelaySeconds,
    int MinimumDelaySeconds,
    int MaximumDelaySeconds,
    IReadOnlyList<string> Messages)
{
    public static PublicChatTipsConfiguration Default { get; } = new(
        true,
        120,
        240,
        420,
        [
            "Player trolling? Start a votekick from .menu.",
            "Choose the next map in the cycle from .menu > Community > Votes.",
            "Votes, rankings and map records are available in .menu."
        ]);
}

public sealed record PublicChatTipsLoadResult(
    bool Supported,
    bool UpgradeAvailable,
    PublicChatTipsConfiguration Configuration,
    string Message);

public sealed record PublicChatTipsSaveResult(bool Success, string Message);
