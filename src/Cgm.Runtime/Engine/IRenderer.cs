namespace Cgm.Runtime.Engine;

/// <summary>Validated atlas dimensions. Source rectangles are checked against this at load time, so
/// an invalid rectangle fails asset loading rather than the draw loop (ENGINE_RUNTIME_SPEC 16B).</summary>
public readonly record struct TextureInfo(int Width, int Height)
{
    public const int BytesPerPixel = 4;

    /// <summary>Validates RGBA atlas bytes against their declared dimensions.</summary>
    public static TextureInfo Validate(int width, int height, int byteLength)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), $"Texture size {width}x{height} must be positive.");
        long expected = (long)width * height * BytesPerPixel;
        if (byteLength != expected)
            throw new ArgumentException(
                $"Texture {width}x{height} needs {expected} RGBA bytes but got {byteLength}.", nameof(byteLength));
        return new TextureInfo(width, height);
    }

    /// <summary>True when a half-open source rectangle lies wholly inside the texture.</summary>
    public bool Contains(RectI source) =>
        !source.IsEmpty && source.X >= 0 && source.Y >= 0 && source.Right <= Width && source.Bottom <= Height;
}

/// <summary>The single rendering seam (ENGINE_RUNTIME_SPEC 16B). Scenes see no GL, buffer, shader,
/// or texture handle beyond an opaque <see cref="TextureHandle"/> lease. Implemented by the OpenGL
/// backend and, in tests, by a recording renderer that needs no context.</summary>
public interface IRenderer : IDisposable
{
    /// <summary>Uploads a validated RGBA atlas and returns its lease.</summary>
    TextureHandle CreateTexture(int width, int height, ReadOnlySpan<byte> rgba);

    /// <summary>Releases a lease. Repeating disposal is harmless.</summary>
    void DisposeTexture(TextureHandle texture);

    /// <summary>Dimensions of a live lease, for source-rectangle validation.</summary>
    TextureInfo Describe(TextureHandle texture);

    /// <summary>Starts a frame: sets the scaled viewport and paints the letterbox.</summary>
    void BeginFrame(Viewport viewport, int virtualWidth, int virtualHeight, Rgba clear);

    /// <summary>Draws one batch's sorted quads through its grouped draw calls.</summary>
    void Draw(IReadOnlyList<Quad> quads, IReadOnlyList<DrawCall> calls);

    /// <summary>Ends the frame, leaving no scissor state behind.</summary>
    void EndFrame();
}
