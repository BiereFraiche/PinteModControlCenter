using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public static class OperatorProfileStoragePaths
{
    public static string GetConfigurationPath(string profileId) =>
        Path.Combine(GetProfileFolder(profileId), "operator-settings.json");

    public static string GetRconSecretPath(string profileId) =>
        Path.Combine(GetProfileFolder(profileId), "rcon.secret.dpapi");

    public static string GetMapCatalogPath(string profileId) =>
        Path.Combine(GetProfileFolder(profileId), "map-catalog.json");

    public static string GetPlayerChatHistoryPath(string profileId) =>
        Path.Combine(GetProfileFolder(profileId), "player-chat-history.json");

    public static string GetManagedServerProfilePath(string profileId) =>
        Path.Combine(GetProfileFolder(profileId), "managed-server.json");

    public static string GetRemoteAgentSecretPath(string profileId) =>
        Path.Combine(GetProfileFolder(profileId), "remote-agent.secret.dpapi");

    private static string GetProfileFolder(string profileId)
    {
        if (!JsonOperatorWorkspaceConfigurationStore.IsValidProfileId(profileId))
        {
            throw new ArgumentException("Identifiant de profil serveur invalide.", nameof(profileId));
        }

        var baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PinteMod",
            "ControlCenter");
        return string.Equals(
            profileId,
            OperatorWorkspaceConfiguration.PrimaryProfileId,
            StringComparison.Ordinal)
            ? baseFolder
            : Path.Combine(baseFolder, "profiles", profileId);
    }
}
