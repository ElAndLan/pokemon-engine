namespace Cgm.Core.Model;

/// <summary>
/// Wander AI for an NPC (Phase 7): each decision it tries one random direction and returns it only
/// if the step stays within <paramref name="radius"/> of home and actually resolves to a plain step
/// (not blocked, occupied, or a ledge). Otherwise it idles (null). Pure + seeded so wander is
/// deterministic and never escapes its pen.
/// </summary>
public static class NpcWander
{
    public static Facing? PickStep(GridPos home, GridPos current, int radius,
        Func<GridPos, Facing, MoveOutcome> resolve, IRng rng)
    {
        var dir = (Facing)rng.Next(4);
        GridPos target = MovementRules.Step(current, dir);

        if (WithinRadius(home, target, radius) && resolve(current, dir).Result == MoveResult.Step)
            return dir;

        return null;
    }

    private static bool WithinRadius(GridPos home, GridPos p, int radius) =>
        Math.Abs(p.X - home.X) <= radius && Math.Abs(p.Y - home.Y) <= radius;
}
