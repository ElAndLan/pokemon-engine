using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.ViewModels;

/// <summary>ASSET_PIPELINE_SPEC 17B slicer editor, headless: grid re-slice, suggestion layers,
/// manual rects, exclusion, batch naming — all undoable whole-record edits.</summary>
public sealed class SheetDocumentTests : IDisposable
{
    private readonly string _project = TestRepo.CopySampleToTemp("fixture-min");
    private readonly ProjectSession _session;

    public SheetDocumentTests() => _session = ProjectSession.Open(_project);

    public void Dispose()
    {
        _session.Close();
        Directory.Delete(_project, recursive: true);
    }

    /// <summary>A 32×32 sheet imported at cell 16 (2×2 grid, all opaque).</summary>
    private SheetDocument Doc(int w = 32, int h = 32)
    {
        string png = Path.Combine(_project, "assets", "doc.png");
        Directory.CreateDirectory(Path.GetDirectoryName(png)!);
        File.WriteAllBytes(png, Assets.TinyPng.Solid(w, h));
        SpriteSheet sheet = Cgm.Creator.Assets.SheetImporter.Import(
            EntityId.Parse("sheet:doc"), png, "assets/doc.png", tileSize: 16);
        _session.Add(sheet);
        return new SheetDocument(_session, sheet);
    }

    [Fact]
    public void GridParamChange_Reslices_AsOneUndoStep()
    {
        SheetDocument doc = Doc();
        Assert.Equal(4, doc.Cells.Count);

        doc.CellW = 32; // 1 column × 2 rows
        Assert.Equal(2, doc.Cells.Count);
        Assert.True(doc.IsDirty);

        doc.Undo.Undo();
        Assert.Equal(4, doc.Cells.Count);
        Assert.Equal(16, doc.CellW);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void InvalidGridValues_AreIgnoredNotApplied()
    {
        SheetDocument doc = Doc();
        doc.CellW = 0;
        doc.OffsetX = -3;
        Assert.Equal(16, doc.CellW);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void ComponentSuggestion_ReplacesCellsWithDetectedRects()
    {
        SheetDocument doc = Doc(); // solid image → one component
        doc.ApplyComponentSuggestion();

        Assert.Equal(SliceMode.Rects, doc.Mode);
        SheetCellView cell = Assert.Single(doc.CellViews);
        Assert.Equal(new Rect(0, 0, 32, 32), cell.Rect);
        Assert.Equal("doc_0", cell.SpriteId.Slug);

        doc.Undo.Undo();
        Assert.Equal(SliceMode.Grid, doc.Mode);
        Assert.Equal(4, doc.Cells.Count);
    }

    [Fact]
    public void AddRect_SwitchesToRectsMode_ClampsToBounds()
    {
        SheetDocument doc = Doc();
        int before = doc.Cells.Count;

        doc.AddRect(new Rect(4, 4, 8, 8));
        Assert.Equal(SliceMode.Rects, doc.Mode);
        Assert.Equal(before + 1, doc.Cells.Count);

        doc.AddRect(new Rect(30, 30, 8, 8)); // out of bounds → ignored
        Assert.Equal(before + 1, doc.Cells.Count);
    }

    [Fact]
    public void RemoveCell_Excludes_UndoRestores()
    {
        SheetDocument doc = Doc();
        EntityId victim = doc.Cells[0].SpriteId;

        doc.RemoveCell(victim);
        Assert.DoesNotContain(doc.Cells, c => c.SpriteId == victim);

        doc.Undo.Undo();
        Assert.Contains(doc.Cells, c => c.SpriteId == victim);
    }

    [Fact]
    public void RenameCells_PatternWithN_NamesInOrder_OneUndoStep()
    {
        SheetDocument doc = Doc();
        Assert.True(doc.RenameCells("coin_{n}"));

        Assert.Equal(["doc_coin_0", "doc_coin_1", "doc_coin_2", "doc_coin_3"],
            doc.Cells.Select(c => c.SpriteId.Slug).ToArray());

        doc.Undo.Undo();
        Assert.Equal("doc_0", doc.Cells[0].SpriteId.Slug);
    }

    [Fact]
    public void RenameCells_WithoutN_AppendsIndex_InvalidPatternRefused()
    {
        SheetDocument doc = Doc();
        Assert.True(doc.RenameCells("gem"));
        Assert.Equal("doc_gem_0", doc.Cells[0].SpriteId.Slug);

        Assert.False(doc.RenameCells("Bad Name!"));
        Assert.Equal("doc_gem_0", doc.Cells[0].SpriteId.Slug); // refused, unchanged
    }

    [Fact]
    public void SetCellClass_ChangesOneCell()
    {
        SheetDocument doc = Doc();
        EntityId target = doc.Cells[2].SpriteId;

        doc.SetCellClass(target, SpriteClass.Character);
        Assert.Equal(SpriteClass.Character, doc.Cells.Single(c => c.SpriteId == target).Class);
        Assert.All(doc.Cells.Where(c => c.SpriteId != target), c => Assert.Equal(SpriteClass.Tile, c.Class));
    }

    [Fact]
    public void MissingAsset_DegradesToDimensionOnlyEditing()
    {
        SpriteSheet orphan = new()
        {
            Id = EntityId.Parse("sheet:ghost"),
            Name = "ghost",
            Asset = "assets/ghost.png", // no such file
            ImageW = 32,
            ImageH = 32,
            Mode = SliceMode.Grid,
            CellW = 16,
            CellH = 16,
        };
        _session.Add(orphan);

        var doc = new SheetDocument(_session, orphan);
        Assert.False(doc.HasPixels);

        doc.CellW = 32; // grid math still works from recorded dimensions
        Assert.NotEmpty(doc.Cells);
    }
}
