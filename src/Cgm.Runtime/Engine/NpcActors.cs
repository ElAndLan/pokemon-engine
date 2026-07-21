using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>One NPC's live movement state. Every step decision comes from Core — `NpcWander` for
/// wandering, the authored path for patrolling — and this only drives a <see cref="GridMover"/> and
/// remembers where the NPC started.</summary>
public sealed class NpcActor
{
    private readonly NpcEntity _npc;
    private readonly GridPos _home;
    private readonly GridMover _mover;
    private readonly Func<GridPos, Facing, MoveOutcome> _resolve;
    private int _waypoint;

    public NpcActor(NpcEntity npc, Func<GridPos, Facing, MoveOutcome> resolve)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(resolve);
        _npc = npc;
        _home = npc.Pos;
        _resolve = resolve;
        _mover = new GridMover(npc.Pos, npc.Facing, resolve);
    }

    public string Key => _npc.Key;

    public NpcEntity Entity => _npc;

    public GridPos Position => _mover.Position;

    public Facing Facing => _mover.Facing;

    public MoverState State => _mover.State;

    public float Progress => _mover.Progress;

    /// <summary>Advances one fixed tick. Only an idle NPC decides a new direction, so a decision is
    /// made once per step rather than once per tick — that keeps the RNG draw count tied to steps
    /// taken, which is what makes a seeded replay reproducible.</summary>
    public void Tick(IRng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        Facing? intent = _mover.State == MoverState.Idle ? Decide(rng) : null;
        _mover.Tick(intent);
    }

    private Facing? Decide(IRng rng) => _npc.Move switch
    {
        NpcMovement.Wander => NpcWander.PickStep(_home, _mover.Position, _npc.Radius ?? 1, _resolve, rng),
        NpcMovement.Patrol => NextWaypoint(),
        _ => null, // Static NPCs never move.
    };

    /// <summary>Walks the authored path in order, looping. An NPC standing on its next waypoint
    /// advances to the following one rather than stalling.</summary>
    private Facing? NextWaypoint()
    {
        if (_npc.Path is not { Count: > 0 } path)
            return null;

        GridPos target = path[_waypoint % path.Count];
        if (target == _mover.Position)
        {
            _waypoint = (_waypoint + 1) % path.Count;
            target = path[_waypoint];
        }

        if (target.X != _mover.Position.X)
            return target.X > _mover.Position.X ? Facing.Right : Facing.Left;
        if (target.Y != _mover.Position.Y)
            return target.Y > _mover.Position.Y ? Facing.Down : Facing.Up;
        return null;
    }

}
