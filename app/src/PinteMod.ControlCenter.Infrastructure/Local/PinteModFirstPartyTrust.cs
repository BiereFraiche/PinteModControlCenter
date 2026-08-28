using System.Security.Cryptography;

namespace PinteMod.ControlCenter.Infrastructure.Local;

/// <summary>
/// Recognizes only reviewed first-party scripts. A familiar filename alone is never
/// sufficient to unlock PinteMod capabilities or a command transport.
/// </summary>
internal static class PinteModFirstPartyTrust
{
    private const long MaximumScriptBytes = 4 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, HashSet<string>> KnownScriptHashes =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ezz_admin_01_main.gsc"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "137E5274F5C248EBCDDA9924DAA69D1B1781D7FBC76DC753C207139E7745D338"
            },
            ["ezz_admin_storage.gsc"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "FA2012C5C870D23947D40A013850A5F5B49C48C4BB261FC2B15999E5E6079359"
            },
            ["ezz_admin_control_center_runtime.gsc"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "2C8934C10BD13044B94B58BB609379F952AB5D83C1D2A489E9DAEE037B5C11CE"
            },
            ["ezz_admin_control_center_contracts.gsc"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "2A409FAEC19475DCF9D0A39AC15B633216C4348865D6FF5CBA5E886CA14FC44C", // v0.2.0
                "6BE54261266F9230165FCC8C3142FECCA0E172D6914AD8DBFF49387E80B9271F", // v0.3.0
                "C9F16C1DD7D644D1F1251FAA4D9E3CC2A352A865DF81FFADF40FC7B60F5DCE9C"  // v0.3.1
            }
        };

    internal static bool IsTrustedCoreInstallation(string customScripts) =>
        IsTrustedScript(Path.Combine(customScripts, "ezz_admin_01_main.gsc"), "ezz_admin_01_main.gsc") &&
        IsTrustedScript(Path.Combine(customScripts, "ezz_admin_storage.gsc"), "ezz_admin_storage.gsc");

    internal static bool IsTrustedRuntime(string path) =>
        IsTrustedScript(path, "ezz_admin_control_center_runtime.gsc");

    internal static bool IsTrustedBridge(string path) =>
        IsTrustedScript(path, "ezz_admin_control_center_contracts.gsc");

    internal static bool IsKnownBridgeHash(string hash) =>
        KnownScriptHashes["ezz_admin_control_center_contracts.gsc"].Contains(hash);

    internal static bool IsTrustedScript(string path, string expectedName)
    {
        if (!KnownScriptHashes.TryGetValue(expectedName, out var knownHashes) ||
            !string.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 or > MaximumScriptBytes)
            {
                return false;
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            return knownHashes.Contains(hash);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
