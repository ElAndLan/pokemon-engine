using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.Assets;

/// <summary>17B audio import + sound/animation editors + asset diagnostics, headless.</summary>
public sealed class SoundAndAnimTests : IDisposable
{
    private readonly string _src = Path.Combine(Path.GetTempPath(), "cgm-snd-src-" + Guid.NewGuid().ToString("N"));
    private readonly string _project = TestRepo.CopySampleToTemp("fixture-min");
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _vm;

    public SoundAndAnimTests()
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

    private string Wav(string name)
    {
        string path = Path.Combine(_src, name);
        File.WriteAllBytes(path, TinyWav.Pcm16Mono());
        return path;
    }

    // --- Audio import ---

    [Fact]
    public void ImportSound_CopiesHashesAndOpensEditor()
    {
        Assert.True(_vm.ImportSound(Wav("theme.wav"), "theme"));

        Sound sound = _vm.Session!.Find<Sound>(EntityId.Parse("sound:theme"))!;
        Assert.Equal("assets/audio/theme.wav", sound.Asset);
        Assert.Equal(64, sound.ContentHash!.Length);
        Assert.False(File.Exists(Path.Combine(_project, sound.Asset)));
        _vm.SaveAll();
        Assert.True(File.Exists(Path.Combine(_project, sound.Asset)));
        Assert.IsType<SoundDocument>(_vm.ActiveDocument);
    }

    [Fact]
    public void ImportSound_RejectsNonWave_BeforeAnyCopy()
    {
        string bad = Path.Combine(_src, "bad.wav");
        File.WriteAllBytes(bad, [1, 2, 3, 4, 5]);

        Assert.False(_vm.ImportSound(bad, "bad"));
        Assert.Contains("RIFF/WAVE", _vm.StatusText);
        Assert.False(_vm.Session!.Contains(EntityId.Parse("sound:bad")));
        Assert.False(File.Exists(Path.Combine(_project, "assets", "audio", "bad.wav")));
    }

    [Fact]
    public void ImportSound_TruncatedRiff_Rejects()
    {
        byte[] bytes = TinyWav.Pcm16Mono();
        string cut = Path.Combine(_src, "cut.wav");
        File.WriteAllBytes(cut, bytes[..^4]); // RIFF length no longer matches

        Assert.False(_vm.ImportSound(cut, "cut"));
        Assert.Contains("truncated", _vm.StatusText);
    }

    // --- Sound editor ---

    [Fact]
    public void SoundDocument_EditsAreUndoable()
    {
        _vm.ImportSound(Wav("bgm.wav"), "bgm");
        var doc = (SoundDocument)_vm.ActiveDocument!;

        doc.Kind = SoundKind.Music;
        doc.Loop = true;
        doc.Volume = 60;
        Sound edited = _vm.Session!.Find<Sound>(EntityId.Parse("sound:bgm"))!;
        Assert.Equal(SoundKind.Music, edited.Kind);
        Assert.True(edited.Loop);
        Assert.Equal(60, edited.Volume);

        doc.Undo.Undo(); // volume
        doc.Undo.Undo(); // loop
        doc.Undo.Undo(); // kind
        Sound reverted = _vm.Session.Find<Sound>(EntityId.Parse("sound:bgm"))!;
        Assert.Equal(SoundKind.Sfx, reverted.Kind);
        Assert.False(reverted.Loop);
        Assert.Equal(100, reverted.Volume);
    }

    [Fact]
    public void SoundRule_FlagsOutOfRangeVolume()
    {
        _vm.ImportSound(Wav("loud.wav"), "loud");
        var session = _vm.Session!;
        session.Put(session.Find<Sound>(EntityId.Parse("sound:loud"))! with { Volume = 150 });
        _vm.RefreshValidation();

        Assert.Contains(_vm.Issues, i => i.RuleId == "sound" && i.Field == "volume");
    }

    // --- Animation grouping + editor ---

    private SheetDocument ImportSheet(string slug, int w = 48, int h = 16)
    {
        string png = Path.Combine(_src, slug + ".png");
        File.WriteAllBytes(png, TinyPng.Solid(w, h));
        _vm.ImportSheet(png, slug); // 16px tiles → w/16 cells
        _vm.OpenDocument(EntityId.Parse("sheet:" + slug));
        return (SheetDocument)_vm.ActiveDocument!;
    }

    [Fact]
    public async Task CreateAnimation_FromSelectedCells_OpensEditor()
    {
        SheetDocument sheet = ImportSheet("strip");
        _dialogs.TextToReturn = "blink";

        Assert.True(await _vm.CreateAnimationAsync(sheet.Cells.Select(c => c.SpriteId).ToList()));

        Animation anim = _vm.Session!.Find<Animation>(EntityId.Parse("anim:blink"))!;
        Assert.Equal(3, anim.Frames.Count);
        Assert.All(anim.Frames, f => Assert.Equal(150, f.Ms));
        Assert.IsType<AnimDocument>(_vm.ActiveDocument);
    }

    [Fact]
    public void CreateWalkClips_From12CellSheet_MakesFourClips()
    {
        ImportSheet("hero", w: 48, h: 64); // 3×4 grid of 16px cells = 12

        Assert.True(_vm.CreateWalkClips(EntityId.Parse("sheet:hero")));
        Assert.Equal(4, _vm.Session!.All<Animation>().Count());
        Assert.NotNull(_vm.Session.Find<Animation>(EntityId.Parse("anim:hero_walk_down")));

        Assert.False(_vm.CreateWalkClips(EntityId.Parse("sheet:hero"))); // already exist
    }

    [Fact]
    public void CreateWalkClips_WrongCellCount_Refuses()
    {
        ImportSheet("short", w: 48, h: 16); // 3 cells
        Assert.False(_vm.CreateWalkClips(EntityId.Parse("sheet:short")));
        Assert.Contains("12 cells", _vm.StatusText);
    }

    [Fact]
    public async Task AnimDocument_ReorderRemoveRetime_AllUndoable()
    {
        SheetDocument sheet = ImportSheet("frames");
        _dialogs.TextToReturn = "cycle";
        await _vm.CreateAnimationAsync(sheet.Cells.Select(c => c.SpriteId).ToList());
        var doc = (AnimDocument)_vm.ActiveDocument!;
        EntityId first = doc.Frames[0].Sprite;

        doc.MoveFrame(0, +1);
        Assert.Equal(first, doc.Frames[1].Sprite);

        doc.SetFrameMs(0, 250);
        Assert.Equal(250, doc.Frames[0].Ms);

        doc.RemoveFrame(2);
        Assert.Equal(2, doc.Frames.Count);

        doc.Undo.Undo();
        doc.Undo.Undo();
        doc.Undo.Undo();
        Assert.Equal(3, doc.Frames.Count);
        Assert.Equal(first, doc.Frames[0].Sprite);
        Assert.Equal(150, doc.Frames[0].Ms);
    }

    // --- Canvas rect move ---

    [Fact]
    public void SetCellRect_MovesCell_OneUndoStep_BoundsClamped()
    {
        SheetDocument sheet = ImportSheet("cells");
        EntityId target = sheet.Cells[0].SpriteId;

        sheet.SetCellRect(target, new Rect(4, 0, 16, 16));
        SheetCellView moved = sheet.CellViews.Single(c => c.SpriteId == target);
        Assert.Equal(new Rect(4, 0, 16, 16), moved.Rect);

        sheet.SetCellRect(target, new Rect(40, 0, 16, 16)); // 40+16 > 48 → ignored
        Assert.Equal(new Rect(4, 0, 16, 16), sheet.CellViews.Single(c => c.SpriteId == target).Rect);

        sheet.Undo.Undo();
        Assert.Equal(new Rect(0, 0, 16, 16), sheet.CellViews.Single(c => c.SpriteId == target).Rect);
    }

    // --- Asset diagnostics ---

    [Fact]
    public void MissingAssetFile_IsAnError()
    {
        ImportSheet("gone");
        _vm.SaveAll();
        File.Delete(Path.Combine(_project, "assets", "gone.png"));
        _vm.RefreshValidation();

        Assert.Contains(_vm.Issues, i => i.RuleId == "asset-file"
            && i.EntityId == EntityId.Parse("sheet:gone") && i.Message.Contains("missing"));
    }

    [Fact]
    public void ExternallyEditedAsset_IsAHashMismatchError()
    {
        ImportSheet("edited");
        _vm.SaveAll();
        File.WriteAllBytes(Path.Combine(_project, "assets", "edited.png"), TinyPng.Solid(8, 8));
        _vm.RefreshValidation();

        Assert.Contains(_vm.Issues, i => i.RuleId == "asset-file" && i.Message.Contains("hash mismatch"));
    }

    [Fact]
    public void UnreferencedAssetFile_IsAnOrphanWarning()
    {
        Directory.CreateDirectory(Path.Combine(_project, "assets"));
        File.WriteAllBytes(Path.Combine(_project, "assets", "stray.png"), TinyPng.Solid(8, 8));
        _vm.RefreshValidation();

        Assert.Contains(_vm.Issues, i => i.RuleId == "asset-orphan" && i.Message.Contains("stray.png"));
    }

    [Fact]
    public void HealthyImports_ProduceNoAssetIssues()
    {
        ImportSheet("clean");
        _vm.ImportSound(Wav("clean.wav"), "clean");
        _vm.RefreshValidation();

        Assert.DoesNotContain(_vm.Issues, i => i.RuleId is "asset-file" or "asset-orphan");
    }
}
