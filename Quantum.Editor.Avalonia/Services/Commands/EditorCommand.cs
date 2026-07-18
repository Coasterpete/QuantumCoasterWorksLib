namespace Quantum.Editor.Avalonia.Services.Commands;

public sealed class EditorCommand : IEditorCommand
{
    private readonly Action<object?> execute;
    private readonly Func<object?, bool> canExecute;

    public EditorCommand(
        string id,
        Action<object?> execute,
        Func<object?, bool>? canExecute = null)
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Command ID is required.", nameof(id))
            : id;
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute ?? (_ => true);
    }

    public string Id { get; }

    public bool CanExecute(object? parameter) => canExecute(parameter);

    public void Execute(object? parameter) => execute(parameter);
}
