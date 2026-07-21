namespace Cgm.Runtime.Engine;

/// <summary>Vertical list and grid navigation (ENGINE_RUNTIME_SPEC 16C). Disabled entries stay
/// visible and are skipped; wrapping happens only when the control opts in; an empty control exposes
/// no selection. Keyboard/gamepad only — no pointer, hover, or key repeat.</summary>
public sealed class SelectionList
{
    private readonly List<bool> _enabled;

    public SelectionList(IEnumerable<bool> enabled, int columns = 1, bool wrap = false)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        if (columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "A control needs at least one column.");
        _enabled = enabled.ToList();
        Columns = columns;
        Wraps = wrap;
        Selected = FirstEnabled();
    }

    public int Columns { get; }

    public bool Wraps { get; }

    public int Count => _enabled.Count;

    /// <summary>The focused index, or null when nothing is selectable.</summary>
    public int? Selected { get; private set; }

    public bool IsEnabled(int index) => index >= 0 && index < _enabled.Count && _enabled[index];

    public void SetEnabled(int index, bool enabled)
    {
        if (index < 0 || index >= _enabled.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _enabled[index] = enabled;
        if (Selected == index && !enabled)
            Selected = Nearest(index);
        else
            Selected ??= FirstEnabled();
    }

    /// <summary>Moves focus one step, skipping disabled entries. Returns true when focus changed.</summary>
    public bool Move(GameAction direction)
    {
        if (Selected is not { } from)
            return false;
        int step = direction switch
        {
            GameAction.Up => -Columns,
            GameAction.Down => Columns,
            GameAction.Left => -1,
            GameAction.Right => 1,
            _ => 0,
        };
        if (step == 0)
            return false;

        // Horizontal movement stays on its row unless the control wraps. This must come from the
        // direction, not the step: in a one-column list Down is also +1.
        bool horizontal = direction is GameAction.Left or GameAction.Right;
        int row = from / Columns;
        int index = from;
        for (int guard = 0; guard < _enabled.Count; guard++)
        {
            index += step;
            if (horizontal && !Wraps && index / Columns != row)
                return false;
            if (index < 0 || index >= _enabled.Count)
            {
                if (!Wraps)
                    return false;
                index = ((index % _enabled.Count) + _enabled.Count) % _enabled.Count;
            }
            if (index == from)
                return false;
            if (_enabled[index])
            {
                Selected = index;
                return true;
            }
        }
        return false;
    }

    /// <summary>Removes an entry. If it was selected, focus moves to the nearest remaining enabled
    /// entry, preferring the next then the previous.</summary>
    public void Remove(int index)
    {
        if (index < 0 || index >= _enabled.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        bool wasSelected = Selected == index;
        _enabled.RemoveAt(index);

        if (wasSelected)
            Selected = _enabled.Count == 0 ? null : Nearest(index);
        else if (Selected is { } current && current > index)
            Selected = current - 1;
        Selected ??= FirstEnabled();
    }

    private int? FirstEnabled()
    {
        int i = _enabled.FindIndex(e => e);
        return i < 0 ? null : i;
    }

    /// <summary>Nearest enabled entry at or after <paramref name="from"/>, else the nearest before.</summary>
    private int? Nearest(int from)
    {
        for (int i = from; i < _enabled.Count; i++)
            if (_enabled[i])
                return i;
        for (int i = Math.Min(from, _enabled.Count) - 1; i >= 0; i--)
            if (_enabled[i])
                return i;
        return null;
    }
}

/// <summary>HP and resource bar presentation. Clamps to 0..max, survives max 0 without dividing, and
/// animates only its displayed value — it never mutates the underlying resource.</summary>
public sealed class ResourceBar(int current, int max, int step = 1)
{
    private readonly int _step = step > 0
        ? step
        : throw new ArgumentOutOfRangeException(nameof(step), step, "Animation step must be positive.");

    public int Max { get; private set; } = Math.Max(0, max);

    /// <summary>The true value the bar is animating toward.</summary>
    public int Target { get; private set; } = Math.Clamp(current, 0, Math.Max(0, max));

    /// <summary>The value currently drawn, which lags <see cref="Target"/> while animating.</summary>
    public int Displayed { get; private set; } = Math.Clamp(current, 0, Math.Max(0, max));

    public bool IsAnimating => Displayed != Target;

    /// <summary>Points the bar at a new value; presentation clamps without touching the source.</summary>
    public void Set(int value, int? max = null)
    {
        if (max is { } newMax)
            Max = Math.Max(0, newMax);
        Target = Math.Clamp(value, 0, Max);
        Displayed = Math.Clamp(Displayed, 0, Max);
    }

    /// <summary>Advances the displayed value one fixed tick toward the target.</summary>
    public void Tick()
    {
        if (Displayed < Target)
            Displayed = Math.Min(Target, Displayed + _step);
        else if (Displayed > Target)
            Displayed = Math.Max(Target, Displayed - _step);
    }

    public void SnapToTarget() => Displayed = Target;

    /// <summary>Filled pixels for a track. A zero max reads as empty rather than dividing by zero,
    /// and any nonzero remainder keeps at least one pixel lit so "nearly dead" never looks dead.</summary>
    public int FillPixels(int trackPixels)
    {
        if (trackPixels <= 0 || Max <= 0 || Displayed <= 0)
            return 0;
        long filled = (long)Displayed * trackPixels / Max;
        return (int)Math.Clamp(Math.Max(filled, 1), 0, trackPixels);
    }
}
