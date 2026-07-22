using Cgm.Runtime;
using Cgm.Runtime.Engine;
using Silk.NET.Input;

namespace Cgm.Runtime.Tests;

/// <summary>The host's keyboard map. Extracted to a pure function and tested exhaustively because
/// the earlier inline version silently omitted Left/Right/Cancel/Menu, making side movement and the
/// menu impossible in the actual game while every headless test still passed.</summary>
public sealed class KeyboardActionsTests
{
    private static IReadOnlyList<GameAction> For(params Key[] held)
    {
        var set = held.ToHashSet();
        return RuntimeHost.KeyboardActions(set.Contains).ToList();
    }

    [Theory]
    [InlineData(Key.Up, GameAction.Up)]
    [InlineData(Key.W, GameAction.Up)]
    [InlineData(Key.Down, GameAction.Down)]
    [InlineData(Key.S, GameAction.Down)]
    [InlineData(Key.Left, GameAction.Left)]
    [InlineData(Key.A, GameAction.Left)]
    [InlineData(Key.Right, GameAction.Right)]
    [InlineData(Key.D, GameAction.Right)]
    [InlineData(Key.Enter, GameAction.Confirm)]
    [InlineData(Key.Space, GameAction.Confirm)]
    [InlineData(Key.Z, GameAction.Confirm)]
    [InlineData(Key.X, GameAction.Cancel)]
    [InlineData(Key.Backspace, GameAction.Cancel)]
    [InlineData(Key.Tab, GameAction.Menu)]
    [InlineData(Key.M, GameAction.Menu)]
    public void EachKeyMapsToItsAction(Key key, GameAction action) =>
        Assert.Contains(action, For(key));

    /// <summary>Every movement direction must be reachable — the regression that shipped was exactly
    /// a missing horizontal axis.</summary>
    [Theory]
    [InlineData(GameAction.Up)]
    [InlineData(GameAction.Down)]
    [InlineData(GameAction.Left)]
    [InlineData(GameAction.Right)]
    public void EveryDirectionHasABinding(GameAction direction) =>
        Assert.Contains(direction, RuntimeHost.KeyboardActions(_ => true));

    [Fact]
    public void NoKeysHeldYieldsNoActions() =>
        Assert.Empty(For());

    [Fact]
    public void HoldingLeftAndRightReportsBoth()
    {
        // Resolution of opposite directions is the merger's job; the map just reports what is held.
        IReadOnlyList<GameAction> actions = For(Key.Left, Key.Right);
        Assert.Contains(GameAction.Left, actions);
        Assert.Contains(GameAction.Right, actions);
    }

    /// <summary>Escape is the window's quit key and must not surface as a game action.</summary>
    [Fact]
    public void EscapeIsNotAGameAction() =>
        Assert.Empty(For(Key.Escape));

    [Fact]
    public void NullPredicateIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => RuntimeHost.KeyboardActions(null!).ToList());
}
