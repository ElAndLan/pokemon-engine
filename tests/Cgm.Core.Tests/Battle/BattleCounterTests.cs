using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Counter/Mirror Coat (deal_damage counter_received_damage, catalog §9.2) + accuracyBypass.</summary>
public sealed class BattleCounterTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove PhysHit() =>
        new(EntityId.Parse("move:hit"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0);

    private static BattleMove SpecHit() =>
        new(EntityId.Parse("move:blast"), Normal, DamageClass.Special, 60, 100, 25, 0, 0);

    // Counter: -5 priority so it resolves after taking the hit.
    private static BattleMove Counter() =>
        new(EntityId.Parse("move:counter"), Normal, DamageClass.Physical, null, null, 25, priority: -5, 0,
            counterCategory: DamageClass.Physical);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleCreature Fast(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void Counter_Returns2xPhysicalDamageTaken()
    {
        var player = Slow(400, Counter());   // slower + -5 priority → acts last
        var enemy = Fast(2000, PhysHit());
        var battle = new BattleController(player, enemy, Chart(), new Rng(3));

        int before = enemy.CurrentHp;
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        int dealtToPlayer = player.MaxHp - player.CurrentHp;
        int dealtToEnemy = before - enemy.CurrentHp;
        Assert.True(dealtToPlayer > 0);
        Assert.Equal(dealtToPlayer * 2, dealtToEnemy); // countered for double
    }

    [Fact]
    public void Counter_IgnoresSpecialDamage()
    {
        // Physical Counter shouldn't reflect a special hit.
        var player = Slow(400, Counter());
        var enemy = Fast(2000, SpecHit());
        var battle = new BattleController(player, enemy, Chart(), new Rng(3));

        int before = enemy.CurrentHp;
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(before, enemy.CurrentHp); // nothing physical to counter → fizzles
    }

    [Fact]
    public void Counter_ResetsBetweenTurns()
    {
        var player = Slow(2000, Counter(), Inert());
        var enemy = Fast(4000, PhysHit(), Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(3));

        battle.ResolveTurn(new UseMove(1), new UseMove(0)); // player inert, takes a hit (turn 1)
        int before = enemy.CurrentHp;
        battle.ResolveTurn(new UseMove(0), new UseMove(1)); // player counters, enemy inert (turn 2, no new damage)

        Assert.Equal(before, enemy.CurrentHp); // last turn's damage doesn't carry — counter fizzles
    }

    [Fact]
    public void AccuracyBypass_AlwaysHits()
    {
        var sureHit = new BattleMove(EntityId.Parse("move:swift"), Normal, DamageClass.Special, 60, 1, 25, 0, 0,
            bypassAccuracy: true); // accuracy 1 would almost always miss, but bypass forces a hit
        var player = Fast(300, sureHit);
        var enemy = Slow(300, Inert());

        // FakeRng gives a high accuracy roll (would miss) — bypass must skip it entirely.
        var events = new BattleController(player, enemy, Chart(), new FakeRng(ints: [15], doubles: [0.99]))
            .ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.DoesNotContain(events, e => e is MoveMissed);
        Assert.True(enemy.CurrentHp < 300);
    }
}
