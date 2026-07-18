using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleAbilityMutationTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId First = EntityId.Parse("ability:first");
    private static readonly EntityId Second = EntityId.Parse("ability:second");
    private static readonly EntityId Third = EntityId.Parse("ability:third");
    private static readonly BattleOverlayOwner User = new(BattleSide.Player, 0, new(BattleSide.Player, 0));
    private static readonly BattleOverlayOwner Ally = new(BattleSide.Player, 1, new(BattleSide.Player, 1));
    private static readonly BattleOverlayOwner Target = new(BattleSide.Enemy, 0, new(BattleSide.Enemy, 0));

    [Fact]
    public void CompilerLocksClosedShapeAndTargetCompatibility()
    {
        AbilityMutationEffect copy = Assert.IsType<AbilityMutationEffect>(Compile("copy",
            Op("abilityMutation", ("operation", "copy"))).SecondaryEffects.Single());
        Assert.Equal(BattleAbilitySubject.Target, copy.Source);
        Assert.Equal(BattleAbilitySubject.User, copy.Subject);

        Assert.Throws<ArgumentException>(() => Compile("chance",
            Op("abilityMutation", 50, ("operation", "copy"))));
        Assert.Throws<ArgumentException>(() => Compile("missing_replacement",
            Op("abilityMutation", ("operation", "replace"))));
        Assert.Throws<ArgumentException>(() => Compile("extra_replacement",
            Op("abilityMutation", ("operation", "copy"), ("ability", Third.ToString()))));
        Assert.Throws<ArgumentException>(() => Compile("bad_swap_shape",
            Op("abilityMutation", ("operation", "swap"), ("subject", "target"))));
        Assert.Throws<ArgumentException>(() => Compile("same_copy_subject",
            Op("abilityMutation", ("operation", "copy"), ("source", "user"), ("subject", "user"))));
        Assert.Throws<ArgumentException>(() => Compile("bad_target",
            Op("abilityMutation", ("operation", "copy")), MoveTarget.User));
    }

    [Fact]
    public void CopySwapReplaceAndProtectionPreflightAtomically()
    {
        Ability guarded = Ability(Second, Guard("copy,swap,replace,suppress"));
        var overlays = new BattleOverlayStore();
        var state = State(overlays, Ability(First), guarded, Ability(Third));

        Assert.Equal(BattleAbilityMutationFailure.Protected, Mutate(state, BattleAbilityOperation.Copy,
            BattleAbilitySubject.User, BattleAbilitySubject.Target, null, First, Second).Failure);
        Assert.Empty(overlays.Snapshot());

        state = State(overlays = new BattleOverlayStore(), Ability(First), Ability(Second), Ability(Third));
        Assert.True(Mutate(state, BattleAbilityOperation.Copy, BattleAbilitySubject.User,
            BattleAbilitySubject.Target, null, First, Second).Succeeded);
        Assert.Equal(Second, state.Effective(User, Values(First)));

        overlays = new BattleOverlayStore();
        state = State(overlays, Ability(First), Ability(Second), Ability(Third));
        BattleAbilityMutationResult swap = Mutate(state, BattleAbilityOperation.Swap,
            BattleAbilitySubject.User, BattleAbilitySubject.Target, null, First, Second);
        Assert.True(swap.Succeeded);
        Assert.Equal(Second, state.Effective(User, Values(First)));
        Assert.Equal(First, state.Effective(Target, Values(Second)));

        overlays = new BattleOverlayStore();
        state = State(overlays, Ability(First), Ability(Second), Ability(Third));
        Assert.True(Mutate(state, BattleAbilityOperation.Replace, BattleAbilitySubject.Target,
            BattleAbilitySubject.Target, Third, First, Second).Succeeded);
        Assert.Equal(Third, state.Effective(Target, Values(Second)));
    }

    [Theory]
    [InlineData(null, "ability:second", BattleAbilityMutationFailure.MissingAbility)]
    [InlineData("ability:missing", "ability:second", BattleAbilityMutationFailure.UnknownAbility)]
    [InlineData("ability:first", "ability:first", BattleAbilityMutationFailure.SameAbility)]
    public void MissingUnknownAndIdenticalAbilitiesFailWithoutWrites(string? userText, string targetText,
        BattleAbilityMutationFailure expected)
    {
        var overlays = new BattleOverlayStore();
        BattleAbilityState state = State(overlays, Ability(First), Ability(Second));
        EntityId? user = userText is null ? null : EntityId.Parse(userText);

        BattleAbilityMutationResult result = Mutate(state, BattleAbilityOperation.Copy,
            BattleAbilitySubject.User, BattleAbilitySubject.Target, null, user, EntityId.Parse(targetText));

        Assert.Equal(expected, result.Failure);
        Assert.Empty(overlays.Snapshot());
    }

    [Fact]
    public void SuppressionCanBeIgnoredForOneQueryWithoutChangingOrdinaryHooks()
    {
        var overlays = new BattleOverlayStore();
        BattleAbilityState state = State(overlays, Ability(First, Hook("residualHeal")), Ability(Second));
        BattleAbilityMutationResult result = Mutate(state, BattleAbilityOperation.Suppress,
            BattleAbilitySubject.User, BattleAbilitySubject.Target, null, First, Second);

        long sequence = Assert.Single(result.SuppressionSequences);
        Assert.Null(state.Effective(User, Values(First)));
        Assert.Empty(state.Hooks(User, Values(First), []));
        Assert.Equal(First, state.Effective(User, Values(First), [sequence]));
        Assert.Single(state.Hooks(User, Values(First), [], [sequence]));
        Assert.Equal(BattleOverlayTraceKind.SuppressionIgnored,
            overlays.Resolve(User, Values(First), [sequence]).Trace.Last().Kind);
    }

    [Fact]
    public void UserAndAlliesCopyIsAtomicAndTracksCreatureIdentity()
    {
        var overlays = new BattleOverlayStore();
        BattleAbilityState state = State(overlays, Ability(First), Ability(Second), Ability(Third));
        BattleAbilityMutationResult copied = state.Mutate(BattleAbilityOperation.Copy,
            BattleAbilitySubject.UserAndAllies, BattleAbilitySubject.Target, null,
            User, Values(Third), false, Target, Values(Third), false, [(Ally, Values(Second), false)], 1, 1);

        Assert.True(copied.Succeeded);
        Assert.Single(copied.Changes);
        Assert.Equal(Third, state.Effective(User, Values(Third)));
        Assert.Equal(Third, state.Effective(Ally, Values(Second)));
        overlays.OwnerSwitched(BattleSide.Player, 1, null, 1, 2);
        Assert.Equal(Second, state.Effective(Ally with { Slot = null }, Values(Second)));
        Assert.Equal(Third, state.Effective(User, Values(Third)));
    }

    [Fact]
    public void FaintAndBattleEndCleanupRestoreImmutableBaseDefinitions()
    {
        Ability first = Ability(First);
        Ability second = Ability(Second);
        var overlays = new BattleOverlayStore();
        BattleAbilityState state = State(overlays, first, second);
        Assert.True(Mutate(state, BattleAbilityOperation.Swap, BattleAbilitySubject.User,
            BattleAbilitySubject.Target, null, First, Second).Succeeded);

        overlays.OwnerFainted(BattleSide.Player, 0, 1, 2);
        Assert.Equal(First, state.Effective(User, Values(First)));
        Assert.Equal(First, first.Id);
        Assert.Equal(First, state.Effective(Target, Values(Second)));
        overlays.EndBattle(1, 3);
        Assert.Equal(Second, state.Effective(Target, Values(Second)));
    }

    [Fact]
    public void ResolverEmitsOrderedEventsTraceAndRefreshesHooksAtNextCheckpoint()
    {
        BattleMove copy = Compile("copy", Op("abilityMutation", ("operation", "copy")));
        Ability first = Ability(First);
        Ability second = Ability(Second, Hook("protectionBypass"));
        var battle = new BattleController(Creature("user", copy, First), Creature("target", Inert(), Second),
            Chart(), new CountingRng(), abilityData: [first, second]);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, e => e is AbilityMutated { Side: BattleSide.Player,
            Operation: BattleAbilityOperation.Copy, Before: var before, After: var after }
            && before == First && after == Second);
        EffectTraceEntry trace = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.AbilityMutation);
        Assert.True(trace.Performed);
        Assert.True(trace.EventEndIndex > trace.EventStartIndex);
        Assert.Contains(battle.Abilities.Hooks(User, Values(First), []), hook =>
            hook.Effects.Any(effect => effect.Op == "protectionBypass"));
        Assert.DoesNotContain(first.Hooks.SelectMany(hook => hook.Effects), effect => effect.Op == "protectionBypass");
    }

    [Fact]
    public void DispatcherSnapshotKeepsOldHooksAndNextCheckpointUsesNewHooks()
    {
        var overlays = new BattleOverlayStore();
        BattleAbilityState state = State(overlays, Ability(First, Hook("oldHook")), Ability(Second, Hook("newHook")));
        IEnumerable<BattleHookSource> Sources() => state.Hooks(User, Values(First), [])
            .Select(hook => new BattleHookSource(User.Slot!.Value, BattleHookSourceKind.Ability,
                hook.Hook, hook.Effects));

        BattleHookInvocation[] captured = BattleHookDispatcher.Damage(BattleSide.Player, Sources()).ToArray();
        Assert.True(Mutate(state, BattleAbilityOperation.Copy, BattleAbilitySubject.User,
            BattleAbilitySubject.Target, null, First, Second).Succeeded);

        Assert.Equal("oldHook", Assert.Single(captured).Effect.Op);
        Assert.Equal("newHook", Assert.Single(BattleHookDispatcher.Damage(BattleSide.Player, Sources())).Effect.Op);
    }

    [Fact]
    public void GroundedQueryUsesTheEffectiveAbilityCatalog()
    {
        BattleMove copy = Compile("grounded_copy", Op("abilityMutation", ("operation", "copy")));
        Ability airborne = Ability(First,
            HookAt(AbilityHookPoint.OnGroundedQuery, "groundedModify", ("state", "airborne")));
        var battle = new BattleController(Creature("user", copy, First), Creature("target", Inert(), Second),
            Chart(), new CountingRng(), abilityData: [airborne, Ability(Second)]);

        Assert.False(battle.IsGrounded(new BattleSlot(BattleSide.Player, 0)));
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.True(battle.IsGrounded(new BattleSlot(BattleSide.Player, 0)));
    }

    [Fact]
    public void SmartAiUsesEffectiveAbilityHooksAndNamesMutationScore()
    {
        BattleMove mutate = Compile("copy", Op("abilityMutation", ("operation", "copy")));
        BattleCreature attacker = Creature("attacker", mutate, First);
        BattleCreature defender = Creature("defender", Inert(), Second);
        var overlays = new BattleOverlayStore();
        var state = State(overlays, Ability(First), Ability(Second, Hook("sideConditionBypass", ("tag", "screen"))));
        Assert.True(Mutate(state, BattleAbilityOperation.Copy, BattleAbilitySubject.User,
            BattleAbilitySubject.Target, null, First, Second).Succeeded);

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext([attacker], 0, [defender], 0,
            Chart(), new CountingRng(), Weights: new SmartAiWeights { NoiseFraction = 0 }, Overlays: overlays,
            AbilityState: state));

        Assert.Contains(decision.Scores.Single().Components,
            component => component is { Name: "abilityMutation", Value: 0 });
    }

    [Fact]
    public void MutationEventAndTraceReplayIsStable()
    {
        BattleMove swap = Compile("ability_swap_replay", Op("abilityMutation", ("operation", "swap")));
        var battle = new BattleController(Creature("user", swap, First), Creature("target", Inert(), Second),
            Chart(), new CountingRng(), abilityData: [Ability(First), Ability(Second)]);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());
        string replay = string.Join('\n', events.Select(EventRow)
            .Concat(battle.Trace.Where(trace => trace.Kind == EffectTraceKind.AbilityMutation)
                .Select(trace => $"trace:{trace.Turn}:{trace.ActionSequence}:{trace.SourceSlot.Side}:"
                    + $"{trace.TargetSlot?.Side}:{trace.Performed}:{trace.Value}:"
                    + $"{trace.EventStartIndex}-{trace.EventEndIndex}")));

        Assert.Equal(Golden("ability-mutation"), replay);
    }

    private static BattleAbilityMutationResult Mutate(BattleAbilityState state, BattleAbilityOperation operation,
        BattleAbilitySubject subject, BattleAbilitySubject source, EntityId? replacement,
        EntityId? userAbility, EntityId? targetAbility) => state.Mutate(operation, subject, source, replacement,
            User, Values(userAbility), false, Target, Values(targetAbility), false, [], 1, 1);

    private static BattleAbilityState State(BattleOverlayStore overlays, params Ability[] abilities) =>
        new(overlays, abilities.ToDictionary(ability => ability.Id));

    private static BattleEffectiveValues Values(EntityId? ability) => new(null, ability, [Normal],
        new Stats(100, 50, 50, 50, 50, 50), []);

    private static Ability Ability(EntityId id, params AbilityHook[] hooks) =>
        new() { Id = id, Name = id.Slug, Hooks = hooks };

    private static AbilityHook Hook(string op, params (string Key, object Value)[] values) =>
        HookAt(AbilityHookPoint.OnModifyOutgoingDamage, op, values);

    private static AbilityHook HookAt(AbilityHookPoint point, string op,
        params (string Key, object Value)[] values) => new()
    {
        Hook = point,
        Effects = [Op(op, values)],
    };

    private static AbilityHook Guard(string operations) => Hook("abilityMutationGuard", ("operations", operations));

    private static BattleMove Compile(string slug, Effect effect, MoveTarget target = MoveTarget.Selected) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal,
            DamageClass = DamageClass.Status, Pp = 10, Target = target, Effects = [effect],
        });

    private static BattleCreature Creature(string slug, BattleMove move, EntityId ability) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal], new Stats(500, 100, 100, 100, 100, 50),
        [move], ability: ability);

    private static BattleMove Inert() => new(EntityId.Parse("move:inert"), Normal,
        DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static string EventRow(BattleEvent battleEvent) => battleEvent switch
    {
        MoveUsed used => $"used:{used.Slot.Side}:{used.Move}",
        AbilityMutated changed => $"ability:{changed.Side}:{changed.PartyIndex}:{changed.Before}:"
            + $"{changed.After}:{changed.Operation}",
        _ => $"event:{battleEvent.GetType().Name}",
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static Effect Op(string op, params (string Key, object Value)[] values) => Op(op, null, values);

    private static Effect Op(string op, int? chance, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Chance = chance,
        Params = values.Length == 0 ? null : values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private sealed class CountingRng : IRng
    {
        public int Next(int maxExclusive) => 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0;
    }
}
