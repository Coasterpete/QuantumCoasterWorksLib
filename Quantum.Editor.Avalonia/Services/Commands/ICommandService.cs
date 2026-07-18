namespace Quantum.Editor.Avalonia.Services.Commands;

public interface ICommandService
{
    void Register(IEditorCommand command);

    bool TryGet(string commandId, out IEditorCommand? command);

    bool CanExecute(string commandId, object? parameter = null);

    bool Execute(string commandId, object? parameter = null);
}
