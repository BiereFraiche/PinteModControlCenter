namespace PinteMod.ControlCenter.ViewModels;

internal static class DurationDisplay
{
    public static string Format(TimeSpan duration)
    {
        var safeDuration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        if (safeDuration.TotalHours >= 1)
        {
            return $"{(long)safeDuration.TotalHours:D2}:{safeDuration.Minutes:D2}:{safeDuration.Seconds:D2}";
        }

        return $"{safeDuration.Minutes:D2}:{safeDuration.Seconds:D2}";
    }
}
