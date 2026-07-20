using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

/// <summary>15G-4 secondary-ailment cohort: each certified row compiles to a chance-bearing ailment
/// (persistent status or confusion). Covers the classic status-chance attackers plus every other move
/// that carries an ailment op.</summary>
public sealed class SecondaryAilmentConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("SecondaryAilmentConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        MoveEffect[] ailments = move.SecondaryEffects
            .Where(effect => effect is AilmentEffect or ConfusionEffect).ToArray();
        Assert.NotEmpty(ailments);
        Assert.All(ailments, effect => Assert.InRange(effect.Chance, 1, 100));
    }

    [Fact]
    public void DamagingAttackerCohortAppliesAPersistentStatusOnHit()
    {
        // The added-effect attackers (Thunderbolt/Ice Beam/Flamethrower/Sludge Bomb/the elemental
        // punches/Lick) are damaging moves whose ailment is a chance-based secondary, not guaranteed.
        BattleMove[] attackers = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("SecondaryAilmentConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .Where(move => move.Power > 0 && move.SecondaryEffects.OfType<AilmentEffect>().Any())
            .ToArray();

        Assert.NotEmpty(attackers);
        Assert.Contains(attackers, move => move.SecondaryEffects.OfType<AilmentEffect>().Single().Chance < 100);
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
