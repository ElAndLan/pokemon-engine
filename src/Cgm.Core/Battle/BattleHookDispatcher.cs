using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleHookSourceKind { Form, Ability, HeldItem, Field }

public sealed record BattleHookSource(
    BattleSlot Slot,
    BattleHookSourceKind Kind,
    AbilityHookPoint Hook,
    IReadOnlyList<Effect> Effects)
{
    public BattleSide Side => Slot.Side;
    public BattleHookSource(BattleSide side, BattleHookSourceKind kind, AbilityHookPoint hook, IReadOnlyList<Effect> effects)
        : this(new BattleSlot(side, 0), kind, hook, effects) { }
}

public sealed record BattleHookInvocation(BattleSlot Slot, BattleHookSourceKind Kind, Effect Effect)
{
    public BattleSide Side => Slot.Side;
    public BattleHookInvocation(BattleSide side, BattleHookSourceKind kind, Effect effect)
        : this(new BattleSlot(side, 0), kind, effect) { }
}

public static class BattleHookDispatcher
{
    public static IReadOnlyList<BattleHookInvocation> SwitchIn(
        BattleSide incoming,
        IEnumerable<BattleHookSource> sources) =>
        Ordered(incoming, Opponent(incoming), AbilityHookPoint.OnSwitchIn, sources);

    public static IReadOnlyList<BattleHookInvocation> SwitchIn(
        BattleSlot incoming,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.Where(source => source.Hook == AbilityHookPoint.OnSwitchIn && source.Slot == incoming).ToList();
        return
        [
            .. Emit(list, incoming, BattleHookSourceKind.Form),
            .. Emit(list, incoming, BattleHookSourceKind.Ability),
            .. Emit(list, incoming, BattleHookSourceKind.HeldItem),
            .. Emit(list, incoming, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> Damage(
        BattleSide attacker,
        IEnumerable<BattleHookSource> sources)
    {
        BattleSide defender = Opponent(attacker);
        var list = sources.ToList();
        return
        [
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyOutgoingDamage), attacker, BattleHookSourceKind.Ability),
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyOutgoingDamage), attacker, BattleHookSourceKind.HeldItem),
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyIncomingDamage), defender, BattleHookSourceKind.Ability),
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyIncomingDamage), defender, BattleHookSourceKind.HeldItem),
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyOutgoingDamage), attacker, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> Damage(
        BattleSlot attacker,
        BattleSlot defender,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.ToList();
        return
        [
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyOutgoingDamage), attacker, BattleHookSourceKind.Ability),
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyOutgoingDamage), attacker, BattleHookSourceKind.HeldItem),
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyIncomingDamage), defender, BattleHookSourceKind.Ability),
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyIncomingDamage), defender, BattleHookSourceKind.HeldItem),
            .. Emit(list.Where(s => s.Hook == AbilityHookPoint.OnModifyOutgoingDamage), attacker, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> EndOfTurn(
        BattleSide side,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.Where(s => s.Hook == AbilityHookPoint.OnEndOfTurn).ToList();
        return
        [
            .. Emit(list, side, BattleHookSourceKind.HeldItem),
            .. Emit(list, side, BattleHookSourceKind.Ability),
            .. Emit(list, side, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> EndOfTurn(
        BattleSlot slot,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.Where(s => s.Hook == AbilityHookPoint.OnEndOfTurn).ToList();
        return
        [
            .. Emit(list, slot, BattleHookSourceKind.HeldItem),
            .. Emit(list, slot, BattleHookSourceKind.Ability),
            .. Emit(list, slot, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> StatusAttempt(
        BattleSide target,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.Where(s => s.Hook == AbilityHookPoint.OnStatusAttempt).ToList();
        return
        [
            .. Emit(list, target, BattleHookSourceKind.Ability),
            .. Emit(list, target, BattleHookSourceKind.HeldItem),
            .. Emit(list, target, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> StatusAttempt(
        BattleSlot target,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.Where(s => s.Hook == AbilityHookPoint.OnStatusAttempt).ToList();
        return
        [
            .. Emit(list, target, BattleHookSourceKind.Ability),
            .. Emit(list, target, BattleHookSourceKind.HeldItem),
            .. Emit(list, target, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> ContactReceived(
        BattleSide receiver,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.Where(s => s.Hook == AbilityHookPoint.OnContactReceived).ToList();
        return
        [
            .. Emit(list, receiver, BattleHookSourceKind.Ability),
            .. Emit(list, receiver, BattleHookSourceKind.HeldItem),
            .. Emit(list, receiver, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> ContactReceived(
        BattleSlot receiver,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.Where(s => s.Hook == AbilityHookPoint.OnContactReceived).ToList();
        return
        [
            .. Emit(list, receiver, BattleHookSourceKind.Ability),
            .. Emit(list, receiver, BattleHookSourceKind.HeldItem),
            .. Emit(list, receiver, BattleHookSourceKind.Field),
        ];
    }

    public static IReadOnlyList<BattleHookInvocation> WeatherChange(
        BattleSide changer,
        IEnumerable<BattleHookSource> sources) =>
        Ordered(changer, Opponent(changer), AbilityHookPoint.OnWeatherChange, sources);

    private static IReadOnlyList<BattleHookInvocation> Ordered(
        BattleSide active,
        BattleSide opposing,
        AbilityHookPoint hook,
        IEnumerable<BattleHookSource> sources)
    {
        var list = sources.Where(s => s.Hook == hook).ToList();
        return
        [
            .. Emit(list, active, BattleHookSourceKind.Form),
            .. Emit(list, active, BattleHookSourceKind.Ability),
            .. Emit(list, active, BattleHookSourceKind.HeldItem),
            .. Emit(list, opposing, BattleHookSourceKind.Ability),
            .. Emit(list, opposing, BattleHookSourceKind.HeldItem),
            .. Emit(list, active, BattleHookSourceKind.Field),
        ];
    }

    private static IEnumerable<BattleHookInvocation> Emit(
        IEnumerable<BattleHookSource> sources,
        BattleSide side,
        BattleHookSourceKind kind) =>
        sources
            .Where(s => s.Side == side && s.Kind == kind)
            .SelectMany(s => s.Effects.Select(e => new BattleHookInvocation(s.Slot, kind, e)));

    private static IEnumerable<BattleHookInvocation> Emit(
        IEnumerable<BattleHookSource> sources,
        BattleSlot slot,
        BattleHookSourceKind kind) =>
        sources
            .Where(s => s.Slot == slot && s.Kind == kind)
            .SelectMany(s => s.Effects.Select(e => new BattleHookInvocation(s.Slot, kind, e)));

    private static BattleSide Opponent(BattleSide side) =>
        side == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;
}
