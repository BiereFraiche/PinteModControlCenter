namespace PinteMod.ControlCenter.Core.Models;

public enum PlayerAdministrationAction
{
    Revive,
    Respawn,
    GrantPoints,
    RefillAmmo,
    ToggleGodMode,
    GiveWeapon,
    PackAPunchCurrentWeapon,
    GivePerk,
    RemovePerk,
    GiveAllPerks,
    GivePowerUp,
    TeleportToOwnAim,
    Mute,
    Unmute,
    Kick,
    Ban,
    Unban,
    SetRole,
    RemoveRole
}

public enum PlayerAdministrationExecutionStatus
{
    SentAwaitingManualVerification,
    InvalidRequest,
    SecretMissing,
    InvalidConfiguration,
    DeliveryUnknown,
    TransportError
}

public sealed record PlayerAdministrationRequest(
    PlayerAdministrationAction Action,
    string TargetXuid,
    int? PointsAmount = null,
    string? Option = null);

public sealed record PlayerAdministrationExecutionResult(
    PlayerAdministrationRequest Request,
    PlayerAdministrationExecutionStatus Status,
    string DisplayMessage,
    bool CommandSent,
    DateTimeOffset CompletedAtUtc);
