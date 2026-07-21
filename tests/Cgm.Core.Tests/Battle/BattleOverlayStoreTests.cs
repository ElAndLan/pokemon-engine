using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleOverlayStoreTests
{
    private static readonly BattleSlot Player0 = new(BattleSide.Player, 0);
    private static readonly BattleSlot Player1 = new(BattleSide.Player, 1);
    private static readonly BattleOverlayOwner Owner = new(BattleSide.Player, 2, Player0);
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Ember = EntityId.Parse("type:ember");
    private static readonly EntityId Tide = EntityId.Parse("type:tide");
    private static readonly EntityId BaseItem = EntityId.Parse("item:base_charm");
    private static readonly EntityId NewItem = EntityId.Parse("item:new_charm");
    private static readonly EntityId BaseAbility = EntityId.Parse("ability:base_trait");
    private static readonly EntityId NewAbility = EntityId.Parse("ability:new_trait");

    [Fact]
    public void BaseAndEachPrecedenceLayerResolveIndependently()
    {
        BattleEffectiveValues baseValues = Base();
        Assert.Equal(ScalarValues(baseValues), ScalarValues(new BattleOverlayStore().Resolve(Owner, baseValues).Values));

        var permanent = new BattleOverlayStore();
        permanent.Apply(App(new HeldItemOverlay(NewItem), BattleOverlayLayer.PermanentInstance));
        Assert.Equal(NewItem, permanent.Resolve(Owner, baseValues).Values.HeldItem);

        var form = new BattleOverlayStore();
        form.Apply(App(new FormOverlay("alternate"), BattleOverlayLayer.FormOrSnapshot));
        Assert.Equal("alternate", form.Resolve(Owner, baseValues).Values.FormId);

        var additive = new BattleOverlayStore();
        additive.Apply(App(new MetricDeltaOverlay("boost", BattleMetric.Weight, 5), BattleOverlayLayer.Additive));
        Assert.Equal(105, additive.Resolve(Owner, baseValues).Values.Metrics![BattleMetric.Weight]);

        var suppression = new BattleOverlayStore();
        suppression.Apply(App(new SuppressionOverlay(BattleEffectiveValueKind.Ability), BattleOverlayLayer.Suppression));
        Assert.Null(suppression.Resolve(Owner, baseValues).Values.Ability);
    }

    [Fact]
    public void EachTypedValueResolvesWithoutMutatingBaseDefinitions()
    {
        BattleEffectiveValues baseValues = Base();
        BattleMove original = baseValues.Moves[0].Definition;
        BattleMove replacement = Move("replacement", Ember, DamageClass.Special);
        var store = new BattleOverlayStore();
        store.Apply(App(new HeldItemOverlay(NewItem)));
        store.Apply(App(new AbilityOverlay(NewAbility)));
        store.Apply(App(new CreatureTypesOverlay([Ember])));
        store.Apply(App(new StatsOverlay(new Stats(150, 80, 70, 60, 50, 40))));
        store.Apply(App(new MoveListOverlay([BattleEffectiveMove.FromBase(replacement, 3)])));
        store.Apply(App(new MoveTypeOverlay(0, Tide)));
        store.Apply(App(new MoveClassOverlay(0, DamageClass.Physical)));
        store.Apply(App(new FormOverlay("bright")));
        store.Apply(App(new DecoyOverlay(new BattleDecoyState(20, 30))));
        store.Apply(App(new MetricOverlay(BattleMetric.Weight, 90)));

        BattleEffectiveResult result = store.Resolve(Owner, baseValues);

        Assert.Equal(NewItem, result.Values.HeldItem);
        Assert.Equal(NewAbility, result.Values.Ability);
        Assert.Equal([Ember], result.Values.CreatureTypes);
        Assert.Equal(new Stats(150, 80, 70, 60, 50, 40), result.Values.Stats);
        BattleEffectiveMove move = Assert.Single(result.Values.Moves);
        Assert.Same(replacement, move.Definition);
        Assert.Equal(3, move.PpOwnerSlot);
        Assert.Equal(Tide, move.Type);
        Assert.Equal(DamageClass.Physical, move.DamageClass);
        Assert.Equal("bright", result.Values.FormId);
        Assert.Equal(new BattleDecoyState(20, 30), result.Values.Decoy);
        Assert.Equal(90, result.Values.Metrics![BattleMetric.Weight]);
        Assert.Equal(Normal, original.Type);
        Assert.Equal(DamageClass.Physical, original.DamageClass);
        Assert.Equal(BaseItem, baseValues.HeldItem);
        Assert.Equal(10, result.Trace.Count);
    }

    [Fact]
    public void PrecedenceIsBasePermanentFormAdditiveSuppressionThenIgnore()
    {
        var store = new BattleOverlayStore();
        store.Apply(App(new HeldItemOverlay(NewItem), BattleOverlayLayer.PermanentInstance));
        store.Apply(App(new HeldItemOverlay(EntityId.Parse("item:form_charm")), BattleOverlayLayer.FormOrSnapshot));
        store.Apply(App(new StatsOverlay(new Stats(20, 20, 20, 20, 20, 20)), BattleOverlayLayer.PermanentInstance));
        store.Apply(App(new StatsOverlay(new Stats(30, 30, 30, 30, 30, 30)), BattleOverlayLayer.FormOrSnapshot));
        store.Apply(App(new StatDeltaOverlay("field", new Stats(5, 5, 5, 5, 5, 5)), BattleOverlayLayer.Additive));
        BattleOverlayInstance suppression = Assert.Single(store.Apply(
            App(new SuppressionOverlay(BattleEffectiveValueKind.HeldItem), BattleOverlayLayer.Suppression)).Affected);

        BattleEffectiveResult suppressed = store.Resolve(Owner, Base());
        BattleEffectiveResult ignored = store.Resolve(Owner, Base(), [suppression.Sequence]);

        Assert.Null(suppressed.Values.HeldItem);
        Assert.Equal(new Stats(35, 35, 35, 35, 35, 35), suppressed.Values.Stats);
        Assert.Equal(EntityId.Parse("item:form_charm"), ignored.Values.HeldItem);
        Assert.Contains(suppressed.Trace, row => row.Kind == BattleOverlayTraceKind.Suppressed);
        Assert.Contains(ignored.Trace, row => row.Kind == BattleOverlayTraceKind.SuppressionIgnored);
    }

    [Fact]
    public void LaterSameKeySupersedesWhileDistinctAdditiveKeysCombineAndClamp()
    {
        var store = new BattleOverlayStore();
        store.Apply(App(new StatDeltaOverlay("same", new Stats(2, 2, 2, 2, 2, 2)), BattleOverlayLayer.Additive));
        store.Apply(App(new StatDeltaOverlay("other", new Stats(3, 3, 3, 3, 3, 3)), BattleOverlayLayer.Additive));
        store.Apply(App(new StatDeltaOverlay("same", new Stats(5, 5, 5, 5, 5, 5)), BattleOverlayLayer.Additive));
        store.Apply(App(new StatDeltaOverlay("floor", new Stats(-99, -99, -99, -99, -99, -99)), BattleOverlayLayer.Additive));
        store.Apply(App(new TypeAdditionOverlay("first", [Ember, Normal]), BattleOverlayLayer.Additive));
        store.Apply(App(new TypeAdditionOverlay("second", [Tide, Ember]), BattleOverlayLayer.Additive));
        store.Apply(App(new MetricDeltaOverlay("load", BattleMetric.Weight, -500), BattleOverlayLayer.Additive));

        BattleEffectiveResult result = store.Resolve(Owner, Base());

        Assert.Equal(new Stats(1, 1, 1, 1, 1, 1), result.Values.Stats);
        Assert.Equal([Normal, Ember, Tide], result.Values.CreatureTypes);
        Assert.Equal(1, result.Values.Metrics![BattleMetric.Weight]);
        Assert.Single(result.Trace, row => row.Kind == BattleOverlayTraceKind.Superseded);
    }

    [Fact]
    public void DurationSwitchFaintAndBattleEndCleanupAreExact()
    {
        var store = new BattleOverlayStore();
        BattleOverlayInstance switchClear = Assert.Single(store.Apply(App(new FormOverlay("switch_clear"),
            cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.BattleEnd)).Affected);
        BattleOverlayInstance faintClear = Assert.Single(store.Apply(App(new AbilityOverlay(NewAbility),
            cleanup: BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd)).Affected);
        BattleOverlayInstance persistent = Assert.Single(store.Apply(App(new HeldItemOverlay(NewItem))).Affected);
        BattleOverlayInstance timed = Assert.Single(store.Apply(App(new MetricOverlay(BattleMetric.Height, 8),
            duration: 2, checkpoint: BattleIntentCheckpoint.TurnEnd)).Affected);

        BattleOverlayChangeSet switched = store.OwnerSwitched(BattleSide.Player, 2, Player1, 0, 4);
        BattleOverlayTraceEntry switchTrace = Assert.Single(switched.Trace,
            row => row.Sequence == switchClear.Sequence && row.Kind == BattleOverlayTraceKind.Removed);
        Assert.Equal(BattleOverlayRemovalReason.Switch, switchTrace.RemovalReason);
        Assert.Equal(Player0, switchTrace.Source.Slot);
        Assert.All(store.Snapshot(), entry => Assert.Equal(Player1, entry.Owner.Slot));
        Assert.DoesNotContain(store.Snapshot(), entry => entry.Sequence == switchClear.Sequence);
        Assert.Contains(store.Snapshot(), entry => entry.Sequence == faintClear.Sequence);
        Assert.Contains(store.Snapshot(), entry => entry.Sequence == persistent.Sequence);

        store.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 1, 0);
        BattleOverlayChangeSet expired = store.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 2, 0);
        Assert.Contains(expired.Trace, row => row.Sequence == timed.Sequence && row.Kind == BattleOverlayTraceKind.Expired);
        BattleOverlayChangeSet fainted = store.OwnerFainted(BattleSide.Player, 2, 2, 1);
        Assert.Equal(faintClear.Sequence, Assert.Single(fainted.Affected).Sequence);
        Assert.Equal(BattleOverlayRemovalReason.Faint, Assert.Single(fainted.Trace).RemovalReason);
        BattleOverlayChangeSet ended = store.EndBattle(2, 2);
        Assert.Equal(persistent.Sequence, Assert.Single(ended.Affected).Sequence);
        Assert.Equal(BattleOverlayRemovalReason.BattleEnd, Assert.Single(ended.Trace).RemovalReason);
        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void ResolverAndAiUseTheSameImmutableEffectiveResult()
    {
        var store = new BattleOverlayStore();
        store.Apply(App(new StatsOverlay(new Stats(80, 90, 100, 110, 120, 130)), BattleOverlayLayer.FormOrSnapshot));
        store.Apply(App(new TypeAdditionOverlay("aura", [Ember]), BattleOverlayLayer.Additive));
        BattleEffectiveValues baseValues = Base();

        BattleEffectiveResult resolver = store.Resolve(Owner, baseValues, turn: 3, actionSequence: 7);
        BattleEffectiveResult ai = store.Resolve(Owner, baseValues, turn: 3, actionSequence: 7);

        Assert.Equal(Scalar(resolver), Scalar(ai));
        Assert.Equal(resolver.Trace, ai.Trace);
        Assert.Equal(new Stats(10, 20, 30, 40, 50, 60), baseValues.Stats);
        Assert.Equal([Normal], baseValues.CreatureTypes);
    }

    [Fact]
    public void OverlayInputsAreCapturedAndReplayOrderIsStable()
    {
        var baseTypes = new List<EntityId> { Normal };
        BattleEffectiveValues capturedBase = new(BaseItem, BaseAbility, baseTypes,
            new Stats(10, 20, 30, 40, 50, 60),
            [BattleEffectiveMove.FromBase(Move("base_capture", Normal, DamageClass.Physical), 0)]);
        baseTypes.Add(Tide);
        Assert.Equal([Normal], capturedBase.CreatureTypes);

        var mutableTypes = new List<EntityId> { Ember };
        var store = new BattleOverlayStore();
        store.Apply(App(new CreatureTypesOverlay(mutableTypes)));
        mutableTypes.Add(Tide);

        Assert.Equal([Ember], store.Resolve(Owner, Base()).Values.CreatureTypes);

        static string Replay()
        {
            var replay = new BattleOverlayStore();
            replay.Apply(App(new StatsOverlay(new Stats(20, 21, 22, 23, 24, 25))));
            replay.Apply(App(new StatDeltaOverlay("bonus", new Stats(1, 1, 1, 1, 1, 1)), BattleOverlayLayer.Additive));
            BattleEffectiveResult result = replay.Resolve(Owner, Base(), turn: 1, actionSequence: 2);
            return Scalar(result) + "|" + JsonSerializer.Serialize(result.Trace);
        }

        Assert.Equal(Replay(), Replay());
    }

    [Fact]
    public void StrictValidationFailsBeforeStoreMutationOrPureResolutionResult()
    {
        var store = new BattleOverlayStore();
        Assert.Throws<ArgumentException>(() => store.Apply(App(new HeldItemOverlay(EntityId.Parse("ability:not_item")))));
        Assert.Throws<ArgumentException>(() => store.Apply(App(new CreatureTypesOverlay([Normal, Normal]))));
        Assert.Throws<ArgumentException>(() => store.Apply(App(new StatDeltaOverlay("Bad-Key", default), BattleOverlayLayer.Additive)));
        Assert.Throws<ArgumentException>(() => store.Apply(App(new SuppressionOverlay(BattleEffectiveValueKind.Stats), BattleOverlayLayer.Suppression)));
        Assert.Throws<ArgumentException>(() => store.Apply(App(new StatsOverlay(new Stats(1, 1, 1, 1, 1, 1)), BattleOverlayLayer.Additive)));
        Assert.Throws<ArgumentException>(() => store.Apply(App(new FormOverlay("valid"), duration: 2)));
        Assert.Throws<ArgumentException>(() => store.Apply(App(new FormOverlay("valid"), cleanup: BattleOverlayCleanup.Switch)));
        Assert.Throws<ArgumentException>(() => store.Apply(App(new FormOverlay("valid")) with
        {
            Owner = new BattleOverlayOwner(BattleSide.Player, -1),
        }));
        Assert.Empty(store.Snapshot());

        BattleOverlayInstance suppression = Assert.Single(store.Apply(
            App(new SuppressionOverlay(BattleEffectiveValueKind.Ability), BattleOverlayLayer.Suppression)).Affected);
        Assert.Throws<ArgumentException>(() => store.Resolve(Owner, Base(), [suppression.Sequence + 1]));
        Assert.Throws<ArgumentException>(() => store.Resolve(Owner, new BattleEffectiveValues(
            BaseItem, BaseAbility, [EntityId.Parse("item:not_type")], new Stats(1, 1, 1, 1, 1, 1),
            [BattleEffectiveMove.FromBase(Move("invalid_base", Normal, DamageClass.Physical), 0)])));
        store.Apply(App(new MoveTypeOverlay(5, Ember)));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Resolve(Owner, Base()));
    }

    [Fact]
    public void Phase15F6NestedSnapshotMatrixRevealsAndCleansExactFamilies()
    {
        var package = SnapshotPackage();
        BattleEffectiveResult resolver = package.Store.Resolve(Owner, package.Base, turn: 1, actionSequence: 1);
        BattleEffectiveResult preview = package.Store.Resolve(Owner, package.Base, turn: 1, actionSequence: 1);
        Assert.Equal(resolver.Trace, preview.Trace);
        BattleEffectiveValues activeForm = resolver.Values;
        Assert.Equal("active_form", activeForm.FormId);
        Assert.Equal([Tide], activeForm.CreatureTypes);
        Assert.Equal(new BattleDecoyState(12, 12), activeForm.Decoy);
        Assert.Equal(["form_zero", "form_one"], activeForm.Moves.Select(move => move.Definition.Move.Slug));

        package.Store.Remove(package.FormSequences, 1, 1);
        BattleEffectiveValues replacement = package.Store.Resolve(Owner, package.Base).Values;
        Assert.Equal("captured_form", replacement.FormId);
        Assert.Equal(["replacement", "transform_one"], replacement.Moves
            .Select(move => move.Definition.Move.Slug));
        Assert.Equal((5, 5), (replacement.Moves[0].Definition.Pp, replacement.Moves[0].Definition.MaxPp));
        Assert.Equal(new BattleDecoyState(12, 12), replacement.Decoy);

        package.Store.Remove([package.ReplacementSequence], 1, 2);
        Assert.Equal(["transform_zero", "transform_one"], package.Store.Resolve(Owner, package.Base).Values.Moves
            .Select(move => move.Definition.Move.Slug));
        package.Store.Remove([package.TransformSequence], 1, 3);
        BattleEffectiveValues authored = package.Store.Resolve(Owner, package.Base).Values;
        Assert.Equal(["base_zero", "base_one"], authored.Moves.Select(move => move.Definition.Move.Slug));
        Assert.Equal(new BattleDecoyState(12, 12), authored.Decoy);
        Assert.All(package.Base.Moves, move => Assert.Equal((20, 20),
            (move.Definition.Pp, move.Definition.MaxPp)));

        package.Store.OwnerSwitched(BattleSide.Player, 2, Player1, 2, 1);
        Assert.Null(package.Store.Resolve(Owner with { Slot = Player1 }, package.Base).Values.Decoy);

        var lifecycle = SnapshotPackage();
        BattleOverlayChangeSet switched = lifecycle.Store.OwnerSwitched(
            BattleSide.Player, 2, Player1, 2, 2);
        BattleOverlayOwner destination = Owner with { Slot = Player1 };
        Assert.Equal("active_form", lifecycle.Store.Resolve(destination, lifecycle.Base).Values.FormId);
        Assert.Equal(3, switched.Trace.Count(trace => trace.Kind == BattleOverlayTraceKind.Removed
            && trace.RemovalReason == BattleOverlayRemovalReason.Switch));
        Assert.Equal(5, switched.Trace.Count(trace => trace.Kind == BattleOverlayTraceKind.Transferred));
        BattleOverlayChangeSet fainted = lifecycle.Store.OwnerFainted(BattleSide.Player, 2, 2, 3);
        Assert.Equal(5, fainted.Trace.Count(trace => trace.RemovalReason == BattleOverlayRemovalReason.Faint));
        Assert.Empty(lifecycle.Store.Snapshot());

        var ending = SnapshotPackage();
        BattleOverlayChangeSet ended = ending.Store.EndBattle(3, 1);
        Assert.Equal(8, ended.Trace.Count(trace => trace.RemovalReason == BattleOverlayRemovalReason.BattleEnd));
        Assert.Empty(ending.Store.Snapshot());
    }

    [Fact]
    public void Phase15F6PackageLifecycleMatchesCumulativeGolden()
    {
        static string Run()
        {
            var reveal = SnapshotPackage();
            var rows = new List<string> { $"active:{PackageState(reveal.Store, Owner, reveal.Base)}" };
            reveal.Store.Remove(reveal.FormSequences, 1, 1);
            rows.Add($"without-form:{PackageState(reveal.Store, Owner, reveal.Base)}");
            reveal.Store.Remove([reveal.ReplacementSequence], 1, 2);
            rows.Add($"without-replacement:{PackageState(reveal.Store, Owner, reveal.Base)}");
            reveal.Store.Remove([reveal.TransformSequence], 1, 3);
            rows.Add($"authored-plus-decoy:{PackageState(reveal.Store, Owner, reveal.Base)}");
            reveal.Store.OwnerSwitched(BattleSide.Player, 2, Player1, 2, 1);
            rows.Add($"after-switch:{PackageState(reveal.Store, Owner with { Slot = Player1 }, reveal.Base)}");

            var lifecycle = SnapshotPackage();
            BattleOverlayChangeSet switched = lifecycle.Store.OwnerSwitched(
                BattleSide.Player, 2, Player1, 2, 2);
            rows.Add($"nested-switch:{TraceCounts(switched)}:{PackageState(lifecycle.Store,
                Owner with { Slot = Player1 }, lifecycle.Base)}");
            BattleOverlayChangeSet fainted = lifecycle.Store.OwnerFainted(BattleSide.Player, 2, 2, 3);
            rows.Add($"nested-faint:{TraceCounts(fainted)}:{lifecycle.Store.Snapshot().Count}");

            var ending = SnapshotPackage();
            BattleOverlayChangeSet ended = ending.Store.EndBattle(3, 1);
            rows.Add($"battle-end:{TraceCounts(ended)}:{ending.Store.Snapshot().Count}");
            return string.Join('\n', rows);
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("snapshot-package"), first);
    }

    private static BattleOverlayApplication App(BattleOverlayPayload payload,
        BattleOverlayLayer layer = BattleOverlayLayer.FormOrSnapshot,
        BattleOverlayCleanup cleanup = BattleOverlayCleanup.BattleEnd,
        int? duration = null,
        BattleIntentCheckpoint? checkpoint = null) => new(
            Owner, new BattleOverlaySource(Player0, 0, EntityId.Parse("move:overlay_source")),
            layer, payload, 0, 0, duration, checkpoint, cleanup);

    private static BattleEffectiveValues Base() => new(
        BaseItem,
        BaseAbility,
        [Normal],
        new Stats(10, 20, 30, 40, 50, 60),
        [BattleEffectiveMove.FromBase(Move("base", Normal, DamageClass.Physical), 0)],
        metrics: new Dictionary<BattleMetric, int>
        {
            [BattleMetric.Weight] = 100,
            [BattleMetric.Height] = 10,
        });

    private static (BattleOverlayStore Store, BattleEffectiveValues Base, long TransformSequence,
        long ReplacementSequence, long[] FormSequences) SnapshotPackage()
    {
        BattleMove baseZero = Move("base_zero", Normal, DamageClass.Physical);
        BattleMove baseOne = Move("base_one", Ember, DamageClass.Special);
        BattleEffectiveValues template = Base();
        var baseValues = new BattleEffectiveValues(template.HeldItem, template.Ability,
            template.CreatureTypes, template.Stats,
            [BattleEffectiveMove.FromBase(baseZero, 0), BattleEffectiveMove.FromBase(baseOne, 1)],
            template.FormId, template.Decoy, template.Metrics, template.MoveTypeRules);
        var store = new BattleOverlayStore();
        BattleOverlayCleanup clearsOnExit = BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint
            | BattleOverlayCleanup.BattleEnd;
        store.Apply(App(new DecoyOverlay(new BattleDecoyState(12, 12)), cleanup: clearsOnExit));
        BattleMove transformZero = Move("transform_zero", Ember, DamageClass.Special).WithPpPool(5, 5);
        BattleMove transformOne = Move("transform_one", Tide, DamageClass.Physical).WithPpPool(5, 5);
        long transform = Assert.Single(store.Apply(App(new TransformOverlay(NewAbility, [Ember],
            new Stats(80, 81, 82, 83, 84, 85),
            [BattleEffectiveMove.FromBase(transformZero, 0), BattleEffectiveMove.FromBase(transformOne, 1)],
            "captured_form", 140), cleanup: clearsOnExit)).Affected).Sequence;
        BattleMove replacementMove = Move("replacement", Tide, DamageClass.Special).WithPpPool(5, 5);
        long replacement = Assert.Single(store.Apply(App(new MoveSlotOverlay(0,
            BattleEffectiveMove.FromBase(replacementMove, 0)), cleanup: clearsOnExit)).Affected).Sequence;

        BattleMove formZero = Move("form_zero", Tide, DamageClass.Special);
        BattleMove formOne = Move("form_one", Tide, DamageClass.Physical);
        BattleOverlayCleanup formCleanup = BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd;
        long[] form = store.ApplyMany(
        [
            App(new StatsOverlay(new Stats(90, 91, 92, 93, 94, 95)), cleanup: formCleanup),
            App(new CreatureTypesOverlay([Tide]), cleanup: formCleanup),
            App(new AbilityOverlay(BaseAbility), cleanup: formCleanup),
            App(new MoveListOverlay([BattleEffectiveMove.FromBase(formZero, 0),
                BattleEffectiveMove.FromBase(formOne, 1)]), cleanup: formCleanup),
            App(new FormOverlay("active_form"), cleanup: formCleanup),
        ]).Affected.Select(entry => entry.Sequence).ToArray();
        return (store, baseValues, transform, replacement, form);
    }

    private static string PackageState(BattleOverlayStore store, BattleOverlayOwner owner,
        BattleEffectiveValues baseValues)
    {
        BattleEffectiveValues values = store.Resolve(owner, baseValues).Values;
        return $"{values.FormId ?? "base"}:{values.Ability}:{string.Join(',', values.CreatureTypes)}:"
            + $"{string.Join(',', values.Moves.Select(move => $"{move.Definition.Move.Slug}@{move.Definition.Pp}"))}:"
            + $"{values.Decoy?.Hp ?? 0}";
    }

    private static string TraceCounts(BattleOverlayChangeSet changes) => string.Join(',', changes.Trace
        .GroupBy(trace => $"{trace.Kind}/{trace.RemovalReason?.ToString() ?? "None"}")
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => $"{group.Key}={group.Count()}"));

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static BattleMove Move(string slug, EntityId type, DamageClass damageClass) =>
        new(EntityId.Parse($"move:{slug}"), type, damageClass, 40, 100, 20, 0, 0);

    private static string Scalar(BattleEffectiveResult result) => ScalarValues(result.Values);

    private static string ScalarValues(BattleEffectiveValues values) => string.Join('|',
        values.HeldItem,
        values.Ability,
        string.Join(',', values.CreatureTypes),
        values.Stats,
        string.Join(',', values.Moves.Select(move =>
            $"{move.Definition.Move}:{move.PpOwnerSlot}:{move.Type}:{move.DamageClass}")),
        values.FormId,
        values.Decoy,
        string.Join(',', values.Metrics!.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}")));
}
