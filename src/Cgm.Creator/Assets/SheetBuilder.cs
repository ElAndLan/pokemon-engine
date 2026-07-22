using Cgm.Core.Model;

namespace Cgm.Creator.Assets;

/// <summary>Decoded image: dimensions + per-pixel opacity (row-major). Produced by PNG decode
/// (next chunk); kept minimal so slicing/import logic is testable without a real image.</summary>
public sealed record ImageData(int Width, int Height, bool[] Opaque)
{
    public bool IsOpaque(int x, int y) => Opaque[y * Width + x];
}

/// <summary>Assembles a <see cref="SpriteSheet"/> from an image + a grid (ASSET_PIPELINE_SPEC v0):
/// slices, drops fully-transparent cells, and names sprites <c>sprite:&lt;sheet&gt;_&lt;gridIndex&gt;</c>.</summary>
public static class SheetBuilder
{
    public static SpriteSheet Build(EntityId sheetId, string assetPath, ImageData image, GridSpec grid,
        bool excludeTransparent = true)
    {
        IReadOnlyList<Rect> rects = GridSlicer.Slice(image.Width, image.Height, grid);

        var cells = new List<SheetCell>();
        for (int index = 0; index < rects.Count; index++)
        {
            Rect r = rects[index];
            if (excludeTransparent && IsFullyTransparent(image, r))
                continue; // grid index preserved via `index`, so ids stay stable

            cells.Add(new SheetCell
            {
                Index = index,
                Rect = r,
                SpriteId = new EntityId(EntityCategory.Sprite, $"{sheetId.Slug}_{index}"),
                Class = SpriteClass.Tile,
            });
        }

        return new SpriteSheet
        {
            Id = sheetId,
            Name = sheetId.Slug,
            Asset = assetPath,
            ImageW = image.Width,
            ImageH = image.Height,
            Mode = SliceMode.Grid,
            CellW = grid.CellW,
            CellH = grid.CellH,
            OffsetX = grid.OffsetX,
            OffsetY = grid.OffsetY,
            SpacingX = grid.SpacingX,
            SpacingY = grid.SpacingY,
            Cells = cells,
        };
    }

    private static bool IsFullyTransparent(ImageData image, Rect r)
    {
        for (int y = r.Y; y < r.Y + r.H; y++)
            for (int x = r.X; x < r.X + r.W; x++)
                if (image.IsOpaque(x, y))
                    return false;
        return true;
    }
}
