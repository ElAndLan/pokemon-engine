using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// Party recovery rules (Phase 13): creature-center healing, respawn-point setting, and blackout.
/// Pure — returns new records, reads max HP/PP from the compiled <see cref="GameDb"/>. Healing means
/// full HP, full PP on every move, and cleared status; blackout warps to the last center (or the
/// project's start map when none was set) and heals.
/// </summary>
public static class Recovery
{
    public static CreatureInstance HealCreature(CreatureInstance creature, GameDb db)
    {
        Species species = db.Find<Species>(creature.Species)
            ?? throw new InvalidOperationException($"Unknown species '{creature.Species}'.");
        int maxHp = StatCalc.Compute(species.BaseStats, creature.Ivs, creature.Evs, creature.Nature, creature.Level).Hp;

        var moves = new List<MoveSlot>(creature.Moves.Count);
        foreach (MoveSlot slot in creature.Moves)
        {
            Move move = db.Find<Move>(slot.Move)
                ?? throw new InvalidOperationException($"Unknown move '{slot.Move}'.");
            moves.Add(slot with { Pp = move.Pp });
        }

        return creature with { CurHp = maxHp, Status = null, StatusCounter = 0, Moves = moves };
    }

    public static IReadOnlyList<CreatureInstance> HealParty(IReadOnlyList<CreatureInstance> party, GameDb db) =>
        [.. party.Select(c => HealCreature(c, db))];

    /// <summary>Visit a creature center: full party heal + respawn point set to the center.</summary>
    public static SaveFile VisitCenter(SaveFile save, GameDb db, EntityId centerMap, GridPos centerPos) =>
        save with { Party = HealParty(save.Party, db), Respawn = new RespawnPoint(centerMap, centerPos) };

    /// <summary>Black out: warp to the last center (or the start map if none) and full-heal the party.</summary>
    public static SaveFile Blackout(SaveFile save, GameDb db)
    {
        RespawnPoint point = save.Respawn ?? new RespawnPoint(
            db.Settings.StartMap ?? throw new InvalidOperationException("Project has no start map for blackout fallback."),
            db.Settings.StartPos);

        return save with
        {
            Map = point.Map,
            Pos = point.Pos,
            Facing = Facing.Down,
            Party = HealParty(save.Party, db),
        };
    }
}
