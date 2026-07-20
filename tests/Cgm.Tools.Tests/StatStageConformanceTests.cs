using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

/// <summary>15G-4 stat-stage cohort: each certified row compiles to chance-bearing stat-stage changes.
/// The damaging attackers lower a target stat as a sub-100% secondary (Shadow Ball/Crunch/Bubblebeam/
/// Moonblast, …); the generic assertion also covers guaranteed drops and self-buffs that carry the op.</summary>
public sealed class StatStageConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("StatStageConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        StatChangeEffect[] stages = move.SecondaryEffects.OfType<StatChangeEffect>().ToArray();
        Assert.NotEmpty(stages);
        Assert.All(stages, stage =>
        {
            Assert.NotEqual(0, stage.Delta);
            Assert.InRange(stage.Chance, 1, 100);
        });
    }

    [Fact]
    public void DamagingAttackersLowerATargetStatAsASubHundredSecondary()
    {
        BattleMove[] attackers = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("StatStageConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .Where(move => move.Power > 0
                && move.SecondaryEffects.OfType<StatChangeEffect>().Any(s => !s.OnSelf && s.Delta < 0 && s.Chance < 100))
            .ToArray();

        Assert.NotEmpty(attackers); // Shadow Ball, Crunch, Bubblebeam, Moonblast, …
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
