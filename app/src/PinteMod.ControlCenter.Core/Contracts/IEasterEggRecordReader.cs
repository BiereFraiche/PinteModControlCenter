using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IEasterEggRecordReader
{
    Task<LocalReadResult<EasterEggRecordCatalog>> ReadAsync(CancellationToken cancellationToken = default);
}
