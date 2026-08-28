namespace PinteMod.ControlCenter.Core.Models;

public enum IntegrationCapabilityKey
{
    ServerLifecycle,
    ServerInformation,
    MapAndRound,
    Players,
    Chat,
    ServerCommands,
    PlayerCommands,
    PublicIdentity,
    Ranks,
    Records,
    BossesAndEvents
}

public enum IntegrationCapabilityAvailability
{
    Unavailable,
    Observed,
    Available
}

public enum IntegrationCommandTransport
{
    None,
    PinteModClosedRconV1,
    GenericBridgeClosedV1
}

public sealed record IntegrationCapability(
    IntegrationCapabilityKey Key,
    IntegrationCapabilityAvailability Availability,
    string Evidence,
    string Source)
{
    public bool IsAvailable => Availability == IntegrationCapabilityAvailability.Available;
}

public sealed record ThirdPartyGscAudit(
    int FilesScanned,
    IReadOnlyList<string> DeclaredCommands,
    IReadOnlyList<string> ObservedFamilies,
    bool UsesScriptData,
    bool ObservesPlayers,
    bool ObservesChat,
    string Summary)
{
    public static ThirdPartyGscAudit Empty { get; } = new(0, [], [], false, false, false, "Aucun audit GSC tiers disponible.");
}

public sealed record ServerIntegrationProfile(
    ManagedServerIntegrationKind Kind,
    string ProviderLabel,
    IntegrationCommandTransport CommandTransport,
    IReadOnlyList<IntegrationCapability> Capabilities,
    ThirdPartyGscAudit ThirdPartyAudit)
{
    public static ServerIntegrationProfile Unknown { get; } = new(
        ManagedServerIntegrationKind.Unknown,
        "Aucun provider",
        IntegrationCommandTransport.None,
        [],
        ThirdPartyGscAudit.Empty);

    public IntegrationCapabilityAvailability Get(IntegrationCapabilityKey key) =>
        Capabilities.FirstOrDefault(capability => capability.Key == key)?.Availability
        ?? IntegrationCapabilityAvailability.Unavailable;

    public bool Supports(IntegrationCapabilityKey key) =>
        Get(key) == IntegrationCapabilityAvailability.Available;

    public bool Observes(IntegrationCapabilityKey key) =>
        Get(key) != IntegrationCapabilityAvailability.Unavailable;

    public bool SupportsPinteModClosedCommands =>
        CommandTransport == IntegrationCommandTransport.PinteModClosedRconV1;
}
