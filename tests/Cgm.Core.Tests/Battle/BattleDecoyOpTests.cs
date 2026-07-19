using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15F-6 decoy (Substitute) creation wired into the controller.</summary>
public sealed class BattleDecoyOpTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void DecoyMoveDeductsHpAndWritesTheOverlay()
    {
        BattleMove sub = Compile("substitute", Op("decoy"));
        BattleCreature user = Creature("user", sub);   // 200 max HP -> cost 50
        BattleCreature enemy = Creature("enemy", Inert());

        var battle = new BattleController(user, enemy, Chart(), new FakeRng(ints: [0, 0]));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        DecoyCreated created = events.OfType<DecoyCreated>().Single();
        Assert.Equal(50, created.Hp);
        Assert.Equal(150, user.CurrentHp);
        Assert.Contains(battle.Overlays.Snapshot(),
            o => o.Payload is DecoyOverlay { Decoy: { Hp: 50, MaxHp: 50 } });
    }

    [Fact]
    public void DecoyFailsWithoutCostWhenHpIsNotAboveTheCost()
    {
        BattleMove sub = Compile("substitute", Op("decoy"));
        BattleCreature user = Creature("user", sub);
        user.TakeDamage(170); // 30 HP left, below the 50 cost
        BattleCreature enemy = Creature("enemy", Inert());

        var battle = new BattleController(user, enemy, Chart(), new FakeRng(ints: [0, 0]));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Empty(events.OfType<DecoyCreated>());
        Assert.Equal(30, user.CurrentHp); // no HP paid
        Assert.DoesNotContain(battle.Overlays.Snapshot(), o => o.Payload is DecoyOverlay);
    }

    [Fact]
    public void SecondDecoyFailsWhileOneIsAlreadyPresent()
    {
        BattleMove sub = Compile("substitute", Op("decoy"));
        BattleCreature user = Creature("user", sub);
        BattleCreature enemy = Creature("enemy", Inert());

        var battle = new BattleController(user, enemy, Chart(), new FakeRng(ints: [0, 0, 0, 0]));
        battle.ResolveTurn(new UseMove(0), new Pass()); // creates: HP 200 -> 150
        int hpAfterFirst = user.CurrentHp;
        IReadOnlyList<BattleEvent> second = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Empty(second.OfType<DecoyCreated>());
        Assert.Equal(hpAfterFirst, user.CurrentHp); // no extra HP paid
        Assert.Single(battle.Overlays.Snapshot(), o => o.Payload is DecoyOverlay);
    }

    private static BattleMove Compile(string slug, Effect effect) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal,
            DamageClass = DamageClass.Status, Pp = 10, Target = MoveTarget.User, Effects = [effect],
        });

    private static BattleCreature Creature(string slug, BattleMove move) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal], new Stats(200, 100, 100, 100, 100, 100), [move]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static Effect Op(string op) => new() { Op = op };
}
