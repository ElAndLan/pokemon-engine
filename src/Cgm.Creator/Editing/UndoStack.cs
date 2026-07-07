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

    public void Push(IEditCommand command)
    {
        command.Do();

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

        Changed?.Invoke();
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
