namespace Cgm.Runtime.Engine;

/// <summary>The mapped game actions (ENGINE_RUNTIME_SPEC input map).</summary>
public enum GameAction { Up, Down, Left, Right, Confirm, Cancel, Menu, Run }

/// <summary>
/// Edge-detected input: given the set of actions currently held each frame, reports IsDown,
/// WasPressed (down this frame, not last), and WasReleased. Pure — the platform input source
/// feeds <see cref="Update"/>; the sim reads the queries. Keeps movement replayable.
/// </summary>
public sealed class InputState
{
    private readonly HashSet<GameAction> _down = [];
    private readonly HashSet<GameAction> _prev = [];

    public void Update(IEnumerable<GameAction> heldNow)
    {
        _prev.Clear();
        _prev.UnionWith(_down);
        _down.Clear();
        _down.UnionWith(heldNow);
    }

    public bool IsDown(GameAction a) => _down.Contains(a);
    public bool WasPressed(GameAction a) => _down.Contains(a) && !_prev.Contains(a);
    public bool WasReleased(GameAction a) => !_down.Contains(a) && _prev.Contains(a);
}
