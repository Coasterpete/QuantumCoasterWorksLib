namespace Quantum.Editor.Avalonia.Services.UndoRedo;

public interface IUndoRedoService
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    void Clear();
}
