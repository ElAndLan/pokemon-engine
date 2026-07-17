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

    private static BattleCreature TerrainSummoner(string slug, Terrain terrain, Terrain? changeTo = null,
        IReadOnlyList<Effect>? heldEffects = null)
    {
        var hooks = new List<AbilityHook>
        {
            new()
            {
                Hook = AbilityHookPoint.OnSwitchIn,
                Effects = [new Effect { Op = "terrainSummon", Params = Params(("terrain", terrain.ToString())) }],
            },
        };
        if (changeTo is not null)
            hooks.Add(new AbilityHook
            {
                Hook = AbilityHookPoint.OnTerrainChange,
                Effects = [new Effect { Op = "terrainSummon", Params = Params(("terrain", changeTo.Value.ToString())) }],
            });
        return new BattleCreature(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(320, 100, 100, 100, 100, 100), [Inert($"{slug}_inert")],
            abilityHooks: hooks, heldItemBattleEffects: heldEffects);
    }

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
        Assert.Equal(new { Natural = BattleEnvironment.Cave, Effective = BattleEnvironment.GrassyTerrain },
            new { battle.Environment.Natural, battle.Environment.Effective });

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal([new BattleSlot(BattleSide.Player, 0), new BattleSlot(BattleSide.Enemy, 0)],
            events.OfType<TerrainHealed>().Select(entry => entry.Slot).ToArray());
        Assert.All(events.OfType<TerrainHealed>(), entry => Assert.Equal(10, entry.Amount));
        Assert.Equal(Terrain.None, battle.CurrentTerrain);
        Assert.Equal(BattleEnvironment.Cave, battle.EffectiveEnvironment);
        Assert.Equal(battle.NaturalEnvironment, battle.Environment.Effective);
        Assert.Contains(events, entry => entry is TerrainEnded { Terrain: Terrain.Grassy });
        Assert.Contains(battle.ConditionTrace, entry => entry.Kind == BattleConditionTraceKind.Expired);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void TerrainSummonAbility_SetsTerrainOnSwitchIn()
    {
        BattleCreature reserve = TerrainSummoner("summoner", Terrain.Electric);
        var rng = new CountingRng();
        var battle = new BattleController([Creature("lead", 101, moves: [Inert("lead_inert")]), reserve],
            [Creature("enemy", 1, moves: [Inert("enemy_inert")])], Chart(), rng,
            fieldInputs: Field(Terrain.None));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Contains(events, entry => entry is SwitchedIn { Side: BattleSide.Player, PartyIndex: 1 });
        Assert.Contains(events, entry => entry is TerrainChanged { Terrain: Terrain.Electric });
        Assert.Equal(new BattleConditionSource(new BattleSlot(BattleSide.Player, 0), 1),
            Assert.Single(battle.ConditionSnapshot).Source);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void TerrainChangeHook_RunsAfterChangedEventAndStopsNestedRedispatch()
    {
        BattleCreature reserve = TerrainSummoner("changer", Terrain.Electric, Terrain.Grassy);
        var battle = new BattleController([Creature("lead", 101, moves: [Inert("lead_inert")]), reserve],
            [Creature("enemy", 1, moves: [Inert("enemy_inert")])], Chart(), new CountingRng(),
            fieldInputs: Field(Terrain.None));

        Terrain[] terrains = battle.ResolveTurn(new Switch(1), new UseMove(0))
            .OfType<TerrainChanged>()
            .Select(entry => entry.Terrain)
            .ToArray();

        Assert.Equal([Terrain.Electric, Terrain.Grassy], terrains);
        Assert.Equal(Terrain.Grassy, battle.CurrentTerrain);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void TerrainDurationExtension_AppliesOnlyToHolderSummonedTerrain(bool sourceHoldsExtension,
        int expectedAfterSwitchTurn)
    {
        IReadOnlyList<Effect> extension =
        [new Effect { Op = "terrainDurationExtend", Params = Params(("turns", 2)) }];
        BattleCreature reserve = TerrainSummoner("summoner", Terrain.Psychic,
            heldEffects: sourceHoldsExtension ? extension : null);
        BattleCreature enemy = new(EntityId.Parse("species:enemy"), "enemy", 50, [Normal],
            new Stats(320, 100, 100, 100, 100, 1), [Inert("enemy_inert")],
            heldItemBattleEffects: sourceHoldsExtension ? null : extension);
        var battle = new BattleController([Creature("lead", 101, moves: [Inert("lead_inert")]), reserve],
            [enemy], Chart(), new CountingRng(), fieldInputs: Field(Terrain.None));

        battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Equal(expectedAfterSwitchTurn, Assert.Single(battle.ConditionSnapshot).RemainingDuration);
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
        Assert.Equal(BattleEnvironment.GrassyTerrain, battle.Environment.Effective);
        Assert.Equal(BattleEnvironment.Building, battle.Environment.Natural);
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
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleController(source, target, Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference,
                NaturalEnvironment: BattleEnvironment.ElectricTerrain)));
    }

    [Theory]
    [InlineData(BattleEnvironment.Building)]
    [InlineData(BattleEnvironment.Cave)]
    [InlineData(BattleEnvironment.DeepWater)]
    [InlineData(BattleEnvironment.Desert)]
    [InlineData(BattleEnvironment.Grass)]
    [InlineData(BattleEnvironment.Mountain)]
    [InlineData(BattleEnvironment.Ocean)]
    [InlineData(BattleEnvironment.Pond)]
    [InlineData(BattleEnvironment.Road)]
    [InlineData(BattleEnvironment.ShallowWater)]
    [InlineData(BattleEnvironment.Snow)]
    [InlineData(BattleEnvironment.TallGrass)]
    public void EnvironmentState_AcceptsEveryNaturalValue(BattleEnvironment natural)
    {
        BattleEnvironmentState clear = BattleEnvironmentState.Resolve(natural);
        BattleEnvironmentState electric = BattleEnvironmentState.Resolve(natural, Terrain.Electric);

        Assert.Equal(natural, clear.Natural);
        Assert.Equal(natural, clear.Effective);
        Assert.Equal(natural, electric.Natural);
        Assert.Equal(BattleEnvironment.ElectricTerrain, electric.Effective);
    }

    [Theory]
    [InlineData(BattleEnvironment.ElectricTerrain)]
    [InlineData(BattleEnvironment.GrassyTerrain)]
    [InlineData(BattleEnvironment.MistyTerrain)]
    [InlineData(BattleEnvironment.PsychicTerrain)]
    public void EnvironmentState_RejectsTerrainOnlyNaturalValues(BattleEnvironment terrainEnvironment)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleEnvironmentState.Resolve(terrainEnvironment));
    }

    [Fact]
    public void ResolverAndSmartAi_ShareTheSameEnvironmentStateAcrossExpiry()
    {
        var battle = new BattleController(
            Creature("source", 100, moves: [Inert()]),
            Creature("target", 1, moves: [Inert("target_inert")]), Chart(), new CountingRng(),
            fieldInputs: Field(Terrain.Misty, 1, BattleEnvironment.DeepWater));
        var context = new SmartAiContext([battle.Active(BattleSide.Player)], 0,
            [battle.Active(BattleSide.Enemy)], 0, Chart(), new Rng(1),
            Conditions: battle.ConditionSnapshot, Ruleset: BattleRulesets.ModernReference,
            NaturalEnvironment: battle.NaturalEnvironment);

        Assert.Equal(battle.Environment, context.Environment);
        Assert.Equal(BattleEnvironment.MistyTerrain, context.Environment.Effective);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        context = context with { Conditions = battle.ConditionSnapshot };

        Assert.Equal(battle.Environment, context.Environment);
        Assert.Equal(BattleEnvironment.DeepWater, context.Environment.Effective);
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
    public void AuthoredTerrainRows_ResolveTypePowerPriorityAndAiParity()
    {
        var interaction = new TerrainMoveEffect(TerrainMoveSubject.User,
            new Dictionary<Terrain, EntityId> { [Terrain.Electric] = Electric },
            new Dictionary<Terrain, Fraction> { [Terrain.Electric] = new(2, 1) },
            new Dictionary<Terrain, int> { [Terrain.Electric] = 1 },
            new HashSet<Terrain>());
        BattleMove terrainHit = new(EntityId.Parse("move:terrain_hit"), Normal, DamageClass.Special,
            50, 100, 30, 0, 0, secondaryEffects: [interaction]);
        var battle = new BattleController(
            Creature("source", 20, [Normal], moves: [terrainHit]),
            Creature("target", 100, [Normal], moves: [Inert()]), Chart(), new Rng(2),
            fieldInputs: Field(Terrain.Electric));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(BattleSide.Player, events.OfType<MoveUsed>().First().Side);
        Assert.Equal(Electric, Assert.Single(battle.ActionHistory.DamageSnapshot()).DamageType);
        Assert.Equal(100, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.BasePower
            && entry.SourceSlot.Side == BattleSide.Player).Result.FinalValue.ToInt32());
        Assert.Contains(battle.QueryTrace, entry => entry.Result.Query == BattleQueryId.Priority
            && entry.SourceSlot.Side == BattleSide.Player && entry.Result.FinalValue.ToInt32() == 1);

        BattleCreature ai = Creature("ai", 20, [Normal], moves:
            [terrainHit, Hit(Normal, 80, "plain")]);
        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext([ai], 0,
            [Creature("ai_target", 100, moves: [Inert("ai_inert")])], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: StoresWith(Terrain.Electric).Snapshot(), Ruleset: BattleRulesets.ModernReference));
        Assert.Equal(new UseMove(0), decision.Action);
        Assert.Empty(TerrainConditions.CollectMoveTypeHooks(StoresWith(Terrain.Electric).Snapshot(),
            interaction, sourceGrounded: false, 0).MoveTypes());
        Assert.Single(TerrainConditions.CollectMoveTypeHooks(StoresWith(Terrain.Electric).Snapshot(),
            interaction with { Subject = TerrainMoveSubject.Field }, sourceGrounded: false, 0).MoveTypes());
        Assert.False(TerrainConditions.Spreads(StoresWith(Terrain.Psychic).Snapshot(),
            interaction with { SpreadTerrains = new HashSet<Terrain> { Terrain.Psychic } },
            sourceGrounded: false));

        var targetInteraction = interaction with
        {
            Subject = TerrainMoveSubject.Target,
            TypeOverrides = new Dictionary<Terrain, EntityId>(),
            PriorityModifiers = new Dictionary<Terrain, int>(),
        };
        Assert.Single(TerrainConditions.CollectBasePowerHooks(StoresWith(Terrain.Electric).Snapshot(),
            targetInteraction, sourceGrounded: true, targetGrounded: true, 0)
            .QueryModifiers(BattleQueryId.BasePower));
        Assert.Empty(TerrainConditions.CollectBasePowerHooks(StoresWith(Terrain.Electric).Snapshot(),
            targetInteraction, sourceGrounded: true, targetGrounded: false, 0)
            .QueryModifiers(BattleQueryId.BasePower));
    }

    [Fact]
    public void AuthoredTerrainSpread_MaterializesAllOpponentsOnlyForGroundedSource()
    {
        var spread = new TerrainMoveEffect(TerrainMoveSubject.User,
            new Dictionary<Terrain, EntityId>(), new Dictionary<Terrain, Fraction>(),
            new Dictionary<Terrain, int>(), new HashSet<Terrain> { Terrain.Psychic });
        BattleMove hit = new(EntityId.Parse("move:expanding"), Psychic, DamageClass.Special,
            50, 100, 30, 0, 0, target: MoveTarget.Selected, secondaryEffects: [spread]);
        var battle = new BattleController(
            [Creature("source", 100, moves: [hit]), Creature("ally", 90, moves: [Inert("ally")])],
            [Creature("target_zero", 20, moves: [Inert("zero")]),
                Creature("target_one", 10, moves: [Inert("one")])],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(4),
            fieldInputs: Field(Terrain.Psychic));
        var actions = new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), new UseMove(0),
                new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0))),
            new(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(actions);

        Assert.Equal(2, events.OfType<DamageDealt>().Count(entry => entry.Slot.Side == BattleSide.Enemy));
    }

    [Fact]
    public void TerrainGateRemovalAndHealing_UseSharedStoreAndQueriesWithoutRng()
    {
        BattleMove gatedRemoval = new(EntityId.Parse("move:roller"), Normal, DamageClass.Physical,
            40, null, 30, 0, 0, secondaryEffects: [new TerrainGateEffect(), new RemoveTerrainEffect()]);
        var clearRng = new CountingRng();
        var clear = new BattleController(Creature("clear_source", 100, moves: [gatedRemoval]),
            Creature("clear_target", 1, moves: [Inert()]), Chart(), clearRng,
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference));
        IReadOnlyList<BattleEvent> failed = clear.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(failed, entry => entry is MoveFailed { Reason: MoveFailureReason.TerrainRequired });
        Assert.DoesNotContain(failed, entry => entry is MoveUsed { Side: BattleSide.Player });
        Assert.Equal(30, gatedRemoval.Pp);
        Assert.Equal(0, clearRng.Calls);

        var terrainRng = new CountingRng();
        var active = new BattleController(Creature("active_source", 100, moves: [gatedRemoval]),
            Creature("active_target", 1, moves: [Inert()]), Chart(), terrainRng,
            fieldInputs: Field(Terrain.Grassy));
        IReadOnlyList<BattleEvent> removed = active.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(Terrain.None, active.CurrentTerrain);
        Assert.Equal(active.Environment.Natural, active.Environment.Effective);
        Assert.Contains(removed, entry => entry is TerrainEnded { Terrain: Terrain.Grassy });
        Assert.Contains(removed, entry => entry is ConditionRemoved
            { Reason: BattleConditionCleanupReason.Effect });

        BattleMove heal = new(EntityId.Parse("move:terrain_heal"), Normal, DamageClass.Status,
            null, null, 30, 0, 0, heal: new Fraction(1, 2), secondaryEffects:
            [new HealEffect(new Fraction(1, 2), TerrainFractions:
                new Dictionary<Terrain, Fraction> { [Terrain.Grassy] = new(2, 3) })]);
        BattleCreature healer = Creature("healer", 100, hp: 320, moves: [heal]);
        healer.TakeDamage(240);
        var healBattle = new BattleController(healer,
            Creature("heal_target", 1, moves: [Inert()]), Chart(), new CountingRng(),
            fieldInputs: Field(Terrain.Grassy));

        IReadOnlyList<BattleEvent> healed = healBattle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(healed, entry => entry is Healed { Amount: 213 });
        Assert.Contains(healBattle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.HealingQuery);

        BattleCreature aiHealer = Creature("ai_healer", 100, hp: 320, moves:
            [heal, Hit(Normal, 1, "weak")]);
        aiHealer.TakeDamage(240);
        SmartAiDecision healDecision = SmartAi.ChooseAction(new SmartAiContext([aiHealer], 0,
            [Creature("ai_heal_target", 1, hp: 320, moves: [Inert("ai_heal_inert")])], 0,
            Chart(), new Rng(1), Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: StoresWith(Terrain.Grassy).Snapshot(), Ruleset: BattleRulesets.ModernReference));
        Assert.Equal(new UseMove(0), healDecision.Action);
        Assert.Contains(healDecision.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component is { Name: "recovery", Value: 213 });

        BattleCreature gatedAi = Creature("gated_ai", 100, moves:
            [gatedRemoval, Hit(Normal, 1, "legal")]);
        SmartAiDecision gateDecision = SmartAi.ChooseAction(new SmartAiContext([gatedAi], 0,
            [Creature("gate_target", 1, moves: [Inert("gate_inert")])], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: [], Ruleset: BattleRulesets.ModernReference));
        Assert.Equal(new UseMove(1), gateDecision.Action);
        Assert.Contains(gateDecision.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component.Name == "terrainGate" && component.Value < 0);
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

    private static Dictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value));

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public double NextDouble() { Calls++; return 0; }
    }
}
