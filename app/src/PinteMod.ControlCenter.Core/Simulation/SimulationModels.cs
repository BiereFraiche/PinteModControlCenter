namespace PinteMod.ControlCenter.Core.Simulation;

public enum SimulationAction
{
    RevivePlayer,
    RespawnPlayer,
    GrantPoints,
    RefillAmmo,
    GiveWeapon,
    PackAPunchCurrentWeapon,
    GivePerk,
    RemovePerk,
    GiveAllPerks,
    GivePowerUpPlayer,
    TeleportPlayer,
    ToggleGodmode,
    MutePlayer,
    UnmutePlayer,
    KickPlayer,
    BanPlayer,
    ChangeRole,
    RemoveRole,
    ViewHistory,
    ChangeMap,
    RestartMap,
    SetRound,
    TogglePower,
    EnablePackAPunch,
    PlayMusic,
    TriggerEvent,
    SpawnBoss,
    SpawnPowerUp,
    RunDiagnostics
}

public enum SimulationStatus
{
    Simulated,
    Rejected
}

public sealed record SimulationRequest(
    SimulationAction Action,
    string? TargetXuid = null,
    string? OptionKey = null);

public sealed record SimulationResult(
    SimulationStatus Status,
    string Message,
    SimulationAction Action,
    string? TargetXuid,
    string? OptionKey,
    bool CommandSent,
    DateTimeOffset CompletedAtUtc);
