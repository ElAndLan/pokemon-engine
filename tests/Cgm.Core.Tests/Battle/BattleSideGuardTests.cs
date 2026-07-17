using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleSideGuardTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    [Fact]
    public void Compiler_AdmitsGuardRowsAndClosedBypassRemovalTags()
    {
        Move status = DataMove("sideCondition", DamageClass.Status, MoveTarget.UsersField,
            ("condition", "statusGuard"));
        Move stage = DataMove("sideCondition", DamageClass.Status, MoveTarget.UsersField,
            ("condition", "stageDropGuard"), ("duration", 7));
        Assert.Contains(MoveCompiler.ToBattleMove(status).SecondaryEffects,
            effect => effect is SetSideConditionEffect { Condition: BattleSideCondition.StatusGuard, Duration: 5 });
        Assert.Contains(MoveCompiler.ToBattleMove(stage).SecondaryEffects,
            effect => effect is SetSideConditionEffect { Condition: BattleSideCondition.StageDropGuard, Duration: 7 });

        Move bypass = DataMove("sideConditionBypass", DamageClass.Status, MoveTarget.Selected,
            ("tag", "status_guard")) with
        {
            Effects =
            [
                new Effect { Op = "sideConditionBypass", Params = Params(("tag", "status_guard")) },
                new Effect { Op = "ailment", Params = Params(("ailment", "poison")) },
            ],
        };
        Assert.Contains(MoveCompiler.ToBattleMove(bypass).SecondaryEffects,
            effect => effect is SideConditionBypassEffect { Tag: "status_guard" });
        Move stageBypass = DataMove("sideConditionBypass", DamageClass.Status, MoveTarget.Selected,
            ("tag", "stage_guard")) with
        {
            Effects =
            [
                new Effect { Op = "sideConditionBypass", Params = Params(("tag", "stage_guard")) },
                new Effect { Op = "statStage", Params = Params(("stat", "atk"), ("delta", -1)) },
            ],
        };
        Assert.Contains(MoveCompiler.ToBattleMove(stageBypass).SecondaryEffects,
            effect => effect is SideConditionBypassEffect { Tag: "stage_guard" });
        Move remove = DataMove("removeSideCondition", DamageClass.Status, MoveTarget.Selected,
            ("tag", "barrier"), ("side", "target"), ("timing", "afterHit"));
        Assert.Contains(MoveCompiler.ToBattleMove(remove).SecondaryEffects,
            effect => effect is RemoveSideConditionEffect { Tag: "barrier" });

        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(
            DataMove("sideConditionBypass", DamageClass.Special, MoveTarget.Selected,
                ("tag", "barrier")) with { Power = 40 }));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(
            DataMove("sideConditionBypass", DamageClass.Status, MoveTarget.Selected,
                ("tag", "stage_guard"))));
    }

    [Fact]
    public void Guards_CoexistRejectDuplicatesAndRemainSideOwned()
    {
        BattleCreature source = Creature("source", 100,
            Guard(BattleSideCondition.StatusGuard, "status"),
            Guard(BattleSideCondition.StageDropGuard, "stage"));
        source.SetStatus(PersistentStatus.Burn);
        var battle = new BattleController(source, Creature("target", 1, Wait()), Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        IReadOnlyList<BattleEvent> duplicate = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2, battle.ConditionSnapshot.Count(row => row.Definition.Scope == BattleConditionScope.Side));
        Assert.All(battle.ConditionSnapshot.Where(row => row.Definition.Scope == BattleConditionScope.Side),
            row => Assert.Equal(SideConditions.Owner(BattleSide.Player), row.Owner));
        Assert.Contains(duplicate, row => row is MoveFailed { Reason: MoveFailureReason.ConditionAlreadyActive });
        Assert.Equal(PersistentStatus.Burn, source.Status);
    }

    [Fact]
    public void StatusGuard_BlocksPersistentAndConfusionBeforeTheirRng()
    {
        BattleMove status = StatusMove("status", chance: 50);
        BattleMove confusion = ConfusionMove("confusion", chance: 100);
        BattleCreature source = Creature("source", 100, Wait("setup"), status, confusion);
        BattleCreature target = Creature("target", 1,
            Guard(BattleSideCondition.StatusGuard, "guard", duration: 8), Wait());
        var battle = new BattleController(source, target, Chart(), new FakeRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(1));
        battle.ResolveTurn(new UseMove(2), new UseMove(1));

        Assert.Null(target.Status);
        Assert.False(target.IsConfused);
        Assert.True(battle.HookTrace.Count(row => row.Checkpoint == BattleConditionHook.StatusAttempt
            && row.PayloadKind == BattleHookPayloadKind.Filter && row.Invoked) >= 2);
        Assert.DoesNotContain(battle.Trace, row => row.Kind == EffectTraceKind.ConfusionDuration);
    }

    [Fact]
    public void StatusGuard_AllowsSelfOriginAndExplicitBypass()
    {
        BattleMove selfStatus = new(EntityId.Parse("move:self_status"), Normal, DamageClass.Status,
            null, null, 20, 0, 0, ailment: PersistentStatus.Poison, ailmentChance: 100,
            target: MoveTarget.User,
            secondaryEffects: [new AilmentEffect(PersistentStatus.Poison)]);
        BattleCreature self = Creature("self", 100,
            Guard(BattleSideCondition.StatusGuard, "guard"), selfStatus);
        var selfBattle = new BattleController(self, Creature("other", 1, Wait()), Chart(), new Rng(2));
        selfBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        selfBattle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Equal(PersistentStatus.Poison, self.Status);

        BattleMove bypass = StatusMove("bypass", 100, new SideConditionBypassEffect("status_guard"));
        BattleCreature source = Creature("source", 100, Wait("setup"), bypass);
        BattleCreature target = Creature("target", 1,
            Guard(BattleSideCondition.StatusGuard, "target_guard"), Wait());
        var bypassBattle = new BattleController(source, target, Chart(), new Rng(2));
        bypassBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        bypassBattle.ResolveTurn(new UseMove(1), new UseMove(1));
        Assert.Equal(PersistentStatus.Poison, target.Status);

        var bypassPayload = new Effect { Op = "sideConditionBypass",
            Params = Params(("tag", "status_guard")) };
        BattleCreature abilitySource = CreatureWithAbility("ability_source", 100,
            [new AbilityHook
            {
                Hook = AbilityHookPoint.OnModifyOutgoingDamage,
                Effects = [bypassPayload],
            }], Wait("ability_setup"), StatusMove("ability_status", 100));
        BattleCreature abilityTarget = Creature("ability_target", 1,
            Guard(BattleSideCondition.StatusGuard, "ability_guard"), Wait("ability_wait"));
        var abilityBattle = new BattleController(abilitySource, abilityTarget, Chart(), new Rng(2));
        abilityBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        abilityBattle.ResolveTurn(new UseMove(1), new UseMove(1));
        Assert.Equal(PersistentStatus.Poison, abilityTarget.Status);
    }

    [Fact]
    public void StageGuard_BlocksNegativeSingleAndBundleButNotPositiveResetOrSelfDrop()
    {
        BattleCreature source = Creature("source", 100, Wait("setup"),
            StageMove("single", -1, chance: 50), StageAllMove("bundle", -1, chance: 50),
            StageMove("positive", 1), ResetMove("reset"), StageBypassMove("bypass"));
        BattleCreature target = Creature("target", 1,
            Guard(BattleSideCondition.StageDropGuard, "guard", duration: 10), Wait());
        var battle = new BattleController(source, target, Chart(), new FakeRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(1));
        battle.ResolveTurn(new UseMove(2), new UseMove(1));
        Assert.All(new[] { StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe },
            stat => Assert.Equal(0, target.Stage(stat)));

        battle.ResolveTurn(new UseMove(3), new UseMove(1));
        Assert.Equal(1, target.Stage(StatKind.Atk));
        battle.ResolveTurn(new UseMove(4), new UseMove(1));
        Assert.Equal(0, target.Stage(StatKind.Atk));
        battle.ResolveTurn(new UseMove(5), new UseMove(1));
        Assert.Equal(-1, target.Stage(StatKind.Atk));

        BattleMove selfDrop = new(EntityId.Parse("move:self_drop"), Normal, DamageClass.Status,
            null, null, 20, 0, 0, target: MoveTarget.User,
            secondaryEffects: [new StatChangeEffect(StatKind.Atk, -1, OnSelf: true)]);
        BattleCreature guarded = Creature("guarded", 100,
            Guard(BattleSideCondition.StageDropGuard, "self_guard"), selfDrop);
        var selfBattle = new BattleController(guarded, Creature("other", 1, Wait()), Chart(), new Rng(3));
        selfBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        selfBattle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Equal(-1, guarded.Stage(StatKind.Atk));
    }

    [Fact]
    public void Guards_BlockOpposingContactPayloadsBeforeChance()
    {
        var contactStatus = new Effect
        {
            Op = "contactChanceEffect", Chance = 50,
            Params = Params(("status", "poison")),
        };
        var contactStage = new Effect
        {
            Op = "contactChanceEffect", Chance = 50,
            Params = Params(("stat", "atk"), ("delta", -1)),
        };
        BattleCreature source = Creature("source", 100,
            Guard(BattleSideCondition.StatusGuard, "status"),
            Guard(BattleSideCondition.StageDropGuard, "stage"), ContactHit("contact"));
        BattleCreature target = CreatureWithAbility("target", 1,
            [new AbilityHook { Hook = AbilityHookPoint.OnContactReceived, Effects = [contactStatus, contactStage] }],
            Wait());
        var battle = new BattleController(source, target, Chart(), new Rng(8));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        battle.ResolveTurn(new UseMove(2), new UseMove(0));

        Assert.Null(source.Status);
        Assert.Equal(0, source.Stage(StatKind.Atk));
        Assert.Equal(2, battle.Trace.Count(row => row.Kind == EffectTraceKind.ContactChance
            && !row.Performed && row.DrawResult is null));
    }

    [Fact]
    public void BarrierRemovalClearsScreensAndGuardsTogether()
    {
        BattleMove remove = new(EntityId.Parse("move:remove"), Normal, DamageClass.Status,
            null, null, 20, 0, 0, target: MoveTarget.Selected,
            secondaryEffects: [new RemoveSideConditionEffect("barrier", SideConditionTarget.Target,
                SideConditionTiming.AfterHit)]);
        BattleCreature source = Creature("source", 100, Wait("wait0"), Wait("wait1"), Wait("wait2"), remove);
        BattleCreature target = Creature("target", 1,
            Guard(BattleSideCondition.StatusGuard, "status"),
            Guard(BattleSideCondition.StageDropGuard, "stage"),
            Guard(BattleSideCondition.PhysicalScreen, "screen"), Wait());
        var battle = new BattleController(source, target, Chart(), new Rng(4));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(1));
        battle.ResolveTurn(new UseMove(2), new UseMove(2));
        battle.ResolveTurn(new UseMove(3), new UseMove(3));

        Assert.DoesNotContain(battle.ConditionSnapshot, row => row.Definition.Scope == BattleConditionScope.Side);
        Assert.Contains(battle.Trace, row => row.Kind == EffectTraceKind.ConditionRemoval && row.Value == 3);
    }

    [Fact]
    public void GuardOwnershipCoversEitherDoublesSlotAndSmartAiUsesTheSameStatusFilter()
    {
        BattleConditionInstance guard = Condition(BattleSide.Player, BattleSideCondition.StatusGuard);
        Assert.Single(SideConditions.CollectStatusHooks([guard], BattleSide.Enemy, BattleSide.Player,
            bypass: false, 0).Filters());
        Assert.Empty(SideConditions.CollectStatusHooks([guard], BattleSide.Player, BattleSide.Player,
            bypass: false, 0).Filters());
        BattleConditionInstance stageGuard = Condition(BattleSide.Player, BattleSideCondition.StageDropGuard);
        Assert.Empty(SideConditions.CollectStageDropHooks([stageGuard], BattleSide.Player, BattleSide.Player,
            bypass: false, 0).Filters());

        BattleCreature attacker = Creature("ai", 100, StatusMove("status", 100));
        BattleCreature defender = Creature("defender", 1, Wait());
        AiCandidateScore clear = Score(attacker, defender, []);
        AiCandidateScore blocked = Score(attacker, defender, [guard]);
        Assert.Contains(clear.Components, component => component.Name == "status" && component.Value > 0);
        Assert.DoesNotContain(blocked.Components, component => component.Name == "status");

        BattleCreature bypass = Creature("bypass", 100,
            StatusMove("bypass", 100, new SideConditionBypassEffect("status_guard")));
        Assert.Contains(Score(bypass, defender, [guard]).Components,
            component => component.Name == "status" && component.Value > 0);
    }

    [Fact]
    public void StatusGuard_OnOneDoublesSideProtectsTheOtherActiveSlot()
    {
        BattleCreature source = Creature("source", 100, Wait("setup"), StatusMove("status", 50));
        BattleCreature sourceAlly = Creature("source_ally", 90, Wait("source_ally_wait"));
        BattleCreature guardSource = Creature("guard_source", 20,
            Guard(BattleSideCondition.StatusGuard, "guard"), Wait("guard_wait"));
        BattleCreature guardedAlly = Creature("guarded_ally", 10, Wait("guarded_wait"));
        var battle = new BattleController([source, sourceAlly], [guardSource, guardedAlly],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng());

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), new UseMove(0)),
            new(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 0), new UseMove(0)),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]));
        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), new UseMove(1), new BattleSlot(BattleSide.Enemy, 1)),
            new(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 0), new UseMove(1)),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Null(guardedAlly.Status);
        Assert.Contains(battle.HookTrace, row => row.Checkpoint == BattleConditionHook.StatusAttempt
            && row.PayloadKind == BattleHookPayloadKind.Filter && row.Invoked);
    }

    [Fact]
    public void GuardResolution_IsDeterministicAndAddsNoGuardRng()
    {
        static (IReadOnlyList<BattleEvent> Events, IReadOnlyList<EffectTraceEntry> Trace, int Draws) Run(bool guarded)
        {
            BattleCreature source = Creature("source", 100, Wait("setup"), StatusMove("status", 50));
            BattleCreature target = Creature("target", 1,
                guarded ? Guard(BattleSideCondition.StatusGuard, "guard") : Wait("target_setup"), Wait());
            var rng = new CountingRng(31);
            var battle = new BattleController(source, target, Chart(), rng);
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            battle.ResolveTurn(new UseMove(1), new UseMove(1));
            return (battle.Log.ToArray(), battle.Trace.ToArray(), rng.Draws);
        }

        var first = Run(guarded: true);
        var second = Run(guarded: true);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(0, first.Draws);
        Assert.True(Run(guarded: false).Draws > first.Draws);
    }

    private static AiCandidateScore Score(BattleCreature attacker, BattleCreature defender,
        IReadOnlyList<BattleConditionInstance> conditions) => SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [defender], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Conditions: conditions,
            Ruleset: BattleRulesets.ModernReference)).Scores.Single();

    private static BattleConditionInstance Condition(BattleSide side, BattleSideCondition condition) => new(0,
        SideConditions.For(condition), SideConditions.Owner(side), new BattleConditionSource(), 0, 0, 5,
        SideConditions.For(condition).Tags, new Dictionary<string, int>(), 1);

    private static BattleMove Guard(BattleSideCondition condition, string slug, int duration = 5) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.UsersField, secondaryEffects: [new SetSideConditionEffect(condition, duration)]);
    private static BattleMove StatusMove(string slug, int chance, params MoveEffect[] extra) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        ailment: PersistentStatus.Poison, ailmentChance: chance, target: MoveTarget.Selected,
        secondaryEffects: [.. extra, new AilmentEffect(PersistentStatus.Poison) { Chance = chance }]);
    private static BattleMove ConfusionMove(string slug, int chance) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        confuseChance: chance, target: MoveTarget.Selected,
        secondaryEffects: [new ConfusionEffect { Chance = chance }]);
    private static BattleMove StageMove(string slug, int delta, int chance = 100) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        stageEffect: new StageEffect(StatKind.Atk, delta, false, chance), target: MoveTarget.Selected,
        secondaryEffects: [new StatChangeEffect(StatKind.Atk, delta, OnSelf: false) { Chance = chance }]);
    private static BattleMove StageAllMove(string slug, int delta, int chance) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        stageAllEffect: new StageAllEffect(delta, false, chance), target: MoveTarget.Selected,
        secondaryEffects: [new StatChangeAllEffect(delta, OnSelf: false) { Chance = chance }]);
    private static BattleMove StageBypassMove(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        stageEffect: new StageEffect(StatKind.Atk, -1, false, 100), target: MoveTarget.Selected,
        secondaryEffects:
        [
            new SideConditionBypassEffect("stage_guard"),
            new StatChangeEffect(StatKind.Atk, -1, OnSelf: false),
        ]);
    private static BattleMove ResetMove(string slug) => new(EntityId.Parse($"move:{slug}"), Normal,
        DamageClass.Status, null, null, 20, 0, 0, target: MoveTarget.Selected,
        secondaryEffects: [new StatResetEffect(StageEffectScope.Target)]);
    private static BattleMove ContactHit(string slug) => new(EntityId.Parse($"move:{slug}"), Normal,
        DamageClass.Physical, 40, 100, 20, 0, 0, makesContact: true);
    private static BattleMove Wait(string slug = "wait") => new(EntityId.Parse($"move:{slug}"), Normal,
        DamageClass.Status, null, null, 20, 0, 0, target: MoveTarget.User);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
        new Stats(400, 100, 100, 100, 100, speed), moves);
    private static BattleCreature CreatureWithAbility(string slug, int speed,
        IReadOnlyList<AbilityHook> abilityHooks, params BattleMove[] moves) => new(
            EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(400, 100, 100, 100, 100, speed), moves, abilityHooks: abilityHooks);

    private static Move DataMove(string op, DamageClass damageClass, MoveTarget target,
        params (string Key, object Value)[] values) => new()
    {
        Id = EntityId.Parse("move:data"), Name = "Data", Type = Normal, DamageClass = damageClass,
        Target = target, Accuracy = 100, Pp = 10,
        Effects = [new Effect { Op = op, Params = Params(values) }],
    };
    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(row => row.Key, row => JsonSerializer.SerializeToElement(row.Value));

    private sealed class CountingRng(int seed) : IRng
    {
        private readonly Rng _inner = new(seed);
        public int Draws { get; private set; }
        public int Next(int maxExclusive) { Draws++; return _inner.Next(maxExclusive); }
        public int Next(int minInclusive, int maxExclusive) { Draws++; return _inner.Next(minInclusive, maxExclusive); }
        public double NextDouble() { Draws++; return _inner.NextDouble(); }
    }
}
