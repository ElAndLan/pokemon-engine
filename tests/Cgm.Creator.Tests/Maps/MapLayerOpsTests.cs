using Cgm.Creator.Maps;

namespace Cgm.Creator.Tests.Maps;

public sealed class MapLayerOpsTests
{
    // 3×3 layer, all -1.
    private static int[] Empty3x3() => Enumerable.Repeat(-1, 9).ToArray();

    [Fact]
    public void Paint_SetsCell_DoesNotMutateOriginal()
    {
        int[] layer = Empty3x3();
        int[] next = MapLayerOps.Paint(layer, 3, 3, 1, 1, 5);
        Assert.Equal(5, next[1 * 3 + 1]);
        Assert.Equal(-1, layer[1 * 3 + 1]); // original untouched
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    public void Paint_OutOfBounds_IsNoOp(int x, int y)
    {
        int[] next = MapLayerOps.Paint(Empty3x3(), 3, 3, x, y, 5);
        Assert.All(next, v => Assert.Equal(-1, v));
    }

    [Fact]
    public void RectFill_FillsInclusiveRegion_ClampsAndAcceptsReversedCorners()
    {
        // Reversed corners (2,2)->(0,0) still fills the whole grid; out-of-range clamps.
        int[] next = MapLayerOps.RectFill(Empty3x3(), 3, 3, 2, 2, -5, -5, 7);
        Assert.All(next, v => Assert.Equal(7, v));
    }

    [Fact]
    public void RectFill_PartialRegion()
    {
        int[] next = MapLayerOps.RectFill(Empty3x3(), 3, 3, 0, 0, 1, 0, 7); // top-left 2 cells of row 0
        Assert.Equal(7, next[0]);
        Assert.Equal(7, next[1]);
        Assert.Equal(-1, next[2]);
        Assert.Equal(-1, next[3]);
    }

    [Fact]
    public void BucketFill_FillsContiguousRegionOnly()
    {
        // Left column = 0, rest = 1. Bucket at (0,0) with 9 should fill only the left column.
        int[] layer = [0, 1, 1, 0, 1, 1, 0, 1, 1];
        int[] next = MapLayerOps.BucketFill(layer, 3, 3, 0, 0, 9);
        Assert.Equal([9, 1, 1, 9, 1, 1, 9, 1, 1], next);
    }

    [Fact]
    public void BucketFill_IsFourConnected_NotDiagonal()
    {
        // Two 0-cells touching only diagonally: filling one must not reach the other.
        int[] layer = [0, 1, 1, 1, 0, 1, 1, 1, 1];
        int[] next = MapLayerOps.BucketFill(layer, 3, 3, 0, 0, 9);
        Assert.Equal(9, next[0]);
        Assert.Equal(0, next[4]); // diagonal neighbour untouched
    }

    [Fact]
    public void BucketFill_SameTargetTile_IsNoOp()
    {
        int[] layer = [5, 5, 5, 5, 5, 5, 5, 5, 5];
        Assert.Equal(layer, MapLayerOps.BucketFill(layer, 3, 3, 1, 1, 5));
    }

    [Fact]
    public void BucketFill_OutOfBoundsStart_IsNoOp()
    {
        int[] layer = Empty3x3();
        Assert.Equal(layer, MapLayerOps.BucketFill(layer, 3, 3, 5, 5, 9));
    }

    [Fact]
    public void Resize_Larger_PadsWithEmpty_PreservesTopLeft()
    {
        int[] layer = [1, 2, 3, 4]; // 2×2
        int[] next = MapLayerOps.Resize(layer, 2, 2, 3, 3);
        Assert.Equal([1, 2, -1, 3, 4, -1, -1, -1, -1], next);
    }

    [Fact]
    public void Resize_Smaller_Crops()
    {
        int[] layer = [1, 2, 3, 4, 5, 6, 7, 8, 9]; // 3×3
        int[] next = MapLayerOps.Resize(layer, 3, 3, 2, 2);
        Assert.Equal([1, 2, 4, 5], next);
    }

    [Fact]
    public void Resize_RejectsNonPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapLayerOps.Resize([1], 1, 1, 0, 3));
    }
}
