using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.ViewModels;

public sealed class EntityCrudTests
{
    private static MainWindowViewModel OnFixture(out string dir)
    {
        dir = TestRepo.CopySampleToTemp("fixture-min");
        var vm = new MainWindowViewModel(new FakeDialogService());
        vm.OpenProject(dir);
        return vm;
    }

    [Fact]
    public void CreateEntity_AddsOpensAndPersists()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.CreateEntity(EntityCategory.Item, "great_ball");
            EntityId id = EntityId.Parse("item:great_ball");

            Assert.True(vm.Session!.Contains(id));
            Assert.IsType<ItemDocument>(vm.ActiveDocument);

            vm.SaveAll();
            Assert.True(File.Exists(Path.Combine(dir, "data", "item", "great_ball.json")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void CreateEntity_RejectsBadSlugAndDuplicate()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.CreateEntity(EntityCategory.Item, "Bad Slug!");
            Assert.Contains("Invalid slug", vm.StatusText);

            vm.CreateEntity(EntityCategory.Item, "potion"); // already exists in fixture
            Assert.Contains("already exists", vm.StatusText);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void CreateMove_DefaultsToAnExistingType()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.CreateEntity(EntityCategory.Move, "scratch");
            Move created = vm.Session!.Find<Move>(EntityId.Parse("move:scratch"))!;
            Assert.Contains(created.Type, vm.Session.All<TypeDef>().Select(t => t.Id));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void CreatePhase15Entities_OpensAbilityAndSpeciesEditors()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.CreateEntity(EntityCategory.Ability, "rain_call");
            Assert.IsType<AbilityDocument>(vm.ActiveDocument);

            vm.CreateEntity(EntityCategory.Species, "raincub");
            Assert.IsType<SpeciesDocument>(vm.ActiveDocument);
            Species species = vm.Session!.Find<Species>(EntityId.Parse("species:raincub"))!;
            Assert.NotEmpty(species.Types);
            Assert.Equal(new Stats(45, 45, 45, 45, 45, 45), species.BaseStats);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Duplicate_CopiesFieldsUnderNewId()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.DuplicateEntity(EntityId.Parse("move:ember"), "ember_plus");
            Move copy = vm.Session!.Find<Move>(EntityId.Parse("move:ember_plus"))!;
            Move original = vm.Session.Find<Move>(EntityId.Parse("move:ember"))!;
            Assert.Equal(original.Power, copy.Power);
            Assert.Equal(original.Type, copy.Type);
            Assert.Equal(EntityId.Parse("move:ember_plus"), copy.Id);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Delete_RefusesWhenReferenced()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            // type:grass is referenced by species:leafcub and move:vine_whip.
            vm.DeleteEntity(EntityId.Parse("type:grass"));
            Assert.True(vm.Session!.Contains(EntityId.Parse("type:grass")));
            Assert.Contains("Can't delete", vm.StatusText);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Delete_SucceedsWhenUnreferenced()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            // item:potion isn't referenced by anything in the fixture.
            vm.DeleteEntity(EntityId.Parse("item:potion"));
            Assert.False(vm.Session!.Contains(EntityId.Parse("item:potion")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void FindReferencers_ReportsReferencingEntities()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            var refs = vm.FindReferencers(EntityId.Parse("type:grass"));
            Assert.Contains(EntityId.Parse("species:leafcub"), refs);
            Assert.Contains(EntityId.Parse("move:vine_whip"), refs);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void NavigateToIssue_OpensReferencedEntity()
    {
        MainWindowViewModel vm = OnFixture(out string dir);
        try
        {
            vm.OpenDocument(EntityId.Parse("move:ember"));
            ((MoveDocument)vm.ActiveDocument!).Pp = 0; // creates a "move" issue on move:ember
            var issue = vm.Issues.First(i => i.RuleId == "move");

            vm.Documents.Clear();
            vm.NavigateToIssue(issue);
            Assert.IsType<MoveDocument>(vm.ActiveDocument);
            Assert.Equal(EntityId.Parse("move:ember"), vm.ActiveDocument!.Id);
        }
        finally { Directory.Delete(dir, true); }
    }
}
