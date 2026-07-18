namespace Quantum.Editor.Avalonia.Services.UndoRedo;

public interface IUndoRedoService
{
    event EventHandler? StateChanged;

    bool CanUndo { get; }

    bool CanRedo { get; }

    string? UndoDescription { get; }

    string? RedoDescription { get; }

    void Execute(IUndoableEditorOperation operation);

    bool Undo();

    bool Redo();

    void Clear();
}
