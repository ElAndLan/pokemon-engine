using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>PNG decode into the renderer's pixel format. The fixtures are real encoded PNGs, so a
/// decoder that stopped working would fail here rather than pass against a mock.</summary>
public sealed class PngImageTests
{
    [Fact]
    public void AnOpaqueImageDecodesToItsSizeAndColour()
    {
        (int width, int height, byte[] rgba) = PngImage.Decode(TestPng.Solid(4, 3, 200, 100, 50));

        Assert.Equal(4, width);
        Assert.Equal(3, height);
        Assert.Equal(4 * 3 * 4, rgba.Length);
        Assert.Equal([200, 100, 50, 255], rgba[..4]);
    }

    /// <summary>The renderer blends premultiplied, so colour must be scaled by alpha at decode. Left
    /// straight, every transparent sprite edge would fringe dark.</summary>
    [Fact]
    public void TranslucentPixelsArePremultiplied()
    {
        (_, _, byte[] rgba) = PngImage.Decode(TestPng.Solid(1, 1, 200, 100, 50, 128));

        Assert.Equal(200 * 128 / 255, rgba[0]);
        Assert.Equal(100 * 128 / 255, rgba[1]);
        Assert.Equal(50 * 128 / 255, rgba[2]);
        Assert.Equal(128, rgba[3]);   // alpha itself is untouched
    }

    [Fact]
    public void FullyTransparentPixelsGoToZero()
    {
        (_, _, byte[] rgba) = PngImage.Decode(TestPng.Solid(1, 1, 255, 255, 255, 0));
        Assert.Equal([0, 0, 0, 0], rgba[..4]);
    }

    /// <summary>Opaque pixels must come through bit-exact, not round-tripped through a divide.</summary>
    [Fact]
    public void OpaquePixelsAreUnchanged()
    {
        (_, _, byte[] rgba) = PngImage.Decode(TestPng.Solid(1, 1, 1, 2, 3));
        Assert.Equal([1, 2, 3, 255], rgba[..4]);
    }

    [Fact]
    public void PerPixelAlphaIsAppliedIndependently()
    {
        byte[] source = [255, 255, 255, 255, 255, 255, 255, 0];   // opaque then transparent
        (_, _, byte[] rgba) = PngImage.Decode(TestPng.Encode(2, 1, source));

        Assert.Equal([255, 255, 255, 255], rgba[..4]);
        Assert.Equal([0, 0, 0, 0], rgba[4..8]);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3, 4 })]
    [InlineData(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0, 0, 0, 0, 0, 0 })]  // header only
    public void MalformedBytesBecomeOnePredictableFailure(byte[] bytes) =>
        Assert.Throws<InvalidDataException>(() => PngImage.Decode(bytes));

    [Fact]
    public void NullBytesAreRejected() =>
        Assert.Throws<ArgumentNullException>(() => PngImage.Decode(null!));
}

/// <summary>Sprite id → drawable. Geometry comes from Core; this owns GPU residency, so the tests
/// centre on caching, failure tolerance, and lease lifetime.</summary>
public sealed class SpriteAtlasTests : IDisposable
{
    private readonly RecordingRenderer _renderer = new();

    public void Dispose() => _renderer.Dispose();

    private const string Asset = "assets/sheet.png";
    private static EntityId Id(string slug) => EntityId.Parse("sprite:" + slug);

    /// <summary>A 32x16 sheet of two 16x16 cells.</summary>
    private static SpriteSheet Sheet(string asset = Asset, int imageW = 32, int imageH = 16) => new()
    {
        Id = EntityId.Parse("sheet:s"), Name = "S", Asset = asset,
        ImageW = imageW, ImageH = imageH, Mode = SliceMode.Grid, CellW = 16, CellH = 16,
        Cells =
        [
            new SheetCell { Index = 0, SpriteId = Id("a") },
            new SheetCell { Index = 1, SpriteId = Id("b") },
        ],
    };

    private static IAssetSource Source(string path, byte[] bytes) =>
        new PackAssetSource(new Dictionary<string, byte[]> { [path] = bytes });

    private static IAssetSource Working(int w = 32, int h = 16) =>
        Source(Asset, TestPng.Solid(w, h, 10, 20, 30));

    private SpriteAtlas Atlas(IAssetSource? assets = null, SpriteSheet? sheet = null) =>
        new(_renderer, assets ?? Working(), [sheet ?? Sheet()]);

    // --- Resolution -------------------------------------------------------------------

    [Fact]
    public void ASpriteResolvesToItsSheetTextureAndCell()
    {
        using SpriteAtlas atlas = Atlas();

        Assert.True(atlas.TryGet(Id("a"), out TextureHandle first, out RectI a));
        Assert.True(atlas.TryGet(Id("b"), out TextureHandle second, out RectI b));

        Assert.Equal(new RectI(0, 0, 16, 16), a);
        Assert.Equal(new RectI(16, 0, 16, 16), b);
        Assert.Equal(first, second);   // one sheet, one texture
    }

    /// <summary>Sheets upload once. Re-decoding per lookup would blow the frame budget outright.</summary>
    [Fact]
    public void ASheetIsDecodedAndUploadedOnlyOnce()
    {
        using SpriteAtlas atlas = Atlas();
        for (int i = 0; i < 20; i++)
        {
            atlas.TryGet(Id("a"), out _, out _);
            atlas.TryGet(Id("b"), out _, out _);
        }

        Assert.Equal(1, atlas.LoadedTextures);
        Assert.Equal(1, _renderer.TextureCount);
    }

    [Fact]
    public void AnUnknownSpriteResolvesToNothing()
    {
        using SpriteAtlas atlas = Atlas();
        Assert.False(atlas.TryGet(Id("missing"), out _, out _));
        Assert.Equal(0, atlas.LoadedTextures);   // an unknown sprite loads no image
    }

    [Fact]
    public void AnAtlasWithNoSheetsResolvesNothing()
    {
        using var atlas = new SpriteAtlas(_renderer, Working(), []);
        Assert.False(atlas.TryGet(Id("a"), out _, out _));
    }

    // --- Failure tolerance ------------------------------------------------------------

    /// <summary>Missing art must degrade to the caller's fallback, never fail the frame.</summary>
    [Fact]
    public void AMissingAssetResolvesToNothingAndIsReported()
    {
        using SpriteAtlas atlas = Atlas(new PackAssetSource(new Dictionary<string, byte[]>()));

        Assert.False(atlas.TryGet(Id("a"), out _, out _));
        Assert.Equal(0, atlas.LoadedTextures);
        Assert.Equal([Asset], atlas.FailedAssets);
    }

    [Fact]
    public void AnUndecodableAssetResolvesToNothingAndIsReported()
    {
        using SpriteAtlas atlas = Atlas(Source(Asset, [1, 2, 3, 4]));

        Assert.False(atlas.TryGet(Id("a"), out _, out _));
        Assert.Equal([Asset], atlas.FailedAssets);
    }

    /// <summary>A broken asset is remembered, so it is not re-decoded on every frame.</summary>
    [Fact]
    public void AFailedLoadIsNotRetried()
    {
        var counting = new CountingAssetSource(Source(Asset, [1, 2, 3, 4]));
        using var atlas = new SpriteAtlas(_renderer, counting, [Sheet()]);

        for (int i = 0; i < 10; i++)
            atlas.TryGet(Id("a"), out _, out _);

        Assert.Equal(1, counting.Reads);
    }

    [Fact]
    public void AnUnsafeAssetPathResolvesToNothing()
    {
        using SpriteAtlas atlas = Atlas(Working(), Sheet(asset: "../escape.png"));
        Assert.False(atlas.TryGet(Id("a"), out _, out _));
    }

    /// <summary>The sheet's recorded size is authored data; the decoded image is the truth. If they
    /// disagree the slice was computed against different pixels, so it must be refused rather than
    /// sampling whatever happens to be there.</summary>
    [Fact]
    public void ACellOutsideTheDecodedImageIsRefused()
    {
        // The sheet claims 32x16 (two cells) but the real image is only 16x16.
        using SpriteAtlas atlas = Atlas(Working(16, 16));

        Assert.True(atlas.TryGet(Id("a"), out _, out _));    // first cell genuinely fits
        Assert.False(atlas.TryGet(Id("b"), out _, out _));   // second does not
    }

    // --- Preload --------------------------------------------------------------------

    /// <summary>Sheets must be resident before the first frame that draws them, or the decode lands
    /// inside that frame and blows the 16G budget.</summary>
    [Fact]
    public void PreloadUploadsEverySheetWithoutADraw()
    {
        using SpriteAtlas atlas = Atlas();
        atlas.PreloadAll();

        Assert.Equal(1, atlas.LoadedTextures);
        Assert.Equal(1, _renderer.TextureCount);
    }

    /// <summary>After preloading, drawing must not touch the asset source again.</summary>
    [Fact]
    public void PreloadingMeansDrawingReadsNothingFurther()
    {
        var counting = new CountingAssetSource(Working());
        using var atlas = new SpriteAtlas(_renderer, counting, [Sheet()]);

        atlas.PreloadAll();
        int afterPreload = counting.Reads;
        for (int i = 0; i < 10; i++)
            atlas.TryGet(Id("a"), out _, out _);

        Assert.Equal(afterPreload, counting.Reads);
    }

    [Fact]
    public void PreloadIsIdempotent()
    {
        using SpriteAtlas atlas = Atlas();
        atlas.PreloadAll();
        atlas.PreloadAll();

        Assert.Equal(1, _renderer.TextureCount);
    }

    /// <summary>A broken sheet must not stop the others loading, or one bad file blanks the game.</summary>
    [Fact]
    public void PreloadRecordsFailuresAndKeepsGoing()
    {
        var sheets = new[] { Sheet(), Sheet() with { Id = EntityId.Parse("sheet:t"), Asset = "assets/broken.png" } };
        using var atlas = new SpriteAtlas(_renderer,
            new PackAssetSource(new Dictionary<string, byte[]>
            {
                [Asset] = TestPng.Solid(32, 16, 1, 2, 3),
                ["assets/broken.png"] = [9, 9, 9],
            }),
            sheets);

        atlas.PreloadAll();

        Assert.Equal(1, atlas.LoadedTextures);
        Assert.Equal(["assets/broken.png"], atlas.FailedAssets);
    }

    [Fact]
    public void PreloadAfterDisposeIsRejected()
    {
        var atlas = Atlas();
        atlas.Dispose();
        Assert.Throws<ObjectDisposedException>(atlas.PreloadAll);
    }

    // --- Lifetime ---------------------------------------------------------------------

    [Fact]
    public void DisposeReleasesEveryTexture()
    {
        var atlas = Atlas();
        atlas.TryGet(Id("a"), out _, out _);
        Assert.Equal(1, _renderer.TextureCount);

        atlas.Dispose();
        Assert.Equal(0, _renderer.TextureCount);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var atlas = Atlas();
        atlas.TryGet(Id("a"), out _, out _);
        atlas.Dispose();
        atlas.Dispose();
        Assert.Equal(0, _renderer.TextureCount);
    }

    [Fact]
    public void UseAfterDisposeIsRejected()
    {
        var atlas = Atlas();
        atlas.Dispose();
        Assert.Throws<ObjectDisposedException>(() => atlas.TryGet(Id("a"), out _, out _));
    }

    /// <summary>The 16G resource row: repeated atlas cycles must not grow the lease count.</summary>
    [Fact]
    public void RepeatedAtlasCyclesLeaveNoLeaseGrowth()
    {
        for (int cycle = 0; cycle < 100; cycle++)
        {
            using SpriteAtlas atlas = Atlas();
            atlas.TryGet(Id("a"), out _, out _);
        }

        Assert.Equal(0, _renderer.TextureCount);
    }

    [Fact]
    public void NullDependenciesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SpriteAtlas(null!, Working(), [Sheet()]));
        Assert.Throws<ArgumentNullException>(() => new SpriteAtlas(_renderer, null!, [Sheet()]));
        Assert.Throws<ArgumentNullException>(() => new SpriteAtlas(_renderer, Working(), null!));
    }

    /// <summary>Counts reads so a caching claim can be asserted rather than assumed.</summary>
    private sealed class CountingAssetSource(IAssetSource inner) : IAssetSource
    {
        public int Reads { get; private set; }

        public bool TryRead(string path, out byte[] bytes)
        {
            Reads++;
            return inner.TryRead(path, out bytes);
        }
    }
}
