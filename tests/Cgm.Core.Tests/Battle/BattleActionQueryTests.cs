using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleActionQueryTests
{
    private static readonly EntityId Neutral = EntityId.Parse("type:neutral");

    [Fact]
    public void Compiler_AdmitsClosedActionQueryRows()
    {
        BattleMove move = Compile(DamageClass.Physical, MoveTarget.Selected, 60,
            Op("queryModifier", ("query", "accuracy"), ("operation", "multiply"), ("num", 3), ("den", 2)),
            Op("queryModifier", ("query", "criticalChance"), ("operation", "replace"),
                ("num", 1), ("den", 2)),
            Op("queryModifier", ("query", "priority"), ("operation", "add"), ("num", 1)),
            Op("queryModifier", ("query", "finalDamage"), ("operation", "min"), ("num", 90)),
            Op("accuracyRule", ("mode", "ignoreTargetEvasion")));
        BattleMove nextAccuracy = Compile(DamageClass.Status, MoveTarget.Selected, null,
            Op("nextQuery", ("query", "accuracy"), ("duration", 3)));
        BattleMove nextCritical = Compile(DamageClass.Status, MoveTarget.User, null,
            Op("nextQuery", ("query", "criticalChance")));
        BattleMove healing = Compile(DamageClass.Status, MoveTarget.User, null,
            Op("heal", ("num", 1), ("den", 2)),
            Op("queryModifier", ("query", "healing"), ("operation", "replace"), ("num", 0)));

        Assert.Equal(4, move.SecondaryEffects.OfType<MoveQueryModifierEffect>().Count());
        Assert.Contains(move.SecondaryEffects, effect => effect is AccuracyQueryEffect
            { Mode: AccuracyQueryMode.IgnoreTargetEvasion });
        Assert.Contains(move.SecondaryEffects, effect => effect is MoveQueryModifierEffect
            { Query: BattleQueryId.CriticalChance, Operation: BattleQueryOperation.Replace,
              Operand.Numerator: 1, Operand.Denominator: 2 });
        Assert.Contains(nextAccuracy.SecondaryEffects, effect => effect is OneShotQueryEffect
            { Query: OneShotQuery.Accuracy, Duration: 3 });
        Assert.Contains(nextCritical.SecondaryEffects, effect => effect is OneShotQueryEffect
            { Query: OneShotQuery.CriticalChance, Duration: 2 });
        Assert.Contains(healing.SecondaryEffects, effect => effect is MoveQueryModifierEffect
            { Query: BattleQueryId.Healing, Operation: BattleQueryOperation.Replace,
              Operand.Numerator: 0 });
    }

    [Fact]
    public void Compiler_RejectsMalformedOrIncompatibleActionQueryRows()
    {
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Physical, MoveTarget.Selected, 60,
            Op("queryModifier", ("query", "basePower"), ("operation", "add"), ("num", 1))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Physical, MoveTarget.Selected, 60,
            Op("queryModifier", ("query", "accuracy"), ("operation", "add"), ("num", 1), ("den", 2))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Physical, MoveTarget.Selected, 60,
            Op("queryModifier", ("query", "finalDamage"), ("operation", "add"), ("num", -1))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Physical, MoveTarget.Selected, 60,
            Op("queryModifier", ("query", "priority"), ("operation", "add"), ("num", 1)),
            Op("queryModifier", ("query", "priority"), ("operation", "add"), ("num", 2))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Status, MoveTarget.User, null,
            Op("queryModifier", ("query", "healing"), ("operation", "replace"), ("num", 0))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Physical, MoveTarget.User, 60,
            Op("nextQuery", ("query", "criticalChance"))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Status, MoveTarget.Selected, null,
            Op("nextQuery", ("query", "criticalChance"))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Status, MoveTarget.Selected, null,
            Op("nextQuery", ("query", "accuracy"), ("duration", 0))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(DamageClass.Status, MoveTarget.AllOpponents, null,
            Op("nextQuery", ("query", "accuracy"))));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleActionQueries.Validate(
            new MoveQueryModifierEffect(BattleQueryId.BasePower, BattleQueryOperation.Add,
                new BattleQueryValue(1))));
    }

    [Fact]
    public void AccuracyQuery_CoversEveryStagePairAndIgnoreEvasion()
    {
        BattleMove ordinary = Hit("ordinary", 80, 60);
        BattleMove ignore = Hit("ignore", 80, 60,
            new AccuracyQueryEffect(AccuracyQueryMode.IgnoreTargetEvasion));
        BattleCreature source = Creature("source", 100, ordinary, ignore);
        BattleCreature target = Creature("target", 1, Wait());

        for (int accuracyStage = -6; accuracyStage <= 6; accuracyStage++)
        for (int evasionStage = -6; evasionStage <= 6; evasionStage++)
        {
            source.SetStage(StatKind.Accuracy, accuracyStage);
            target.SetStage(StatKind.Evasion, evasionStage);
            BattleQueryValue multiplier = BattleQuery.AccuracyStageMultiplier(accuracyStage, evasionStage);
            int expected = Math.Min(100, (int)(60L * multiplier.Numerator / multiplier.Denominator));
            BattleAccuracyQueryResult result = BattleActionQueries.Accuracy(ordinary, 60, source, target,
                false, false, null, new BattleQueryContext(Source: source, Target: target));
            Assert.Equal(expected, result.Query.FinalValue.ToInt32());
        }

        source.SetStage(StatKind.Accuracy, -2);
        target.SetStage(StatKind.Evasion, 6);
        BattleAccuracyQueryResult ignored = BattleActionQueries.Accuracy(ignore, 60, source, target,
            false, false, null, new BattleQueryContext(Source: source, Target: target));
        Assert.Equal(36, ignored.Query.FinalValue.ToInt32());
    }

    [Fact]
    public void NextAccuracy_ConsumesAtTheSuccessfulQueryAndSkipsOnlyAccuracyDraw()
    {
        BattleMove prepare = new(EntityId.Parse("move:prepare_accuracy"), Neutral, DamageClass.Status,
            null, null, 10, 0, 0, secondaryEffects: [new OneShotQueryEffect(OneShotQuery.Accuracy, 2)]);
        BattleMove hit = Hit("inaccurate", 80, 1,
            new MoveQueryModifierEffect(BattleQueryId.CriticalChance,
                BattleQueryOperation.Replace, new BattleQueryValue(0)));
        BattleCreature source = Creature("source", 100, prepare, hit);
        BattleCreature target = Creature("target", 1, Wait());
        var battle = new BattleController(source, target, Chart(),
            new FakeRng(ints: [15], doubles: [0.5]));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(OneShotQueryConditions.Accuracy.Id, Assert.Single(battle.ConditionSnapshot).Definition.Id);
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Contains(events, item => item is DamageDealt { Slot.Side: BattleSide.Enemy, Amount: > 0 });
        Assert.DoesNotContain(battle.ConditionSnapshot,
            instance => instance.Definition.Id == OneShotQueryConditions.Accuracy.Id);
        Assert.Contains(battle.ConditionTrace, item => item.Condition == OneShotQueryConditions.Accuracy.Id
            && item.Kind == BattleConditionTraceKind.Removed
            && item.CleanupReason == BattleConditionCleanupReason.Effect);
        Assert.Equal(100, battle.QueryTrace.Last(item => item.Result.Query == BattleQueryId.Accuracy)
            .Result.FinalValue.ToInt32());
    }

    [Fact]
    public void NextCritical_ConsumesAfterImmunityAndSkipsTheCriticalDraw()
    {
        BattleMove prepare = new(EntityId.Parse("move:prepare_critical"), Neutral, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.User,
            secondaryEffects: [new OneShotQueryEffect(OneShotQuery.CriticalChance, 2)]);
        BattleMove hit = Hit("critical_hit");
        var battle = new BattleController(Creature("source", 100, prepare, hit),
            Creature("target", 1, Wait()), Chart(), new FakeRng(ints: [15]));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Contains(events, item => item is DamageDealt { Slot.Side: BattleSide.Enemy, Crit: true });
        Assert.Contains(battle.Trace, item => item.Kind == EffectTraceKind.Critical && !item.Performed);
        Assert.DoesNotContain(battle.ConditionSnapshot,
            instance => instance.Definition.Id == OneShotQueryConditions.CriticalChance.Id);
    }

    [Fact]
    public void CriticalQuery_UsesTheCompleteStageTableAndGuardCanPreserveOneShot()
    {
        BattleMove hit = Hit("critical_table");
        BattleQueryValue[] expected =
        [
            new(1, 16), new(1, 16), new(1, 16), new(1, 8), new(1, 4),
            new(1, 3), new(1, 2), new(1, 2), new(1, 2),
        ];
        for (int stage = -2; stage <= 6; stage++)
            Assert.Equal(expected[stage + 2], BattleActionQueries.CriticalChance(hit, stage,
                false, null, new BattleQueryContext()).FinalValue);

        BattleMove prepare = new(EntityId.Parse("move:prepare_guarded_critical"), Neutral,
            DamageClass.Status, null, null, 10, 0, 0, target: MoveTarget.User,
            secondaryEffects: [new OneShotQueryEffect(OneShotQuery.CriticalChance, 2)]);
        BattleMove guard = new(EntityId.Parse("move:critical_guard"), Neutral, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.UsersField,
            secondaryEffects: [new SetSideConditionEffect(BattleSideCondition.CriticalGuard, 5)]);
        var battle = new BattleController(Creature("source", 100, prepare, hit),
            Creature("target", 1, guard, Wait()), Chart(),
            new FakeRng(ints: [15], doubles: [0.5]));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(1));

        Assert.Contains(events, item => item is DamageDealt { Crit: false });
        Assert.Equal(new BattleQueryValue(0), battle.QueryTrace.Last(item =>
            item.Result.Query == BattleQueryId.CriticalChance).Result.FinalValue);
        Assert.DoesNotContain(battle.ConditionTrace, item =>
            item.Condition == OneShotQueryConditions.CriticalChance.Id
            && item.Kind == BattleConditionTraceKind.Removed
            && item.CleanupReason == BattleConditionCleanupReason.Effect);
        Assert.Contains(battle.ConditionTrace, item =>
            item.Condition == OneShotQueryConditions.CriticalChance.Id
            && item.Kind == BattleConditionTraceKind.Expired);
    }

    [Fact]
    public void AccuracyOneShot_IsSourceAndOwnerIsolatedAndCleansUpOnEitherDeparture()
    {
        var store = new BattleConditionStores(new BattleConditionRegistry(
            OneShotQueryConditions.Definitions));
        BattleSlot sourceSlot = new(BattleSide.Player, 0);
        BattleSlot otherSourceSlot = new(BattleSide.Player, 1);
        BattleSlot targetSlot = new(BattleSide.Enemy, 0);
        var targetOwner = new BattleConditionOwner(BattleConditionScope.Creature,
            BattleSide.Enemy, targetSlot, 0);
        var source = new BattleConditionSource(sourceSlot, 0);
        store.Apply(new BattleConditionApplication(OneShotQueryConditions.Accuracy.Id,
            targetOwner, source, 0, 0));

        Assert.NotNull(OneShotQueryConditions.FindAccuracy(store.Snapshot(), targetOwner, source));
        Assert.Null(OneShotQueryConditions.FindAccuracy(store.Snapshot(), targetOwner,
            new BattleConditionSource(otherSourceSlot, 1)));
        store.SourceLeft(BattleSide.Player, 0, BattleConditionCleanupReason.Switch, 0, 1);
        Assert.Empty(store.Snapshot());

        store.Apply(new BattleConditionApplication(OneShotQueryConditions.Accuracy.Id,
            targetOwner, source, 1, 0));
        store.OwnerSwitched(BattleSide.Enemy, 0, null, 1, 1);
        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void AccuracyOneShot_DoublesResolutionPreservesWrongSourceAndConsumesForBoundPair()
    {
        BattleMove prepare = new(EntityId.Parse("move:doubles_prepare_accuracy"), Neutral,
            DamageClass.Status, null, null, 10, 0, 0, target: MoveTarget.Selected,
            secondaryEffects: [new OneShotQueryEffect(OneShotQuery.Accuracy, 3)]);
        BattleMove hit = Hit("doubles_inaccurate", 80, 1,
            new MoveQueryModifierEffect(BattleQueryId.CriticalChance,
                BattleQueryOperation.Replace, new BattleQueryValue(0)));
        BattleCreature player0 = Creature("player_zero", 100, prepare, hit);
        BattleCreature player1 = Creature("player_one", 90, hit);
        BattleCreature enemy0 = Creature("enemy_zero", 80, Wait());
        BattleCreature enemy1 = Creature("enemy_one", 70, Wait());
        var battle = new BattleController([player0, player1], [enemy0, enemy1],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(),
            new FakeRng(ints: [99, 15], doubles: [0.5]));
        BattleSlot playerZero = new(BattleSide.Player, 0);
        BattleSlot playerOne = new(BattleSide.Player, 1);
        BattleSlot enemyOne = new(BattleSide.Enemy, 1);

        battle.ResolveTurn(DoublesActions(new(playerZero, new UseMove(0),
            new ActiveSlotSelection(enemyOne))));
        BattleConditionInstance condition = Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(enemyOne, condition.Owner.Slot);
        Assert.Equal(playerZero, condition.Source.Slot);

        battle.ResolveTurn(DoublesActions(new(playerOne, new UseMove(0),
            new ActiveSlotSelection(enemyOne))));
        Assert.Contains(battle.ConditionSnapshot,
            instance => instance.Definition.Id == OneShotQueryConditions.Accuracy.Id);

        battle.ResolveTurn(DoublesActions(new(playerZero, new UseMove(1),
            new ActiveSlotSelection(enemyOne))));
        Assert.DoesNotContain(battle.ConditionSnapshot,
            instance => instance.Definition.Id == OneShotQueryConditions.Accuracy.Id);
        Assert.True(enemy1.CurrentHp < enemy1.MaxHp);
    }

    [Fact]
    public void PriorityModifier_IsSnapshottedForTurnOrderAndClamped()
    {
        BattleMove priority = Hit("priority", 10, null,
            new MoveQueryModifierEffect(BattleQueryId.Priority, BattleQueryOperation.Add,
                new BattleQueryValue(20)));
        BattleMove ordinary = Hit("ordinary", power: 10);
        var battle = new BattleController(Creature("slow", 1, priority),
            Creature("fast", 100, ordinary), Chart(),
            new FakeRng(ints: [15, 15], doubles: [0.5, 0.5]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(EntityId.Parse("move:priority"), events.OfType<MoveUsed>().First().Move);
        Assert.Equal(7, battle.QueryTrace.First(item => item.Result.Query == BattleQueryId.Priority)
            .Result.FinalValue.ToInt32());
    }

    [Fact]
    public void FinalDamageModifier_AppliesFloorAndCapToStandardAndFixedDamage()
    {
        BattleMove standard = Hit("standard", 80, null,
            new MoveQueryModifierEffect(BattleQueryId.FinalDamage, BattleQueryOperation.Min,
                new BattleQueryValue(20)),
            new MoveQueryModifierEffect(BattleQueryId.CriticalChance, BattleQueryOperation.Replace,
                new BattleQueryValue(0)));
        BattleCreature standardTarget = Creature("standard_target", 1, Wait());
        var standardBattle = new BattleController(Creature("standard_source", 100, standard),
            standardTarget, Chart(), new FakeRng(ints: [15], doubles: [0.5]));
        int standardDamage = standardBattle.ResolveTurn(new UseMove(0), new UseMove(0))
            .OfType<DamageDealt>().Single(item => item.Slot.Side == BattleSide.Enemy).Amount;

        BattleMove fixedMove = Compile(DamageClass.Special, MoveTarget.Selected, null,
            Op("fixedDamage", ("levelBased", false), ("amount", 100)),
            Op("queryModifier", ("query", "finalDamage"), ("operation", "multiply"), ("num", 1), ("den", 2)),
            Op("queryModifier", ("query", "finalDamage"), ("operation", "max"), ("num", 60)));
        BattleCreature fixedTarget = Creature("fixed_target", 1, Wait());
        var fixedBattle = new BattleController(Creature("fixed_source", 100, fixedMove),
            fixedTarget, Chart(), new FakeRng());
        int fixedDamage = fixedBattle.ResolveTurn(new UseMove(0), new UseMove(0))
            .OfType<DamageDealt>().Single(item => item.Slot.Side == BattleSide.Enemy).Amount;

        Assert.Equal(20, standardDamage);
        Assert.Equal(60, fixedDamage);
    }

    [Fact]
    public void FinalDamageModifier_CannotResurrectTypeImmuneFixedDamage()
    {
        EntityId attack = EntityId.Parse("type:attack");
        EntityId immune = EntityId.Parse("type:immune");
        BattleMove move = new(EntityId.Parse("move:immune_fixed"), attack, DamageClass.Special,
            null, null, 10, 0, 0, fixedDamage: 40,
            secondaryEffects:
            [
                new MoveQueryModifierEffect(BattleQueryId.FinalDamage,
                    BattleQueryOperation.Replace, new BattleQueryValue(40)),
            ]);
        BattleCreature source = new(EntityId.Parse("species:source"), "source", 50, [attack],
            new Stats(300, 100, 100, 100, 100, 100), [move]);
        BattleCreature target = new(EntityId.Parse("species:target"), "target", 50, [immune],
            new Stats(300, 100, 100, 100, 100, 1), [Wait()]);
        var battle = new BattleController(source, target,
            new TypeChart([new TypeDef { Id = attack, NoDamageTo = [immune] }, new TypeDef { Id = immune }]),
            new FakeRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(target.MaxHp, target.CurrentHp);
        Assert.Equal(0, battle.QueryTrace.Last(item => item.Result.Query == BattleQueryId.FinalDamage)
            .Result.FinalValue.ToInt32());
    }

    [Fact]
    public void HealingModifier_MultipliesClampsAndCanBlockWithoutEvent()
    {
        BattleMove boosted = Compile(DamageClass.Status, MoveTarget.User, null,
            Op("heal", ("num", 1), ("den", 2)),
            Op("queryModifier", ("query", "healing"), ("operation", "multiply"), ("num", 3), ("den", 2)));
        BattleCreature boostedSource = Creature("boosted", 100, boosted);
        boostedSource.TakeDamage(50);
        var boostedBattle = new BattleController(boostedSource, Creature("target", 1, Wait()),
            Chart(), new FakeRng());
        IReadOnlyList<BattleEvent> boostedEvents = boostedBattle.ResolveTurn(new UseMove(0), new UseMove(0));

        BattleMove blocked = Compile(DamageClass.Status, MoveTarget.User, null,
            Op("heal", ("num", 1), ("den", 2)),
            Op("queryModifier", ("query", "healing"), ("operation", "replace"), ("num", 0)));
        BattleCreature blockedSource = Creature("blocked", 100, blocked);
        blockedSource.TakeDamage(50);
        var blockedBattle = new BattleController(blockedSource, Creature("other", 1, Wait()),
            Chart(), new FakeRng());
        IReadOnlyList<BattleEvent> blockedEvents = blockedBattle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(boostedSource.MaxHp, boostedSource.CurrentHp);
        Assert.Contains(boostedEvents, item => item is Healed { Side: BattleSide.Player, Amount: 50 });
        Assert.Equal(225, boostedBattle.QueryTrace.Last(item => item.Result.Query == BattleQueryId.Healing)
            .Result.FinalValue.ToInt32());
        Assert.Equal(250, blockedSource.CurrentHp);
        Assert.DoesNotContain(blockedEvents, item => item is Healed);
        Assert.Equal(0, blockedBattle.QueryTrace.Last(item => item.Result.Query == BattleQueryId.Healing)
            .Result.FinalValue.ToInt32());
    }

    [Fact]
    public void SmartAiAndResolverShareAccuracyCriticalAndFinalDamageQueries()
    {
        BattleMove move = Hit("parity", 80, 1,
            new AccuracyQueryEffect(AccuracyQueryMode.Bypass),
            new MoveQueryModifierEffect(BattleQueryId.CriticalChance,
                BattleQueryOperation.Replace, new BattleQueryValue(1)),
            new MoveQueryModifierEffect(BattleQueryId.FinalDamage,
                BattleQueryOperation.Min, new BattleQueryValue(80)));
        BattleCreature source = Creature("source", 100, move);
        BattleCreature target = Creature("target", 1, Wait());
        var battle = new BattleController(source, target, Chart(), new FakeRng(ints: [15], doubles: [0]));
        int resolved = battle.ResolveTurn(new UseMove(0), new UseMove(0))
            .OfType<DamageDealt>().Single(item => item.Slot.Side == BattleSide.Enemy).Amount;

        double preview = DamageScore(new SmartAiContext([source], 0, [target], 0, Chart(),
            new FakeRng(doubles: [0.5]), Weights: new SmartAiWeights { NoiseFraction = 0 }));

        Assert.Equal(80, resolved);
        Assert.Equal(resolved, preview);
    }

    [Fact]
    public void ActionQueryFamily_MatchesDeterministicGolden()
    {
        static string Run()
        {
            BattleMove prepare = new(EntityId.Parse("move:golden_prepare"), Neutral,
                DamageClass.Status, null, null, 10, 0, 0, target: MoveTarget.User,
                secondaryEffects: [new OneShotQueryEffect(OneShotQuery.CriticalChance, 2)]);
            BattleMove hit = Hit("golden_hit", 80, 1,
                new AccuracyQueryEffect(AccuracyQueryMode.Bypass),
                new MoveQueryModifierEffect(BattleQueryId.Priority, BattleQueryOperation.Add,
                    new BattleQueryValue(1)),
                new MoveQueryModifierEffect(BattleQueryId.FinalDamage, BattleQueryOperation.Min,
                    new BattleQueryValue(50)),
                new DrainEffect(new Fraction(1, 2)),
                new MoveQueryModifierEffect(BattleQueryId.Healing, BattleQueryOperation.Multiply,
                    new BattleQueryValue(2)));
            BattleCreature source = Creature("golden_source", 100, prepare, hit);
            source.TakeDamage(100);
            var battle = new BattleController(source, Creature("golden_target", 1, Wait()),
                Chart(), new FakeRng(ints: [15]));
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

            string Query(BattleQueryId query) => battle.QueryTrace.Last(item =>
                item.SourceSlot.Side == BattleSide.Player && item.Result.Query == query)
                .Result.FinalValue.ToString();
            EffectTraceEntry critical = battle.Trace.Last(item =>
                item.SourceSlot.Side == BattleSide.Player && item.Kind == EffectTraceKind.Critical);
            return string.Join('\n',
            [
                $"priority:{Query(BattleQueryId.Priority)}",
                $"accuracy:{Query(BattleQueryId.Accuracy)}",
                $"critical:{Query(BattleQueryId.CriticalChance)}:draw={critical.Performed}",
                $"finalDamage:{Query(BattleQueryId.FinalDamage)}",
                $"healing:{Query(BattleQueryId.Healing)}",
                .. events.OfType<DamageDealt>().Select(item =>
                    $"damage:{item.Amount}:critical={item.Crit}"),
                .. events.OfType<Healed>().Select(item => $"healed:{item.Amount}"),
                .. battle.ConditionTrace.Where(item =>
                    item.Condition == OneShotQueryConditions.CriticalChance.Id
                    && item.Kind == BattleConditionTraceKind.Removed)
                    .Select(item => $"condition:{item.Condition}:{item.Kind}:{item.CleanupReason}"),
            ]);
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("action-query"), first);
    }

    private static BattleMove Compile(DamageClass damageClass, MoveTarget target, int? power,
        params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse("move:compiled_action_query"), Name = "Compiled Action Query",
            Type = Neutral, DamageClass = damageClass, Power = power, Accuracy = null, Pp = 10,
            Target = target, Effects = effects,
        });

    private static BattleMove Hit(string slug, int power = 80, int? accuracy = null,
        params MoveEffect[] effects) => new(EntityId.Parse($"move:{slug}"), Neutral,
        DamageClass.Physical, power, accuracy, 10, 0, 0, secondaryEffects: effects);

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Neutral,
        DamageClass.Status, null, null, 10, 0, 0);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Neutral],
            new Stats(300, 100, 100, 100, 100, speed), moves);

    private static TypeChart Chart() => new([new TypeDef { Id = Neutral }]);

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static double DamageScore(SmartAiContext context) => SmartAi.ChooseAction(context).Scores
        .Single(score => score.Action is UseMove { MoveIndex: 0 }).Components
        .Single(component => component.Name == "damage").Value;

    private static BattleTurnActions DoublesActions(BattleActionSubmission active) => new(
        BattleTopology.Doubles,
        [
            active,
            .. BattleTopology.Doubles.Slots.Where(slot => slot != active.Source)
                .Select(slot => new BattleActionSubmission(slot, new Pass())),
        ]);

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();
}
