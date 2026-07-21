using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleGroundedConditionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Electric = EntityId.Parse("type:electric");
    private static readonly EntityId Flying = EntityId.Parse("type:flying");
    private static readonly BattleSlot PlayerSlot = new(BattleSide.Player, 0);
    private static readonly BattleSlot EnemySlot = new(BattleSide.Enemy, 0);

    [Fact]
    public void Query_UsesEffectiveTypesAndLockedOverridePrecedence()
    {
        BattleCreature creature = Creature("subject", [Normal], Inert());
        BattleConditionOwner owner = CreatureOwner(BattleSide.Player, 0, PlayerSlot);
        var stores = Stores();

        Assert.True(IsGrounded(creature, [Normal], owner, stores));
        Assert.False(IsGrounded(creature, [Flying], owner, stores));

        Apply(stores, GroundedConditions.CreatureAirborne, owner);
        Assert.False(IsGrounded(creature, [Normal], owner, stores));

        creature = Creature("passive", [Flying], Inert(),
            abilityHooks: [GroundedHook(GroundedState.Airborne)],
            heldEffects: [GroundedEffect(GroundedState.Grounded)]);
        Assert.True(IsGrounded(creature, [Flying], owner, stores));

        Apply(stores, GroundedConditions.FieldGrounded,
            new BattleConditionOwner(BattleConditionScope.Field));
        Assert.True(IsGrounded(Creature("field_subject", [Flying], Inert(),
                abilityHooks: [GroundedHook(GroundedState.Airborne)]), [Flying], owner, stores));

        var battle = new BattleController(Creature("overlay_source", [Flying], Inert()),
            Creature("overlay_target", [Normal], Inert("overlay_inert")), Chart(), new Rng(1));
        battle.Overlays.Apply(new BattleOverlayApplication(
            new BattleOverlayOwner(BattleSide.Player, 0, PlayerSlot), new(),
            BattleOverlayLayer.FormOrSnapshot, new CreatureTypesOverlay([Normal]), 0, 0));
        Assert.True(battle.IsGrounded(PlayerSlot));
    }

    [Fact]
    public void Resolver_ReplacesExpiresAndCleansCreatureGroundedStateWithoutRng()
    {
        BattleMove airborne = StateMove("airborne", GroundedState.Airborne, duration: 3);
        BattleMove grounded = StateMove("grounded", GroundedState.Grounded, duration: 3);
        BattleCreature player = Creature("player", [Normal], airborne, grounded, Inert("player_inert"));
        BattleCreature enemy = Creature("enemy", [Normal], Inert("enemy_inert"));
        BattleCreature reserve = Creature("reserve", [Normal], Inert("reserve_inert"));
        var rng = new CountingRng();
        var battle = new BattleController([player], [enemy, reserve], Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        BattleConditionInstance applied = Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(GroundedConditions.CreatureAirborne.Id, applied.Definition.Id);
        Assert.Equal(2, applied.RemainingDuration);
        Assert.False(battle.IsGrounded(EnemySlot));

        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        BattleConditionInstance replaced = Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(GroundedConditions.CreatureGrounded.Id, replaced.Definition.Id);
        Assert.Equal(2, replaced.RemainingDuration);
        Assert.True(battle.IsGrounded(EnemySlot));
        Assert.Contains(battle.ConditionTrace, entry => entry.Kind == BattleConditionTraceKind.Replaced);

        battle.ResolveTurn(new UseMove(2), new Switch(1));
        Assert.Empty(battle.ConditionSnapshot);
        Assert.Contains(battle.ConditionTrace, entry => entry is
            { Kind: BattleConditionTraceKind.Removed, CleanupReason: BattleConditionCleanupReason.Switch });
        Assert.Equal(2, rng.Calls); // two ordinary same-speed turn-order ties; groundedState adds no draw

        var direct = Stores();
        Apply(direct, GroundedConditions.CreatureAirborne, CreatureOwner(BattleSide.Enemy, 0, EnemySlot));
        BattleConditionChangeSet fainted = direct.OwnerFainted(BattleSide.Enemy, 0, 1, 1);
        Assert.Empty(direct.Snapshot());
        Assert.Contains(fainted.Trace, entry => entry.CleanupReason == BattleConditionCleanupReason.Faint);

        direct.Apply(new BattleConditionApplication(GroundedConditions.CreatureAirborne.Id,
            CreatureOwner(BattleSide.Enemy, 0, EnemySlot), new BattleConditionSource(), 1, 1, Duration: 1));
        BattleConditionChangeSet expired = direct.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 1, 2);
        Assert.Empty(direct.Snapshot());
        Assert.Contains(expired.Events, entry => entry is ConditionExpired);
    }

    [Fact]
    public void FieldGroundingOverridesAirborneAbilityForResolverAndSmartAiTerrainQueries()
    {
        BattleMove electric = Hit(Electric, 40, "electric");
        BattleMove normal = Hit(Normal, 50, "normal");
        BattleCreature attacker = Creature("attacker", [Flying], electric, normal,
            abilityHooks: [GroundedHook(GroundedState.Airborne)]);
        BattleCreature target = Creature("target", [Normal], Inert());
        var conditions = StoresWithTerrain();

        SmartAiDecision airborne = SmartAi.ChooseAction(Context(attacker, target, conditions.Snapshot()));
        Assert.Equal(new UseMove(1), airborne.Action);

        Apply(conditions, GroundedConditions.FieldGrounded,
            new BattleConditionOwner(BattleConditionScope.Field));
        SmartAiDecision grounded = SmartAi.ChooseAction(Context(attacker, target, conditions.Snapshot()));
        Assert.Equal(new UseMove(0), grounded.Action);

        BattleMove field = new(EntityId.Parse("move:field_ground"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.EntireField,
            secondaryEffects: [new GroundedStateEffect(GroundedState.Grounded, GroundedStateScope.Field, 2)]);
        var rng = new CountingRng();
        var battle = new BattleController(
            Creature("resolver_source", [Normal], field),
            Creature("resolver_target", [Flying], Inert(),
                abilityHooks: [GroundedHook(GroundedState.Airborne)]),
            Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(battle.IsGrounded(EnemySlot));
        Assert.Equal(GroundedConditions.FieldGrounded.Id, Assert.Single(battle.ConditionSnapshot).Definition.Id);
        Assert.Equal(1, rng.Calls); // ordinary same-speed turn-order tie only
    }

    [Fact]
    public void TypeMutationFeedsResolverAndSmartAiGroundingThroughSharedOverlays()
    {
        BattleMove becomeGrounded = new(EntityId.Parse("move:become_grounded"), Normal,
            DamageClass.Status, null, null, 10, 0, 0,
            secondaryEffects: [new TypeMutationEffect(BattleTypeOperation.Replace,
                BattleTypeSubject.User, BattleTypeSource.Fixed, Normal)]);
        BattleMove electric = Hit(Electric, 40, "overlay_electric");
        BattleMove normal = Hit(Normal, 30, "overlay_normal");
        BattleCreature attacker = Creature("overlay_attacker", [Flying], becomeGrounded, electric, normal);
        BattleCreature target = Creature("overlay_target", [Normal], Inert("overlay_target_wait"));
        var battle = new BattleController(target, attacker, Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference,
                InitialTerrain: Terrain.Electric));

        battle.ResolveTurn(new Pass(), new UseMove(0));
        Assert.True(battle.IsGrounded(EnemySlot));

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [target], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Overlays: battle.Overlays,
            Conditions: battle.ConditionSnapshot, Ruleset: BattleRulesets.ModernReference));

        Assert.Equal(new UseMove(1), decision.Action);
        Assert.Equal([Normal], PhysicalMetricFormulas.EffectiveValues(attacker, battle.Overlays,
            new BattleOverlayOwner(BattleSide.Enemy, 0, EnemySlot)).CreatureTypes);
        Assert.Equal([Flying], attacker.Types);
    }

    [Fact]
    public void TargetGroundedState_MaterializesEveryDoublesTargetWithStableCreatureIdentity()
    {
        BattleMove spread = new(EntityId.Parse("move:spread_airborne"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.AllOpponents,
            secondaryEffects: [new GroundedStateEffect(GroundedState.Airborne, GroundedStateScope.Target, 3)]);
        var battle = new BattleController(
            [Creature("p0", [Normal], spread), Creature("p1", [Normal], Inert("p1_inert"))],
            [Creature("e0", [Normal], Inert("e0_inert")), Creature("e1", [Normal], Inert("e1_inert"))],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(1));

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(PlayerSlot, new UseMove(0)),
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(EnemySlot, new Pass()),
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Equal([0, 1], battle.ConditionSnapshot.Select(condition => condition.Owner.PartyIndex).ToArray());
        Assert.All(battle.ConditionSnapshot, condition =>
        {
            Assert.Equal(BattleSide.Enemy, condition.Owner.Side);
            Assert.Equal(GroundedConditions.CreatureAirborne.Id, condition.Definition.Id);
            Assert.Equal(2, condition.RemainingDuration);
        });
        Assert.False(battle.IsGrounded(EnemySlot));
        Assert.False(battle.IsGrounded(new BattleSlot(BattleSide.Enemy, 1)));
    }

    private static SmartAiContext Context(BattleCreature attacker, BattleCreature target,
        IReadOnlyList<BattleConditionInstance> conditions) => new([attacker], 0, [target], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Conditions: conditions,
            Ruleset: BattleRulesets.ModernReference);

    private static BattleConditionStores Stores() => new(new BattleConditionRegistry(GroundedConditions.Definitions));

    private static BattleConditionStores StoresWithTerrain()
    {
        var stores = new BattleConditionStores(new BattleConditionRegistry(
            [.. TerrainConditions.Definitions, .. GroundedConditions.Definitions]));
        stores.Apply(new BattleConditionApplication(TerrainConditions.For(Terrain.Electric).Definition!.Id,
            TerrainConditions.FieldOwner, new BattleConditionSource(), 0, 0));
        return stores;
    }

    private static void Apply(BattleConditionStores stores, BattleConditionDefinition definition,
        BattleConditionOwner owner) => stores.Apply(new BattleConditionApplication(
            definition.Id, owner, new BattleConditionSource(), 0, 0));

    private static bool IsGrounded(BattleCreature creature, IReadOnlyList<EntityId> types,
        BattleConditionOwner owner, BattleConditionStores stores) =>
        GroundedConditions.Query(creature, types, owner, stores.Snapshot()).FinalValue.ToInt32() == 1;

    private static BattleConditionOwner CreatureOwner(BattleSide side, int partyIndex, BattleSlot slot) =>
        new(BattleConditionScope.Creature, side, slot, partyIndex);

    private static AbilityHook GroundedHook(GroundedState state) => new()
    {
        Hook = AbilityHookPoint.OnGroundedQuery,
        Effects = [GroundedEffect(state)],
    };

    private static Effect GroundedEffect(GroundedState state) => new()
    {
        Op = "groundedModify",
        Params = new Dictionary<string, JsonElement>
        {
            ["state"] = JsonSerializer.SerializeToElement(state.ToString().ToLowerInvariant()),
        },
    };

    private static BattleMove StateMove(string slug, GroundedState state, int duration) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 10, 0, 0,
            secondaryEffects: [new GroundedStateEffect(state, GroundedStateScope.Target, duration)]);

    private static BattleMove Inert(string slug = "inert") =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 30, 0, 0);

    private static BattleMove Hit(EntityId type, int power, string slug) =>
        new(EntityId.Parse($"move:{slug}"), type, DamageClass.Special, power, null, 30, 0, 0);

    private static BattleCreature Creature(string slug, IReadOnlyList<EntityId> types, BattleMove move,
        IReadOnlyList<AbilityHook>? abilityHooks = null, IReadOnlyList<Effect>? heldEffects = null) =>
        Creature(slug, types, [move], abilityHooks, heldEffects);

    private static BattleCreature Creature(string slug, IReadOnlyList<EntityId> types,
        BattleMove move1, BattleMove move2, BattleMove move3,
        IReadOnlyList<AbilityHook>? abilityHooks = null, IReadOnlyList<Effect>? heldEffects = null) =>
        Creature(slug, types, [move1, move2, move3], abilityHooks, heldEffects);

    private static BattleCreature Creature(string slug, IReadOnlyList<EntityId> types,
        BattleMove move1, BattleMove move2,
        IReadOnlyList<AbilityHook>? abilityHooks = null, IReadOnlyList<Effect>? heldEffects = null) =>
        Creature(slug, types, [move1, move2], abilityHooks, heldEffects);

    private static BattleCreature Creature(string slug, IReadOnlyList<EntityId> types,
        IReadOnlyList<BattleMove> moves, IReadOnlyList<AbilityHook>? abilityHooks,
        IReadOnlyList<Effect>? heldEffects) => new(EntityId.Parse($"species:{slug}"), slug, 50, types,
            new Stats(320, 100, 100, 100, 100, 100), moves,
            abilityHooks: abilityHooks, heldItemBattleEffects: heldEffects);

    private static TypeChart Chart() => new([
        new TypeDef { Id = Normal }, new TypeDef { Id = Electric }, new TypeDef { Id = Flying },
    ]);

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public double NextDouble() { Calls++; return 0; }
    }
}
