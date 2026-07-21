using Cgm.Core.Model;
using StbImageSharp;

namespace Cgm.Runtime.Engine;

/// <summary>Decodes a PNG to the renderer's pixel format. Thin StbImageSharp adapter — the only
/// place in Runtime that knows an image file format, and the reason Core never needs to.</summary>
public static class PngImage
{
    /// <summary>Decodes to premultiplied RGBA, matching the renderer's ONE/ONE_MINUS_SRC_ALPHA
    /// blend. Straight alpha here would fringe every transparent sprite edge with black.</summary>
    public static (int Width, int Height, byte[] Rgba) Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        ImageResult image;
        try
        {
            image = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
        }
        catch (Exception ex)
        {
            // Stb signals a malformed image by throwing an assortment of types; content should
            // never take down a frame, so it becomes one predictable failure.
            throw new InvalidDataException($"Image could not be decoded: {ex.Message}", ex);
        }

        if (image.Width <= 0 || image.Height <= 0)
            throw new InvalidDataException($"Image has a non-positive size {image.Width}x{image.Height}.");

        byte[] pixels = image.Data;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = pixels[i + 3];
            if (alpha == 255)
                continue;
            pixels[i] = (byte)(pixels[i] * alpha / 255);
            pixels[i + 1] = (byte)(pixels[i + 1] * alpha / 255);
            pixels[i + 2] = (byte)(pixels[i + 2] * alpha / 255);
        }
        return (image.Width, image.Height, pixels);
    }
}

/// <summary>
/// Turns a <c>sprite:*</c> id into something drawable: the texture holding its sheet and the pixel
/// rectangle within it. Sheet images are decoded and uploaded once — normally by
/// <see cref="PreloadAll"/> at load time — and every texture is released on disposal, since a scene
/// cycle must not grow the renderer's lease count.
/// <para>Geometry comes from Core's <see cref="SpriteResolver"/>; this type only owns GPU
/// residency. A sprite whose sheet is missing or undecodable resolves to nothing, so absent art
/// degrades to the caller's fallback rather than failing the frame.</para>
/// </summary>
public sealed class SpriteAtlas : IDisposable
{
    private readonly IRenderer _renderer;
    private readonly IAssetSource _assets;
    private readonly SpriteResolver _resolver;
    private readonly IReadOnlyList<SpriteSheet> _sheets;
    private readonly Dictionary<string, TextureHandle?> _textures = new(StringComparer.Ordinal);
    private bool _disposed;

    public SpriteAtlas(IRenderer renderer, IAssetSource assets, IEnumerable<SpriteSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(sheets);
        _renderer = renderer;
        _assets = assets;
        _sheets = [.. sheets];
        _resolver = new SpriteResolver(_sheets);
    }

    /// <summary>Sheet images currently resident on the GPU. Failed loads are remembered but hold no
    /// texture, so they are not counted.</summary>
    public int LoadedTextures => _textures.Values.Count(handle => handle is not null);

    /// <summary>Sprites whose sheet could not be loaded, for the debug overlay. Reported rather than
    /// thrown: a missing image should be visible to a developer, not fatal to a player.</summary>
    public IReadOnlyCollection<string> FailedAssets =>
        [.. _textures.Where(pair => pair.Value is null).Select(pair => pair.Key)];

    /// <summary>Decodes and uploads every sheet up front. Loading lazily would put a PNG decode and
    /// a texture upload inside whichever frame first drew the sprite — precisely the stall the 16G
    /// steady-state budget rules out. Called before the metrics baseline, so it counts as load time.
    /// Failures are not thrown: <see cref="FailedAssets"/> reports them and the caller decides.</summary>
    public void PreloadAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (SpriteSheet sheet in _sheets)
            Load(sheet.Asset);
    }

    public bool TryGet(EntityId sprite, out TextureHandle texture, out RectI source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        texture = default;
        source = default;

        if (!_resolver.TryResolve(sprite, out SpriteSheet sheet, out Rect rect))
            return false;
        if (Load(sheet.Asset) is not { } handle)
            return false;

        texture = handle;
        source = new RectI(rect.X, rect.Y, rect.W, rect.H);

        // The sheet's recorded size is authored data; the decoded image is truth. A mismatch means
        // the slice was computed against different pixels, so it is refused rather than drawn.
        TextureInfo info = _renderer.Describe(handle);
        return info.Contains(source);
    }

    /// <summary>Uploads a sheet image once. A failure is cached as "absent" so a broken asset is not
    /// re-decoded every frame.</summary>
    private TextureHandle? Load(string asset)
    {
        string key = AssetPath.Normalize(asset);
        if (key.Length == 0)
            return null;
        if (_textures.TryGetValue(key, out TextureHandle? cached))
            return cached;

        TextureHandle? handle = null;
        if (_assets.TryRead(key, out byte[] bytes))
        {
            try
            {
                (int width, int height, byte[] rgba) = PngImage.Decode(bytes);
                handle = _renderer.CreateTexture(width, height, rgba);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
            {
                handle = null;   // recorded below as a failed asset
            }
        }

        _textures[key] = handle;
        return handle;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (TextureHandle? handle in _textures.Values)
            if (handle is { } live)
                _renderer.DisposeTexture(live);
        _textures.Clear();
    }
}
