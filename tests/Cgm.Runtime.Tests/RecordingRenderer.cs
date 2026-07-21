using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>A context-free <see cref="IRenderer"/>: the same validation and lease bookkeeping as the
/// GL backend, with draws recorded instead of submitted. Lets scene flow and UI layout be asserted
/// headless, which is what the 16C reachability row needs.</summary>
internal sealed class RecordingRenderer : IRenderer
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
