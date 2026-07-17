using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleSideOrderConditionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void Compiler_UsesSpeedBoostDefaultAndAdmitsAuthoredClassicDuration()
    {
        BattleMove modern = MoveCompiler.ToBattleMove(DataMove());
        BattleMove classic = MoveCompiler.ToBattleMove(DataMove(3));

        Assert.Contains(modern.SecondaryEffects, effect => effect is SetSideConditionEffect
            { Condition: BattleSideCondition.SpeedBoost, Duration: 4 });
        Assert.Contains(classic.SecondaryEffects, effect => effect is SetSideConditionEffect
            { Condition: BattleSideCondition.SpeedBoost, Duration: 3 });
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(0)));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(4) with
        {
            Target = MoveTarget.OpponentsField,
        }));
    }

    [Fact]
    public void SpeedBoost_MultipliesEffectiveSpeedAfterStagesAndStatus()
    {
        BattleCreature creature = Creature("subject", 101, Inert("wait"));
        creature.SetStage(StatKind.Spe, -1);
        creature.SetStatus(PersistentStatus.Paralysis);
        BattleHookDispatchSnapshot hooks = SideConditions.CollectSpeedHooks(
            [Condition(BattleSide.Player, 4)], BattleSide.Player, 7);

        BattleQueryResult result = PhysicalMetricFormulas.SpeedQuery(creature,
            hooks.QueryModifiers(BattleQueryId.Speed));

        Assert.Equal(32, result.FinalValue.ToInt32());
        Assert.Equal(BattleQueryStage.Hooks, result.Steps.Single(step =>
            step.OwnerScope == BattleQueryOwnerScope.SourceSide).Stage);
        Assert.Single(hooks.Trace);
    }

    [Fact]
    public void SpeedBoost_ChangesOnlyFutureSchedulingAndExpiresAfterSharedDuration()
    {
        BattleCreature source = Creature("source", 50, Boost("boost", 4), Inert("source_wait"));
        BattleCreature target = Creature("target", 75, Inert("target_wait"));
        var battle = new BattleController(source, target, Chart(), new ZeroRng());

        IReadOnlyList<BattleEvent> application = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal([BattleSide.Enemy, BattleSide.Player], UsedSides(application));
        Assert.Equal(3, Assert.Single(battle.ConditionSnapshot).RemainingDuration);

        IReadOnlyList<BattleEvent> boosted = battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Equal([BattleSide.Player, BattleSide.Enemy], UsedSides(boosted));
        Assert.Contains(battle.QueryTrace, row => row.Result.Query == BattleQueryId.Speed
            && row.SourceSlot.Side == BattleSide.Player
            && row.Result.FinalValue.ToInt32() == 100
            && row.Result.Steps.Any(step => step.OwnerScope == BattleQueryOwnerScope.SourceSide));

        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Empty(battle.ConditionSnapshot);
        Assert.Contains(battle.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Expired);

        IReadOnlyList<BattleEvent> expired = battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Equal([BattleSide.Enemy, BattleSide.Player], UsedSides(expired));
    }

    [Fact]
    public void SpeedBoost_RejectsDuplicateWithoutRefreshingSharedDuration()
    {
        BattleCreature source = Creature("source", 100, Boost("boost", 4));
        var battle = new BattleController(source, Creature("target", 90, Inert("wait")), Chart(), new ZeroRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> duplicate = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2, Assert.Single(battle.ConditionSnapshot).RemainingDuration);
        Assert.Contains(duplicate, row => row is MoveFailed
            { Reason: MoveFailureReason.ConditionAlreadyActive });
    }

    [Fact]
    public void SpeedBoost_IsOneSharedDoublesConditionForBothActiveSlots()
    {
        BattleCreature source = Creature("source", 50, Boost("boost", 4), Inert("source_wait"));
        BattleCreature ally = Creature("ally", 60, Inert("ally_wait"));
        BattleCreature foe = Creature("foe", 90, Inert("foe_wait"));
        BattleCreature foeAlly = Creature("foe_ally", 80, Inert("foe_ally_wait"));
        var battle = new BattleController([source, ally], [foe, foeAlly], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new ZeroRng());

        battle.ResolveTurn(Actions(new UseMove(0), new Pass(), new Pass(), new Pass()));
        Assert.Single(battle.ConditionSnapshot);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            new UseMove(1), new UseMove(0), new UseMove(0), new UseMove(0)));
        BattleSlot[] order = events.OfType<MoveUsed>().Select(row => row.Slot).ToArray();
        Assert.Equal([
            new BattleSlot(BattleSide.Player, 1),
            new BattleSlot(BattleSide.Player, 0),
            new BattleSlot(BattleSide.Enemy, 0),
            new BattleSlot(BattleSide.Enemy, 1),
        ], order);
        Assert.Equal(2, battle.QueryTrace.Count(row => row.Result.Query == BattleQueryId.Speed
            && row.SourceSlot.Side == BattleSide.Player
            && row.Result.Steps.Any(step => step.OwnerScope == BattleQueryOwnerScope.SourceSide)));
    }

    [Fact]
    public void TrickRoom_ReversesOrderAfterSpeedBoostIsCalculated()
    {
        BattleCreature source = Creature("source", 50, Boost("boost", 4), Room("room"), Inert("source_wait"));
        BattleCreature target = Creature("target", 75, Inert("target_wait"));
        var battle = new BattleController(source, target, Chart(), new ZeroRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        IReadOnlyList<BattleEvent> reversed = battle.ResolveTurn(new UseMove(2), new UseMove(0));

        Assert.Equal([BattleSide.Enemy, BattleSide.Player], UsedSides(reversed));
        Assert.Contains(battle.QueryTrace, row => row.SourceSlot.Side == BattleSide.Player
            && row.Result.Query == BattleQueryId.Speed && row.Result.FinalValue.ToInt32() == 100);
    }

    [Fact]
    public void SmartAi_UsesSideSpeedForSpeedRatioPower()
    {
        BattleMove formula = SpeedFormula();
        BattleCreature attacker = Creature("attacker", 100, formula);
        BattleCreature defender = Creature("defender", 100, Inert("wait"));

        double clear = DamageScore(attacker, defender, []);
        double sourceBoosted = DamageScore(attacker, defender, [Condition(BattleSide.Enemy, 4)]);
        double targetBoosted = DamageScore(attacker, defender, [Condition(BattleSide.Player, 4)]);

        Assert.True(sourceBoosted > clear);
        Assert.True(targetBoosted < clear);
    }

    [Fact]
    public void Resolver_UsesSideSpeedForSpeedRatioPower()
    {
        BattleCreature attacker = Creature("attacker", 100, Boost("boost", 4), SpeedFormula());
        BattleCreature defender = Creature("defender", 100, Inert("wait"));
        var battle = new BattleController(attacker, defender, Chart(), new ZeroRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal(80, Assert.Single(battle.QueryTrace,
            row => row.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
        Assert.Contains(battle.HookTrace, row => row.Checkpoint == BattleConditionHook.StatQuery
            && row.PayloadKind == BattleHookPayloadKind.QueryModifier);
    }

    [Fact]
    public void SpeedBoost_PersistsWhenItsSourceSwitchesAndAddsNoConditionRng()
    {
        BattleCreature source = Creature("source", 50, Boost("boost", 4));
        BattleCreature reserve = Creature("reserve", 40, Inert("reserve_wait"));
        BattleCreature target = Creature("target", 75, Inert("target_wait"));
        var rng = new CountingRng();
        var battle = new BattleController([source, reserve], [target], Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int drawsAfterApplication = rng.Draws;
        battle.ResolveTurn(new Switch(1), new Pass());

        Assert.Contains(battle.ConditionSnapshot, row => row.Owner == SideConditions.Owner(BattleSide.Player)
            && row.Source.PartyIndex == 0);
        Assert.Equal(drawsAfterApplication, rng.Draws);
    }

    private static double DamageScore(BattleCreature attacker, BattleCreature defender,
        IReadOnlyList<BattleConditionInstance> conditions) => SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [defender], 0, Chart(), new ZeroRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Conditions: conditions)).Scores.Single()
        .Components.Single(component => component.Name == "damage").Value;

    private static BattleConditionInstance Condition(BattleSide side, int duration) => new(0,
        SideConditions.For(BattleSideCondition.SpeedBoost), SideConditions.Owner(side),
        new BattleConditionSource(new BattleSlot(side, 0), 0), 0, 0, duration,
        SideConditions.For(BattleSideCondition.SpeedBoost).Tags, new Dictionary<string, int>(), 1);

    private static BattleTurnActions Actions(BattleAction player0, BattleAction player1,
        BattleAction enemy0, BattleAction enemy1) => new(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), player0),
            new(new BattleSlot(BattleSide.Player, 1), player1),
            new(new BattleSlot(BattleSide.Enemy, 0), enemy0),
            new(new BattleSlot(BattleSide.Enemy, 1), enemy1),
        ]);

    private static BattleSide[] UsedSides(IEnumerable<BattleEvent> events) =>
        events.OfType<MoveUsed>().Select(row => row.Side).ToArray();

    private static BattleMove Boost(string slug, int duration) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.UsersField,
        secondaryEffects: [new SetSideConditionEffect(BattleSideCondition.SpeedBoost, duration)]);

    private static BattleMove Room(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.EntireField,
        secondaryEffects: [new SetFieldConditionEffect(BattleFieldCondition.TrickRoom, 5)]);

    private static BattleMove SpeedFormula() => new(
        EntityId.Parse("move:formula"), Normal, DamageClass.Special, null, null, 20, 0, 0,
        secondaryEffects: [new SpeedRatioPowerEffect(HpRatioPowerSource.User, HpRatioPowerSource.Target,
            null, 0, null, [new(0, 40), new(1, 60), new(2, 80)])]);

    private static BattleMove Inert(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.User);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
        new Stats(500, 100, 100, 100, 100, speed), moves);

    private static Move DataMove(int? duration = null) => new()
    {
        Id = EntityId.Parse("move:data"), Name = "Data", Type = Normal,
        DamageClass = DamageClass.Status, Target = MoveTarget.UsersField, Pp = 10,
        Effects = [new Effect
        {
            Op = "sideCondition",
            Params = duration is null
                ? new Dictionary<string, JsonElement> { ["condition"] = JsonSerializer.SerializeToElement("speedBoost") }
                : new Dictionary<string, JsonElement>
                {
                    ["condition"] = JsonSerializer.SerializeToElement("speedBoost"),
                    ["duration"] = JsonSerializer.SerializeToElement(duration.Value),
                },
        }],
    };

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private sealed class ZeroRng : IRng
    {
        public int Next(int maxExclusive) => maxExclusive == 16 ? 15 : 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0.99;
    }

    private sealed class CountingRng : IRng
    {
        public int Draws { get; private set; }
        public int Next(int maxExclusive) { Draws++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Draws++; return minInclusive; }
        public double NextDouble() { Draws++; return 0.99; }
    }
}
