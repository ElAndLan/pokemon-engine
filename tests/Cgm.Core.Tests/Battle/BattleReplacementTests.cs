using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleReplacementTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly BattleSlot Enemy0 = new(BattleSide.Enemy, 0);
    private static readonly BattleSlot Enemy1 = new(BattleSide.Enemy, 1);

    [Fact]
    public void RequestsEveryFillableSlotAndAppliesAtomicChoicesInTopologyOrder()
    {
        var rng = new CountingRng([15, 15], [0.99, 0.99]);
        BattleController battle = ReplacementBattle(enemyReserveCount: 2, rng);
        IReadOnlyList<BattleEvent> turnEvents = KnockOutBothEnemySlots(battle);

        Assert.Equal([Enemy0, Enemy1], battle.PendingReplacementSlots);
        Assert.Equal([Enemy0, Enemy1], turnEvents.OfType<ReplacementRequested>().Select(item => item.Slot));
        Assert.False(battle.CanSubmitAction(new BattleSlot(BattleSide.Player, 0), new Pass()));
        Assert.Throws<InvalidOperationException>(() => battle.ResolveTurn(LivingPasses(battle)));

        int logCount = battle.Log.Count;
        int rngCalls = rng.Calls;
        int turn = battle.Turn;
        int playerPp = battle.Active(new BattleSlot(BattleSide.Player, 0)).Moves[0].Pp;
        BattleReplacementSelection[][] invalidChoices =
        [
            [new(Enemy0, 2)],
            [new(Enemy0, 2), new(Enemy1, 2)],
            [new(Enemy0, 0), new(Enemy1, 3)],
            [new(Enemy0, 99), new(Enemy1, 3)],
            [new(Enemy0, 4), new(Enemy1, 3)],
        ];
        foreach (BattleReplacementSelection[] choices in invalidChoices)
            Assert.Throws<ArgumentException>(() => battle.ResolveReplacements(choices));
        Assert.Equal(logCount, battle.Log.Count);
        Assert.Equal(0, battle.ActiveIndex(Enemy0));
        Assert.Equal(1, battle.ActiveIndex(Enemy1));
        Assert.Equal([Enemy0, Enemy1], battle.PendingReplacementSlots);
        Assert.Equal(rngCalls, rng.Calls);
        Assert.Equal(turn, battle.Turn);
        Assert.Equal(playerPp, battle.Active(new BattleSlot(BattleSide.Player, 0)).Moves[0].Pp);

        IReadOnlyList<BattleEvent> replacementEvents = battle.ResolveReplacements(
        [
            new(Enemy1, 3),
            new(Enemy0, 2),
        ]);

        Assert.Equal([Enemy0, Enemy1], replacementEvents.OfType<SwitchedIn>().Select(item => item.Slot));
        Assert.Equal([2, 3], replacementEvents.OfType<SwitchedIn>().Select(item => item.PartyIndex));
        Assert.Empty(battle.PendingReplacementSlots);
    }

    [Fact]
    public void LeavesUnfillableSlotEmptyAndAllowsOnlyLivingSlotsToAct()
    {
        BattleController battle = ReplacementBattle(enemyReserveCount: 1);
        KnockOutBothEnemySlots(battle);

        Assert.Equal([Enemy0], battle.PendingReplacementSlots);
        Assert.Throws<ArgumentException>(() => battle.ResolveReplacements([new(Enemy0, 1)]));
        battle.ResolveReplacements([new(Enemy0, 2)]);

        Assert.Empty(battle.PendingReplacementSlots);
        Assert.False(battle.Active(Enemy0).IsFainted);
        Assert.True(battle.Active(Enemy1).IsFainted);

        battle.ResolveTurn(LivingPasses(battle));

        Assert.Null(battle.Outcome);
    }

    [Fact]
    public void RequestsBothSurvivingSidesInGlobalTopologyOrder()
    {
        var battle = new BattleController(
            [Creature("p0", 100, 10, Wait()), Creature("p1", 100, 10, Wait()), Creature("p2", 100, 10, Wait())],
            [Creature("e0", 100, 10, Wait()), Creature("e1", 100, 10, Wait()), Creature("e2", 100, 10, Wait())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng());
        battle.Active(new BattleSlot(BattleSide.Player, 0)).TakeDamage(100);
        battle.Active(new BattleSlot(BattleSide.Enemy, 0)).TakeDamage(100);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(LivingPasses(battle));

        Assert.Equal(
        [
            new BattleSlot(BattleSide.Player, 0),
            new BattleSlot(BattleSide.Enemy, 0),
        ], events.OfType<ReplacementRequested>().Select(item => item.Slot));
    }

    [Fact]
    public void FaintedActorInvalidatesItsCapturedActionBeforeReplacement()
    {
        BattleSlot player0 = new(BattleSide.Player, 0);
        BattleCreature enemy0 = Creature("e0", 1, 50, Hit());
        var battle = new BattleController(
            [Creature("p0", 200, 200, Hit()), Creature("p1", 200, 10, Wait())],
            [enemy0, Creature("e1", 200, 10, Wait()), Creature("reserve", 200, 10, Wait())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(),
            new FakeRng(ints: [15], doubles: [0.99]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(player0, new UseMove(0), new ActiveSlotSelection(Enemy0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(Enemy0, new UseMove(0), new ActiveSlotSelection(player0)),
            new BattleActionSubmission(Enemy1, new Pass()),
        ]));

        Assert.Contains(events, item => item is ActionInvalidated
        {
            Slot: { Side: BattleSide.Enemy, Position: 0 },
            Reason: ActionInvalidationReason.ActorFainted,
        });
        Assert.DoesNotContain(events, item => item is MoveUsed { Slot: { Side: BattleSide.Enemy, Position: 0 } });
        Assert.Equal([Enemy0], battle.PendingReplacementSlots);
    }

    [Fact]
    public void EntryHazardFaintRequestsTheSameSlotAgainAfterTheHookBatch()
    {
        BattleMove spikes = new(EntityId.Parse("move:hazard"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.OpponentsField,
            secondaryEffects: [new SetEntryHazardEffect(EntryHazardConditions.LegacyLayeredDamage)]);
        BattleMove hit = new(EntityId.Parse("move:hit"), Normal, DamageClass.Special,
            300, null, 10, 0, 0);
        BattleCreature player = Creature("player", 400, 200, spikes, hit);
        BattleCreature first = Creature("first", 1, 1, Wait());
        BattleCreature frailReserve = Creature("frail", 1, 1, Wait());
        BattleCreature healthyReserve = Creature("healthy", 400, 1, Wait());
        var battle = new BattleController([player], [first, frailReserve, healthyReserve], Chart(),
            new FakeRng(ints: [15], doubles: [0.99]));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        BattleSlot slot = new(BattleSide.Enemy, 0);
        IReadOnlyList<BattleEvent> firstEntry = battle.ResolveReplacements([new(slot, 1)]);

        Assert.Equal(
        [
            typeof(SwitchedIn),
            typeof(EntryHazardTriggered),
            typeof(Fainted),
            typeof(ReplacementRequested),
        ], firstEntry.Select(item => item.GetType()));
        Assert.Equal(slot, Assert.Single(firstEntry.OfType<Fainted>()).Slot);
        Assert.Equal([slot], battle.PendingReplacementSlots);

        IReadOnlyList<BattleEvent> secondEntry = battle.ResolveReplacements([new(slot, 2)]);

        Assert.Single(secondEntry.OfType<SwitchedIn>());
        Assert.Single(secondEntry.OfType<EntryHazardTriggered>());
        Assert.Empty(secondEntry.OfType<ReplacementRequested>());
        Assert.Equal(350, healthyReserve.CurrentHp);
        Assert.Empty(battle.PendingReplacementSlots);
    }

    private static BattleController ReplacementBattle(int enemyReserveCount, IRng? rng = null)
    {
        BattleCreature faintedReserve = Creature("fainted_reserve", 200, 1, Wait());
        faintedReserve.TakeDamage(faintedReserve.MaxHp);
        BattleCreature[] enemies =
        [
            Creature("e0", 1, 1, Wait()),
            Creature("e1", 1, 1, Wait()),
            .. Enumerable.Range(0, enemyReserveCount).Select(index => Creature($"reserve{index}", 200, 1, Wait())),
            faintedReserve,
        ];
        return new BattleController(
            [Creature("p0", 200, 200, Hit()), Creature("p1", 200, 150, Hit())], enemies,
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(),
            rng ?? new FakeRng(ints: [15, 15], doubles: [0.99, 0.99]));
    }

    private static IReadOnlyList<BattleEvent> KnockOutBothEnemySlots(BattleController battle) => battle.ResolveTurn(
        new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(Enemy0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new UseMove(0), new ActiveSlotSelection(Enemy1)),
            new BattleActionSubmission(Enemy0, new Pass()),
            new BattleActionSubmission(Enemy1, new Pass()),
        ]));

    private static BattleTurnActions LivingPasses(BattleController battle) => new(BattleTopology.Doubles,
        BattleTopology.Doubles.Slots
            .Where(slot => !battle.Active(slot).IsFainted)
            .Select(slot => new BattleActionSubmission(slot, new Pass()))
            .ToArray());

    private static BattleCreature Creature(string slug, int hp, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal], new Stats(hp, 100, 100, 100, 100, speed), moves);

    private static BattleMove Hit() => new(EntityId.Parse("move:hit"), Normal, DamageClass.Special,
        300, null, 10, 0, 0, target: MoveTarget.Selected);

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Normal, DamageClass.Status,
        null, null, 10, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private sealed class CountingRng(IEnumerable<int> ints, IEnumerable<double> doubles) : IRng
    {
        private readonly Queue<int> _ints = new(ints);
        private readonly Queue<double> _doubles = new(doubles);
        public int Calls { get; private set; }

        public int Next(int maxExclusive)
        {
            Calls++;
            return _ints.Dequeue();
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            Calls++;
            return _ints.Dequeue();
        }

        public double NextDouble()
        {
            Calls++;
            return _doubles.Dequeue();
        }
    }
}
