using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleSwitchTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Plain() =>
        new(EntityId.Parse("move:m"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0);

    private static BattleMove SelfBuff() =>
        new(EntityId.Parse("move:sd"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            stageEffect: new StageEffect(StatKind.Atk, 2, OnSelf: true, Chance: 100));

    private static BattleCreature Creature(string name, int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:x"), name, 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleController Party(IReadOnlyList<BattleCreature> p, IReadOnlyList<BattleCreature> e, int seed = 1) =>
        new(p, e, Chart(), new Rng(seed));

    [Fact]
    public void VoluntarySwitch_ChangesActive_AndEmitsSwitchedIn()
    {
        var a = Creature("A", 200, Plain());
        var b = Creature("B", 200, Plain());
        var enemy = Creature("E", 200, Plain());
        var battle = Party([a, b], [enemy]);

        var events = battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Same(b, battle.Active(BattleSide.Player));
        Assert.Contains(events, ev => ev is SwitchedIn { Side: BattleSide.Player, PartyIndex: 1 });
    }

    [Fact]
    public void Switch_ResetsOutgoingStages_BeforeSwitching()
    {
        var a = Creature("A", 200, SelfBuff());
        var b = Creature("B", 200, Plain());
        var enemy = Creature("E", 200, Plain());
        var battle = Party([a, b], [enemy]);

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // A buffs to +2 Atk
        Assert.Equal(2, a.Stage(StatKind.Atk));

        battle.ResolveTurn(new Switch(1), new UseMove(0)); // switch A out
        Assert.Equal(0, a.Stage(StatKind.Atk));            // stages cleared on switch
    }

    [Fact]
    public void SwitchedInCreature_TakesTheEnemyHit_ThisTurn()
    {
        var a = Creature("A", 200, Plain());
        var b = Creature("B", 200, Plain());
        var enemy = Creature("E", 200, Plain());
        var battle = Party([a, b], [enemy]);

        battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Equal(a.MaxHp, a.CurrentHp);   // outgoing untouched
        Assert.True(b.CurrentHp < b.MaxHp);   // incoming took the enemy move
    }

    [Fact]
    public void FaintedActive_AutoReplacedByFirstHealthyReserve()
    {
        var a = Creature("A", 1, Plain());   // faints instantly
        var b = Creature("B", 200, Plain());
        // Enemy strong enough to KO A in one hit.
        var enemy = Creature("E", 200,
            new BattleMove(EntityId.Parse("move:big"), Normal, DamageClass.Physical, 250, 100, 25, 0, 0));
        var battle = Party([a, b], [enemy]);

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(a.IsFainted);
        Assert.Contains(events, ev => ev is Fainted { Side: BattleSide.Player });
        Assert.Contains(events, ev => ev is SwitchedIn { Side: BattleSide.Player, PartyIndex: 1 });
        Assert.Same(b, battle.Active(BattleSide.Player));
        Assert.Null(battle.Outcome); // battle continues
    }

    [Fact]
    public void WholePartyFainted_EndsBattle()
    {
        var a = Creature("A", 1, Plain());
        var enemy = Creature("E", 200,
            new BattleMove(EntityId.Parse("move:big"), Normal, DamageClass.Physical, 250, 100, 25, 0, 0));
        var battle = Party([a], [enemy]);

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.NotNull(battle.Outcome);
        Assert.Equal(BattleSide.Enemy, battle.Outcome!.Winner);
        Assert.Contains(events, ev => ev is BattleEnded { Winner: BattleSide.Enemy });
    }

    [Theory]
    [InlineData(1)]   // fainted reserve
    [InlineData(0)]   // same as current active
    [InlineData(5)]   // out of range
    public void InvalidSwitch_Rejected(int index)
    {
        var a = Creature("A", 200, Plain());
        var fainted = Creature("F", 200, Plain());
        fainted.TakeDamage(fainted.MaxHp);
        var enemy = Creature("E", 200, Plain());
        var battle = Party([a, fainted], [enemy]);

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new Switch(index), new UseMove(0)));
    }

    [Fact]
    public void SwitchHappensBeforeEnemyMove_RegardlessOfSpeed()
    {
        // Fast enemy would normally move first, but the player's switch resolves before any move.
        var a = Creature("A", 200, Plain());
        var b = Creature("B", 200, Plain());
        var enemy = new BattleCreature(EntityId.Parse("species:e"), "E", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 255), [Plain()]);
        var battle = Party([a, b], [enemy]);

        battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Same(b, battle.Active(BattleSide.Player));
        Assert.True(b.CurrentHp < b.MaxHp); // b (not a) received the enemy's hit
    }
}
