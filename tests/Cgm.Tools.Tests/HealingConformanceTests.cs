using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

/// <summary>15G-4 healing cohorts: each certified row compiles to one typed HealEffect with its
/// authored recipient, base fraction, and environment replacement table.</summary>
public sealed class HealingConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("HealingConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        HealEffect heal = Assert.Single(move.SecondaryEffects.OfType<HealEffect>());
        Assert.True(Enum.IsDefined(heal.Recipient));
        Assert.True(heal.Fraction.Num > 0 && heal.Fraction.Den > 0);
        Assert.Equal(DamageClass.Status, move.DamageClass);
        Assert.Null(move.Power);
    }

    [Fact]
    public void PlainSelfHealCohortHealsHalfOfMaxHp()
    {
        HealEffect[] heals = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("HealingConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey))
                .SecondaryEffects.OfType<HealEffect>().Single())
            .Where(heal => heal.Recipient == HpFractionRecipient.Self)
            .ToArray();

        Assert.NotEmpty(heals);
        Assert.All(heals, heal => Assert.Equal(heal.Fraction.Den, heal.Fraction.Num * 2));
    }

    [Fact]
    public void WeatherScaledHealsCarryABoostAboveTheBaseFraction()
    {
        HealEffect[] weatherHeals = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("HealingConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey))
                .SecondaryEffects.OfType<HealEffect>().Single())
            .Where(heal => heal.WeatherFractions is not null)
            .ToArray();

        Assert.NotEmpty(weatherHeals); // Morning Sun/Synthesis/Moonlight/Shore Up
        Assert.All(weatherHeals, heal =>
        {
            Assert.All(heal.WeatherFractions!.Values, f => Assert.True(f.Num > 0 && f.Num <= f.Den));
            // at least one weather heals strictly more than the 1/2 base (the favoured weather).
            Assert.Contains(heal.WeatherFractions!.Values, f => f.Num * heal.Fraction.Den > heal.Fraction.Num * f.Den);
        });
    }

    [Fact]
    public void SelectedTargetHealCohortUsesHalfFractionAndAuthoredTerrainReplacement()
    {
        BattleMove[] moves = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("HealingConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .Where(move => move.Target == MoveTarget.Selected
                && move.SecondaryEffects.OfType<HealEffect>().Single().Recipient == HpFractionRecipient.Target)
            .ToArray();

        Assert.NotEmpty(moves);
        Assert.All(moves, move =>
        {
            Assert.Equal(MoveTarget.Selected, move.Target);
            HealEffect heal = move.SecondaryEffects.OfType<HealEffect>().Single();
            Assert.Equal(heal.Fraction.Den, heal.Fraction.Num * 2);
        });
        Assert.Contains(moves.SelectMany(move => move.SecondaryEffects).OfType<HealEffect>(),
            heal => heal.TerrainFractions is not null
                && heal.TerrainFractions.TryGetValue(Terrain.Grassy, out Fraction fraction)
                && fraction == new Fraction(2, 3));
    }

    [Fact]
    public void UserAndAlliesHealCohortUsesTargetRecipientAndQuarterFraction()
    {
        BattleMove[] moves = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("HealingConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .Where(move => move.Target == MoveTarget.UserAndAllies)
            .ToArray();

        Assert.NotEmpty(moves);
        Assert.All(moves, move => Assert.Equal(
            new HealEffect(new Fraction(1, 4), HpFractionRecipient.Target),
            Assert.Single(move.SecondaryEffects.OfType<HealEffect>())));
    }

    private static MoveConformanceCatalog Catalog() => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(RepoRoot(), "docs", "move-conformance", "definitions.v1.json")));

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CreatureGameMaker.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
