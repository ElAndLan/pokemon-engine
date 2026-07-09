using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Battle v5 numeric ops (drain/recoil/heal/crash) resolved through the controller.</summary>
public sealed class BattleV5OpTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }, new TypeDef { Id = Grass }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleCreature Fast(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void Drain_HealsAttackerFromDamageDealt()
    {
        var drainMove = new BattleMove(EntityId.Parse("move:absorb"), Normal, DamageClass.Special, 40, 100, 25, 0, 0,
            drain: new Fraction(1, 2));
        var player = Fast(200, drainMove);
        player.TakeDamage(100); // down to 100/200
        var enemy = Slow(200, Inert());

        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        int dealt = enemy.MaxHp - enemy.CurrentHp;
        Assert.True(dealt > 0);
        Assert.Equal(100 + Math.Max(1, dealt / 2), player.CurrentHp); // healed half the damage
        Assert.Contains(events, e => e is Healed { Side: BattleSide.Player });
    }

    [Fact]
    public void Recoil_HurtsAttacker()
    {
        var recoilMove = new BattleMove(EntityId.Parse("move:takedown"), Normal, DamageClass.Physical, 90, 100, 25, 0, 0,
            recoil: new Fraction(1, 4));
        var player = Fast(200, recoilMove);
        var enemy = Slow(300, Inert());

        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        int dealt = enemy.MaxHp - enemy.CurrentHp;
        Assert.Equal(200 - Math.Max(1, dealt / 4), player.CurrentHp);
        Assert.Contains(events, e => e is Recoiled { Side: BattleSide.Player });
    }

    [Fact]
    public void Recoil_CanFaintAttacker()
    {
        var recoilMove = new BattleMove(EntityId.Parse("move:takedown"), Normal, DamageClass.Physical, 90, 100, 25, 0, 0,
            recoil: new Fraction(1, 1)); // absurd 100% recoil → self-KO
        var player = Fast(1, recoilMove); // 1 HP
        var enemy = Slow(300, Inert());

        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Player });
    }

    [Fact]
    public void CrashOnMiss_HurtsAttacker_OnlyWhenItMisses()
    {
        // Accuracy 1 with a seed that misses → crash ½ maxHp.
        var jumpKick = new BattleMove(EntityId.Parse("move:jumpkick"), Normal, DamageClass.Physical, 100, 1, 25, 0, 0,
            recoil: new Fraction(1, 2), recoilOnMiss: true);
        var player = Fast(200, jumpKick);
        var enemy = Slow(300, Inert());

        // Force a miss by scripting the accuracy roll ≥ accuracy.
        var events = new BattleController(player, enemy, Chart(), new FakeRng(ints: [50]))
            .ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(events, e => e is MoveMissed);
        Assert.Equal(100, player.CurrentHp); // 200 − ½·200 crash
        Assert.Contains(events, e => e is Recoiled { Side: BattleSide.Player });
    }

    [Fact]
    public void Heal_RestoresUserHp()
    {
        var recover = new BattleMove(EntityId.Parse("move:recover"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            heal: new Fraction(1, 2));
        var player = Fast(200, recover);
        player.TakeDamage(150); // down to 50/200
        var enemy = Slow(200, Inert());

        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(150, player.CurrentHp); // 50 + ½·200
        Assert.Contains(events, e => e is Healed { Side: BattleSide.Player });
    }

    [Fact]
    public void MultiHit_StrikesTheRolledNumberOfTimes()
    {
        var move = new BattleMove(EntityId.Parse("move:fury"), Normal, DamageClass.Physical, 25, 100, 25, 0, 0,
            multiHitMin: 2, multiHitMax: 5);
        var player = Fast(300, move);
        var enemy = Slow(300, Inert());

        // FakeRng: HitCount draw (6 → 4 hits), then 4×(crit-double, damage-roll int).
        var rng = new FakeRng(
            ints: [0, 6, 100, 100, 100, 100],   // accuracy(hit), hit count, then 4 damage rolls
            doubles: [0.99, 0.99, 0.99, 0.99]); // 4 crit checks (no crit)
        var events = new BattleController(player, enemy, Chart(), rng).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(4, events.Count(e => e is DamageDealt { Target: BattleSide.Enemy }));
    }

    [Fact]
    public void MultiHit_RollsCritIndependentlyPerHit()
    {
        // Each strike rolls its own crit: with crit doubles [crit, no, crit, no], the four
        // DamageDealt events must carry exactly that crit pattern (not all-or-nothing).
        var move = new BattleMove(EntityId.Parse("move:fury"), Normal, DamageClass.Physical, 25, 100, 25, 0, 0,
            multiHitMin: 2, multiHitMax: 5);
        var player = Fast(300, move);
        var enemy = Slow(3000, Inert()); // survives all four hits so each one lands

        // ints: accuracy(hit), hit-count(→4), 4 damage rolls. doubles: 4 per-hit crit checks.
        var rng = new FakeRng(ints: [0, 6, 100, 100, 100, 100], doubles: [0.0, 0.99, 0.0, 0.99]);
        var events = new BattleController(player, enemy, Chart(), rng).ResolveTurn(new UseMove(0), new UseMove(0));

        var crits = events.OfType<DamageDealt>().Where(d => d.Target == BattleSide.Enemy).Select(d => d.Crit).ToList();
        Assert.Equal([true, false, true, false], crits);
    }

    [Fact]
    public void MultiHit_StopsEarlyWhenTargetFaints()
    {
        var move = new BattleMove(EntityId.Parse("move:fury"), Normal, DamageClass.Physical, 200, 100, 25, 0, 0,
            multiHitMin: 2, multiHitMax: 5);
        var player = Fast(300, move);
        var enemy = Slow(1, Inert()); // dies on the first hit

        var rng = new FakeRng(ints: [0, 7, 100, 100, 100, 100, 100], doubles: [0.99, 0.99, 0.99, 0.99, 0.99]);
        var events = new BattleController(player, enemy, Chart(), rng).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(1, events.Count(e => e is DamageDealt { Target: BattleSide.Enemy })); // only one landed
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Enemy });
    }

    [Fact]
    public void MultiHit_WithDrain_HealsFromTotalDamage()
    {
        var move = new BattleMove(EntityId.Parse("move:leechhits"), Normal, DamageClass.Special, 25, 100, 25, 0, 0,
            drain: new Fraction(1, 2), multiHitMin: 2, multiHitMax: 5);
        var player = Fast(400, move);
        player.TakeDamage(200); // 200/400
        var enemy = Slow(400, Inert());

        var rng = new FakeRng(ints: [0, 6, 100, 100, 100, 100], doubles: [0.99, 0.99, 0.99, 0.99]);
        new BattleController(player, enemy, Chart(), rng).ResolveTurn(new UseMove(0), new UseMove(0));

        int totalDealt = enemy.MaxHp - enemy.CurrentHp;
        Assert.Equal(200 + Math.Max(1, totalDealt / 2), player.CurrentHp); // drained from the sum of all hits
    }

    [Fact]
    public void FixedDamage_DealsFlatAmount_IgnoringStats()
    {
        var sonicBoom = new BattleMove(EntityId.Parse("move:sonicboom"), Normal, DamageClass.Special, null, 100, 25, 0, 0,
            fixedDamage: 20);
        var player = Fast(200, sonicBoom);
        var enemy = Slow(200, Inert());

        new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(180, enemy.CurrentHp); // exactly 20, regardless of stats
    }

    [Fact]
    public void FixedDamage_LevelBased_DealsUserLevel()
    {
        var seismicToss = new BattleMove(EntityId.Parse("move:seismictoss"), Normal, DamageClass.Physical, null, 100, 25, 0, 0,
            fixedDamageLevel: true);
        var player = Fast(200, seismicToss); // level 50
        var enemy = Slow(200, Inert());

        new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(150, enemy.CurrentHp); // 200 − 50 (user level)
    }

    [Fact]
    public void FixedDamage_TypeImmuneTargetTakesNone()
    {
        // Ghost type immune to a Normal-typed fixed-damage move (chart: normal deals 0 to ghost).
        EntityId ghost = EntityId.Parse("type:ghost");
        var chart = new TypeChart([new TypeDef { Id = Normal, NoDamageTo = [ghost] }, new TypeDef { Id = ghost }]);
        var sonicBoom = new BattleMove(EntityId.Parse("move:sonicboom"), Normal, DamageClass.Special, null, 100, 25, 0, 0,
            fixedDamage: 20);
        var player = Fast(200, sonicBoom);
        var enemy = new BattleCreature(EntityId.Parse("species:g"), "G", 50, [ghost],
            new Stats(200, 100, 100, 100, 100, 1), [Inert()]);

        new BattleController(player, enemy, chart, new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, enemy.CurrentHp); // immune → no damage
    }

    [Fact]
    public void Ohko_KnocksOutWhenItLands()
    {
        var fissure = new BattleMove(EntityId.Parse("move:fissure"), Normal, DamageClass.Physical, null, null, 5, 0, 0,
            ohko: true);
        var player = Fast(200, fissure); // level 50
        var enemy = Slow(9999, Inert());  // level 50, huge HP

        // OHKO accuracy at equal levels = 30; scripted accuracy roll 0 (<30) hits.
        new BattleController(player, enemy, Chart(), new FakeRng(ints: [0])).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.IsFainted); // full HP → 0 in one blow
    }

    [Fact]
    public void Ohko_FailsAgainstHigherLevelTarget()
    {
        var fissure = new BattleMove(EntityId.Parse("move:fissure"), Normal, DamageClass.Physical, null, null, 5, 0, 0,
            ohko: true);
        var player = new BattleCreature(EntityId.Parse("species:a"), "A", 20, [Normal],
            new Stats(200, 100, 100, 100, 100, 100), [fissure]);
        var enemy = new BattleCreature(EntityId.Parse("species:b"), "B", 80, [Normal],
            new Stats(200, 100, 100, 100, 100, 1), [Inert()]);

        // OhkoAccuracy(20, 80) = 0 → auto-fail; the roll draws once and misses.
        var events = new BattleController(player, enemy, Chart(), new FakeRng(ints: [0]))
            .ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.False(enemy.IsFainted);
        Assert.Contains(events, e => e is MoveMissed);
    }

    [Fact]
    public void SelfDestruct_FaintsTheUserAfterDealingDamage()
    {
        var explosion = new BattleMove(EntityId.Parse("move:explosion"), Normal, DamageClass.Physical, 250, 100, 5, 0, 0,
            selfDestruct: true);
        var player = Fast(200, explosion);
        var enemy = Slow(300, Inert());

        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.IsFainted);                        // user blew up
        Assert.True(enemy.CurrentHp < enemy.MaxHp);           // but still dealt damage first
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Player });
    }

    [Fact]
    public void CritBoost_RaisesUsersCritStage_ForLaterMoves()
    {
        // Focus-Energy-style status move (+2 crit stages), then a physical hit that should always crit.
        var focus = new BattleMove(EntityId.Parse("move:focus"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            critBoost: 2);
        var hit = new BattleMove(EntityId.Parse("move:hit"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0);
        var player = Fast(300, focus, hit);
        var enemy = Slow(300, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(3));

        var t1 = battle.ResolveTurn(new UseMove(0), new UseMove(0)); // focus energy
        Assert.Contains(t1, e => e is CritBoosted { Side: BattleSide.Player });
        Assert.Equal(2, player.CritStageBonus); // stacked onto the user for later moves
    }

    [Fact]
    public void CritBoost_MakesHitsCrit_AtHighStage()
    {
        // A move that both boosts crit a lot and hits: at a very high crit stage, IsCrit(stage) with a
        // low crit roll must produce a crit-flagged DamageDealt.
        var move = new BattleMove(EntityId.Parse("move:slashfocus"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0,
            critBoost: 3);
        var player = Fast(300, move);
        player.RaiseCrit(3); // already focused from a prior turn → stage 3 this hit
        var enemy = Slow(300, Inert());

        // FakeRng: accuracy(0 hit), crit double 0.0 (< any crit chance → crit), damage roll 100.
        var events = new BattleController(player, enemy, Chart(), new FakeRng(ints: [0, 100], doubles: [0.0]))
            .ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(events, e => e is DamageDealt { Crit: true });
    }

    [Fact]
    public void CritBoost_ClearsOnSwitchOut()
    {
        var focus = new BattleMove(EntityId.Parse("move:focus"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            critBoost: 2);
        var a = Fast(300, focus);
        var b = Fast(300, Inert());
        var enemy = Slow(300, Inert());
        var battle = new BattleController([a, b], [enemy], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // a focuses
        Assert.Equal(2, a.CritStageBonus);
        battle.ResolveTurn(new Switch(1), new UseMove(0));  // a switches out
        Assert.Equal(0, a.CritStageBonus);                  // crit bonus cleared
    }

    [Fact]
    public void LeechSeed_SapsVictimAndHealsSeederEachTurn()
    {
        var seed = new BattleMove(EntityId.Parse("move:leechseed"), Grass, DamageClass.Status, null, null, 25, 0, 0,
            leechSeed: true); // Grass-typed; the Normal victim is not immune
        var player = Fast(400, seed);
        var enemy = Slow(400, Inert());
        player.TakeDamage(200); // seeder at 200/400 so its heal is visible
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.Seeded);
        Assert.Contains(events, e => e is LeechSeeded { Side: BattleSide.Enemy });
        int sap = enemy.MaxHp / 8; // 50
        Assert.Equal(400 - sap, enemy.CurrentHp);   // enemy sapped end-of-turn
        Assert.Equal(200 + sap, player.CurrentHp);  // seeder recovered the same amount
        Assert.Contains(events, e => e is LeechSapped { Side: BattleSide.Enemy });
    }

    [Fact]
    public void LeechSeed_TypeImmuneTargetIsNotSeeded()
    {
        EntityId grass = EntityId.Parse("type:grass");
        var chart = new TypeChart([new TypeDef { Id = grass }]);
        var seed = new BattleMove(EntityId.Parse("move:leechseed"), grass, DamageClass.Status, null, null, 25, 0, 0,
            leechSeed: true);
        var player = new BattleCreature(EntityId.Parse("species:a"), "A", 50, [grass],
            new Stats(300, 100, 100, 100, 100, 100), [seed]);
        var enemy = new BattleCreature(EntityId.Parse("species:b"), "B", 50, [grass], // grass → immune
            new Stats(300, 100, 100, 100, 100, 1), [Inert()]);

        new BattleController(player, enemy, chart, new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.False(enemy.Seeded);
    }

    [Fact]
    public void LeechSeed_ClearsOnSwitchOut()
    {
        var seed = new BattleMove(EntityId.Parse("move:leechseed"), Grass, DamageClass.Status, null, null, 25, 0, 0,
            leechSeed: true);
        var player = Fast(300, seed);
        var enemyA = Slow(300, Inert());
        var enemyB = Slow(300, Inert());
        var battle = new BattleController([player], [enemyA, enemyB], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // turn 1: seeds enemyA
        Assert.True(enemyA.Seeded);
        battle.ResolveTurn(new UseMove(0), new Switch(1));  // turn 2: enemyA switches out → volatile clears
        Assert.False(enemyA.Seeded);
    }

    [Fact]
    public void Drain_OnKnockoutHit_StillHeals()
    {
        var drainMove = new BattleMove(EntityId.Parse("move:absorb"), Normal, DamageClass.Special, 200, 100, 25, 0, 0,
            drain: new Fraction(1, 2));
        var player = Fast(300, drainMove);
        player.TakeDamage(200); // 100/300
        var enemy = Slow(1, Inert()); // will be KO'd

        new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.IsFainted);
        Assert.True(player.CurrentHp > 100); // drained from the finishing blow
    }
}
