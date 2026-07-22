using Cgm.Core.Model;
using Cgm.Creator.Editing;
using Cgm.Creator.Services;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests;

/// <summary>CREATOR_APP_SPEC §10.5 (unsaved guard) and §10.6 (recent list), headless through the
/// shell view-model with a fake dialog service.</summary>
public sealed class LifecycleTests : IDisposable
{
    private readonly string _dir = TestRepo.CopySampleToTemp("fixture-min");
    private readonly string _recentDir = Path.Combine(Path.GetTempPath(), "cgm-recent-" + Guid.NewGuid().ToString("N"));
    private readonly FakeDialogService _dialogs = new();

    private MainWindowViewModel Vm() => new(_dialogs, new RecentProjects(_recentDir));

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        if (Directory.Exists(_recentDir))
            Directory.Delete(_recentDir, recursive: true);
    }

    private static void Dirty(MainWindowViewModel vm)
    {
        var session = vm.Session!;
        Species leafcub = session.Find<Species>(EntityId.Parse("species:leafcub"))!;
        session.Put(leafcub with { CatchRate = 200 });
    }

    // --- Recent list (§10.6) ---

    [Fact]
    public void Open_RecordsTheFolderInRecent_NewestFirst_Persisted()
    {
        var vm = Vm();
        vm.OpenProject(_dir);

        Assert.Equal(Path.GetFullPath(_dir), Assert.Single(vm.Recent));
        Assert.Equal(Path.GetFullPath(_dir), Assert.Single(new RecentProjects(_recentDir).Folders));
    }

    [Fact]
    public void Recent_Deduplicates_Caps_AndOrdersNewestFirst()
    {
        var store = new RecentProjects(_recentDir);
        for (int i = 0; i < 12; i++)
            store.Add(Path.Combine(Path.GetTempPath(), $"proj{i}"));
        store.Add(Path.Combine(Path.GetTempPath(), "proj5")); // re-open moves to front, no duplicate

        Assert.Equal(RecentProjects.Capacity, store.Folders.Count);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "proj5"), store.Folders[0]);
        Assert.Single(store.Folders, f => f.EndsWith("proj5"));
    }

    [Fact]
    public void Recent_MalformedFileReadsAsEmpty()
    {
        Directory.CreateDirectory(_recentDir);
        File.WriteAllText(Path.Combine(_recentDir, "recent.json"), "{broken");
        Assert.Empty(new RecentProjects(_recentDir).Folders);
    }

    [Fact]
    public async Task OpenRecent_MissingFolder_OffersRemoval()
    {
        var vm = Vm();
        string gone = Path.Combine(Path.GetTempPath(), "cgm-gone-" + Guid.NewGuid().ToString("N"));
        new RecentProjects(_recentDir).Add(gone);

        _dialogs.ConfirmToReturn = true;
        await vm.OpenRecentCommand.ExecuteAsync(gone);

        Assert.Empty(new RecentProjects(_recentDir).Folders);
        Assert.Null(vm.Session);
    }

    // --- Unsaved guard (§10.5) ---

    [Fact]
    public async Task Guard_CleanSession_ProceedsWithoutPrompting()
    {
        var vm = Vm();
        vm.OpenProject(_dir);

        Assert.True(await vm.ConfirmLoseChangesAsync());
        Assert.Equal(0, _dialogs.UnsavedPrompts);
    }

    [Fact]
    public async Task Guard_Cancel_Refuses()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        Dirty(vm);

        _dialogs.UnsavedChoiceToReturn = UnsavedChoice.Cancel;
        Assert.False(await vm.ConfirmLoseChangesAsync());
        Assert.True(vm.Session!.IsDirty); // nothing changed
    }

    [Fact]
    public async Task Guard_Discard_Proceeds_WithoutSaving()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        Dirty(vm);

        _dialogs.UnsavedChoiceToReturn = UnsavedChoice.Discard;
        Assert.True(await vm.ConfirmLoseChangesAsync());
        Assert.Equal(45, ProjectSession.Open(_dir).Find<Species>(EntityId.Parse("species:leafcub"))!.CatchRate);
    }

    [Fact]
    public async Task Guard_Save_SavesAndProceeds()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        Dirty(vm);

        _dialogs.UnsavedChoiceToReturn = UnsavedChoice.Save;
        Assert.True(await vm.ConfirmLoseChangesAsync());
        Assert.False(vm.Session!.IsDirty);
        Assert.Equal(200, ProjectSession.Open(_dir).Find<Species>(EntityId.Parse("species:leafcub"))!.CatchRate);
    }

    [Fact]
    public async Task Guard_SaveThatFails_RefusesToClose()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        Dirty(vm);

        _dialogs.UnsavedChoiceToReturn = UnsavedChoice.Save;
        using (File.Open(Path.Combine(_dir, "data", "species", "leafcub.json"),
            FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(await vm.ConfirmLoseChangesAsync()); // edits must not be lost
        }
        Assert.True(vm.Session!.IsDirty);
    }
}
