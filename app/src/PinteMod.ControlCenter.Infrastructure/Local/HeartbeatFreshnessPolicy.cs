using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public static class HeartbeatFreshnessPolicy
{
    public static readonly TimeSpan FreshMaximumAge = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan StaleMaximumAge = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan FutureTimestampTolerance = TimeSpan.FromSeconds(5);

    public static DataFreshness Evaluate(TimeSpan age) =>
        age <= FreshMaximumAge
            ? DataFreshness.Fresh
            : age <= StaleMaximumAge
                ? DataFreshness.Stale
                : DataFreshness.Expired;
}
