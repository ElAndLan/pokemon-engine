using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Entry hazards as a side condition with an on_switch_in hook (catalog §7.3).</summary>
public sealed class BattleHazardTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleMove Spikes() =>
        new(EntityId.Parse("move:spikes"), Normal, DamageClass.Status, null, null, 25, 0, 0, setsSpikes: true);

    private static BattleCreature Fast(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void Spikes_SetsHazardOnOpponentSide()
    {
        var player = Fast(200, Spikes());
        var enemy = Slow(200, Inert());
        var events = new BattleController([player], [enemy, Slow(200, Inert())], Chart(), new Rng(1))
            .ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(events, e => e is HazardSet { Side: BattleSide.Enemy, Layers: 1 });
    }

    [Fact]
    public void SwitchingIn_TakesHazardDamage()
    {
        var player = Fast(400, Spikes());
        var enemyA = Slow(400, Inert());
        var enemyB = Slow(400, Inert());
        var battle = new BattleController([player], [enemyA, enemyB], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // player sets spikes on enemy side
        Assert.Equal(400, enemyB.CurrentHp);                // hasn't entered yet

        var events = battle.ResolveTurn(new UseMove(0), new Switch(1)); // enemy switches to B
        Assert.Equal(400 - 400 / 8, enemyB.CurrentHp);      // 1 layer → 1/8 on entry
        Assert.Contains(events, e => e is HurtByHazard { Side: BattleSide.Enemy });
    }

    [Fact]
    public void HazardLayers_StackAndScaleDamage()
    {
        var player = Fast(400, Spikes());
        var enemyA = Slow(400, Spikes());
        var enemyB = Slow(400, Inert());
        var battle = new BattleController([player], [enemyA, enemyB], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // player spikes (layer 1 on enemy)
        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // player spikes again (layer 2 on enemy)
        battle.ResolveTurn(new UseMove(0), new Switch(1));  // enemy switches to B → 2 layers = 1/6

        Assert.Equal(400 - 400 / 6, enemyB.CurrentHp);
    }

    [Fact]
    public void InitialActive_TakesNoHazardDamage()
    {
        // No hazards exist at battle start, so the opening active is untouched.
        var player = Fast(200, Inert());
        var enemy = Slow(200, Inert());
        new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(200, enemy.CurrentHp);
    }

    private static readonly EntityId Fire = EntityId.Parse("type:fire");

    // Rock is 2× super-effective vs Fire, ½× vs a resistant type; neutral otherwise.
    private static TypeChart RockChart() => new(
    [
        new TypeDef { Id = EntityId.Parse("type:rock"), DoubleDamageTo = [Fire] },
        new TypeDef { Id = Normal }, new TypeDef { Id = Fire },
    ]);

    private static BattleMove StealthRock() =>
        new(EntityId.Parse("move:stealthrock"), Normal, DamageClass.Status, null, null, 25, 0, 0, setsStealthRock: true);

    [Fact]
    public void StealthRock_ScalesDamageByType()
    {
        // Neutral (Normal) target: 1/8. Fire target (2× to rock): 1/4.
        var player = Fast(400, StealthRock());
        var normalIn = Slow(400, Inert());
        var fireIn = new BattleCreature(EntityId.Parse("species:fire"), "Fi", 50, [Fire],
            new Stats(400, 100, 100, 100, 100, 1), [Inert()]);
        var battle = new BattleController([player], [normalIn, fireIn], RockChart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // rocks on enemy side
        battle.ResolveTurn(new UseMove(0), new Switch(1));  // fire creature switches in

        Assert.Equal(400 - 400 / 4, fireIn.CurrentHp); // 1/8 × 2 (super-effective) = 1/4
    }

    [Fact]
    public void StealthRock_NeutralTarget_TakesEighth()
    {
        var player = Fast(400, StealthRock());
        var enemyA = Slow(400, Inert());
        var enemyB = Slow(400, Inert()); // Normal → neutral to rock
        var battle = new BattleController([player], [enemyA, enemyB], RockChart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(0), new Switch(1));

        Assert.Equal(400 - 400 / 8, enemyB.CurrentHp);
    }

    [Fact]
    public void FaintReplacement_TakesHazardDamage()
    {
        // Enemy A faints; the selected replacement B walks into the spikes.
        var enemyA = Slow(1, Inert());
        var enemyB = Slow(400, Inert());
        var player = Fast(400, Spikes(), new BattleMove(EntityId.Parse("move:big"), Normal, DamageClass.Physical, 250, 100, 25, 0, 0));
        var battle = new BattleController([player], [enemyA, enemyB], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // spikes on enemy side
        battle.ResolveTurn(new UseMove(1), new UseMove(0)); // KO enemy A and request a replacement

        battle.ResolveReplacements([new(new BattleSlot(BattleSide.Enemy, 0), 1)]);

        Assert.True(enemyA.IsFainted);
        Assert.Equal(400 - 400 / 8, enemyB.CurrentHp); // replacement took 1 layer of spikes
    }
}
