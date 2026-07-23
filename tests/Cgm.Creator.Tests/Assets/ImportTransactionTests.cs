using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.Assets;

/// <summary>ASSET_PIPELINE_SPEC 17B: validate-first import (a malformed file changes nothing),
/// collision-free asset names, content hashing, and identity-preserving reimport.</summary>
public sealed class ImportTransactionTests : IDisposable
{
    private readonly string _src = Path.Combine(Path.GetTempPath(), "cgm-imp-src-" + Guid.NewGuid().ToString("N"));
    private readonly string _project = TestRepo.CopySampleToTemp("fixture-min");
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _vm;

    public ImportTransactionTests()
    {
        Directory.CreateDirectory(_src);
        _vm = TestRepo.NewVm(_dialogs);
        _vm.OpenProject(_project);
    }

    public void Dispose()
    {
        Directory.Delete(_src, recursive: true);
        Directory.Delete(_project, recursive: true);
    }

    private string Png(string name, int w, int h)
    {
        string path = Path.Combine(_src, name);
        File.WriteAllBytes(path, TinyPng.Solid(w, h));
        return path;
    }

    [Fact]
    public void MalformedPng_RejectsBeforeAnythingIsCopied()
    {
        string bad = Path.Combine(_src, "bad.png");
        File.WriteAllBytes(bad, [1, 2, 3, 4]);

        Assert.False(_vm.ImportSheet(bad, "bad"));
        Assert.Contains("not a readable PNG", _vm.StatusText);
        Assert.False(_vm.Session!.Contains(EntityId.Parse("sheet:bad")));
        Assert.False(File.Exists(Path.Combine(_project, "assets", "bad.png"))); // nothing copied
    }

    [Fact]
    public void Import_RecordsHashAndDimensions()
    {
        Assert.True(_vm.ImportSheet(Png("hero.png", 32, 32), "hero"));

        SpriteSheet sheet = _vm.Session!.Find<SpriteSheet>(EntityId.Parse("sheet:hero"))!;
        Assert.Equal(32, sheet.ImageW);
        Assert.Equal(32, sheet.ImageH);
        Assert.NotNull(sheet.ContentHash);
        Assert.Equal(64, sheet.ContentHash!.Length); // SHA-256 hex
    }

    [Fact]
    public void SameFileName_GetsAUniqueAssetPerSheet()
    {
        Assert.True(_vm.ImportSheet(Png("art.png", 16, 16), "first"));
        Assert.True(_vm.ImportSheet(Png("art.png", 32, 32), "second"));

        SpriteSheet first = _vm.Session!.Find<SpriteSheet>(EntityId.Parse("sheet:first"))!;
        SpriteSheet second = _vm.Session.Find<SpriteSheet>(EntityId.Parse("sheet:second"))!;
        Assert.NotEqual(first.Asset, second.Asset); // never a silent overwrite
        Assert.False(File.Exists(Path.Combine(_project, first.Asset))); // staged until explicit Save
        _vm.SaveAll();
        Assert.True(File.Exists(Path.Combine(_project, first.Asset)));
        Assert.True(File.Exists(Path.Combine(_project, second.Asset)));
        Assert.Equal(16, first.ImageW); // first sheet's pixels untouched by the second import
    }

    [Fact]
    public async Task Reimport_KeepsIdAndCells_UpdatesHashAndDims()
    {
        _vm.ImportSheet(Png("world.png", 32, 32), "world");
        SpriteSheet before = _vm.Session!.Find<SpriteSheet>(EntityId.Parse("sheet:world"))!;

        _dialogs.ConfirmToReturn = true;
        Assert.True(await _vm.ReimportSheetAsync(before.Id, Png("world2.png", 32, 32)));

        SpriteSheet after = _vm.Session.Find<SpriteSheet>(EntityId.Parse("sheet:world"))!;
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Asset, after.Asset); // same file path, new pixels
        Assert.Equal(before.Cells.Count, after.Cells.Count); // all cells still fit
    }

    [Fact]
    public async Task Reimport_SmallerImage_RemovesOnlyInvalidatedCells()
    {
        _vm.ImportSheet(Png("big.png", 32, 32), "big"); // 2×2 cells of 16
        var id = EntityId.Parse("sheet:big");
        int cellsBefore = _vm.Session!.Find<SpriteSheet>(id)!.Cells.Count;
        Assert.Equal(4, cellsBefore);

        _dialogs.ConfirmToReturn = true;
        Assert.True(await _vm.ReimportSheetAsync(id, Png("small.png", 32, 16)));

        SpriteSheet after = _vm.Session.Find<SpriteSheet>(id)!;
        Assert.Equal(16, after.ImageH);
        Assert.Equal(2, after.Cells.Count); // bottom row invalidated, top row kept
        Assert.All(after.Cells, c => Assert.True(c.Index is 0 or 1));
    }

    [Fact]
    public async Task Reimport_LargerGrid_ExtendsAcrossEntireDecodedImage()
    {
        _vm.ImportSheet(Png("small_grid.png", 32, 32), "growing");
        var id = EntityId.Parse("sheet:growing");
        SpriteSheet before = _vm.Session!.Find<SpriteSheet>(id)!;
        EntityId first = before.Cells[0].SpriteId;

        _dialogs.ConfirmToReturn = true;
        Assert.True(await _vm.ReimportSheetAsync(id, Png("large_grid.png", 64, 32)));

        SpriteSheet after = _vm.Session.Find<SpriteSheet>(id)!;
        Assert.Equal(8, after.Cells.Count);
        Assert.Equal(64, after.Cells.Max(c => c.Rect!.Value.X + c.Rect.Value.W));
        Assert.Equal(first, after.Cells[0].SpriteId); // authored identity preserved
    }

    [Fact]
    public async Task Reimport_Declined_LeavesFileAndEntityUntouched()
    {
        _vm.ImportSheet(Png("keep.png", 32, 32), "keep");
        _vm.SaveAll();
        var id = EntityId.Parse("sheet:keep");
        SpriteSheet before = _vm.Session!.Find<SpriteSheet>(id)!;
        byte[] fileBefore = File.ReadAllBytes(Path.Combine(_project, before.Asset));

        _dialogs.ConfirmToReturn = false;
        Assert.False(await _vm.ReimportSheetAsync(id, Png("other.png", 16, 16)));

        Assert.Equal(before, _vm.Session.Find<SpriteSheet>(id));
        Assert.Equal(fileBefore, File.ReadAllBytes(Path.Combine(_project, before.Asset)));
    }

    // --- Import straight to tileset ---

    [Fact]
    public void ImportAsTileset_BuildsSheetPlusTileset_OpensTileset()
    {
        Assert.True(_vm.ImportSheetAsTileset(Png("town.png", 48, 32), "town")); // 3×2 = 6 tiles

        var sheet = _vm.Session!.Find<SpriteSheet>(EntityId.Parse("sheet:town"))!;
        var tileset = _vm.Session.Find<Tileset>(EntityId.Parse("tileset:town"))!;
        Assert.Equal(6, sheet.Cells.Count);
        Assert.Equal(6, tileset.Tiles.Count);
        // Every tile references a real sheet sprite.
        var sprites = sheet.Cells.Select(c => c.SpriteId).ToHashSet();
        Assert.All(tileset.Tiles, t => Assert.Contains(t.Sprite!.Value, sprites));
        Assert.IsType<TilesetDocument>(_vm.ActiveDocument);
    }

    [Fact]
    public void ExistingAuthoredSheet_CreatesTilesetWithoutReimport()
    {
        Assert.True(_vm.ImportSheet(Png("authored.png", 48, 32), "authored"));
        var sheetId = EntityId.Parse("sheet:authored");
        SpriteSheet sheet = _vm.Session!.Find<SpriteSheet>(sheetId)!;

        Assert.True(_vm.CreateTilesetFromSheet(sheetId));

        Tileset tileset = _vm.Session.Find<Tileset>(EntityId.Parse("tileset:authored"))!;
        Assert.Equal(sheet.Cells.Select(c => c.SpriteId), tileset.Tiles.Select(t => t.Sprite!.Value));
        Assert.IsType<TilesetDocument>(_vm.ActiveDocument);
    }

    [Theory]
    [InlineData(512, 16, 32)] // really wide → one row of 32 tiles
    [InlineData(16, 512, 32)] // really tall → one column of 32 tiles
    [InlineData(160, 96, 60)] // ordinary grid
    public void ImportAsTileset_DicesAnySheetSize_OnTheTileGrid(int w, int h, int expectedTiles)
    {
        Assert.True(_vm.ImportSheetAsTileset(Png($"s{w}x{h}.png", w, h), $"s{w}_{h}"));
        var tileset = _vm.Session!.Find<Tileset>(EntityId.Parse($"tileset:s{w}_{h}"))!;
        Assert.Equal(expectedTiles, tileset.Tiles.Count); // fixture-min tile size is 16
    }

    [Fact]
    public void ImportAsTileset_RefusesWhenSheetOrTilesetSlugTaken()
    {
        _vm.ImportSheetAsTileset(Png("dup.png", 16, 16), "dup");
        Assert.False(_vm.ImportSheetAsTileset(Png("dup2.png", 16, 16), "dup"));
        Assert.Contains("already exists", _vm.StatusText);
    }

    [Fact]
    public void OffGridSheet_IsFlagged_AtImportAndInValidation()
    {
        _vm.ImportSheet(Png("odd.png", 40, 24), "odd"); // 40,24 not multiples of 16
        Assert.Contains("isn't a multiple", _vm.StatusText);

        _vm.RefreshValidation();
        Assert.Contains(_vm.Issues, i => i.RuleId == "sheet-grid-fit"
            && i.EntityId == EntityId.Parse("sheet:odd"));
    }

    [Fact]
    public void OnGridSheet_ProducesNoGridWarning()
    {
        _vm.ImportSheet(Png("even.png", 64, 32), "even");
        _vm.RefreshValidation();
        Assert.DoesNotContain(_vm.Issues, i => i.RuleId == "sheet-grid-fit");
    }
}
