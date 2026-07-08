using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>The effect-list architecture (EFFECT_TYPES_CATALOG): a move exposes its secondary effects
/// as an ordered primitive list that the resolver dispatches — not a fixed per-op pipeline.</summary>
public sealed class EffectListTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void SecondaryEffects_CompileToOrderedPrimitiveList()
    {
        var move = new BattleMove(EntityId.Parse("move:m"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0,
            ailment: PersistentStatus.Paralysis, ailmentChance: 30,
            stageEffect: new StageEffect(StatKind.Spe, -1, OnSelf: false, Chance: 100),
            confuseChance: 10, flinchChance: 20);

        Assert.Collection(move.SecondaryEffects,
            e => Assert.Equal(new AilmentEffect(PersistentStatus.Paralysis) { Chance = 30 }, e),
            e => Assert.Equal(new StatChangeEffect(StatKind.Spe, -1, false) { Chance = 100 }, e),
            e => Assert.Equal(new ConfusionEffect { Chance = 10 }, e),
            e => Assert.Equal(new FlinchEffect { Chance = 20 }, e));
    }

    [Fact]
    public void MoveWithNoSecondaries_HasEmptyEffectList()
    {
        var plain = new BattleMove(EntityId.Parse("move:tackle"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0);
        Assert.Empty(plain.SecondaryEffects);
    }

    [Fact]
    public void PostDamageOps_AppendInDeterministicOrder()
    {
        // leech, drain, heal, recoil, crit, selfDestruct — after any chance-gated secondaries.
        var move = new BattleMove(EntityId.Parse("move:kitchensink"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0,
            drain: new Fraction(1, 2), recoil: new Fraction(1, 4), heal: new Fraction(1, 3),
            critBoost: 2, selfDestruct: true, leechSeed: true);

        Assert.Collection(move.SecondaryEffects,
            e => Assert.IsType<LeechSeedEffect>(e),
            e => Assert.Equal(new DrainEffect(new Fraction(1, 2)), e),
            e => Assert.Equal(new HealEffect(new Fraction(1, 3)), e),
            e => Assert.Equal(new RecoilEffect(new Fraction(1, 4)), e),
            e => Assert.Equal(new CritBoostEffect(2), e),
            e => Assert.IsType<SelfDestructEffect>(e));
    }

    [Fact]
    public void OnMissRecoil_IsNotInTheEffectList()
    {
        // Crash-on-miss (Jump Kick) resolves in the miss branch, not as a post-damage effect.
        var jumpKick = new BattleMove(EntityId.Parse("move:jumpkick"), Normal, DamageClass.Physical, 100, 100, 25, 0, 0,
            recoil: new Fraction(1, 2), recoilOnMiss: true);
        Assert.DoesNotContain(jumpKick.SecondaryEffects, e => e is RecoilEffect);
    }

    [Fact]
    public void ZeroChanceEffects_AreNotCompiledIntoTheList()
    {
        var move = new BattleMove(EntityId.Parse("move:m"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            ailment: PersistentStatus.Burn, ailmentChance: 0,
            stageEffect: new StageEffect(StatKind.Atk, 2, true, Chance: 0));
        Assert.Empty(move.SecondaryEffects);
    }
}
