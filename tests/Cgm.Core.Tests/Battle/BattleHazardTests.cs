using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleHazardTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Rock = EntityId.Parse("type:rock");
    private static readonly EntityId Flying = EntityId.Parse("type:flying");
    private static readonly EntityId PoisonType = EntityId.Parse("type:poison");
    private static readonly EntityId Immune = EntityId.Parse("type:ghost");

    [Fact]
    public void Compiler_AdmitsGenericHazardFamiliesAndLegacyAliasesWithStrictValidation()
    {
        BattleMove damage = Compile("entryHazardDamage",
            ("key", "shards"), ("maxLayers", 3), ("groundedOnly", true),
            ("fractions", "1/8,1/6,1/4"));
        Assert.Contains(damage.SecondaryEffects, effect => effect is SetEntryHazardEffect
            { Hazard.Kind: EntryHazardKind.Damage, Hazard.MaximumLayers: 3 });

        BattleMove typed = Compile("entryHazardDamage",
            ("key", "typed_shards"), ("maxLayers", 1), ("groundedOnly", false),
            ("type", "rock"), ("num", 1), ("den", 8));
        Assert.Contains(typed.SecondaryEffects, effect => effect is SetEntryHazardEffect
            { Hazard.DamageType: not null });

        BattleMove status = Compile("entryHazardStatus",
            ("key", "toxic_mist"), ("maxLayers", 2), ("groundedOnly", true),
            ("statuses", "poison,toxic"), ("absorbTypes", "poison"));
        Assert.Contains(status.SecondaryEffects, effect => effect is SetEntryHazardEffect
            { Hazard.Kind: EntryHazardKind.Status, Hazard.MaximumLayers: 2 });

        BattleMove stage = Compile("entryHazardStage",
            ("key", "slowing_threads"), ("groundedOnly", true), ("stat", "spe"), ("delta", -1));
        Assert.Contains(stage.SecondaryEffects, effect => effect is SetEntryHazardEffect
            { Hazard.Kind: EntryHazardKind.Stage, Hazard.Stat: StatKind.Spe, Hazard.StageDelta: -1 });

        Assert.IsType<SetEntryHazardEffect>(Assert.Single(Compile("spikes").SecondaryEffects));
        Assert.IsType<SetEntryHazardEffect>(Assert.Single(Compile("stealthRock").SecondaryEffects));
        Assert.Throws<ArgumentException>(() => Compile("entryHazardDamage",
            ("key", "bad"), ("maxLayers", 2), ("groundedOnly", true), ("fractions", "1/8")));
        Assert.Throws<ArgumentException>(() => Compile("entryHazardStatus",
            ("key", "bad"), ("maxLayers", 1), ("groundedOnly", true), ("statuses", "poison"),
            ("other", 1)));
        Assert.Throws<ArgumentException>(() => Compile("entryHazardDamage",
            ("key", "bad"), ("maxLayers", 1), ("fractions", "1/8")));
        Assert.Throws<ArgumentException>(() => Compile("entryHazardDamage",
            ("key", "bad"), ("maxLayers", 1), ("groundedOnly", false),
            ("type", "rock"), ("num", 1)));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove("entryHazardStage",
            DamageClass.Physical, MoveTarget.OpponentsField, ("key", "bad"), ("groundedOnly", true),
            ("stat", "spe"), ("delta", -1)) with { Power = 40 }));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove("entryHazardStage",
            DamageClass.Status, MoveTarget.Selected, ("key", "bad"), ("groundedOnly", true),
            ("stat", "spe"), ("delta", -1))));
        Assert.IsType<RemoveSideConditionEffect>(Assert.Single(MoveCompiler.ToBattleMove(
            DataMove("removeSideCondition", DamageClass.Status, MoveTarget.User,
                ("tag", "entry_hazard"), ("side", "source"), ("timing", "afterHit"))).SecondaryEffects));
    }

    [Fact]
    public void HazardProfiles_AreImmutableAndRejectKeyCollisionsWithDifferentMechanics()
    {
        Fraction[] fractions = [new Fraction(1, 8)];
        var absorbers = new HashSet<EntityId> { PoisonType };
        EntryHazardProfile damage = EntryHazardConditions.LayeredDamage("immutable", fractions);
        EntryHazardProfile status = EntryHazardConditions.Status("immutable_status",
            [PersistentStatus.Poison], absorbers);
        fractions[0] = new Fraction(1, 2);
        absorbers.Clear();
        Assert.Equal(new Fraction(1, 8), Assert.Single(damage.Fractions));
        Assert.Contains(PoisonType, status.AbsorbTypes);

        var stores = new BattleConditionStores(new BattleConditionRegistry([]));
        BattleConditionOwner owner = SideConditions.Owner(BattleSide.Enemy);
        BattleConditionSource source = new(new BattleSlot(BattleSide.Player, 0), 0);
        stores.Apply(new BattleConditionApplication(damage.Id, owner, source, 0, 0),
            EntryHazardConditions.Definition(damage));
        EntryHazardProfile collision = EntryHazardConditions.LayeredDamage("immutable", [new Fraction(1, 4)]);
        Assert.Throws<ArgumentException>(() => stores.Apply(
            new BattleConditionApplication(collision.Id, owner, source, 0, 1),
            EntryHazardConditions.Definition(collision)));
    }

    [Fact]
    public void LayeredHazard_UsesSharedConditionStacksCapsAndCreditsSource()
    {
        EntryHazardProfile profile = EntryHazardConditions.LegacyLayeredDamage;
        BattleCreature source = Creature("source", 400, Normal, Hazard(profile));
        BattleCreature active = Creature("active", 400, Normal, Inert());
        BattleCreature reserve = Creature("reserve", 400, Normal, Inert());
        var battle = new BattleController([source], [active, reserve], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> capped = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        BattleConditionInstance condition = Assert.Single(battle.ConditionSnapshot,
            item => item.Definition.Id == profile.Id);
        Assert.Equal(3, condition.StackCount);
        Assert.Equal(new BattleConditionSource(new BattleSlot(BattleSide.Player, 0), 0), condition.Source);
        Assert.Contains(capped, item => item is MoveFailed { Reason: MoveFailureReason.ConditionAlreadyActive });

        IReadOnlyList<BattleEvent> entry = battle.ResolveTurn(new UseMove(0), new Switch(1));
        EntryHazardTriggered trigger = Assert.Single(entry.OfType<EntryHazardTriggered>());
        Assert.Equal(100, trigger.Value);
        Assert.Equal(condition.Source, trigger.Source);
        Assert.Equal(300, reserve.CurrentHp);
        Assert.Contains(battle.ConditionTrace, item => item.Kind == BattleConditionTraceKind.Stacked);
        Assert.Contains(battle.ConditionTrace, item => item is
            { Kind: BattleConditionTraceKind.Rejected, RejectionReason: BattleConditionRejectionReason.StackLimit });
    }

    [Fact]
    public void GroundedAndTypeFilters_SkipAirborneAndImmuneEntriesWithoutDamage()
    {
        EntryHazardProfile grounded = EntryHazardConditions.LayeredDamage("grounded_shards", [new Fraction(1, 8)]);
        EntryHazardProfile typed = EntryHazardConditions.TypeScaledDamage("typed_shards", Rock, new Fraction(1, 8));
        BattleCreature source = Creature("source", 400, Normal, Hazard(grounded), Hazard(typed));
        BattleCreature active = Creature("active", 400, Normal, Inert());
        BattleCreature airborne = Creature("airborne", 400, Flying, Inert());
        BattleCreature immune = Creature("immune", 400, Immune, Inert());
        var battle = new BattleController([source], [active, airborne, immune], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        IReadOnlyList<BattleEvent> airborneEntry = battle.ResolveTurn(new UseMove(0), new Switch(1));
        Assert.Equal(300, airborne.CurrentHp);
        Assert.Single(airborneEntry.OfType<EntryHazardTriggered>());

        IReadOnlyList<BattleEvent> immuneEntry = battle.ResolveTurn(new UseMove(0), new Switch(2));
        Assert.Equal(350, immune.CurrentHp);
        Assert.Equal(2, immuneEntry.OfType<EntryHazardTriggered>().Count());
        Assert.Contains(battle.Trace, item => item is
            { Kind: EffectTraceKind.EntryHazard, Performed: false, Value: 0 });
    }

    [Fact]
    public void StatusHazard_AppliesLayerPayloadAndGroundedAbsorberRemovesTheCondition()
    {
        EntryHazardProfile profile = EntryHazardConditions.Status("toxic_mist",
            [PersistentStatus.Poison, PersistentStatus.Toxic], new HashSet<EntityId> { PoisonType });
        BattleCreature source = Creature("source", 400, Normal, Hazard(profile));
        BattleCreature active = Creature("active", 400, Normal, Inert());
        BattleCreature target = Creature("target", 400, Normal, Inert());
        BattleCreature absorber = Creature("absorber", 400, PoisonType, Inert());
        var battle = new BattleController([source], [active, target, absorber], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> statusEntry = battle.ResolveTurn(new UseMove(0), new Switch(1));
        Assert.Equal(PersistentStatus.Toxic, target.Status);
        Assert.Contains(statusEntry, item => item is EntryHazardTriggered
            { Kind: EntryHazardKind.Status, Value: (int)PersistentStatus.Toxic });

        target.ClearStatus();
        IReadOnlyList<BattleEvent> absorbed = battle.ResolveTurn(new Pass(), new Switch(2));
        Assert.Null(absorber.Status);
        Assert.Contains(absorbed, item => item is EntryHazardAbsorbed { Condition: var id } && id == profile.Id);
        Assert.DoesNotContain(battle.ConditionSnapshot, item => item.Definition.Id == profile.Id);
        Assert.Contains(battle.ConditionTrace, item => item is
            { Kind: BattleConditionTraceKind.Removed, CleanupReason: BattleConditionCleanupReason.Effect });
    }

    [Fact]
    public void StatusHazard_RespectsOrdinaryStatusImmunityWithoutRemovingItself()
    {
        EntryHazardProfile profile = EntryHazardConditions.Status("poisoning_dust",
            [PersistentStatus.Poison], new HashSet<EntityId>());
        BattleCreature source = Creature("source", 400, Normal, Hazard(profile));
        BattleCreature active = Creature("active", 400, Normal, Inert());
        BattleCreature immune = Creature("immune_status", 400, PoisonType, Inert());
        var battle = new BattleController([source], [active, immune], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new Switch(1));

        Assert.Null(immune.Status);
        Assert.Contains(events, item => item is EntryHazardTriggered
            { Kind: EntryHazardKind.Status, Value: 0 });
        Assert.Contains(battle.ConditionSnapshot, item => item.Definition.Id == profile.Id);
    }

    [Fact]
    public void StageHazardAndTaggedRemoval_UseSharedStageAndConditionPaths()
    {
        EntryHazardProfile profile = EntryHazardConditions.Stage("slowing_threads", StatKind.Spe, -1);
        BattleMove clear = new(EntityId.Parse("move:clear_hazards"), Normal, DamageClass.Status,
            null, null, 20, 0, 0, target: MoveTarget.User,
            secondaryEffects: [new RemoveSideConditionEffect("entry_hazard", SideConditionTarget.Source,
                SideConditionTiming.AfterHit)]);
        BattleCreature source = Creature("source", 400, Normal, Hazard(profile));
        BattleCreature active = Creature("active", 400, Normal, Inert());
        BattleCreature reserve = Creature("reserve", 400, Normal, clear);
        var battle = new BattleController([source], [active, reserve], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new Switch(1));
        Assert.Equal(-1, reserve.Stage(StatKind.Spe));

        IReadOnlyList<BattleEvent> removed = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(removed, item => item is ConditionRemoved { Condition: var id } && id == profile.Id);
        Assert.DoesNotContain(battle.ConditionSnapshot, item => item.Definition.Id == profile.Id);
    }

    [Fact]
    public void StatusAndStageHazards_RespectExistingSideGuards()
    {
        EntryHazardProfile status = EntryHazardConditions.Status("guarded_dust",
            [PersistentStatus.Poison], new HashSet<EntityId>());
        EntryHazardProfile stage = EntryHazardConditions.Stage("guarded_threads", StatKind.Spe, -1);
        BattleMove statusGuard = SideGuard(BattleSideCondition.StatusGuard);
        BattleMove stageGuard = SideGuard(BattleSideCondition.StageDropGuard);
        BattleCreature source = Creature("source", 400, Normal, Hazard(status), Hazard(stage));
        BattleCreature active = Creature("active", 400, Normal, statusGuard, stageGuard);
        BattleCreature reserve = Creature("reserve", 400, Normal, Inert());
        var battle = new BattleController([source], [active, reserve], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(1));
        IReadOnlyList<BattleEvent> entry = battle.ResolveTurn(new Pass(), new Switch(1));

        Assert.Null(reserve.Status);
        Assert.Equal(0, reserve.Stage(StatKind.Spe));
        Assert.Equal(2, entry.OfType<EntryHazardTriggered>().Count(item => item.Value == 0));
        Assert.Contains(battle.HookTrace, item => item.Checkpoint == BattleConditionHook.StatusAttempt);
        Assert.Contains(battle.HookTrace, item => item.Checkpoint == BattleConditionHook.SecondaryEffect);
    }

    [Fact]
    public void DoublesSwitches_TriggerInSlotThenConditionSequence()
    {
        EntryHazardProfile first = EntryHazardConditions.LayeredDamage("first", [new Fraction(1, 8)]);
        EntryHazardProfile second = EntryHazardConditions.LayeredDamage("second", [new Fraction(1, 10)]);
        BattleMove setFirst = Hazard(first);
        BattleMove setSecond = Hazard(second);
        var battle = new BattleController(
            [Creature("p0", 400, Normal, setFirst), Creature("p1", 400, Normal, setSecond)],
            [Creature("e0", 400, Normal, Inert()), Creature("e1", 400, Normal, Inert()),
             Creature("e2", 400, Normal, Inert()), Creature("e3", 400, Normal, Inert())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(1));

        battle.ResolveTurn(Actions(new UseMove(0), new UseMove(0), new Pass(), new Pass()));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            new Pass(), new Pass(), new Switch(2), new Switch(3)));

        Assert.Equal(
        [
            (new BattleSlot(BattleSide.Enemy, 0), first.Id),
            (new BattleSlot(BattleSide.Enemy, 0), second.Id),
            (new BattleSlot(BattleSide.Enemy, 1), first.Id),
            (new BattleSlot(BattleSide.Enemy, 1), second.Id),
        ], events.OfType<EntryHazardTriggered>().Select(item => (item.Slot, item.Condition)));
    }

    [Fact]
    public void HazardFamily_MatchesGolden()
    {
        EntryHazardProfile damage = EntryHazardConditions.LayeredDamage("golden_damage", [new Fraction(1, 8)]);
        EntryHazardProfile stage = EntryHazardConditions.Stage("golden_stage", StatKind.Spe, -1);
        BattleCreature source = Creature("source", 400, Normal, Hazard(damage), Hazard(stage));
        BattleCreature active = Creature("active", 400, Normal, Inert());
        BattleCreature reserve = Creature("reserve", 400, Normal, Inert());
        var battle = new BattleController([source], [active, reserve], Chart(), new Rng(1));
        var events = new List<BattleEvent>();
        events.AddRange(battle.ResolveTurn(new UseMove(0), new UseMove(0)));
        events.AddRange(battle.ResolveTurn(new UseMove(1), new UseMove(0)));
        events.AddRange(battle.ResolveTurn(new UseMove(0), new Switch(1)));

        string snapshot = string.Join('\n',
            ["events", .. events.Select(Event), "condition-trace", .. battle.ConditionTrace.Select(ConditionTrace),
             "effect-trace", .. battle.Trace.Where(item => item.Kind == EffectTraceKind.EntryHazard).Select(EffectTrace)]);
        Assert.Equal(Golden("entry-hazard"), snapshot);
    }

    private static BattleMove Compile(string op, params (string Key, object Value)[] parameters) =>
        MoveCompiler.ToBattleMove(DataMove(op, DamageClass.Status, MoveTarget.OpponentsField, parameters));

    private static Move DataMove(string op, DamageClass damageClass, MoveTarget target,
        params (string Key, object Value)[] parameters) => new()
    {
        Id = EntityId.Parse("move:hazard_probe"),
        Name = "Hazard Probe",
        Type = Normal,
        DamageClass = damageClass,
        Target = target,
        Pp = 10,
        Effects =
        [
            new Effect
            {
                Op = op,
                Params = parameters.ToDictionary(pair => pair.Key,
                    pair => JsonSerializer.SerializeToElement(pair.Value)),
            },
        ],
    };

    private static BattleMove Hazard(EntryHazardProfile profile) =>
        new(EntityId.Parse($"move:{profile.Id.Value.Split(':')[1]}"), Normal, DamageClass.Status,
            null, null, 20, 0, 0, target: MoveTarget.OpponentsField,
            secondaryEffects: [new SetEntryHazardEffect(profile)]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleMove SideGuard(BattleSideCondition condition) =>
        new(EntityId.Parse($"move:{condition.ToString().ToLowerInvariant()}"), Normal, DamageClass.Status,
            null, null, 20, 0, 0, target: MoveTarget.UsersField,
            secondaryEffects: [new SetSideConditionEffect(condition, SideConditions.DefaultTurns)]);

    private static BattleCreature Creature(string slug, int hp, EntityId type, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(hp, 100, 100, 100, 100, slug == "source" ? 200 : 1), moves);

    private static TypeChart Chart() => new(
    [
        new TypeDef { Id = Rock, DoubleDamageTo = [Flying], NoDamageTo = [Immune] },
        new TypeDef { Id = Normal },
        new TypeDef { Id = Flying },
        new TypeDef { Id = PoisonType },
        new TypeDef { Id = Immune },
    ]);

    private static BattleTurnActions Actions(BattleAction p0, BattleAction p1, BattleAction e0, BattleAction e1) =>
        new(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), p0),
            new BattleActionSubmission(new(BattleSide.Player, 1), p1),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), e0),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), e1),
        ]);

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static string Event(BattleEvent item) => item switch
    {
        MoveUsed e => $"MoveUsed:{Slot(e.Slot)}",
        EntryHazardSet e => $"EntryHazardSet:{e.Side}:{e.Condition}:{e.Layers}",
        SwitchedIn e => $"SwitchedIn:{Slot(e.Slot)}:{e.PartyIndex}",
        EntryHazardTriggered e => $"EntryHazardTriggered:{Slot(e.Slot)}:{e.Condition}:{e.Kind}:{e.Value}",
        StatStageChanged e => $"StatStageChanged:{Slot(e.Slot)}:{e.Stat}:{e.Delta}",
        _ => item.GetType().Name,
    };

    private static string ConditionTrace(BattleConditionTraceEntry item) =>
        $"{item.Kind}:{item.Condition}:{item.StacksAfter}";

    private static string EffectTrace(EffectTraceEntry item) =>
        $"{item.Kind}:{(item.TargetSlot is { } slot ? Slot(slot) : "-")}:{item.Condition}:{item.Performed}:{item.Value}";

    private static string Slot(BattleSlot slot) => $"{slot.Side}:{slot.Position}";
}
