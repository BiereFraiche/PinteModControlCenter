namespace PinteMod.ControlCenter.Core.Models;

public sealed record PlayerModerationHistory(
    int Kicks,
    int Mutes,
    int TemporaryBans,
    int PermanentBans,
    int Unbans,
    string LastAction,
    string LastReason);
