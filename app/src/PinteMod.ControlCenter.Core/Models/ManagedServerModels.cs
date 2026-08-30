namespace PinteMod.ControlCenter.Core.Models;

public enum ManagedServerIntegrationKind
{
    Unknown,
    BoiiiNative,
    ControlCenterBridge,
    ThirdPartyScripts,
    PinteMod
}

public sealed record ManagedServerProfileConfiguration(
    int SchemaVersion,
    string LauncherRelativePath)
{
    public string RemoteAgentId { get; init; } = string.Empty;
    public const int CurrentSchemaVersion = 1;

    public static ManagedServerProfileConfiguration Default { get; } = new(
        CurrentSchemaVersion,
        string.Empty);
}

public sealed record ManagedServerAnalysis(
    bool RootExists,
    bool BoiiiRootDetected,
    bool PinteModDetected,
    bool ControlCenterRuntimeDetected,
    bool ControlCenterBridgeDetected,
    bool GenericBridgeDetected,
    bool IsUnc,
    int GscFileCount,
    IReadOnlyList<string> LauncherCandidates,
    ManagedServerIntegrationKind IntegrationKind,
    string Summary)
{
    public bool ThirdPartyGscDetected { get; init; }

    public int ThirdPartyGscCount { get; init; }

    public IReadOnlyList<string> ThirdPartyGscNames { get; init; } = [];

    // BOIII exposes its UDP/RCON endpoint through the launcher on the usual
    // dedicated-server layouts. This is advisory data gathered from a local
    // file; no port is probed and no command is sent while analysing.
    public int? DetectedServerPort { get; init; }

    public string DetectedServerPortLauncher { get; init; } = string.Empty;

    public ServerIntegrationProfile IntegrationProfile { get; init; } = ServerIntegrationProfile.Unknown;

    public bool CanDeployFirstPartyFiles => RootExists && BoiiiRootDetected;

    public bool CanLaunchLocally =>
        RootExists && BoiiiRootDetected && !IsUnc && LauncherCandidates.Count > 0;
}

public sealed record ServerDeploymentResult(
    bool Success,
    string Message,
    IReadOnlyList<string> CreatedFiles,
    IReadOnlyList<string> SkippedFiles);

public sealed record ServerLaunchResult(
    bool Success,
    string Message,
    int? ProcessId = null,
    bool ApplicationRestartRequired = false);
