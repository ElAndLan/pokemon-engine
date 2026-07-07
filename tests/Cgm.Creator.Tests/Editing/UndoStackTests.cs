using Cgm.Creator.Editing;

namespace Cgm.Creator.Tests.Editing;

public sealed class UndoStackTests
{
    private sealed class Box { public int Value; }

    private static SnapshotCommand<int> Set(Box box, int to) =>
        new(box.Value, to, v => box.Value = v);

    [Fact]
    public void Push_AppliesAndTracksState()
    {
        var box = new Box();
        var stack = new UndoStack();

        stack.Push(Set(box, 5));
        Assert.Equal(5, box.Value);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.True(stack.IsDirty);
    }

    [Fact]
    public void UndoRedo_RestoreValues()
    {
        var box = new Box();
        var stack = new UndoStack();
        stack.Push(Set(box, 5));
        stack.Push(Set(box, 9));

        stack.Undo();
        Assert.Equal(5, box.Value);
        stack.Undo();
        Assert.Equal(0, box.Value);
        Assert.False(stack.CanUndo);

        stack.Redo();
        Assert.Equal(5, box.Value);
        stack.Redo();
        Assert.Equal(9, box.Value);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Push_DropsRedoTail()
    {
        var box = new Box();
        var stack = new UndoStack();
        stack.Push(Set(box, 1));
        stack.Push(Set(box, 2));
        stack.Undo(); // back to 1, redo available

        stack.Push(Set(box, 7)); // new branch clears redo
        Assert.False(stack.CanRedo);
        Assert.Equal(7, box.Value);
    }

    [Fact]
    public void MarkSaved_ClearsDirtyUntilNextEdit()
    {
        var box = new Box();
        var stack = new UndoStack();
        stack.Push(Set(box, 5));
        stack.MarkSaved();
        Assert.False(stack.IsDirty);

        stack.Undo();
        Assert.True(stack.IsDirty); // moved away from saved point
        stack.Redo();
        Assert.False(stack.IsDirty); // back at saved point
    }

    [Fact]
    public void RespectsMaxDepth()
    {
        var box = new Box();
        var stack = new UndoStack { MaxDepth = 3 };
        for (int i = 1; i <= 5; i++)
            stack.Push(Set(box, i));

        // Only the last 3 edits are undoable; older history was trimmed.
        int undos = 0;
        while (stack.CanUndo) { stack.Undo(); undos++; }
        Assert.Equal(3, undos);
    }

    [Fact]
    public void Changed_FiresOnMutations()
    {
        var box = new Box();
        var stack = new UndoStack();
        int fired = 0;
        stack.Changed += () => fired++;

        stack.Push(Set(box, 1));
        stack.Undo();
        stack.Redo();
        Assert.Equal(3, fired);
    }
}
