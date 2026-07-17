using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleSideProtectionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void Compiler_AdmitsClosedProtectionRowsBypassAndRemoval()
    {
        foreach (BattleSideCondition condition in ProtectionRows())
        {
            BattleMove compiled = MoveCompiler.ToBattleMove(DataMove(condition));
            Assert.Contains(compiled.SecondaryEffects, effect => effect is SetSideConditionEffect
                { Condition: var row, Duration: 1 } && row == condition);
        }

        Move bypass = DataMove(BattleSideCondition.DamageProtection) with
        {
            DamageClass = DamageClass.Physical,
            Power = 40,
            Target = MoveTarget.Selected,
            Effects = [Op("sideConditionBypass", ("tag", "side_protection"))],
        };
        Assert.Contains(MoveCompiler.ToBattleMove(bypass).SecondaryEffects,
            effect => effect is SideConditionBypassEffect { Tag: "side_protection" });

        Move remove = bypass with
        {
            Effects = [Op("removeSideCondition", ("tag", "side_protection"),
                ("side", "target"), ("timing", "beforeDamage"))],
        };
        Assert.Contains(MoveCompiler.ToBattleMove(remove).SecondaryEffects,
            effect => effect is RemoveSideConditionEffect { Tag: "side_protection" });

        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(
            BattleSideCondition.PriorityProtection, duration: 2)));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(bypass with
        {
            Target = MoveTarget.User,
        }));
    }

    [Fact]
    public void PriorityProtection_UsesAuthoredOrResolvedPriorityByProfile()
    {
        BattleMove ordinary = Hit("ordinary", MoveTarget.Selected, priority: 0);
        BattleConditionInstance guard = Condition(BattleSide.Player, BattleSideCondition.PriorityProtection);

        Assert.Empty(Filters(guard, ordinary, effectivePriority: 1, BattleRulesets.Gen4Like));
        Assert.Single(Filters(guard, ordinary, effectivePriority: 1, BattleRulesets.ModernReference));
        Assert.Single(Filters(guard, Hit("authored", MoveTarget.Selected, priority: 1),
            effectivePriority: 1, BattleRulesets.Gen4Like));
    }

    [Fact]
    public void ProtectionRows_UseExactBehaviorFilters()
    {
        BattleSlot source = new(BattleSide.Enemy, 0);
        BattleSlot target = new(BattleSide.Player, 0);
        Assert.True(Blocked(BattleSideCondition.MultiTargetProtection,
            Hit("spread", MoveTarget.AllOpponents), source, target));
        Assert.False(Blocked(BattleSideCondition.MultiTargetProtection,
            Hit("selected", MoveTarget.Selected), source, target));
        Assert.True(Blocked(BattleSideCondition.StatusProtection,
            Status("status", MoveTarget.Selected), source, target));
        Assert.False(Blocked(BattleSideCondition.StatusProtection,
            Status("self", MoveTarget.User), source, source));
        Assert.False(Blocked(BattleSideCondition.StatusProtection,
            Status("field", MoveTarget.AllPokemon), source, target));
        Assert.True(Blocked(BattleSideCondition.DamageProtection,
            Hit("damage", MoveTarget.Selected), source, target));
        Assert.False(Blocked(BattleSideCondition.DamageProtection,
            Status("not_damage", MoveTarget.Selected), source, target));
    }

    [Fact]
    public void SinglesProtection_BlocksBeforeAccuracyThenExpires()
    {
        BattleCreature defender = Creature("defender", 40,
            Guard("guard", BattleSideCondition.DamageProtection), Wait("wait"));
        BattleCreature attacker = Creature("attacker", 100, Hit("hit", MoveTarget.Selected, accuracy: 50));
        var rng = new CountingRng();
        var battle = new BattleController(defender, attacker, Chart(), rng);

        IReadOnlyList<BattleEvent> first = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(500, defender.CurrentHp);
        Assert.Equal(0, rng.IntDraws);
        Assert.Contains(first, entry => entry is MoveBlocked { Side: BattleSide.Enemy });
        Assert.Contains(battle.Trace, entry => entry is
            { Kind: EffectTraceKind.SideProtection, TargetSlot.Side: BattleSide.Player, Value: 0 });
        Assert.Contains(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.TryHit
            && entry.PayloadKind == BattleHookPayloadKind.Filter);
        Assert.Empty(battle.ConditionSnapshot);

        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.True(defender.CurrentHp < 500);
        Assert.True(rng.IntDraws > 0);
    }

    [Fact]
    public void MultiTargetProtection_BlocksAuthoredSpreadEvenInSingles()
    {
        BattleCreature defender = Creature("defender", 40,
            Guard("guard", BattleSideCondition.MultiTargetProtection));
        BattleCreature spread = Creature("spread", 100, Hit("spread", MoveTarget.AllOpponents));
        var battle = new BattleController(defender, spread, Chart(), new CountingRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(500, defender.CurrentHp);
    }

    [Fact]
    public void ProtectionRows_CoexistAndRejectDuplicateSideApplications()
    {
        BattleCreature first = Creature("first", 100,
            Guard("damage", BattleSideCondition.DamageProtection));
        BattleCreature second = Creature("second", 80,
            Guard("priority", BattleSideCondition.PriorityProtection));
        var coexist = new BattleController([first, second],
            [Creature("foe0", 60, Wait("wait0")), Creature("foe1", 50, Wait("wait1"))],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new CountingRng());

        IReadOnlyList<BattleEvent> applied = coexist.ResolveTurn(Actions(
            new UseMove(0), new UseMove(0), new Pass(), new Pass()));

        Assert.Equal(2, applied.OfType<ConditionApplied>().Count());
        Assert.Equal(2, coexist.ConditionTrace.Count(entry => entry.Kind == BattleConditionTraceKind.Applied));
        Assert.Empty(coexist.ConditionSnapshot);

        BattleCreature duplicate0 = Creature("duplicate0", 100,
            Guard("guard0", BattleSideCondition.DamageProtection));
        BattleCreature duplicate1 = Creature("duplicate1", 80,
            Guard("guard1", BattleSideCondition.DamageProtection));
        var duplicate = new BattleController([duplicate0, duplicate1],
            [Creature("other0", 60, Wait("other_wait0")), Creature("other1", 50, Wait("other_wait1"))],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new CountingRng());

        IReadOnlyList<BattleEvent> rejected = duplicate.ResolveTurn(Actions(
            new UseMove(0), new UseMove(0), new Pass(), new Pass()));

        Assert.Single(rejected.OfType<ConditionApplicationRejected>());
        Assert.Contains(rejected, entry => entry is MoveFailed
            { Reason: MoveFailureReason.ConditionAlreadyActive });
    }

    [Fact]
    public void StatusProtection_SkipsEffectChanceAndStatusMutation()
    {
        BattleMove status = new(EntityId.Parse("move:status"), Normal, DamageClass.Status,
            null, null, 20, 0, 0, ailment: PersistentStatus.Poison, ailmentChance: 50,
            target: MoveTarget.Selected,
            secondaryEffects: [new AilmentEffect(PersistentStatus.Poison) { Chance = 50 }]);
        BattleCreature defender = Creature("defender", 40,
            Guard("guard", BattleSideCondition.StatusProtection));
        BattleCreature attacker = Creature("attacker", 100, status);
        var rng = new CountingRng();
        var battle = new BattleController(defender, attacker, Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Null(defender.Status);
        Assert.Equal(0, rng.IntDraws);
        Assert.Equal(0, rng.DoubleDraws);
    }

    [Fact]
    public void DamageProtection_ComposesWithFirstActionGate()
    {
        BattleMove gated = Guard("gated", BattleSideCondition.DamageProtection,
            new MoveGateEffect(MoveGateKind.FirstAction));
        BattleCreature defender = Creature("defender", 40, Wait("consume"), gated);
        BattleCreature attacker = Creature("attacker", 100, Hit("hit", MoveTarget.Selected));
        var battle = new BattleController(defender, attacker, Chart(), new CountingRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> failed = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Contains(failed, entry => entry is MoveFailed { Reason: MoveFailureReason.FirstActionOnly });
        Assert.DoesNotContain(battle.ConditionTrace, entry => entry.Condition ==
            SideConditions.For(BattleSideCondition.DamageProtection).Id);
    }

    [Fact]
    public void DoublesProtection_IsSharedAndBlocksEligibleAlliedSpreadTargets()
    {
        BattleCreature source = Creature("source", 100, Hit("spread", MoveTarget.AllOtherPokemon));
        BattleCreature ally = Creature("ally", 80,
            Guard("guard", BattleSideCondition.MultiTargetProtection));
        BattleCreature foe0 = Creature("foe0", 60, Wait("wait0"));
        BattleCreature foe1 = Creature("foe1", 50, Wait("wait1"));
        var battle = new BattleController([source, ally], [foe0, foe1], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new CountingRng());

        battle.ResolveTurn(Actions(new UseMove(0), new UseMove(0), new Pass(), new Pass()));

        Assert.Equal(500, ally.CurrentHp);
        Assert.True(foe0.CurrentHp < 500);
        Assert.True(foe1.CurrentHp < 500);
        Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.SideProtection);
    }

    [Fact]
    public void TaggedBypassAndRemoval_HitsAndClearsProtection()
    {
        BattleMove breaker = new(EntityId.Parse("move:breaker"), Normal, DamageClass.Physical,
            60, null, 20, 0, 0, target: MoveTarget.Selected,
            secondaryEffects:
            [
                new SideConditionBypassEffect("side_protection"),
                new RemoveSideConditionEffect("side_protection", SideConditionTarget.Target,
                    SideConditionTiming.BeforeDamage),
            ]);
        BattleCreature attacker = Creature("attacker", 100, breaker);
        BattleCreature defender = Creature("defender", 40,
            Guard("guard", BattleSideCondition.DamageProtection));
        var battle = new BattleController(attacker, defender, Chart(), new CountingRng());

        int before = defender.CurrentHp;
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(defender.CurrentHp < before);
        Assert.DoesNotContain(battle.ConditionSnapshot, entry => entry.Definition.Tags.Contains("side_protection"));
        Assert.Contains(battle.Trace, entry => entry is
            { Kind: EffectTraceKind.ConditionRemoval, Value: 1 });
    }

    [Fact]
    public void SmartAi_UsesSharedProtectionPredicateForDamageStatusAndBypass()
    {
        BattleCreature defender = Creature("defender", 40, Wait("wait"));
        BattleCreature damage = Creature("damage", 100, Hit("hit", MoveTarget.Selected));
        BattleCreature status = Creature("status", 100, new BattleMove(EntityId.Parse("move:status"),
            Normal, DamageClass.Status, null, null, 20, 0, 0,
            ailment: PersistentStatus.Poison, ailmentChance: 100, target: MoveTarget.Selected,
            secondaryEffects: [new AilmentEffect(PersistentStatus.Poison)]));
        BattleCreature bypass = Creature("bypass", 100, new BattleMove(EntityId.Parse("move:bypass"),
            Normal, DamageClass.Physical, 60, null, 20, 0, 0, target: MoveTarget.Selected,
            secondaryEffects: [new SideConditionBypassEffect("side_protection")]));

        AiCandidateScore damageBlocked = Score(damage, defender,
            [Condition(BattleSide.Player, BattleSideCondition.DamageProtection)]);
        AiCandidateScore statusBlocked = Score(status, defender,
            [Condition(BattleSide.Player, BattleSideCondition.StatusProtection)]);
        AiCandidateScore bypassed = Score(bypass, defender,
            [Condition(BattleSide.Player, BattleSideCondition.DamageProtection)]);

        Assert.Contains(damageBlocked.Components, component => component.Name == "damage" && component.Value == 0);
        Assert.DoesNotContain(statusBlocked.Components, component => component.Name == "status");
        Assert.Contains(bypassed.Components, component => component.Name == "damage" && component.Value > 0);
    }

    [Fact]
    public void ProtectionReplay_IsDeterministicAndSourceFaintDoesNotRemoveEarly()
    {
        static (string[] Events, string[] Conditions, string[] Effects) Run()
        {
            BattleCreature guard = Creature("guard", 100,
                Guard("guard_move", BattleSideCondition.DamageProtection));
            BattleCreature ally = Creature("ally", 40, Wait("ally_wait"));
            BattleMove bypassingHit = new(EntityId.Parse("move:killer_hit"), Normal,
                DamageClass.Physical, 60, null, 20, 2, 0, target: MoveTarget.Selected,
                secondaryEffects: [new SideConditionBypassEffect("side_protection")]);
            BattleCreature killer = Creature("killer", 90, bypassingHit);
            BattleCreature later = Creature("later", 80, Hit("later_hit", MoveTarget.Selected, priority: 1));
            var battle = new BattleController([guard, ally], [killer, later], BattleTopology.Doubles,
                [0, 1], [0, 1], Chart(), new CountingRng());
            guard.TakeDamage(495);

            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
            [
                new(new BattleSlot(BattleSide.Player, 0), new UseMove(0)),
                new(new BattleSlot(BattleSide.Player, 1), new Pass()),
                new(new BattleSlot(BattleSide.Enemy, 0), new UseMove(0),
                    new ActiveSlotSelection(new BattleSlot(BattleSide.Player, 0))),
                new(new BattleSlot(BattleSide.Enemy, 1), new UseMove(0),
                    new ActiveSlotSelection(new BattleSlot(BattleSide.Player, 1))),
            ]));

            Assert.True(guard.IsFainted);
            Assert.Equal(500, ally.CurrentHp);
            return ([.. events.Select(entry => entry.ToString()!)],
                [.. battle.ConditionTrace.Select(entry => entry.ToString()!)],
                [.. battle.Trace.Select(entry => entry.ToString()!)]);
        }

        var first = Run();
        var second = Run();
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Conditions, second.Conditions);
        Assert.Equal(first.Effects, second.Effects);
    }

    private static IReadOnlyList<BattleHookFilter> Filters(BattleConditionInstance condition,
        BattleMove move, int effectivePriority, string ruleset) => SideConditions.CollectProtectionHooks(
        [condition], new BattleSlot(BattleSide.Enemy, 0), new BattleSlot(BattleSide.Player, 0),
        move, effectivePriority, ruleset, bypass: false, actionSequence: 1).Filters();

    private static bool Blocked(BattleSideCondition condition, BattleMove move,
        BattleSlot source, BattleSlot target) => SideConditions.CollectProtectionHooks(
        [Condition(target.Side, condition)], source, target, move, move.Priority,
        BattleRulesets.ModernReference, bypass: false, actionSequence: 1).Filters().Count > 0;

    private static AiCandidateScore Score(BattleCreature attacker, BattleCreature defender,
        IReadOnlyList<BattleConditionInstance> conditions) => SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [defender], 0, Chart(), new CountingRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Conditions: conditions,
            Ruleset: BattleRulesets.ModernReference)).Scores.Single();

    private static BattleSideCondition[] ProtectionRows() =>
    [
        BattleSideCondition.PriorityProtection,
        BattleSideCondition.MultiTargetProtection,
        BattleSideCondition.StatusProtection,
        BattleSideCondition.DamageProtection,
    ];

    private static BattleConditionInstance Condition(BattleSide side, BattleSideCondition condition) => new(
        1, SideConditions.For(condition), SideConditions.Owner(side),
        new BattleConditionSource(new BattleSlot(side, 0), 0), 0, 0, 1,
        SideConditions.For(condition).Tags, new Dictionary<string, int>(), 1);

    private static BattleMove Guard(string slug, BattleSideCondition condition, params MoveEffect[] extra) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 4, 0,
        target: MoveTarget.UsersField,
        secondaryEffects: [new SetSideConditionEffect(condition, 1), .. extra]);

    private static BattleMove Hit(string slug, MoveTarget target, int priority = 0, int? accuracy = null) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Physical, 60, accuracy, 20, priority, 0,
        target: target);

    private static BattleMove Status(string slug, MoveTarget target) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: target);

    private static BattleMove Wait(string slug) => Status(slug, MoveTarget.User);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
        new Stats(500, 100, 100, 100, 100, speed), moves);

    private static BattleTurnActions Actions(BattleAction player0, BattleAction player1,
        BattleAction enemy0, BattleAction enemy1) => new(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), player0),
            new(new BattleSlot(BattleSide.Player, 1), player1),
            new(new BattleSlot(BattleSide.Enemy, 0), enemy0),
            new(new BattleSlot(BattleSide.Enemy, 1), enemy1),
        ]);

    private static Move DataMove(BattleSideCondition condition, int? duration = null)
    {
        var values = new List<(string Key, object Value)> { ("condition", condition.ToString()) };
        if (duration is not null)
            values.Add(("duration", duration.Value));
        return new Move
        {
            Id = EntityId.Parse("move:data"),
            Name = "Data",
            Type = Normal,
            DamageClass = DamageClass.Status,
            Pp = 10,
            Priority = 3,
            Target = MoveTarget.UsersField,
            Effects = [Op("sideCondition", [.. values])],
        };
    }

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private sealed class CountingRng : IRng
    {
        public int IntDraws { get; private set; }
        public int DoubleDraws { get; private set; }

        public int Next(int maxExclusive)
        {
            IntDraws++;
            return maxExclusive == 16 ? 15 : 0;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            IntDraws++;
            return minInclusive;
        }

        public double NextDouble()
        {
            DoubleDraws++;
            return 0.99;
        }
    }
}
