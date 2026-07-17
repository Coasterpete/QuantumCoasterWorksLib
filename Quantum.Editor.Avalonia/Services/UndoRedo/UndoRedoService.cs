namespace Quantum.Editor.Avalonia.Services.UndoRedo;

public sealed class UndoRedoService : IUndoRedoService
{
    public bool CanUndo => false;

    public bool CanRedo => false;

    public void Clear()
    {
    }
}
