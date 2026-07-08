using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class ProgressionTests
{
    private static CreatureInstance At(int level, string growth = "medium-fast") => new()
    {
        Species = EntityId.Parse("species:x"),
        Level = level,
        Exp = ExpCurve.TotalExp(growth, level),
    };

    private static LearnsetEntry L(int level, string move) => new(level, EntityId.Parse($"move:{move}"));

    [Fact]
    public void GainExp_LevelsUp_AndLearnsMovesInRange()
    {
        CreatureInstance c = At(5); // medium-fast level 5 = 125 exp
        var learnset = new[] { L(5, "old"), L(6, "a"), L(8, "b"), L(9, "c") };

        // enough exp to reach level 8 (216=lvl6, 343=lvl7, 512=lvl8).
        (CreatureInstance updated, LevelUpResult r) = Progression.GainExp(c, 512 - 125, "medium-fast", learnset);

        Assert.Equal(8, updated.Level);
        Assert.True(r.LeveledUp);
        Assert.Equal(5, r.OldLevel);
        // Moves at levels 6 and 8 are learned; level 5 (already had) and 9 (not reached) excluded.
        Assert.Equal([EntityId.Parse("move:a"), EntityId.Parse("move:b")], r.MovesLearned);
    }

    [Fact]
    public void GainExp_NotEnoughToLevel_NoChange()
    {
        CreatureInstance c = At(5);
        (CreatureInstance updated, LevelUpResult r) = Progression.GainExp(c, 10, "medium-fast", []);
        Assert.Equal(5, updated.Level);
        Assert.False(r.LeveledUp);
        Assert.Empty(r.MovesLearned);
        Assert.Equal(135, updated.Exp); // exp still accumulates
    }

    [Fact]
    public void GainExp_NegativeAmount_Ignored()
    {
        CreatureInstance c = At(5);
        (CreatureInstance updated, _) = Progression.GainExp(c, -100, "medium-fast", []);
        Assert.Equal(125, updated.Exp);
    }

    [Fact]
    public void ApplyEvYield_AddsAndCapsPerStatAt252()
    {
        CreatureInstance c = new() { Species = EntityId.Parse("species:x"), Evs = new Stats(250, 0, 0, 0, 0, 0) };
        CreatureInstance r = Progression.ApplyEvYield(c, new Stats(10, 0, 0, 0, 0, 0));
        Assert.Equal(252, r.Evs.Hp); // clamped to per-stat cap
    }

    [Fact]
    public void ApplyEvYield_CapsTotalAt510()
    {
        // 252 + 252 = 504 across two stats; a third stat can only gain 6 before hitting 510.
        CreatureInstance c = new() { Species = EntityId.Parse("species:x"), Evs = new Stats(252, 252, 0, 0, 0, 0) };
        CreatureInstance r = Progression.ApplyEvYield(c, new Stats(0, 0, 100, 0, 0, 0));
        Assert.Equal(6, r.Evs.Def);
        Assert.Equal(510, r.Evs.Hp + r.Evs.Atk + r.Evs.Def + r.Evs.Spa + r.Evs.Spd + r.Evs.Spe);
    }

    [Fact]
    public void ApplyEvYield_NormalGain()
    {
        CreatureInstance c = new() { Species = EntityId.Parse("species:x") };
        CreatureInstance r = Progression.ApplyEvYield(c, new Stats(0, 1, 0, 2, 0, 0));
        Assert.Equal(1, r.Evs.Atk);
        Assert.Equal(2, r.Evs.Spa);
    }
}
