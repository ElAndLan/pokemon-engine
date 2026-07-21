using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTemporaryMoveReplacementTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Flame = EntityId.Parse("type:flame");
    private static readonly BattleSlot PlayerSlot = new(BattleSide.Player, 0);
    private static readonly BattleSlot EnemySlot = new(BattleSide.Enemy, 0);

    [Fact]
    public void CompilerAdmitsOnlyTheClosedReplacementShapeAndTagsTheOp()
    {
        BattleMove replacement = Replacement();

        Assert.IsType<TemporaryMoveReplacementEffect>(Assert.Single(replacement.SecondaryEffects));
        Assert.Contains(TemporaryMoveReplacementEffect.ExclusionTag, replacement.Tags);
        Assert.Throws<ArgumentException>(() => Compile("chance", DamageClass.Status, null,
            MoveTarget.Selected, Op("temporaryMoveReplacement", 50)));
        Assert.Throws<ArgumentException>(() => Compile("params", DamageClass.Status, null,
            MoveTarget.Selected, Op("temporaryMoveReplacement", ("pp", 4))));
        Assert.Throws<ArgumentException>(() => Compile("damage", DamageClass.Physical, 40,
            MoveTarget.Selected, Op("damage"), Op("temporaryMoveReplacement")));
        Assert.Throws<ArgumentException>(() => Compile("user", DamageClass.Status, null,
            MoveTarget.User, Op("temporaryMoveReplacement")));
        Assert.Throws<ArgumentException>(() => Compile("duplicate", DamageClass.Status, null,
            MoveTarget.Selected, Op("temporaryMoveReplacement"), Op("temporaryMoveReplacement")));
    }

    [Fact]
    public void ReplacementOwnsIndependentFivePpAndSwitchRestoresTheAuthoredSlot()
    {
        BattleMove replacement = Replacement();
        BattleMove copied = Fixed("copied", 40, pp: 20, type: Flame);
        BattleCreature source = Creature("source", 100, replacement, Wait("source_wait"));
        BattleCreature reserve = Creature("reserve", 50, Wait("reserve_wait"));
        BattleCreature target = Creature("target", 10, copied);
        var battle = new BattleController([source, reserve], [target], Chart(), new CountingRng());

        battle.ResolveTurn(new Pass(), new UseMove(0));
        IReadOnlyList<BattleEvent> applied = battle.ResolveTurn(new UseMove(0), new Pass());
        BattleEffectiveValues values = Values(battle, PlayerSlot);
        BattleMove runtimeCopy = values.Moves[0].Definition;

        Assert.Contains(applied, item => item == new TemporaryMoveReplacementApplied(
            PlayerSlot, EnemySlot, 0, replacement.Move, copied.Move));
        Assert.NotSame(copied, runtimeCopy);
        Assert.Equal((5, 5), (runtimeCopy.Pp, runtimeCopy.MaxPp));
        Assert.Equal(19, copied.Pp);
        Assert.Equal(9, replacement.Pp);
        Assert.Equal(Flame, values.Moves[0].Type);
        Assert.Equal([new UseMove(0), new UseMove(1)], battle.LegalMoveActions(PlayerSlot));

        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(4, runtimeCopy.Pp);
        Assert.Equal(19, copied.Pp);

        battle.ResolveTurn(new Switch(1), new Pass());
        battle.ResolveTurn(new Switch(0), new Pass());
        BattleEffectiveValues restored = Values(battle, PlayerSlot);
        Assert.Same(replacement, restored.Moves[0].Definition);
        Assert.Equal(9, replacement.Pp);
        Assert.DoesNotContain(battle.Overlays.Snapshot(), entry => entry.Payload is MoveSlotOverlay);
    }

    [Fact]
    public void MissingFailedKnownExcludedAndDecoyCandidatesFailWithoutAnOverlayOrRng()
    {
        static TemporaryMoveReplacementFailure Failure(BattleController battle, BattleAction player,
            BattleAction enemy)
        {
            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(player, enemy);
            return Assert.Single(events.OfType<TemporaryMoveReplacementFailed>()).Reason;
        }

        var noMoveRng = new CountingRng();
        var noMove = Battle(Creature("no_move_source", 100, Replacement()),
            Creature("no_move_target", 1, Wait("idle")), noMoveRng);
        Assert.Equal(TemporaryMoveReplacementFailure.NoTargetMove,
            Failure(noMove, new UseMove(0), new Pass()));
        Assert.Empty(noMove.Overlays.Snapshot());
        Assert.Equal(0, noMoveRng.Calls);

        BattleMove prior = Wait("prior");
        BattleCreature failedSource = Creature("failed_source", 10, Replacement());
        BattleCreature failedTarget = Creature("failed_target", 100, prior);
        var failed = Battle(failedSource, failedTarget);
        failed.ResolveTurn(new Pass(), new UseMove(0));
        failedTarget.SetStatus(PersistentStatus.Sleep, 1);
        Assert.Equal(TemporaryMoveReplacementFailure.TargetMoveFailed,
            Failure(failed, new UseMove(0), new UseMove(0)));
        Assert.DoesNotContain(failed.Overlays.Snapshot(), entry => entry.Payload is MoveSlotOverlay);

        BattleMove duplicate = Fixed("duplicate_known", 10);
        BattleCreature duplicateSource = Creature("duplicate_source", 100, Replacement(), duplicate);
        BattleCreature duplicateTarget = Creature("duplicate_target", 1, duplicate.WithPpPool(20, 20));
        var duplicateBattle = Battle(duplicateSource, duplicateTarget);
        duplicateBattle.ResolveTurn(new Pass(), new UseMove(0));
        Assert.Equal(TemporaryMoveReplacementFailure.AlreadyKnown,
            Failure(duplicateBattle, new UseMove(0), new Pass()));

        BattleMove excluded = TaggedFixed("excluded", TemporaryMoveReplacementEffect.ExclusionTag);
        var excludedBattle = Battle(Creature("excluded_source", 100, Replacement()),
            Creature("excluded_target", 1, excluded));
        excludedBattle.ResolveTurn(new Pass(), new UseMove(0));
        Assert.Equal(TemporaryMoveReplacementFailure.TargetMoveExcluded,
            Failure(excludedBattle, new UseMove(0), new Pass()));

        BattleMove fallback = new(EntityId.Parse("move:fallback"), Normal, DamageClass.Status,
            null, null, 1, 0, 0, target: MoveTarget.Selected, isFallback: true);
        var fallbackBattle = Battle(Creature("fallback_source", 100, Replacement()),
            Creature("fallback_target", 1, fallback));
        fallbackBattle.ResolveTurn(new Pass(), new UseMove(0));
        Assert.Equal(TemporaryMoveReplacementFailure.TargetMoveExcluded,
            Failure(fallbackBattle, new UseMove(0), new Pass()));

        BattleMove visible = Wait("visible");
        var blocked = Battle(Creature("blocked_source", 100, Replacement()),
            Creature("blocked_target", 1, visible));
        blocked.ResolveTurn(new Pass(), new UseMove(0));
        blocked.Overlays.Apply(Overlay(EnemySlot, new DecoyOverlay(new BattleDecoyState(10, 10)), 99));
        Assert.Equal(TemporaryMoveReplacementFailure.TargetBlocked,
            Failure(blocked, new UseMove(0), new Pass()));
        Assert.DoesNotContain(blocked.Overlays.Snapshot(), entry => entry.Payload is MoveSlotOverlay);
        Assert.Contains(blocked.Trace, trace => trace.Kind == EffectTraceKind.MoveReplacement && !trace.Performed);
    }

    [Fact]
    public void ReplacementUsesTheLastSuccessfulMoveRatherThanTheLastAttempt()
    {
        BattleMove successful = Fixed("successful", 10);
        BattleMove missed = new(EntityId.Parse("move:missed"), Normal, DamageClass.Physical,
            40, 0, 20, 0, 0, target: MoveTarget.Selected);
        BattleCreature source = Creature("history_source", 10, Replacement());
        BattleCreature target = Creature("history_target", 100, successful, missed);
        BattleController battle = Battle(source, target);

        battle.ResolveTurn(new Pass(), new UseMove(0));
        battle.ResolveTurn(new Pass(), new UseMove(1));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, item => item == new TemporaryMoveReplacementApplied(
            PlayerSlot, EnemySlot, 0, source.Moves[0].Move, successful.Move));
        Assert.Equal(successful.Move, Values(battle, PlayerSlot).Moves[0].Definition.Move);
        Assert.Equal(missed.Move, target.LastMoveUsed);
    }

    [Fact]
    public void SlotOverlayComposesWithWholeMoveListsWithoutFreezingSiblingSlots()
    {
        BattleMove replacement = Replacement();
        BattleMove baseSibling = Wait("base_sibling");
        BattleMove priorSibling = Wait("prior_sibling");
        BattleMove copied = Fixed("slot_copy", 15, type: Flame);
        BattleCreature source = Creature("overlay_source", 100, replacement, baseSibling);
        BattleCreature target = Creature("overlay_target", 1, copied);
        BattleController battle = Battle(source, target);
        BattleOverlayChangeSet prior = battle.Overlays.Apply(Overlay(PlayerSlot, new MoveListOverlay([
            BattleEffectiveMove.FromBase(replacement, 0),
            BattleEffectiveMove.FromBase(priorSibling, 1),
        ]), 1));
        battle.ResolveTurn(new Pass(), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new Pass());
        BattleOverlayInstance replacementOverlay = Assert.Single(battle.Overlays.Snapshot(),
            entry => entry.Payload is MoveSlotOverlay);

        Assert.Equal([copied.Move, priorSibling.Move], Values(battle, PlayerSlot).Moves
            .Select(move => move.Definition.Move));

        BattleMove later0 = Wait("later_zero");
        BattleMove later1 = Wait("later_one");
        BattleOverlayChangeSet later = battle.Overlays.Apply(Overlay(PlayerSlot, new MoveListOverlay([
            BattleEffectiveMove.FromBase(later0, 0),
            BattleEffectiveMove.FromBase(later1, 1),
        ]), 99));
        Assert.Equal([later0.Move, later1.Move], Values(battle, PlayerSlot).Moves
            .Select(move => move.Definition.Move));

        battle.Overlays.Remove(later.Affected.Select(entry => entry.Sequence).ToArray(), 3, 1);
        Assert.Equal([copied.Move, priorSibling.Move], Values(battle, PlayerSlot).Moves
            .Select(move => move.Definition.Move));
        battle.Overlays.Remove([replacementOverlay.Sequence], 3, 2);
        Assert.Equal([replacement.Move, priorSibling.Move], Values(battle, PlayerSlot).Moves
            .Select(move => move.Definition.Move));
        Assert.Single(prior.Affected);
    }

    [Fact]
    public void FaintAndLiveBattleEndRemoveTheReplacement()
    {
        BattleMove copied = Wait("cleanup_copy");
        BattleMove lethal = Fixed("lethal", 5000);
        BattleCreature faintSource = Creature("faint_source", 10, Replacement());
        BattleCreature faintTarget = Creature("faint_target", 100, copied, lethal);
        BattleController faintBattle = Battle(faintSource, faintTarget);
        faintBattle.ResolveTurn(new Pass(), new UseMove(0));
        faintBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(faintBattle.Overlays.Snapshot(), entry => entry.Payload is MoveSlotOverlay);

        faintBattle.ResolveTurn(new Pass(), new UseMove(1));

        Assert.True(faintSource.IsFainted);
        Assert.DoesNotContain(faintBattle.Overlays.Snapshot(), entry => entry.Payload is MoveSlotOverlay);

        BattleMove endCopy = Wait("end_copy");
        BattleMove endBattle = Compile("end_battle", DamageClass.Status, null,
            MoveTarget.User, Op("selfDestruct"));
        BattleCreature liveSource = Creature("end_source", 100, Replacement());
        BattleCreature endingTarget = Creature("ending_target", 1, endCopy, endBattle);
        BattleController ending = Battle(liveSource, endingTarget);
        ending.ResolveTurn(new Pass(), new UseMove(0));
        ending.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(ending.Overlays.Snapshot(), entry => entry.Payload is MoveSlotOverlay);

        ending.ResolveTurn(new Pass(), new UseMove(1));

        Assert.False(liveSource.IsFainted);
        Assert.NotNull(ending.Outcome);
        Assert.DoesNotContain(ending.Overlays.Snapshot(), entry => entry.Payload is MoveSlotOverlay);
    }

    [Fact]
    public void SmartAiUsesTheEffectiveReplacementAndRejectsVisibleFailure()
    {
        BattleMove copied = Fixed("ai_copy", 100);
        BattleCreature attacker = Creature("ai_source", 100, Replacement(), Wait("ai_wait"));
        BattleCreature defender = Creature("ai_target", 1, copied);
        defender.RecordMoveUse(copied.Move);

        SmartAiDecision before = SmartAi.ChooseAction(new SmartAiContext([attacker], 0, [defender], 0,
            Chart(), new CountingRng(), Weights: new SmartAiWeights { NoiseFraction = 0 }));
        Assert.Contains(before.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component.Name == "moveReplacement");

        var unsuccessfulHistory = new BattleActionHistory();
        var enemyOwner = new BattleHistoryOwner(BattleSide.Enemy, 0, EnemySlot);
        var playerOwner = new BattleHistoryOwner(BattleSide.Player, 0, PlayerSlot);
        unsuccessfulHistory.BeginTurn(0, [new(enemyOwner, BattlePlannedActionKind.Move),
            new(playerOwner, BattlePlannedActionKind.Move)]);
        BattleActionAttemptId missed = unsuccessfulHistory.BeginMove(1, playerOwner, copied.Move);
        unsuccessfulHistory.MarkStarted(missed);
        unsuccessfulHistory.Complete(missed, BattleActionResult.Missed, [enemyOwner]);
        SmartAiDecision historyRejected = SmartAi.ChooseAction(new SmartAiContext([attacker], 0,
            [defender], 0, Chart(), new CountingRng(), ActionHistory: unsuccessfulHistory,
            Weights: new SmartAiWeights { NoiseFraction = 0 }));
        Assert.Contains(historyRejected.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component.Name == "moveReplacementUnavailable" && component.Value == -1_000_000);

        var overlays = new BattleOverlayStore();
        overlays.Apply(new BattleOverlayApplication(new BattleOverlayOwner(BattleSide.Enemy, 0,
                new BattleSlot(BattleSide.Enemy, 0)), new(), BattleOverlayLayer.FormOrSnapshot,
            new MoveSlotOverlay(0, new BattleEffectiveMove(copied.WithPpPool(5, 5), 0, Normal,
                DamageClass.Physical)), 0, 0, Cleanup: BattleOverlayCleanup.Switch
                | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));
        SmartAiDecision after = SmartAi.ChooseAction(new SmartAiContext([attacker], 0, [defender], 0,
            Chart(), new CountingRng(), Overlays: overlays,
            Weights: new SmartAiWeights { NoiseFraction = 0 }));
        Assert.Equal(new UseMove(0), after.Action);
        Assert.DoesNotContain(after.Scores.SelectMany(score => score.Components),
            component => component.Name == "moveReplacement");

        BattleCreature unavailable = Creature("ai_unavailable", 100, Replacement(), Fixed("alternative", 20));
        SmartAiDecision rejected = SmartAi.ChooseAction(new SmartAiContext([unavailable], 0,
            [Creature("fresh_target", 1, Wait("fresh"))], 0, Chart(), new CountingRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }));
        Assert.Contains(rejected.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component.Name == "moveReplacementUnavailable" && component.Value == -1_000_000);
        Assert.Equal(new UseMove(1), rejected.Action);
    }

    [Fact]
    public void ReplacementLifecycleMatchesDeterministicGolden()
    {
        static string Run()
        {
            var rng = new CountingRng();
            BattleMove replacement = Replacement();
            BattleMove copied = Fixed("golden_copy", 25, type: Flame);
            BattleCreature source = Creature("golden_source", 100, replacement, Wait("golden_wait"));
            BattleCreature reserve = Creature("golden_reserve", 50, Wait("reserve_wait"));
            BattleCreature target = Creature("golden_target", 1, copied);
            var battle = new BattleController([source, reserve], [target], Chart(), rng);
            battle.ResolveTurn(new Pass(), new UseMove(0));
            battle.ResolveTurn(new UseMove(0), new Pass());
            BattleMove runtime = Values(battle, PlayerSlot).Moves[0].Definition;
            battle.ResolveTurn(new UseMove(0), new Pass());
            battle.ResolveTurn(new Switch(1), new Pass());
            return string.Join('\n', battle.Log.Where(item => item is MoveUsed
                    or TemporaryMoveReplacementApplied or SwitchedIn)
                .Select(EventRow)
                .Concat(battle.Trace.Where(trace => trace.Kind == EffectTraceKind.MoveReplacement)
                    .Select(trace => $"trace:{trace.SourceSlot.Side}:{trace.TargetSlot?.Side}:"
                        + $"{trace.Performed}:{trace.Value}"))
                .Append($"pp:{replacement.Pp}:{runtime.Pp}:{copied.Pp}")
                .Append($"overlay:{battle.Overlays.Snapshot().Count(entry => entry.Payload is MoveSlotOverlay)}")
                .Append($"rng:{rng.Calls}"));
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("temporary-move-replacement"), first);
    }

    private static BattleController Battle(BattleCreature player, BattleCreature enemy, IRng? rng = null) =>
        new(player, enemy, Chart(), rng ?? new CountingRng());

    private static BattleEffectiveValues Values(BattleController battle, BattleSlot slot) =>
        PhysicalMetricFormulas.EffectiveValues(battle.Active(slot), battle.Overlays,
            new BattleOverlayOwner(slot.Side, battle.ActiveIndex(slot), slot));

    private static BattleOverlayApplication Overlay(BattleSlot owner, BattleOverlayPayload payload, int sequence) =>
        new(new BattleOverlayOwner(owner.Side, 0, owner), new(), BattleOverlayLayer.FormOrSnapshot,
            payload, 0, sequence, Cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint
                | BattleOverlayCleanup.BattleEnd);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
        new Stats(1000, 100, 100, 100, 100, speed), moves);

    private static BattleMove Replacement() => Compile("replacement", DamageClass.Status, null,
        MoveTarget.Selected, Op("temporaryMoveReplacement"));

    private static BattleMove Fixed(string slug, int amount, int pp = 20, EntityId? type = null) =>
        Compile(slug, DamageClass.Physical, null, MoveTarget.Selected,
            Op("fixedDamage", ("amount", amount)), pp: pp, type: type);

    private static BattleMove TaggedFixed(string slug, string tag) => Compile(slug, DamageClass.Physical,
        null, MoveTarget.Selected, Op("moveTags", ("tags", tag)), Op("fixedDamage", ("amount", 10)));

    private static BattleMove Wait(string slug) => new(EntityId.Parse($"move:{slug}"), Normal,
        DamageClass.Status, null, null, 20, 0, 0);

    private static BattleMove Compile(string slug, DamageClass damageClass, int? power,
        MoveTarget target, Effect effect, int pp = 10, EntityId? type = null) =>
        Compile(slug, damageClass, power, target, [effect], pp, type);

    private static BattleMove Compile(string slug, DamageClass damageClass, int? power,
        MoveTarget target, Effect first, Effect second) =>
        Compile(slug, damageClass, power, target, [first, second], 10, null);

    private static BattleMove Compile(string slug, DamageClass damageClass, int? power,
        MoveTarget target, IReadOnlyList<Effect> effects, int pp, EntityId? type) => MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = type ?? Normal,
            DamageClass = damageClass, Power = power, Accuracy = null, Pp = pp,
            Target = target, Effects = effects,
        });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static Effect Op(string op, int chance) => new() { Op = op, Chance = chance };

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }, new TypeDef { Id = Flame }]);

    private static string EventRow(BattleEvent battleEvent) => battleEvent switch
    {
        MoveUsed used => $"used:{used.Slot.Side}:{used.Move}",
        TemporaryMoveReplacementApplied applied =>
            $"replaced:{applied.Source.Side}:{applied.MoveSlot}:{applied.OriginalMove}>{applied.ReplacementMove}",
        SwitchedIn switched => $"switched:{switched.Slot.Side}:{switched.PartyIndex}",
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
