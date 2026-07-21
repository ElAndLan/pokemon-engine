using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTransformTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Flame = EntityId.Parse("type:flame");
    private static readonly EntityId SourceAbility = EntityId.Parse("ability:source");
    private static readonly EntityId TargetAbility = EntityId.Parse("ability:target");
    private static readonly EntityId SourceItem = EntityId.Parse("item:source");
    private static readonly BattleSlot PlayerSlot = new(BattleSide.Player, 0);
    private static readonly BattleSlot EnemySlot = new(BattleSide.Enemy, 0);

    [Fact]
    public void CompilerAdmitsOnlyTheClosedTransformShape()
    {
        Assert.IsType<TransformEffect>(Assert.Single(Transform().SecondaryEffects));
        Assert.Throws<ArgumentException>(() => Compile("chance", DamageClass.Status, null,
            MoveTarget.Selected, Op("transform", 50)));
        Assert.Throws<ArgumentException>(() => Compile("params", DamageClass.Status, null,
            MoveTarget.Selected, Op("transform", ("mode", "all"))));
        Assert.Throws<ArgumentException>(() => Compile("damage", DamageClass.Physical, 40,
            MoveTarget.Selected, Op("damage"), Op("transform")));
        Assert.Throws<ArgumentException>(() => Compile("user", DamageClass.Status, null,
            MoveTarget.User, Op("transform")));
        Assert.Throws<ArgumentException>(() => Compile("duplicate", DamageClass.Status, null,
            MoveTarget.Selected, Op("transform"), Op("transform")));
    }

    [Fact]
    public void SnapshotCopiesOnlyWhitelistedEffectiveFieldsFromOnePreState()
    {
        BattleMove copied = Fixed("copied", 12, pp: 18);
        BattleCreature source = Creature("source", [Normal], new Stats(120, 30, 31, 32, 33, 34),
            80, 9, [Transform()], SourceAbility, SourceItem, "source_form", [Op("choiceLock")]);
        source.TakeDamage(47);
        source.SetStatus(PersistentStatus.Paralysis);
        source.SetStage(StatKind.Atk, -2);
        BattleCreature target = Creature("target", [Flame], new Stats(200, 90, 91, 92, 93, 94),
            345, 27, [copied], TargetAbility, null, "target_form");
        target.SetStage(StatKind.Atk, 3);
        target.SetStage(StatKind.Evasion, -1);
        var battle = Battle(source, target);
        battle.Overlays.ApplyMany([
            Overlay(EnemySlot, new StatsOverlay(target.Stats with { Spa = 150 }), 0),
            Overlay(EnemySlot, new MetricValueOverlay(BattleMetric.Weight, 777), 1,
                BattleOverlayLayer.Additive),
            Overlay(EnemySlot, new MoveTypeRuleOverlay("capture_rule", new BattleMoveTypeRule(Flame)), 2,
                BattleOverlayLayer.Additive),
        ]);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());
        BattleEffectiveValues values = Values(battle, PlayerSlot);

        Assert.Equal(73, source.CurrentHp);
        Assert.Equal(120, source.MaxHp);
        Assert.Equal(PersistentStatus.Paralysis, source.Status);
        Assert.Equal(SourceItem, values.HeldItem);
        Assert.Null(source.ChoiceLockedMoveIndex);
        Assert.Equal(TargetAbility, values.Ability);
        Assert.Equal([Flame], values.CreatureTypes);
        Assert.Equal(new Stats(120, 90, 91, 150, 93, 94), values.Stats);
        Assert.Equal("target_form", values.FormId);
        Assert.Equal(777, values.Metrics[BattleMetric.Weight]);
        Assert.Equal(9, values.Metrics[BattleMetric.Height]);
        Assert.Equal(3, source.Stage(StatKind.Atk));
        Assert.Equal(-1, source.Stage(StatKind.Evasion));
        Assert.Equal(copied.Move, Assert.Single(values.Moves).Definition.Move);
        Assert.Equal(Flame, values.Moves[0].Type);
        BattleMove capturedMove = values.Moves[0].Definition;
        Assert.Contains(events, e => e == new Transformed(PlayerSlot, EnemySlot));
        Assert.Contains(battle.Trace, trace => trace.Kind == EffectTraceKind.Transform && trace.Performed);

        target.SetStage(StatKind.Atk, -4);
        copied.UsePp();
        battle.Overlays.Apply(Overlay(EnemySlot, new CreatureTypesOverlay([Normal]), 3));
        Assert.Equal([Flame], Values(battle, PlayerSlot).CreatureTypes);
        Assert.Equal(3, source.Stage(StatKind.Atk));
        Assert.Equal([Normal], source.Types);
        Assert.Equal(SourceAbility, source.Ability);
        Assert.Equal(17, copied.Pp);
        Assert.Equal(5, capturedMove.Pp);
    }

    [Fact]
    public void CopiedMovesOwnIndependentFivePpPoolsAndDriveActionLegality()
    {
        BattleMove copied = Fixed("copied_pp", 8, pp: 20);
        copied.UsePp();
        BattleMove transform = Transform();
        BattleCreature source = Creature("pp_source", [Normal], Stats(), 100, 10, [transform]);
        BattleCreature target = Creature("pp_target", [Normal], Stats(), 100, 1, [copied]);
        var battle = Battle(source, target);

        battle.ResolveTurn(new UseMove(0), new Pass());
        BattleMove runtimeCopy = Assert.Single(Values(battle, PlayerSlot).Moves).Definition;
        Assert.NotSame(copied, runtimeCopy);
        Assert.Equal((5, 5), (runtimeCopy.Pp, runtimeCopy.MaxPp));
        Assert.Equal([new UseMove(0)], battle.LegalMoveActions(PlayerSlot));

        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(3, runtimeCopy.Pp);
        Assert.Equal(19, copied.Pp);
        Assert.Equal(9, transform.Pp);
    }

    [Fact]
    public void EffectiveAbilityHooksAndSpeedQueriesSeeTheCapturedSnapshot()
    {
        Ability airborne = new()
        {
            Id = TargetAbility,
            Name = "target",
            Hooks = [new AbilityHook
            {
                Hook = AbilityHookPoint.OnGroundedQuery,
                Effects = [Op("groundedModify", ("state", "airborne"))],
            }],
        };
        BattleCreature source = Creature("hook_source", [Normal], Stats(speed: 120), 100, 10,
            [Transform()], SourceAbility);
        BattleCreature target = Creature("hook_target", [Normal], Stats(speed: 73), 100, 10,
            [Wait()], TargetAbility, abilityHooks: airborne.Hooks);
        var battle = new BattleController(source, target, Chart(), new Rng(2));
        Assert.True(battle.IsGrounded(PlayerSlot));
        Assert.False(battle.IsGrounded(EnemySlot));

        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.False(battle.IsGrounded(PlayerSlot));
        Assert.Equal(73, PhysicalMetricFormulas.SpeedQuery(source, battle.Overlays, Owner(PlayerSlot))
            .FinalValue.ToInt32());
    }

    [Fact]
    public void NestedAndDecoyFailuresAreAtomicAndDrawNoRng()
    {
        var rng = new CountingRng();
        BattleMove transform = Transform();
        BattleCreature source = Creature("failure_source", [Normal], Stats(speed: 100), 100, 10, [transform]);
        BattleCreature target = Creature("failure_target", [Normal], Stats(speed: 1), 100, 10,
            [transform, Wait()]);
        var battle = Battle(source, target, rng);

        battle.ResolveTurn(new UseMove(0), new Pass());
        int draws = rng.Calls;
        IReadOnlyList<BattleEvent> nested = battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(nested, e => e == new TransformFailed(PlayerSlot, EnemySlot,
            TransformFailure.SourceTransformed));
        Assert.Equal(draws, rng.Calls);
        Assert.True(battle.Overlays.HasTransform(Owner(PlayerSlot)));

        BattleCreature blockedSource = Creature("blocked_source", [Normal], Stats(speed: 100), 100, 10, [transform]);
        BattleCreature blockedTarget = Creature("blocked_target", [Normal], Stats(speed: 1), 100, 10, [Wait()]);
        var blocked = Battle(blockedSource, blockedTarget);
        blocked.Overlays.Apply(Overlay(EnemySlot, new DecoyOverlay(new BattleDecoyState(10, 10)), 0));
        IReadOnlyList<BattleEvent> blockedEvents = blocked.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(blockedEvents, e => e == new TransformFailed(PlayerSlot, EnemySlot,
            TransformFailure.TargetBlocked));
        Assert.False(blocked.Overlays.HasTransform(Owner(PlayerSlot)));

        BattleCreature targetNestedSource = Creature("target_nested_source", [Normal], Stats(speed: 100),
            100, 10, [transform]);
        BattleCreature transformedTarget = Creature("transformed_target", [Normal], Stats(speed: 1),
            100, 10, [Wait()]);
        var targetNested = Battle(targetNestedSource, transformedTarget);
        targetNested.Overlays.Apply(Overlay(EnemySlot,
            Snapshot([Wait()], Stats(), [Normal], null, null, 100), 0));
        IReadOnlyList<BattleEvent> targetFailure = targetNested.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(targetFailure, e => e == new TransformFailed(PlayerSlot, EnemySlot,
            TransformFailure.TargetTransformed));
        Assert.False(targetNested.Overlays.HasTransform(Owner(PlayerSlot)));
    }

    [Fact]
    public void LaterOverlayWinsAndSwitchRestoresOriginalMoveAndPp()
    {
        BattleMove transform = Transform();
        BattleCreature source = Creature("switch_source", [Normal], Stats(), 100, 10, [transform]);
        BattleCreature reserve = Creature("reserve", [Normal], Stats(), 100, 5, [Wait()]);
        BattleCreature target = Creature("switch_target", [Flame], Stats(atk: 150), 100, 1, [Fixed("copy", 5)]);
        var battle = new BattleController([source, reserve], [target], Chart(), new Rng(3));
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.Overlays.Apply(Overlay(PlayerSlot, new CreatureTypesOverlay([Normal]), 99));
        Assert.Equal([Normal], Values(battle, PlayerSlot).CreatureTypes);

        battle.ResolveTurn(new Switch(1), new Pass());

        Assert.False(battle.Overlays.HasTransform(new BattleOverlayOwner(BattleSide.Player, 0)));
        BattleEffectiveValues original = battle.Overlays.Resolve(new BattleOverlayOwner(BattleSide.Player, 0),
            PhysicalMetricFormulas.BaseEffectiveValues(source)).Values;
        Assert.Equal(transform.Move, Assert.Single(original.Moves).Definition.Move);
        Assert.Equal(9, transform.Pp);
        Assert.Equal(0, source.Stage(StatKind.Atk));
    }

    [Fact]
    public void FaintAndBattleEndCleanupRemoveTheSnapshot()
    {
        BattleMove lethal = Fixed("lethal", 500);
        BattleCreature source = Creature("faint_source", [Normal], Stats(speed: 100), 100, 10, [Transform()]);
        BattleCreature target = Creature("faint_target", [Normal], Stats(speed: 1), 100, 10, [lethal]);
        var battle = Battle(source, target);
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.True(battle.Overlays.HasTransform(Owner(PlayerSlot)));

        battle.ResolveTurn(new Pass(), new UseMove(0));

        Assert.True(source.IsFainted);
        Assert.NotNull(battle.Outcome);
        Assert.False(battle.Overlays.HasTransform(new BattleOverlayOwner(BattleSide.Player, 0)));
    }

    [Fact]
    public void LiveBattleEndAlsoRemovesTheSnapshotWithoutRestorationWrites()
    {
        BattleMove forceOut = Compile("force_out", DamageClass.Status, null,
            MoveTarget.Selected, Op("forceSwitch"));
        BattleCreature source = Creature("end_source", [Normal], Stats(speed: 100), 100, 10, [Transform()]);
        BattleCreature target = Creature("end_target", [Normal], Stats(speed: 1), 100, 10, [forceOut]);
        var battle = new BattleController(source, target, Chart(), new Rng(8), isWild: true);
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.True(battle.Overlays.HasTransform(Owner(PlayerSlot)));

        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.False(source.IsFainted);
        Assert.Equal(BattleSide.Player, battle.Outcome?.Winner);
        Assert.False(battle.Overlays.HasTransform(new BattleOverlayOwner(BattleSide.Player, 0)));
    }

    [Fact]
    public void SmartAiEnumeratesCopiedMovesAndRejectsVisibleNestedTransform()
    {
        BattleMove transform = Transform();
        BattleMove damage = Fixed("ai_damage", 40);
        BattleCreature attacker = Creature("ai_source", [Normal], Stats(), 100, 10, [transform]);
        BattleCreature defender = Creature("ai_target", [Normal], Stats(), 100, 1, [Wait()]);
        var overlays = new BattleOverlayStore();
        overlays.Apply(Overlay(EnemySlot, Snapshot([transform, damage], Stats(), [Normal], null, null, 100), 0));

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext([attacker], 0, [defender], 0,
            Chart(), new Rng(4), Overlays: overlays, Weights: new SmartAiWeights { NoiseFraction = 0 }));

        AiCandidateScore nested = Assert.Single(decision.Scores, score => score.Action == new UseMove(0));
        Assert.Contains(nested.Components, component => component.Name == "transformUnavailable"
            && component.Value == -1_000_000);
        Assert.Equal(new UseMove(1), decision.Action);

        var decoyOverlays = new BattleOverlayStore();
        decoyOverlays.Apply(Overlay(new(BattleSide.Player, 0),
            new DecoyOverlay(new BattleDecoyState(20, 20)), 0));
        BattleCreature cleanAttacker = Creature("ai_clean", [Normal], Stats(), 100, 10, [transform, damage]);
        SmartAiDecision decoyDecision = SmartAi.ChooseAction(new SmartAiContext([cleanAttacker], 0,
            [defender], 0, Chart(), new Rng(5), Overlays: decoyOverlays,
            Weights: new SmartAiWeights { NoiseFraction = 0 }));
        Assert.Contains(decoyDecision.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component.Name == "transformUnavailable" && component.Value == -1_000_000);
        Assert.Equal(new UseMove(1), decoyDecision.Action);
    }

    [Fact]
    public void DoublesCapturesOnlyTheExplicitSelectedTargetAndOwner()
    {
        BattleCreature source = Creature("double_source", [Normal], Stats(speed: 100), 100, 10, [Transform()]);
        BattleCreature ally = Creature("double_ally", [Normal], Stats(speed: 50), 100, 10, [Wait()]);
        BattleCreature target0 = Creature("double_target_0", [Normal], Stats(speed: 20), 100, 10,
            [Fixed("wrong_copy", 3)]);
        BattleCreature target1 = Creature("double_target_1", [Flame], Stats(atk: 170, speed: 10), 100, 10,
            [Fixed("selected_copy", 4)]);
        var battle = new BattleController([source, ally], [target0, target1], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new Rng(6));

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0),
                new ActiveSlotSelection(new(BattleSide.Enemy, 1))),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        BattleEffectiveValues captured = Values(battle, new(BattleSide.Player, 0));
        Assert.Equal(EntityId.Parse("move:selected_copy"), Assert.Single(captured.Moves).Definition.Move);
        Assert.Equal([Flame], captured.CreatureTypes);
        Assert.Equal(170, captured.Stats.Atk);
        Assert.False(battle.Overlays.HasTransform(new BattleOverlayOwner(BattleSide.Player, 1,
            new(BattleSide.Player, 1))));
    }

    [Fact]
    public void TransformSnapshotMatchesDeterministicGolden()
    {
        static string Run()
        {
            BattleCreature source = Creature("golden_source", [Normal], Stats(speed: 100), 100, 10, [Transform()]);
            BattleCreature target = Creature("golden_target", [Flame], Stats(atk: 140, speed: 1), 240, 16,
                [Fixed("golden_copy", 7)], TargetAbility, null, "golden_form");
            target.SetStage(StatKind.Atk, 2);
            BattleController battle = Battle(source, target);
            battle.ResolveTurn(new UseMove(0), new Pass());
            BattleEffectiveValues values = Values(battle, PlayerSlot);
            return string.Join('\n', battle.Log.Select(EventRow)
                .Concat(battle.Trace.Where(trace => trace.Kind == EffectTraceKind.Transform)
                    .Select(trace => $"trace:{trace.SourceSlot.Side}:{trace.TargetSlot?.Side}:"
                        + $"{trace.Performed}:{trace.Value}:{trace.EventStartIndex}-{trace.EventEndIndex}"))
                .Append($"state:{values.Stats.Atk}:{values.CreatureTypes[0]}:{values.Ability}:"
                    + $"{values.FormId}:{values.Metrics[BattleMetric.Weight]}:{source.Stage(StatKind.Atk)}:"
                    + $"{values.Moves[0].Definition.Move}:{values.Moves[0].Definition.Pp}"));
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("transform-snapshot"), first);
    }

    private static BattleController Battle(BattleCreature player, BattleCreature enemy, IRng? rng = null) =>
        new(player, enemy, Chart(), rng ?? new Rng(7));

    private static BattleEffectiveValues Values(BattleController battle, BattleSlot slot) =>
        PhysicalMetricFormulas.EffectiveValues(battle.Active(slot), battle.Overlays,
            new BattleOverlayOwner(slot.Side, battle.ActiveIndex(slot), slot));

    private static BattleOverlayOwner Owner(BattleSlot slot) => new(slot.Side, 0, slot);

    private static BattleOverlayApplication Overlay(BattleSlot owner, BattleOverlayPayload payload, int sequence,
        BattleOverlayLayer layer = BattleOverlayLayer.FormOrSnapshot) => new(Owner(owner), new(), layer,
        payload, 0, sequence, Cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint
            | BattleOverlayCleanup.BattleEnd);

    private static TransformOverlay Snapshot(IReadOnlyList<BattleMove> moves, Stats stats,
        IReadOnlyList<EntityId> types, EntityId? ability, string? form, int weight) => new(ability, types,
        stats, moves.Select((move, index) => BattleEffectiveMove.FromBase(move, index)).ToArray(), form, weight);

    private static BattleCreature Creature(string slug, IReadOnlyList<EntityId> types, Stats stats,
        int weight, int height, IReadOnlyList<BattleMove> moves, EntityId? ability = null,
        EntityId? item = null, string? form = null, IReadOnlyList<Effect>? heldEffects = null,
        IReadOnlyList<AbilityHook>? abilityHooks = null) => new(
        EntityId.Parse($"species:{slug}"), slug, 50,
        types, stats, moves, abilityHooks: abilityHooks, heldItemBattleEffects: heldEffects, heldItem: item,
        weightHectograms: weight, heightDecimeters: height,
        ability: ability, formId: form);

    private static Stats Stats(int atk = 100, int speed = 50) => new(100, atk, 100, 100, 100, speed);

    private static BattleMove Transform() => Compile("transform", DamageClass.Status, null,
        MoveTarget.Selected, Op("transform"));

    private static BattleMove Fixed(string slug, int amount, int pp = 20) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal, DamageClass = DamageClass.Physical,
        Accuracy = null, Pp = pp, Target = MoveTarget.Selected,
        Effects = [Op("fixedDamage", ("amount", amount))],
    });

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Normal,
        DamageClass.Status, null, null, 20, 0, 0);

    private static BattleMove Compile(string slug, DamageClass damageClass, int? power,
        MoveTarget target, params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal, DamageClass = damageClass,
        Power = power, Accuracy = null, Pp = 10, Target = target, Effects = effects,
    });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static Effect Op(string op, int chance, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Chance = chance,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }, new TypeDef { Id = Flame }]);

    private static string EventRow(BattleEvent battleEvent) => battleEvent switch
    {
        MoveUsed used => $"used:{used.Slot.Side}:{used.Move}",
        Transformed transformed => $"transformed:{transformed.Source.Side}:{transformed.Target.Side}",
        TransformFailed failed => $"failed:{failed.Source.Side}:{failed.Target.Side}:{failed.Reason}",
        _ => $"event:{battleEvent.GetType().Name}",
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public double NextDouble() { Calls++; return 0; }
    }
}
