using Cgm.Core.Model;
using Cgm.Creator.Assets;

namespace Cgm.Creator.Tests.Assets;

public sealed class GridSlicerTests
{
    [Fact]
    public void Slice_UniformGrid_NoOffsetOrSpacing()
    {
        IReadOnlyList<Rect> cells = GridSlicer.Slice(32, 32, new GridSpec(16, 16));
        Assert.Equal(4, cells.Count);
        Assert.Contains(new Rect(0, 0, 16, 16), cells);
        Assert.Contains(new Rect(16, 16, 16, 16), cells);
    }

    [Fact]
    public void Slice_HonorsMarginAndSpacing()
    {
        // 1px margin + 2px spacing between 16px cells: 1 + 16 + 2 + 16 = 35 wide/high → 2×2.
        IReadOnlyList<Rect> cells = GridSlicer.Slice(35, 35, new GridSpec(16, 16, 1, 1, 2, 2));
        Assert.Equal(4, cells.Count);
        Assert.Contains(new Rect(1, 1, 16, 16), cells);
        Assert.Contains(new Rect(19, 19, 16, 16), cells);
    }

    [Fact]
    public void Slice_IgnoresPartialTrailingCells()
    {
        // 40 wide, 16px cells → cells at x=0,16 (x=32 would overflow). 2×2 = 4.
        Assert.Equal(4, GridSlicer.Slice(40, 40, new GridSpec(16, 16)).Count);
    }

    [Fact]
    public void Slice_CellBiggerThanImage_Empty()
    {
        Assert.Empty(GridSlicer.Slice(10, 10, new GridSpec(16, 16)));
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(16, 0)]
    [InlineData(-1, 16)]
    public void Slice_RejectsNonPositiveCells(int cw, int ch)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridSlicer.Slice(64, 64, new GridSpec(cw, ch)));
    }

    [Fact]
    public void Slice_RejectsNegativeOffsetOrSpacing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridSlicer.Slice(64, 64, new GridSpec(16, 16, -1)));
    }
}

public sealed class SizeSuggesterTests
{
    [Theory]
    [InlineData(64, 64, 64)]
    [InlineData(48, 48, 48)]
    [InlineData(32, 48, 16)]  // common divisor in the set is 16
    [InlineData(96, 96, 48)]  // 16/32/48 divide; 48 is largest (64 doesn't divide 96)
    public void Suggest_ReturnsLargestDivisor(int w, int h, int expected)
    {
        Assert.Equal(expected, SizeSuggester.Suggest(w, h));
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData(17, 64)]
    public void Suggest_NoCommonSize_ReturnsNull(int w, int h)
    {
        Assert.Null(SizeSuggester.Suggest(w, h));
    }

    [Fact]
    public void Suggest_PrefersProjectTileSizeWhenItFits()
    {
        Assert.Equal(16, SizeSuggester.Suggest(64, 64, preferTileSize: 16));
        // Preferred size that doesn't divide → falls back to largest.
        Assert.Equal(96 % 64 == 0 ? 64 : 48, SizeSuggester.Suggest(96, 96, preferTileSize: 64));
    }
}

public sealed class GutterDetectorTests
{
    /// <summary>Builds an opaque grid where a pixel is opaque iff both its column and row fall in a cell band.</summary>
    private static bool[] Build(int size, Func<int, bool> inCell)
    {
        var op = new bool[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                op[y * size + x] = inCell(x) && inCell(y);
        return op;
    }

    [Fact]
    public void Detect_GutteredGridWithMargin()
    {
        // 1px margin, two 3px cells, 2px gutter, 1px trailing → size 10.
        bool[] op = Build(10, i => (i is >= 1 and <= 3) || (i is >= 6 and <= 8));
        GutterFit fit = GutterDetector.Detect(op, 10, 10)!.Value;
        Assert.Equal(new GutterFit(3, 3, 2, 2, 1, 1), fit);
    }

    [Fact]
    public void Detect_BlankCellStillDetected()
    {
        // Same grid but the top-left cell is empty; its column/row are still occupied by neighbours.
        var op = Build(10, i => (i is >= 1 and <= 3) || (i is >= 6 and <= 8));
        for (int y = 1; y <= 3; y++)
            for (int x = 1; x <= 3; x++)
                op[y * 10 + x] = false;
        Assert.Equal(new GutterFit(3, 3, 2, 2, 1, 1), GutterDetector.Detect(op, 10, 10)!.Value);
    }

    [Fact]
    public void Detect_GutterlessSheet_ReturnsNull()
    {
        var op = new bool[16 * 16];
        Array.Fill(op, true); // one solid block, no gutters
        Assert.Null(GutterDetector.Detect(op, 16, 16));
    }

    [Fact]
    public void Detect_NonUniformCells_ReturnsNull()
    {
        // cells of width 3 then 4 → not a clean grid.
        bool[] op = Build(12, i => (i is >= 1 and <= 3) || (i is >= 6 and <= 9));
        Assert.Null(GutterDetector.Detect(op, 12, 12));
    }

    [Fact]
    public void Detect_SingleCellPerAxis_ReturnsNull()
    {
        // One centred blob → only one occupied run per axis, no repeating grid.
        bool[] op = Build(10, i => i is >= 3 and <= 6);
        Assert.Null(GutterDetector.Detect(op, 10, 10));
    }

    [Fact]
    public void Detect_RejectsMismatchedDimensions()
    {
        Assert.Throws<ArgumentException>(() => GutterDetector.Detect(new bool[5], 3, 3));
    }
}
