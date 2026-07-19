using Cgm.Core.Battle;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class StatMutationConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("StatMutationConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        Assert.Contains(move.SecondaryEffects, effect =>
            effect is StatStealEffect or RandomStatRaiseEffect or DerivedStatSwapEffect or DerivedStatSplitEffect);
        Assert.Contains(entry.TestIds, id => id == $"StatMutationConformanceTests.Certified({referenceKey})");
    }

    [Fact]
    public void CertifiedCohortCoversEveryStatMutationOperation()
    {
        MoveEffect[] effects = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("StatMutationConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .SelectMany(move => move.SecondaryEffects).ToArray();

        // Acupressure (random-raise) targets user-or-ally, which the target-topology harness does not yet
        // drive, so it stays proven by the engine/controller tests rather than a generated vector.
        Assert.Contains(effects, effect => effect is StatStealEffect);
        Assert.Contains(effects, effect => effect is DerivedStatSwapEffect { Stat: StatKind.Spe });
        Assert.Contains(effects, effect => effect is DerivedStatSplitEffect { Group: DerivedStatGroup.Offense });
        Assert.Contains(effects, effect => effect is DerivedStatSplitEffect { Group: DerivedStatGroup.Defense });
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
