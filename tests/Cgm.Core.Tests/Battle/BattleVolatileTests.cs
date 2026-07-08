using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Confusion + flinch wired through the controller (Battle v4). RNG draws are conditional,
/// so a non-confused, effect-less move draws nothing beyond its normal accuracy/crit/roll.</summary>
public sealed class BattleVolatileTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    // A status move with null accuracy + no effects draws no RNG at all.
    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleMove Hit() =>
        new(EntityId.Parse("move:hit"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0);

    private static BattleMove Confuser() =>
        new(EntityId.Parse("move:confuse"), Normal, DamageClass.Status, null, null, 25, 0, 0, confuseChance: 100);

    private static BattleMove Flincher() =>
        new(EntityId.Parse("move:flinch"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0, flinchChance: 100);

    // spe controls turn order; the "fast" side (spe 100) always acts before the "slow" side (spe 1).
    private static BattleCreature Fast(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void Confusion_SelfHit_CostsTheTurn()
    {
        var player = Fast(200, Hit());
        player.SetConfusion(2);
        var enemy = Slow(200, Inert());
        // doubles: [0.0] → 0.0 < 0.5 → hurts itself. No int draws needed (self-hit returns; inert enemy draws none).
        var battle = new BattleController(player, enemy, Chart(), new FakeRng(doubles: [0.0]));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.CurrentHp < player.MaxHp);            // hurt itself
        Assert.Equal(200, enemy.CurrentHp);                     // never got hit — player lost its turn
        Assert.Equal(25, player.Moves[0].Pp);                   // no PP spent
        Assert.Contains(events, e => e is HurtInConfusion { Side: BattleSide.Player });
        Assert.Equal(1, player.ConfusionCounter);               // ticked 2 → 1
    }

    [Fact]
    public void Confusion_PushThrough_ActsNormally()
    {
        var player = Fast(200, Hit());
        player.SetConfusion(2);
        var enemy = Slow(200, Inert());
        // doubles: [0.99 confusion-no-selfhit, 0.99 crit-no] ; ints: [50 hit, 15 damageroll]
        var battle = new BattleController(player, enemy, Chart(), new FakeRng(ints: [50, 15], doubles: [0.99, 0.99]));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.CurrentHp < enemy.MaxHp);  // move landed
        Assert.Equal(24, player.Moves[0].Pp);        // PP spent
        Assert.Equal(1, player.ConfusionCounter);
    }

    [Fact]
    public void Confusion_SnapsOut_WhenCounterHitsZero()
    {
        var player = Fast(200, Inert());
        player.SetConfusion(1);                        // ticks to 0 → snaps out, acts freely, no self-hit roll
        var enemy = Slow(200, Inert());
        var battle = new BattleController(player, enemy, Chart(), new FakeRng()); // zero draws

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.False(player.IsConfused);
        Assert.Contains(events, e => e is ConfusionEnded { Side: BattleSide.Player });
        Assert.Contains(events, e => e is MoveUsed { Side: BattleSide.Player }); // it still got to move
    }

    [Fact]
    public void Confusion_SelfHit_CanFaint()
    {
        var player = Fast(1, Hit());                   // 1 HP → self-hit faints it
        player.SetConfusion(2);
        var enemy = Slow(200, Inert());
        var battle = new BattleController(player, enemy, Chart(), new FakeRng(doubles: [0.0]));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Player });
        Assert.Equal(BattleSide.Enemy, battle.Outcome!.Winner);
    }

    [Fact]
    public void Move_InflictsConfusion_OnTarget()
    {
        // Confuser on the slow side: the fast enemy acts first (inert, no draw), THEN gets confused —
        // so it isn't ticked this turn and the counter reads the full rolled duration.
        var player = Slow(200, Confuser());
        var enemy = Fast(200, Inert());
        // ints: [0 confuse-roll (<100 → applies), 3 duration]
        var battle = new BattleController(player, enemy, Chart(), new FakeRng(ints: [0, 3]));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.IsConfused);
        Assert.Equal(3, enemy.ConfusionCounter);
        Assert.Contains(events, e => e is Confused { Side: BattleSide.Enemy });
    }

    [Fact]
    public void Confusion_ClearsOnSwitchOut()
    {
        var a = Fast(200, Inert());
        a.SetConfusion(3);
        var b = Fast(200, Inert());
        var enemy = Slow(200, Inert());
        var battle = new BattleController([a, b], [enemy], Chart(), new FakeRng());

        battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.False(a.IsConfused); // volatile cleared on switch-out
    }

    [Fact]
    public void Flinch_SkipsVictimTurn_WhenFlincherIsFaster()
    {
        var player = Fast(200, Flincher());   // fast → moves first, flinches enemy
        var enemy = Slow(200, Hit());
        // player draws: ints[50 hit, 15 roll, 0 flinch(<100)], doubles[0.99 no-crit]
        var battle = new BattleController(player, enemy, Chart(), new FakeRng(ints: [50, 15, 0], doubles: [0.99]));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, player.CurrentHp);              // enemy never landed its hit
        Assert.Equal(25, enemy.Moves[0].Pp);              // enemy spent no PP
        Assert.Contains(events, e => e is Flinched { Side: BattleSide.Enemy });
    }

    [Fact]
    public void Flinch_HasNoEffect_WhenFlincherIsSlower()
    {
        // Player is slow and carries a flinch move; the fast enemy already acted, so the flinch is moot this turn.
        var player = Slow(200, Flincher());
        var enemy = Fast(200, Hit());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1)); // acc 100 → enemy hit is guaranteed

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.CurrentHp < player.MaxHp); // enemy acted despite being "flinched" afterward
    }

    [Fact]
    public void Flinch_DoesNotPersist_ToNextTurn()
    {
        var player = Fast(200, Flincher(), Inert()); // move 0 flinches; move 1 leaves the enemy alone
        var enemy = Slow(200, Hit());
        var battle = new BattleController(player, enemy, Chart(), new Rng(7));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // enemy flinches — takes no action
        Assert.Equal(200, player.CurrentHp);                // proof it was flinched this turn

        battle.ResolveTurn(new UseMove(1), new UseMove(0)); // player doesn't flinch this turn → enemy acts
        Assert.True(player.CurrentHp < 200);                // enemy landed a hit on turn 2
    }
}
