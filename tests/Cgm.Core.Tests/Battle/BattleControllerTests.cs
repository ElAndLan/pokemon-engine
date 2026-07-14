using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleControllerTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    private static TypeChart Chart() =>
        new([new TypeDef { Id = Fire, DoubleDamageTo = [Grass] }, new TypeDef { Id = Grass }]);

    private static BattleMove M(int power = 40, int acc = 100, int pp = 10, int prio = 0) =>
        new(EntityId.Parse("move:m"), Fire, DamageClass.Special, power, acc, pp, prio, 0);

    private static BattleCreature Creature(EntityId type, int hp, int spe, params BattleMove[] moves) =>
        new(EntityId.Parse("species:x"), "X", 50, [type], new Stats(hp, 100, 100, 100, 100, spe), moves);

    private static BattleController Battle(BattleCreature p, BattleCreature e, int seed = 1) =>
        new(p, e, Chart(), new Rng(seed));

    [Fact]
    public void OneShot_ProducesMoveDamageFaintEnd_AndPlayerWins()
    {
        var player = Creature(Fire, hp: 100, spe: 100, M(power: 100));
        var enemy = Creature(Grass, hp: 10, spe: 10, M());
        BattleController b = Battle(player, enemy);

        IReadOnlyList<BattleEvent> events = b.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.IsFainted);
        Assert.Equal(new BattleOutcome(BattleSide.Player), b.Outcome);
        Assert.IsType<MoveUsed>(events[0]);
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Enemy });
        Assert.Contains(events, e => e is BattleEnded { Winner: BattleSide.Player });
        // The fainted enemy never got to act.
        Assert.DoesNotContain(events, e => e is MoveUsed { Side: BattleSide.Enemy });
    }

    [Fact]
    public void SimultaneousActionWipeIsADrawAndStopsLaterActions()
    {
        BattleMove selfDestruct = new(EntityId.Parse("move:mutual"), Fire, DamageClass.Special,
            300, null, 10, 0, 0, selfDestruct: true);
        BattleCreature player = Creature(Fire, hp: 20, spe: 100, selfDestruct);
        BattleCreature enemy = Creature(Grass, hp: 20, spe: 10, M());
        var battle = new BattleController(player, enemy, Chart(), new FakeRng(ints: [15], doubles: [0.99]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.True(enemy.IsFainted);
        Assert.True(battle.Outcome!.IsDraw);
        Assert.Contains(events, item => item is BattleEnded { Winner: null });
        Assert.DoesNotContain(events, item => item is MoveUsed { Side: BattleSide.Enemy });
    }

    [Fact]
    public void EndTurnResidualBatchCanProduceADraw()
    {
        BattleCreature player = Creature(Fire, hp: 8, spe: 100, M(power: 1));
        BattleCreature enemy = Creature(Grass, hp: 8, spe: 10, M(power: 1));
        player.SetStatus(PersistentStatus.Burn);
        enemy.SetStatus(PersistentStatus.Burn);
        player.TakeDamage(7);
        enemy.TakeDamage(7);
        var battle = Battle(player, enemy);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new Pass());

        Assert.True(player.IsFainted);
        Assert.True(enemy.IsFainted);
        Assert.True(battle.Outcome!.IsDraw);
        Assert.IsType<BattleEnded>(events[^1]);
    }

    [Fact]
    public void FasterSideActsFirst()
    {
        var player = Creature(Fire, hp: 100, spe: 100, M(power: 1));
        var enemy = Creature(Grass, hp: 100, spe: 10, M(power: 1));
        IReadOnlyList<BattleEvent> events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        var moveOrder = events.OfType<MoveUsed>().Select(m => m.Side).ToList();
        Assert.Equal([BattleSide.Player, BattleSide.Enemy], moveOrder);
    }

    [Fact]
    public void HigherPriorityBeatsSpeed()
    {
        var player = Creature(Fire, hp: 100, spe: 10, M(power: 1, prio: 0));
        var enemy = Creature(Grass, hp: 100, spe: 200, M(power: 1, prio: 0));
        // Player uses a +1 priority move; enemy a normal one → player first despite lower speed.
        var priorityMove = M(power: 1, prio: 1);
        var pc = Creature(Fire, hp: 100, spe: 10, priorityMove);
        var ec = Creature(Grass, hp: 100, spe: 200, M(power: 1));
        var events = Battle(pc, ec).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(BattleSide.Player, events.OfType<MoveUsed>().First().Side);
    }

    [Fact]
    public void ZeroAccuracyMove_Misses_NoDamage()
    {
        var player = Creature(Fire, hp: 100, spe: 100, M(acc: 0));
        var enemy = Creature(Grass, hp: 100, spe: 10, M());
        var events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(events, e => e is MoveMissed { Side: BattleSide.Player });
        Assert.Equal(100, enemy.CurrentHp); // enemy took no damage from the missed move
    }

    [Fact]
    public void PpIsConsumed()
    {
        var player = Creature(Fire, hp: 100, spe: 100, M(power: 1, pp: 5));
        var enemy = Creature(Grass, hp: 100, spe: 10, M(power: 1, pp: 5));
        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(4, player.Moves[0].Pp);
    }

    [Theory]
    [InlineData(5)]   // out of range
    [InlineData(-1)]
    public void InvalidMoveIndex_Throws(int index)
    {
        var b = Battle(Creature(Fire, 100, 100, M()), Creature(Grass, 100, 10, M()));
        Assert.Throws<ArgumentException>(() => b.ResolveTurn(new UseMove(index), new UseMove(0)));
    }

    [Fact]
    public void NoPpMove_Throws()
    {
        var noPp = M(pp: 0);
        var b = Battle(Creature(Fire, 100, 100, noPp), Creature(Grass, 100, 10, M()));
        Assert.Throws<ArgumentException>(() => b.ResolveTurn(new UseMove(0), new UseMove(0)));
    }

    [Fact]
    public void ResolvingAfterBattleOver_Throws()
    {
        var b = Battle(Creature(Fire, 100, 100, M(power: 100)), Creature(Grass, 10, 10, M()));
        b.ResolveTurn(new UseMove(0), new UseMove(0)); // enemy faints, battle over
        Assert.Throws<InvalidOperationException>(() => b.ResolveTurn(new UseMove(0), new UseMove(0)));
    }

    [Fact]
    public void SameSeedAndActions_ProduceIdenticalLogs()
    {
        static (BattleController b, BattleCreature p, BattleCreature e) Setup()
        {
            var p = Creature(Fire, hp: 200, spe: 100, M(power: 30));
            var e = Creature(Grass, hp: 200, spe: 100, M(power: 30));
            return (new BattleController(p, e, Chart(), new Rng(2026)), p, e);
        }

        var (b1, _, _) = Setup();
        var (b2, _, _) = Setup();
        for (int t = 0; t < 3; t++)
        {
            b1.ResolveTurn(new UseMove(0), new UseMove(0));
            b2.ResolveTurn(new UseMove(0), new UseMove(0));
        }
        Assert.Equal(b1.Log, b2.Log); // deterministic → identical event streams
    }

    [Fact]
    public void SinglesDirectHit_RecordsAccuracyCritAndDamageRollTrace()
    {
        var player = Creature(Fire, hp: 100, spe: 100, M(power: 40));
        var enemy = Creature(Grass, hp: 999, spe: 10,
            new BattleMove(EntityId.Parse("move:wait"), Grass, DamageClass.Status, null, null, 10, 0, 0));
        var battle = new BattleController(player, enemy, Chart(), new FakeRng(ints: [0, 15], doubles: [0.99]));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal([EffectTraceKind.StatusGate, EffectTraceKind.FlinchGate, EffectTraceKind.ConfusionGate,
            EffectTraceKind.Accuracy, EffectTraceKind.HitCount, EffectTraceKind.Immunity,
            EffectTraceKind.Critical, EffectTraceKind.DamageRoll, EffectTraceKind.Damage],
            battle.Trace.Take(9).Select(entry => entry.Kind));
        Assert.Equal(0.99d, Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.Critical).DrawResult);
        Assert.Equal(15d, Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.DamageRoll).DrawResult);
    }
}
