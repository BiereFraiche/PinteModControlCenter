using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class ServerStorageAnalyzer
{
    public Task<ServerStorageSummary> AnalyzeAsync(
        string serverRoot,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(serverRoot, cancellationToken), cancellationToken);

    public ServerStorageSummary Analyze(
        string serverRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverRoot) || !Directory.Exists(serverRoot))
        {
            return new ServerStorageSummary(0, 0, 0, 0, 0, false);
        }

        var root = Path.GetFullPath(serverRoot.Trim());
        var scriptData = Path.Combine(root, "boiii", "scriptdata");
        if (!Directory.Exists(scriptData))
        {
            return new ServerStorageSummary(0, 0, 0, 0, 0, false);
        }

        long total = 0;
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(scriptData, "*", SearchOption.AllDirectories).Take(100_000))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                total += new FileInfo(path).Length;
                count++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        var statsRoot = Path.Combine(scriptData, "pintemod", "localization", "stats");
        return new ServerStorageSummary(
            total,
            count,
            Length(Path.Combine(statsRoot, "countries.json")),
            EnumerateLength(statsRoot, "countries.json.tmp*"),
            Length(Path.Combine(statsRoot, "countries_summary.txt")),
            NeedsGeoIpHardening(root));
    }


    private static bool NeedsGeoIpHardening(string root)
    {
        var path = Path.Combine(root, "boiii", "tools", "PinteMod_GeoIP_Bridge.ps1");
        try
        {
            if (!File.Exists(path)) return false;
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 256 * 1024) return true;
            var text = File.ReadAllText(path);
            return !text.Contains("geoip-stats-fix1", StringComparison.OrdinalIgnoreCase) ||
                   !text.Contains("maxEntries = 300", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static long Length(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return 0; }
    }

    private static long EnumerateLength(string root, string pattern)
    {
        if (!Directory.Exists(root)) return 0;
        long total = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly))
            {
                try { total += new FileInfo(path).Length; }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return total;
    }
}
