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
    RestorePowerUpTimeout
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
    int? TargetRound = null);

public sealed record ServerAdministrationExecutionResult(
    ServerAdministrationRequest Request,
    ServerAdministrationExecutionStatus Status,
    string DisplayMessage,
    bool CommandSent,
    DateTimeOffset CompletedAtUtc);
