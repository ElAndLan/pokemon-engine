using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class RandomAiTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static BattleMove Move(int pp = 10) =>
        new(EntityId.Parse("move:m"), Normal, DamageClass.Physical, 40, 100, pp, 0, 0);

    private static BattleCreature Attacker(params BattleMove[] moves) =>
        new(EntityId.Parse("species:a"), "A", 50, [Normal], new Stats(100, 100, 100, 100, 100, 100), moves);

    [Fact]
    public void PicksOnlyUsableMoves_OverManySeeds()
    {
        // Only index 2 has PP; every seed must land on it.
        var atk = Attacker(Move(pp: 0), Move(pp: 0), Move(), Move(pp: 0));
        for (int seed = 0; seed < 50; seed++)
            Assert.Equal(2, RandomAi.ChooseMove(atk, new Rng(seed)));
    }

    [Fact]
    public void Deterministic_ForSameSeed()
    {
        var atk = Attacker(Move(), Move(), Move(), Move());
        Assert.Equal(RandomAi.ChooseMove(atk, new Rng(9)), RandomAi.ChooseMove(atk, new Rng(9)));
    }

    [Fact]
    public void SpreadsAcrossMoves_OverManySeeds()
    {
        // With four usable moves, the choices shouldn't collapse to a single index.
        var atk = Attacker(Move(), Move(), Move(), Move());
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 50; seed++)
            seen.Add(RandomAi.ChooseMove(atk, new Rng(seed)));
        Assert.True(seen.Count > 1, "random AI should not always return the same move");
    }

    [Fact]
    public void AllNoPp_FallsBackToFirst()
    {
        var atk = Attacker(Move(pp: 0), Move(pp: 0));
        Assert.Equal(0, RandomAi.ChooseMove(atk, new Rng(1)));
    }
}
