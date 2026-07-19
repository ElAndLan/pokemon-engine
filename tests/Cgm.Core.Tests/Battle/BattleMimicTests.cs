using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15F-6 temporary move replacement (Mimic): copies the target's last-used move into the
/// Mimic slot with a fresh PP pool, reverting on switch/faint via ADR-011 OverrideMoves.</summary>
public sealed class BattleMimicTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId TackleId = EntityId.Parse("move:tackle");

    [Fact]
    public void MimicCopiesTheTargetsLastMoveIntoItsOwnSlot()
    {
        BattleCreature user = Creature("user", 100, Mimic());
        BattleCreature target = Creature("target", 50, Tackle());

        var battle = new BattleController(user, target, Chart(), new FakeRng(ints: [0, 100, 0, 0], doubles: [0.99]));
        battle.ResolveTurn(new Pass(), new UseMove(0));      // target uses tackle -> LastMoveUsed
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass()); // user mimics

        Assert.Contains(events, e => e is MoveReplaced { Side: BattleSide.Player, MoveSlot: 0 } m && m.Move == TackleId);
        Assert.Equal(TackleId, user.Moves[0].Move);
        Assert.Equal(5, user.Moves[0].MaxPp); // min(5, base 35)
        Assert.True(user.IsTransformed);
    }

    [Fact]
    public void MimicFailsWhenTheTargetHasNotUsedAMove()
    {
        BattleCreature user = Creature("user", 100, Mimic());
        BattleCreature target = Creature("target", 50, Tackle());

        var battle = new BattleController(user, target, Chart(), new FakeRng(ints: [0, 0]));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass()); // target never moved

        Assert.Empty(events.OfType<MoveReplaced>());
        Assert.Equal(EntityId.Parse("move:mimic"), user.Moves[0].Move); // unchanged
        Assert.False(user.IsTransformed);
    }

    private static BattleCreature Creature(string slug, int spe, BattleMove move) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
        new Stats(400, 100, 100, 100, 100, spe), [move]);

    private static BattleMove Mimic() => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:mimic"), Name = "mimic", Type = Normal, DamageClass = DamageClass.Status,
        Pp = 10, Target = MoveTarget.Selected, Effects = [new Effect { Op = "replaceMove" }],
    });

    private static BattleMove Tackle() =>
        new(TackleId, Normal, DamageClass.Physical, 10, 100, 35, 0, 0, target: MoveTarget.Selected);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
}
