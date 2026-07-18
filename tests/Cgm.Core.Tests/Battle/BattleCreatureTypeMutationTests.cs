using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleCreatureTypeMutationTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Water = EntityId.Parse("type:water");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Ghost = EntityId.Parse("type:ghost");
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly BattleOverlayOwner User = new(BattleSide.Player, 0, new(BattleSide.Player, 0));
    private static readonly BattleOverlayOwner Target = new(BattleSide.Enemy, 0, new(BattleSide.Enemy, 0));

    [Fact]
    public void ReplaceAddRemoveCopyProduceExpectedEffectiveTypes()
    {
        var store = new BattleOverlayStore();
        var state = new BattleCreatureTypeState(store);

        // Replace: dual Fire/Water -> Water.
        Assert.True(Mutate(state, BattleTypeOperation.Replace, [Water], Subject(User, Fire, Water)).Succeeded);
        Assert.Equal([Water], state.Effective(User, Values(Fire, Water)));

        // Add: appends Ghost onto the current effective (Water) list.
        Assert.True(Mutate(state, BattleTypeOperation.Add, [Ghost], Subject(User, Fire, Water)).Succeeded);
        Assert.Equal([Water, Ghost], state.Effective(User, Values(Fire, Water)));

        // Remove: drops Ghost, leaving Water.
        Assert.True(Mutate(state, BattleTypeOperation.Remove, [Ghost], Subject(User, Fire, Water)).Succeeded);
        Assert.Equal([Water], state.Effective(User, Values(Fire, Water)));

        // Copy: target (Grass/Ghost) types onto the user.
        Assert.True(state.Mutate(BattleTypeOperation.Copy, null, Subject(User, Fire, Water),
            Subject(Target, Grass, Ghost), 1, 1).Succeeded);
        Assert.Equal([Grass, Ghost], state.Effective(User, Values(Fire, Water)));
    }

    [Fact]
    public void AddDeduplicatesPreservingFirstOccurrence()
    {
        var store = new BattleOverlayStore();
        var state = new BattleCreatureTypeState(store);

        // Fire/Water base; add Fire (already present) + Ghost -> Fire, Water, Ghost (Fire keeps its slot).
        BattleTypeMutationResult result = Mutate(state, BattleTypeOperation.Add, [Fire, Ghost],
            Subject(User, Fire, Water));

        Assert.True(result.Succeeded);
        Assert.Equal([Fire, Water, Ghost], result.After);
    }

    [Fact]
    public void EmptyResultUsesFallbackOrFailsWithoutWriting()
    {
        var store = new BattleOverlayStore();
        var noFallback = new BattleCreatureTypeState(store);
        Assert.Equal(BattleTypeMutationFailure.WouldEmptyTypes,
            Mutate(noFallback, BattleTypeOperation.Remove, [Fire], Subject(User, Fire)).Failure);
        Assert.Empty(store.Snapshot());

        var withFallback = new BattleCreatureTypeState(store, emptyFallback: Normal);
        BattleTypeMutationResult result = Mutate(withFallback, BattleTypeOperation.Remove, [Fire],
            Subject(User, Fire));
        Assert.True(result.Succeeded);
        Assert.Equal([Normal], result.After);
        Assert.Equal([Normal], withFallback.Effective(User, Values(Fire)));
    }

    [Fact]
    public void ExceedingMaxTypeCountFailsWithoutWriting()
    {
        var store = new BattleOverlayStore();
        var state = new BattleCreatureTypeState(store, maxTypes: 2);

        Assert.Equal(BattleTypeMutationFailure.ExceedsMax,
            Mutate(state, BattleTypeOperation.Add, [Ghost], Subject(User, Fire, Water)).Failure);
        Assert.Empty(store.Snapshot());
    }

    [Theory]
    [InlineData(BattleTypeOperation.Remove, "type:grass")] // absent type -> no change
    [InlineData(BattleTypeOperation.Add, "type:fire")]     // already present -> no change
    [InlineData(BattleTypeOperation.Replace, "type:fire")] // same list -> no change
    public void NoOpMutationsFailWithoutWriting(BattleTypeOperation operation, string typeText)
    {
        var store = new BattleOverlayStore();
        var state = new BattleCreatureTypeState(store);

        Assert.Equal(BattleTypeMutationFailure.NoChange,
            Mutate(state, operation, [EntityId.Parse(typeText)], Subject(User, Fire)).Failure);
        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void FaintedSubjectOrCopySourceFailsWithoutWriting()
    {
        var store = new BattleOverlayStore();
        var state = new BattleCreatureTypeState(store);

        Assert.Equal(BattleTypeMutationFailure.Fainted, state.Mutate(BattleTypeOperation.Replace, [Water],
            (User, Values(Fire), true), null, 1, 1).Failure);
        Assert.Equal(BattleTypeMutationFailure.Fainted, state.Mutate(BattleTypeOperation.Copy, null,
            Subject(User, Fire), (Target, Values(Grass), true), 1, 1).Failure);
        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void CopyRequiresDistinctSourceAndListOpsRejectBadShape()
    {
        var store = new BattleOverlayStore();
        var state = new BattleCreatureTypeState(store);

        Assert.Throws<ArgumentException>(() => state.Mutate(BattleTypeOperation.Copy, null,
            Subject(User, Fire), Subject(User, Fire), 1, 1));
        Assert.Throws<ArgumentException>(() => state.Mutate(BattleTypeOperation.Copy, [Water],
            Subject(User, Fire), Subject(Target, Grass), 1, 1));
        Assert.Throws<ArgumentException>(() => Mutate(state, BattleTypeOperation.Replace, [], Subject(User, Fire)));
        Assert.Throws<ArgumentException>(() => Mutate(state, BattleTypeOperation.Replace, [Water, Water],
            Subject(User, Fire)));
        Assert.Throws<ArgumentException>(() => state.Mutate(BattleTypeOperation.Replace,
            [EntityId.Parse("ability:x")], Subject(User, Fire), null, 1, 1));
    }

    [Fact]
    public void MutationClearsOnSwitchFaintAndBattleEndRestoringBaseTypes()
    {
        foreach (Action<BattleOverlayStore> cleanup in new Action<BattleOverlayStore>[]
        {
            s => s.OwnerSwitched(BattleSide.Player, 0, null, 2, 0),
            s => s.OwnerFainted(BattleSide.Player, 0, 2, 0),
            s => s.EndBattle(2, 0),
        })
        {
            var store = new BattleOverlayStore();
            var state = new BattleCreatureTypeState(store);
            Assert.True(Mutate(state, BattleTypeOperation.Replace, [Water], Subject(User, Fire)).Succeeded);
            Assert.Equal([Water], state.Effective(User, Values(Fire)));

            cleanup(store);

            Assert.Equal([Fire], state.Effective(User, Values(Fire)));
        }
    }

    [Fact]
    public void MutationNeverTouchesTheBaseDefinition()
    {
        var store = new BattleOverlayStore();
        var state = new BattleCreatureTypeState(store);
        BattleEffectiveValues baseValues = Values(Fire, Water);

        Assert.True(Mutate(state, BattleTypeOperation.Replace, [Grass], Subject(User, Fire, Water)).Succeeded);

        Assert.Equal([Fire, Water], baseValues.CreatureTypes);
    }

    private static BattleTypeMutationResult Mutate(BattleCreatureTypeState state, BattleTypeOperation operation,
        IReadOnlyList<EntityId> types, (BattleOverlayOwner, BattleEffectiveValues, bool) subject) =>
        state.Mutate(operation, types, subject, null, 1, 1);

    private static (BattleOverlayOwner, BattleEffectiveValues, bool) Subject(BattleOverlayOwner owner,
        params EntityId[] types) => (owner, Values(types), false);

    private static BattleEffectiveValues Values(params EntityId[] types) =>
        new(null, null, types, new Stats(100, 50, 50, 50, 50, 50), []);
}
