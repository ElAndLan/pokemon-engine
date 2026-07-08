using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>forceSwitchOut (Roar/Whirlwind): drags a trainer's target out to a random reserve, or ends
/// a wild battle (catalog §9.6 switch_flow).</summary>
public sealed class BattleForceSwitchTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    // Roar: negative priority, forces the target out.
    private static BattleMove Roar() =>
        new(EntityId.Parse("move:roar"), Normal, DamageClass.Status, null, null, 25, priority: -6, 0, forcesSwitch: true);

    private static BattleCreature Fast(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void Roar_ForcesTrainerTargetToReserve()
    {
        var player = Fast(300, Roar());
        var enemyA = Slow(300, Inert());
        var enemyB = Slow(300, Inert());
        var battle = new BattleController([player], [enemyA, enemyB], Chart(), new Rng(1));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Same(enemyB, battle.Active(BattleSide.Enemy)); // dragged out to the only reserve
        Assert.Contains(events, e => e is ForcedOut { Side: BattleSide.Enemy });
    }

    [Fact]
    public void Roar_ClearsForcedCreaturesVolatiles()
    {
        var player = Fast(300, Roar());
        var enemyA = Slow(300, Inert());
        enemyA.SetConfusion(3); // enemyA is confused; being roared out clears it
        var enemyB = Slow(300, Inert());
        var battle = new BattleController([player], [enemyA, enemyB], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.False(enemyA.IsConfused);
    }

    [Fact]
    public void Roar_NoReserves_HasNoEffect()
    {
        var player = Fast(300, Roar());
        var enemy = Slow(300, Inert()); // only one creature
        var battle = new BattleController([player], [enemy], Chart(), new Rng(1));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Same(enemy, battle.Active(BattleSide.Enemy));
        Assert.DoesNotContain(events, e => e is ForcedOut);
        Assert.Null(battle.Outcome); // battle continues
    }

    [Fact]
    public void Roar_EndsWildBattle()
    {
        var player = Fast(300, Roar());
        var wild = Slow(300, Inert());
        var battle = new BattleController(player, wild, Chart(), new Rng(1), isWild: true);

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.NotNull(battle.Outcome);
        Assert.Equal(BattleSide.Player, battle.Outcome!.Winner); // wild fled
        Assert.Contains(events, e => e is BattleEnded { Winner: BattleSide.Player });
    }
}
