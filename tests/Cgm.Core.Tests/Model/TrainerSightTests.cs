using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class TrainerSightTests
{
    private const int W = 8, H = 8;

    private static CollisionValue[] Open()
    {
        var g = new CollisionValue[W * H];
        Array.Fill(g, CollisionValue.Open);
        return g;
    }

    private static void Solid(CollisionValue[] g, int x, int y) => g[y * W + x] = CollisionValue.Solid;

    private static bool Sees(GridPos npc, Facing dir, int range, GridPos player,
        CollisionValue[]? grid = null, IReadOnlySet<GridPos>? blockers = null) =>
        TrainerSight.Sees(npc, dir, range, player, grid ?? Open(), W, H, blockers ?? new HashSet<GridPos>());

    [Fact]
    public void DefeatedFlag_UsesDerivedFormat()
    {
        Assert.Equal("flag:trainer.gym_leader_flora_defeated",
            TrainerSight.DefeatedFlag(EntityId.Parse("trainer:gym_leader_flora")));
    }

    [Theory]
    [InlineData(1, true)]   // adjacent
    [InlineData(3, true)]   // exactly at range
    [InlineData(4, false)]  // one past range
    public void Sees_StraightAhead_WithinRange(int playerDist, bool expected)
    {
        // NPC at (2,2) facing Down, range 3. Player straight below.
        Assert.Equal(expected, Sees(new GridPos(2, 2), Facing.Down, 3, new GridPos(2, 2 + playerDist)));
    }

    [Theory]
    [InlineData(Facing.Down, true)]
    [InlineData(Facing.Up, false)]
    [InlineData(Facing.Left, false)]
    [InlineData(Facing.Right, false)]
    public void Sees_OnlyInFacingDirection(Facing dir, bool expected)
    {
        Assert.Equal(expected, Sees(new GridPos(3, 3), dir, 4, new GridPos(3, 6))); // player is below
    }

    [Fact]
    public void Sees_False_WhenPlayerOffTheLine()
    {
        Assert.False(Sees(new GridPos(2, 2), Facing.Down, 5, new GridPos(3, 5))); // different column
    }

    [Fact]
    public void Sight_BlockedBySolidWall()
    {
        var grid = Open();
        Solid(grid, 2, 4); // wall between NPC(2,2) and player(2,6)
        Assert.False(Sees(new GridPos(2, 2), Facing.Down, 6, new GridPos(2, 6), grid));
    }

    [Fact]
    public void Sight_BlockedByAnotherNpc()
    {
        var blockers = new HashSet<GridPos> { new(2, 4) };
        Assert.False(Sees(new GridPos(2, 2), Facing.Down, 6, new GridPos(2, 6), blockers: blockers));
    }

    [Fact]
    public void Sight_StopsAtMapEdge_NoWrapOrCrash()
    {
        Assert.False(Sees(new GridPos(2, 6), Facing.Down, 5, new GridPos(2, 6))); // range runs off the bottom
    }

    [Fact]
    public void Sees_False_WhenRangeIsZero()
    {
        Assert.False(Sees(new GridPos(2, 2), Facing.Down, 0, new GridPos(2, 3)));
    }

    // ---- FirstSpotter ----

    private static readonly EntityId TrA = EntityId.Parse("trainer:a");
    private static readonly EntityId TrB = EntityId.Parse("trainer:b");

    private static Trainer Trainer(EntityId id, int range) => new() { Id = id, SightRange = range };

    private static NpcEntity Npc(int x, int y, Facing f, EntityId? trainer) =>
        new() { Pos = new GridPos(x, y), Facing = f, Trainer = trainer };

    [Fact]
    public void FirstSpotter_ReturnsSeeingTrainer()
    {
        var npc = Npc(2, 2, Facing.Down, TrA);
        var trainers = new Dictionary<EntityId, Trainer> { [TrA] = Trainer(TrA, 4) };
        var hit = TrainerSight.FirstSpotter(new GridPos(2, 5), [npc], trainers, new FlagStore(), Open(), W, H);

        Assert.NotNull(hit);
        Assert.Equal(TrA, hit!.Value.Trainer.Id);
    }

    [Fact]
    public void FirstSpotter_SkipsDefeatedTrainer()
    {
        var npc = Npc(2, 2, Facing.Down, TrA);
        var trainers = new Dictionary<EntityId, Trainer> { [TrA] = Trainer(TrA, 4) };
        var flags = new FlagStore();
        flags.SetBool(TrainerSight.DefeatedFlag(TrA), true);

        Assert.Null(TrainerSight.FirstSpotter(new GridPos(2, 5), [npc], trainers, flags, Open(), W, H));
    }

    [Fact]
    public void FirstSpotter_SkipsInteractOnlyTrainer()
    {
        var npc = Npc(2, 2, Facing.Down, TrA);
        var trainers = new Dictionary<EntityId, Trainer> { [TrA] = Trainer(TrA, 0) }; // range 0 = interact-only
        Assert.Null(TrainerSight.FirstSpotter(new GridPos(2, 3), [npc], trainers, new FlagStore(), Open(), W, H));
    }

    [Fact]
    public void FirstSpotter_IgnoresPlainNpcsWithoutTrainer()
    {
        var npc = Npc(2, 2, Facing.Down, trainer: null);
        Assert.Null(TrainerSight.FirstSpotter(new GridPos(2, 4), [npc], new Dictionary<EntityId, Trainer>(),
            new FlagStore(), Open(), W, H));
    }

    [Fact]
    public void FirstSpotter_ReturnsFirstInEntityOrder_WhenTwoSee()
    {
        // Two trainers both looking at the player from opposite sides; entity order decides.
        var top = Npc(4, 1, Facing.Down, TrA);
        var bottom = Npc(4, 7, Facing.Up, TrB);
        var trainers = new Dictionary<EntityId, Trainer> { [TrA] = Trainer(TrA, 6), [TrB] = Trainer(TrB, 6) };
        var hit = TrainerSight.FirstSpotter(new GridPos(4, 4), [top, bottom], trainers, new FlagStore(), Open(), W, H);

        Assert.Equal(TrA, hit!.Value.Trainer.Id); // `top` comes first in the list
    }

    [Fact]
    public void FirstSpotter_OneTrainerBlocksAnothersLine()
    {
        // TrA at (4,1) facing down would see the player at (4,5), but TrB stands at (4,3) in the way.
        var blocker = Npc(4, 3, Facing.Down, TrB);
        var seer = Npc(4, 1, Facing.Down, TrA);
        var trainers = new Dictionary<EntityId, Trainer>
        {
            [TrA] = Trainer(TrA, 6),
            [TrB] = Trainer(TrB, 0), // interact-only, so it won't spot on its own
        };
        var hit = TrainerSight.FirstSpotter(new GridPos(4, 5), [seer, blocker], trainers, new FlagStore(), Open(), W, H);

        Assert.Null(hit); // TrB's body breaks TrA's sight line
    }
}
