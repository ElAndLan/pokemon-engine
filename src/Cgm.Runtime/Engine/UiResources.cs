namespace Cgm.Runtime.Engine;

/// <summary>Owns the shared UI atlas and hands out a painter. The atlas outlives individual scenes,
/// which borrow it, so it lives here rather than in any one scene (ENGINE_RUNTIME_SPEC 16C
/// ownership rule).</summary>
public sealed class UiResources : IDisposable
{
    private readonly IRenderer _renderer;
    private readonly TextureHandle _atlas;
    private bool _disposed;

    public UiResources(IRenderer renderer, QuadBatch batch)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
        _atlas = renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        Painter = new UiPainter(batch, _atlas, new BitmapFont());
    }

    public UiPainter Painter { get; }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _renderer.DisposeTexture(_atlas);
    }
}
