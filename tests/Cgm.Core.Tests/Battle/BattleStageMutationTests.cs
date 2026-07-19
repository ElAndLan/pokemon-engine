using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStageMutationTests
{
    [Fact]
    public void SetResetMaximizeAndInvertRespectBounds()
    {
        var current = Stages(atk: 3, def: -2, spe: 6);

        Assert.Equal(0, BattleStageMutation.SetTo(current, 0)[StatKind.Atk]);
        Assert.Equal(6, BattleStageMutation.Maximize(current)[StatKind.Def]);
        Assert.Equal(-3, BattleStageMutation.Invert(current)[StatKind.Atk]);
        Assert.Equal(2, BattleStageMutation.Invert(current)[StatKind.Def]);
        // Clamp: setting above the ceiling saturates.
        Assert.Equal(6, BattleStageMutation.SetTo(current, 99)[StatKind.Spe]);
    }

    [Fact]
    public void SetToOnlyTouchesTheChosenSubset()
    {
        var current = Stages(atk: 1, def: 1, spa: 1);
        IReadOnlyDictionary<StatKind, int> result = BattleStageMutation.SetTo(current, 0, [StatKind.Atk]);

        Assert.Equal(0, result[StatKind.Atk]);
        Assert.Equal(1, result[StatKind.Def]);
        Assert.Equal(1, result[StatKind.Spa]);
    }

    [Fact]
    public void CopyOverwritesSubjectWithSourceSnapshot()
    {
        var source = Stages(atk: 4, spe: -1);
        var subject = Stages(atk: -6, spe: 6, def: 2);

        IReadOnlyDictionary<StatKind, int> result = BattleStageMutation.Copy(source, subject);

        Assert.Equal(4, result[StatKind.Atk]);
        Assert.Equal(-1, result[StatKind.Spe]);
        Assert.Equal(0, result[StatKind.Def]); // source had no Def boost
    }

    [Fact]
    public void SwapExchangesBothOwnersFromOneSnapshot()
    {
        var a = Stages(atk: 3, def: -1);
        var b = Stages(atk: -2, def: 5);

        (IReadOnlyDictionary<StatKind, int> newA, IReadOnlyDictionary<StatKind, int> newB) =
            BattleStageMutation.Swap(a, b);

        Assert.Equal(-2, newA[StatKind.Atk]);
        Assert.Equal(5, newA[StatKind.Def]);
        Assert.Equal(3, newB[StatKind.Atk]);
        Assert.Equal(-1, newB[StatKind.Def]);
    }

    [Fact]
    public void StealTakesOnlyPositiveBoostsAndZeroesThem()
    {
        var user = Stages(atk: 1, def: -2);
        var target = Stages(atk: 4, def: 3, spe: -1);

        (IReadOnlyDictionary<StatKind, int> newUser, IReadOnlyDictionary<StatKind, int> newTarget) =
            BattleStageMutation.Steal(user, target);

        Assert.Equal(5, newUser[StatKind.Atk]);   // 1 + 4
        Assert.Equal(1, newUser[StatKind.Def]);   // -2 + 3
        Assert.Equal(0, newTarget[StatKind.Atk]);  // positive removed
        Assert.Equal(0, newTarget[StatKind.Def]);
        Assert.Equal(-1, newTarget[StatKind.Spe]); // negative untouched
    }

    [Fact]
    public void StealClampsTheUserGainToTheCeiling()
    {
        var user = Stages(atk: 5);
        var target = Stages(atk: 4);

        (IReadOnlyDictionary<StatKind, int> newUser, _) = BattleStageMutation.Steal(user, target);

        Assert.Equal(6, newUser[StatKind.Atk]); // 5 + 4 clamped to +6
    }

    [Theory]
    [InlineData(2, 4, 3)]     // (2+4)/2 = 3
    [InlineData(1, 2, 1)]     // floor(1.5) = 1
    [InlineData(-1, 2, 0)]    // floor(0.5) = 0
    [InlineData(-3, -2, -3)]  // floor(-2.5) = -3
    public void AverageUsesFloorAndAppliesToBothOwners(int a, int b, int expected)
    {
        var setA = Stages(atk: a);
        var setB = Stages(atk: b);

        (IReadOnlyDictionary<StatKind, int> newA, IReadOnlyDictionary<StatKind, int> newB) =
            BattleStageMutation.Average(setA, setB);

        Assert.Equal(expected, newA[StatKind.Atk]);
        Assert.Equal(expected, newB[StatKind.Atk]);
    }

    [Fact]
    public void RandomRaiseDrawsOnceInEnumOrderAndSkipsMaxedStats()
    {
        // Atk already maxed -> eligible pool starts at Def; a draw of 0 selects Def (enum order).
        var current = Stages(atk: 6);
        (StatKind? chosen, IReadOnlyDictionary<StatKind, int> result) =
            BattleStageMutation.RandomRaise(current, 2, new OneDrawRng(0));

        Assert.Equal(StatKind.Def, chosen);
        Assert.Equal(2, result[StatKind.Def]);
        Assert.Equal(6, result[StatKind.Atk]); // untouched
    }

    [Fact]
    public void RandomRaiseWithEmptyPoolChangesNothing()
    {
        var current = BattleStageMutation.Maximize(Stages()); // all at +6
        var rng = new OneDrawRng(0);

        (StatKind? chosen, IReadOnlyDictionary<StatKind, int> result) =
            BattleStageMutation.RandomRaise(current, 2, rng);

        Assert.Null(chosen);
        Assert.Same(current, result);
        Assert.Equal(0, rng.Draws); // no draw taken for an empty pool
    }

    [Fact]
    public void InvalidStatSubsetsAndSnapshotsAreRejected()
    {
        var current = Stages();
        Assert.Throws<ArgumentException>(() => BattleStageMutation.SetTo(current, 0, [StatKind.Hp]));
        Assert.Throws<ArgumentException>(() => BattleStageMutation.SetTo(current, 0, []));
        Assert.Throws<ArgumentException>(() =>
            BattleStageMutation.SetTo(current, 0, [StatKind.Atk, StatKind.Atk]));
        Assert.Throws<ArgumentException>(() =>
            BattleStageMutation.SetTo(new Dictionary<StatKind, int> { [StatKind.Atk] = 0 }, 0)); // incomplete snapshot
        Assert.Throws<ArgumentException>(() =>
            BattleStageMutation.SetTo(Stages(atk: 99), 0)); // out-of-range snapshot value
    }

    private static IReadOnlyDictionary<StatKind, int> Stages(int atk = 0, int def = 0, int spa = 0,
        int spd = 0, int spe = 0, int accuracy = 0, int evasion = 0) => new Dictionary<StatKind, int>
    {
        [StatKind.Atk] = atk,
        [StatKind.Def] = def,
        [StatKind.Spa] = spa,
        [StatKind.Spd] = spd,
        [StatKind.Spe] = spe,
        [StatKind.Accuracy] = accuracy,
        [StatKind.Evasion] = evasion,
    };

    private sealed class OneDrawRng(int value) : IRng
    {
        public int Draws { get; private set; }
        public int Next(int maxExclusive) { Draws++; return value % maxExclusive; }
        public int Next(int minInclusive, int maxExclusive) { Draws++; return minInclusive; }
        public double NextDouble() => 0;
    }
}
