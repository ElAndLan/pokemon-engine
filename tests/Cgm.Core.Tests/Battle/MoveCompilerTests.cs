using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class MoveCompilerTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static Effect Op(string op, int? chance = null, params (string Key, object Value)[] ps)
    {
        var dict = ps.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));
        return new Effect { Op = op, Chance = chance, Params = dict.Count == 0 ? null : dict };
    }

    private static Move Move(DamageClass cls, int? power, params Effect[] effects) => new()
    {
        Id = EntityId.Parse("move:m"),
        Name = "M",
        Type = Normal,
        DamageClass = cls,
        Power = power,
        Accuracy = 100,
        Pp = 25,
        Effects = effects,
    };

    [Fact]
    public void CompilesBasicDamagingMove()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 40, Op("damage")));
        Assert.Equal(40, bm.Power);
        Assert.Null(bm.Ailment);
        Assert.Null(bm.StageEffect);
        Assert.Equal(0, bm.FlinchChance);
    }

    [Fact]
    public void CompilesContactFlag()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 40, Op("damage")) with { MakesContact = true });
        Assert.True(bm.MakesContact);
    }

    [Fact]
    public void CompilesAilmentOp()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Physical, 40, Op("damage"), Op("ailment", chance: 30, ("ailment", "burn"))));
        Assert.Equal(PersistentStatus.Burn, bm.Ailment);
        Assert.Equal(30, bm.AilmentChance);
    }

    [Fact]
    public void ConfusionAilment_MapsToConfuseChance()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Status, null, Op("ailment", chance: 100, ("ailment", "confusion"))));
        Assert.Equal(100, bm.ConfuseChance);
        Assert.Null(bm.Ailment);
    }

    [Fact]
    public void CompilesStatStageOp()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Status, null, Op("statStage", chance: 100, ("stat", "atk"), ("delta", 2), ("onSelf", true))));
        Assert.NotNull(bm.StageEffect);
        Assert.Equal(StatKind.Atk, bm.StageEffect!.Stat);
        Assert.Equal(2, bm.StageEffect.Delta);
        Assert.True(bm.StageEffect.OnSelf);
        Assert.Equal(100, bm.StageEffect.Chance);
    }

    [Fact]
    public void CompilesFlinchOp()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 60, Op("flinch", chance: 30)));
        Assert.Equal(30, bm.FlinchChance);
    }

    [Fact]
    public void NullChance_DefaultsToGuaranteed()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("ailment", null, ("ailment", "poison"))));
        Assert.Equal(100, bm.AilmentChance);
    }

    [Fact]
    public void MultipleOps_AllCompiled()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 40,
            Op("damage"),
            Op("ailment", chance: 10, ("ailment", "paralysis")),
            Op("statStage", chance: 100, ("stat", "spe"), ("delta", -1))));
        Assert.Equal(PersistentStatus.Paralysis, bm.Ailment);
        Assert.Equal(StatKind.Spe, bm.StageEffect!.Stat);
        Assert.Equal(-1, bm.StageEffect.Delta);
        Assert.False(bm.StageEffect.OnSelf); // onSelf omitted → target
    }

    [Fact]
    public void UnimplementedOp_ThrowsUntilControllerSupport()
    {
        // transformForm is a v6/post-slice op the compiler doesn't handle yet — it must not silently drop.
        Assert.Throws<NotSupportedException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 100, Op("transformForm"))));
    }

    [Fact]
    public void CompilesMultiHitOp()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Physical, 25, Op("damage"), Op("multiHit", null, ("min", 2), ("max", 5))));
        Assert.Equal(2, bm.MultiHitMin);
        Assert.Equal(5, bm.MultiHitMax);
    }

    [Fact]
    public void MultiHit_InvalidRange_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 25, Op("multiHit", null, ("min", 5), ("max", 2)))));
    }

    [Fact]
    public void CompilesFixedDamageAndOhkoOps()
    {
        BattleMove flat = MoveCompiler.ToBattleMove(Move(DamageClass.Special, null, Op("fixedDamage", null, ("amount", 20))));
        Assert.Equal(20, flat.FixedDamage);
        Assert.False(flat.FixedDamageLevel);

        BattleMove level = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, null, Op("fixedDamage", null, ("levelBased", true))));
        Assert.True(level.FixedDamageLevel);
        Assert.Null(level.FixedDamage);

        BattleMove ohko = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, null, Op("ohko")));
        Assert.True(ohko.Ohko);
    }

    [Fact]
    public void FixedDamage_FlatWithoutAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, null, Op("fixedDamage"))));
    }

    [Fact]
    public void CompilesCritBoostAndSelfDestructOps()
    {
        BattleMove focus = MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("critBoost")));
        Assert.Equal(2, focus.CritBoost); // default +2 stages

        BattleMove sniper = MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("critBoost", null, ("stages", 1))));
        Assert.Equal(1, sniper.CritBoost);

        BattleMove boom = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 250, Op("damage"), Op("selfDestruct")));
        Assert.True(boom.SelfDestruct);

        BattleMove seed = MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("leechSeed")));
        Assert.True(seed.LeechSeed);
    }

    [Fact]
    public void CompilesDrainRecoilHealOps()
    {
        BattleMove drain = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 75, Op("damage"), Op("drain")));
        Assert.Equal(new Fraction(1, 2), drain.Drain); // defaults to ½

        BattleMove recoil = MoveCompiler.ToBattleMove(
            Move(DamageClass.Physical, 120, Op("damage"), Op("recoil", null, ("num", 1), ("den", 3))));
        Assert.Equal(new Fraction(1, 3), recoil.Recoil);
        Assert.False(recoil.RecoilOnMiss);

        BattleMove jumpKick = MoveCompiler.ToBattleMove(
            Move(DamageClass.Physical, 100, Op("damage"), Op("recoil", null, ("num", 1), ("den", 2), ("onMiss", true))));
        Assert.True(jumpKick.RecoilOnMiss);

        BattleMove recover = MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("heal")));
        Assert.Equal(new Fraction(1, 2), recover.Heal);
    }

    [Fact]
    public void MissingParam_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStage", chance: 100, ("delta", 1)))));
    }

    [Fact]
    public void StatStageOnHp_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStage", null, ("stat", "hp"), ("delta", 1)))));
    }

    [Fact]
    public void UnknownAilment_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("ailment", null, ("ailment", "petrified")))));
    }

    // End-to-end: a data move with a guaranteed-burn ailment op actually burns the target in a real battle.
    [Fact]
    public void CompiledMove_InflictsAilmentInRealBattle()
    {
        BattleMove burnMove = MoveCompiler.ToBattleMove(
            Move(DamageClass.Physical, 40, Op("damage"), Op("ailment", chance: 100, ("ailment", "burn"))));

        var chart = new TypeChart([new TypeDef { Id = Normal }]);
        var player = new BattleCreature(EntityId.Parse("species:a"), "A", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 100), [burnMove]);
        var enemy = new BattleCreature(EntityId.Parse("species:b"), "B", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 1),
            [new BattleMove(EntityId.Parse("move:idle"), Normal, DamageClass.Status, null, null, 25, 0, 0)]);

        var battle = new BattleController(player, enemy, chart, new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(PersistentStatus.Burn, enemy.Status);
    }
}
