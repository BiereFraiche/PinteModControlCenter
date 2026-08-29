namespace PinteMod.ControlCenter.Configuration;

internal static class RemoteAgentStartupOptions
{
    private const string AgentArgument = "--remote-agent";
    private const string ManualRepairArgument = "--agent-manual-repair";

    public static bool IsAgentRequested(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => string.Equals(argument, AgentArgument, StringComparison.OrdinalIgnoreCase));

    public static bool IsManualRepairRequested(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => string.Equals(argument, ManualRepairArgument, StringComparison.OrdinalIgnoreCase));
}
