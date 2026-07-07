namespace Cgm.Core.Model;

public enum MoverState { Idle, Turning, Moving }

/// <summary>
/// Grid-based movement with Gen 3/4 feel (Phase 7): a quick tap turns to face without stepping;
/// holding a direction steps and then walks continuously; one turn can be buffered mid-step.
/// Driven one fixed tick at a time via <see cref="Tick"/> with the currently-held direction;
/// collision is injected so this stays pure and testable. <see cref="Progress"/> drives the
/// render-side interpolation between tiles.
/// </summary>
public sealed class GridMover
{
    private readonly Func<GridPos, Facing, MoveOutcome> _resolve;
    private readonly int _turnTicks;
    private readonly int _stepTicks;

    private int _timer;
    private int _duration;
    private GridPos _target;
    private Facing? _buffered;

    public GridMover(GridPos start, Facing facing, Func<GridPos, Facing, MoveOutcome> resolve,
        int turnTicks = 4, int stepTicks = 15)
    {
        Position = start;
        Facing = facing;
        _resolve = resolve;
        _turnTicks = turnTicks;
        _stepTicks = stepTicks;
    }

    public GridPos Position { get; private set; }
    public Facing Facing { get; private set; }
    public MoverState State { get; private set; } = MoverState.Idle;

    /// <summary>0→1 progress toward the target tile while moving (for visual interpolation).</summary>
    public float Progress => State == MoverState.Moving && _duration > 0 ? 1f - (float)_timer / _duration : 0f;

    public void Tick(Facing? requested)
    {
        switch (State)
        {
            case MoverState.Idle:
                if (requested is { } dir)
                    Begin(dir);
                break;

            case MoverState.Turning:
                if (--_timer <= 0)
                {
                    if (requested == Facing)
                        TryStep(Facing); // still held after the turn → step
                    else
                        State = MoverState.Idle; // released → just a tap-to-face
                }
                break;

            case MoverState.Moving:
                if (requested is { } rd && rd != Facing)
                    _buffered = rd; // remember a turn requested mid-step
                if (--_timer <= 0)
                {
                    Position = _target;
                    State = MoverState.Idle;
                    Facing? next = _buffered ?? requested;
                    _buffered = null;
                    if (next is { } d)
                        Begin(d);
                }
                break;
        }
    }

    private void Begin(Facing dir)
    {
        if (Facing != dir)
        {
            Facing = dir;
            State = MoverState.Turning;
            _timer = _turnTicks;
        }
        else
        {
            TryStep(dir);
        }
    }

    private void TryStep(Facing dir)
    {
        Facing = dir;
        MoveOutcome outcome = _resolve(Position, dir);
        if (outcome.Result == MoveResult.Blocked)
        {
            State = MoverState.Idle; // faced the wall but stayed put
            return;
        }

        _target = outcome.Destination;
        _duration = _timer = outcome.Result == MoveResult.LedgeHop ? _stepTicks * 2 : _stepTicks;
        State = MoverState.Moving;
    }
}
