using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Protect/Detect volatile (catalog §7.2): a priority self-shield that blocks the opponent's
/// move for a turn, with success-chain decay on consecutive use.</summary>
public sealed class BattleProtectTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Hit() =>
        new(EntityId.Parse("move:hit"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    // Protect: +4 priority so the user shields before the attacker strikes.
    private static BattleMove Protect() =>
        new(EntityId.Parse("move:protect"), Normal, DamageClass.Status, null, null, 25, priority: 4, 0, isProtect: true);

    private static BattleCreature Slower(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 50), moves);

    private static BattleCreature Faster(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    [Fact]
    public void Protect_BlocksIncomingDamage()
    {
        // Player is slower, but Protect's +4 priority makes it resolve first.
        var player = Slower(200, Protect());
        var enemy = Faster(200, Hit());
        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, player.CurrentHp); // shielded
        Assert.Contains(events, e => e is Protected { Side: BattleSide.Player });
        Assert.Contains(events, e => e is MoveBlocked { Side: BattleSide.Enemy });
    }

    [Fact]
    public void Protect_BlocksAllOpponentsTargetInSingles()
    {
        var spreadHit = new BattleMove(EntityId.Parse("move:spread"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0,
            target: MoveTarget.AllOpponents);
        var player = Slower(200, Protect());
        var enemy = Faster(200, spreadHit);
        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, player.CurrentHp);
        Assert.Contains(events, e => e is MoveBlocked { Side: BattleSide.Enemy });
    }

    [Fact]
    public void Protect_BlocksAllOtherPokemonTargetInSingles()
    {
        var spreadHit = new BattleMove(EntityId.Parse("move:spread"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0,
            target: MoveTarget.AllOtherPokemon);
        var player = Slower(200, Protect());
        var enemy = Faster(200, spreadHit);
        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, player.CurrentHp);
        Assert.Contains(events, e => e is MoveBlocked { Side: BattleSide.Enemy });
    }

    [Fact]
    public void Protect_ExpiresNextTurn()
    {
        var player = Slower(200, Protect(), Inert());
        var enemy = Faster(200, Hit());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // protect blocks
        Assert.Equal(200, player.CurrentHp);
        battle.ResolveTurn(new UseMove(1), new UseMove(0)); // no protect this turn → takes the hit
        Assert.True(player.CurrentHp < 200);
    }

    [Fact]
    public void Protect_DoesNotBlockSelfTargetedMoves()
    {
        // A self-buff (stat change on self) is not aimed at the protected creature, so it isn't blocked.
        var buff = new BattleMove(EntityId.Parse("move:swordsdance"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            stageEffect: new StageEffect(StatKind.Atk, 2, OnSelf: true, Chance: 100));
        var player = Slower(200, Protect());
        var enemy = Faster(200, buff);
        new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2, enemy.Stage(StatKind.Atk)); // enemy still buffed itself through the shield
    }

    [Fact]
    public void ConsecutiveProtect_ChainCanFail()
    {
        // With a rigged RNG: first protect (chain 0) always succeeds; a later roll ≥ chance fails.
        var player = Slower(400, Protect());
        var enemy = Faster(400, Hit());
        // Draw order per turn: (turn order — no tie), protect success double, enemy accuracy/crit/roll if it hits.
        // Turn 1: protect chain 0 → NextDouble()<1.0 always true. Enemy blocked (no draws).
        // Turn 2: protect chain 1 → success chance 0.5; feed 0.9 (≥0.5) → fail; enemy hit lands.
        var rng = new FakeRng(
            ints: [50, 15],                 // turn 2 enemy accuracy(hit=50<100), damage roll(85+15)
            doubles: [0.0, 0.9, 0.5]);      // t1 protect ok(0.0), t2 protect fail(0.9), t2 enemy crit(0.5 no crit)
        var battle = new BattleController(player, enemy, Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // t1: protect succeeds, blocks
        Assert.Equal(400, player.CurrentHp);
        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // t2: protect fails, enemy hits
        Assert.True(player.CurrentHp < 400);

        EffectTraceEntry[] protectTraces = battle.Trace.Where(entry => entry.Kind == EffectTraceKind.Protect).ToArray();
        Assert.Equal(2, protectTraces.Length);
        Assert.All(protectTraces, entry =>
        {
            Assert.Equal(new BattleSlot(BattleSide.Player, 0), entry.SourceSlot);
            Assert.Null(entry.TargetSlot);
            Assert.True(entry.Performed);
            Assert.Equal(1d, entry.DrawBound);
            Assert.True(entry.EventEndIndex > entry.EventStartIndex);
        });
        Assert.Equal([0d, 0.9d], protectTraces.Select(entry => entry.DrawResult));
        Assert.Equal([1, 0], protectTraces.Select(entry => entry.Value));
    }
}
