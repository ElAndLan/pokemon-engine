using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

/// <summary>15G-4 drain cohort: each certified row is a damaging move that heals the user by a
/// fraction of the damage dealt (Absorb/Giga Drain/Drain Punch/Draining Kiss/Oblivion Wing, …).</summary>
public sealed class DrainConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("DrainConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        DrainEffect drain = Assert.Single(move.SecondaryEffects.OfType<DrainEffect>());
        Assert.True(drain.Fraction.Num > 0 && drain.Fraction.Num <= drain.Fraction.Den); // in (0,1]
        Assert.True(move.Power > 0);                    // drain rides a damaging hit
        Assert.NotEqual(DamageClass.Status, move.DamageClass);
    }

    [Fact]
    public void DrainCohortCoversHalfAndThreeQuarterFractions()
    {
        Fraction[] fractions = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("DrainConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey))
                .SecondaryEffects.OfType<DrainEffect>().Single().Fraction)
            .ToArray();

        Assert.Contains(fractions, f => f.Num * 2 == f.Den);   // 1/2 (Absorb/Giga Drain/…)
        Assert.Contains(fractions, f => f.Num * 4 == f.Den * 3); // 3/4 (Draining Kiss/Oblivion Wing)
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
