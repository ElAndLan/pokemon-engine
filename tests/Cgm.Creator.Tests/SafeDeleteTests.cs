using Cgm.Core.Model;
using Cgm.Creator.Editing;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests;

/// <summary>CREATOR_APP_SPEC §10.7/§10.9: safe delete with explicit replacement is one grouped,
/// reversible step; §10.8 field navigation; §10.9 undo grouping semantics.</summary>
public sealed class SafeDeleteTests : IDisposable
{
    private readonly string _dir = TestRepo.CopySampleToTemp("fixture-min");
    private readonly string _stateDir = Path.Combine(Path.GetTempPath(), "cgm-sd-" + Guid.NewGuid().ToString("N"));
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _vm;

    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Leafcub = EntityId.Parse("species:leafcub");
    private static readonly EntityId VineWhip = EntityId.Parse("move:vine_whip");

    public SafeDeleteTests()
    {
        _vm = new MainWindowViewModel(_dialogs, new RecentProjects(_stateDir),
            new RecoverySnapshots(Path.Combine(_stateDir, "recovery")));
        _vm.OpenProject(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        if (Directory.Exists(_stateDir))
            Directory.Delete(_stateDir, recursive: true);
    }

    [Fact]
    public async Task ReplacementDelete_RewritesEveryReference_AndDeletes()
    {
        _dialogs.EntityToReturn = Fire;
        Assert.True(await _vm.DeleteEntityAsync(Grass));

        Assert.False(_vm.Session!.Contains(Grass));
        Assert.Contains(Fire, _vm.Session.Find<Species>(Leafcub)!.Types);
        Assert.Equal(Fire, _vm.Session.Find<Move>(VineWhip)!.Type);
        Assert.Empty(_vm.FindUsages(Grass));
    }

    [Fact]
    public async Task ReplacementDelete_IsOneUndoStep_AndRedoable()
    {
        var originalTypes = _vm.Session!.Find<Species>(Leafcub)!.Types;
        _dialogs.EntityToReturn = Fire;
        await _vm.DeleteEntityAsync(Grass);

        _vm.UndoCommand.Execute(null); // one undo reverses the rewrites AND the delete
        Assert.True(_vm.Session.Contains(Grass));
        Assert.Equal(originalTypes, _vm.Session.Find<Species>(Leafcub)!.Types);
        Assert.Equal(Grass, _vm.Session.Find<Move>(VineWhip)!.Type);

        _vm.RedoCommand.Execute(null);
        Assert.False(_vm.Session.Contains(Grass));
        Assert.Equal(Fire, _vm.Session.Find<Move>(VineWhip)!.Type);
    }

    [Fact]
    public async Task ReplacementCandidates_ExcludeTheDeletedEntity_AndOtherCategories()
    {
        _dialogs.EntityToReturn = null;
        await _vm.DeleteEntityAsync(Grass);

        Assert.NotNull(_dialogs.LastPickCandidates);
        Assert.DoesNotContain(_dialogs.LastPickCandidates!, c => c.Id == Grass);
        Assert.All(_dialogs.LastPickCandidates!, c => Assert.Equal(EntityCategory.Type, c.Id.Category));
    }

    [Fact]
    public async Task ReplacementWithSelf_OrMissing_Refuses()
    {
        _dialogs.EntityToReturn = Grass; // picking the entity being deleted
        Assert.False(await _vm.DeleteEntityAsync(Grass));
        Assert.True(_vm.Session!.Contains(Grass));

        _dialogs.EntityToReturn = EntityId.Parse("type:nonexistent");
        Assert.False(await _vm.DeleteEntityAsync(Grass));
        Assert.True(_vm.Session.Contains(Grass));
    }

    [Fact]
    public void NavigateToIssue_FocusesTheNamedField()
    {
        // Break a reference so validation produces a field-carrying issue.
        var session = _vm.Session!;
        session.Put(session.Find<Move>(VineWhip)! with { Type = EntityId.Parse("type:gone") });
        _vm.RefreshValidation();

        var issue = _vm.Issues.Single(i => i.EntityId == VineWhip && i.RuleId == "broken-reference");
        Assert.Equal("type", issue.Field);

        _vm.NavigateToIssue(issue);
        Assert.Equal(VineWhip, _vm.ActiveDocument!.Id);
        Assert.Equal("type", _vm.ActiveDocument.FocusedField);
    }

    [Fact]
    public void UndoGrouping_NEditsAreOneStep()
    {
        var stack = new UndoStack();
        int value = 0;

        stack.BeginGroup();
        stack.Push(new SnapshotCommand<int>(0, 1, v => value = v));
        stack.Push(new SnapshotCommand<int>(1, 2, v => value = v));
        stack.Push(new SnapshotCommand<int>(2, 3, v => value = v));
        stack.EndGroup();

        Assert.Equal(3, value);
        stack.Undo();
        Assert.Equal(0, value); // all three reversed by one undo
        Assert.False(stack.CanUndo);
        stack.Redo();
        Assert.Equal(3, value);
    }

    [Fact]
    public void UndoGrouping_EmptyGroupAddsNothing()
    {
        var stack = new UndoStack();
        stack.BeginGroup();
        stack.EndGroup();
        Assert.False(stack.CanUndo);
        Assert.False(stack.IsDirty);
    }

    [Fact]
    public void ReferencePicker_FiltersByNameAndSlug()
    {
        var picker = new ReferencePickerViewModel(
        [
            (EntityId.Parse("move:vine_whip"), "Vine Whip"),
            (EntityId.Parse("move:ember"), "Ember"),
            (EntityId.Parse("move:tackle"), "Tackle"),
        ]);

        Assert.Equal(3, picker.Choices.Count);

        picker.SearchText = "vine";
        Assert.Equal("Vine Whip", Assert.Single(picker.Choices).Name);

        picker.SearchText = "TACK"; // case-insensitive, matches slug too
        Assert.Equal("Tackle", Assert.Single(picker.Choices).Name);

        picker.Selected = picker.Choices[0];
        picker.SearchText = "zzz";
        Assert.Empty(picker.Choices);
        Assert.Null(picker.Selected); // selection can't point at a filtered-out row
    }
}
