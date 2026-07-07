using Cgm.Core.Model;
using StbImageSharp;

namespace Cgm.Creator.Assets;

/// <summary>Decodes a PNG to <see cref="ImageData"/> (opacity per pixel). Thin StbImageSharp adapter.</summary>
public static class PngDecoder
{
    public static ImageData Decode(byte[] bytes)
    {
        ImageResult img = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
        var opaque = new bool[img.Width * img.Height];
        for (int i = 0; i < opaque.Length; i++)
            opaque[i] = img.Data[i * 4 + 3] > 0; // alpha > 0
        return new ImageData(img.Width, img.Height, opaque);
    }

    public static ImageData DecodeFile(string path) => Decode(File.ReadAllBytes(path));
}

/// <summary>Imports a PNG into a <see cref="SpriteSheet"/>: decode → suggest a cell size → slice
/// (ASSET_PIPELINE_SPEC v0–v1). Falls back to the project tile size, then the image size.</summary>
public static class SheetImporter
{
    public static SpriteSheet Import(EntityId sheetId, string pngPath, string assetRelPath, int? tileSize)
    {
        ImageData image = PngDecoder.DecodeFile(pngPath);
        int size = SizeSuggester.Suggest(image.Width, image.Height, tileSize)
            ?? tileSize
            ?? Math.Min(image.Width, image.Height);
        return SheetBuilder.Build(sheetId, assetRelPath, image, new GridSpec(size, size));
    }
}
