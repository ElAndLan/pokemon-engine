using Cgm.Core.Model;
using Cgm.Creator.Editing;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests;

/// <summary>CREATOR_APP_SPEC §10.4: snapshots go to app-data, rotate at five, are offered (never
/// auto-applied) after an unclean close, and are discarded by a clean one.</summary>
public sealed class RecoverySnapshotTests : IDisposable
{
    private readonly string _dir = TestRepo.CopySampleToTemp("fixture-min");
    private readonly string _stateDir = Path.Combine(Path.GetTempPath(), "cgm-rec-" + Guid.NewGuid().ToString("N"));
    private readonly FakeDialogService _dialogs = new();

    private static readonly EntityId Leafcub = EntityId.Parse("species:leafcub");

    private RecoverySnapshots Store => new(Path.Combine(_stateDir, "recovery"));

    private MainWindowViewModel Vm() => new(_dialogs,
        new RecentProjects(_stateDir), Store);

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        if (Directory.Exists(_stateDir))
            Directory.Delete(_stateDir, recursive: true);
    }

    private void MakeDirty(MainWindowViewModel vm, int catchRate = 200)
    {
        var session = vm.Session!;
        session.Put(session.Find<Species>(Leafcub)! with { CatchRate = catchRate });
        vm.RefreshValidation(); // the edit funnel the documents drive in production
    }

    [Fact]
    public void Autosave_SnapshotsAfterInactivity_OncePerEditBurst()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        MakeDirty(vm);

        vm.AutosaveTick(DateTime.UtcNow.AddSeconds(30));
        Assert.Empty(Store.For(_dir)); // too soon

        vm.AutosaveTick(DateTime.UtcNow.AddSeconds(121));
        Assert.Single(Store.For(_dir));

        vm.AutosaveTick(DateTime.UtcNow.AddSeconds(300));
        Assert.Single(Store.For(_dir)); // no second snapshot without a new edit
    }

    [Fact]
    public void Autosave_CleanSessionNeverSnapshots()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        vm.AutosaveTick(DateTime.UtcNow.AddHours(1));
        Assert.Empty(Store.For(_dir));
    }

    [Fact]
    public void Deactivation_SnapshotsImmediately_WhileDirty()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        MakeDirty(vm);
        vm.SnapshotNow();
        Assert.Single(Store.For(_dir));
    }

    [Fact]
    public void Snapshots_RotateAtFive_NewestFirst()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        for (int i = 0; i < 7; i++)
        {
            MakeDirty(vm, catchRate: 100 + i);
            vm.SnapshotNow();
        }

        IReadOnlyList<string> kept = Store.For(_dir);
        Assert.Equal(RecoverySnapshots.Keep, kept.Count);
        Assert.True(string.CompareOrdinal(Path.GetFileName(kept[0]), Path.GetFileName(kept[^1])) > 0);
    }

    [Fact]
    public async Task CleanClose_DiscardsSnapshots()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        MakeDirty(vm);
        vm.SnapshotNow();
        Assert.Single(Store.For(_dir));

        _dialogs.UnsavedChoiceToReturn = Cgm.Creator.Services.UnsavedChoice.Save;
        Assert.True(await vm.ConfirmLoseChangesAsync());
        Assert.Empty(Store.For(_dir));
    }

    [Fact]
    public async Task Recovery_Declined_LeavesSourceAndSnapshots()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        MakeDirty(vm);
        vm.SnapshotNow(); // then "crash": no clean close

        var next = Vm();
        next.OpenProject(_dir);
        _dialogs.ConfirmToReturn = false;
        await next.OfferRecoveryAsync();

        Assert.Equal(45, next.Session!.Find<Species>(Leafcub)!.CatchRate); // untouched
        Assert.Single(Store.For(_dir)); // still offered next time
    }

    [Fact]
    public async Task Recovery_Applied_IsInMemoryAndDirty_UntilSaved()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        MakeDirty(vm);
        vm.SnapshotNow(); // then "crash"

        var next = Vm();
        next.OpenProject(_dir);
        _dialogs.ConfirmToReturn = true;
        await next.OfferRecoveryAsync();

        Assert.Equal(200, next.Session!.Find<Species>(Leafcub)!.CatchRate); // recovered in memory
        Assert.True(next.Session.IsDirty);
        Assert.Equal(45, ProjectSession.Open(_dir).Find<Species>(Leafcub)!.CatchRate); // disk untouched

        next.SaveAll();
        Assert.Equal(200, ProjectSession.Open(_dir).Find<Species>(Leafcub)!.CatchRate);
    }

    [Fact]
    public void TornSnapshot_IsNeverOffered()
    {
        var vm = Vm();
        vm.OpenProject(_dir);
        MakeDirty(vm);
        vm.SnapshotNow();

        // A .tmp directory (crash mid-write) must be invisible.
        string projectDir = Path.GetDirectoryName(Store.For(_dir)[0])!;
        Directory.CreateDirectory(Path.Combine(projectDir, "99999999-999999-999.tmp"));
        Assert.Single(Store.For(_dir));
    }
}
