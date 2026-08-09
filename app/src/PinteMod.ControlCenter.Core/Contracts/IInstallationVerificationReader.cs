using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IInstallationVerificationReader
{
    Task<LocalReadResult<InstallationVerificationReport>> ReadAsync(CancellationToken cancellationToken = default);
}
