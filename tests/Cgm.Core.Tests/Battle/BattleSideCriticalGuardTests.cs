using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleSideCriticalGuardTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void Compiler_AdmitsStrictCriticalGuardWithFiveTurnDefault()
    {
        BattleMove compiled = MoveCompiler.ToBattleMove(DataMove());
        Assert.Contains(compiled.SecondaryEffects, effect => effect is SetSideConditionEffect
            { Condition: BattleSideCondition.CriticalGuard, Duration: 5 });

        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(0)));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove() with
        {
            DamageClass = DamageClass.Physical,
            Power = 40,
        }));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove() with
        {
            Target = MoveTarget.OpponentsField,
        }));
    }

    [Fact]
    public void CriticalGuard_ClampsOnlyOpposingCriticalChanceToZero()
    {
        BattleConditionInstance condition = Condition(BattleSide.Enemy, 5);
        BattleHookDispatchSnapshot opposing = SideConditions.CollectCriticalHooks(
            [condition], BattleSide.Player, BattleSide.Enemy, 3);
        BattleHookDispatchSnapshot sameSide = SideConditions.CollectCriticalHooks(
            [condition], BattleSide.Enemy, BattleSide.Enemy, 3);

        BattleQueryResult result = BattleQuery.Evaluate(BattleQueryId.CriticalChance,
            BattleRolls.CritChanceValue(4),
            [new(BattleQueryStage.Hooks, BattleQueryOperation.Add, new BattleQueryValue(1, 4),
                OwnerScope: BattleQueryOwnerScope.Source, InsertionOrder: 1),
             .. opposing.QueryModifiers(BattleQueryId.CriticalChance)]);
        Assert.Equal(new BattleQueryValue(0), result.FinalValue);
        Assert.Single(opposing.Trace);
        Assert.Empty(sameSide.QueryModifiers(BattleQueryId.CriticalChance));
    }

    [Fact]
    public void CriticalGuard_SuppressesCritAndPreservesCritThenDamageDraws()
    {
        var clearRng = new CountingRng();
        var guardedRng = new CountingRng();
        BattleController clear = DamageBattle(guard: false, screen: false, clearRng);
        BattleController guarded = DamageBattle(guard: true, screen: false, guardedRng);

        int clearDamage = ResolveAttack(clear);
        int guardedDamage = ResolveAttack(guarded);

        Assert.True(clearDamage > guardedDamage);
        Assert.Contains(clear.Log, row => row is DamageDealt { Crit: true });
        Assert.Contains(guarded.Log, row => row is DamageDealt { Crit: false });
        Assert.Equal(clearRng.DrawKinds, guardedRng.DrawKinds);
        BattleQueryResult query = Assert.Single(guarded.QueryTrace,
            row => row.Result.Query == BattleQueryId.CriticalChance).Result;
        Assert.Equal(new BattleQueryValue(0), query.FinalValue);
        Assert.Contains(query.Steps, step => step.OwnerScope == BattleQueryOwnerScope.TargetSide);
    }

    [Fact]
    public void CriticalGuard_MakesGuardedCritObeyStagesAndPhysicalScreen()
    {
        BattleController critical = DamageBattle(guard: false, screen: true, new CountingRng());
        BattleController guarded = DamageBattle(guard: true, screen: true, new CountingRng());
        critical.Active(BattleSide.Enemy).SetStage(StatKind.Atk, -6);
        critical.Active(BattleSide.Player).SetStage(StatKind.Def, 6);
        guarded.Active(BattleSide.Enemy).SetStage(StatKind.Atk, -6);
        guarded.Active(BattleSide.Player).SetStage(StatKind.Def, 6);

        int criticalDamage = ResolveAttack(critical);
        int guardedDamage = ResolveAttack(guarded);

        Assert.True(criticalDamage > guardedDamage * 4);
        Assert.DoesNotContain(critical.QueryTrace, row => row.Result.Query == BattleQueryId.FinalDamage
            && row.Result.Steps.Any(step => step.OwnerScope == BattleQueryOwnerScope.TargetSide));
        Assert.Contains(guarded.QueryTrace, row => row.Result.Query == BattleQueryId.FinalDamage
            && row.Result.Steps.Any(step => step.OwnerScope == BattleQueryOwnerScope.TargetSide));
    }

    [Fact]
    public void CriticalGuard_UsesOneSharedDoublesConditionAcrossTargetsAndHits()
    {
        BattleCreature player0 = Creature("player_0", 100, Guard("guard"), Inert("wait_0"));
        BattleCreature player1 = Creature("player_1", 90, Inert("wait_1"));
        BattleCreature enemy0 = Creature("enemy_0", 80, SpreadMultiHit("spread"));
        BattleCreature enemy1 = Creature("enemy_1", 70, Inert("wait_2"));
        var rng = new CountingRng();
        var battle = new BattleController([player0, player1], [enemy0, enemy1], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), rng);

        battle.ResolveTurn(Actions(new UseMove(0), new Pass(), new Pass(), new Pass()));
        Assert.Single(battle.ConditionSnapshot);
        int before = rng.DrawKinds.Count;
        battle.ResolveTurn(Actions(new UseMove(1), new UseMove(0), new UseMove(0), new UseMove(0)));

        BattleQueryResult[] queries = battle.QueryTrace
            .Where(row => row.Result.Query == BattleQueryId.CriticalChance)
            .Select(row => row.Result).ToArray();
        Assert.Equal(4, queries.Length);
        Assert.All(queries, query => Assert.Equal(new BattleQueryValue(0), query.FinalValue));
        Assert.Equal(4, battle.HookTrace.Count(row => row.Checkpoint == BattleConditionHook.CriticalQuery
            && row.PayloadKind == BattleHookPayloadKind.QueryModifier));
        Assert.Equal(["double", "int", "double", "int", "double", "int", "double", "int"],
            rng.DrawKinds.Skip(before));
    }

    [Fact]
    public void CriticalGuard_RejectsDuplicatePersistsAfterSourceSwitchAndExpires()
    {
        BattleCreature source = Creature("source", 100, Guard("guard"));
        BattleCreature reserve = Creature("reserve", 90, Inert("reserve_wait"));
        BattleCreature target = Creature("target", 80, Inert("target_wait"));
        var battle = new BattleController([source, reserve], [target], Chart(), new CountingRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> duplicate = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(duplicate, row => row is MoveFailed
            { Reason: MoveFailureReason.ConditionAlreadyActive });
        Assert.Equal(3, Assert.Single(battle.ConditionSnapshot).RemainingDuration);

        battle.ResolveTurn(new Switch(1), new Pass());
        BattleConditionInstance active = Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(0, active.Source.PartyIndex);
        Assert.Equal(2, active.RemainingDuration);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Empty(battle.ConditionSnapshot);
        Assert.Contains(battle.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Expired);
    }

    [Fact]
    public void CriticalGuard_ReducesSharedAiCriticalExpectation()
    {
        BattleCreature attacker = Creature("attacker", 100, Hit("hit"));
        BattleCreature defender = Creature("defender", 90, Inert("wait"));
        double clear = DamageScore(attacker, defender, []);
        double guarded = DamageScore(attacker, defender, [Condition(BattleSide.Player, 5)]);

        Assert.True(guarded < clear);
    }

    [Fact]
    public void CriticalGuard_ReplayIsStable()
    {
        static (string Events, string Effects, string Queries, string Hooks, string Draws) Run()
        {
            var rng = new CountingRng();
            BattleController battle = DamageBattle(guard: true, screen: false, rng);
            ResolveAttack(battle);
            return (JsonSerializer.Serialize(battle.Log), JsonSerializer.Serialize(battle.Trace),
                JsonSerializer.Serialize(battle.QueryTrace), JsonSerializer.Serialize(battle.HookTrace),
                string.Join(',', rng.DrawKinds));
        }

        Assert.Equal(Run(), Run());
    }

    private static int ResolveAttack(BattleController battle)
    {
        BattleCreature target = battle.Active(BattleSide.Player);
        int before = target.CurrentHp;
        battle.ResolveTurn(new UseMove(2), new UseMove(1));
        return before - target.CurrentHp;
    }

    private static BattleController DamageBattle(bool guard, bool screen, CountingRng rng)
    {
        var setup = new List<BattleMove>();
        setup.Add(guard ? Guard("guard") : Inert("setup_guard"));
        setup.Add(screen ? Screen("screen") : Inert("setup_screen"));
        setup.Add(Inert("player_wait"));
        BattleCreature target = Creature("target", 100, setup.ToArray());
        BattleCreature attacker = Creature("attacker", 80, Inert("enemy_wait"), Hit("hit"));
        var battle = new BattleController(target, attacker, Chart(), rng);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        return battle;
    }

    private static double DamageScore(BattleCreature attacker, BattleCreature defender,
        IReadOnlyList<BattleConditionInstance> conditions) => SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [defender], 0, Chart(), new CountingRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Conditions: conditions)).Scores.Single()
        .Components.Single(component => component.Name == "damage").Value;

    private static BattleConditionInstance Condition(BattleSide side, int duration) => new(0,
        SideConditions.For(BattleSideCondition.CriticalGuard), SideConditions.Owner(side),
        new BattleConditionSource(new BattleSlot(side, 0), 0), 0, 0, duration,
        SideConditions.For(BattleSideCondition.CriticalGuard).Tags, new Dictionary<string, int>(), 1);

    private static BattleTurnActions Actions(BattleAction player0, BattleAction player1,
        BattleAction enemy0, BattleAction enemy1) => new(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), player0),
            new(new BattleSlot(BattleSide.Player, 1), player1),
            new(new BattleSlot(BattleSide.Enemy, 0), enemy0),
            new(new BattleSlot(BattleSide.Enemy, 1), enemy1),
        ]);

    private static BattleMove Guard(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.UsersField,
        secondaryEffects: [new SetSideConditionEffect(BattleSideCondition.CriticalGuard, 5)]);

    private static BattleMove Screen(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.UsersField,
        secondaryEffects: [new SetSideConditionEffect(BattleSideCondition.PhysicalScreen, 5)]);

    private static BattleMove Hit(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Physical, 100, null, 20, 0, 4);

    private static BattleMove SpreadMultiHit(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Physical, 20, null, 20, 0, 4,
        multiHitMin: 2, multiHitMax: 2, target: MoveTarget.AllOpponents);

    private static BattleMove Inert(string slug) => new(
        EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
        target: MoveTarget.User);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
        new Stats(2000, 120, 120, 120, 120, speed), moves);

    private static Move DataMove(int? duration = null) => new()
    {
        Id = EntityId.Parse("move:data"), Name = "Data", Type = Normal,
        DamageClass = DamageClass.Status, Target = MoveTarget.UsersField, Pp = 10,
        Effects = [new Effect
        {
            Op = "sideCondition",
            Params = duration is null
                ? new Dictionary<string, JsonElement>
                    { ["condition"] = JsonSerializer.SerializeToElement("criticalGuard") }
                : new Dictionary<string, JsonElement>
                {
                    ["condition"] = JsonSerializer.SerializeToElement("criticalGuard"),
                    ["duration"] = JsonSerializer.SerializeToElement(duration.Value),
                },
        }],
    };

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private sealed class CountingRng : IRng
    {
        public List<string> DrawKinds { get; } = [];
        public int Next(int maxExclusive) { DrawKinds.Add("int"); return maxExclusive - 1; }
        public int Next(int minInclusive, int maxExclusive) { DrawKinds.Add("int"); return maxExclusive - 1; }
        public double NextDouble() { DrawKinds.Add("double"); return 0; }
    }
}
