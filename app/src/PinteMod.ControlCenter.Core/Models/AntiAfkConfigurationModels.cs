namespace PinteMod.ControlCenter.Core.Models;

public sealed record AntiAfkConfiguration(bool Enabled, int TimeoutSeconds, int WarningSeconds)
{
    public static AntiAfkConfiguration Default { get; } = new(true, 600, 120);
}

public sealed record AntiAfkConfigurationLoadResult(bool Supported, AntiAfkConfiguration Configuration, string Message);

public sealed record AntiAfkConfigurationSaveResult(bool Success, string Message);
