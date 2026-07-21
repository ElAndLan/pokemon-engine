using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC save durability: temp → flush → replace with a retained backup, and
/// corruption that offers the backup rather than silently replacing state.</summary>
public sealed class SaveRepositoryTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cgm-save").FullName;
    private readonly SaveRepository _repo;

    public SaveRepositoryTests() => _repo = new SaveRepository(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static SaveFile Save(string hash = "abc", int money = 0) => new()
    {
        GameContentHash = hash,
        Map = EntityId.Parse("map:town"),
        Pos = new GridPos(3, 4),
        Facing = Facing.Left,
        Money = money,
        Flags = new Dictionary<string, int> { ["met_rival"] = 1 },
    };

    private void Corrupt(string path) => File.WriteAllText(path, "{ not json");

    // --- Writing ------------------------------------------------------------------

    [Fact]
    public void FirstWrite_CreatesThePrimaryWithNoBackup()
    {
        _repo.Write(Save());
        Assert.True(_repo.Exists);
        Assert.False(_repo.BackupExists);
    }

    [Fact]
    public void SecondWrite_RetainsThePreviousFileAsBackup()
    {
        _repo.Write(Save(money: 100));
        _repo.Write(Save(money: 200));

        Assert.True(_repo.BackupExists);
        Assert.Equal(200, _repo.Load("abc").Save!.Money);
        Assert.Equal(100, _repo.LoadBackup("abc").Save!.Money);   // the previous save, preserved
    }

    [Fact]
    public void RepeatedWrites_KeepOnlyTheImmediatelyPreviousBackup()
    {
        foreach (int money in new[] { 1, 2, 3 })
            _repo.Write(Save(money: money));

        Assert.Equal(3, _repo.Load("abc").Save!.Money);
        Assert.Equal(2, _repo.LoadBackup("abc").Save!.Money);
    }

    /// <summary>The temp file must not survive a successful write.</summary>
    [Fact]
    public void Write_LeavesNoTemporaryFileBehind()
    {
        _repo.Write(Save());
        _repo.Write(Save());
        Assert.False(File.Exists(_repo.Path + SaveRepository.TempExtension));
    }

    [Fact]
    public void Write_CreatesTheFolderIfMissing()
    {
        var nested = new SaveRepository(Path.Combine(_dir, "deep", "saves"));
        nested.Write(Save());
        Assert.True(nested.Exists);
    }

    [Fact]
    public void Write_RejectsNull() => Assert.Throws<ArgumentNullException>(() => _repo.Write(null!));

    [Fact]
    public void Constructor_RejectsABlankFolder()
    {
        Assert.Throws<ArgumentException>(() => new SaveRepository(""));
        Assert.Throws<ArgumentException>(() => new SaveRepository("   "));
        Assert.Throws<ArgumentNullException>(() => new SaveRepository(null!));
    }

    // --- Loading ------------------------------------------------------------------

    [Fact]
    public void RoundTrip_PreservesSessionState()
    {
        _repo.Write(Save());
        SaveFile loaded = _repo.Load("abc").Save!;

        Assert.Equal(EntityId.Parse("map:town"), loaded.Map);
        Assert.Equal(new GridPos(3, 4), loaded.Pos);
        Assert.Equal(Facing.Left, loaded.Facing);
        Assert.Equal(1, loaded.Flags["met_rival"]);
    }

    [Fact]
    public void NoSave_ReportsMissing()
    {
        SaveLoadResult result = _repo.Load("abc");
        Assert.Equal(SaveLoadStatus.Missing, result.Status);
        Assert.False(result.Succeeded);
    }

    /// <summary>A corrupt primary with a backup must offer it, not load it silently: rolling a
    /// player back without telling them is worse than the error.</summary>
    [Fact]
    public void CorruptPrimaryWithABackup_OffersTheBackupWithoutLoadingIt()
    {
        _repo.Write(Save(money: 100));
        _repo.Write(Save(money: 200));
        Corrupt(_repo.Path);

        SaveLoadResult result = _repo.Load("abc");
        Assert.Equal(SaveLoadStatus.CorruptWithBackup, result.Status);
        Assert.Null(result.Save);                       // nothing loaded yet
        Assert.Contains("backup", result.Message!, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(100, _repo.LoadBackup("abc").Save!.Money);   // only when asked
    }

    [Fact]
    public void CorruptPrimaryWithoutABackup_ReportsCorrupt()
    {
        _repo.Write(Save());
        File.Delete(_repo.BackupPath);
        Corrupt(_repo.Path);

        Assert.Equal(SaveLoadStatus.Corrupt, _repo.Load("abc").Status);
    }

    [Fact]
    public void ACorruptBackup_NeverOffersItself()
    {
        _repo.Write(Save());
        _repo.Write(Save());
        Corrupt(_repo.BackupPath);

        Assert.Equal(SaveLoadStatus.Corrupt, _repo.LoadBackup("abc").Status);
    }

    [Fact]
    public void AnEmptyDocument_IsTreatedAsCorrupt()
    {
        _repo.Write(Save());                       // one write, so no backup exists yet
        File.WriteAllText(_repo.Path, "null");
        Assert.Equal(SaveLoadStatus.Corrupt, _repo.Load("abc").Status);
    }

    [Fact]
    public void ASaveWithNoMap_IsTreatedAsCorrupt()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_repo.Path, CgmJson.Serialize(new SaveFile { GameContentHash = "abc" }));
        Assert.Equal(SaveLoadStatus.Corrupt, _repo.Load("abc").Status);
    }

    // --- Versions and content -----------------------------------------------------

    [Fact]
    public void ANewerSaveFormat_IsRefusedRatherThanGuessed()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_repo.Path, CgmJson.Serialize(Save() with
        {
            SaveFormatVersion = SaveRepository.CurrentFormatVersion + 1,
        }));

        SaveLoadResult result = _repo.Load("abc");
        Assert.Equal(SaveLoadStatus.NewerFormat, result.Status);
        Assert.Null(result.Save);
    }

    [Fact]
    public void ASaveFromDifferentContent_IsFlaggedButStillReturned()
    {
        _repo.Write(Save(hash: "old-content"));
        SaveLoadResult result = _repo.Load("new-content");

        Assert.Equal(SaveLoadStatus.ContentMismatch, result.Status);
        Assert.NotNull(result.Save);   // the host decides whether to continue
    }

    [Fact]
    public void AMatchingContentHash_LoadsCleanly() =>
        Assert.Equal(SaveLoadStatus.Ok, WriteThenLoad("abc", "abc").Status);

    /// <summary>Raw project mode has no pack hash, so an empty hash on either side must not block.</summary>
    [Fact]
    public void AnEmptyHashOnEitherSide_SkipsTheContentCheck()
    {
        Assert.Equal(SaveLoadStatus.Ok, WriteThenLoad("", "anything").Status);
        Assert.Equal(SaveLoadStatus.Ok, WriteThenLoad("abc", "").Status);
    }

    private SaveLoadResult WriteThenLoad(string written, string current)
    {
        _repo.Write(Save(hash: written));
        return _repo.Load(current);
    }

    // --- Durability ---------------------------------------------------------------

    /// <summary>A crash mid-write leaves a stray temp file; the previous save must still load.</summary>
    [Fact]
    public void AStrayTempFile_DoesNotAffectLoading()
    {
        _repo.Write(Save(money: 42));
        File.WriteAllText(_repo.Path + SaveRepository.TempExtension, "{ half-written");

        Assert.Equal(42, _repo.Load("abc").Save!.Money);
    }

    [Fact]
    public void WritingOverAStrayTempFile_Succeeds()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_repo.Path + SaveRepository.TempExtension, "{ half-written");

        _repo.Write(Save(money: 7));
        Assert.Equal(7, _repo.Load("abc").Save!.Money);
    }
}

/// <summary>Session state projecting into a save and restoring from one.</summary>
public sealed class WorldSessionSaveTests : IDisposable
{
    private const int Tile = 16;
    private static readonly EntityId MapA = EntityId.Parse("map:town");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public WorldSessionSaveTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private WorldSession Session()
    {
        var map = new Map
        {
            Id = MapA, Name = "Town", Width = 8, Height = 8,
            Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
        };
        var db = new GameDb(new ProjectSettings { Name = "T", TileSize = Tile }, [map]);
        return new WorldSession(db, _ui, Tile, 256, 192);
    }

    [Fact]
    public void ToSave_CapturesMapPositionFacingAndFlags()
    {
        WorldSession session = Session();
        session.Enter(MapA, new GridPos(2, 5), Facing.Right);
        session.Flags.SetInt("badges", 3);

        SaveFile save = session.ToSave("hash");
        Assert.Equal(MapA, save.Map);
        Assert.Equal(new GridPos(2, 5), save.Pos);
        Assert.Equal(Facing.Right, save.Facing);
        Assert.Equal(3, save.Flags["badges"]);
        Assert.Equal("hash", save.GameContentHash);
    }

    [Fact]
    public void Restore_PutsThePlayerBackWhereTheySaved()
    {
        WorldSession session = Session();
        OverworldScene scene = session.Restore(new SaveFile
        {
            Map = MapA,
            Pos = new GridPos(6, 1),
            Facing = Facing.Up,
            Flags = new Dictionary<string, int> { ["seen"] = 1 },
        })!;

        Assert.Equal(new GridPos(6, 1), scene.PlayerPos);
        Assert.Equal(Facing.Up, scene.PlayerFacing);
        Assert.True(session.Flags.GetBool("seen"));
    }

    /// <summary>Restoring replaces flags rather than merging, or deleted flags would resurrect.</summary>
    [Fact]
    public void Restore_ReplacesExistingFlagsRatherThanMerging()
    {
        WorldSession session = Session();
        session.Flags.SetBool("stale", true);

        session.Restore(new SaveFile { Map = MapA, Flags = new Dictionary<string, int> { ["fresh"] = 1 } });

        Assert.False(session.Flags.GetBool("stale"));
        Assert.True(session.Flags.GetBool("fresh"));
    }

    [Fact]
    public void Restore_AMissingMap_ReturnsNull() =>
        Assert.Null(Session().Restore(new SaveFile { Map = EntityId.Parse("map:absent") }));

    [Fact]
    public void Restore_ASaveWithNoMap_ReturnsNull() =>
        Assert.Null(Session().Restore(new SaveFile()));

    [Fact]
    public void Restore_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => Session().Restore(null!));

    /// <summary>Save then restore lands the player exactly where they were.</summary>
    [Fact]
    public void SaveThenRestore_IsALosslessRoundTripForSessionState()
    {
        WorldSession first = Session();
        first.Enter(MapA, new GridPos(4, 4), Facing.Left);
        first.Flags.SetInt("progress", 9);
        SaveFile save = first.ToSave("h");

        WorldSession second = Session();
        OverworldScene scene = second.Restore(save)!;

        Assert.Equal(first.Position, second.Position);
        Assert.Equal(first.Facing, second.Facing);
        Assert.Equal(first.CurrentMap, second.CurrentMap);
        Assert.Equal(9, scene.Flags.GetInt("progress"));
    }
}
