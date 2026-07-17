using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTerrainConditionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Electric = EntityId.Parse("type:electric");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Dragon = EntityId.Parse("type:dragon");
    private static readonly EntityId Psychic = EntityId.Parse("type:psychic");
    private static readonly EntityId Flying = EntityId.Parse("type:flying");

    private static TypeChart Chart() => new([
        new TypeDef { Id = Normal }, new TypeDef { Id = Electric }, new TypeDef { Id = Grass },
        new TypeDef { Id = Dragon }, new TypeDef { Id = Psychic }, new TypeDef { Id = Flying },
    ]);

    private static BattleMove Inert(string slug = "inert") =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 30, 0, 0);

    private static BattleMove SetTerrain(Terrain terrain, string slug) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 30, 0, 0,
            secondaryEffects: [new SetTerrainEffect(terrain)]);

    private static BattleMove Hit(EntityId type, int power, string slug, int priority = 0,
        MoveTarget target = MoveTarget.Selected) =>
        new(EntityId.Parse($"move:{slug}"), type, DamageClass.Special, power, null, 30, priority, 0,
            target: target);

    private static BattleMove Status(PersistentStatus status, string slug) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 30, 0, 0,
            ailment: status, ailmentChance: 100);

    private static BattleMove Confuse(string slug) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 30, 0, 0,
            confuseChance: 100);

    private static BattleCreature Creature(string slug, int speed, IReadOnlyList<EntityId>? types = null,
        int hp = 320, params BattleMove[] moves) => new(EntityId.Parse($"species:{slug}"), slug, 50,
            types ?? [Normal], new Stats(hp, 100, 100, 100, 100, speed), moves);

    private static BattleFieldInputs Field(Terrain terrain, int? duration = null,
        BattleEnvironment environment = BattleEnvironment.Building) =>
        new(BattleRulesets.ModernReference, NaturalEnvironment: environment,
            InitialTerrain: terrain, InitialTerrainDuration: duration);

    [Fact]
    public void RegistryAndGroundedQuery_LockTheIntrinsicRows()
    {
        Assert.Equal(4, TerrainConditions.Definitions.Count);
        Assert.All(TerrainConditions.Definitions, definition =>
        {
            Assert.Equal(BattleConditionScope.Terrain, definition.Scope);
            Assert.Equal("terrain", definition.StackingKey);
            Assert.Equal(BattleConditionStackingPolicy.Replace, definition.StackingPolicy);
            Assert.Equal(TerrainConditions.DefaultTurns, definition.DefaultDuration);
            Assert.Equal(BattleIntentCheckpoint.TurnEnd, definition.DurationCheckpoint);
            Assert.Equal(BattleConditionSwitchPolicy.StayScope, definition.SwitchPolicy);
            Assert.Equal(BattleConditionFaintPolicy.Persist, definition.FaintPolicy);
        });
        Assert.False(TerrainConditions.Supports(Terrain.Electric, BattleRulesets.Gen4Like));
        Assert.True(TerrainConditions.Supports(Terrain.Electric, BattleRulesets.ModernReference));

        BattleCreature grounded = Creature("grounded", 2, [Normal], moves: [Inert()]);
        BattleCreature airborne = Creature("airborne", 1, [Flying], moves: [Inert("airborne_inert")]);
        Assert.Equal(1, TerrainConditions.GroundedQuery(grounded).FinalValue.ToInt32());
        Assert.Equal(0, TerrainConditions.GroundedQuery(airborne).FinalValue.ToInt32());

        var battle = new BattleController(grounded, airborne, Chart(), new Rng(1));
        Assert.True(battle.IsGrounded(new BattleSlot(BattleSide.Player, 0)));
        Assert.False(battle.IsGrounded(new BattleSlot(BattleSide.Enemy, 0)));
        Assert.All(battle.QueryTrace, trace => Assert.Equal(BattleQueryId.Grounded, trace.Result.Query));
    }

    [Fact]
    public void NaturalInput_UsesStoreLifecycleAndEffectiveEnvironment()
    {
        var rng = new CountingRng();
        BattleCreature player = Creature("player", 100, hp: 160, moves: [Inert()]);
        BattleCreature enemy = Creature("enemy", 1, hp: 160, moves: [Inert("enemy_inert")]);
        player.TakeDamage(32);
        enemy.TakeDamage(32);
        var battle = new BattleController(player, enemy, Chart(), rng,
            fieldInputs: Field(Terrain.Grassy, 1, BattleEnvironment.Cave));

        BattleConditionInstance condition = Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(new BattleConditionId("terrain:grassy"), condition.Definition.Id);
        Assert.Equal(new BattleConditionSource(), condition.Source);
        Assert.Equal(BattleEnvironment.GrassyTerrain, battle.EffectiveEnvironment);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal([new BattleSlot(BattleSide.Player, 0), new BattleSlot(BattleSide.Enemy, 0)],
            events.OfType<TerrainHealed>().Select(entry => entry.Slot).ToArray());
        Assert.All(events.OfType<TerrainHealed>(), entry => Assert.Equal(10, entry.Amount));
        Assert.Equal(Terrain.None, battle.CurrentTerrain);
        Assert.Equal(BattleEnvironment.Cave, battle.EffectiveEnvironment);
        Assert.Contains(events, entry => entry is TerrainEnded { Terrain: Terrain.Grassy });
        Assert.Contains(battle.ConditionTrace, entry => entry.Kind == BattleConditionTraceKind.Expired);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void SetSameAndReplace_CaptureSourceWithoutRefreshingSameTerrain()
    {
        var battle = new BattleController(
            Creature("source", 100, moves: [SetTerrain(Terrain.Electric, "electric"),
                SetTerrain(Terrain.Grassy, "grassy")]),
            Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(3, Assert.Single(battle.ConditionSnapshot).RemainingDuration);
        Assert.Single(battle.Log.OfType<TerrainChanged>());

        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        BattleConditionInstance condition = Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(new BattleConditionId("terrain:grassy"), condition.Definition.Id);
        Assert.Equal(new BattleConditionSource(new BattleSlot(BattleSide.Player, 0), 0), condition.Source);
        Assert.Equal(4, condition.RemainingDuration);
        Assert.Contains(battle.ConditionTrace, entry => entry.Kind == BattleConditionTraceKind.Replaced);
    }

    [Fact]
    public void InitialInput_RejectsLegacyTerrainOrInvalidDuration()
    {
        BattleCreature source = Creature("source", 2, moves: [Inert()]);
        BattleCreature target = Creature("target", 1, moves: [Inert("target_inert")]);

        Assert.Throws<ArgumentException>(() => new BattleController(source, target, Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(InitialTerrain: Terrain.Electric)));
        Assert.Throws<ArgumentException>(() => new BattleController(source, target, Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference, InitialTerrainDuration: 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleController(source, target, Chart(), new Rng(1),
            fieldInputs: Field(Terrain.Electric, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleController(source, target, Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference,
                NaturalEnvironment: (BattleEnvironment)999)));
    }

    [Fact]
    public void DamageHooks_FilterBoostAndReductionByGroundedSubject()
    {
        IReadOnlyList<BattleConditionInstance> electric = StoresWith(Terrain.Electric).Snapshot();
        IReadOnlyList<BattleConditionInstance> misty = StoresWith(Terrain.Misty).Snapshot();

        Assert.Equal(new BattleQueryValue(3, 2), Assert.Single(TerrainConditions.CollectDamageHooks(
            electric, "electric", sourceGrounded: true, targetGrounded: true, 0)
            .QueryModifiers(BattleQueryId.FinalDamage)).Operand);
        Assert.Empty(TerrainConditions.CollectDamageHooks(
            electric, "electric", sourceGrounded: false, targetGrounded: true, 0).Invocations);
        Assert.Equal(new BattleQueryValue(1, 2), Assert.Single(TerrainConditions.CollectDamageHooks(
            misty, "dragon", sourceGrounded: true, targetGrounded: true, 0)
            .QueryModifiers(BattleQueryId.FinalDamage)).Operand);
        Assert.Empty(TerrainConditions.CollectDamageHooks(
            misty, "dragon", sourceGrounded: true, targetGrounded: false, 0).Invocations);
    }

    [Fact]
    public void ResolverAndSmartAi_UseTheSameGroundedTerrainDamageRows()
    {
        static int Damage(BattleFieldInputs? field)
        {
            var battle = new BattleController(
                Creature("source", 100, [Normal], moves: [Hit(Electric, 80, "electric_hit")]),
                Creature("target", 1, [Normal], moves: [Inert()]), Chart(), new Rng(4), fieldInputs: field);
            return Assert.Single(battle.ResolveTurn(new UseMove(0), new UseMove(0))
                .OfType<DamageDealt>(), entry => entry.Slot.Side == BattleSide.Enemy).Amount;
        }

        Assert.True(Damage(Field(Terrain.Electric))
            > Damage(new BattleFieldInputs(BattleRulesets.ModernReference)));

        BattleCreature attacker = Creature("ai", 100, [Grass], moves:
            [Hit(Electric, 80, "electric_hit"), Hit(Normal, 100, "plain_hit")]);
        BattleCreature target = Creature("ai_target", 1, [Normal], moves: [Inert()]);
        var weights = new SmartAiWeights { NoiseFraction = 0 };
        SmartAiDecision clear = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [target], 0, Chart(), new Rng(2), Weights: weights));
        SmartAiDecision terrain = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [target], 0, Chart(), new Rng(2), Weights: weights,
            Conditions: StoresWith(Terrain.Electric).Snapshot(), Ruleset: BattleRulesets.ModernReference));

        Assert.Equal(new UseMove(1), clear.Action);
        Assert.Equal(new UseMove(0), terrain.Action);
    }

    [Fact]
    public void ElectricAndMisty_StatusFiltersUseGroundedTargetAndNoDeniedRng()
    {
        Assert.Single(TerrainConditions.CollectStatusHooks(StoresWith(Terrain.Electric).Snapshot(),
            PersistentStatus.Sleep, targetGrounded: true, 0).Filters());
        Assert.Empty(TerrainConditions.CollectStatusHooks(StoresWith(Terrain.Electric).Snapshot(),
            PersistentStatus.Sleep, targetGrounded: false, 0).Filters());
        Assert.Single(TerrainConditions.CollectStatusHooks(StoresWith(Terrain.Misty).Snapshot(),
            PersistentStatus.Burn, targetGrounded: true, 0).Filters());

        var rng = new CountingRng();
        var battle = new BattleController(
            Creature("source", 100, moves: [Confuse("confuse")]),
            Creature("target", 1, moves: [Inert()]), Chart(), rng,
            fieldInputs: Field(Terrain.Misty));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.False(battle.Active(BattleSide.Enemy).IsConfused);
        Assert.DoesNotContain(events, entry => entry is Confused);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void ResolverAndSmartAi_UseTheSameTerrainStatusAndPriorityFilters()
    {
        var groundedBattle = new BattleController(
            Creature("status_source", 100, moves: [Status(PersistentStatus.Burn, "burn")]),
            Creature("grounded_target", 1, [Normal], moves: [Inert()]), Chart(), new Rng(1),
            fieldInputs: Field(Terrain.Misty));
        var airborneBattle = new BattleController(
            Creature("air_status_source", 100, moves: [Status(PersistentStatus.Burn, "air_burn")]),
            Creature("airborne_target", 1, [Flying], moves: [Inert("air_inert")]), Chart(), new Rng(1),
            fieldInputs: Field(Terrain.Misty));

        groundedBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        airborneBattle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Null(groundedBattle.Active(BattleSide.Enemy).Status);
        Assert.Equal(PersistentStatus.Burn, airborneBattle.Active(BattleSide.Enemy).Status);

        BattleCreature statusAi = Creature("status_ai", 100, moves:
            [Status(PersistentStatus.Sleep, "ai_sleep"), Status(PersistentStatus.Paralysis, "ai_para")]);
        BattleCreature target = Creature("status_target", 1, moves: [Inert()]);
        var context = new SmartAiContext([statusAi], 0, [target], 0, Chart(), new Rng(2),
            Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: StoresWith(Terrain.Electric).Snapshot(), Ruleset: BattleRulesets.ModernReference);
        SmartAiDecision statusDecision = SmartAi.ChooseAction(context);
        Assert.Equal(new UseMove(1), statusDecision.Action);
        Assert.DoesNotContain(statusDecision.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component.Name == "status");

        BattleCreature priorityAi = Creature("priority_ai", 100, moves:
            [Hit(Psychic, 120, "fast", priority: 1), Hit(Normal, 40, "ordinary")]);
        SmartAiDecision priorityDecision = SmartAi.ChooseAction(context with
        {
            EnemyParty = [priorityAi],
            Conditions = StoresWith(Terrain.Psychic).Snapshot(),
        });
        Assert.Equal(new UseMove(1), priorityDecision.Action);
    }

    [Fact]
    public void PsychicTerrain_BlocksPriorityPerGroundedTargetBeforeAccuracy()
    {
        BattleMove spread = Hit(Psychic, 60, "priority_spread", priority: 1,
            target: MoveTarget.AllOpponents);
        var battle = new BattleController(
            [Creature("source", 100, moves: [spread]), Creature("ally", 90, moves: [Inert("ally")])],
            [Creature("grounded", 20, [Normal], moves: [Inert("grounded_inert")]),
                Creature("airborne", 10, [Flying], moves: [Inert("airborne_inert")])],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(3),
            fieldInputs: Field(Terrain.Psychic));
        var actions = new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), new UseMove(0)),
            new(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(actions);

        Assert.Contains(events, entry => entry is TerrainPriorityBlocked
            { Target: { Side: BattleSide.Enemy, Position: 0 } });
        Assert.DoesNotContain(events.OfType<DamageDealt>(), entry => entry.Slot == new BattleSlot(BattleSide.Enemy, 0));
        Assert.Contains(events.OfType<DamageDealt>(), entry => entry.Slot == new BattleSlot(BattleSide.Enemy, 1));
        Assert.Contains(battle.ActionHistory.DamageSnapshot(), record =>
            record.Target.Slot == new BattleSlot(BattleSide.Enemy, 0)
            && record.Failure == BattleDamageFailure.Blocked && record.HitNumber == 0);
        Assert.Contains(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.TryHit && entry.Invoked);
    }

    [Fact]
    public void PsychicTerrain_DoesNotBlockPositivePriorityFieldAction()
    {
        BattleMove fieldAction = new(EntityId.Parse("move:field_action"), Normal, DamageClass.Status,
            null, null, 30, 1, 0, target: MoveTarget.EntireField,
            secondaryEffects: [new SetTerrainEffect(Terrain.Grassy)]);
        var battle = new BattleController(
            Creature("source", 100, moves: [fieldAction]),
            Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1),
            fieldInputs: Field(Terrain.Psychic));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(Terrain.Grassy, battle.CurrentTerrain);
        Assert.DoesNotContain(events, entry => entry is TerrainPriorityBlocked);
    }

    [Fact]
    public void Replay_ReproducesTerrainEventsQueriesAndHooks()
    {
        static string Run()
        {
            var battle = new BattleController(
                Creature("source", 100, moves: [Hit(Electric, 60, "electric_hit")]),
                Creature("target", 1, moves: [Inert()]), Chart(), new Rng(7),
                fieldInputs: Field(Terrain.Electric, 2, BattleEnvironment.Road));
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            return JsonSerializer.Serialize(new
            {
                Events = battle.Log.Select(entry => entry.ToString()),
                Conditions = battle.ConditionSnapshot,
                battle.ConditionTrace,
                battle.QueryTrace,
                battle.HookTrace,
            });
        }

        Assert.Equal(Run(), Run());
    }

    private static BattleConditionStores StoresWith(Terrain terrain)
    {
        var stores = new BattleConditionStores(new BattleConditionRegistry(TerrainConditions.Definitions));
        stores.Apply(new BattleConditionApplication(TerrainConditions.For(terrain).Definition!.Id,
            TerrainConditions.FieldOwner, new BattleConditionSource(), 0, 0));
        return stores;
    }

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public double NextDouble() { Calls++; return 0; }
    }
}
