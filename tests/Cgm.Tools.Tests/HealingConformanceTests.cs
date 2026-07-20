using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

/// <summary>15G-4 healing cohort: each certified row compiles to a self HealEffect of its authored
/// fraction (the plain 50% self-recovery family — Recover/Soft-Boiled/Milk Drink/Slack Off/Heal Order).</summary>
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
        Assert.Equal(HpFractionRecipient.Self, heal.Recipient);
        Assert.True(heal.Fraction.Num > 0 && heal.Fraction.Den > 0);
        Assert.Equal(DamageClass.Status, move.DamageClass);
        Assert.Null(move.Power);
    }

    [Fact]
    public void PlainSelfHealCohortHealsHalfOfMaxHp()
    {
        BattleMove[] moves = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("HealingConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey))).ToArray();

        Assert.NotEmpty(moves);
        Assert.All(moves, move =>
        {
            Fraction fraction = move.SecondaryEffects.OfType<HealEffect>().Single().Fraction;
            Assert.Equal(fraction.Den, fraction.Num * 2); // one-half, whether stored as 1/2 or 50/100
        });
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
