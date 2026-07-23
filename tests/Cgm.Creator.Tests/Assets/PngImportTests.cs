using Cgm.Core.Model;
using Cgm.Creator.Assets;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.Assets;

public sealed class PngImportTests
{
    private static string WriteSolidPng(string dir, int w, int h)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "sheet.png");
        File.WriteAllBytes(path, TinyPng.Solid(w, h));
        return path;
    }

    [Fact]
    public void PngDecoder_ReadsDimensionsAndAlpha()
    {
        // 2×1: pixel 0 opaque (a=255), pixel 1 transparent (a=0).
        byte[] rgba = [255, 255, 255, 255, 0, 0, 0, 0];
        ImageData img = PngDecoder.Decode(TinyPng.Encode(2, 1, rgba));
        Assert.Equal(2, img.Width);
        Assert.Equal(1, img.Height);
        Assert.True(img.IsOpaque(0, 0));
        Assert.False(img.IsOpaque(1, 0));
    }

    [Fact]
    public void SheetImporter_SlicesBySuggestedSize()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cgm-png-" + Guid.NewGuid().ToString("N"));
        try
        {
            string png = WriteSolidPng(dir, 32, 32);
            SpriteSheet sheet = SheetImporter.Import(EntityId.Parse("sheet:s"), png, "assets/sheet.png", tileSize: 16);
            Assert.Equal("assets/sheet.png", sheet.Asset);
            Assert.Equal(16, sheet.CellW);
            Assert.Equal(4, sheet.Cells.Count); // 32/16 = 2×2, all opaque
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ImportSheet_CopiesAssetAndAddsEntity()
    {
        string src = Path.Combine(Path.GetTempPath(), "cgm-src-" + Guid.NewGuid().ToString("N"));
        string projectDir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            string png = WriteSolidPng(src, 32, 32);
            var vm = TestRepo.NewVm();
            vm.OpenProject(projectDir);

            vm.ImportSheet(png, "overworld");

            SpriteSheet sheet = vm.Session!.Find<SpriteSheet>(EntityId.Parse("sheet:overworld"))!;
            Assert.NotNull(sheet);
            Assert.Equal(4, sheet.Cells.Count);
            Assert.False(File.Exists(Path.Combine(projectDir, "assets", "sheet.png")));
            vm.SaveAll();
            Assert.True(File.Exists(Path.Combine(projectDir, "assets", "sheet.png")));
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void ImportSheet_RejectsBadSlugAndDuplicate()
    {
        string src = Path.Combine(Path.GetTempPath(), "cgm-src2-" + Guid.NewGuid().ToString("N"));
        string projectDir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            string png = WriteSolidPng(src, 16, 16);
            var vm = TestRepo.NewVm();
            vm.OpenProject(projectDir);

            vm.ImportSheet(png, "Bad Slug");
            Assert.Contains("Invalid slug", vm.StatusText);

            vm.ImportSheet(png, "dup");
            vm.ImportSheet(png, "dup");
            Assert.Contains("already exists", vm.StatusText);
        }
        finally
        {
            Directory.Delete(src, true);
            Directory.Delete(projectDir, true);
        }
    }
}
