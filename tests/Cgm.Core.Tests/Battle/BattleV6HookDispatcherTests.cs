using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleV6HookDispatcherTests
{
    [Fact]
    public void SwitchIn_OrdersFormAbilityItemOpponentThenField()
    {
        BattleHookSource[] sources =
        [
            Source(BattleSide.Player, BattleHookSourceKind.Field, AbilityHookPoint.OnSwitchIn, "field"),
            Source(BattleSide.Enemy, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnSwitchIn, "enemy_item"),
            Source(BattleSide.Player, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnSwitchIn, "item"),
            Source(BattleSide.Player, BattleHookSourceKind.Form, AbilityHookPoint.OnSwitchIn, "form"),
            Source(BattleSide.Enemy, BattleHookSourceKind.Ability, AbilityHookPoint.OnSwitchIn, "enemy_ability"),
            Source(BattleSide.Player, BattleHookSourceKind.Ability, AbilityHookPoint.OnSwitchIn, "ability"),
        ];

        string[] ops = BattleHookDispatcher.SwitchIn(BattleSide.Player, sources)
            .Select(i => i.Effect.Op)
            .ToArray();

        Assert.Equal(["form", "ability", "item", "enemy_ability", "enemy_item", "field"], ops);
    }

    [Fact]
    public void Damage_OrdersOutgoingThenIncomingAbilityAndItem()
    {
        BattleHookSource[] sources =
        [
            Source(BattleSide.Enemy, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnModifyIncomingDamage, "incoming_item"),
            Source(BattleSide.Player, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnModifyOutgoingDamage, "outgoing_item"),
            Source(BattleSide.Enemy, BattleHookSourceKind.Ability, AbilityHookPoint.OnModifyIncomingDamage, "incoming_ability"),
            Source(BattleSide.Player, BattleHookSourceKind.Ability, AbilityHookPoint.OnModifyOutgoingDamage, "outgoing_ability"),
            Source(BattleSide.Player, BattleHookSourceKind.Field, AbilityHookPoint.OnModifyOutgoingDamage, "field"),
        ];

        string[] ops = BattleHookDispatcher.Damage(BattleSide.Player, sources)
            .Select(i => i.Effect.Op)
            .ToArray();

        Assert.Equal(["outgoing_ability", "outgoing_item", "incoming_ability", "incoming_item", "field"], ops);
    }

    [Fact]
    public void Dispatcher_IgnoresOtherHookPoints()
    {
        BattleHookSource[] sources =
        [
            Source(BattleSide.Player, BattleHookSourceKind.Ability, AbilityHookPoint.OnEndOfTurn, "wrong"),
            Source(BattleSide.Player, BattleHookSourceKind.Ability, AbilityHookPoint.OnSwitchIn, "right"),
        ];

        var invocations = BattleHookDispatcher.SwitchIn(BattleSide.Player, sources);

        Assert.Equal("right", Assert.Single(invocations).Effect.Op);
    }

    [Fact]
    public void EndOfTurn_OrdersHeldItemBeforeAbility()
    {
        BattleHookSource[] sources =
        [
            Source(BattleSide.Player, BattleHookSourceKind.Ability, AbilityHookPoint.OnEndOfTurn, "ability"),
            Source(BattleSide.Player, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnEndOfTurn, "item"),
            Source(BattleSide.Enemy, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnEndOfTurn, "enemy_item"),
            Source(BattleSide.Player, BattleHookSourceKind.Field, AbilityHookPoint.OnEndOfTurn, "field"),
        ];

        string[] ops = BattleHookDispatcher.EndOfTurn(BattleSide.Player, sources)
            .Select(i => i.Effect.Op)
            .ToArray();

        Assert.Equal(["item", "ability", "field"], ops);
    }

    [Fact]
    public void StatusAttempt_OrdersTargetAbilityHeldItemThenField()
    {
        BattleHookSource[] sources =
        [
            Source(BattleSide.Player, BattleHookSourceKind.Ability, AbilityHookPoint.OnStatusAttempt, "attacker"),
            Source(BattleSide.Enemy, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnStatusAttempt, "item"),
            Source(BattleSide.Enemy, BattleHookSourceKind.Ability, AbilityHookPoint.OnStatusAttempt, "ability"),
            Source(BattleSide.Enemy, BattleHookSourceKind.Field, AbilityHookPoint.OnStatusAttempt, "field"),
        ];

        string[] ops = BattleHookDispatcher.StatusAttempt(BattleSide.Enemy, sources)
            .Select(i => i.Effect.Op)
            .ToArray();

        Assert.Equal(["ability", "item", "field"], ops);
    }

    [Fact]
    public void WeatherChange_OrdersChangingSideThenOpponent()
    {
        BattleHookSource[] sources =
        [
            Source(BattleSide.Player, BattleHookSourceKind.Field, AbilityHookPoint.OnWeatherChange, "field"),
            Source(BattleSide.Enemy, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnWeatherChange, "enemy_item"),
            Source(BattleSide.Player, BattleHookSourceKind.HeldItem, AbilityHookPoint.OnWeatherChange, "item"),
            Source(BattleSide.Player, BattleHookSourceKind.Form, AbilityHookPoint.OnWeatherChange, "form"),
            Source(BattleSide.Enemy, BattleHookSourceKind.Ability, AbilityHookPoint.OnWeatherChange, "enemy_ability"),
            Source(BattleSide.Player, BattleHookSourceKind.Ability, AbilityHookPoint.OnWeatherChange, "ability"),
        ];

        string[] ops = BattleHookDispatcher.WeatherChange(BattleSide.Player, sources)
            .Select(i => i.Effect.Op)
            .ToArray();

        Assert.Equal(["form", "ability", "item", "enemy_ability", "enemy_item", "field"], ops);
    }

    private static BattleHookSource Source(
        BattleSide side,
        BattleHookSourceKind kind,
        AbilityHookPoint hook,
        string op) =>
        new(side, kind, hook, [new Effect { Op = op }]);
}
