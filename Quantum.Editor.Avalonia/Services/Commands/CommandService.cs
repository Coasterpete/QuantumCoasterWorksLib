using System.Collections.Generic;

namespace Quantum.Editor.Avalonia.Services.Commands;

public sealed class CommandService : ICommandService
{
    private readonly Dictionary<string, IEditorCommand> commands = new();

    public void Register(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        commands[command.Id] = command;
    }

    public bool TryGet(string commandId, out IEditorCommand? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        return commands.TryGetValue(commandId, out command);
    }

    public bool CanExecute(string commandId, object? parameter = null)
    {
        return TryGet(commandId, out IEditorCommand? command) && command!.CanExecute(parameter);
    }

    public bool Execute(string commandId, object? parameter = null)
    {
        if (!TryGet(commandId, out IEditorCommand? command) || !command!.CanExecute(parameter))
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }
}
