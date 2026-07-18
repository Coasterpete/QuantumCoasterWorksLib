namespace Quantum.Editor.Avalonia.Services.UndoRedo;

public interface IUndoableEditorOperation
{
    string Description { get; }

    void Execute();

    void Undo();
}
