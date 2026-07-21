using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>Texture validation and the <see cref="IRenderer"/> contract, exercised without a GL
/// context. The OpenGL backend's own draw path is covered by the hidden-window smoke run.</summary>
public sealed class RendererTests
{
    // --- Texture validation ------------------------------------------------------

    [Fact]
    public void Validate_AcceptsExactRgbaLength()
    {
        TextureInfo info = TextureInfo.Validate(4, 3, 4 * 3 * 4);
        Assert.Equal(4, info.Width);
        Assert.Equal(3, info.Height);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    [InlineData(4, -1)]
    public void Validate_RejectsNonPositiveDimensions(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureInfo.Validate(width, height, 64));

    [Theory]
    [InlineData(63)]
    [InlineData(65)]
    [InlineData(0)]
    public void Validate_RejectsWrongByteLength(int length) =>
        Assert.Throws<ArgumentException>(() => TextureInfo.Validate(4, 4, length));

    /// <summary>A 16k x 16k atlas overflows 32-bit pixel maths if the check is not widened.</summary>
    [Fact]
    public void Validate_DoesNotOverflowOnHugeDimensions() =>
        Assert.Throws<ArgumentException>(() => TextureInfo.Validate(16384, 16384, 1024));

    [Theory]
    [InlineData(0, 0, 16, 16, true)]     // exact fit
    [InlineData(8, 8, 8, 8, true)]       // bottom-right corner
    [InlineData(0, 0, 17, 16, false)]    // one past the right edge
    [InlineData(0, 0, 16, 17, false)]    // one past the bottom edge
    [InlineData(-1, 0, 4, 4, false)]     // negative origin
    [InlineData(0, -1, 4, 4, false)]
    [InlineData(0, 0, 0, 4, false)]      // empty
    [InlineData(16, 0, 1, 1, false)]     // starts exactly at the edge
    public void Contains_UsesHalfOpenBoundsInsideTheTexture(int x, int y, int w, int h, bool inside) =>
        Assert.Equal(inside, new TextureInfo(16, 16).Contains(new RectI(x, y, w, h)));

    // --- IRenderer contract, via a context-free recorder -------------------------

    [Fact]
    public void CreateTexture_ValidatesBeforeIssuingALease()
    {
        using var renderer = new RecordingRenderer();
        Assert.Throws<ArgumentException>(() => renderer.CreateTexture(2, 2, new byte[3]));
        Assert.Equal(0, renderer.TextureCount);
    }

    [Fact]
    public void Leases_AreDistinctAndDescribable()
    {
        using var renderer = new RecordingRenderer();
        TextureHandle a = renderer.CreateTexture(2, 2, new byte[16]);
        TextureHandle b = renderer.CreateTexture(4, 1, new byte[16]);

        Assert.NotEqual(a, b);
        Assert.Equal(new TextureInfo(2, 2), renderer.Describe(a));
        Assert.Equal(new TextureInfo(4, 1), renderer.Describe(b));
    }

    [Fact]
    public void DisposeTexture_IsIdempotentAndReleasesTheLease()
    {
        using var renderer = new RecordingRenderer();
        TextureHandle handle = renderer.CreateTexture(2, 2, new byte[16]);

        renderer.DisposeTexture(handle);
        renderer.DisposeTexture(handle);   // repeating disposal is harmless
        renderer.DisposeTexture(new TextureHandle(999));

        Assert.Equal(0, renderer.TextureCount);
        Assert.Throws<ArgumentException>(() => renderer.Describe(handle));
    }

    [Fact]
    public void Dispose_ReleasesEveryLeaseAndIsIdempotent()
    {
        var renderer = new RecordingRenderer();
        renderer.CreateTexture(2, 2, new byte[16]);
        renderer.CreateTexture(2, 2, new byte[16]);

        renderer.Dispose();
        renderer.Dispose();
        Assert.Equal(0, renderer.TextureCount);
    }

    [Fact]
    public void UseAfterDispose_IsRejected()
    {
        var renderer = new RecordingRenderer();
        renderer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => renderer.CreateTexture(2, 2, new byte[16]));
    }

    /// <summary>A hundred load/unload cycles must leave no live lease behind (leak-check shape).</summary>
    [Fact]
    public void RepeatedLoadUnloadCycles_LeaveNoLiveLeases()
    {
        using var renderer = new RecordingRenderer();
        for (int i = 0; i < 100; i++)
            renderer.DisposeTexture(renderer.CreateTexture(8, 8, new byte[8 * 8 * 4]));
        Assert.Equal(0, renderer.TextureCount);
    }

    // --- Batch to renderer -------------------------------------------------------

    [Fact]
    public void BatchOutput_ReachesTheRendererInSortedOrder()
    {
        using var renderer = new RecordingRenderer();
        TextureHandle tex = renderer.CreateTexture(16, 16, new byte[16 * 16 * 4]);

        var batch = new QuadBatch();
        batch.Begin();
        batch.Ui(tex, new RectI(0, 0, 16, 16), new RectI(0, 0, 16, 16), layer: 3);
        batch.Ui(tex, new RectI(0, 0, 16, 16), new RectI(9, 0, 16, 16), layer: 1);
        var (quads, calls, _) = batch.End();

        renderer.BeginFrame(new Viewport(2, 0, 0, 512, 384), 256, 192, new Rgba(0, 0, 0, 255));
        renderer.Draw(quads, calls);
        renderer.EndFrame();

        Assert.Equal([9, 0], renderer.Drawn.Select(q => q.Dest.X).ToArray());
        Assert.Equal(1, renderer.FramesBegun);
        Assert.Equal(1, renderer.FramesEnded);
    }

    [Fact]
    public void BeginFrame_RejectsNonPositiveVirtualSize()
    {
        using var renderer = new RecordingRenderer();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            renderer.BeginFrame(new Viewport(1, 0, 0, 10, 10), 0, 192, new Rgba(0, 0, 0, 255)));
    }

    /// <summary>A context-free IRenderer: the same validation and lease bookkeeping as the GL
    /// backend, with draws recorded instead of submitted.</summary>
    private sealed class RecordingRenderer : IRenderer
    {
        private readonly Dictionary<int, TextureInfo> _textures = [];
        private int _next = 1;
        private bool _disposed;

        public List<Quad> Drawn { get; } = [];
        public int FramesBegun { get; private set; }
        public int FramesEnded { get; private set; }
        public int TextureCount => _textures.Count;

        public TextureHandle CreateTexture(int width, int height, ReadOnlySpan<byte> rgba)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            TextureInfo info = TextureInfo.Validate(width, height, rgba.Length);
            var handle = new TextureHandle(_next++);
            _textures[handle.Id] = info;
            return handle;
        }

        public void DisposeTexture(TextureHandle texture) => _textures.Remove(texture.Id);

        public TextureInfo Describe(TextureHandle texture) =>
            _textures.TryGetValue(texture.Id, out TextureInfo info)
                ? info
                : throw new ArgumentException($"Texture {texture.Id} is not a live lease.", nameof(texture));

        public void BeginFrame(Viewport viewport, int virtualWidth, int virtualHeight, Rgba clear)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (virtualWidth <= 0 || virtualHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(virtualWidth), "Virtual size must be positive.");
            FramesBegun++;
        }

        public void Draw(IReadOnlyList<Quad> quads, IReadOnlyList<DrawCall> calls)
        {
            foreach (Quad quad in quads)
            {
                if (!_textures.TryGetValue(quad.Texture.Id, out TextureInfo info) || !info.Contains(quad.Source))
                    throw new ArgumentException($"Bad quad against texture {quad.Texture.Id}.", nameof(quads));
                Drawn.Add(quad);
            }
        }

        public void EndFrame() => FramesEnded++;

        public void Dispose()
        {
            _disposed = true;
            _textures.Clear();
        }
    }
}
