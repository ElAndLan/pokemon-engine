using Cgm.Core.Model;
using Cgm.Creator.Assets;

namespace Cgm.Creator.Tests.Assets;

/// <summary>ASSET_PIPELINE_SPEC 17B import v3: flood-fill components, noise discard, near-gap
/// merge, reading order.</summary>
public sealed class ComponentSlicerTests
{
    /// <summary>'#' = opaque, '.' = transparent; rows top to bottom.</summary>
    private static (bool[] Opaque, int W, int H) Grid(params string[] rows)
    {
        int w = rows[0].Length, h = rows.Length;
        var opaque = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                opaque[y * w + x] = rows[y][x] == '#';
        return (opaque, w, h);
    }

    [Fact]
    public void SeparatedSprites_BecomeSeparateRects_InReadingOrder()
    {
        var (op, w, h) = Grid(
            "##....##",
            "##....##",
            "........",
            "........",
            "...##...",
            "...##...");

        var rects = ComponentSlicer.Detect(op, w, h, mergeThreshold: 1);

        Assert.Equal(3, rects.Count);
        Assert.Equal(new Rect(0, 0, 2, 2), rects[0]); // top-left first
        Assert.Equal(new Rect(6, 0, 2, 2), rects[1]); // then left-to-right
        Assert.Equal(new Rect(3, 4, 2, 2), rects[2]); // then next row
    }

    [Fact]
    public void DiagonallyTouchingPixels_AreSeparateComponents_ButMergeWithinThreshold()
    {
        var (op, w, h) = Grid(
            "##..",
            "##..",
            "..##",
            "..##");

        // 4-neighbor: the diagonal halves are two components; their gap is 0 on both axes
        // (adjacent bounds), so the default threshold merges them into one sprite.
        Assert.Single(ComponentSlicer.Detect(op, w, h));
    }

    [Fact]
    public void OnePixelNoise_IsDiscarded()
    {
        var (op, w, h) = Grid(
            "###....",
            "###...#",  // lone pixel at far right
            "###....");

        var rects = ComponentSlicer.Detect(op, w, h, mergeThreshold: 0);
        Assert.Equal(new Rect(0, 0, 3, 3), Assert.Single(rects));
    }

    [Fact]
    public void GapWiderThanThreshold_StaysSeparate_WithinItMerges()
    {
        var (op, w, h) = Grid(
            "##...##");

        Assert.Equal(2, ComponentSlicer.Detect(op, w, 1, mergeThreshold: 2).Count); // gap of 3
        Assert.Single(ComponentSlicer.Detect(op, w, 1, mergeThreshold: 3));
    }

    [Fact]
    public void MergeIsTransitive_ChainsCollapseToOneRect()
    {
        // Three pieces, each within threshold of the next; a single pass that only merged pairs
        // once would leave two rects.
        var (op, w, h) = Grid(
            "##.##.##");

        var rects = ComponentSlicer.Detect(op, w, 1, mergeThreshold: 1);
        Assert.Equal(new Rect(0, 0, 8, 1), Assert.Single(rects));
    }

    [Fact]
    public void FullyTransparent_YieldsNoRects()
    {
        var (op, w, h) = Grid("....", "....");
        Assert.Empty(ComponentSlicer.Detect(op, w, h));
    }

    [Fact]
    public void SingleComponent_TightBounds()
    {
        var (op, w, h) = Grid(
            "......",
            "..##..",
            "..##..",
            "......");
        Assert.Equal(new Rect(2, 1, 2, 2), Assert.Single(ComponentSlicer.Detect(op, w, h)));
    }

    /// <summary>An outline broken by anti-alias-style gaps reads as one sprite (the merge's whole
    /// point): a hollow ring whose top arc is separated from the body by a 1px gap.</summary>
    [Fact]
    public void BrokenOutline_MergesIntoOneSprite()
    {
        var (op, w, h) = Grid(
            ".####.",
            "......",
            "#....#",
            "#....#",
            ".####.");

        Assert.Single(ComponentSlicer.Detect(op, w, h, mergeThreshold: 2));
    }

    [Fact]
    public void LargeImage_CompletesIteratively()
    {
        // One giant serpentine component across a 1024² image — recursion would overflow here.
        const int n = 1024;
        var opaque = new bool[n * n];
        Array.Fill(opaque, true);

        var rects = ComponentSlicer.Detect(opaque, n, n);
        Assert.Equal(new Rect(0, 0, n, n), Assert.Single(rects));
    }

    /// <summary>The 17B performance bound: a fully-opaque 4096² image (the worst case for the
    /// flood fill — one 16.7M-pixel component) completes. No timing assert — CI boxes vary — but
    /// a hang or overflow fails the run.</summary>
    [Fact]
    public void MaxSizeImage_4096Square_Completes()
    {
        const int n = 4096;
        var opaque = new bool[n * n];
        Array.Fill(opaque, true);

        var rects = ComponentSlicer.Detect(opaque, n, n);
        Assert.Equal(new Rect(0, 0, n, n), Assert.Single(rects));
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentException>(() => ComponentSlicer.Detect(new bool[3], 2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ComponentSlicer.Detect(new bool[4], 2, 2, -1));
    }
}
