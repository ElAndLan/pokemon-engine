using Avalonia.Media.Imaging;
using Cgm.Core.Model;
using Cgm.Creator;

namespace Cgm.Creator.Views;

/// <summary>
/// Loads and caches sheet bitmaps for a project and crops individual sprites out of them, so the
/// tileset palette, map canvas, and animation preview all resolve <c>sprite:*</c> ids to pixels
/// the same way. Disposable — owns the decoded sheet bitmaps.
/// </summary>
public sealed class SpriteBitmaps(ProjectSession session) : IDisposable
{
    private readonly Dictionary<string, Bitmap?> _sheets = [];
    private readonly Dictionary<EntityId, CroppedBitmap?> _crops = [];
    private readonly SpriteResolver _resolver = new(session.All<SpriteSheet>());

    /// <summary>The cropped sprite, or null when unresolved or the sheet art is missing.</summary>
    public CroppedBitmap? Crop(EntityId sprite)
    {
        if (_crops.TryGetValue(sprite, out CroppedBitmap? cached))
            return cached;
        if (!_resolver.TryResolve(sprite, out SpriteSheet sheet, out Rect rect))
            return null;
        if (Load(sheet.Asset) is not { } bitmap)
            return null;
        // Guard against a rect that runs past the actual decoded image (stale slice metadata).
        if (rect.X < 0 || rect.Y < 0 || rect.X + rect.W > bitmap.PixelSize.Width
            || rect.Y + rect.H > bitmap.PixelSize.Height || rect.W <= 0 || rect.H <= 0)
            return null;
        var crop = new CroppedBitmap(bitmap, new Avalonia.PixelRect(rect.X, rect.Y, rect.W, rect.H));
        _crops[sprite] = crop;
        return crop;
    }

    private Bitmap? Load(string asset)
    {
        if (_sheets.TryGetValue(asset, out Bitmap? cached))
            return cached;
        Bitmap? bitmap = null;
        try
        {
            using var stream = new MemoryStream(session.ReadAsset(asset), writable: false);
            bitmap = new Bitmap(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Missing/corrupt art: the asset-file diagnostic reports it; here we just draw nothing.
        }
        _sheets[asset] = bitmap;
        return bitmap;
    }

    public void Dispose()
    {
        foreach (CroppedBitmap? crop in _crops.Values)
            crop?.Dispose();
        _crops.Clear();
        foreach (Bitmap? bitmap in _sheets.Values)
            bitmap?.Dispose();
        _sheets.Clear();
    }
}
