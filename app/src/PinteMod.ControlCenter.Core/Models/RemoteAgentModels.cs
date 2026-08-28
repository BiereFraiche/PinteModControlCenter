namespace PinteMod.ControlCenter.Core.Models;

public static class RemoteAgentProtocol
{
    public const int SchemaVersion = 1;
    public const string LaunchAction = "launch";
    public const string StopAction = "stop";
    public const string QueueFolderName = ".pintemod-controlcenter";
    public const string AgentFolderName = "remote-agent";
    public const string PairingFileName = "pairing.json";
    public const string StatusFileName = "status.json";
    public const string RequestsFolderName = "requests";
    public const string ResponsesFolderName = "responses";
    public const string UpdatesFolderName = "updates";
    public const string UpdateManifestFileName = "update.json";
    public const string AvailablePackageManifestFileName = "available.json";
    public const string ProfileCatalogFileName = "profiles.json";
    public const string ServerRuntimeFileName = "runtime.json";
}

public sealed record RemoteAgentProfileRegistration(
    string AgentId,
    string LocalProfileId,
    string DisplayName,
    string ServerRoot,
    string LauncherRelativePath,
    int ServerPort,
    bool PinteModDetected,
    string ProtectedSecretBase64);

public sealed record RemoteAgentConfiguration(
    int SchemaVersion,
    IReadOnlyList<RemoteAgentProfileRegistration> Profiles);

public sealed record RemoteAgentPairingEnvelope(
    int SchemaVersion,
    string AgentId,
    string DisplayName,
    string MachineName,
    string SecretBase64,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record RemoteAgentStatusEnvelope(
    int SchemaVersion,
    string AgentId,
    string DisplayName,
    string MachineName,
    string State,
    DateTimeOffset UpdatedAtUtc,
    string AgentVersion,
    string Signature);

public sealed record RemoteAgentUpdateEnvelope(
    int SchemaVersion,
    string AgentId,
    string TargetVersion,
    string PackageFileName,
    string Sha256,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Signature);

public sealed record RemoteAgentAvailablePackageEnvelope(
    int SchemaVersion,
    string AgentId,
    string Version,
    string PackageFileName,
    string Sha256,
    DateTimeOffset UpdatedAtUtc,
    string Signature);


public sealed record RemoteAgentProfileCatalogEntry(
    string AgentId,
    string DisplayName,
    string RootFolderName,
    string LauncherRelativePath,
    int ServerPort,
    bool PinteModDetected);

public sealed record RemoteAgentProfileCatalogEnvelope(
    int SchemaVersion,
    string AuthorityAgentId,
    string MachineName,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<RemoteAgentProfileCatalogEntry> Profiles,
    string Signature);

public sealed record RemoteAgentServerRuntimeEnvelope(
    int SchemaVersion,
    string AgentId,
    DateTimeOffset UpdatedAtUtc,
    bool ServerRunning,
    string Signature);

public sealed record RemoteAgentProfileCatalogResult(
    bool Success,
    string Message,
    string MachineName,
    IReadOnlyList<RemoteAgentProfileCatalogEntry> Profiles)
{
    public static RemoteAgentProfileCatalogResult Unavailable(string message) => new(false, message, string.Empty, []);
}

public sealed record RemoteLaunchRequest(
    int SchemaVersion,
    string RequestId,
    string AgentId,
    string Action,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Nonce,
    string Signature);

public sealed record RemoteLaunchResponse(
    int SchemaVersion,
    string RequestId,
    string AgentId,
    string Status,
    string ResultCode,
    string Message,
    DateTimeOffset CompletedAtUtc,
    int? ProcessId,
    string Signature);

public sealed record RemoteAgentPairingResult(
    bool Success,
    string Message,
    string? AgentId = null);

public sealed record RemoteAgentProbeResult(
    bool AgentDetected,
    bool Paired,
    bool Online,
    string Message,
    DateTimeOffset? UpdatedAtUtc = null)
{
    public string AgentVersion { get; init; } = string.Empty;

    public string MachineName { get; init; } = string.Empty;
}
