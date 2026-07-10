using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleTargetScopeKind { ActiveCreature, Side, Field }

public readonly record struct BattleTargetScope(BattleTargetScopeKind Kind, BattleSide? Side = null);

public static class BattleTargetResolver
{
    public static BattleTargetScope ResolveSinglesScope(MoveTarget target, BattleSide sourceSide) => target switch
    {
        MoveTarget.User or MoveTarget.Selected or MoveTarget.AllOpponents or MoveTarget.AllOtherPokemon =>
            new BattleTargetScope(BattleTargetScopeKind.ActiveCreature),
        MoveTarget.UsersField => new BattleTargetScope(BattleTargetScopeKind.Side, sourceSide),
        MoveTarget.EntireField => new BattleTargetScope(BattleTargetScopeKind.Field),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown move target."),
    };

    public static bool IsSinglesActiveCreatureTarget(MoveTarget target) => target switch
    {
        MoveTarget.User or MoveTarget.Selected or MoveTarget.AllOpponents or MoveTarget.AllOtherPokemon => true,
        MoveTarget.UsersField or MoveTarget.EntireField => false,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown move target."),
    };

    public static BattleSide ResolveSinglesActiveCreatureSide(MoveTarget target, BattleSide sourceSide) => target switch
    {
        MoveTarget.User => sourceSide,
        MoveTarget.Selected or MoveTarget.AllOpponents or MoveTarget.AllOtherPokemon => Opponent(sourceSide),
        MoveTarget.UsersField or MoveTarget.EntireField =>
            throw new InvalidOperationException($"Move target '{target}' does not resolve to an active creature."),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown move target."),
    };

    private static BattleSide Opponent(BattleSide side) =>
        side == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;
}
