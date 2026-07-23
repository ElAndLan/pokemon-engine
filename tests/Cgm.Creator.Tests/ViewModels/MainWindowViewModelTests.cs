using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    private static MainWindowViewModel OnFixture(out string dir, FakeDialogService? dialogs = null)
    {
        dir = TestRepo.CopySampleToTemp("fixture-min");
        var vm = TestRepo.NewVm(dialogs);
        vm.OpenProject(dir);
        return vm;
    }

    [Fact]
    public void OpenProject_PopulatesNav_AndValidatesClean()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            Assert.True(vm.HasProject);
            Assert.Equal("Fixture Min", vm.ProjectName);
            Assert.NotEmpty(vm.Nav);
            Assert.Contains(vm.Nav, c => c.Name == nameof(EntityCategory.Move));
            Assert.Equal(0, vm.ErrorCount);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void OpenDocument_Move_OpensOneTab_AndRefocusesOnReopen()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.OpenDocument(EntityId.Parse("move:ember"));
            Assert.Single(vm.Documents);
            Assert.IsType<MoveDocument>(vm.ActiveDocument);

            vm.OpenDocument(EntityId.Parse("move:ember")); // same entity
            Assert.Single(vm.Documents);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void OpenDocument_NoEditorForCategory_ReportsStatus()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.OpenDocument(EntityId.Parse("encounter:test_room_grass")); // no encounter editor yet
            Assert.Empty(vm.Documents);
            Assert.Contains("No editor", vm.StatusText);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EditThenSave_PersistsAndClearsDirty()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.OpenDocument(EntityId.Parse("move:ember"));
            var doc = (MoveDocument)vm.ActiveDocument!;
            doc.Power = 60;
            Assert.True(doc.IsDirty);

            vm.SaveAll();
            Assert.False(doc.IsDirty);

            var reopened = TestRepo.NewVm();
            reopened.OpenProject(dir);
            reopened.OpenDocument(EntityId.Parse("move:ember"));
            Assert.Equal(60, ((MoveDocument)reopened.ActiveDocument!).Power);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Editing_UpdatesValidationStripLive()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.OpenDocument(EntityId.Parse("move:ember"));
            var doc = (MoveDocument)vm.ActiveDocument!;
            doc.Pp = 0; // invalid — the move rule should fire
            Assert.Contains(vm.Issues, i => i.RuleId == "move");
            Assert.True(vm.ErrorCount > 0);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task OpenCommand_UsesDialogFolder()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            var vm = TestRepo.NewVm(new FakeDialogService { FolderToReturn = dir });
            await vm.OpenCommand.ExecuteAsync(null);
            Assert.True(vm.HasProject);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void NewProject_CreatesFolderAndOpens()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cgm-new-" + Guid.NewGuid().ToString("N"));
        try
        {
            var vm = TestRepo.NewVm();
            vm.NewProject(new Cgm.Creator.Services.NewProjectRequest(dir, "Fresh Start", 16));
            Assert.True(vm.HasProject);
            Assert.Equal("Fresh Start", vm.ProjectName);
            Assert.True(File.Exists(Path.Combine(dir, "project.cgmproj")));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void OpenProject_BadFolder_SetsStatus_NoCrash()
    {
        var vm = TestRepo.NewVm();
        vm.OpenProject(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N")));
        Assert.False(vm.HasProject);
        Assert.Contains("Could not open", vm.StatusText);
    }
}
