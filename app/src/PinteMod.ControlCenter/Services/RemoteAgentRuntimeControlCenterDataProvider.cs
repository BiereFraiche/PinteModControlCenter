using System.Security.Cryptography;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Infrastructure.Local;
using PinteMod.ControlCenter.Security;

namespace PinteMod.ControlCenter.Services;

internal sealed class RemoteAgentRuntimeControlCenterDataProvider(
    IControlCenterDataProvider inner,
    string serverRoot,
    string profileId,
    string remoteAgentId) : IControlCenterDataProvider
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await inner.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(remoteAgentId) || !serverRoot.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return snapshot;
        }

        var secretStore = new DpapiRemoteAgentSecretStore(
            OperatorProfileStoragePaths.GetRemoteAgentSecretPath(profileId));
        var secret = await secretStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (secret is null) return snapshot;

        try
        {
            var runtime = await RemoteAgentProtocolService.ReadJsonBoundedAsync<RemoteAgentServerRuntimeEnvelope>(
                RemoteAgentProtocolService.GetServerRuntimePath(serverRoot), cancellationToken).ConfigureAwait(false);
            if (runtime is null ||
                runtime.SchemaVersion != RemoteAgentProtocol.SchemaVersion ||
                !string.Equals(runtime.AgentId, remoteAgentId, StringComparison.Ordinal) ||
                runtime.UpdatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(2) ||
                DateTimeOffset.UtcNow - runtime.UpdatedAtUtc > TimeSpan.FromSeconds(15) ||
                !RemoteAgentProtocolService.VerifyServerRuntime(runtime, secret))
            {
                return snapshot;
            }

            var metadata = new LocalSourceMetadata(
                LocalReadStatus.Success,
                DataFreshness.Fresh,
                TimeSpan.Zero,
                DataProvenance.LocalFile,
                "Agent SMB",
                "État processus BOIII distant authentifié par l’Agent SMB.");
            var server = snapshot.Server with
            {
                ServerRunning = runtime.ServerRunning,
                ServerRunningAvailable = true,
                ObservedServerHealth = runtime.ServerRunning ? ServiceHealth.Healthy : ServiceHealth.Offline,
                UpdatedAtUtc = runtime.UpdatedAtUtc,
                RuntimeSource = metadata
            };
            return snapshot with { Server = server };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }
}
