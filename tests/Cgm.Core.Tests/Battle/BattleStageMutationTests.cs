using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStageMutationTests
{
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
        (IReadOnlyDictionary<StatKind, int> newUser, _) =
            BattleStageMutation.Steal(Stages(atk: 5), Stages(atk: 4));

        Assert.Equal(6, newUser[StatKind.Atk]); // 5 + 4 clamped to +6
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
        var current = Stages(atk: 6, def: 6, spa: 6, spd: 6, spe: 6, accuracy: 6, evasion: 6);
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
        Assert.Throws<ArgumentException>(() => BattleStageMutation.Steal(current, current, [StatKind.Hp]));
        Assert.Throws<ArgumentException>(() => BattleStageMutation.Steal(current, current, []));
        Assert.Throws<ArgumentException>(() =>
            BattleStageMutation.RandomRaise(current, 2, new OneDrawRng(0), [StatKind.Atk, StatKind.Atk]));
        Assert.Throws<ArgumentException>(() =>
            BattleStageMutation.RandomRaise(new Dictionary<StatKind, int> { [StatKind.Atk] = 0 }, 2, new OneDrawRng(0)));
        Assert.Throws<ArgumentException>(() =>
            BattleStageMutation.RandomRaise(Stages(atk: 99), 2, new OneDrawRng(0))); // out-of-range snapshot value
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
