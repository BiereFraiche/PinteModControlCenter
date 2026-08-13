namespace PinteMod.ControlCenter.Core.Models;

public enum ServerAdministrationAction
{
    NextRound,
    SetRound,
    EnablePower,
    EnablePackAPunch,
    PlayMapMusic,
    StopMapMusic,
    UnlockStandardPassages,
    KeepLastZombie,
    KillAllZombies,
    MakePowerUpsPermanent,
    RestorePowerUpTimeout,
    RestartMap,
    SpawnBoss,
    SetHostname,
    ClearJoinPassword
}

public enum ServerAdministrationExecutionStatus
{
    SentAwaitingManualVerification,
    InvalidRequest,
    SecretMissing,
    InvalidConfiguration,
    DeliveryUnknown,
    TransportError
}

public sealed record ServerAdministrationRequest(
    ServerAdministrationAction Action,
    int? TargetRound = null,
    string? RequestId = null,
    string? Option = null,
    string? TargetXuid = null);

public sealed record ServerAdministrationExecutionResult(
    ServerAdministrationRequest Request,
    ServerAdministrationExecutionStatus Status,
    string DisplayMessage,
    bool CommandSent,
    DateTimeOffset CompletedAtUtc);
