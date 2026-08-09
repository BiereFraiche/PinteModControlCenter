namespace PinteMod.ControlCenter.Core.Models;

public enum RconDiagnosticCommand
{
    HealthFull,
    PauseStatus,
    MapInfo,
    PowerStatus,
    PackAPunchStatus,
    RoundStatus,
    Players,
    MapAudit,
    EventStatus,
    PowerUpCatalog
}

public enum RconExecutionStatus
{
    Success,
    SecretMissing,
    InvalidConfiguration,
    Timeout,
    EmptyResponse,
    UnexpectedResponse,
    TransportError
}

public sealed record RconEndpoint(
    string Address,
    int Port,
    TimeSpan Timeout);

public sealed record RconExecutionResult(
    RconDiagnosticCommand Command,
    RconExecutionStatus Status,
    string DisplayResponse,
    bool CommandSent,
    DateTimeOffset CompletedAtUtc);
