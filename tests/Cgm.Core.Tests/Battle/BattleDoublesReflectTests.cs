using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15G-3 doubles source-addressed reflection: Counter/Mirror Coat/revenge return damage to the
/// exact attacker from damage memory, not the opposing slot 0.</summary>
public sealed class BattleDoublesReflectTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void Counter_ReflectsToTheSpecificAttacker_NotOpposingSlotZero()
    {
        // Only enemy slot 1 hits the Counter user; Counter must strike slot 1, leaving slot 0 untouched.
        BattleCreature counterer = Slow(Counter());
        BattleCreature ally = Slow(Wait());
        BattleCreature enemy0 = Fast(Strike());
        BattleCreature enemy1 = Fast(Strike());
        var battle = new BattleController([counterer, ally], [enemy0, enemy1], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new Rng(5));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),          // Counter
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),               // slot 0 idle
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new UseMove(0),
                new ActiveSlotSelection(new BattleSlot(BattleSide.Player, 0))),             // slot 1 hits counterer
        ]));

        int hitOnCounterer = events.OfType<DamageDealt>()
            .Single(e => e.Slot == new BattleSlot(BattleSide.Player, 0)).Amount;
        Assert.Equal(enemy1.MaxHp - hitOnCounterer * 2, enemy1.CurrentHp); // reflected to the attacker (slot 1)
        Assert.Equal(enemy0.MaxHp, enemy0.CurrentHp);                      // the idle foe is untouched
    }

    [Fact]
    public void MetalBurst_ReflectsToTheSpecificAttacker_AnyClass()
    {
        BattleCreature user = Slow(MetalBurst());
        BattleCreature ally = Slow(Wait());
        BattleCreature enemy0 = Fast(Strike());
        BattleCreature enemy1 = Fast(Blast()); // special hit — revenge reflects any class
        var battle = new BattleController([user, ally], [enemy0, enemy1], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new Rng(9));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new UseMove(0),
                new ActiveSlotSelection(new BattleSlot(BattleSide.Player, 0))),
        ]));

        int hitOnUser = events.OfType<DamageDealt>()
            .Single(e => e.Slot == new BattleSlot(BattleSide.Player, 0)).Amount;
        Assert.Equal(enemy1.MaxHp - hitOnUser * 3 / 2, enemy1.CurrentHp);
        Assert.Equal(enemy0.MaxHp, enemy0.CurrentHp);
    }

    [Fact]
    public void Counter_FizzlesWhenTheLastHitIsTheWrongCategory()
    {
        BattleCreature counterer = Slow(Counter());       // physical Counter
        BattleCreature ally = Slow(Wait());
        BattleCreature enemy0 = Fast(Strike());
        BattleCreature enemy1 = Fast(Blast());            // special hit — nothing physical to counter
        var battle = new BattleController([counterer, ally], [enemy0, enemy1], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new Rng(5));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new UseMove(0),
                new ActiveSlotSelection(new BattleSlot(BattleSide.Player, 0))),
        ]));

        Assert.Equal(enemy0.MaxHp, enemy0.CurrentHp);
        Assert.Equal(enemy1.MaxHp, enemy1.CurrentHp);
        Assert.Contains(events, e => e is MoveMissed { Slot: { Side: BattleSide.Player, Position: 0 } });
    }

    // target: User so the move validates in doubles without a selection — the reflect addresses the
    // attacker from damage memory and ignores the declared target regardless.
    [Fact]
    public void Counter_FizzlesGracefullyWhenOpposingSlotZeroIsEmpty()
    {
        // The ally KOs foe slot 0 first, emptying it; the Counter user then fizzles (took no hit).
        // Recording the fizzle must not touch the now-empty slot 0.
        BattleCreature counterer = Slow(Counter());
        BattleCreature ally = Fast(Strike());          // acts first, KOs enemy0
        BattleCreature enemy0 = Frail(Wait());         // 1 HP → faints to the ally's strike
        BattleCreature enemy1 = Fast(Wait());          // never attacks → nothing to counter
        var battle = new BattleController([counterer, ally], [enemy0, enemy1], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new Rng(5));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),               // Counter (slow)
            new BattleActionSubmission(new(BattleSide.Player, 1), new UseMove(0),
                new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0))),                    // ally KOs enemy0
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.True(enemy0.IsFainted);
        Assert.Contains(events, e => e is MoveMissed { Slot: { Side: BattleSide.Player, Position: 0 } });
    }

    private static BattleMove Counter() =>
        new(EntityId.Parse("move:counter"), Normal, DamageClass.Physical, null, null, 25, priority: -5, 0,
            counterCategory: DamageClass.Physical, target: MoveTarget.User);

    private static BattleMove MetalBurst() =>
        new(EntityId.Parse("move:metal_burst"), Normal, DamageClass.Physical, null, null, 10, 0, 0,
            target: MoveTarget.User, secondaryEffects: [new RevengeDamageEffect(new Fraction(3, 2))]);

    private static BattleMove Strike() =>
        new(EntityId.Parse("move:strike"), Normal, DamageClass.Physical, 40, null, 30, 0, 0,
            target: MoveTarget.Selected);

    private static BattleMove Blast() =>
        new(EntityId.Parse("move:blast"), Normal, DamageClass.Special, 40, null, 30, 0, 0,
            target: MoveTarget.Selected);

    private static BattleMove Wait() =>
        new(EntityId.Parse("move:wait"), Normal, DamageClass.Status, null, null, 10, 0, 0);

    private static BattleCreature Slow(BattleMove move) => new(
        EntityId.Parse("species:slow"), "slow", 50, [Normal], new Stats(4000, 100, 100, 100, 100, 1), [move]);

    private static BattleCreature Fast(BattleMove move) => new(
        EntityId.Parse("species:fast"), "fast", 50, [Normal], new Stats(4000, 100, 100, 100, 100, 100), [move]);

    private static BattleCreature Frail(BattleMove move) => new(
        EntityId.Parse("species:frail"), "frail", 50, [Normal], new Stats(1, 100, 100, 100, 100, 50), [move]);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
}
