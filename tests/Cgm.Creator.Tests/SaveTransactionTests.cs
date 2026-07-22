using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Creator.Editing;

namespace Cgm.Creator.Tests;

/// <summary>CREATOR_APP_SPEC §10.2: the project on disk is only ever the pre-save state or the
/// post-save state — never a mix, no matter where the save fails.</summary>
public sealed class SaveTransactionTests : IDisposable
{
    private readonly string _dir = TestRepo.CopySampleToTemp("fixture-min");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static readonly EntityId Leafcub = EntityId.Parse("species:leafcub");
    private static readonly EntityId Potion = EntityId.Parse("item:potion");

    private string PathOf(string relPath) => Path.Combine(_dir, relPath);

    [Fact]
    public void Commit_WritesReplacesAndDeletes_AndCleansWorkDir()
    {
        SaveTransaction.Run(_dir,
        [
            ("data/species/leafcub.json", "{\"edited\":1}"),
            ("data/item/potion.json", null),
            (Path.Combine("data", "type", "brand_new.json"), "{\"new\":1}"),
        ]);

        Assert.Equal("{\"edited\":1}", File.ReadAllText(PathOf("data/species/leafcub.json")));
        Assert.False(File.Exists(PathOf("data/item/potion.json")));
        Assert.Equal("{\"new\":1}", File.ReadAllText(PathOf("data/type/brand_new.json")));
        Assert.False(Directory.Exists(PathOf(".cgm/staging")));
        Assert.False(Directory.Exists(PathOf(".cgm/backup")));
        Assert.False(File.Exists(PathOf(".cgm/save-journal.json")));
    }

    [Fact]
    public void MidSwapFailure_RollsBackEveryFile()
    {
        string originalPotion = File.ReadAllText(PathOf("data/item/potion.json"));
        string originalLeafcub = File.ReadAllText(PathOf("data/species/leafcub.json"));

        // Lock leafcub.json: potion (ordinally first) swaps successfully, then leafcub's backup
        // move fails — so rollback must restore an already-swapped file, not just clean up.
        using (File.Open(PathOf("data/species/leafcub.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => SaveTransaction.Run(_dir,
            [
                ("data/item/potion.json", "{\"edited\":1}"),
                ("data/species/leafcub.json", "{\"edited\":1}"),
                ("data/type/brand_new.json", "{\"new\":1}"),
            ]));
        }

        // Byte-identical pre-save state: replaced file restored, new file removed, work dirs gone.
        Assert.Equal(originalPotion, File.ReadAllText(PathOf("data/item/potion.json")));
        Assert.Equal(originalLeafcub, File.ReadAllText(PathOf("data/species/leafcub.json")));
        Assert.False(File.Exists(PathOf("data/type/brand_new.json")));
        Assert.False(File.Exists(PathOf(".cgm/save-journal.json")));
        Assert.False(Directory.Exists(PathOf(".cgm/staging")));
        Assert.False(Directory.Exists(PathOf(".cgm/backup")));
    }

    /// <summary>A crash mid-swap leaves a journal; the next open must finish the rollback.</summary>
    [Fact]
    public void Open_CompletesRollbackFromAnUnfinishedJournal()
    {
        string original = File.ReadAllText(PathOf("data/species/leafcub.json"));

        // Simulate the crash by hand-building the on-disk state mid-swap: original moved to
        // backup, staged replacement in place, journal still present.
        Directory.CreateDirectory(PathOf(".cgm/backup/data/species"));
        File.Move(PathOf("data/species/leafcub.json"), PathOf(".cgm/backup/data/species/leafcub.json"));
        File.WriteAllText(PathOf("data/species/leafcub.json"), "{\"halfsaved\":1}");
        File.WriteAllText(PathOf(".cgm/save-journal.json"),
            """{"SchemaVersion":1,"StartedUtc":"2026-07-22T00:00:00Z","Entries":[{"Path":"data/species/leafcub.json","Action":"replace","HadOriginal":true}]}""");

        var session = ProjectSession.Open(_dir);

        Assert.True(session.RolledBackInterruptedSave);
        Assert.Equal(original, File.ReadAllText(PathOf("data/species/leafcub.json")));
        Assert.False(File.Exists(PathOf(".cgm/save-journal.json")));
        Assert.NotNull(session.Find<Species>(Leafcub)); // loaded the restored state
    }

    [Fact]
    public void Open_WithoutJournal_ReportsNoRollback()
    {
        Assert.False(ProjectSession.Open(_dir).RolledBackInterruptedSave);
    }

    [Fact]
    public void FailedSave_LeavesSessionDirtyForRetry()
    {
        var session = ProjectSession.Open(_dir);
        Species leafcub = session.Find<Species>(Leafcub)!;
        session.Put(leafcub with { CatchRate = 200 });

        using (File.Open(PathOf("data/species/leafcub.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(session.Save);
        }

        Assert.True(session.IsDirty); // the edit survives; Save can be retried
        session.Save();
        Assert.False(session.IsDirty);
        Assert.Equal(200, ProjectSession.Open(_dir).Find<Species>(Leafcub)!.CatchRate);
    }

    [Fact]
    public void EmptySave_WritesNoSaveArtifacts()
    {
        var session = ProjectSession.Open(_dir);
        session.Save();
        // The lock file (§10.3) lives in .cgm too; an empty save must add nothing beyond it.
        Assert.False(File.Exists(PathOf(".cgm/save-journal.json")));
        Assert.False(Directory.Exists(PathOf(".cgm/staging")));
        Assert.False(Directory.Exists(PathOf(".cgm/backup")));
    }

    [Fact]
    public void SettingsSave_GoesThroughTheTransaction()
    {
        var session = ProjectSession.Open(_dir);
        session.UpdateSettings(session.Settings with { Name = "Renamed" });
        session.Save();

        Assert.Equal("Renamed", ProjectFile.Load(_dir).Name);
        Assert.False(session.IsDirty);
    }
}
