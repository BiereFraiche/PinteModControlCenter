namespace PinteMod.ControlCenter.Core.Contracts;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
