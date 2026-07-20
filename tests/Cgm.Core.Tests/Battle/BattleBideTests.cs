using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15G-3 cross-turn damage-memory consumer (Bide): store damage over N locked turns, then
/// unleash twice the stored total at the opponent; fizzle if nothing was stored.</summary>
public sealed class BattleBideTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void StoresTwoTurnsThenUnleashesDoubleTheStoredDamage()
    {
        BattleCreature bider = Bider(3000);
        var battle = new BattleController(bider, Striker(999999), Chart(), new Rng(7));

        IReadOnlyList<BattleEvent> t1 = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int h1 = t1.OfType<DamageDealt>().Single(e => e.Target == BattleSide.Player).Amount;
        Assert.True(bider.IsBiding);                          // storing, did not attack
        Assert.DoesNotContain(t1, e => e is DamageDealt { Target: BattleSide.Enemy });

        IReadOnlyList<BattleEvent> t2 = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int h2 = t2.OfType<DamageDealt>().Single(e => e.Target == BattleSide.Player).Amount;
        Assert.True(bider.IsBiding);

        IReadOnlyList<BattleEvent> t3 = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int returned = t3.OfType<DamageDealt>().Single(e => e.Target == BattleSide.Enemy).Amount;
        Assert.Equal((h1 + h2) * 2, returned);               // player is fast → unleashes before turn-3 hit
        Assert.False(bider.IsBiding);                         // state cleared after unleash
        Assert.Contains(t3, e => e is BideUnleashed);
    }

    [Fact]
    public void FizzlesWhenNoDamageWasStored()
    {
        BattleCreature bider = Bider(3000);
        var battle = new BattleController(bider, Idler(3000), Chart(), new Rng(3));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // store (no damage taken)
        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // store
        IReadOnlyList<BattleEvent> t3 = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.DoesNotContain(t3, e => e is DamageDealt { Target: BattleSide.Enemy });
        Assert.Contains(t3, e => e is MoveMissed);
        Assert.False(bider.IsBiding);
    }

    [Fact]
    public void ForcesBideEvenWhenAnotherMoveIsSubmitted()
    {
        var player = new BattleCreature(EntityId.Parse("species:p"), "P", 50, [Normal],
            new Stats(3000, 100, 100, 100, 100, 100), [Bide(), Inert()]);
        var battle = new BattleController(player, Striker(999999), Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // bide starts
        int inertPp = player.Moves[1].Pp;
        battle.ResolveTurn(new UseMove(1), new UseMove(0)); // submit inert — bide is forced instead

        Assert.True(player.IsBiding);
        Assert.Equal(inertPp, player.Moves[1].Pp);          // inert never fired
    }

    [Fact]
    public void ReplacesSubmittedSwitchWithForcedBide()
    {
        var player = new BattleCreature(EntityId.Parse("species:p"), "P", 50, [Normal],
            new Stats(3000, 100, 100, 100, 100, 100), [Bide()]);
        var bench = new BattleCreature(EntityId.Parse("species:b"), "B", 50, [Normal],
            new Stats(3000, 100, 100, 100, 100, 100), [Inert()]);
        var battle = new BattleController([player, bench], [Striker(999999)], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));  // bide starts
        battle.ResolveTurn(new Switch(1), new UseMove(0));   // submit switch — bide is forced instead

        Assert.True(player.IsBiding);
        Assert.Same(player, battle.Active(BattleSide.Player)); // never left the field
    }

    private static BattleMove Bide() =>
        new(EntityId.Parse("move:bide"), Normal, DamageClass.Physical, null, null, 10, 1, 0,
            target: MoveTarget.Selected, secondaryEffects: [new BideEffect(2)]);

    private static BattleMove Strike() =>
        new(EntityId.Parse("move:strike"), Normal, DamageClass.Physical, 40, null, 30, 0, 0,
            target: MoveTarget.Selected);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    // Bider is fast (Spe 100) so on the unleash turn it acts before taking that turn's hit.
    private static BattleCreature Bider(int hp) => new(
        EntityId.Parse("species:bider"), "Bider", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), [Bide()]);

    private static BattleCreature Striker(int hp) => new(
        EntityId.Parse("species:striker"), "Striker", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), [Strike()]);

    private static BattleCreature Idler(int hp) => new(
        EntityId.Parse("species:idler"), "Idler", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), [Inert()]);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
}
