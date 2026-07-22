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

/// <summary>Imports a decoded image as a <see cref="SpriteSheet"/> using the suggestion ladder
/// (ASSET_PIPELINE_SPEC 17B): v2 gutter fit → v1 common size → project tile size → whole image.</summary>
public static class SheetImporter
{
    public static SpriteSheet Import(EntityId sheetId, string pngPath, string assetRelPath, int? tileSize) =>
        Import(sheetId, PngDecoder.DecodeFile(pngPath), assetRelPath, tileSize);

    public static SpriteSheet Import(EntityId sheetId, ImageData image, string assetRelPath, int? tileSize)
    {
        if (GutterDetector.Detect(image.Opaque, image.Width, image.Height) is { } fit)
            return SheetBuilder.Build(sheetId, assetRelPath, image,
                new GridSpec(fit.CellW, fit.CellH, fit.MarginX, fit.MarginY, fit.SpacingX, fit.SpacingY));

        int size = SizeSuggester.Suggest(image.Width, image.Height, tileSize)
            ?? tileSize
            ?? Math.Min(image.Width, image.Height);
        return SheetBuilder.Build(sheetId, assetRelPath, image, new GridSpec(size, size));
    }
}
