using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IRconClient
{
    Task<string> SendAsync(
        RconEndpoint endpoint,
        string password,
        string command,
        CancellationToken cancellationToken = default);
}
