using Cgm.Core.Battle;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class StatMutationConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Rows()
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));

        Assert.Contains(move.SecondaryEffects, IsStatMutation);
        Assert.Contains($"StatMutationConformanceTests.Certified({referenceKey})", entry.TestIds);
    }

    [Fact]
    public void CertifiedCohortCoversEligibleStatAndMetricFamilies()
    {
        BattleMove[] moves = Rows()
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey))).ToArray();
        MoveEffect[] effects = moves.SelectMany(move => move.SecondaryEffects).ToArray();

        Assert.Equal(7, moves.Length);
        Assert.Contains(effects, effect => effect is StatStageMutationEffect
            { Operation: StatStageMutationOperation.Maximize, Stat: StatKind.Atk });
        Assert.Contains(effects, effect => effect is StatStageMutationEffect
            { Operation: StatStageMutationOperation.Random, Delta: 2 });
        Assert.Contains(effects, effect => effect is StatStageMutationEffect
            { Operation: StatStageMutationOperation.Steal, Subject: StageEffectScope.Target });
        Assert.Contains(effects, effect => effect is DerivedStatMutationEffect
            { Operation: BattleDerivedStatOperation.Split, Group: BattleDerivedStatGroup.Offense });
        Assert.Contains(effects, effect => effect is DerivedStatMutationEffect
            { Operation: BattleDerivedStatOperation.Split, Group: BattleDerivedStatGroup.Defense });
        Assert.Contains(effects, effect => effect is DerivedStatMutationEffect
            { Operation: BattleDerivedStatOperation.Swap, Stat: StatKind.Spe });
        Assert.Contains(effects, effect => effect is MetricMutationEffect
            { Operation: BattleMetricMutationOperation.Add, Subject: StageEffectScope.Self,
                Metric: BattleMetric.Weight, Value: -1000 });
        Assert.Contains(moves, move => move.SecondaryEffects.OfType<HpCostEffect>().Any());
    }

    private static bool IsStatMutation(MoveEffect effect) => effect is StatStageMutationEffect
        or DerivedStatMutationEffect or MetricMutationEffect;

    private static IEnumerable<MoveConformanceRecord> Rows() => Catalog().Entries.Where(entry =>
        entry.TestIds.Any(id => id.StartsWith("StatMutationConformanceTests.Certified(",
            StringComparison.Ordinal)));

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
