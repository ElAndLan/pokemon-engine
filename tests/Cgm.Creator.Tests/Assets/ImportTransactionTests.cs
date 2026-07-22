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
    public async Task Reimport_Declined_LeavesFileAndEntityUntouched()
    {
        _vm.ImportSheet(Png("keep.png", 32, 32), "keep");
        var id = EntityId.Parse("sheet:keep");
        SpriteSheet before = _vm.Session!.Find<SpriteSheet>(id)!;
        byte[] fileBefore = File.ReadAllBytes(Path.Combine(_project, before.Asset));

        _dialogs.ConfirmToReturn = false;
        Assert.False(await _vm.ReimportSheetAsync(id, Png("other.png", 16, 16)));

        Assert.Equal(before, _vm.Session.Find<SpriteSheet>(id));
        Assert.Equal(fileBefore, File.ReadAllBytes(Path.Combine(_project, before.Asset)));
    }
}
