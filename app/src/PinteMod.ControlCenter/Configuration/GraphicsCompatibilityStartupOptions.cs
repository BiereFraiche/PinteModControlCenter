namespace PinteMod.ControlCenter.Configuration;

internal static class GraphicsCompatibilityStartupOptions
{
    internal const string SoftwareRenderingArgument = "--software-rendering";

    internal static bool IsRequested(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => string.Equals(
            argument,
            SoftwareRenderingArgument,
            StringComparison.OrdinalIgnoreCase));
}
