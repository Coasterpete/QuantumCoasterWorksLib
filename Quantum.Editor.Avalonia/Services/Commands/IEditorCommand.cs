namespace Quantum.Editor.Avalonia.Services.Commands;

public interface IEditorCommand
{
    string Id { get; }

    bool CanExecute(object? parameter);

    void Execute(object? parameter);
}
