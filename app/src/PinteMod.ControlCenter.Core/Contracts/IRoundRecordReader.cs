using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IRoundRecordReader
{
    Task<LocalReadResult<RoundRecordCatalog>> ReadAsync(CancellationToken cancellationToken = default);
}
