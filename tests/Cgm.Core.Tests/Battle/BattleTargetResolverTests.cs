using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTargetResolverTests
{
    [Theory]
    [InlineData(MoveTarget.User)]
    [InlineData(MoveTarget.Selected)]
    [InlineData(MoveTarget.AllOpponents)]
    [InlineData(MoveTarget.AllOtherPokemon)]
    public void ResolveSinglesScope_ClassifiesActiveCreatureTargets(MoveTarget target)
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveSinglesScope(target, BattleSide.Player);

        Assert.Equal(BattleTargetScopeKind.ActiveCreature, scope.Kind);
        Assert.Null(scope.Side);
    }

    [Fact]
    public void ResolveSinglesScope_ClassifiesUsersFieldAsSourceSide()
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveSinglesScope(MoveTarget.UsersField, BattleSide.Enemy);

        Assert.Equal(BattleTargetScopeKind.Side, scope.Kind);
        Assert.Equal(BattleSide.Enemy, scope.Side);
    }

    [Fact]
    public void ResolveSinglesScope_ClassifiesEntireField()
    {
        BattleTargetScope scope = BattleTargetResolver.ResolveSinglesScope(MoveTarget.EntireField, BattleSide.Player);

        Assert.Equal(BattleTargetScopeKind.Field, scope.Kind);
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
}
