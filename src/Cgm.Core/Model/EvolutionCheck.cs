namespace Cgm.Core.Model;

/// <summary>The inputs an evolution is tested against (DATA_SCHEMA §4.3a). <see cref="UsedItem"/> is the
/// item applied for a <see cref="EvolutionTrigger.UseItem"/> attempt; the rest describe the creature.</summary>
public readonly record struct EvolutionContext(
    EvolutionTrigger Trigger,
    int Level,
    int Happiness,
    TimeOfDay TimeOfDay,
    EntityId? UsedItem = null,
    EntityId? HeldItem = null,
    IReadOnlyCollection<EntityId>? KnownMoves = null);

/// <summary>
/// Decides whether a creature evolves (Phase 13 / DATA_SCHEMA §4.3a). Pure: the first of a species'
/// evolutions whose conditions all hold wins, else no evolution. v1 executes the seven in-scope
/// conditions only — level, item, trade-flag, happiness, time-of-day, known-move, held-item.
/// </summary>
// ponytail: gender/location conditions are stored but not executed in v1 (DATA_SCHEMA §4.3a note);
// no in-scope demo content uses them. Add a check here when their phase lands.
public static class EvolutionCheck
{
    public static EntityId? Evaluate(Species species, EvolutionContext ctx)
    {
        foreach (Evolution evo in species.Evolutions)
            if (Matches(evo, ctx))
                return evo.Target;
        return null;
    }

    /// <summary>Convenience overload deriving level/happiness/held-item/known-moves from an instance.</summary>
    public static EntityId? Evaluate(Species species, CreatureInstance creature,
        EvolutionTrigger trigger, TimeOfDay timeOfDay, EntityId? usedItem = null) =>
        Evaluate(species, new EvolutionContext(
            trigger, creature.Level, creature.Happiness, timeOfDay, usedItem, creature.HeldItem,
            [.. creature.Moves.Select(m => m.Move)]));

    private static bool Matches(Evolution evo, EvolutionContext ctx)
    {
        if (evo.Trigger != ctx.Trigger)
            return false;
        if (evo.Trigger == EvolutionTrigger.UseItem && evo.Item != ctx.UsedItem)
            return false;
        if (evo.MinLevel is { } level && ctx.Level < level)
            return false;
        if (evo.MinHappiness is { } happiness && ctx.Happiness < happiness)
            return false;
        if (evo.TimeOfDay is { } tod && ctx.TimeOfDay != tod)
            return false;
        if (evo.HeldItem is { } held && ctx.HeldItem != held)
            return false;
        if (evo.KnownMove is { } move && ctx.KnownMoves?.Contains(move) != true)
            return false;
        return true;
    }
}
