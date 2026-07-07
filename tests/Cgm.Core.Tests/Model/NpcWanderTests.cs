using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class NpcWanderTests
{
    private static readonly GridPos Home = new(5, 5);

    private static Func<GridPos, Facing, MoveOutcome> AlwaysStep =>
        (p, d) => new MoveOutcome(MoveResult.Step, MovementRules.Step(p, d));

    private static Func<GridPos, Facing, MoveOutcome> AlwaysBlocked => (_, _) => MoveOutcome.Blocked;

    [Fact]
    public void SameSeed_ProducesSameDecisions()
    {
        var a = Enumerable.Range(0, 20)
            .Select(_ => NpcWander.PickStep(Home, Home, 2, AlwaysStep, RngA())).ToList();
        // Fresh rng, same seed → identical sequence.
        var rng = new Rng(99);
        var b = Enumerable.Range(0, 20).Select(_ => NpcWander.PickStep(Home, Home, 2, AlwaysStep, rng)).ToList();
        var rng2 = new Rng(99);
        var c = Enumerable.Range(0, 20).Select(_ => NpcWander.PickStep(Home, Home, 2, AlwaysStep, rng2)).ToList();
        Assert.Equal(b, c);
    }

    private static Rng RngA() => new(1);

    [Fact]
    public void RadiusZero_NeverMoves()
    {
        var rng = new Rng(3);
        for (int i = 0; i < 50; i++)
            Assert.Null(NpcWander.PickStep(Home, Home, 0, AlwaysStep, rng));
    }

    [Fact]
    public void Blocked_ReturnsNull()
    {
        var rng = new Rng(3);
        for (int i = 0; i < 50; i++)
            Assert.Null(NpcWander.PickStep(Home, Home, 3, AlwaysBlocked, rng));
    }

    [Fact]
    public void ReturnedStep_AlwaysLandsWithinRadius()
    {
        var rng = new Rng(7);
        GridPos pos = Home;
        for (int i = 0; i < 50; i++)
        {
            Facing? dir = NpcWander.PickStep(Home, pos, 2, AlwaysStep, rng);
            if (dir is { } d)
            {
                GridPos next = MovementRules.Step(pos, d);
                Assert.True(Math.Abs(next.X - Home.X) <= 2 && Math.Abs(next.Y - Home.Y) <= 2);
            }
        }
    }

    [Fact]
    public void Fuzz_NpcNeverEscapesRadius_OverManyDecisions()
    {
        var rng = new Rng(2026);
        GridPos pos = Home;
        for (int i = 0; i < 2000; i++)
        {
            if (NpcWander.PickStep(Home, pos, 3, AlwaysStep, rng) is { } d)
                pos = MovementRules.Step(pos, d);
            Assert.True(Math.Abs(pos.X - Home.X) <= 3 && Math.Abs(pos.Y - Home.Y) <= 3,
                $"NPC escaped its radius at {pos}");
        }
    }
}
