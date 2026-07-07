using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class MovementRulesTests
{
    private const int W = 3, H = 3;
    private static readonly IReadOnlySet<GridPos> None = new HashSet<GridPos>();

    private static CollisionValue[] Open() => Enumerable.Repeat(CollisionValue.Open, W * H).ToArray();

    private static CollisionValue[] With(params (int x, int y, CollisionValue v)[] cells)
    {
        CollisionValue[] c = Open();
        foreach ((int x, int y, CollisionValue v) in cells)
            c[y * W + x] = v;
        return c;
    }

    [Fact]
    public void Step_IntoOpenCell()
    {
        MoveOutcome m = MovementRules.Resolve(new GridPos(1, 1), Facing.Right, Open(), W, H, None);
        Assert.Equal(MoveResult.Step, m.Result);
        Assert.Equal(new GridPos(2, 1), m.Destination);
    }

    [Fact]
    public void Blocked_BySolid()
    {
        var c = With((2, 1, CollisionValue.Solid));
        Assert.Equal(MoveResult.Blocked, MovementRules.Resolve(new GridPos(1, 1), Facing.Right, c, W, H, None).Result);
    }

    [Theory]
    [InlineData(0, 0, Facing.Left)]  // off the left edge
    [InlineData(0, 0, Facing.Up)]    // off the top edge
    [InlineData(2, 2, Facing.Right)] // off the right edge
    public void Blocked_OutOfBounds(int x, int y, Facing dir)
    {
        Assert.Equal(MoveResult.Blocked, MovementRules.Resolve(new GridPos(x, y), dir, Open(), W, H, None).Result);
    }

    [Fact]
    public void Blocked_ByOccupiedCell()
    {
        var occupied = new HashSet<GridPos> { new(2, 1) };
        Assert.Equal(MoveResult.Blocked, MovementRules.Resolve(new GridPos(1, 1), Facing.Right, Open(), W, H, occupied).Result);
    }

    [Fact]
    public void LedgeHop_InLedgeDirection_LandsTwoCellsAway()
    {
        // Down-ledge at (1,1); mover at (1,0) moving Down → hop to (1,2).
        var c = With((1, 1, CollisionValue.LedgeDown));
        MoveOutcome m = MovementRules.Resolve(new GridPos(1, 0), Facing.Down, c, W, H, None);
        Assert.Equal(MoveResult.LedgeHop, m.Result);
        Assert.Equal(new GridPos(1, 2), m.Destination);
    }

    [Fact]
    public void Ledge_FromWrongDirection_IsBlocked()
    {
        // Down-ledge at (1,1); approaching it moving Up (from (1,2)) must be blocked.
        var c = With((1, 1, CollisionValue.LedgeDown));
        Assert.Equal(MoveResult.Blocked, MovementRules.Resolve(new GridPos(1, 2), Facing.Up, c, W, H, None).Result);
        // Approaching sideways (moving Right into it) also blocked.
        Assert.Equal(MoveResult.Blocked, MovementRules.Resolve(new GridPos(0, 1), Facing.Right, c, W, H, None).Result);
    }

    [Fact]
    public void LedgeHop_BlockedWhenLandingSolidOccupiedOrOffMap()
    {
        // Right-ledge at (1,1), landing (2,1). Blocked when landing is solid.
        var solidLanding = With((1, 1, CollisionValue.LedgeRight), (2, 1, CollisionValue.Solid));
        Assert.Equal(MoveResult.Blocked, MovementRules.Resolve(new GridPos(0, 1), Facing.Right, solidLanding, W, H, None).Result);

        // Blocked when landing is occupied.
        var openLanding = With((1, 1, CollisionValue.LedgeRight));
        var occupied = new HashSet<GridPos> { new(2, 1) };
        Assert.Equal(MoveResult.Blocked, MovementRules.Resolve(new GridPos(0, 1), Facing.Right, openLanding, W, H, occupied).Result);

        // Blocked when landing is off the map: down-ledge at (1,2), landing (1,3) out of bounds.
        var edgeLedge = With((1, 2, CollisionValue.LedgeDown));
        Assert.Equal(MoveResult.Blocked, MovementRules.Resolve(new GridPos(1, 1), Facing.Down, edgeLedge, W, H, None).Result);
    }

    [Fact]
    public void Step_Helper_MovesOneCellPerDirection()
    {
        var o = new GridPos(1, 1);
        Assert.Equal(new GridPos(1, 0), MovementRules.Step(o, Facing.Up));
        Assert.Equal(new GridPos(1, 2), MovementRules.Step(o, Facing.Down));
        Assert.Equal(new GridPos(0, 1), MovementRules.Step(o, Facing.Left));
        Assert.Equal(new GridPos(2, 1), MovementRules.Step(o, Facing.Right));
    }
}
