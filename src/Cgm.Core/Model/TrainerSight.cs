namespace Cgm.Core.Model;

/// <summary>
/// Trainer line-of-sight (Phase 11 / MASTER_PLAN §8). Pure: a trainer NPC sees the player if the
/// player stands on a straight line in the NPC's facing direction within its sight range, with no
/// solid cell or other NPC in between. The runtime runs <see cref="FirstSpotter"/> after each player
/// step; defeated trainers (their derived flag set) and interact-only trainers (range 0) never spot.
/// </summary>
public static class TrainerSight
{
    /// <summary>The derived, never-stored defeated flag for a trainer (DATA_SCHEMA §4.13).</summary>
    public static string DefeatedFlag(EntityId trainerId) => $"flag:trainer.{trainerId.Slug}_defeated";

    /// <summary>True if a trainer at <paramref name="npcPos"/> facing <paramref name="dir"/> can see the
    /// player within <paramref name="range"/> tiles. Sight travels one cell at a time; a solid cell or a
    /// blocker cell stops it (sight passes over ledges, which aren't solid).</summary>
    public static bool Sees(GridPos npcPos, Facing dir, int range, GridPos playerPos,
        CollisionValue[] collision, int width, int height, IReadOnlySet<GridPos> blockers)
    {
        GridPos cell = npcPos;
        for (int step = 1; step <= range; step++)
        {
            cell = MovementRules.Step(cell, dir);
            if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height)
                return false;
            if (cell == playerPos)
                return true;
            if (collision[cell.Y * width + cell.X] == CollisionValue.Solid || blockers.Contains(cell))
                return false; // wall or another NPC breaks the line before it reaches the player
        }
        return false;
    }

    /// <summary>The first not-yet-defeated trainer NPC on the map that currently sees the player, in
    /// entity order, or null if none does. Other NPCs block sight lines.</summary>
    public static (NpcEntity Npc, Trainer Trainer)? FirstSpotter(GridPos playerPos,
        IReadOnlyList<MapEntity> entities, IReadOnlyDictionary<EntityId, Trainer> trainers,
        FlagStore flags, CollisionValue[] collision, int width, int height)
    {
        HashSet<GridPos> npcCells = [.. entities.OfType<NpcEntity>().Select(n => n.Pos)];

        foreach (NpcEntity npc in entities.OfType<NpcEntity>())
        {
            if (npc.Trainer is not { } trainerId || !trainers.TryGetValue(trainerId, out Trainer? trainer))
                continue;
            if (trainer.SightRange <= 0 || flags.GetBool(DefeatedFlag(trainerId)))
                continue;

            npcCells.Remove(npc.Pos); // the spotter itself isn't its own blocker
            bool sees = Sees(npc.Pos, npc.Facing, trainer.SightRange, playerPos, collision, width, height, npcCells);
            npcCells.Add(npc.Pos);
            if (sees)
                return (npc, trainer);
        }
        return null;
    }
}
