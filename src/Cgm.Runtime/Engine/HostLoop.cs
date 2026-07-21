using Cgm.Core.Timing;

namespace Cgm.Runtime.Engine;

/// <summary>Input delivered to one simulation tick. Held state reflects the latest poll; edges are
/// delivered to the first due tick only, so a press is never observed twice.</summary>
public readonly record struct TickInput(
    IReadOnlySet<GameAction> Held,
    IReadOnlySet<GameAction> Pressed,
    IReadOnlySet<GameAction> Released)
{
    public bool IsDown(GameAction action) => Held.Contains(action);
    public bool WasPressed(GameAction action) => Pressed.Contains(action);
    public bool WasReleased(GameAction action) => Released.Contains(action);
}

/// <summary>The 16B outer-frame contract (ENGINE_RUNTIME_SPEC host-loop): poll once, feed elapsed
/// milliseconds to the clock, run zero through five fixed ticks with edges delivered once to the
/// first due tick, then render exactly once with the interpolation alpha.
///
/// Platform-free by design — it takes elapsed milliseconds and held actions, so the whole frame
/// order is unit-testable without a window, and the simulation never reads wall time.</summary>
public sealed class HostLoop(FixedStepClock? clock = null)
{
    private static readonly IReadOnlySet<GameAction> None = new HashSet<GameAction>();

    private readonly FixedStepClock _clock = clock ?? new FixedStepClock();
    private readonly HashSet<GameAction> _held = [];
    private readonly HashSet<GameAction> _pendingPressed = [];
    private readonly HashSet<GameAction> _pendingReleased = [];

    public double InterpolationAlpha => _clock.InterpolationAlpha;
    public long TotalTicks { get; private set; }
    public long TotalFrames { get; private set; }

    /// <summary>Ticks that were due but dropped by the clock's catch-up cap, reported for
    /// diagnostics and never replayed.</summary>
    public long DroppedTicks { get; private set; }

    /// <summary>Runs one outer frame. <paramref name="tick"/> is invoked once per due tick in order;
    /// zero-tick frames still poll and still count as a rendered frame, and their edges stay buffered
    /// until the first later tick. Returns the number of ticks executed.</summary>
    public int Frame(double elapsedMs, IEnumerable<GameAction> heldNow, Action<TickInput> tick)
    {
        ArgumentNullException.ThrowIfNull(heldNow);
        ArgumentNullException.ThrowIfNull(tick);

        Poll(heldNow);
        double due = (_clock.AccumulatorMs + elapsedMs) / _clock.TickIntervalMs;
        int ticks = _clock.Advance(elapsedMs);
        if (due > _clock.MaxTicksPerAdvance)
            DroppedTicks += (long)due - _clock.MaxTicksPerAdvance;

        for (int i = 0; i < ticks; i++)
        {
            tick(ConsumeForTick());
            TotalTicks++;
        }

        TotalFrames++;
        return ticks;
    }

    /// <summary>Drops accumulated time after a stall (minimize/restore, debugger pause, device reset,
    /// resize) so the next frame does not burn catch-up ticks on time the player did not experience.
    /// Buffered input edges survive: the player's presses were real.</summary>
    public void ResetAfterStall() => _clock.Reset();

    private void Poll(IEnumerable<GameAction> heldNow)
    {
        var now = heldNow as IReadOnlySet<GameAction> ?? heldNow.ToHashSet();
        foreach (GameAction action in now)
            if (!_held.Contains(action))
                _pendingPressed.Add(action);
        foreach (GameAction action in _held)
            if (!now.Contains(action))
                _pendingReleased.Add(action);

        _held.Clear();
        _held.UnionWith(now);
    }

    /// <summary>Edges go to the first due tick and are then cleared, so later ticks in the same frame
    /// see current held state without repeating the press.</summary>
    private TickInput ConsumeForTick()
    {
        if (_pendingPressed.Count == 0 && _pendingReleased.Count == 0)
            return new TickInput(_held.ToHashSet(), None, None);

        var input = new TickInput(_held.ToHashSet(), _pendingPressed.ToHashSet(), _pendingReleased.ToHashSet());
        _pendingPressed.Clear();
        _pendingReleased.Clear();
        return input;
    }
}
