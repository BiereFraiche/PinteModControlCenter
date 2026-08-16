namespace PinteMod.ControlCenter.Core.Models;

public enum ControlCenterContractAction
{
    RestartMap,
    SpawnBoss,
    SetHostname,
    SetJoinPassword,
    ClearJoinPassword
}

public enum ControlCenterFeedbackStatus
{
    Accepted,
    Applied,
    Rejected,
    Failed
}

public enum ControlCenterTransitionStatus
{
    Accepted,
    Transitioning,
    Active,
    Failed
}

public enum PublicHostnameState
{
    Observed,
    Neutralized,
    Empty
}

public sealed record SupportedMapCapability(string Code, string DisplayName);

public sealed record ControlCenterCapabilitiesSnapshot(
    string ModuleVersion,
    string ContractModuleVersion,
    string SessionId,
    long Sequence,
    long GeneratedGetTime,
    string MapCode,
    bool RestartMap,
    IReadOnlyList<SupportedMapCapability> SupportedMaps,
    IReadOnlyList<string> BossAliases,
    IReadOnlyList<string> PowerUpAliases,
    IReadOnlyList<string> DiagnosticAliases,
    string TransitionState,
    bool SetHostname,
    bool SetJoinPassword,
    bool ClearJoinPassword,
    string MapProfile,
    string PowerSupport,
    string PackAPunchSupport,
    string EventSupport,
    string BossSupport,
    string MusicSupport,
    string DogRoundSupport,
    int ActivePinteModBosses,
    int MaximumPinteModBosses);

public sealed record ControlCenterActionFeedbackSnapshot(
    string SessionId,
    long Sequence,
    long GeneratedGetTime,
    string RequestId,
    ControlCenterContractAction Action,
    ControlCenterFeedbackStatus Status,
    string ResultCode);

public sealed record ControlCenterMapTransitionSnapshot(
    string RequestId,
    string RequestedMap,
    string OriginatingSessionId,
    ControlCenterTransitionStatus Status,
    string ResultCode,
    long GeneratedGetTime,
    string? ResultingSessionId);

public sealed record ControlCenterServerIdentitySnapshot(
    string SessionId,
    long Sequence,
    long GeneratedGetTime,
    string PublicHostname,
    PublicHostnameState PublicHostnameState,
    bool JoinPasswordEnabled,
    long Revision);

public sealed record ControlCenterContractSnapshot(
    LocalReadResult<ControlCenterCapabilitiesSnapshot> Capabilities,
    LocalReadResult<ControlCenterActionFeedbackSnapshot> ActionFeedback,
    LocalReadResult<ControlCenterMapTransitionSnapshot> MapTransition,
    LocalReadResult<ControlCenterServerIdentitySnapshot> ServerIdentity)
{
    public static ControlCenterContractSnapshot Unavailable { get; } = new(
        Empty<ControlCenterCapabilitiesSnapshot>("Capabilities Control Center non lues."),
        Empty<ControlCenterActionFeedbackSnapshot>("Feedback Control Center non lu."),
        Empty<ControlCenterMapTransitionSnapshot>("Transition Control Center non lue."),
        Empty<ControlCenterServerIdentitySnapshot>("Identité serveur Control Center non lue."));

    private static LocalReadResult<T> Empty<T>(string message) where T : class =>
        new(null, LocalSourceMetadata.Unavailable(message), null);
}
