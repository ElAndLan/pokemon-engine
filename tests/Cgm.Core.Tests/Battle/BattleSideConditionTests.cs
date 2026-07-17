using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleSideConditionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    [Fact]
    public void Compiler_AdmitsStrictScreenRowsAndRejectsWrongShapes()
    {
        Move screen = DataMove("sideCondition", DamageClass.Status, MoveTarget.UsersField,
            ("condition", "physicalScreen"), ("duration", 7));
        Assert.Contains(MoveCompiler.ToBattleMove(screen).SecondaryEffects,
            effect => effect is SetSideConditionEffect
                { Condition: BattleSideCondition.PhysicalScreen, Duration: 7 });

        Move bypass = DataMove("sideConditionBypass", DamageClass.Physical, MoveTarget.Selected,
            ("tag", "screen")) with { Power = 80 };
        Assert.IsType<SideConditionBypassEffect>(Assert.Single(MoveCompiler.ToBattleMove(bypass).SecondaryEffects));

        Move remove = DataMove("removeSideCondition", DamageClass.Physical, MoveTarget.Selected,
            ("tag", "screen"), ("side", "target"), ("timing", "beforeDamage")) with { Power = 80 };
        Assert.IsType<RemoveSideConditionEffect>(Assert.Single(MoveCompiler.ToBattleMove(remove).SecondaryEffects));

        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(
            DataMove("sideCondition", DamageClass.Status, MoveTarget.Selected, ("condition", "physicalScreen"))));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(
            DataMove("sideConditionBypass", DamageClass.Physical, MoveTarget.Selected, ("tag", "other")) with { Power = 80 }));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(
            DataMove("removeSideCondition", DamageClass.Status, MoveTarget.Selected,
                ("tag", "screen"), ("side", "target"), ("timing", "beforeDamage"))));
    }

    [Fact]
    public void Screens_AreSideOwnedCoexistAndRejectDuplicateWithoutRefresh()
    {
        BattleCreature source = Creature("source", 100,
            Screen(BattleSideCondition.PhysicalScreen, "physical"),
            Screen(BattleSideCondition.SpecialScreen, "special"));
        var battle = new BattleController(source, Creature("target", 1, Inert()), Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Equal(2, battle.ConditionSnapshot.Count);
        Assert.All(battle.ConditionSnapshot, row => Assert.Equal(SideConditions.Owner(BattleSide.Player), row.Owner));

        IReadOnlyList<BattleEvent> duplicate = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(2, battle.ConditionSnapshot.Single(row =>
            row.Definition.Id == SideConditions.For(BattleSideCondition.PhysicalScreen).Id).RemainingDuration);
        Assert.Contains(duplicate, row => row is MoveFailed { Reason: MoveFailureReason.ConditionAlreadyActive });
        Assert.Contains(battle.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Rejected);
    }

    [Theory]
    [InlineData(DamageClass.Physical, BattleSideCondition.PhysicalScreen)]
    [InlineData(DamageClass.Special, BattleSideCondition.SpecialScreen)]
    [InlineData(DamageClass.Physical, BattleSideCondition.AllDamageScreen)]
    [InlineData(DamageClass.Special, BattleSideCondition.AllDamageScreen)]
    public void Screens_FilterDamageClass(DamageClass damageClass, BattleSideCondition condition)
    {
        int clear = Damage(damageClass, null);
        int screened = Damage(damageClass, condition);
        Assert.InRange(screened, clear / 2 - 1, clear / 2 + 1);
    }

    [Fact]
    public void Screen_UsesTwoThirdsInDoublesEvenForOneSelectedTarget()
    {
        int clear = DoublesDamage(setScreen: false);
        int screened = DoublesDamage(setScreen: true);
        Assert.InRange(screened, clear * 2 / 3 - 1, clear * 2 / 3 + 1);
    }

    [Fact]
    public void CriticalAndExplicitBypassIgnoreScreen()
    {
        BattleConditionInstance condition = Condition(BattleSide.Enemy, BattleSideCondition.PhysicalScreen);
        BattleHookDispatchSnapshot normal = SideConditions.CollectDamageHooks([condition], BattleSide.Enemy,
            DamageClass.Physical, 1, critical: false, bypass: false, 0);
        Assert.Single(normal.QueryModifiers(BattleQueryId.FinalDamage));
        Assert.DoesNotContain(SideConditions.CollectDamageHooks([condition], BattleSide.Enemy,
            DamageClass.Physical, 1, critical: true, bypass: false, 0).QueryModifiers(BattleQueryId.FinalDamage),
            _ => true);

        int clear = Damage(DamageClass.Physical, null);
        int bypass = Damage(DamageClass.Physical, BattleSideCondition.PhysicalScreen,
            new SideConditionBypassEffect("screen"));
        Assert.Equal(clear, bypass);
        Assert.Equal(CriticalDamage(null), CriticalDamage(BattleSideCondition.PhysicalScreen));
    }

    [Fact]
    public void AbilityBypassIgnoresScreenWithoutRemovingIt()
    {
        var bypass = new Effect { Op = "sideConditionBypass", Params = Params(("tag", "screen")) };
        BattleCreature attacker = CreatureWithAbility("attacker", 100,
            [new AbilityHook { Hook = AbilityHookPoint.OnModifyOutgoingDamage, Effects = [bypass] }],
            Inert("setup"), Hit("hit", DamageClass.Physical));
        BattleCreature target = Creature("target", 1,
            Screen(BattleSideCondition.PhysicalScreen, "screen"), Inert("wait"));
        var battle = new BattleController(attacker, target, Chart(), new Rng(17));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int before = target.CurrentHp;
        battle.ResolveTurn(new UseMove(1), new UseMove(1));

        Assert.Equal(Damage(DamageClass.Physical, null), before - target.CurrentHp);
        Assert.Contains(battle.ConditionSnapshot,
            row => row.Definition.Id == SideConditions.For(BattleSideCondition.PhysicalScreen).Id);
    }

    [Fact]
    public void BeforeDamageRemovalMakesSameHitUnmitigatedAndTracesNoOp()
    {
        var removal = new RemoveSideConditionEffect("screen", SideConditionTarget.Target,
            SideConditionTiming.BeforeDamage);
        int clear = Damage(DamageClass.Physical, null);
        BattleController battle = DamageBattle(BattleSideCondition.PhysicalScreen, removal);
        BattleCreature target = battle.Active(BattleSide.Enemy);
        int before = target.CurrentHp;
        battle.ResolveTurn(new UseMove(1), new UseMove(1));

        Assert.Equal(clear, before - target.CurrentHp);
        Assert.DoesNotContain(battle.ConditionSnapshot,
            row => row.Definition.Scope == BattleConditionScope.Side);
        Assert.Contains(battle.Trace, row => row.Kind == EffectTraceKind.ConditionRemoval && row.Value == 1);

        battle.ResolveTurn(new UseMove(1), new UseMove(1));
        Assert.Contains(battle.Trace, row => row.Kind == EffectTraceKind.ConditionRemoval && row.Value == 0);
    }

    [Fact]
    public void AfterHitRemovalMitigatesTheHitThenRemovesTheScreen()
    {
        var removal = new RemoveSideConditionEffect("screen", SideConditionTarget.Target,
            SideConditionTiming.AfterHit);
        int clear = Damage(DamageClass.Physical, null);
        BattleController battle = DamageBattle(BattleSideCondition.PhysicalScreen, removal);
        BattleCreature target = battle.Active(BattleSide.Enemy);
        int before = target.CurrentHp;
        battle.ResolveTurn(new UseMove(1), new UseMove(1));

        Assert.InRange(before - target.CurrentHp, clear / 2 - 1, clear / 2 + 1);
        Assert.DoesNotContain(battle.ConditionSnapshot,
            row => row.Definition.Scope == BattleConditionScope.Side);
        Assert.Contains(battle.Trace, row => row.Kind == EffectTraceKind.ConditionRemoval && row.Value == 1);
    }

    [Fact]
    public void OneTurnScreenExpiresAtItsFirstTurnEndCheckpoint()
    {
        BattleCreature source = Creature("source", 100,
            Screen(BattleSideCondition.PhysicalScreen, "short", duration: 1));
        var battle = new BattleController(source, Creature("target", 1, Inert()), Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.DoesNotContain(battle.ConditionSnapshot,
            row => row.Definition.Scope == BattleConditionScope.Side);
        Assert.Contains(battle.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Expired);
    }

    [Fact]
    public void AllDamageScreenRequiresModernSnowAndHeldEffectExtendsDuration()
    {
        IReadOnlyList<Effect> held = [new Effect { Op = "sideConditionDurationExtend",
            Params = Params(("tag", "screen"), ("turns", 3)) }];
        BattleCreature source = Creature("source", 100, held,
            Screen(BattleSideCondition.AllDamageScreen, "all"));
        var invalid = new BattleController(source, Creature("target", 1, Inert()), Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference));
        Assert.Contains(invalid.ResolveTurn(new UseMove(0), new UseMove(0)),
            row => row is MoveFailed { Reason: MoveFailureReason.ConditionRequirementNotMet });
        Assert.Empty(invalid.ConditionSnapshot);

        source = Creature("snow_source", 100, held, Screen(BattleSideCondition.AllDamageScreen, "snow_all"));
        var valid = new BattleController(source, Creature("snow_target", 1, Inert()), Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference, Weather.Snow));
        valid.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(7, valid.ConditionSnapshot.Single(row => row.Definition.Scope == BattleConditionScope.Side)
            .RemainingDuration);
    }

    [Fact]
    public void SmartAi_UsesScreenTopologyAndBypassDamageQueries()
    {
        BattleCreature attacker = Creature("ai", 100, Hit("hit", DamageClass.Physical));
        BattleCreature bypass = Creature("bypass", 100,
            Hit("bypass_hit", DamageClass.Physical, new SideConditionBypassEffect("screen")));
        BattleCreature defender = Creature("defender", 1, Inert());
        BattleConditionInstance condition = Condition(BattleSide.Player, BattleSideCondition.PhysicalScreen);

        double clear = Score(attacker, defender, [], 1);
        double singles = Score(attacker, defender, [condition], 1);
        double doubles = Score(attacker, defender, [condition], 2);
        double ignored = Score(bypass, defender, [condition], 1);

        Assert.InRange(singles, clear / 2 - 1, clear / 2 + 1);
        Assert.InRange(doubles, clear * 2 / 3 - 1, clear * 2 / 3 + 1);
        Assert.Equal(clear, ignored, 1);
    }

    [Fact]
    public void ScreenResolution_IsReplayStableAndAddsNoRngDraws()
    {
        static (IReadOnlyList<BattleEvent> Events, IReadOnlyList<EffectTraceEntry> Effects,
            IReadOnlyList<BattleQueryTraceEntry> Queries, int Draws) Run(bool screened)
        {
            BattleCreature attacker = Creature("replay_attacker", 100, Inert("setup"),
                Hit("hit", DamageClass.Physical));
            BattleCreature target = Creature("replay_target", 1,
                screened ? Screen(BattleSideCondition.PhysicalScreen, "screen") : Inert("target_setup"),
                Inert("wait"));
            var rng = new CountingRng(23);
            var battle = new BattleController(attacker, target, Chart(), rng);
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            battle.ResolveTurn(new UseMove(1), new UseMove(1));
            if (screened)
                Assert.Contains(battle.QueryTrace, row => row.Result.Query == BattleQueryId.FinalDamage
                    && row.Result.Steps.Any(step => step.OwnerScope == BattleQueryOwnerScope.TargetSide));
            return (battle.Log.ToArray(), battle.Trace.ToArray(), battle.QueryTrace.ToArray(), rng.Draws);
        }

        var first = Run(screened: true);
        var second = Run(screened: true);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Effects, second.Effects);
        Assert.Equal(JsonSerializer.Serialize(first.Queries), JsonSerializer.Serialize(second.Queries));
        Assert.Equal(Run(screened: false).Draws, first.Draws);
    }

    private static int Damage(DamageClass damageClass, BattleSideCondition? condition,
        params MoveEffect[] effects)
    {
        BattleController battle = DamageBattle(condition, effects.Length == 0 ? null : effects[0], damageClass);
        BattleCreature target = battle.Active(BattleSide.Enemy);
        int before = target.CurrentHp;
        battle.ResolveTurn(new UseMove(1), new UseMove(1));
        return before - target.CurrentHp;
    }

    private static BattleController DamageBattle(BattleSideCondition? condition, MoveEffect? effect = null,
        DamageClass damageClass = DamageClass.Physical)
    {
        BattleCreature source = Creature("source", 100, Inert("setup"),
            Hit("hit", damageClass, effect is null ? [] : [effect]));
        BattleCreature target = Creature("target", 1,
            condition is null ? Inert("target_setup") : Screen(condition.Value, "target_screen"),
            Inert("target_wait"));
        var battle = new BattleController(source, target, Chart(), new Rng(17),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference,
                condition == BattleSideCondition.AllDamageScreen ? Weather.Snow : Weather.None));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        return battle;
    }

    private static int DoublesDamage(bool setScreen)
    {
        BattleCreature attacker = Creature("attacker", 100, Inert("setup"), Hit("hit", DamageClass.Physical));
        BattleCreature ally = Creature("ally", 90, Inert());
        BattleCreature target = Creature("target", 10,
            setScreen ? Screen(BattleSideCondition.PhysicalScreen, "screen") : Inert("target_setup"),
            Inert("target_wait"));
        BattleCreature targetAlly = Creature("target_ally", 5, Inert());
        var battle = new BattleController([attacker, ally], [target, targetAlly], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new Rng(17));
        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
            [new(new BattleSlot(BattleSide.Player, 0), new Pass()), new(new BattleSlot(BattleSide.Player, 1), new Pass()),
             new(new BattleSlot(BattleSide.Enemy, 0), setScreen ? new UseMove(0) : new Pass()),
             new(new BattleSlot(BattleSide.Enemy, 1), new Pass())]));
        int before = target.CurrentHp;
        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
            [new(new BattleSlot(BattleSide.Player, 0), new UseMove(1), new BattleSlot(BattleSide.Enemy, 0)),
             new(new BattleSlot(BattleSide.Player, 1), new Pass()), new(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
             new(new BattleSlot(BattleSide.Enemy, 1), new Pass())]));
        return before - target.CurrentHp;
    }

    private static int CriticalDamage(BattleSideCondition? condition)
    {
        BattleCreature attacker = Creature("critical_attacker", 100, Inert("setup"), Hit("critical_hit", DamageClass.Physical));
        BattleCreature target = Creature("critical_target", 1,
            condition is null ? Inert("target_setup") : Screen(condition.Value, "target_screen"),
            Inert("target_wait"));
        var battle = new BattleController(attacker, target, Chart(),
            new FakeRng(ints: [0, 15], doubles: [0]));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int before = target.CurrentHp;
        battle.ResolveTurn(new UseMove(1), new UseMove(1));
        Assert.Contains(battle.Log, row => row is DamageDealt { Crit: true });
        return before - target.CurrentHp;
    }

    private static double Score(BattleCreature attacker, BattleCreature defender,
        IReadOnlyList<BattleConditionInstance> conditions, int activeSlots) => SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [defender], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Conditions: conditions,
            Ruleset: BattleRulesets.ModernReference, ActiveSlotsPerSide: activeSlots)).Scores.Single().Components
        .Single(component => component.Name == "damage").Value;

    private static BattleConditionInstance Condition(BattleSide side, BattleSideCondition condition) => new(0,
        SideConditions.For(condition), SideConditions.Owner(side), new BattleConditionSource(), 0, 0, 5,
        ["screen"], new Dictionary<string, int>(), 1);

    private static BattleMove Screen(BattleSideCondition condition, string slug, int duration = 5) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.UsersField, secondaryEffects: [new SetSideConditionEffect(condition, duration)]);
    private static BattleMove Hit(string slug, DamageClass damageClass, params MoveEffect[] effects) => new(
        EntityId.Parse($"move:{slug}"), Normal, damageClass, 90, 100, 20, 0, 0, secondaryEffects: effects);
    private static BattleMove Inert(string slug = "inert") => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0);
    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) =>
        Creature(slug, speed, null, moves);
    private static BattleCreature Creature(string slug, int speed, IReadOnlyList<Effect>? held,
        params BattleMove[] moves) => new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(500, 120, 120, 120, 120, speed), moves, heldItemBattleEffects: held);
    private static BattleCreature CreatureWithAbility(string slug, int speed,
        IReadOnlyList<AbilityHook> abilityHooks, params BattleMove[] moves) => new(
            EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(500, 120, 120, 120, 120, speed), moves, abilityHooks: abilityHooks);

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
