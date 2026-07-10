using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTargetResolverTests
{
    [Theory]
    [InlineData(MoveTarget.User, 1)]
    [InlineData(MoveTarget.Selected, 1)]
    [InlineData(MoveTarget.AllOpponents, 1)]
    [InlineData(MoveTarget.AllOtherPokemon, 1)]
    public void ResolveSinglesScope_ClassifiesActiveSlotTargets(MoveTarget target, int count)
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveSinglesScope(target, BattleSide.Player);

        Assert.Equal(BattleTargetScopeKind.ActiveSlots, scope.Kind);
        Assert.Equal(count, scope.Slots.Count);
        Assert.Null(scope.Side);
    }

    [Fact]
    public void ResolveSinglesScope_ClassifiesUsersFieldAsSourceSide()
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveSinglesScope(MoveTarget.UsersField, BattleSide.Enemy);

        Assert.Equal(BattleTargetScopeKind.Side, scope.Kind);
        Assert.Empty(scope.Slots);
        Assert.Equal(BattleSide.Enemy, scope.Side);
    }

    [Fact]
    public void ResolveSinglesScope_ClassifiesEntireField()
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveSinglesScope(MoveTarget.EntireField, BattleSide.Player);

        Assert.Equal(BattleTargetScopeKind.Field, scope.Kind);
        Assert.Empty(scope.Slots);
        Assert.Null(scope.Side);
    }

    [Theory]
    [InlineData(MoveTarget.User, BattleSide.Player)]
    [InlineData(MoveTarget.Selected, BattleSide.Enemy)]
    [InlineData(MoveTarget.AllOpponents, BattleSide.Enemy)]
    [InlineData(MoveTarget.AllOtherPokemon, BattleSide.Enemy)]
    public void ResolveSinglesActiveCreatureSide_MapsKnownCreatureTargets(MoveTarget target, BattleSide expected)
    {
        Assert.True(BattleTargetResolver.IsSinglesActiveCreatureTarget(target));
        Assert.Equal(expected, BattleTargetResolver.ResolveSinglesActiveCreatureSide(target, BattleSide.Player));
    }

    [Theory]
    [InlineData(MoveTarget.UsersField)]
    [InlineData(MoveTarget.EntireField)]
    public void ResolveSinglesActiveCreatureSide_RejectsFieldScopes(MoveTarget target)
    {
        Assert.False(BattleTargetResolver.IsSinglesActiveCreatureTarget(target));
        Assert.Throws<InvalidOperationException>(() =>
            BattleTargetResolver.ResolveSinglesActiveCreatureSide(target, BattleSide.Player));
    }

    [Fact]
    public void ResolveSinglesActiveCreatureSide_RejectsUnknownTarget()
    {
        var target = (MoveTarget)999;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleTargetResolver.ResolveSinglesScope(target, BattleSide.Player));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleTargetResolver.IsSinglesActiveCreatureTarget(target));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleTargetResolver.ResolveSinglesActiveCreatureSide(target, BattleSide.Player));
    }

    [Fact]
    public void ResolveScope_DoublesUsesStableTopologyOrderForSpreadTargets()
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveScope(
            MoveTarget.AllOtherPokemon,
            BattleTopology.Doubles,
            new BattleSlot(BattleSide.Enemy, 1));

        Assert.Equal(BattleTargetScopeKind.ActiveSlots, scope.Kind);
        Assert.Equal(
            [
                new BattleSlot(BattleSide.Player, 0),
                new BattleSlot(BattleSide.Player, 1),
                new BattleSlot(BattleSide.Enemy, 0),
            ],
            scope.Slots);
    }

    [Fact]
    public void ResolveScope_DoublesSupportsAllyAndExplicitSelection()
    {
        BattleSlot source = new(BattleSide.Player, 0);
        BattleSlot ally = new(BattleSide.Player, 1);

        BattleTargetScope scope = BattleTargetResolver.ResolveScope(MoveTarget.Ally, BattleTopology.Doubles, source, ally);

        Assert.Equal(BattleTargetScopeKind.ActiveSlots, scope.Kind);
        Assert.Equal([ally], scope.Slots);
    }

    [Fact]
    public void ResolveScope_RandomOpponentDefersItsSingleDraw()
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveScope(
            MoveTarget.RandomOpponent,
            BattleTopology.Doubles,
            new BattleSlot(BattleSide.Player, 1));

        Assert.Equal(BattleTargetSelection.RandomOpponent, scope.Selection);
        Assert.Equal([new BattleSlot(BattleSide.Enemy, 0), new BattleSlot(BattleSide.Enemy, 1)], scope.Slots);
    }

    [Theory]
    [InlineData(MoveTarget.UsersField, BattleTargetScopeKind.Side, BattleSide.Player)]
    [InlineData(MoveTarget.OpponentsField, BattleTargetScopeKind.Side, BattleSide.Enemy)]
    [InlineData(MoveTarget.EntireField, BattleTargetScopeKind.Field, null)]
    [InlineData(MoveTarget.FaintingPokemon, BattleTargetScopeKind.FaintedParty, BattleSide.Player)]
    [InlineData(MoveTarget.SpecificMove, BattleTargetScopeKind.MoveReference, null)]
    public void ResolveScope_ClassifiesNonActiveScopes(MoveTarget target, BattleTargetScopeKind kind, BattleSide? side)
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveScope(
            target, BattleTopology.Doubles, new BattleSlot(BattleSide.Player, 0));

        Assert.Equal(kind, scope.Kind);
        Assert.Equal(side, scope.Side);
        Assert.Empty(scope.Slots);
    }

    [Fact]
    public void ResolveScope_RejectsMissingOrInvalidSelectedAlly()
    {
        BattleSlot source = new(BattleSide.Player, 0);

        Assert.Throws<ArgumentException>(() =>
            BattleTargetResolver.ResolveScope(MoveTarget.Ally, BattleTopology.Doubles, source));
        Assert.Throws<ArgumentException>(() =>
            BattleTargetResolver.ResolveScope(MoveTarget.Ally, BattleTopology.Doubles, source, new BattleSlot(BattleSide.Enemy, 0)));
    }
}
