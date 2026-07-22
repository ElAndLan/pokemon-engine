namespace Cgm.Creator.Editing;

/// <summary>A reversible edit (CREATOR_APP_SPEC §4.1). For small entities this is a whole-record
/// snapshot swap (see <see cref="SnapshotCommand{T}"/>).</summary>
public interface IEditCommand
{
    void Do();
    void Undo();
}

/// <summary>
/// Per-document undo/redo history. Pushing an edit applies it and clears any redo tail; the stack
/// is capped at <see cref="MaxDepth"/>. <see cref="IsDirty"/> compares the current position to the
/// last <see cref="MarkSaved"/> point. Pure and headless — UI subscribes via <see cref="Changed"/>.
/// </summary>
public sealed class UndoStack
{
    private readonly List<IEditCommand> _commands = [];
    private int _index;      // count of currently-applied commands
    private int _savedIndex; // _index at the last save

    public int MaxDepth { get; init; } = 100;

    public bool CanUndo => _index > 0;
    public bool CanRedo => _index < _commands.Count;
    public bool IsDirty => _index != _savedIndex;

    public event Action? Changed;

    private List<IEditCommand>? _group;

    public void Push(IEditCommand command)
    {
        command.Do();

        if (_group is not null)
        {
            _group.Add(command); // grouped members join the stack as one entry at EndGroup
            return;
        }

        AddApplied(command);
        Changed?.Invoke();
    }

    /// <summary>Starts collecting pushes into one composite step (CREATOR_APP_SPEC §10.9): each
    /// still executes immediately, but one Undo reverses them all. Groups do not nest.</summary>
    public void BeginGroup() => _group = [];

    public void EndGroup()
    {
        List<IEditCommand>? group = _group;
        _group = null;
        if (group is { Count: > 0 })
        {
            AddApplied(new CompositeCommand(group));
            Changed?.Invoke();
        }
    }

    /// <summary>Adds an already-executed command to the history (shared by Push and EndGroup).</summary>
    private void AddApplied(IEditCommand command)
    {
        if (_index < _commands.Count)
            _commands.RemoveRange(_index, _commands.Count - _index); // drop redo tail

        _commands.Add(command);
        _index++;

        if (_commands.Count > MaxDepth)
        {
            int drop = _commands.Count - MaxDepth;
            _commands.RemoveRange(0, drop);
            _index -= drop;
            _savedIndex -= drop; // if this goes negative the saved point was trimmed → stays dirty
        }
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _commands[--_index].Undo();
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _commands[_index++].Do();
        Changed?.Invoke();
    }

    public void MarkSaved()
    {
        _savedIndex = _index;
        Changed?.Invoke();
    }
}

/// <summary>N already-grouped edits as one undo step: Do replays in order, Undo reverses in
/// reverse order (CREATOR_APP_SPEC §10.9).</summary>
public sealed class CompositeCommand(IReadOnlyList<IEditCommand> members) : IEditCommand
{
    public void Do()
    {
        foreach (IEditCommand member in members)
            member.Do();
    }

    public void Undo()
    {
        for (int i = members.Count - 1; i >= 0; i--)
            members[i].Undo();
    }
}

/// <summary>Swaps a value between a before/after snapshot via an apply callback. For immutable
/// entity records, "snapshot" is just holding the two references.</summary>
public sealed class SnapshotCommand<T> : IEditCommand
{
    private readonly Action<T> _apply;
    private readonly T _before;
    private readonly T _after;

    public SnapshotCommand(T before, T after, Action<T> apply)
    {
        _before = before;
        _after = after;
        _apply = apply;
    }

    public void Do() => _apply(_after);
    public void Undo() => _apply(_before);
}
