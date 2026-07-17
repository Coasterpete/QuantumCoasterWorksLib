namespace Quantum.Editor.Avalonia.Services.Commands;

public interface ICommandService
{
    void Register(IEditorCommand command);

    bool TryGet(string commandId, out IEditorCommand? command);
}
