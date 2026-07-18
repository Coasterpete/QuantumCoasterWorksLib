namespace Quantum.Editor.Avalonia.Services.UndoRedo;

public sealed class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IUndoableEditorOperation> undoStack = new();
    private readonly Stack<IUndoableEditorOperation> redoStack = new();

    public event EventHandler? StateChanged;

    public bool CanUndo => undoStack.Count != 0;

    public bool CanRedo => redoStack.Count != 0;

    public string? UndoDescription => CanUndo ? undoStack.Peek().Description : null;

    public string? RedoDescription => CanRedo ? redoStack.Peek().Description : null;

    public void Execute(IUndoableEditorOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        operation.Execute();
        undoStack.Push(operation);
        redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        IUndoableEditorOperation operation = undoStack.Peek();
        operation.Undo();
        undoStack.Pop();
        redoStack.Push(operation);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        IUndoableEditorOperation operation = redoStack.Peek();
        operation.Execute();
        redoStack.Pop();
        undoStack.Push(operation);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
