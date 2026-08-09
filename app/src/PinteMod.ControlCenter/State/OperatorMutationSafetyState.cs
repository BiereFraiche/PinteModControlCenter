namespace PinteMod.ControlCenter.State;

public enum OperatorMutationScope
{
    CommunityPause,
    ServerAdministration,
    PlayerAdministration
}

public sealed class OperatorMutationSafetyState
{
    private readonly object _sync = new();
    private readonly HashSet<OperatorMutationScope> _blockedScopes = [];

    public event EventHandler? Changed;

    public bool IsAnyBlocked
    {
        get
        {
            lock (_sync)
            {
                return _blockedScopes.Count > 0;
            }
        }
    }

    public bool IsBlockedByOtherThan(OperatorMutationScope scope)
    {
        lock (_sync)
        {
            return _blockedScopes.Any(item => item != scope);
        }
    }

    public bool IsBlocked(OperatorMutationScope scope)
    {
        lock (_sync)
        {
            return _blockedScopes.Contains(scope);
        }
    }

    public void Block(OperatorMutationScope scope)
    {
        bool changed;
        lock (_sync)
        {
            changed = _blockedScopes.Add(scope);
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear(OperatorMutationScope scope)
    {
        bool changed;
        lock (_sync)
        {
            changed = _blockedScopes.Remove(scope);
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
