namespace Cgm.Core.Model;

/// <summary>
/// Resolves a <c>sprite:*</c> id to the sheet and pixel rectangle that define it
/// (DATA_SCHEMA.md §4.6-4.7). Sprites are projections of sheet cells, so this indexing *is* the
/// definition of a sprite — it is a rule and lives in Core. The renderer looks a sprite up here and
/// never recomputes cell geometry itself.
/// </summary>
public sealed class SpriteResolver
{
    private readonly Dictionary<EntityId, (SpriteSheet Sheet, Rect Rect)> _sprites = [];

    public SpriteResolver(IEnumerable<SpriteSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        foreach (SpriteSheet sheet in sheets)
            foreach (SheetCell cell in sheet.Cells)
                if (TryRect(sheet, cell, out Rect rect))
                    _sprites.TryAdd(cell.SpriteId, (sheet, rect));
    }

    public bool Contains(EntityId sprite) => _sprites.ContainsKey(sprite);

    public bool TryResolve(EntityId sprite, out SpriteSheet sheet, out Rect rect)
    {
        if (_sprites.TryGetValue(sprite, out (SpriteSheet Sheet, Rect Rect) found))
        {
            (sheet, rect) = found;
            return true;
        }
        sheet = null!;
        rect = default;
        return false;
    }

    /// <summary>Columns that fit across the sheet. The trailing cell needs no spacing after it, so
    /// the spacing is added to the width before dividing rather than subtracted per column.</summary>
    public static int Columns(SpriteSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        int stride = sheet.CellW + sheet.SpacingX;
        return stride <= 0 || sheet.ImageW <= sheet.OffsetX
            ? 0
            : (sheet.ImageW - sheet.OffsetX + sheet.SpacingX) / stride;
    }

    /// <summary>The rectangle a cell occupies, or false when the cell is unresolvable (a grid cell on
    /// a sheet with no recorded image size, or a rects cell with no rect).</summary>
    public static bool TryRect(SpriteSheet sheet, SheetCell cell, out Rect rect)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(cell);

        if (sheet.Mode == SliceMode.Rects)
        {
            rect = cell.Rect ?? default;
            return cell.Rect is not null;
        }

        int columns = Columns(sheet);
        if (columns <= 0 || cell.Index is not int index || index < 0)
        {
            rect = default;
            return false;
        }

        rect = new Rect(
            sheet.OffsetX + index % columns * (sheet.CellW + sheet.SpacingX),
            sheet.OffsetY + index / columns * (sheet.CellH + sheet.SpacingY),
            sheet.CellW,
            sheet.CellH);
        return true;
    }

    /// <summary>True when the rectangle lies wholly inside the sheet's recorded image.</summary>
    public static bool InBounds(SpriteSheet sheet, Rect rect)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return rect.W > 0 && rect.H > 0 && rect.X >= 0 && rect.Y >= 0
            && rect.X + rect.W <= sheet.ImageW && rect.Y + rect.H <= sheet.ImageH;
    }
}
