namespace Cgm.Core.Model;

/// <summary>
/// Runtime story-flag state (Phase 7): bool and int flags in one map (bools are 0/1). Unset flags
/// read as false/0. Drives triggers, NPC visibility, and door locks; round-trips through the save.
/// </summary>
public sealed class FlagStore
{
    private readonly Dictionary<string, int> _flags = [];

    public bool GetBool(string flag) => _flags.TryGetValue(flag, out int v) && v != 0;
    public int GetInt(string flag) => _flags.GetValueOrDefault(flag, 0);

    public void SetBool(string flag, bool value) => _flags[flag] = value ? 1 : 0;
    public void SetInt(string flag, int value) => _flags[flag] = value;
    public void Increment(string flag, int by = 1) => _flags[flag] = GetInt(flag) + by;

    public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_flags);

    public void Load(IReadOnlyDictionary<string, int> flags)
    {
        _flags.Clear();
        foreach ((string key, int value) in flags)
            _flags[key] = value;
    }
}

/// <summary>Finds what the player would interact with (Confirm): the interactable entity one tile
/// ahead (Phase 7). Warps/triggers are step-on, not talk-to, so they aren't returned.</summary>
public static class Interaction
{
    public static MapEntity? InFront(GridPos playerPos, Facing facing, IReadOnlyList<MapEntity> entities)
    {
        GridPos target = MovementRules.Step(playerPos, facing);
        return entities.FirstOrDefault(e => e.Pos == target && IsInteractable(e));
    }

    private static bool IsInteractable(MapEntity e) => e is NpcEntity or SignEntity or PickupEntity;
}
