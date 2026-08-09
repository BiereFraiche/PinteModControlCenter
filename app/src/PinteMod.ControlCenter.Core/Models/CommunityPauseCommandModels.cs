namespace PinteMod.ControlCenter.Core.Models;

public enum CommunityPauseAction
{
    Pause,
    Resume
}

public enum CommunityPauseExecutionStatus
{
    SentAwaitingObservation,
    SecretMissing,
    InvalidConfiguration,
    DeliveryUnknown,
    TransportError
}

public sealed record CommunityPauseExecutionResult(
    CommunityPauseAction Action,
    CommunityPauseExecutionStatus Status,
    string DisplayMessage,
    bool CommandSent,
    bool StatusRefreshRequested,
    DateTimeOffset CompletedAtUtc);

public sealed record OperatorConfirmationRequest(
    string Title,
    string Message);
