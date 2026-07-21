using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16D NPC updates: Core decides every step, actors tick in stable key
/// order, and wander is seeded so a replay reproduces it exactly.</summary>
public sealed class NpcActorTests
{
    private static Func<GridPos, Facing, MoveOutcome> OpenField(int size = 16,
        IReadOnlySet<GridPos>? blocked = null) => (from, dir) =>
        {
            GridPos target = MovementRules.Step(from, dir);
            if (target.X < 0 || target.Y < 0 || target.X >= size || target.Y >= size)
                return MoveOutcome.Blocked;
            if (blocked?.Contains(target) == true)
                return MoveOutcome.Blocked;
            return new MoveOutcome(MoveResult.Step, target);
        };

    private static NpcEntity Npc(string key, NpcMovement move, GridPos pos, int? radius = null,
        IReadOnlyList<GridPos>? path = null) =>
        new() { Key = key, Pos = pos, Move = move, Radius = radius, Path = path };

    private static void Ticks(NpcActor actor, IRng rng, int count)
    {
        for (int i = 0; i < count; i++)
            actor.Tick(rng);
    }

    // --- Static -------------------------------------------------------------------

    [Fact]
    public void StaticNpcs_NeverMove()
    {
        var actor = new NpcActor(Npc("a", NpcMovement.Static, new GridPos(4, 4)), OpenField());
        Ticks(actor, new Rng(1), 100);
        Assert.Equal(new GridPos(4, 4), actor.Position);
    }

    /// <summary>Counts draws, so "consumes no randomness" is measured rather than inferred.</summary>
    private sealed class CountingRng(int seed = 1) : IRng
    {
        private readonly Rng _inner = new(seed);
        public int Draws { get; private set; }

        public int Next(int maxExclusive) { Draws++; return _inner.Next(maxExclusive); }
        public int Next(int min, int max) { Draws++; return _inner.Next(min, max); }
        public double NextDouble() { Draws++; return _inner.NextDouble(); }
    }

    /// <summary>A static NPC must not draw from the RNG, or it would shift every other stream that
    /// shares it — including wild encounters.</summary>
    [Fact]
    public void StaticNpcs_ConsumeNoRandomness()
    {
        var rng = new CountingRng();
        var actor = new NpcActor(Npc("a", NpcMovement.Static, new GridPos(4, 4)), OpenField());
        Ticks(actor, rng, 50);

        Assert.Equal(0, rng.Draws);
        Assert.Equal(new GridPos(4, 4), actor.Position);
    }

    /// <summary>Patrol is authored, not random, so it must not draw either.</summary>
    [Fact]
    public void PatrollingNpcs_ConsumeNoRandomness()
    {
        var rng = new CountingRng();
        var actor = new NpcActor(Npc("a", NpcMovement.Patrol, new GridPos(4, 4),
            path: [new GridPos(6, 4)]), OpenField());
        Ticks(actor, rng, 60);

        Assert.Equal(0, rng.Draws);
    }

    /// <summary>A wandering NPC decides once per step, not once per tick, so draws stay tied to
    /// steps taken rather than to frame rate.</summary>
    [Fact]
    public void WanderingNpcs_DrawOncePerDecisionNotPerTick()
    {
        var rng = new CountingRng(3);
        var actor = new NpcActor(Npc("a", NpcMovement.Wander, new GridPos(8, 8), radius: 3), OpenField());
        Ticks(actor, rng, 60);

        Assert.True(rng.Draws > 0, "a wandering NPC should decide at least once");
        Assert.True(rng.Draws < 60, $"drew {rng.Draws} times in 60 ticks: deciding every tick");
    }

    // --- Wander -------------------------------------------------------------------

    [Fact]
    public void WanderingNpcs_StayWithinTheirRadius()
    {
        var actor = new NpcActor(Npc("a", NpcMovement.Wander, new GridPos(8, 8), radius: 2), OpenField());
        var rng = new Rng(3);

        for (int i = 0; i < 500; i++)
        {
            actor.Tick(rng);
            Assert.InRange(actor.Position.X, 6, 10);
            Assert.InRange(actor.Position.Y, 6, 10);
        }
    }

    [Fact]
    public void WanderingNpcs_DoActuallyMove()
    {
        var actor = new NpcActor(Npc("a", NpcMovement.Wander, new GridPos(8, 8), radius: 3), OpenField());
        Ticks(actor, new Rng(3), 300);
        Assert.NotEqual(new GridPos(8, 8), actor.Position);
    }

    /// <summary>Same seed, same walk: this is the property a golden replay depends on.</summary>
    [Fact]
    public void WanderIsDeterministicForASeed()
    {
        static GridPos Run()
        {
            var actor = new NpcActor(Npc("a", NpcMovement.Wander, new GridPos(8, 8), radius: 3), OpenField());
            Ticks(actor, new Rng(11), 200);
            return actor.Position;
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void DifferentSeeds_DivergeEventually()
    {
        static GridPos Run(int seed)
        {
            var actor = new NpcActor(Npc("a", NpcMovement.Wander, new GridPos(8, 8), radius: 4), OpenField());
            Ticks(actor, new Rng(seed), 300);
            return actor.Position;
        }

        Assert.NotEqual(Run(5), Run(9999));
    }

    [Fact]
    public void WanderingNpcs_RespectBlockedTiles()
    {
        var blocked = new HashSet<GridPos> { new(9, 8), new(7, 8), new(8, 9), new(8, 7) };
        var actor = new NpcActor(Npc("a", NpcMovement.Wander, new GridPos(8, 8), radius: 4),
            OpenField(blocked: blocked));

        Ticks(actor, new Rng(2), 200);
        Assert.Equal(new GridPos(8, 8), actor.Position);   // walled in on all four sides
    }

    [Fact]
    public void WanderWithoutARadius_DefaultsToOneTile()
    {
        var actor = new NpcActor(Npc("a", NpcMovement.Wander, new GridPos(8, 8)), OpenField());
        for (int i = 0; i < 300; i++)
        {
            actor.Tick(new Rng(i + 1));
            Assert.InRange(actor.Position.X, 7, 9);
            Assert.InRange(actor.Position.Y, 7, 9);
        }
    }

    // --- Patrol -------------------------------------------------------------------

    [Fact]
    public void PatrollingNpcs_WalkTheirPath()
    {
        var actor = new NpcActor(Npc("a", NpcMovement.Patrol, new GridPos(4, 4),
            path: [new GridPos(6, 4), new GridPos(4, 4)]), OpenField());

        Ticks(actor, new Rng(1), 120);
        Assert.InRange(actor.Position.X, 4, 6);
        Assert.Equal(4, actor.Position.Y);
    }

    [Fact]
    public void PatrollingNpcs_ReachTheirFirstWaypoint()
    {
        var actor = new NpcActor(Npc("a", NpcMovement.Patrol, new GridPos(4, 4),
            path: [new GridPos(7, 4)]), OpenField());

        Ticks(actor, new Rng(1), 200);
        Assert.Equal(new GridPos(7, 4), actor.Position);
    }

    [Fact]
    public void PatrolWithoutAPath_StandsStill()
    {
        var actor = new NpcActor(Npc("a", NpcMovement.Patrol, new GridPos(4, 4)), OpenField());
        Ticks(actor, new Rng(1), 100);
        Assert.Equal(new GridPos(4, 4), actor.Position);
    }

    [Fact]
    public void PatrolIsDeterministic()
    {
        static GridPos Run()
        {
            var actor = new NpcActor(Npc("a", NpcMovement.Patrol, new GridPos(4, 4),
                path: [new GridPos(6, 4), new GridPos(6, 6), new GridPos(4, 4)]), OpenField());
            Ticks(actor, new Rng(1), 150);
            return actor.Position;
        }

        Assert.Equal(Run(), Run());
    }

    // --- Guards -------------------------------------------------------------------

    [Fact]
    public void NullArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new NpcActor(null!, OpenField()));
        Assert.Throws<ArgumentNullException>(() => new NpcActor(Npc("a", NpcMovement.Static, default), null!));
        Assert.Throws<ArgumentNullException>(() =>
            new NpcActor(Npc("a", NpcMovement.Static, default), OpenField()).Tick(null!));
    }

    [Fact]
    public void ActorExposesItsEntityAndKey()
    {
        NpcEntity npc = Npc("guard", NpcMovement.Static, new GridPos(1, 2));
        var actor = new NpcActor(npc, OpenField());
        Assert.Equal("guard", actor.Key);
        Assert.Same(npc, actor.Entity);
    }
}
