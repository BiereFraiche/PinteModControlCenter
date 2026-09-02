using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IBoiiiRconBootstrapService
{
    // Returns only whether a configuration directive exists. It never returns
    // or exposes the configured secret.
    Task<bool> HasConfiguredRconAsync(
        string serverRoot,
        CancellationToken cancellationToken = default);

    Task<BoiiiRconBootstrapResult> InitializeAsync(
        string serverRoot,
        string secret,
        CancellationToken cancellationToken = default);

    // Replaces a configured directive with an operator-supplied new secret.
    // The previous secret is never returned, logged, or displayed.
    Task<BoiiiRconBootstrapResult> ReplaceAsync(
        string serverRoot,
        string secret,
        CancellationToken cancellationToken = default);

    // Updates only the BOIII net_port directive in the server cfg declared
    // by Server.bat. It never reads, returns or writes an RCON secret.
    Task<BoiiiRconBootstrapResult> UpdateServerPortAsync(
        string serverRoot,
        int port,
        CancellationToken cancellationToken = default);
}
