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

    [Theory]
    [InlineData("noBattleEffect")]
    [InlineData("postBattleReward")]
    public void CompilesExplicitNoBattleOps(string op)
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op(op)));
        Assert.Empty(bm.SecondaryEffects);
    }

    [Fact]
    public void CompilesContactFlag()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 40, Op("damage")) with { MakesContact = true });
        Assert.True(bm.MakesContact);
    }

    [Fact]
    public void CompilesTarget()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 40, Op("damage")) with { Target = MoveTarget.AllOpponents });
        Assert.Equal(MoveTarget.AllOpponents, bm.Target);
    }

    [Fact]
    public void CompilesWeatherOp()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Status, null, Op("weather", null, ("weather", "rain")))
            with { Target = MoveTarget.EntireField });

        Assert.Equal(Weather.Rain, bm.SetsWeather);
        Assert.Contains(bm.SecondaryEffects, e => e is SetWeatherEffect { Weather: Weather.Rain });
    }

    [Fact]
    public void WeatherOp_RejectsMissingInvalidAndUnknownParams()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("weather"))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("weather", null, ("weather", "snow")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("weather", null, ("weather", "rain"), ("turns", 8)))));
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
    public void AilmentOp_AcceptsLegacyStatusParam()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Physical, 40, Op("damage"), Op("ailment", chance: 10, ("status", "burn"))));
        Assert.Equal(PersistentStatus.Burn, bm.Ailment);
        Assert.Equal(10, bm.AilmentChance);
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
        Assert.Single(bm.StageEffects);
    }

    [Fact]
    public void UserTargetStatStage_DefaultsToSelf()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Status, null, Op("statStage", chance: 100, ("stat", "atk"), ("delta", 2)))
            with { Target = MoveTarget.User });

        Assert.True(bm.StageEffect!.OnSelf);
    }

    [Fact]
    public void CompilesMultipleStatStageOpsInOrder()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Status, null,
                Op("statStage", chance: 100, ("stat", "atk"), ("delta", 1), ("onSelf", true)),
                Op("statStage", chance: 100, ("stat", "spa"), ("delta", 1), ("onSelf", true))));

        Assert.Collection(bm.StageEffects,
            e => Assert.Equal(new StageEffect(StatKind.Atk, 1, true, 100), e),
            e => Assert.Equal(new StageEffect(StatKind.Spa, 1, true, 100), e));
        Assert.Equal(StatKind.Atk, bm.StageEffect!.Stat);
    }

    [Fact]
    public void CompilesStatStageAllOp()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Status, null, Op("statStageAll", chance: 10, ("delta", 1), ("onSelf", true))));

        Assert.Equal(new StageAllEffect(1, true, 10), bm.StageAllEffect);
        Assert.Contains(bm.SecondaryEffects, e => e is StatChangeAllEffect { Delta: 1, OnSelf: true, Chance: 10 });
    }

    [Fact]
    public void CompilesStageHelperOpsInAuthoredOrder()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Status, null,
            Op("hpCost", null, ("num", 1), ("den", 2)),
            Op("statStageReset", chance: 90, ("scope", "target")),
            Op("statStageCopy", null, ("from", "target"), ("to", "self")),
            Op("statStageSwap", null, ("group", "offense")),
            Op("statStageInvert", null, ("onSelf", false))));

        Assert.Collection(bm.SecondaryEffects,
            e => Assert.Equal(new HpCostEffect(new Fraction(1, 2), AllowFaint: false), e),
            e => Assert.Equal(new StatResetEffect(StageEffectScope.Target) { Chance = 90 }, e),
            e => Assert.Equal(new StatCopyEffect(StageEffectScope.Target, StageEffectScope.Self), e),
            e => Assert.Equal(new StatSwapEffect(StageSwapGroup.Offense), e),
            e => Assert.Equal(new StatInvertEffect(OnSelf: false), e));
    }

    [Theory]
    [InlineData("accuracy", StatKind.Accuracy)]
    [InlineData("evasion", StatKind.Evasion)]
    [InlineData("acc", StatKind.Accuracy)]
    [InlineData("eva", StatKind.Evasion)]
    public void CompilesAccuracyAndEvasionStatStageOps(string stat, StatKind expected)
    {
        BattleMove bm = MoveCompiler.ToBattleMove(
            Move(DamageClass.Status, null, Op("statStage", chance: 100, ("stat", stat), ("delta", 1), ("onSelf", true))));

        Assert.Equal(expected, bm.StageEffect!.Stat);
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
    public void CompilesDamageStatOverrideOp()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 80,
            Op("damage"),
            Op("damageStatOverride", null, ("offensiveStat", "def"), ("defensiveStat", "spd"))));

        Assert.Equal(StatKind.Def, bm.OffensiveStatOverride);
        Assert.Equal(StatKind.Spd, bm.DefensiveStatOverride);
    }

    [Fact]
    public void CompilesTargetHpThresholdPowerOp()
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Special, 65,
            Op("damage"),
            Op("targetHpThresholdPower", null,
                ("thresholdNum", 1), ("thresholdDen", 2),
                ("multiplierNum", 2), ("multiplierDen", 1))));

        Assert.Equal(new TargetHpThresholdPower(new Fraction(1, 2), new Fraction(2, 1)), bm.TargetHpThresholdPower);
    }

    [Theory]
    [InlineData("user", HpRatioPowerSource.User)]
    [InlineData("target", HpRatioPowerSource.Target)]
    public void CompilesHpRatioPowerOp(string source, HpRatioPowerSource expected)
    {
        BattleMove bm = MoveCompiler.ToBattleMove(Move(DamageClass.Special, 150,
            Op("damage"),
            Op("hpRatioPower", null, ("source", source))));

        Assert.Equal(new HpRatioPower(expected), bm.HpRatioPower);
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
    public void StatStageAll_InvalidParamsThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStageAll"))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStageAll", null, ("delta", 0)))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStageAll", null, ("delta", 1), ("stat", "atk")))));
    }

    [Fact]
    public void StageHelperOps_InvalidParamsThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("hpCost", null, ("num", 0), ("den", 2)))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("hpCost", chance: 50, ("num", 1), ("den", 2)))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStageReset"))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStageCopy", null, ("from", "both"), ("to", "self")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStageCopy", null, ("from", "self"), ("to", "self")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStageSwap", null, ("group", "speed")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Status, null, Op("statStageInvert", null, ("scope", "target")))));
    }

    [Fact]
    public void DamageStatOverride_InvalidParamsThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 80, Op("damageStatOverride"))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 80, Op("damageStatOverride", chance: 50, ("offensiveStat", "def")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 80, Op("damageStatOverride", null, ("offensiveStat", "spe")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 80, Op("damageStatOverride", null, ("defensiveStat", "atk")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Physical, 80, Op("damageStatOverride", null, ("offensiveStat", "def"), ("extra", "x")))));
    }

    [Fact]
    public void TargetHpThresholdPower_InvalidParamsThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, 65, Op("targetHpThresholdPower"))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, 65, Op("targetHpThresholdPower", chance: 50,
                ("thresholdNum", 1), ("thresholdDen", 2), ("multiplierNum", 2), ("multiplierDen", 1)))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, 65, Op("targetHpThresholdPower", null,
                ("thresholdNum", 1), ("thresholdDen", 0), ("multiplierNum", 2), ("multiplierDen", 1)))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, 65, Op("targetHpThresholdPower", null,
                ("thresholdNum", 1), ("thresholdDen", 2), ("multiplierNum", 2), ("multiplierDen", 1), ("extra", 1)))));
    }

    [Fact]
    public void HpRatioPower_InvalidParamsThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, 150, Op("hpRatioPower"))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, 150, Op("hpRatioPower", chance: 50, ("source", "user")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, 150, Op("hpRatioPower", null, ("source", "bench")))));
        Assert.Throws<ArgumentException>(() =>
            MoveCompiler.ToBattleMove(Move(DamageClass.Special, 150, Op("hpRatioPower", null, ("source", "user"), ("extra", 1)))));
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
