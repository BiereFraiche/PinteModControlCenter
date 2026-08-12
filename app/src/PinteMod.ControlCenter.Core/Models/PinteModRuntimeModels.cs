namespace PinteMod.ControlCenter.Core.Models;

public enum RuntimePowerState
{
    On,
    Off,
    Unknown,
    NotApplicable
}

public enum RuntimePackAPunchState
{
    Available,
    Unavailable,
    Unknown,
    NotApplicable
}

public enum RuntimeGodModeState
{
    On,
    Off,
    Unknown
}

public enum RuntimeWeaponPackAPunchState
{
    Base,
    Upgraded,
    Unknown,
    NotApplicable
}

public sealed record PinteModHeartbeatSnapshot(
    int SchemaVersion,
    string ModuleVersion,
    string SessionId,
    ServiceDeclaredState DeclaredState,
    string? LastErrorCode,
    long Sequence,
    long GeneratedGetTime,
    DateTimeOffset? DeclaredUpdatedAtUtc,
    string TimeAuthority);

public sealed record RuntimeWeaponSnapshot(
    string Id,
    RuntimeWeaponPackAPunchState PackAPunchState,
    int? AmmoClip,
    int? AmmoReserve);

public sealed record RuntimePlayerSnapshot(
    string Xuid,
    string DisplayName,
    int ClientNumber,
    string Presence,
    PlayerLifeState LifeState,
    RuntimeGodModeState GodModeState,
    int? Points,
    int? Health,
    int? MaximumHealth,
    string EquippedWeapon,
    RuntimeWeaponPackAPunchState EquippedWeaponPackAPunchState,
    int? EquippedAmmoClip,
    int? EquippedAmmoReserve,
    IReadOnlyList<RuntimeWeaponSnapshot> Weapons,
    bool WeaponsTruncated,
    IReadOnlyList<string> Perks);

public sealed record ControlCenterRuntimeSnapshot(
    int SchemaVersion,
    string ModuleVersion,
    string SessionId,
    long Sequence,
    long GeneratedGetTime,
    DateTimeOffset? DeclaredUpdatedAtUtc,
    string TimeAuthority,
    string MapCode,
    int? Round,
    long? SessionStartedGetTime,
    TimeSpan? SessionElapsed,
    RankedStatus RankedStatus,
    RuntimePowerState PowerState,
    RuntimePackAPunchState PackAPunchState,
    int ConnectedPlayers,
    int? MaximumPlayers,
    int ObservablePlayers,
    int IdentityUnavailablePlayers,
    bool PlayersTruncated,
    IReadOnlyList<RuntimePlayerSnapshot> Players);
