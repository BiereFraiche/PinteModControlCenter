using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.State;

public sealed class MapCatalogState
{
    public MapCatalogSnapshot Current { get; private set; } = MapCatalogSnapshot.OfficialOnly;

    public event EventHandler? Changed;

    public void Update(MapCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Current = snapshot;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
