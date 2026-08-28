namespace PinteMod.ControlCenter.Core.Models;

public sealed record ServerStorageSummary(
    long ScriptDataBytes,
    int FileCount,
    long CountryJsonBytes,
    long CountryTempBytes,
    long CountrySummaryBytes,
    bool GeoIpBridgeNeedsHardening)
{
    public long GeoIpStatisticsBytes => CountryJsonBytes + CountryTempBytes + CountrySummaryBytes;

    public bool GeoIpStatisticsAnomalous =>
        CountryJsonBytes > 1L * 1024 * 1024 ||
        CountryTempBytes > 1L * 1024 * 1024 ||
        CountrySummaryBytes > 1L * 1024 * 1024;

    public string DisplaySummary =>
        $"scriptdata {FormatBytes(ScriptDataBytes)} · {FileCount} fichiers · stats pays {FormatBytes(GeoIpStatisticsBytes)}" +
        (GeoIpStatisticsAnomalous ? " · ANOMALIE GEOIP" : string.Empty) +
        (GeoIpBridgeNeedsHardening ? " · BRIDGE GEOIP À DURCIR" : string.Empty);

    private static string FormatBytes(long value)
    {
        if (value >= 1024L * 1024 * 1024) return $"{value / (1024d * 1024 * 1024):0.00} Go";
        if (value >= 1024L * 1024) return $"{value / (1024d * 1024):0.00} Mo";
        if (value >= 1024L) return $"{value / 1024d:0.00} Ko";
        return $"{value} o";
    }
}
