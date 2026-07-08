using Cgm.Core.Model;
using Cgm.Creator.Assets;

namespace Cgm.Creator.Tests.Assets;

public sealed class AtlasPackerTests
{
    private static List<(int, int)> Sizes(params (int, int)[] s) => [.. s];

    // --- Invariants asserted across many cases ----------------------------------

    private static void AssertValid(IReadOnlyList<(int W, int H)> sizes, IReadOnlyList<Atlas> atlases, int maxSize)
    {
        // Every input sprite placed exactly once.
        var seen = atlases.SelectMany(a => a.Placements).Select(p => p.SpriteIndex).ToList();
        Assert.Equal(sizes.Count, seen.Count);
        Assert.Equal(Enumerable.Range(0, sizes.Count).ToHashSet(), seen.ToHashSet());

        foreach (Atlas atlas in atlases)
        {
            foreach (AtlasPlacement p in atlas.Placements)
            {
                // Placement preserves the sprite's size.
                Assert.Equal(sizes[p.SpriteIndex].W, p.Rect.W);
                Assert.Equal(sizes[p.SpriteIndex].H, p.Rect.H);

                // Within atlas extent and within the max canvas.
                Assert.InRange(p.Rect.X, 0, maxSize - p.Rect.W);
                Assert.InRange(p.Rect.Y, 0, maxSize - p.Rect.H);
                Assert.True(p.Rect.X + p.Rect.W <= atlas.Width);
                Assert.True(p.Rect.Y + p.Rect.H <= atlas.Height);
            }

            // No two rects in the same atlas overlap.
            var rects = atlas.Placements.Select(p => p.Rect).ToList();
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.False(Overlaps(rects[i], rects[j]), $"overlap: {rects[i]} vs {rects[j]}");
        }
    }

    private static bool Overlaps(Rect a, Rect b) =>
        a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;

    // --- Cases ------------------------------------------------------------------

    [Fact]
    public void Empty_ProducesNoAtlases()
    {
        Assert.Empty(AtlasPacker.Pack(Sizes()));
    }

    [Fact]
    public void SingleSprite_OneAtlas_AtOrigin()
    {
        var sizes = Sizes((10, 20));
        var atlases = AtlasPacker.Pack(sizes, maxSize: 64);
        Assert.Single(atlases);
        Assert.Equal(new Rect(0, 0, 10, 20), atlases[0].Placements[0].Rect);
        Assert.Equal(10, atlases[0].Width);
        Assert.Equal(20, atlases[0].Height);
        AssertValid(sizes, atlases, 64);
    }

    [Fact]
    public void ManySprites_FitInOneAtlas_NoOverlap()
    {
        var sizes = Sizes((16, 16), (32, 8), (8, 24), (16, 16), (40, 40), (4, 60));
        var atlases = AtlasPacker.Pack(sizes, maxSize: 128);
        Assert.Single(atlases);
        AssertValid(sizes, atlases, 128);
    }

    [Fact]
    public void Overflow_SplitsAcrossMultipleAtlases()
    {
        // Six 40×40 sprites can't share a single 64² atlas → must split.
        var sizes = Sizes((40, 40), (40, 40), (40, 40), (40, 40), (40, 40), (40, 40));
        var atlases = AtlasPacker.Pack(sizes, maxSize: 64);
        Assert.True(atlases.Count > 1);
        AssertValid(sizes, atlases, 64);
    }

    [Fact]
    public void Deterministic_SameInputSameOutput()
    {
        var sizes = Sizes((13, 7), (21, 21), (5, 30), (30, 5), (8, 8), (17, 12), (9, 25));
        var a = AtlasPacker.Pack(sizes, maxSize: 48);
        var b = AtlasPacker.Pack(sizes, maxSize: 48);
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
            Assert.Equal(a[i].Placements, b[i].Placements); // record-struct equality, element-wise
    }

    [Fact]
    public void ExactlyFillsAtlas_NoOverlapNoOverflow()
    {
        // Four 32×32 exactly tile a 64² atlas.
        var sizes = Sizes((32, 32), (32, 32), (32, 32), (32, 32));
        var atlases = AtlasPacker.Pack(sizes, maxSize: 64);
        Assert.Single(atlases);
        Assert.Equal(64, atlases[0].Width);
        Assert.Equal(64, atlases[0].Height);
        AssertValid(sizes, atlases, 64);
    }

    [Theory]
    [InlineData(2049, 10)] // too wide
    [InlineData(10, 2049)] // too tall
    public void SpriteLargerThanMax_Throws(int w, int h)
    {
        Assert.Throws<ArgumentException>(() => AtlasPacker.Pack(Sizes((w, h))));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void NonPositiveSize_Throws(int w, int h)
    {
        Assert.Throws<ArgumentException>(() => AtlasPacker.Pack(Sizes((w, h))));
    }

    [Fact]
    public void StressManyRandomSprites_InvariantsHold()
    {
        var rng = new Random(1234);
        var sizes = new List<(int, int)>();
        for (int i = 0; i < 300; i++)
            sizes.Add((rng.Next(1, 65), rng.Next(1, 65)));

        var atlases = AtlasPacker.Pack(sizes, maxSize: 256);
        AssertValid(sizes, atlases, 256);
    }
}
