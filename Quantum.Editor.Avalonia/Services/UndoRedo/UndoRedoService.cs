namespace Quantum.Editor.Avalonia.Services.UndoRedo;

public sealed class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IUndoableEditorOperation> undoStack = new();
    private readonly Stack<IUndoableEditorOperation> redoStack = new();
    private Func<bool>? delegatedCanUndo;
    private Func<bool>? delegatedCanRedo;
    private Func<string?>? delegatedUndoDescription;
    private Func<string?>? delegatedRedoDescription;
    private Func<bool>? delegatedUndo;
    private Func<bool>? delegatedRedo;
    private Action? delegatedClear;

    public event EventHandler? StateChanged;

    public bool CanUndo => delegatedCanUndo?.Invoke() ?? undoStack.Count != 0;

    public bool CanRedo => delegatedCanRedo?.Invoke() ?? redoStack.Count != 0;

    public string? UndoDescription => delegatedUndoDescription is not null
        ? delegatedUndoDescription()
        : CanUndo ? undoStack.Peek().Description : null;

    public string? RedoDescription => delegatedRedoDescription is not null
        ? delegatedRedoDescription()
        : CanRedo ? redoStack.Peek().Description : null;

    internal void DelegateHistory(
        Func<bool> canUndo,
        Func<bool> canRedo,
        Func<string?> undoDescription,
        Func<string?> redoDescription,
        Func<bool> undo,
        Func<bool> redo,
        Action clear)
    {
        delegatedCanUndo = canUndo ?? throw new ArgumentNullException(nameof(canUndo));
        delegatedCanRedo = canRedo ?? throw new ArgumentNullException(nameof(canRedo));
        delegatedUndoDescription = undoDescription ??
            throw new ArgumentNullException(nameof(undoDescription));
        delegatedRedoDescription = redoDescription ??
            throw new ArgumentNullException(nameof(redoDescription));
        delegatedUndo = undo ?? throw new ArgumentNullException(nameof(undo));
        delegatedRedo = redo ?? throw new ArgumentNullException(nameof(redo));
        delegatedClear = clear ?? throw new ArgumentNullException(nameof(clear));
        undoStack.Clear();
        redoStack.Clear();
        NotifyStateChanged();
    }

    public void Execute(IUndoableEditorOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (delegatedUndo is not null)
        {
            throw new InvalidOperationException(
                "This undo service is a view of session-owned authoring history.");
        }

        operation.Execute();
        undoStack.Push(operation);
        redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Undo()
    {
        if (delegatedUndo is not null)
        {
            bool delegatedResult = delegatedUndo();
            if (delegatedResult)
            {
                NotifyStateChanged();
            }

            return delegatedResult;
        }

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
        if (delegatedRedo is not null)
        {
            bool delegatedResult = delegatedRedo();
            if (delegatedResult)
            {
                NotifyStateChanged();
            }

            return delegatedResult;
        }

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
        if (delegatedClear is not null)
        {
            delegatedClear();
            NotifyStateChanged();
            return;
        }

        undoStack.Clear();
        redoStack.Clear();
        NotifyStateChanged();
    }

    internal void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
