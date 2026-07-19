using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15G-1 Baton Pass: switch the user out and carry its stat stages to the incoming reserve
/// (single-reserve auto-switch case).</summary>
public sealed class BattleBatonPassTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void BatonPassCarriesStatStagesToTheIncomingReserve()
    {
        BattleCreature user = Creature("user", BatonPass());
        BattleCreature reserve = Creature("reserve", Inert());
        user.ChangeStage(StatKind.Atk, 2);
        user.ChangeStage(StatKind.Spe, -1);

        var battle = new BattleController([user, reserve], [Creature("enemy", Inert())], Chart(), new FakeRng());
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, e => e is StatePassed { Side: BattleSide.Player });
        Assert.Contains(events, e => e is SwitchedIn);
        Assert.Equal(2, reserve.Stage(StatKind.Atk));   // boost carried
        Assert.Equal(-1, reserve.Stage(StatKind.Spe));  // drop carried too
    }

    [Fact]
    public void MultiReserveBatonPassSwitchesToThePartyIndexFirstReserve()
    {
        BattleCreature user = Creature("user", BatonPass());
        BattleCreature reserveA = Creature("reserve_a", Inert());
        BattleCreature reserveB = Creature("reserve_b", Inert());
        user.ChangeStage(StatKind.Atk, 2);

        var battle = new BattleController([user, reserveA, reserveB], [Creature("enemy", Inert())],
            Chart(), new FakeRng());
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, e => e is SwitchedIn { PartyIndex: 1 }); // ADR-012: party-index-first reserve
        Assert.Equal(2, reserveA.Stage(StatKind.Atk)); // carried to the chosen reserve
        Assert.Equal(0, reserveB.Stage(StatKind.Atk)); // the other reserve is untouched
    }

    [Fact]
    public void BatonPassCarriesPassableVolatilesToTheIncomingReserve()
    {
        BattleCreature user = Creature("user", BatonPass());
        BattleCreature reserve = Creature("reserve", Inert());
        user.SetSeeded(true);   // Leech Seed
        user.RaiseCrit(2);      // Focus Energy crit boost

        var battle = new BattleController([user, reserve], [Creature("enemy", Inert())], Chart(), new FakeRng());
        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.True(reserve.Seeded);
        Assert.Equal(2, reserve.CritStageBonus);
    }

    [Fact]
    public void VoluntarySwitchDoesNotCarryStages()
    {
        BattleCreature user = Creature("user", Inert());
        BattleCreature reserve = Creature("reserve", Inert());
        user.ChangeStage(StatKind.Atk, 2);

        var battle = new BattleController([user, reserve], [Creature("enemy", Inert())], Chart(), new FakeRng());
        battle.ResolveTurn(new Switch(1), new Pass());

        Assert.Equal(0, reserve.Stage(StatKind.Atk)); // a normal switch passes nothing
    }

    [Fact]
    public void BatonPassFailsWithoutAReserve()
    {
        BattleCreature user = Creature("user", BatonPass());
        user.ChangeStage(StatKind.Atk, 2);

        var battle = new BattleController([user], [Creature("enemy", Inert())], Chart(), new FakeRng());
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Empty(events.OfType<StatePassed>());
        Assert.Empty(events.OfType<SwitchedIn>());
    }

    [Fact]
    public void PivotMoveDealsDamageThenSwitchesTheUserOut()
    {
        BattleMove uturn = new(EntityId.Parse("move:uturn"), Normal, DamageClass.Physical, 70, 100, 20, 0, 0,
            target: MoveTarget.Selected, secondaryEffects: [new PivotSwitchEffect()]);
        BattleCreature user = Creature("user", uturn);
        BattleCreature reserve = Creature("reserve", Inert());
        BattleCreature enemy = Creature("enemy", Inert());

        var battle = new BattleController([user, reserve], [enemy], Chart(),
            new FakeRng(ints: [0, 100, 0, 0], doubles: [0.99, 0.99]));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, e => e is DamageDealt { Target: BattleSide.Enemy }); // hit first
        Assert.Contains(events, e => e is SwitchedIn);                                // then pivoted out
        Assert.Empty(events.OfType<StatePassed>());                                   // pivot carries no state
    }

    [Fact]
    public void PivotMoveStillSucceedsWithNoReserveToSwitchTo()
    {
        BattleMove uturn = new(EntityId.Parse("move:uturn"), Normal, DamageClass.Physical, 70, 100, 20, 0, 0,
            target: MoveTarget.Selected, secondaryEffects: [new PivotSwitchEffect()]);
        BattleCreature user = Creature("user", uturn);
        BattleCreature enemy = Creature("enemy", Inert());

        var battle = new BattleController([user], [enemy], Chart(),
            new FakeRng(ints: [0, 100, 0, 0], doubles: [0.99, 0.99]));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, e => e is DamageDealt { Target: BattleSide.Enemy });
        Assert.Empty(events.OfType<SwitchedIn>()); // no reserve -> no switch, move still hit
    }

    [Fact]
    public void TrappedUserCannotBatonPassOrPivotOut()
    {
        BattleCreature bpUser = Creature("bp_user", BatonPass());
        bpUser.ChangeStage(StatKind.Atk, 2);
        bpUser.SetTrap(3);
        BattleCreature reserve = Creature("reserve", Inert());
        var bpBattle = new BattleController([bpUser, reserve], [Creature("e1", Inert())], Chart(), new FakeRng());
        IReadOnlyList<BattleEvent> bpEvents = bpBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Empty(bpEvents.OfType<StatePassed>());
        Assert.Empty(bpEvents.OfType<SwitchedIn>());

        BattleMove uturn = new(EntityId.Parse("move:uturn"), Normal, DamageClass.Physical, 70, 100, 20, 0, 0,
            target: MoveTarget.Selected, secondaryEffects: [new PivotSwitchEffect()]);
        BattleCreature pivotUser = Creature("pivot_user", uturn);
        pivotUser.SetTrap(3);
        var pivotBattle = new BattleController([pivotUser, Creature("r2", Inert())], [Creature("e2", Inert())],
            Chart(), new FakeRng(ints: [0, 100, 0, 0], doubles: [0.99, 0.99]));
        IReadOnlyList<BattleEvent> pivotEvents = pivotBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(pivotEvents, e => e is DamageDealt { Target: BattleSide.Enemy }); // still hits
        Assert.Empty(pivotEvents.OfType<SwitchedIn>());                                    // but does not switch
    }

    private static BattleCreature Creature(string slug, BattleMove move) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal], new Stats(300, 100, 100, 100, 100, 100), [move]);

    private static BattleMove BatonPass() => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:baton_pass"), Name = "baton_pass", Type = Normal,
        DamageClass = DamageClass.Status, Pp = 10, Target = MoveTarget.User,
        Effects = [new Effect { Op = "batonPass" }],
    });

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
}
