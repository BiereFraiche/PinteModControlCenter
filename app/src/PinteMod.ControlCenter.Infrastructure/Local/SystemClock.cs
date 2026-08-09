using PinteMod.ControlCenter.Core.Contracts;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
