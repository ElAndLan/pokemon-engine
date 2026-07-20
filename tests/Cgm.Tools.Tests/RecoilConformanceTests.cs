using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

/// <summary>15G-4 recoil cohort: each certified row is a damaging move that hurts the user by a
/// fraction of the damage dealt (Take Down/Double-Edge/Brave Bird/Head Smash/Light of Ruin, …).</summary>
public sealed class RecoilConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("RecoilConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        RecoilEffect recoil = Assert.Single(move.SecondaryEffects.OfType<RecoilEffect>());
        Assert.True(recoil.Fraction.Num > 0 && recoil.Fraction.Num <= recoil.Fraction.Den); // in (0,1]
        Assert.True(move.Power > 0);                    // recoil rides a damaging hit
        Assert.NotEqual(DamageClass.Status, move.DamageClass);
    }

    [Fact]
    public void RecoilCohortCoversQuarterThirdAndHalfFractions()
    {
        Fraction[] fractions = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("RecoilConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey))
                .SecondaryEffects.OfType<RecoilEffect>().Single().Fraction)
            .ToArray();

        Assert.Contains(fractions, f => f.Num * 4 == f.Den);   // 1/4 (Take Down / Wild Charge)
        Assert.Contains(fractions, f => f.Num * 2 == f.Den);   // 1/2 (Head Smash / Light of Ruin)
        Assert.Contains(fractions, f => f.Num * 100 == f.Den * 33); // 33/100 (Double-Edge family)
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
