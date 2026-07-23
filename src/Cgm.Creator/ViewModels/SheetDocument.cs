using Cgm.Core.Model;
using Cgm.Creator.Assets;

namespace Cgm.Creator.ViewModels;

/// <summary>A cell with its resolved pixel rect, as the canvas and cell list consume it.</summary>
public sealed record SheetCellView(EntityId SpriteId, Rect Rect, SpriteClass Class)
{
    public string Size => $"{Rect.W}×{Rect.H}";
}

/// <summary>
/// The slicer editor (ASSET_PIPELINE_SPEC 17B): one sprite sheet's grid parameters, suggestion
/// layers, manual rects, cell naming, and classes — every change an undoable whole-record edit.
/// Pixels are loaded once for transparency exclusion and component detection; a missing or
/// unreadable image degrades to grid math over the recorded dimensions (the editor stays usable,
/// validation reports the missing asset).
/// </summary>
public sealed class SheetDocument : EntityEditorDocument<SpriteSheet>
{
    private readonly ImageData _image;

    /// <summary>True when the sheet's PNG decoded; false = dimension-only fallback.</summary>
    public bool HasPixels { get; }

    public SheetDocument(ProjectSession session, SpriteSheet model) : base(session, model)
    {
        ImageData? decoded = null;
        try
        {
            decoded = PngDecoder.DecodeFile(Path.Combine(session.Folder, model.Asset));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Missing/corrupt asset: validation owns the report; the editor degrades gracefully.
        }
        HasPixels = decoded is not null;
        _image = decoded ?? new ImageData(Math.Max(1, model.ImageW), Math.Max(1, model.ImageH),
            Enumerable.Repeat(true, Math.Max(1, model.ImageW) * Math.Max(1, model.ImageH)).ToArray());
    }

    public IReadOnlyList<SheetCell> Cells => Model.Cells;

    /// <summary>Cells with their resolved pixel rects, for the canvas overlay and cell list.</summary>
    public IReadOnlyList<SheetCellView> CellViews => Model.Cells
        .Select(c => SheetBuilder.ResolveRect(Model, c) is { } r ? new SheetCellView(c.SpriteId, r, c.Class) : null)
        .Where(v => v is not null)
        .Select(v => v!)
        .ToList();

    public bool HasGutterSuggestion => GutterSuggestion is not null;
    public bool HasSizeSuggestion => SizeSuggestion is not null;

    public SliceMode Mode => Model.Mode;
    public string AssetPath => Model.Asset;
    public int ImageW => Model.ImageW;
    public int ImageH => Model.ImageH;

    // --- Grid parameters: each change re-slices the grid as one undo step. Grid re-slice
    // regenerates cells (index naming); authored rect-mode work is untouched by these.

    public int CellW { get => Model.CellW; set => Reslice(value, Model.CellH, Model.OffsetX, Model.OffsetY, Model.SpacingX, Model.SpacingY); }
    public int CellH { get => Model.CellH; set => Reslice(Model.CellW, value, Model.OffsetX, Model.OffsetY, Model.SpacingX, Model.SpacingY); }
    public int OffsetX { get => Model.OffsetX; set => Reslice(Model.CellW, Model.CellH, value, Model.OffsetY, Model.SpacingX, Model.SpacingY); }
    public int OffsetY { get => Model.OffsetY; set => Reslice(Model.CellW, Model.CellH, Model.OffsetX, value, Model.SpacingX, Model.SpacingY); }
    public int SpacingX { get => Model.SpacingX; set => Reslice(Model.CellW, Model.CellH, Model.OffsetX, Model.OffsetY, value, Model.SpacingY); }
    public int SpacingY { get => Model.SpacingY; set => Reslice(Model.CellW, Model.CellH, Model.OffsetX, Model.OffsetY, Model.SpacingX, value); }

    private void Reslice(int cellW, int cellH, int offX, int offY, int spX, int spY)
    {
        if (cellW <= 0 || cellH <= 0 || offX < 0 || offY < 0 || spX < 0 || spY < 0)
            return; // invalid mid-typing values are ignored, not errors
        var grid = new GridSpec(cellW, cellH, offX, offY, spX, spY);
        if (grid == new GridSpec(Model.CellW, Model.CellH, Model.OffsetX, Model.OffsetY, Model.SpacingX, Model.SpacingY)
            && Model.Mode == SliceMode.Grid)
            return;
        Edit(SheetBuilder.Build(Model.Id, Model.Asset, _image, grid) with
        {
            ContentHash = Model.ContentHash,
            Name = Model.Name,
        });
    }

    // --- Suggestion layers (each acceptance = one undo step) ---

    /// <summary>v2 gutter fit, when the pixels show a uniform guttered grid.</summary>
    public GutterFit? GutterSuggestion =>
        HasPixels ? GutterDetector.Detect(_image.Opaque, _image.Width, _image.Height) : null;

    /// <summary>v1 common size that divides both axes, preferring the project tile size.</summary>
    public int? SizeSuggestion => SizeSuggester.Suggest(_image.Width, _image.Height, Session.Settings.TileSize);

    public void ApplyGutterSuggestion()
    {
        if (GutterSuggestion is { } fit)
            Reslice(fit.CellW, fit.CellH, fit.MarginX, fit.MarginY, fit.SpacingX, fit.SpacingY);
    }

    public void ApplySizeSuggestion()
    {
        if (SizeSuggestion is { } size)
            Reslice(size, size, 0, 0, 0, 0);
    }

    /// <summary>v3 connected components: replaces all cells with detected sprite bounds
    /// (Mode=Rects), named <c>&lt;sheet&gt;_&lt;n&gt;</c> in reading order.</summary>
    public void ApplyComponentSuggestion(int mergeThreshold = 2)
    {
        if (!HasPixels)
            return;
        IReadOnlyList<Rect> rects = ComponentSlicer.Detect(_image.Opaque, _image.Width, _image.Height, mergeThreshold);
        Edit(Model with
        {
            Mode = SliceMode.Rects,
            Cells = rects.Select((r, n) => new SheetCell
            {
                Rect = r,
                SpriteId = new EntityId(EntityCategory.Sprite, $"{Model.Id.Slug}_{n}"),
                Class = SpriteClass.Object,
            }).ToList(),
        });
    }

    // --- Manual rects & cell operations ---

    /// <summary>Adds a manual rect (clamped to image bounds); switches the sheet to Rects mode.</summary>
    public void AddRect(Rect rect)
    {
        if (rect.W <= 0 || rect.H <= 0 || rect.X < 0 || rect.Y < 0
            || rect.X + rect.W > _image.Width || rect.Y + rect.H > _image.Height)
            return;
        int n = Model.Cells.Count;
        while (Model.Cells.Any(c => c.SpriteId.Slug == $"{Model.Id.Slug}_{n}"))
            n++;
        Edit(Model with
        {
            Mode = SliceMode.Rects,
            Cells = Model.Cells.Append(new SheetCell
            {
                Rect = rect,
                SpriteId = new EntityId(EntityCategory.Sprite, $"{Model.Id.Slug}_{n}"),
                Class = SpriteClass.Object,
            }).ToList(),
        });
    }

    /// <summary>Excludes a cell: removed from the sheet, restored by undo (17B exclusion model —
    /// a removed cell projects no sprite; grid re-slice regenerates grid cells).</summary>
    public void RemoveCell(EntityId spriteId) =>
        EditCells(Model.Cells.Where(c => c.SpriteId != spriteId).ToList());

    /// <summary>Moves/resizes one cell's rect (a canvas drag = one undo step). The cell becomes an
    /// authored rect even if it was a grid cell, and the whole rect must stay in bounds.</summary>
    public void SetCellRect(EntityId spriteId, Rect rect)
    {
        if (rect.W <= 0 || rect.H <= 0 || rect.X < 0 || rect.Y < 0
            || rect.X + rect.W > _image.Width || rect.Y + rect.H > _image.Height)
            return;
        EditCells(Model.Cells
            .Select(c => c.SpriteId == spriteId ? c with { Index = null, Rect = rect } : c)
            .ToList());
    }

    public void SetCellClass(EntityId spriteId, SpriteClass @class) =>
        EditCells(Model.Cells.Select(c => c.SpriteId == spriteId ? c with { Class = @class } : c).ToList());

    /// <summary>Batch naming (17B): a pattern with <c>{n}</c> names cells in order; without it,
    /// <c>_{n}</c> is appended. Sprite ids become <c>sprite:&lt;sheet&gt;_&lt;name&gt;</c>; the
    /// whole rename is one undo step. An invalid pattern is refused.</summary>
    public bool RenameCells(string pattern)
    {
        string template = pattern.Contains("{n}") ? pattern : pattern + "_{n}";
        var cells = new List<SheetCell>();
        for (int n = 0; n < Model.Cells.Count; n++)
        {
            string name = $"{Model.Id.Slug}_{template.Replace("{n}", n.ToString())}";
            if (!EntityId.IsValidSlug(name))
                return false;
            cells.Add(Model.Cells[n] with { SpriteId = new EntityId(EntityCategory.Sprite, name) });
        }
        EditCells(cells);
        return true;
    }

    private void EditCells(IReadOnlyList<SheetCell> cells)
    {
        if (!cells.SequenceEqual(Model.Cells))
            Edit(Model with { Cells = cells });
    }
}
