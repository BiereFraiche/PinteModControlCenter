using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.State;

public sealed class PlayerSelectionState
{
    private string? _selectedXuid;

    public event EventHandler? SelectionChanged;

    public string? SelectedXuid => _selectedXuid;

    public void Select(string? xuid)
    {
        if (xuid is not null && !XuidValidator.IsValid(xuid))
        {
            throw new ArgumentException("Le XUID de sélection est invalide.", nameof(xuid));
        }

        if (string.Equals(_selectedXuid, xuid, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedXuid = xuid;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
