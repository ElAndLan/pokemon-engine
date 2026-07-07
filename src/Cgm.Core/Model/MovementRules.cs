namespace Cgm.Core.Model;

public enum MoveResult { Blocked, Step, LedgeHop }

/// <summary>The outcome of a move attempt: whether it happened and where it lands.</summary>
public readonly record struct MoveOutcome(MoveResult Result, GridPos Destination)
{
    public static readonly MoveOutcome Blocked = new(MoveResult.Blocked, default);
}

/// <summary>
/// Grid movement resolution (ENGINE_RUNTIME_SPEC / Phase 7). Pure: given the derived collision
/// grid and which cells are occupied, decides if a step is blocked, a normal step, or a one-way
/// ledge hop (only when moving in the ledge's own direction; lands two cells away). Reused by the
/// runtime's mover and by tests.
/// </summary>
public static class MovementRules
{
    public static MoveOutcome Resolve(GridPos from, Facing dir, CollisionValue[] collision,
        int width, int height, IReadOnlySet<GridPos> occupied)
    {
        GridPos target = Step(from, dir);
        if (!InBounds(target, width, height))
            return MoveOutcome.Blocked;

        CollisionValue cell = collision[target.Y * width + target.X];

        if (IsLedge(cell))
        {
            if (!LedgeAllows(cell, dir))
                return MoveOutcome.Blocked; // can only take a ledge in its own direction

            GridPos landing = Step(target, dir); // hop over the ledge, two cells from `from`
            if (!InBounds(landing, width, height) || occupied.Contains(landing) ||
                collision[landing.Y * width + landing.X] == CollisionValue.Solid)
                return MoveOutcome.Blocked;

            return new MoveOutcome(MoveResult.LedgeHop, landing);
        }

        if (cell == CollisionValue.Solid || occupied.Contains(target))
            return MoveOutcome.Blocked;

        return new MoveOutcome(MoveResult.Step, target);
    }

    public static GridPos Step(GridPos p, Facing dir) => dir switch
    {
        Facing.Up => new GridPos(p.X, p.Y - 1),
        Facing.Down => new GridPos(p.X, p.Y + 1),
        Facing.Left => new GridPos(p.X - 1, p.Y),
        Facing.Right => new GridPos(p.X + 1, p.Y),
        _ => p,
    };

    private static bool InBounds(GridPos p, int w, int h) => p.X >= 0 && p.Y >= 0 && p.X < w && p.Y < h;

    private static bool IsLedge(CollisionValue c) =>
        c is CollisionValue.LedgeUp or CollisionValue.LedgeDown or CollisionValue.LedgeLeft or CollisionValue.LedgeRight;

    private static bool LedgeAllows(CollisionValue cell, Facing dir) => (cell, dir) switch
    {
        (CollisionValue.LedgeUp, Facing.Up) => true,
        (CollisionValue.LedgeDown, Facing.Down) => true,
        (CollisionValue.LedgeLeft, Facing.Left) => true,
        (CollisionValue.LedgeRight, Facing.Right) => true,
        _ => false,
    };
}
