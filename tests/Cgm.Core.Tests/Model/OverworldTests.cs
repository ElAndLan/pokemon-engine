using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class FlagStoreTests
{
    [Fact]
    public void UnsetFlags_ReadAsFalseAndZero()
    {
        var flags = new FlagStore();
        Assert.False(flags.GetBool("missing"));
        Assert.Equal(0, flags.GetInt("missing"));
    }

    [Fact]
    public void SetAndGet_BoolAndInt()
    {
        var flags = new FlagStore();
        flags.SetBool("badge.rock", true);
        flags.SetInt("steps", 42);
        Assert.True(flags.GetBool("badge.rock"));
        Assert.Equal(42, flags.GetInt("steps"));

        flags.SetBool("badge.rock", false);
        Assert.False(flags.GetBool("badge.rock"));
    }

    [Fact]
    public void Increment_AddsToCurrent()
    {
        var flags = new FlagStore();
        flags.Increment("happiness");
        flags.Increment("happiness", 4);
        Assert.Equal(5, flags.GetInt("happiness"));
    }

    [Fact]
    public void SnapshotAndLoad_RoundTrip()
    {
        var flags = new FlagStore();
        flags.SetBool("a", true);
        flags.SetInt("b", 3);
        IReadOnlyDictionary<string, int> snap = flags.Snapshot();

        var loaded = new FlagStore();
        loaded.Load(snap);
        Assert.True(loaded.GetBool("a"));
        Assert.Equal(3, loaded.GetInt("b"));
    }

    [Fact]
    public void Snapshot_IsDetachedCopy()
    {
        var flags = new FlagStore();
        flags.SetInt("x", 1);
        IReadOnlyDictionary<string, int> snap = flags.Snapshot();
        flags.SetInt("x", 99); // mutate after snapshot
        Assert.Equal(1, snap["x"]); // snapshot unaffected
    }
}

public sealed class InteractionTests
{
    private static NpcEntity Npc(int x, int y) => new() { Pos = new GridPos(x, y), Dialogue = "hi" };

    [Fact]
    public void InFront_ReturnsInteractableOneTileAhead()
    {
        var npc = Npc(2, 1); // directly right of the player at (1,1)
        MapEntity? hit = Interaction.InFront(new GridPos(1, 1), Facing.Right, [npc]);
        Assert.Same(npc, hit);
    }

    [Fact]
    public void InFront_EmptyCell_ReturnsNull()
    {
        Assert.Null(Interaction.InFront(new GridPos(1, 1), Facing.Up, [Npc(5, 5)]));
    }

    [Fact]
    public void InFront_IgnoresNonInteractables()
    {
        var warp = new WarpEntity { Pos = new GridPos(1, 0), Target = EntityId.Parse("map:x") };
        var trigger = new TriggerEntity { Pos = new GridPos(1, 0) };
        Assert.Null(Interaction.InFront(new GridPos(1, 1), Facing.Up, [warp, trigger]));
    }

    [Fact]
    public void InFront_FindsSignAndPickup()
    {
        var sign = new SignEntity { Pos = new GridPos(1, 0), Text = "route 1" };
        Assert.Same(sign, Interaction.InFront(new GridPos(1, 1), Facing.Up, [sign]));

        var pickup = new PickupEntity { Pos = new GridPos(0, 1), Item = EntityId.Parse("item:potion") };
        Assert.Same(pickup, Interaction.InFront(new GridPos(1, 1), Facing.Left, [pickup]));
    }
}
