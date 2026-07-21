namespace Cgm.Runtime.Engine;

/// <summary>Paces battle event presentation (ENGINE_RUNTIME_SPEC 16F). Each event holds the screen
/// for a minimum beat; Confirm completes the current beat only; holding Confirm runs presentation at
/// 4× by consuming four presentation ticks per simulation tick.
///
/// Presentation-only: it changes how long text is shown and never the order or content of events,
/// so a fast-forwarded battle produces byte-identical Core state to a slow one.</summary>
public sealed class BattleBeatQueue
{
    public const int MinimumBeatTicks = 6;
    public const int FastForwardMultiplier = 4;

    private readonly Queue<string> _pending = new();
    private readonly List<string> _shown = [];
    private readonly int _beatTicks;
    private int _elapsed;

    public BattleBeatQueue(int beatTicks = MinimumBeatTicks)
    {
        if (beatTicks < MinimumBeatTicks)
            throw new ArgumentOutOfRangeException(nameof(beatTicks), beatTicks,
                $"A beat cannot be shorter than {MinimumBeatTicks} ticks.");
        _beatTicks = beatTicks;
    }

    /// <summary>The line currently holding the screen, or null when nothing is presenting.</summary>
    public string? Current { get; private set; }

    /// <summary>Lines already fully presented, oldest first — the message log.</summary>
    public IReadOnlyList<string> Shown => _shown;

    /// <summary>True while any event is still waiting to be presented.</summary>
    public bool IsPresenting => Current is not null || _pending.Count > 0;

    public int Pending => _pending.Count;

    /// <summary>Ticks the current beat still needs before it may be replaced.</summary>
    public int RemainingBeat => Current is null ? 0 : Math.Max(0, _beatTicks - _elapsed);

    public void Enqueue(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _pending.Enqueue(line);
    }

    public void EnqueueAll(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        foreach (string line in lines)
            Enqueue(line);
    }

    /// <summary>Advances one simulation tick. <paramref name="holdingConfirm"/> multiplies the
    /// presentation ticks consumed, never the number of events.</summary>
    public void Tick(bool holdingConfirm = false)
    {
        int steps = holdingConfirm ? FastForwardMultiplier : 1;
        for (int i = 0; i < steps; i++)
            Step();
    }

    /// <summary>Completes the current beat only — never several at once, so a held button cannot
    /// skip past events the player has not seen.</summary>
    public void Confirm()
    {
        if (Current is not null)
            _elapsed = _beatTicks;
    }

    /// <summary>Drops everything pending, for a battle that ended or was abandoned.</summary>
    public void Clear()
    {
        _pending.Clear();
        Current = null;
        _elapsed = 0;
    }

    private void Step()
    {
        if (Current is null)
        {
            if (_pending.Count == 0)
                return;
            Current = _pending.Dequeue();
            _elapsed = 0;
            return;
        }

        _elapsed++;
        if (_elapsed < _beatTicks)
            return;

        _shown.Add(Current);
        Current = _pending.Count > 0 ? _pending.Dequeue() : null;
        _elapsed = 0;
    }
}
