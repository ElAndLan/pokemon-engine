namespace Cgm.Core.Model;

/// <summary>What a completed step or an interaction produced. Exactly one outcome fires; Runtime
/// presents it and never decides which.</summary>
public abstract record StepOutcome
{
    private StepOutcome() { }

    /// <summary>Nothing happened; the player keeps walking.</summary>
    public sealed record None : StepOutcome
    {
        public static readonly None Instance = new();
    }

    public sealed record Warp(WarpEntity Entity) : StepOutcome;

    /// <summary>A trigger fired; its actions are the closed vocabulary from DATA_SCHEMA §4.11b.</summary>
    public sealed record Trigger(TriggerEntity Entity) : StepOutcome;

    public sealed record TrainerSpotted(NpcEntity Npc, Trainer Trainer) : StepOutcome;

    public sealed record WildEncounter(EntityId Species, int Level) : StepOutcome;

    /// <summary>An interactable the player is facing, or standing on when nothing is ahead.</summary>
    public sealed record Interact(MapEntity Entity) : StepOutcome;
}

/// <summary>Resolves what happens after a completed step and what a Confirm press interacts with
/// (ENGINE_RUNTIME_SPEC 16D). The order is a game rule, so it lives in Core: Runtime asks and
/// presents, and never sequences these itself.</summary>
public static class OverworldStep
{
    /// <summary>Completed-step order: warp → tile trigger → trainer sight → random encounter,
    /// stopping at the first transition.
    ///
    /// The encounter roll is evaluated last and only when nothing else fired, so an earlier
    /// transition consumes no RNG. That ordering is what keeps a seeded replay identical: a step
    /// that warps must not advance the encounter stream.</summary>
    public static StepOutcome Resolve(
        GridPos landed,
        Map map,
        CollisionValue[] collision,
        FlagStore flags,
        IReadOnlyDictionary<EntityId, Trainer> trainers,
        IReadOnlyDictionary<EntityId, EncounterTable> tables,
        IRng rng,
        TimeOfDay? time = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(collision);
        ArgumentNullException.ThrowIfNull(flags);
        ArgumentNullException.ThrowIfNull(trainers);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(rng);

        // Entity order is the authored list order, which is stable because every entity has a key.
        if (At<WarpEntity>(map, landed) is { } warp)
            return new StepOutcome.Warp(warp);

        if (At<TriggerEntity>(map, landed) is { } trigger && ConditionMet(trigger, flags))
            return new StepOutcome.Trigger(trigger);

        if (TrainerSight.FirstSpotter(landed, map.Entities, trainers, flags, collision,
                map.Width, map.Height) is { } spotter)
            return new StepOutcome.TrainerSpotted(spotter.Npc, spotter.Trainer);

        return Encounter(landed, map, flags, tables, rng, time);
    }

    /// <summary>Interaction priority: the entity the player faces, then a trigger or object on that
    /// tile, then whatever they are standing on.</summary>
    public static StepOutcome Interact(GridPos playerPos, Facing facing, Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (Interaction.InFront(playerPos, facing, map.Entities) is { } ahead)
            return new StepOutcome.Interact(ahead);

        GridPos front = MovementRules.Step(playerPos, facing);
        if (At<TriggerEntity>(map, front) is { } facingTrigger)
            return new StepOutcome.Interact(facingTrigger);

        if (map.Entities.FirstOrDefault(e => e.Pos == playerPos) is { } underfoot)
            return new StepOutcome.Interact(underfoot);

        return StepOutcome.None.Instance;
    }

    private static StepOutcome Encounter(GridPos landed, Map map, FlagStore flags,
        IReadOnlyDictionary<EntityId, EncounterTable> tables, IRng rng, TimeOfDay? time)
    {
        int index = landed.Y * map.Width + landed.X;
        EncounterZoneCell zone = map.EncounterZones.FirstOrDefault(z => z.Index == index);
        if (zone.Table == default || !tables.TryGetValue(zone.Table, out EncounterTable? table))
            return StepOutcome.None.Instance;

        if (!EncounterRoll.Triggers(table.BaseRate, rng))
            return StepOutcome.None.Instance;

        EncounterSlot? slot = EncounterRoll.PickSlot(table, rng, time, flags.GetBool);
        return slot is null
            ? StepOutcome.None.Instance
            : new StepOutcome.WildEncounter(slot.Species, EncounterRoll.RollLevel(slot, rng));
    }

    private static T? At<T>(Map map, GridPos pos) where T : MapEntity =>
        map.Entities.OfType<T>().FirstOrDefault(e => e.Pos == pos);

    /// <summary>A trigger's condition is a flag name; an unset flag means the trigger stays dormant.</summary>
    private static bool ConditionMet(TriggerEntity trigger, FlagStore flags) =>
        string.IsNullOrWhiteSpace(trigger.Condition) || flags.GetBool(trigger.Condition);
}
