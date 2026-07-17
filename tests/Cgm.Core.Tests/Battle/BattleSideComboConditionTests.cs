using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleSideComboConditionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Fire = EntityId.Parse("type:fire");

    [Fact]
    public void Compiler_AdmitsClosedSourceAndTargetSideRows()
    {
        BattleMove source = MoveCompiler.ToBattleMove(DataMove(
            BattleSideCondition.SecondaryChanceBoost, MoveTarget.UsersField));
        BattleMove target = MoveCompiler.ToBattleMove(DataMove(
            BattleSideCondition.SpeedReduction, MoveTarget.OpponentsField, SideConditionTarget.Target));

        Assert.Contains(source.SecondaryEffects, effect => effect is SetSideConditionEffect
            { Condition: BattleSideCondition.SecondaryChanceBoost, Duration: 4, Side: SideConditionTarget.Source });
        Assert.Contains(target.SecondaryEffects, effect => effect is SetSideConditionEffect
            { Condition: BattleSideCondition.SpeedReduction, Duration: 4, Side: SideConditionTarget.Target });
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(
            BattleSideCondition.ResidualDamage, MoveTarget.UsersField, SideConditionTarget.Target)));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(
            BattleSideCondition.SpeedReduction, MoveTarget.UsersField)));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(
            BattleSideCondition.SecondaryChanceBoost, MoveTarget.OpponentsField, SideConditionTarget.Target)));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(
            BattleSideCondition.ResidualDamage, MoveTarget.OpponentsField, SideConditionTarget.Target, 0)));
    }

    [Fact]
    public void SpeedReduction_QuartersSpeedAndComposesAfterBoostInStableOrder()
    {
        BattleCreature creature = Creature("subject", Normal, 101, Inert("wait"));
        BattleHookDispatchSnapshot reduced = SideConditions.CollectSpeedHooks(
            [Condition(BattleSide.Enemy, BattleSideCondition.SpeedReduction)], BattleSide.Enemy, 4);
        BattleHookDispatchSnapshot composed = SideConditions.CollectSpeedHooks(
            [
                Condition(BattleSide.Enemy, BattleSideCondition.SpeedReduction, sequence: 2),
                Condition(BattleSide.Enemy, BattleSideCondition.SpeedBoost, sequence: 1),
            ], BattleSide.Enemy, 5);

        Assert.Equal(25, PhysicalMetricFormulas.SpeedQuery(creature,
            reduced.QueryModifiers(BattleQueryId.Speed)).FinalValue.ToInt32());
        BattleQueryResult result = PhysicalMetricFormulas.SpeedQuery(creature,
            composed.QueryModifiers(BattleQueryId.Speed));
        Assert.Equal(50, result.FinalValue.ToInt32());
        Assert.Equal([new BattleQueryValue(2), new BattleQueryValue(1, 4)], result.Steps
            .Where(step => step.Stage == BattleQueryStage.Hooks)
            .Select(step => step.Operand!.Value).ToArray());
    }

    [Fact]
    public void TargetSideRows_CoexistRejectDuplicatesAndExpireTogether()
    {
        BattleCreature source = Creature("source", Normal, 100,
            SideMove("slow", BattleSideCondition.SpeedReduction, SideConditionTarget.Target),
            SideMove("chip", BattleSideCondition.ResidualDamage, SideConditionTarget.Target));
        BattleCreature target = Creature("target", Normal, 90, Inert("wait"));
        var battle = new BattleController(source, target, Chart(), new FixedRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> duplicate = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal(2, battle.ConditionSnapshot.Count);
        Assert.All(battle.ConditionSnapshot, condition => Assert.Equal(BattleSide.Enemy, condition.Owner.Side));
        Assert.Contains(duplicate, entry => entry is MoveFailed
            { Reason: MoveFailureReason.ConditionAlreadyActive });
        Assert.Equal(1, battle.ConditionSnapshot.Single(condition =>
            condition.Definition.Id == SideConditions.For(BattleSideCondition.SpeedReduction).Id).RemainingDuration);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.DoesNotContain(battle.ConditionSnapshot, condition =>
            condition.Definition.Id == SideConditions.For(BattleSideCondition.SpeedReduction).Id);
        Assert.Contains(battle.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Expired
            && row.OwnerBefore?.Side == BattleSide.Enemy);
    }

    [Fact]
    public void ResidualDamage_AppliesBeforeSharedTickAndSkipsExcludedType()
    {
        BattleCreature source = Creature("source", Normal, 100,
            SideMove("chip", BattleSideCondition.ResidualDamage, SideConditionTarget.Target));
        BattleCreature target = Creature("target", Normal, 90, Inert("wait"));
        var battle = new BattleController(source, target, Chart(), new FixedRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(438, target.CurrentHp);
        Assert.Contains(events, entry => entry is ResidualDamage
            { Slot.Side: BattleSide.Enemy, Amount: 62 });
        Assert.Equal(3, Assert.Single(battle.ConditionSnapshot).RemainingDuration);

        BattleCreature immuneSource = Creature("immune_source", Normal, 100,
            SideMove("chip", BattleSideCondition.ResidualDamage, SideConditionTarget.Target));
        BattleCreature immune = Creature("immune", Normal, 90, Inert("wait"));
        var immuneBattle = new BattleController(immuneSource, immune, Chart(), new FixedRng());
        immuneBattle.Overlays.Apply(new BattleOverlayApplication(
            new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0)),
            new BattleOverlaySource(), BattleOverlayLayer.FormOrSnapshot,
            new CreatureTypesOverlay([Fire]), 0, 0));
        immuneBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(500, immune.CurrentHp);

        BattleCreature tinySource = Creature("tiny_source", Normal, 100,
            SideMove("chip", BattleSideCondition.ResidualDamage, SideConditionTarget.Target));
        BattleCreature tiny = new(EntityId.Parse("species:tiny"), "tiny", 50, [Normal],
            new Stats(7, 100, 100, 100, 100, 90), [Inert("wait")]);
        var tinyBattle = new BattleController(tinySource, tiny, Chart(), new FixedRng());
        tinyBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(6, tiny.CurrentHp);
    }

    [Fact]
    public void ResidualDamage_ResolvesBeforeWeatherAndSharedDurationTick()
    {
        BattleCreature source = Creature("source", Normal, 100,
            WeatherMove(), SideMove("chip", BattleSideCondition.ResidualDamage, SideConditionTarget.Target));
        BattleCreature target = Creature("target", Normal, 90, Inert("wait"));
        var battle = new BattleController(source, target, Chart(), new FixedRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.True(IndexOf<ResidualDamage>(events, BattleSide.Enemy) < IndexOf<WeatherDamage>(events, BattleSide.Enemy));
        Assert.Equal(3, battle.ConditionSnapshot.Single(condition =>
            condition.Definition.Id == SideConditions.For(BattleSideCondition.ResidualDamage).Id).RemainingDuration);
    }

    [Fact]
    public void ResidualDamage_UsesOneDoublesConditionForEachEligibleActiveSlot()
    {
        BattleCreature source = Creature("source", Normal, 100,
            SideMove("chip", BattleSideCondition.ResidualDamage, SideConditionTarget.Target));
        BattleCreature ally = Creature("ally", Normal, 80, Inert("ally_wait"));
        BattleCreature foe = Creature("foe", Normal, 70, Inert("foe_wait"));
        BattleCreature immune = Creature("immune", Fire, 60, Inert("immune_wait"));
        var battle = new BattleController([source, ally], [foe, immune], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new FixedRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            new UseMove(0), new Pass(), new Pass(), new Pass()));

        Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(438, foe.CurrentHp);
        Assert.Equal(500, immune.CurrentHp);
        Assert.Single(events.OfType<ResidualDamage>());
    }

    [Fact]
    public void SecondaryChanceBoost_DoublesDamagingEffectsClampsAndPreservesOneDraw()
    {
        BattleCreature source = Creature("source", Normal, 100,
            SideMove("boost", BattleSideCondition.SecondaryChanceBoost, SideConditionTarget.Source),
            DamagingStatus("chance", 30), DamagingStatus("clamp", 60));
        BattleCreature target = Creature("target", Normal, 80, Inert("wait"));
        var rng = new FixedRng(chanceDraw: 50);
        var battle = new BattleController(source, target, Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal(PersistentStatus.Poison, target.Status);
        Assert.Equal(1, rng.HundredDraws);
        Assert.Contains(battle.QueryTrace, row => row.Result.Query == BattleQueryId.SecondaryChance
            && row.Result.AuthoredBase.ToInt32() == 30 && row.Result.FinalValue.ToInt32() == 60);
        Assert.Contains(battle.HookTrace, row => row.Checkpoint == BattleConditionHook.SecondaryEffect
            && row.PayloadKind == BattleHookPayloadKind.QueryModifier);

        target.ClearStatus();
        battle.ResolveTurn(new UseMove(2), new UseMove(0));
        Assert.Equal(PersistentStatus.Poison, target.Status);
        Assert.Contains(battle.QueryTrace, row => row.Result.Query == BattleQueryId.SecondaryChance
            && row.Result.AuthoredBase.ToInt32() == 60 && row.Result.FinalValue.ToInt32() == 100);
    }

    [Fact]
    public void SecondaryChanceBoost_DoesNotModifyStatusMoveChance()
    {
        BattleCreature source = Creature("source", Normal, 100,
            SideMove("boost", BattleSideCondition.SecondaryChanceBoost, SideConditionTarget.Source),
            StatusMove("status", 30));
        BattleCreature target = Creature("target", Normal, 80, Inert("wait"));
        var rng = new FixedRng(chanceDraw: 50);
        var battle = new BattleController(source, target, Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Null(target.Status);
        Assert.Equal(1, rng.HundredDraws);
        Assert.DoesNotContain(battle.QueryTrace, row => row.Result.Query == BattleQueryId.SecondaryChance);
    }

    [Fact]
    public void SmartAi_UsesTheSameVisibleSecondaryChanceModifier()
    {
        BattleCreature attacker = Creature("attacker", Normal, 100, DamagingStatus("chance", 30));
        BattleCreature defender = Creature("defender", Normal, 80, Inert("wait"));

        double clear = StatusScore(attacker, defender, []);
        double boosted = StatusScore(attacker, defender,
            [Condition(BattleSide.Enemy, BattleSideCondition.SecondaryChanceBoost)]);

        Assert.Equal(clear * 2, boosted, 6);
    }

    [Fact]
    public void ComboConditions_PersistAfterSourceSwitchAndReplayDeterministically()
    {
        static (string[] Events, string[] Trace) Run()
        {
            BattleCreature source = Creature("source", Normal, 100,
                SideMove("slow", BattleSideCondition.SpeedReduction, SideConditionTarget.Target));
            BattleCreature reserve = Creature("reserve", Normal, 90, Inert("reserve_wait"));
            BattleCreature target = Creature("target", Normal, 80, Inert("wait"));
            var battle = new BattleController([source, reserve], [target], Chart(), new FixedRng());
            List<BattleEvent> events = [.. battle.ResolveTurn(new UseMove(0), new UseMove(0))];
            events.AddRange(battle.ResolveTurn(new Switch(1), new Pass()));
            Assert.Contains(battle.ConditionSnapshot, row => row.Source.PartyIndex == 0
                && row.Owner.Side == BattleSide.Enemy);
            return ([.. events.Select(entry => entry.ToString()!)],
                [.. battle.ConditionTrace.Select(entry => entry.ToString()!)]);
        }

        (string[] firstEvents, string[] firstTrace) = Run();
        (string[] secondEvents, string[] secondTrace) = Run();
        Assert.Equal(firstEvents, secondEvents);
        Assert.Equal(firstTrace, secondTrace);
    }

    private static double StatusScore(BattleCreature attacker, BattleCreature defender,
        IReadOnlyList<BattleConditionInstance> conditions) => SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [defender], 0, Chart(), new FixedRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Conditions: conditions)).Scores.Single()
        .Components.Single(component => component.Name == "status").Value;

    private static BattleConditionInstance Condition(BattleSide side, BattleSideCondition condition,
        int duration = 4, long sequence = 1) => new(sequence, SideConditions.For(condition),
        SideConditions.Owner(side), new BattleConditionSource(new BattleSlot(side, 0), 0), 0, 0, duration,
        SideConditions.For(condition).Tags, new Dictionary<string, int>(), 1);

    private static BattleTurnActions Actions(BattleAction player0, BattleAction player1,
        BattleAction enemy0, BattleAction enemy1) => new(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), player0),
            new(new BattleSlot(BattleSide.Player, 1), player1),
            new(new BattleSlot(BattleSide.Enemy, 0), enemy0),
            new(new BattleSlot(BattleSide.Enemy, 1), enemy1),
        ]);

    private static BattleMove SideMove(string slug, BattleSideCondition condition, SideConditionTarget side) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: side == SideConditionTarget.Source ? MoveTarget.UsersField : MoveTarget.OpponentsField,
        secondaryEffects: [new SetSideConditionEffect(condition, 4, side)]);

    private static BattleMove DamagingStatus(string slug, int chance) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Special, 40, null, 20, 0, 0,
        ailment: PersistentStatus.Poison, ailmentChance: chance,
        secondaryEffects: [new AilmentEffect(PersistentStatus.Poison) { Chance = chance }]);

    private static BattleMove StatusMove(string slug, int chance) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        ailment: PersistentStatus.Poison, ailmentChance: chance,
        secondaryEffects: [new AilmentEffect(PersistentStatus.Poison) { Chance = chance }]);

    private static BattleMove Inert(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.User);

    private static BattleMove WeatherMove() => new(
        EntityId.Parse("move:weather"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.EntireField, secondaryEffects: [new SetWeatherEffect(Weather.Sandstorm)]);

    private static int IndexOf<T>(IReadOnlyList<BattleEvent> events, BattleSide side) where T : BattleEvent
    {
        for (int index = 0; index < events.Count; index++)
            if (events[index] is T entry && entry switch
                {
                    ResidualDamage residual => residual.Side == side,
                    WeatherDamage weather => weather.Side == side,
                    _ => false,
                })
                return index;
        return -1;
    }

    private static BattleCreature Creature(string slug, EntityId type, int speed, params BattleMove[] moves) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [type],
        new Stats(500, 100, 100, 100, 100, speed), moves);

    private static Move DataMove(BattleSideCondition condition, MoveTarget target,
        SideConditionTarget? side = null, int? duration = null)
    {
        var parameters = new Dictionary<string, JsonElement>
        {
            ["condition"] = JsonSerializer.SerializeToElement(condition.ToString()),
        };
        if (side is not null)
            parameters["side"] = JsonSerializer.SerializeToElement(side.ToString());
        if (duration is not null)
            parameters["duration"] = JsonSerializer.SerializeToElement(duration.Value);
        return new Move
        {
            Id = EntityId.Parse("move:data"), Name = "Data", Type = Normal,
            DamageClass = DamageClass.Status, Target = target, Pp = 10,
            Effects = [new Effect { Op = "sideCondition", Params = parameters }],
        };
    }

    private static TypeChart Chart() => new([
        new TypeDef { Id = Normal },
        new TypeDef { Id = Fire },
    ]);

    private sealed class FixedRng(int chanceDraw = 0) : IRng
    {
        public int HundredDraws { get; private set; }

        public int Next(int maxExclusive)
        {
            if (maxExclusive == 100)
            {
                HundredDraws++;
                return chanceDraw;
            }
            return maxExclusive == 16 ? 15 : 0;
        }

        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0.99;
    }
}
