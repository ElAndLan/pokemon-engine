using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class ItemMutationConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("ItemMutationConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        ItemMutationEffect mutation = Assert.Single(move.SecondaryEffects.OfType<ItemMutationEffect>());
        Assert.Contains(mutation.Operation, new[]
        {
            BattleItemOperation.Give,
            BattleItemOperation.Steal,
            BattleItemOperation.Swap,
            BattleItemOperation.Restore,
        });
        Assert.Contains(entry.TestIds, id => id == $"ItemMutationConformanceTests.Certified({referenceKey})");
    }

    [Fact]
    public void CertifiedCohortCoversTransferAndRestoreFamilies()
    {
        ItemMutationEffect[] effects = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("ItemMutationConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .SelectMany(move => move.SecondaryEffects.OfType<ItemMutationEffect>()).ToArray();

        Assert.Equal(6, effects.Length);
        Assert.Contains(effects, effect => effect.Operation == BattleItemOperation.Give);
        Assert.Contains(effects, effect => effect.Operation == BattleItemOperation.Steal);
        Assert.Contains(effects, effect => effect.Operation == BattleItemOperation.Swap);
        Assert.Contains(effects, effect => effect.Operation == BattleItemOperation.Restore);
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
