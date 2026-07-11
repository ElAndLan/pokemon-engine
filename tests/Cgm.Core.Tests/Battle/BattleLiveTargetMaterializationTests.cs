using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleLiveTargetMaterializationTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Theory]
    [MemberData(nameof(TargetCases))]
    public void EveryTargetShape_AdmitsAndMaterializesInDoubles(MoveTarget target, BattleActionSelection? selection)
    {
        BattleController battle = Battle(target, new FakeRng(ints: [0]));
        BattleMove move = battle.Active(new BattleSlot(BattleSide.Player, 0)).Moves[0];

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(target, selection));

        Assert.Equal(9, move.Pp);
        Assert.Contains(events, item => item is MoveUsed { Slot: { Side: BattleSide.Player, Position: 0 } });
        Assert.DoesNotContain(events, item => item is MoveFailed { Reason: MoveFailureReason.TargetUnavailable });
    }

    [Fact]
    public void InvalidSelectionIsRejectedWithoutPpEventsOrRng()
    {
        BattleController battle = Battle(MoveTarget.Ally, new FakeRng());
        BattleMove move = battle.Active(new BattleSlot(BattleSide.Player, 0)).Moves[0];

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(Actions(MoveTarget.Ally,
            new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0)))));

        Assert.Equal(10, move.Pp);
        Assert.Empty(battle.Log);
    }

    [Fact]
    public void DeadSelectedOpponentFallsBackButDeadAllyFailsAfterPpAndMoveUsed()
    {
        BattleController opponentBattle = Battle(MoveTarget.Selected, new FakeRng());
        opponentBattle.Active(new BattleSlot(BattleSide.Enemy, 0)).TakeDamage(100);
        IReadOnlyList<BattleEvent> fallback = opponentBattle.ResolveTurn(Actions(MoveTarget.Selected,
            new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0))));
        Assert.DoesNotContain(fallback, item => item is MoveFailed { Reason: MoveFailureReason.TargetUnavailable });

        BattleController allyBattle = Battle(MoveTarget.Ally, new FakeRng());
        allyBattle.Active(new BattleSlot(BattleSide.Player, 1)).TakeDamage(100);
        IReadOnlyList<BattleEvent> failed = allyBattle.ResolveTurn(Actions(MoveTarget.Ally,
            new ActiveSlotSelection(new BattleSlot(BattleSide.Player, 1))));
        Assert.Contains(failed, item => item is MoveUsed { Slot: { Side: BattleSide.Player, Position: 0 } });
        Assert.Contains(failed, item => item is MoveFailed { Reason: MoveFailureReason.TargetUnavailable });
    }

    [Fact]
    public void RandomOpponentDrawsOnlyForTwoLiveCandidates()
    {
        var two = new CountingRng(0);
        Battle(MoveTarget.RandomOpponent, two).ResolveTurn(Actions(MoveTarget.RandomOpponent, null));
        Assert.Equal(1, two.IntCalls);

        var one = new CountingRng();
        BattleController battle = Battle(MoveTarget.RandomOpponent, one);
        battle.Active(new BattleSlot(BattleSide.Enemy, 1)).TakeDamage(100);
        battle.ResolveTurn(Actions(MoveTarget.RandomOpponent, null));
        Assert.Equal(0, one.IntCalls);
    }

    public static IEnumerable<object?[]> TargetCases()
    {
        BattleSlot enemy = new(BattleSide.Enemy, 0);
        BattleSlot ally = new(BattleSide.Player, 1);
        foreach (MoveTarget target in Enum.GetValues<MoveTarget>())
        {
            BattleActionSelection? selection = target switch
            {
                MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst => new ActiveSlotSelection(enemy),
                MoveTarget.Ally => new ActiveSlotSelection(ally),
                MoveTarget.UserOrAlly => new ActiveSlotSelection(new BattleSlot(BattleSide.Player, 0)),
                MoveTarget.FaintingPokemon => new PartyMemberSelection(BattleSide.Player, 2),
                MoveTarget.SpecificMove => new MoveReferenceSelection(enemy, 0),
                _ => null,
            };
            yield return [target, selection];
        }
    }

    private static BattleController Battle(MoveTarget target, IRng rng)
    {
        BattleCreature p0 = Creature("p0", target);
        BattleCreature p1 = Creature("p1", target);
        BattleCreature fainted = Creature("fainted", target);
        fainted.TakeDamage(fainted.MaxHp);
        return new BattleController([p0, p1, fainted], [Creature("e0", target), Creature("e1", target)],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), rng);
    }

    private static BattleTurnActions Actions(MoveTarget target, BattleActionSelection? selection) => new(BattleTopology.Doubles,
    [
        new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0), selection),
        new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
    ]);

    private static BattleCreature Creature(string slug, MoveTarget target) => new(EntityId.Parse($"species:{slug}"), slug, 50,
        [Normal], new Stats(100, 100, 100, 100, 100, 50),
        [new BattleMove(EntityId.Parse("move:target_probe"), Normal, DamageClass.Status, null, null, 10, 0, 0, target: target)]);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private sealed class CountingRng(params int[] values) : IRng
    {
        private readonly Queue<int> _values = new(values);
        public int IntCalls { get; private set; }
        public int Next(int maxExclusive) { IntCalls++; return _values.Dequeue(); }
        public int Next(int minInclusive, int maxExclusive) { IntCalls++; return _values.Dequeue(); }
        public double NextDouble() => 0;
    }
}
