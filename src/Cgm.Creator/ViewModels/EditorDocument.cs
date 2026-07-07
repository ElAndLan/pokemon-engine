using CommunityToolkit.Mvvm.ComponentModel;
using Cgm.Core.Model;
using Cgm.Creator.Editing;

namespace Cgm.Creator.ViewModels;

/// <summary>Base for an open editor tab (CREATOR_APP_SPEC §4): identity, undo history, dirty state.</summary>
public abstract class EditorDocument : ObservableObject
{
    protected EditorDocument(ProjectSession session, EntityId? id)
    {
        Session = session;
        Id = id;
        Undo.Changed += () =>
        {
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(TabTitle));
        };
    }

    public ProjectSession Session { get; }

    /// <summary>The edited entity, or null for app-level documents (e.g. the type chart).</summary>
    public EntityId? Id { get; }
    public UndoStack Undo { get; } = new();

    public abstract string Title { get; }
    public bool IsDirty => Undo.IsDirty;
    public string TabTitle => IsDirty ? Title + " •" : Title;

    public void MarkSaved() => Undo.MarkSaved();
}

/// <summary>
/// A single-entity editor over an immutable record <typeparamref name="T"/>. Field setters build a
/// new record and commit it through the undo stack; undo/redo swaps the record back and refreshes
/// every bound field. This is the pattern every entity editor copies (CREATOR_APP_SPEC §4).
/// </summary>
public abstract class EntityEditorDocument<T> : EditorDocument where T : class, IEntity
{
    protected EntityEditorDocument(ProjectSession session, T model) : base(session, model.Id) =>
        Model = model;

    protected T Model { get; private set; }

    public override string Title => Model.Id.Slug;

    /// <summary>Apply an edited record as one undoable step.</summary>
    protected void Edit(T next)
    {
        T before = Model;
        Undo.Push(new SnapshotCommand<T>(before, next, m =>
        {
            Model = m;
            Session.Put(m);
            OnPropertyChanged(string.Empty); // refresh all bound fields (covers undo/redo)
        }));
    }
}
