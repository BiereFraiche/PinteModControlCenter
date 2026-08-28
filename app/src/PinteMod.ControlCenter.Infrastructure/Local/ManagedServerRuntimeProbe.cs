using System.Diagnostics;
using System.Text.Json;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class ManagedServerRuntimeProbe
{
    private static readonly TimeSpan MaximumHeartbeatAge = TimeSpan.FromSeconds(20);

    public bool IsRunning(string? serverRoot, int serverPort)
    {
        var root = serverRoot?.Trim() ?? string.Empty;
        if (root.Length == 0) return false;

        // Heartbeats are authoritative when first-party runtime exists.
        if (HasFreshRunningHeartbeat(root))
        {
            return true;
        }

        // For a local native/third-party BOIII profile, bind the state to the
        // actual boiii.exe under this exact root. A UDP port alone is not enough:
        // another registered server can legitimately use the same configured
        // default port and must not disable this profile's Start button.
        return !root.StartsWith(@"\\", StringComparison.Ordinal) &&
               HasRunningBoiiiProcessUnderRoot(root);
    }

    internal static bool HasRunningBoiiiProcessUnderRoot(string serverRoot)
    {
        string root;
        try
        {
            root = Path.GetFullPath(serverRoot.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var rootPrefix = root + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcessesByName("boiii"))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited) continue;
                    var executable = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(executable)) continue;
                    var fullExecutable = Path.GetFullPath(executable);
                    if (fullExecutable.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    // Best effort only. If Windows hides process details, do not
                    // claim this profile is running merely because a shared UDP
                    // port happens to be occupied by another BOIII instance.
                }
            }
        }

        return false;
    }

    private static bool HasFreshRunningHeartbeat(string root)
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("boiii", "scriptdata", "pintemod", "health", "supervisor.json"),
                     Path.Combine("boiii", "scriptdata", "pintemod", "health", "pintemod.json")
                 })
        {
            var path = Path.Combine(root, relative);
            try
            {
                if (!File.Exists(path)) continue;
                var info = new FileInfo(path);
                if (info.Length is <= 0 or > 16 * 1024) continue;
                var age = DateTime.UtcNow - info.LastWriteTimeUtc;
                if (age < TimeSpan.FromSeconds(-5) || age > MaximumHeartbeatAge) continue;

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var document = JsonDocument.Parse(stream);
                var rootElement = document.RootElement;
                var state = TryGetString(rootElement, "state") ?? TryGetString(rootElement, "declared_state") ?? string.Empty;
                if (state.Trim().ToLowerInvariant() is "monitoring" or "running" or "connected" or "active" or "online")
                {
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }

        return false;
    }

    private static string? TryGetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
