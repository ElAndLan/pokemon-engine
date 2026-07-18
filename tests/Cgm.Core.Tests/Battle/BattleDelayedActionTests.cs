using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDelayedActionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Immune = EntityId.Parse("type:immune");

    [Fact]
    public void Compiler_ProducesTypedDelayedVocabulary()
    {
        BattleMove damage = Compile(DamageClass.Special, 80, MoveTarget.Selected,
            Op("damage"), Op("delayedDamage", ("turns", 2), ("sourceRequired", true),
                ("uniquePerSlot", true)));
        BattleMove heal = Compile(DamageClass.Status, null, MoveTarget.User,
            Op("delayedHeal", ("turns", 1), ("num", 1), ("den", 2),
                ("basis", "sourceMaxHp"), ("targetPolicy", "liveSlot")));
        BattleMove status = Compile(DamageClass.Status, null, MoveTarget.Selected,
            Op("delayedStatus", ("turns", 1), ("status", "sleep")));
        BattleMove replacement = Compile(DamageClass.Status, null, MoveTarget.User,
            Op("replacementRestore", ("pp", true)), Op("selfDestruct"));

        Assert.Contains(new DelayedDamageEffect(2, true, true), damage.SecondaryEffects);
        Assert.Contains(new DelayedHealEffect(1, new Fraction(1, 2)), heal.SecondaryEffects);
        Assert.Contains(new DelayedStatusEffect(1, PersistentStatus.Sleep), status.SecondaryEffects);
        Assert.Contains(new ReplacementRestoreEffect(true, true, true), replacement.SecondaryEffects);
    }

    [Fact]
    public void Compiler_RejectsMalformedAndIncompatibleDelayedRows()
    {
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 40, MoveTarget.Selected,
            Op("delayedDamage", ("turns", 0))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null, MoveTarget.Selected,
            Op("delayedDamage", ("turns", 1))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null, MoveTarget.User,
            Op("delayedHeal", ("turns", 1), ("num", 1), ("den", 0))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null, MoveTarget.Selected,
            Op("delayedStatus", ("turns", 1), ("status", "unknown"))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null, MoveTarget.User,
            Op("replacementRestore")));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null, MoveTarget.User,
            Op("selfDestruct"), Op("replacementRestore")));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null, MoveTarget.User,
            Op("delayedHeal", ("turns", 1), ("num", 1), ("den", 2)),
            Op("delayedStatus", ("turns", 1), ("status", "sleep"))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 40, MoveTarget.Selected,
            Op("delayedDamage", ("turns", 1)),
            Op("effectivenessQuery", ("mode", "inverse"))));
    }

    [Fact]
    public void DelayedDamage_PaysOnceAndDrawsOnlyAccuracyUntilDueTurn()
    {
        BattleMove delayed = Move("delayed", DamageClass.Special, 80, MoveTarget.Selected,
            [new DelayedDamageEffect(2)]);
        var rng = new CountingRng();
        var battle = Battle(Creature("source", 100, delayed), Creature("target", 1, Inert()), rng: rng);
        int before = battle.Active(BattleSide.Enemy).CurrentHp;

        IReadOnlyList<BattleEvent> use = battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(before, battle.Active(BattleSide.Enemy).CurrentHp);
        Assert.Equal(1, rng.Calls);
        Assert.Equal(9, delayed.Pp);
        Assert.Contains(use, e => e is DelayedActionQueued { Payload: BattleIntentPayloadKind.DelayedDamage });

        battle.ResolveTurn(new Pass(), new Pass());
        Assert.Equal(before, battle.Active(BattleSide.Enemy).CurrentHp);
        Assert.Equal(1, rng.Calls);

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Pass(), new Pass());
        Assert.True(battle.Active(BattleSide.Enemy).CurrentHp < before);
        Assert.Equal(2, rng.Calls);
        Assert.Equal(9, delayed.Pp);
        Assert.Contains(due, e => e is DelayedActionResolved { Payload: BattleIntentPayloadKind.DelayedDamage });
    }

    [Fact]
    public void DelayedDamage_UsesStoredSourceAndLiveReplacementDefense()
    {
        int lowDefense = DamageAfterSourceAndTargetSwitch(replacementSpd: 50, reserveSpa: 1);
        int highDefense = DamageAfterSourceAndTargetSwitch(replacementSpd: 400, reserveSpa: 1);
        int differentReserve = DamageAfterSourceAndTargetSwitch(replacementSpd: 50, reserveSpa: 500);

        Assert.True(lowDefense > highDefense);
        Assert.Equal(lowDefense, differentReserve);
    }

    [Fact]
    public void DelayedDamage_ExecutionImmunitySkipsDamageRoll()
    {
        BattleMove delayed = Move("delayed", DamageClass.Special, 80, MoveTarget.Selected,
            [new DelayedDamageEffect(2)]);
        BattleCreature source = Creature("source", 100, delayed);
        BattleCreature original = Creature("original", 1, Inert());
        BattleCreature replacement = Creature("replacement", 1, [Immune], 100, 100, Inert());
        var rng = new CountingRng();
        var chart = new TypeChart([
            new TypeDef { Id = Normal, NoDamageTo = [Immune] }, new TypeDef { Id = Immune },
        ]);
        var battle = new BattleController([source], [original, replacement], chart, rng);
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new Pass(), new Switch(1));
        int before = replacement.CurrentHp;

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Pass(), new Pass());

        Assert.Equal(before, replacement.CurrentHp);
        Assert.Equal(1, rng.Calls);
        Assert.Contains(due, e => e is DelayedActionFailed
            { Reason: DelayedActionFailureReason.Immune });
    }

    [Fact]
    public void DelayedDamage_UniqueSlotRejectsASecondPendingActionWithoutReplacingTheFirst()
    {
        BattleMove delayed = Move("delayed", DamageClass.Special, 80, MoveTarget.Selected,
            [new DelayedDamageEffect(2, UniquePerSlot: true)]);
        var battle = Battle(Creature("source", 100, delayed), Creature("target", 1, Inert()));

        battle.ResolveTurn(new UseMove(0), new Pass());
        IReadOnlyList<BattleEvent> second = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(second, e => e is DelayedActionFailed
            { Reason: DelayedActionFailureReason.SlotOccupied });
        Assert.Single(battle.IntentQueueSnapshot);
    }

    [Theory]
    [InlineData(BattleIntentTargetPolicy.LiveSlot, true)]
    [InlineData(BattleIntentTargetPolicy.SnapshotSlot, false)]
    public void DelayedHeal_UsesAuthoredReplacementPolicy(BattleIntentTargetPolicy policy,
        bool healsReplacement)
    {
        BattleMove delayed = Move("delayed_heal", DamageClass.Status, null, MoveTarget.User,
            [new DelayedHealEffect(1, new Fraction(1, 2), TargetPolicy: policy)]);
        BattleCreature source = Creature("source", 100, delayed);
        BattleCreature replacement = Creature("replacement", 1, Inert());
        replacement.TakeDamage(600);
        var battle = new BattleController([source, replacement], [Creature("target", 1, Inert())],
            Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new Pass());
        int before = replacement.CurrentHp;

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Switch(1), new Pass());

        Assert.Equal(healsReplacement, replacement.CurrentHp > before);
        Assert.Equal(!healsReplacement, due.Any(e => e is DelayedActionFailed
            { Reason: DelayedActionFailureReason.TargetUnavailable }));
    }

    [Fact]
    public void SourceRequired_FailsVisiblyAfterSourceSwitches()
    {
        BattleMove delayed = Move("source_required", DamageClass.Status, null, MoveTarget.User,
            [new DelayedHealEffect(1, new Fraction(1, 2), SourceRequired: true)]);
        BattleCreature source = Creature("source", 100, delayed);
        BattleCreature replacement = Creature("replacement", 1, Inert());
        replacement.TakeDamage(500);
        var battle = new BattleController([source, replacement], [Creature("target", 1, Inert())],
            Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Switch(1), new Pass());

        Assert.Contains(due, e => e is DelayedActionFailed
            { Reason: DelayedActionFailureReason.SourceUnavailable });
        Assert.Equal(500, replacement.CurrentHp);
    }

    [Fact]
    public void DelayedHealing_ReevaluatesBlockingQueryAtExecution()
    {
        BattleMove delayed = Move("blocked_heal", DamageClass.Status, null, MoveTarget.User,
        [
            new DelayedHealEffect(1, new Fraction(1, 2)),
            new MoveQueryModifierEffect(BattleQueryId.Healing, BattleQueryOperation.Replace,
                new BattleQueryValue(0)),
        ]);
        BattleCreature source = Creature("source", 100, delayed);
        source.TakeDamage(500);
        var battle = Battle(source, Creature("target", 1, Inert()));
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Pass(), new Pass());

        Assert.Equal(500, source.CurrentHp);
        Assert.Contains(due, e => e is DelayedActionFailed
            { Reason: DelayedActionFailureReason.HealingBlocked });
        Assert.Equal(0, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.Healing)
            .Result.FinalValue.ToInt32());
    }

    [Theory]
    [InlineData(BattleIntentTargetPolicy.LiveSlot, true)]
    [InlineData(BattleIntentTargetPolicy.SnapshotSlot, false)]
    public void DelayedStatus_RevalidatesLiveTargetAndPolicy(BattleIntentTargetPolicy policy,
        bool applies)
    {
        BattleMove delayed = Move("delayed_status", DamageClass.Status, null, MoveTarget.Selected,
            [new DelayedStatusEffect(1, PersistentStatus.Sleep, policy)]);
        BattleCreature source = Creature("source", 100, delayed);
        BattleCreature original = Creature("original", 1, Inert());
        BattleCreature replacement = Creature("replacement", 1, Inert());
        var battle = new BattleController([source], [original, replacement], Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Pass(), new Switch(1));

        Assert.Equal(applies ? PersistentStatus.Sleep : null, replacement.Status);
        Assert.Equal(!applies, due.Any(e => e is DelayedActionFailed
            { Reason: DelayedActionFailureReason.TargetUnavailable }));
    }

    [Fact]
    public void DelayedStatus_RespectsExecutionTimeTypeImmunity()
    {
        BattleMove delayed = Move("delayed_status", DamageClass.Status, null, MoveTarget.Selected,
            [new DelayedStatusEffect(1, PersistentStatus.Burn)]);
        BattleCreature target = Creature("target", 1, [EntityId.Parse("type:fire")], 100, 100, Inert());
        var battle = new BattleController(Creature("source", 100, delayed), target,
            new TypeChart([new TypeDef { Id = Normal }, new TypeDef { Id = EntityId.Parse("type:fire") }]),
            new Rng(1));
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Pass(), new Pass());

        Assert.Null(target.Status);
        Assert.Contains(due, e => e is DelayedActionFailed
            { Reason: DelayedActionFailureReason.StatusBlocked });
    }

    [Fact]
    public void ReplacementRestore_RunsAfterHazardsAndRestoresHpStatusAndPp()
    {
        BattleMove restore = ReplacementMove(restorePp: true);
        BattleCreature source = Creature("source", 100, restore);
        BattleMove reserveMove = Move("reserve_move");
        reserveMove.UsePp();
        BattleCreature reserve = Creature("reserve", 50, reserveMove);
        reserve.TakeDamage(500);
        reserve.SetStatus(PersistentStatus.Poison);
        BattleMove hazard = new(EntityId.Parse("move:hazard"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.OpponentsField,
            secondaryEffects: [new SetEntryHazardEffect(EntryHazardConditions.LegacyLayeredDamage)]);
        var battle = new BattleController([source, reserve], [Creature("enemy", 1, hazard)],
            Chart(), new Rng(1));
        battle.ResolveTurn(new Pass(), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> entry = battle.ResolveReplacements(
            [new BattleReplacementSelection(new(BattleSide.Player, 0), 1)]);

        Assert.Equal(reserve.MaxHp, reserve.CurrentHp);
        Assert.Null(reserve.Status);
        Assert.Equal(reserveMove.MaxPp, reserveMove.Pp);
        Assert.True(Enumerable.Range(0, entry.Count).First(index => entry[index] is EntryHazardTriggered)
            < Enumerable.Range(0, entry.Count).First(index => entry[index] is DelayedActionResolved));
        Assert.Empty(battle.IntentQueueSnapshot);
    }

    [Fact]
    public void ReplacementRestore_DefersPastHazardKoToNextReplacement()
    {
        BattleMove restore = ReplacementMove();
        BattleCreature source = Creature("source", 100, restore);
        BattleCreature first = Creature("first", 50, Inert());
        first.TakeDamage(first.MaxHp - 1);
        BattleCreature second = Creature("second", 40, Inert());
        second.TakeDamage(400);
        BattleMove hazard = new(EntityId.Parse("move:hazard"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.OpponentsField,
            secondaryEffects: [new SetEntryHazardEffect(EntryHazardConditions.LegacyLayeredDamage)]);
        var battle = new BattleController([source, first, second], [Creature("enemy", 1, hazard)],
            Chart(), new Rng(1));
        battle.ResolveTurn(new Pass(), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveReplacements([new(new(BattleSide.Player, 0), 1)]);
        Assert.Single(battle.IntentQueueSnapshot);

        IReadOnlyList<BattleEvent> entry = battle.ResolveReplacements([new(new(BattleSide.Player, 0), 2)]);

        Assert.Equal(second.MaxHp, second.CurrentHp);
        Assert.Contains(entry, e => e is DelayedActionResolved);
        Assert.Empty(battle.IntentQueueSnapshot);
    }

    [Fact]
    public void ReplacementRestore_WithoutReserveFailsBeforeSelfDestruct()
    {
        BattleCreature source = Creature("source", 100, ReplacementMove());
        var battle = Battle(source, Creature("enemy", 1, Inert()));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.False(source.IsFainted);
        Assert.Contains(events, e => e is DelayedActionFailed
            { Reason: DelayedActionFailureReason.NoReserve });
        Assert.Empty(battle.IntentQueueSnapshot);
    }

    [Fact]
    public void SameSlotDelayedIntents_ResolveInInsertionOrder()
    {
        BattleMove first = Move("first", DamageClass.Status, null, MoveTarget.Selected,
            [new DelayedHealEffect(1, new Fraction(1, 4))]);
        BattleMove second = Move("second", DamageClass.Status, null, MoveTarget.Selected,
            [new DelayedHealEffect(1, new Fraction(1, 4))]);
        BattleCreature player0 = Creature("player0", 100, first);
        BattleCreature player1 = Creature("player1", 90, Inert());
        player1.TakeDamage(800);
        BattleCreature enemy0 = Creature("enemy0", 80, second);
        var battle = new BattleController([player0, player1],
            [enemy0, Creature("enemy1", 1, Inert())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(1));
        BattleSlot target = new(BattleSide.Player, 1);
        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(target)),
            new(new(BattleSide.Player, 1), new Pass()),
            new(new(BattleSide.Enemy, 0), new UseMove(0), new ActiveSlotSelection(target)),
            new(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(Passes());
        EntityId[] resolved = due.OfType<DelayedActionResolved>().Select(e => e.Move).ToArray();

        Assert.Equal([first.Move, second.Move], resolved);
        Assert.Equal(700, player1.CurrentHp);
    }

    [Fact]
    public void DelayedDamageLifecycle_MatchesGolden()
    {
        BattleMove delayed = Move("golden_delayed", DamageClass.Special, 80,
            MoveTarget.Selected, [new DelayedDamageEffect(2)]);
        BattleCreature target = Creature("target", 1, Inert());
        var battle = Battle(Creature("source", 100, delayed), target);

        IReadOnlyList<BattleEvent> first = battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new Pass(), new Pass());
        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Pass(), new Pass());
        string actual = string.Join('\n',
        [
            .. first.Concat(due).Where(e => e is DelayedActionQueued or DamageDealt or DelayedActionResolved)
                .Select(e => $"event:{e.GetType().Name}"),
            .. battle.Trace.Where(entry => entry.Kind is EffectTraceKind.IntentEnqueued
                    or EffectTraceKind.IntentConsumed or EffectTraceKind.DelayedAction
                    or EffectTraceKind.DamageRoll)
                .Select(entry => $"trace:{entry.Kind}:{entry.IntentPayload?.ToString() ?? "-"}:{entry.Value}"),
            $"pp:{delayed.Pp}",
            $"targetHp:{target.CurrentHp}",
        ]);

        Assert.Equal(Golden("delayed-action"), actual);
    }

    private static int DamageAfterSourceAndTargetSwitch(int replacementSpd, int reserveSpa)
    {
        BattleMove delayed = Move("delayed", DamageClass.Special, 80, MoveTarget.Selected,
            [new DelayedDamageEffect(2)]);
        BattleCreature source = Creature("source", 100, [Normal], 100, 300, delayed);
        BattleCreature sourceReserve = Creature("source_reserve", 50, [Normal], 100, reserveSpa, Inert());
        BattleCreature original = Creature("original", 1, Inert());
        BattleCreature replacement = Creature("replacement", 1, [Normal], 100, replacementSpd, Inert());
        var battle = new BattleController([source, sourceReserve], [original, replacement], Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new Switch(1), new Switch(1));
        int before = replacement.CurrentHp;
        battle.ResolveTurn(new Pass(), new Pass());
        return before - replacement.CurrentHp;
    }

    private static BattleMove ReplacementMove(bool restorePp = false) =>
        new(EntityId.Parse("move:replacement_restore"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, selfDestruct: true, target: MoveTarget.User,
            secondaryEffects:
            [
                new ReplacementRestoreEffect(RestorePp: restorePp),
                new SelfDestructEffect(),
            ]);

    private static BattleTurnActions Passes() => new(BattleTopology.Doubles,
    [
        new(new(BattleSide.Player, 0), new Pass()),
        new(new(BattleSide.Player, 1), new Pass()),
        new(new(BattleSide.Enemy, 0), new Pass()),
        new(new(BattleSide.Enemy, 1), new Pass()),
    ]);

    private static BattleController Battle(BattleCreature player, BattleCreature enemy,
        TypeChart? chart = null, IRng? rng = null) => new(player, enemy, chart ?? Chart(), rng ?? new Rng(1));

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) =>
        Creature(slug, speed, [Normal], 100, 100, moves);

    private static BattleCreature Creature(string slug, int speed, IReadOnlyList<EntityId> types,
        int defense, int special, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, types,
            new Stats(1000, 100, defense, special, special, speed), moves);

    private static BattleMove Inert() => new(EntityId.Parse("move:inert"), Normal,
        DamageClass.Status, null, null, 20, 0, 0);

    private static BattleMove Move(string slug, DamageClass damageClass = DamageClass.Physical,
        int? power = 40, MoveTarget target = MoveTarget.Selected,
        IReadOnlyList<MoveEffect>? effects = null) =>
        new(EntityId.Parse($"move:{slug}"), Normal, damageClass, power,
            damageClass == DamageClass.Status ? null : 100, 10, 0, 0,
            target: target, secondaryEffects: effects);

    private static BattleMove Compile(DamageClass damageClass, int? power, MoveTarget target,
        params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse("move:compiled_delayed"),
            Name = "Compiled Delayed",
            Type = Normal,
            DamageClass = damageClass,
            Power = power,
            Accuracy = damageClass == DamageClass.Status ? null : 100,
            Pp = 10,
            Target = target,
            Effects = effects,
        });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.Length == 0 ? null : values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden")).TrimEnd();

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public double NextDouble() { Calls++; return 0; }
    }
}
